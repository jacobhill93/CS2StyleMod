using Colossal.Entities;
using CS2StyleMod;
using Game.Common;
using Game.Prefabs;
using Game.Zones;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace CS2StyleMod.CustomZones.GameAdapters
{
    // WORKAROUND, not a proper adapter - kept deliberately separate from
    // ZonePrefabCloner/SpawnableBuildingZoneLinker (which are still correct
    // per the game's documented AddPrefab API) so it can be deleted the
    // moment the real root cause is found, without touching them.
    //
    // Confirmed empirically (see DECISIONS.md/TOUCHPOINTS.md): a prefab
    // cloned and registered via PrefabSystem.AddPrefab never gets a real
    // Initialize/LateInitialize pass from Game.Prefabs.PrefabInitializeSystem
    // OR from Game.Prefabs.ZoneSystem's own Created-tag processing - both
    // are separate systems, both keyed on Created+PrefabData, both never
    // fire for a runtime-added entity, with no exception anywhere. Root
    // cause not found; true regardless of registration phase, timing, or
    // zone type - see TOUCHPOINTS.md for the full history.
    //
    // Rather than hand-copy individual fields one at a time (every round of
    // testing turned up one more missing component), this calls the real
    // ComponentBase.Initialize/LateInitialize on every component attached
    // to the clone - the exact same methods PrefabInitializeSystem would
    // have called, just invoked directly instead of waiting for that
    // system to do it. That covers everything a ComponentBase sets
    // generically (ZonePropertiesData, UIObjectData + its group buffer,
    // SpawnableBuildingData, BuildingPropertyData, whatever else) without
    // needing to know every field in advance. Two things are NOT
    // ComponentBase methods and still need manual replication here: the
    // ZoneType index assignment + ZoneSystem's own private m_ZonePrefabs
    // registry (bespoke to ZoneSystem.InitializeZonePrefabs - the same
    // reflection technique DillonN/specialized-industrial-zones uses on a
    // different private field), and BuildingSpawnGroupData (set by
    // BuildingInitializeSystem, not by any ComponentBase).
    public sealed class ManualPrefabInitializationWorkaround
    {
        private static readonly FieldInfo ZonePrefabsField =
            typeof(ZoneSystem).GetField("m_ZonePrefabs", BindingFlags.NonPublic | BindingFlags.Instance);

        // No-arg overload - rebuilds the fill/edge color arrays for every
        // registered zone (reading each one's current ZonePrefab.m_Color/
        // m_Edge plus user alpha settings) and uploads them to the shader
        // globals the zone-painting tool reads to highlight cells. Without
        // this, a new zone is functionally paintable but never gets an
        // entry in that shader array, so it highlights as blank/invisible
        // instead of its color. Called via reflection rather than
        // reimplementing its color-index math ourselves - Type.EmptyTypes
        // disambiguates from the private 2-arg per-zone overload of the
        // same name.
        private static readonly MethodInfo UpdateZoneColorsMethod =
            typeof(ZoneSystem).GetMethod("UpdateZoneColors", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

        private readonly EntityManager m_EntityManager;
        private readonly ZoneSystem m_ZoneSystem;

        public ManualPrefabInitializationWorkaround(World world)
        {
            m_EntityManager = world.EntityManager;
            m_ZoneSystem = world.GetOrCreateSystemManaged<ZoneSystem>();
        }

        // Assigns a fresh ZoneType index, sets ZoneData accordingly
        // (replicating ZoneSystem.InitializeZonePrefabs' own logic - not a
        // ComponentBase method), registers that index in ZoneSystem's
        // private m_ZonePrefabs list, then runs the real
        // Initialize/LateInitialize for every component on the clone
        // (ZonePropertiesData, UIObjectData + joining its group's buffer,
        // etc.). Returns the ZoneType assigned.
        public ZoneType InitializeClonedZone(ZonePrefab clonedZonePrefab, Entity clonedZoneEntity)
        {
            var zonePrefabs = (NativeList<Entity>)ZonePrefabsField.GetValue(m_ZoneSystem);

            // Mirrors ZoneSystem's own GetNextIndex(), minus the
            // free-slot-reuse-after-removal case - this workaround never
            // removes a zone, so it doesn't need it.
            var newIndex = (ushort)math.max(1, zonePrefabs.Length);
            var newZoneType = new ZoneType { m_Index = newIndex };

            while (newIndex > zonePrefabs.Length)
                zonePrefabs.Add(Entity.Null);
            if (newIndex < zonePrefabs.Length)
                zonePrefabs[newIndex] = clonedZoneEntity;
            else
                zonePrefabs.Add(clonedZoneEntity);

            // NativeList<T> wraps an unmanaged pointer - if Add() above
            // reallocated, the reflection-boxed copy's pointer may now
            // differ from what ZoneSystem's field holds. Writing it back
            // unconditionally is cheap and avoids a stale-pointer bug.
            ZonePrefabsField.SetValue(m_ZoneSystem, zonePrefabs);

            // Replicates ZoneSystem.InitializeZonePrefabs' own branch
            // exactly (area type from the prefab's own field, height
            // defaults). Doesn't yet handle the Office flag - neither test
            // zone used it, so it's untouched here; revisit if this is
            // ever used to clone an office zone.
            var zoneData = m_EntityManager.GetComponentData<ZoneData>(clonedZoneEntity);
            zoneData.m_AreaType = clonedZonePrefab.m_AreaType;
            if (clonedZonePrefab.m_AreaType != AreaType.None)
            {
                zoneData.m_ZoneType = newZoneType;
                zoneData.m_MinOddHeight = ushort.MaxValue;
                zoneData.m_MinEvenHeight = ushort.MaxValue;
                zoneData.m_MaxHeight = 0;
            }
            else
            {
                zoneData.m_ZoneFlags |= ZoneFlags.SupportNarrow;
                zoneData.m_MinOddHeight = 1;
                zoneData.m_MinEvenHeight = 1;
                zoneData.m_MaxHeight = 1;
            }
            m_EntityManager.SetComponentData(clonedZoneEntity, zoneData);

            UpdateZoneColorsMethod.Invoke(m_ZoneSystem, null);

            RunComponentLifecycle(clonedZonePrefab, clonedZoneEntity);

            // Nothing left for the automatic pipeline to do - prevent it
            // from stomping these values if it ever does fire, delayed.
            m_EntityManager.RemoveComponent<Created>(clonedZoneEntity);
            m_EntityManager.RemoveComponent<Updated>(clonedZoneEntity);

            return newZoneType;
        }

        // Runs the real Initialize/LateInitialize for every component on
        // the clone (sets SpawnableBuildingData.m_Level/m_ZonePrefab via
        // SpawnableBuilding's own LateInitialize - which reads m_ZoneType,
        // already correctly pointed at the cloned zone prefab by
        // SpawnableBuildingZoneLinker before AddPrefab - plus
        // UIObjectData, BuildingPropertyData via the zone's
        // IZoneBuildingComponent cascade, etc.), then sets
        // BuildingSpawnGroupData manually - the one thing BuildingInitializeSystem
        // sets that isn't a ComponentBase method - and copies
        // ObjectGeometryData/BuildingData, plus every DynamicBuffer<T> the
        // template has (SubMesh - the renderable mesh reference(s); SubObject
        // - the prefab-side list of decorative sub-objects read by
        // Game.Objects.SubObjectSystem at per-instance spawn time; and
        // whatever else turns up - found two so far by trial and error,
        // stopped guessing and now copy every buffer the archetype
        // declares, see CopyAllBuffers), straight from the template.
        // Those are a THIRD category, different from both the
        // ComponentBase-method case above and the bespoke-system-logic
        // case (ZoneType/m_ZonePrefabs): ObjectGeometryPrefab has no
        // Initialize/LateInitialize override at all - its values get
        // computed by yet another independent Created-tag-keyed system
        // (Game.Prefabs.ObjectInitializeSystem) that, like every other
        // such system found so far, never fires for a runtime-added
        // entity. Since the clone's geometry/footprint/mesh/sub-objects
        // should be identical to the template's anyway (same mesh, same
        // lot size - we're not changing what the building looks like,
        // only what zone it belongs to), copying directly is both correct
        // and simpler than re-deriving any of it ourselves.
        public void InitializeClonedBuilding(BuildingPrefab clonedBuildingPrefab, Entity templateBuildingEntity, Entity clonedBuildingEntity, ZoneType clonedZoneType)
        {
            // All template-derived baseline data goes in BEFORE
            // RunComponentLifecycle, not after - geometry/lot size and the
            // buffers included. Observed bug this fixes: lot-boundary
            // fences (regular SubObject entries, EdgePlacement-flagged -
            // Game.Objects.SubObjectSystem.CreateSubObjects computes where
            // they go from the owner's actual ObjectGeometryData/lot size)
            // coming out partial/stopping halfway when geometry/lot size
            // were still zeroed at the time anything in RunComponentLifecycle
            // read them, only getting corrected afterward. Order now
            // matches intent: establish the full template baseline first,
            // then let real Initialize/LateInitialize (and the manual
            // BuildingSpawnGroupData assignment) override whatever's
            // actually meant to differ per clone (today, just the zone
            // pointer) - not the other way around.
            if (m_EntityManager.TryGetComponent<ObjectGeometryData>(templateBuildingEntity, out var geometry))
                m_EntityManager.SetComponentData(clonedBuildingEntity, geometry);

            if (m_EntityManager.TryGetComponent<BuildingData>(templateBuildingEntity, out var buildingData))
                m_EntityManager.SetComponentData(clonedBuildingEntity, buildingData);

            CopyAllBuffers(templateBuildingEntity, clonedBuildingEntity);

            RunComponentLifecycle(clonedBuildingPrefab, clonedBuildingEntity);

            m_EntityManager.SetSharedComponent(clonedBuildingEntity, new BuildingSpawnGroupData(clonedZoneType));

            m_EntityManager.RemoveComponent<Created>(clonedBuildingEntity);
            m_EntityManager.RemoveComponent<Updated>(clonedBuildingEntity);
        }

        // Discovers every DynamicBuffer<T> type actually present on
        // source's archetype (same technique Game.Prefabs.ReplacePrefabSystem
        // itself uses to inspect an entity's live component set:
        // EntityManager.GetChunk(entity).Archetype.GetComponentTypes())
        // and mirrors each one onto destination, rather than hand-copying
        // one named buffer type at a time - every round of in-game testing
        // turned up a different buffer ObjectInitializeSystem would
        // normally have populated (SubMesh, then SubObject; there's no
        // documented list of what ObjectGeometryPrefab/BuildingPrefab's
        // full buffer set is, so there's no reason to expect those are the
        // last two). ComponentType.IsBuffer distinguishes buffers from
        // plain IComponentData/tag/shared types in the same archetype;
        // destination is skipped per-type if it doesn't declare that
        // buffer itself (nothing to copy into - e.g. a buffer type only
        // present because of some other component this specific prefab
        // doesn't have). The actual element copy has to go through
        // reflection (MakeGenericMethod) since EntityManager.GetBuffer<T>
        // needs a compile-time T and ComponentType only gives us a
        // System.Type at runtime.
        private static readonly MethodInfo CopyBufferMethod =
            typeof(ManualPrefabInitializationWorkaround).GetMethod(nameof(CopyBuffer), BindingFlags.NonPublic | BindingFlags.Instance);

        private void CopyAllBuffers(Entity source, Entity destination)
        {
            var componentTypes = m_EntityManager.GetChunk(source).Archetype.GetComponentTypes();
            try
            {
                foreach (var componentType in componentTypes)
                {
                    if (!componentType.IsBuffer || !m_EntityManager.HasComponent(destination, componentType))
                        continue;

                    CopyBufferMethod.MakeGenericMethod(componentType.GetManagedType()).Invoke(this, new object[] { source, destination });
                }
            }
            finally
            {
                componentTypes.Dispose();
            }
        }

        private void CopyBuffer<T>(Entity source, Entity destination) where T : unmanaged, IBufferElementData
        {
            if (!m_EntityManager.TryGetBuffer<T>(source, true, out var sourceBuffer))
                return;

            var destinationBuffer = m_EntityManager.GetBuffer<T>(destination, false);
            destinationBuffer.Clear();
            for (var i = 0; i < sourceBuffer.Length; i++)
                destinationBuffer.Add(sourceBuffer[i]);
        }

        // Mirrors PrefabInitializeSystem.InitializePrefab/LateInitializePrefab's
        // core loop (minus the dependency-queueing logic for newly-discovered
        // dependency prefabs, which doesn't apply here - our dependencies,
        // like the zone a building points at, are already fully loaded
        // vanilla objects). Logs and continues on a per-component failure,
        // same as the real system does, rather than letting one bad
        // component abort everything else.
        private void RunComponentLifecycle(PrefabBase prefab, Entity entity)
        {
            var components = new List<ComponentBase>();
            prefab.GetComponents(components);

            foreach (var component in components)
            {
                try
                {
                    component.Initialize(m_EntityManager, entity);
                }
                catch (Exception exception)
                {
                    Mod.log.Error($"[ManualPrefabInitializationWorkaround] Initialize failed for component {component.GetType().Name} on '{prefab.name}': {exception}");
                }
            }

            foreach (var component in components)
            {
                try
                {
                    component.LateInitialize(m_EntityManager, entity);
                }
                catch (Exception exception)
                {
                    Mod.log.Error($"[ManualPrefabInitializationWorkaround] LateInitialize failed for component {component.GetType().Name} on '{prefab.name}': {exception}");
                }
            }
        }
    }
}

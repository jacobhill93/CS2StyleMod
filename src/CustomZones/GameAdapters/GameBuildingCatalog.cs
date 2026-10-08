using Colossal.Entities;
using CS2StyleMod.Core;
using Game.Buildings;
using Game.Prefabs;
using Game.Zones;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using CoreZoneType = CS2StyleMod.Core.ZoneType;

namespace CS2StyleMod.CustomZones.GameAdapters
{
    // Real Core.IBuildingCatalog implementation: resolves Core's
    // game-independent CollectionEntry kinds against the live game's
    // registered prefabs.
    //
    // Looks buildings up by iterating an EntityQuery and matching by
    // PrefabBase.name, rather than via PrefabSystem.TryGetPrefab(PrefabID,
    // out _) - that overload's PrefabID equality includes a content hash
    // derived from the prefab's asset GUID (see Game.Prefabs.PrefabID),
    // which isn't known in advance from a name alone, so a name-only
    // PrefabID never matches anything. TryGetPrefab(Entity, out _) (used
    // here, same as the other adapters) has no such issue.
    public sealed class GameBuildingCatalog : IBuildingCatalog
    {
        private readonly PrefabSystem m_PrefabSystem;
        private readonly EntityManager m_EntityManager;
        private readonly EntityQuery m_BuildingQuery;

        public GameBuildingCatalog(World world)
        {
            m_EntityManager = world.EntityManager;
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_BuildingQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpawnableBuildingData>(),
                ComponentType.ReadOnly<BuildingData>(),
                ComponentType.ReadOnly<PrefabData>());
        }

        public IEnumerable<BuildingCandidate> Resolve(VanillaSelectorEntry selector)
        {
            var results = new List<BuildingCandidate>();
            var buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buildingEntity in buildingEntities)
                {
                    if (!TryDescribeBuilding(buildingEntity, out var candidate))
                        continue;

                    if (selector.ZoneType.HasValue && candidate.ZoneType != selector.ZoneType.Value)
                        continue;

                    // Not yet implemented: candidate.AssetPackId is always
                    // null (see TryDescribeBuilding), so a selector that
                    // specifies an asset pack currently matches nothing.
                    // Needs reading the building prefab's AssetPackElement
                    // buffer and resolving each AssetPackPrefab's name.
                    if (!string.IsNullOrEmpty(selector.AssetPackId) && candidate.AssetPackId != selector.AssetPackId)
                        continue;

                    results.Add(candidate);
                }
            }
            finally
            {
                buildingEntities.Dispose();
            }

            return results;
        }

        public BuildingCandidate Resolve(ExplicitAssetEntry entry)
        {
            if (TryGetBuildingPrefab(entry.PrefabId, out var _, out var buildingEntity) && TryDescribeBuilding(buildingEntity, out var candidate))
                return candidate;

            throw new KeyNotFoundException($"No registered building prefab named '{entry.PrefabId}'.");
        }

        // Not part of IBuildingCatalog - Core deliberately only deals in
        // plain BuildingCandidate values, never real game objects. Feature
        // logic (e.g. CustomZoneBuilder) that needs the actual prefab to
        // clone calls this directly on the concrete adapter instead.
        public bool TryGetBuildingPrefab(string prefabId, out BuildingPrefab prefab, out Entity entity)
        {
            var buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buildingEntity in buildingEntities)
                {
                    if (!m_PrefabSystem.TryGetPrefab<BuildingPrefab>(buildingEntity, out var candidatePrefab) || candidatePrefab.name != prefabId)
                        continue;

                    prefab = candidatePrefab;
                    entity = buildingEntity;
                    return true;
                }
            }
            finally
            {
                buildingEntities.Dispose();
            }

            prefab = null;
            entity = Entity.Null;
            return false;
        }

        // No game lookup needed - an adopted asset's candidate is built
        // entirely from the mod-supplied settings on the entry itself,
        // same as FakeBuildingCatalog's test double.
        public BuildingCandidate Resolve(AdoptedAssetEntry entry)
        {
            return new BuildingCandidate(entry.PrefabId, entry.Settings.Category, entry.Settings.Level, entry.Settings.LotSize);
        }

        private bool TryDescribeBuilding(Entity buildingEntity, out BuildingCandidate candidate)
        {
            candidate = null;

            if (!m_PrefabSystem.TryGetPrefab<BuildingPrefab>(buildingEntity, out var prefab))
                return false;

            // Signature buildings (the 12 "...Signature01/02/03" vanilla
            // residentials, confirmed via Game.Prefabs.SignatureBuilding)
            // carry SpawnableBuildingData too - SignatureBuilding.LateInitialize
            // sets it directly - so they pass this query and showed up as
            // ordinary candidates, but their PrefabBase has a
            // SignatureBuilding component instead of SpawnableBuilding.
            // SpawnableBuildingZoneLinker requires the latter and correctly
            // rejects them, but only after they'd already been resolved as
            // candidates - hence 12 clone failures per run with nothing
            // actually wrong. They're also one-per-city unique landmarks
            // with their own unlock/leveling model (PlacementFlags.Unique,
            // fixed m_Level=5) - not a fit for "clone this growable into my
            // zone" regardless. Filtering them out here is the correct
            // place: nothing should ever present them as a candidate.
            if (m_EntityManager.HasComponent<SignatureBuildingData>(buildingEntity))
                return false;

            var spawnable = m_EntityManager.GetComponentData<SpawnableBuildingData>(buildingEntity);
            var buildingData = m_EntityManager.GetComponentData<BuildingData>(buildingEntity);

            if (!m_EntityManager.TryGetComponent<ZoneData>(spawnable.m_ZonePrefab, out var zoneData) ||
                !m_EntityManager.TryGetComponent<ZonePropertiesData>(spawnable.m_ZonePrefab, out var zoneProps))
                return false;

            if (!TryMapToCoreZoneType(zoneData, zoneProps, out var coreZoneType))
                return false;

            candidate = new BuildingCandidate(
                prefab.name,
                coreZoneType,
                spawnable.m_Level,
                new LotSize(buildingData.m_LotSize.x, buildingData.m_LotSize.y));
            return true;
        }

        // Inverse of how the game itself categorizes a zone for its own UI
        // (confirmed against ZoneOrganizer's IndexZones, which does this
        // exact AreaType/IsOffice/density switch): maps a live ZoneData +
        // ZonePropertiesData back to Core's own ZoneType enum. Returns
        // false for anything that doesn't correspond to one of Core's
        // seven categories (e.g. AreaType.None).
        private static bool TryMapToCoreZoneType(ZoneData zoneData, ZonePropertiesData zoneProps, out CoreZoneType zoneType)
        {
            var density = PropertyUtils.GetZoneDensity(zoneData, zoneProps);
            var isOffice = zoneData.IsOffice();

            switch (zoneData.m_AreaType)
            {
                case AreaType.Residential when !isOffice && density == ZoneDensity.Low:
                    zoneType = CoreZoneType.ResidentialLow;
                    return true;
                case AreaType.Residential when !isOffice && density == ZoneDensity.Medium:
                    zoneType = CoreZoneType.ResidentialMedium;
                    return true;
                case AreaType.Residential when !isOffice:
                    zoneType = CoreZoneType.ResidentialHigh;
                    return true;
                case AreaType.Commercial when density == ZoneDensity.Low:
                    zoneType = CoreZoneType.CommercialLow;
                    return true;
                case AreaType.Commercial:
                    zoneType = CoreZoneType.CommercialHigh;
                    return true;
                case AreaType.Industrial when isOffice:
                    zoneType = CoreZoneType.Office;
                    return true;
                case AreaType.Industrial:
                    zoneType = CoreZoneType.Industrial;
                    return true;
                default:
                    zoneType = default;
                    return false;
            }
        }
    }
}

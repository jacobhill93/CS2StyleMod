using Colossal;
using CS2StyleMod.Core;
using CS2StyleMod.CustomZones.GameAdapters;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace CS2StyleMod.CustomZones
{
    // Feature logic for Custom Zones - the first thing above the
    // GameAdapters layer. Turns a CustomZoneDefinition into an actual
    // playable zone: resolves its referenced Collections' entries through
    // GameBuildingCatalog, then clones the zone and each resolved building
    // via ZonePrefabCloner/SpawnableBuildingZoneLinker. That's genuinely
    // all it takes - see DECISIONS.md "A runtime-cloned prefab is only
    // visible to init systems for one frame" for why no further manual
    // initialization is needed. The one thing that actually matters:
    // BuildOrRebuild must only ever be called from
    // CustomZoneBuildRequestSystem's own OnUpdate, never directly from
    // OnGameLoaded or any other call site - that system exists
    // specifically to own this timing requirement so nothing else has to
    // remember it.
    //
    // Depends on the concrete GameAdapters directly, not interfaces -
    // consistent with how they depend on each other, and there's no
    // testability benefit to abstracting them here: none of this is
    // unit-testable without the game anyway (same as every adapter it
    // composes).
    public sealed class CustomZoneBuilder
    {
        // Prefix for every prefab this builder registers - keeps our
        // clones trivially distinguishable from vanilla/other-mod prefabs
        // by name alone, and namespaces the internal naming scheme below.
        private const string NamePrefix = "CS2StyleMod.Zone.";

        private readonly PrefabSystem m_PrefabSystem;
        private readonly PrefabUISystem m_PrefabUISystem;
        private readonly EntityManager m_EntityManager;
        private readonly ZonePrefabCloner m_ZoneCloner;
        private readonly SpawnableBuildingZoneLinker m_BuildingLinker;
        private readonly GameBuildingCatalog m_Catalog;
        private readonly GameZonePrefabResolver m_ZoneResolver;

        public CustomZoneBuilder(World world, GameBuildingCatalog catalog)
        {
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabUISystem = world.GetOrCreateSystemManaged<PrefabUISystem>();
            m_EntityManager = world.EntityManager;
            m_ZoneCloner = new ZonePrefabCloner(m_PrefabSystem);
            m_BuildingLinker = new SpawnableBuildingZoneLinker(m_PrefabSystem);
            m_Catalog = catalog;
            m_ZoneResolver = new GameZonePrefabResolver(world);
        }

        // Builds definition fresh the first time, or rebuilds it in place
        // on every later call - found by its stable internal name (derived
        // from definition.Id, not its user-facing Name), not by tracking
        // any extra state of our own. Additive: a building newly resolved
        // from collections that wasn't cloned before gets cloned now; one
        // that WAS cloned before but is no longer resolved gets its
        // BuildingSpawnGroupData cleared (stops being offered for new
        // construction) without touching the entity itself or anything
        // already built from it - see DECISIONS.md "Removing an asset
        // doesn't touch what's already built".
        public ZonePrefab BuildOrRebuild(CustomZoneDefinition definition, IEnumerable<Collection> collections)
        {
            var internalZoneName = GetInternalZoneName(definition.Id);

            if (!TryGetOrCloneZone(definition, internalZoneName, out var clonedZone, out var clonedZoneEntity))
                return null;

            RegisterDisplayName(clonedZoneEntity, definition.Name);
            var zoneType = m_EntityManager.GetComponentData<ZoneData>(clonedZoneEntity).m_ZoneType;

            var resolvedPrefabIds = new HashSet<string>();
            var clonedCount = 0;

            foreach (var entry in EnumerateEntries(collections))
            {
                switch (entry)
                {
                    case VanillaSelectorEntry selector:
                        foreach (var candidate in m_Catalog.Resolve(selector))
                        {
                            if (resolvedPrefabIds.Add(candidate.PrefabId) &&
                                EnsureBuildingCloned(candidate.PrefabId, clonedZone, zoneType, internalZoneName, definition.Name))
                                clonedCount++;
                        }
                        break;

                    case ExplicitAssetEntry explicitEntry:
                        if (resolvedPrefabIds.Add(explicitEntry.PrefabId) &&
                            EnsureBuildingCloned(explicitEntry.PrefabId, clonedZone, zoneType, internalZoneName, definition.Name))
                            clonedCount++;
                        break;

                    case AdoptedAssetEntry adopted:
                        Mod.log.Warn($"[CustomZoneBuilder] Skipping adopted asset '{adopted.PrefabId}' for zone '{definition.Name}' - adopted assets aren't cloneable yet (no GameAdapter gives them growable data).");
                        break;
                }
            }

            var retiredCount = RetireBuildingsNoLongerResolved(internalZoneName, resolvedPrefabIds);

            Mod.log.Info($"[CustomZoneBuilder] Built/rebuilt zone '{definition.Name}': {clonedCount} newly cloned, {resolvedPrefabIds.Count} total offered, {retiredCount} retired.");
            return clonedZone;
        }

        private bool TryGetOrCloneZone(CustomZoneDefinition definition, string internalZoneName, out ZonePrefab clonedZone, out Entity clonedZoneEntity)
        {
            if (m_ZoneResolver.TryFindByName(internalZoneName, out clonedZone, out clonedZoneEntity))
                return true; // Already built in an earlier request - rebuilding in place.

            if (!m_ZoneResolver.TryFindVanillaZone(definition.BaseZoneType, out var baseZone))
            {
                Mod.log.Warn($"[CustomZoneBuilder] No vanilla zone found for base type '{definition.BaseZoneType}' - cannot build '{definition.Name}'.");
                clonedZone = null;
                clonedZoneEntity = Entity.Null;
                return false;
            }

            clonedZone = m_ZoneCloner.CloneZone(baseZone, internalZoneName);
            if (clonedZone == null)
            {
                Mod.log.Warn($"[CustomZoneBuilder] Failed to clone zone '{baseZone.name}' as '{internalZoneName}' - see PrefabSystem's own log output above for why.");
                clonedZoneEntity = Entity.Null;
                return false;
            }

            if (!m_PrefabSystem.TryGetEntity(clonedZone, out clonedZoneEntity))
            {
                Mod.log.Warn($"[CustomZoneBuilder] Cloned zone '{internalZoneName}' has no registered Entity despite AddPrefab succeeding - unexpected.");
                clonedZone = null;
                return false;
            }

            return true;
        }

        // Returns true only when a NEW clone was made this call, so the
        // caller can count it - an already-existing building being
        // re-offered (or re-displayed under a renamed definition) isn't a
        // new clone.
        private bool EnsureBuildingCloned(string templatePrefabId, ZonePrefab clonedZone, Game.Zones.ZoneType zoneType, string internalZoneName, string displayZoneName)
        {
            var internalBuildingName = GetInternalBuildingName(internalZoneName, templatePrefabId);

            if (m_Catalog.TryGetBuildingPrefab(internalBuildingName, out _, out var existingEntity))
            {
                // Already cloned in an earlier build/rebuild. Re-offer it
                // (a previous rebuild may have retired it if it had
                // temporarily dropped out of every referenced collection)
                // and refresh its display name in case the definition was
                // renamed since.
                m_EntityManager.SetSharedComponent(existingEntity, new BuildingSpawnGroupData(zoneType));
                RegisterDisplayName(existingEntity, $"{displayZoneName} - {templatePrefabId}");
                return false;
            }

            if (!m_Catalog.TryGetBuildingPrefab(templatePrefabId, out var templateBuilding, out _))
            {
                Mod.log.Warn($"[CustomZoneBuilder] Could not resolve building prefab '{templatePrefabId}' for zone '{displayZoneName}' - skipping.");
                return false;
            }

            var clonedBuilding = m_BuildingLinker.CloneBuildingForZone(templateBuilding, clonedZone, internalBuildingName);
            if (clonedBuilding == null)
            {
                Mod.log.Warn($"[CustomZoneBuilder] Failed to clone building '{templateBuilding.name}' for zone '{displayZoneName}' - see PrefabSystem's own log output above for why.");
                return false;
            }

            if (!m_PrefabSystem.TryGetEntity(clonedBuilding, out var clonedBuildingEntity))
            {
                Mod.log.Warn($"[CustomZoneBuilder] Cloned building '{clonedBuilding.name}' has no registered Entity despite AddPrefab succeeding - unexpected.");
                return false;
            }

            RegisterDisplayName(clonedBuildingEntity, $"{displayZoneName} - {templateBuilding.name}");
            return true;
        }

        // Clears BuildingSpawnGroupData (zone index 0 - never assigned to
        // any real zone, see ManualPrefabInitializationWorkaround's
        // history in DECISIONS.md) on every already-cloned building for
        // this zone that didn't get resolved this round, so it stops
        // being offered for new construction. Doesn't touch the entity or
        // anything already built from it.
        private int RetireBuildingsNoLongerResolved(string internalZoneName, HashSet<string> resolvedPrefabIds)
        {
            var namePrefix = GetInternalBuildingNamePrefix(internalZoneName);
            var retiredCount = 0;

            foreach (var (prefab, entity) in m_Catalog.FindBuildingsByNamePrefix(namePrefix))
            {
                var templatePrefabId = prefab.name.Substring(namePrefix.Length);
                if (resolvedPrefabIds.Contains(templatePrefabId))
                    continue;

                m_EntityManager.SetSharedComponent(entity, new BuildingSpawnGroupData(default));
                retiredCount++;
            }

            return retiredCount;
        }

        private void RegisterDisplayName(Entity entity, string displayName)
        {
            m_PrefabUISystem.GetTitleAndDescription(entity, out var titleId, out _);
            GameManager.instance.localizationManager.AddSource("en-US", new DisplayNameLocaleSource(titleId, displayName));
        }

        private static string GetInternalZoneName(Guid definitionId) => $"{NamePrefix}{definitionId:N}";

        private static string GetInternalBuildingNamePrefix(string internalZoneName) => $"{internalZoneName}.";

        private static string GetInternalBuildingName(string internalZoneName, string templatePrefabId) =>
            $"{GetInternalBuildingNamePrefix(internalZoneName)}{templatePrefabId}";

        private static IEnumerable<CollectionEntry> EnumerateEntries(IEnumerable<Collection> collections)
        {
            foreach (var collection in collections)
                foreach (var entry in collection.Entries)
                    yield return entry;
        }

        private sealed class DisplayNameLocaleSource : IDictionarySource
        {
            private readonly string m_Key;
            private readonly string m_Value;

            public DisplayNameLocaleSource(string key, string value)
            {
                m_Key = key;
                m_Value = value;
            }

            public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
            {
                return new[] { new KeyValuePair<string, string>(m_Key, m_Value) };
            }

            public void Unload()
            {
            }
        }
    }
}

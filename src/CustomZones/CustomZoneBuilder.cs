using Colossal;
using CS2StyleMod.Core;
using CS2StyleMod.CustomZones.GameAdapters;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI.InGame;
using System.Collections.Generic;
using Unity.Entities;

namespace CS2StyleMod.CustomZones
{
    // Feature logic for Custom Zones - the first thing above the
    // GameAdapters layer. Turns a Collection into an actual playable zone:
    // resolves its entries through GameBuildingCatalog, then clones the
    // zone and each resolved building via ZonePrefabCloner/
    // SpawnableBuildingZoneLinker. That's genuinely all it takes - see
    // DECISIONS.md "A runtime-cloned prefab is only visible to init
    // systems for one frame" for why no further manual initialization is
    // needed (it used to be, via a since-deleted workaround, before that
    // root cause was found). The one thing that actually matters: Build()
    // must only ever be called from CustomZoneBuildRequestSystem's own
    // OnUpdate, never directly from OnGameLoaded or any other call site -
    // that system exists specifically to own this timing requirement so
    // nothing else has to remember it.
    //
    // Depends on the concrete GameAdapters directly, not interfaces -
    // consistent with how they depend on each other, and there's no
    // testability benefit to abstracting them here: none of this is
    // unit-testable without the game anyway (same as every adapter it
    // composes).
    public sealed class CustomZoneBuilder
    {
        private readonly PrefabSystem m_PrefabSystem;
        private readonly PrefabUISystem m_PrefabUISystem;
        private readonly ZonePrefabCloner m_ZoneCloner;
        private readonly SpawnableBuildingZoneLinker m_BuildingLinker;
        private readonly GameBuildingCatalog m_Catalog;

        public CustomZoneBuilder(World world, GameBuildingCatalog catalog)
        {
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabUISystem = world.GetOrCreateSystemManaged<PrefabUISystem>();
            m_ZoneCloner = new ZonePrefabCloner(m_PrefabSystem);
            m_BuildingLinker = new SpawnableBuildingZoneLinker(m_PrefabSystem);
            m_Catalog = catalog;
        }

        // Clones baseZone as newZoneName, then clones one building per
        // distinct prefab resolved from the given collections' entries
        // (vanilla selectors can resolve to many; duplicates across
        // entries/collections are cloned only once). Adopted assets are
        // skipped with a warning - they need their own GameAdapter to
        // give an arbitrary building growable data first, not yet built.
        // Returns null (logging why) if the zone itself couldn't be
        // cloned; a building failing to clone is logged and skipped
        // without aborting the rest.
        public ZonePrefab Build(ZonePrefab baseZone, string newZoneName, IEnumerable<Collection> collections)
        {
            var clonedZone = m_ZoneCloner.CloneZone(baseZone, newZoneName);
            if (clonedZone == null)
            {
                Mod.log.Warn($"[CustomZoneBuilder] Failed to clone zone '{baseZone.name}' as '{newZoneName}' - see PrefabSystem's own log output above for why.");
                return null;
            }

            if (!m_PrefabSystem.TryGetEntity(clonedZone, out var clonedZoneEntity))
            {
                Mod.log.Warn($"[CustomZoneBuilder] Cloned zone '{newZoneName}' has no registered Entity despite AddPrefab succeeding - unexpected.");
                return null;
            }

            // PrefabUISystem resolves a prefab's display name to a locale
            // key like "Assets.NAME[<prefab name>]" - with no locale entry
            // for a freshly-cloned name, the UI shows blank. Register one
            // directly.
            RegisterDisplayName(clonedZoneEntity, newZoneName);

            var clonedPrefabIds = new HashSet<string>();
            var clonedCount = 0;

            foreach (var entry in EnumerateEntries(collections))
            {
                switch (entry)
                {
                    case VanillaSelectorEntry selector:
                        foreach (var candidate in m_Catalog.Resolve(selector))
                        {
                            if (CloneOneBuilding(candidate.PrefabId, clonedZone, newZoneName, clonedPrefabIds))
                                clonedCount++;
                        }
                        break;

                    case ExplicitAssetEntry explicitEntry:
                        if (CloneOneBuilding(explicitEntry.PrefabId, clonedZone, newZoneName, clonedPrefabIds))
                            clonedCount++;
                        break;

                    case AdoptedAssetEntry adopted:
                        Mod.log.Warn($"[CustomZoneBuilder] Skipping adopted asset '{adopted.PrefabId}' for zone '{newZoneName}' - adopted assets aren't cloneable yet (no GameAdapter gives them growable data).");
                        break;
                }
            }

            Mod.log.Info($"[CustomZoneBuilder] Built zone '{newZoneName}' from '{baseZone.name}' with {clonedCount} building(s).");
            return clonedZone;
        }

        private bool CloneOneBuilding(string prefabId, ZonePrefab clonedZone, string newZoneName, HashSet<string> clonedPrefabIds)
        {
            if (!clonedPrefabIds.Add(prefabId))
                return false; // already cloned for this zone via another entry/collection.

            if (!m_Catalog.TryGetBuildingPrefab(prefabId, out var templateBuilding, out _))
            {
                Mod.log.Warn($"[CustomZoneBuilder] Could not resolve building prefab '{prefabId}' for zone '{newZoneName}' - skipping.");
                return false;
            }

            var clonedBuilding = m_BuildingLinker.CloneBuildingForZone(templateBuilding, clonedZone, $"{newZoneName} - {templateBuilding.name}");
            if (clonedBuilding == null)
            {
                Mod.log.Warn($"[CustomZoneBuilder] Failed to clone building '{templateBuilding.name}' for zone '{newZoneName}' - see PrefabSystem's own log output above for why.");
                return false;
            }

            if (!m_PrefabSystem.TryGetEntity(clonedBuilding, out var clonedBuildingEntity))
            {
                Mod.log.Warn($"[CustomZoneBuilder] Cloned building '{clonedBuilding.name}' has no registered Entity despite AddPrefab succeeding - unexpected.");
                return false;
            }

            RegisterDisplayName(clonedBuildingEntity, $"{newZoneName} - {templateBuilding.name}");
            return true;
        }

        private void RegisterDisplayName(Entity entity, string displayName)
        {
            m_PrefabUISystem.GetTitleAndDescription(entity, out var titleId, out _);
            GameManager.instance.localizationManager.AddSource("en-US", new DisplayNameLocaleSource(titleId, displayName));
        }

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

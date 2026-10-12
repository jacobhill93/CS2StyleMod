using Colossal;
using ZoneBuilder.Core;
using ZoneBuilder.CustomZones.GameAdapters;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace ZoneBuilder.CustomZones
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
        private const string NamePrefix = "ZoneBuilder.Zone.";

        private readonly PrefabSystem m_PrefabSystem;
        private readonly PrefabUISystem m_PrefabUISystem;
        private readonly EntityManager m_EntityManager;
        private readonly ZonePrefabCloner m_ZoneCloner;
        private readonly SpawnableBuildingZoneLinker m_BuildingLinker;
        private readonly GameBuildingCatalog m_Catalog;
        private readonly GameZonePrefabResolver m_ZoneResolver;
        private readonly ZoneToolSystem m_ZoneToolSystem;

        public CustomZoneBuilder(World world, GameBuildingCatalog catalog)
        {
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabUISystem = world.GetOrCreateSystemManaged<PrefabUISystem>();
            m_ZoneToolSystem = world.GetOrCreateSystemManaged<ZoneToolSystem>();
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
                            // A Collection is a zone-type-agnostic bag by
                            // design (see Core.Collection) - nothing stops
                            // a building whose own zone type doesn't match
                            // this zone's BaseZoneType from ending up in an
                            // attached collection. Silently excluded here,
                            // not warned about - a selector can resolve
                            // many candidates, and this is the expected,
                            // common case for a collection shared across
                            // zones of different types.
                            if (candidate.ZoneType != definition.BaseZoneType)
                                continue;

                            if (resolvedPrefabIds.Add(candidate.PrefabId) &&
                                EnsureBuildingCloned(candidate.PrefabId, clonedZone, zoneType, internalZoneName, definition.Name))
                                clonedCount++;
                        }
                        break;

                    case ExplicitAssetEntry explicitEntry:
                        if (!m_Catalog.TryResolve(explicitEntry, out var explicitCandidate))
                        {
                            Mod.log.Warn($"[CustomZoneBuilder] Could not resolve building '{explicitEntry.PrefabId}' for zone '{definition.Name}' - skipping.");
                            break;
                        }

                        // Same exclusion as the selector case above, just
                        // warned about here: an explicit pick is a direct,
                        // one-off user choice (e.g. via the building
                        // picker), not a broad filter - found via real
                        // in-game testing (2026-10-08): a ResidentialLow
                        // zone offered a ResidentialMedium building for
                        // construction because nothing checked this,
                        // confirmed by reading the actual collections.json/
                        // customZones.json data, not guessed. See
                        // DECISIONS.md.
                        if (explicitCandidate.ZoneType != definition.BaseZoneType)
                        {
                            Mod.log.Warn($"[CustomZoneBuilder] Skipping '{explicitEntry.PrefabId}' for zone '{definition.Name}' - its zone type ({explicitCandidate.ZoneType}) doesn't match the zone's base type ({definition.BaseZoneType}).");
                            break;
                        }

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

        // Sixth attempt (2026-10-11) - five prior mechanisms (active=false,
        // manually removing the zone's UIGroupElement buffer entry,
        // PrefabSystem.UpdateAvailable(), then enabling Locked on the
        // entity) were each confirmed via logging to run cleanly with zero
        // early returns, yet the zone stayed fully visible/paintable
        // in-game every time, even after navigating away and back. Stopped
        // guessing at the symptom layer and read the actual decompiled
        // game source for the real removal pathway instead of inferring
        // one from method names:
        //  - Game.Prefabs.PrefabSystem.RemovePrefab(PrefabBase) is what the
        //    game itself calls "removing a prefab" - it does
        //    EntityManager.AddComponent<Deleted>(entity) (a real tag
        //    component, Game.Common.Deleted) plus deregisters the prefab
        //    from PrefabSystem's own m_Prefabs/m_Entities/m_PrefabIndices.
        //  - Game.Prefabs.UIInitializeSystem runs every frame (not a
        //    one-time init despite the name) with a query matching
        //    PrefabData + Deleted, and for each match removes that entity
        //    from its group's UIGroupElement AND UnlockRequirement buffers
        //    automatically (RemoveFrom, called from its own OnUpdate).
        //    This is the actual mechanism the picker's underlying data
        //    depends on - none of the first five attempts ever added
        //    Deleted, so this system never had anything to act on.
        // Previously avoided RemovePrefab here on the (unverified)
        // suspicion that fully removing the prefab would orphan cells
        // already painted with this zone type. Checked that directly in
        // Game.Prefabs.ZoneSystem.InitializeZonePrefabs: a painted cell's
        // Cell.m_Zone.m_Index is a separate, zone-specific index (assigned
        // by ZoneSystem.GetNextIndex, tracked in m_ZonePrefabs), not
        // PrefabSystem's own general-purpose prefab index - the two are
        // unrelated, so RemovePrefab's internal m_Prefabs/m_Entities
        // reindexing can't renumber an already-painted cell's zone type
        // out from under it. ZoneSystem's own Deleted-tag handling branch
        // (same component RemovePrefab adds) just clears that zone's slot
        // in m_ZonePrefabs to Entity.Null and frees the index for reuse -
        // exactly the "stop offering it, don't corrupt what's already
        // placed" behavior wanted here, and clearly a case the base game's
        // own authors designed for already, not an edge case being
        // stumbled into.
        // Looked up by internal name (same stable name every build/rebuild
        // uses), not a held reference - a zone that was never actually
        // built (its vanilla template was never found) has nothing to
        // retire, a no-op not an error. Called via
        // CustomZoneBuildRequestSystem.RequestRetirement, not directly -
        // same UpdateBefore<_, PrefabSystem> timing BuildOrRebuild already
        // relies on.
        public void RetireZone(Guid definitionId)
        {
            var internalZoneName = GetInternalZoneName(definitionId);

            if (!m_ZoneResolver.TryFindByName(internalZoneName, out var zone, out var zoneEntity))
            {
                Mod.log.Warn($"[CustomZoneBuilder] RetireZone: no built prefab found for '{internalZoneName}' - nothing to retire.");
                return;
            }

            // First crash found in-game (2026-10-11): deleting a zone while
            // the zone tool still had it selected threw a CRITICAL
            // KeyNotFoundException from Game.Rendering.NetColorSystem -
            // confirmed via its own decompiled source
            // (D:\CS2-Decompiled\Game.Rendering\NetColorSystem.cs, OnUpdate):
            // every frame, while the zone tool is the active tool, it
            // unconditionally does m_PrefabSystem.GetEntity(m_ZoneToolSystem.prefab)
            // with no Deleted-awareness at all - a stale selection pointing
            // at a prefab RemovePrefab just deregistered throws instead of
            // returning Entity.Null. Clearing the tool's own selection
            // first (same idea as a UI discarding a reference to something
            // it's about to delete) avoids this - PrefabSystem.RemovePrefab
            // itself is still correct, see the comment below.
            if (m_ZoneToolSystem.prefab == zone)
            {
                m_ZoneToolSystem.prefab = null;
                Mod.log.Info($"[CustomZoneBuilder] RetireZone: '{zone.name}' was the zone tool's active selection - cleared it before removing the prefab.");
            }

            var removed = m_PrefabSystem.RemovePrefab(zone);
            Mod.log.Info($"[CustomZoneBuilder] RetireZone: '{zone.name}' found (Entity {zoneEntity.Index}:{zoneEntity.Version}), PrefabSystem.RemovePrefab returned {removed}.");
        }

        private bool TryGetOrCloneZone(CustomZoneDefinition definition, string internalZoneName, out ZonePrefab clonedZone, out Entity clonedZoneEntity)
        {
            if (m_ZoneResolver.TryFindByName(internalZoneName, out clonedZone, out clonedZoneEntity))
                return true; // Already built in an earlier request - rebuilding in place.

            if (!m_ZoneResolver.TryFindVanillaZone(definition.BaseZoneType, definition.Region, out var baseZone))
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

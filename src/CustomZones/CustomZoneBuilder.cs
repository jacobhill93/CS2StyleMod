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

        // Deactivates (does NOT remove) an already-built custom zone's
        // prefab - same "stop offering, don't touch what's already built"
        // philosophy as RetireBuildingsNoLongerResolved below: cells
        // already painted with this zone keep working (removing the
        // prefab outright, via PrefabSystem.RemovePrefab, risks orphaning
        // them - not attempted, same reasoning that kept building
        // retirement non-destructive). Looked up by internal name (same
        // stable name every build/rebuild uses), not a held reference - a
        // zone that was never actually built (its vanilla template was
        // never found) has nothing to retire, a no-op not an error.
        // Called via CustomZoneBuildRequestSystem.RequestRetirement, not
        // directly - PrefabSystem.AddOrUpdatePrefab's own timing
        // requirements (if any) aren't verified, and routing through the
        // existing request queue costs nothing and guarantees the same
        // safe UpdateBefore<_, PrefabSystem> timing BuildOrRebuild already
        // relies on, rather than assuming this specific method has none.
        public void RetireZone(Guid definitionId)
        {
            var internalZoneName = GetInternalZoneName(definitionId);

            if (!m_ZoneResolver.TryFindByName(internalZoneName, out var zone, out var zoneEntity))
                return;

            zone.active = false;
            m_PrefabSystem.AddOrUpdatePrefab(zone);

            // Second attempt (2026-10-11) - flipping `active` alone was
            // confirmed in-game NOT to hide the zone from the zone tool's
            // picker. Real mechanism, found via further live reflection:
            // Game.Prefabs.UIGroupElement is a genuine ECS
            // IBufferElementData ({ Entity m_Prefab }) - a UI group's own
            // entity carries a BUFFER of its member prefabs' entities,
            // populated once at registration (the same UIObject.LateInitialize
            // step ZonePrefabCloner's own m_Group comment already
            // documents - "skips adding it to any category when m_Group
            // is null"). Nothing removes an entry from that buffer
            // automatically just because `active` changed afterward.
            // Still not confirmed this buffer is what the picker actually
            // reads (strong structural inference, not observed directly) -
            // removing our zone's entry from it directly is the next,
            // more targeted attempt.
            if (zone.TryGet<UIObject>(out var uiObject) &&
                uiObject.m_Group != null &&
                m_PrefabSystem.TryGetEntity(uiObject.m_Group, out var groupEntity) &&
                m_EntityManager.HasBuffer<UIGroupElement>(groupEntity))
            {
                var buffer = m_EntityManager.GetBuffer<UIGroupElement>(groupEntity);
                for (var i = buffer.Length - 1; i >= 0; i--)
                {
                    if (buffer[i].m_Prefab == zoneEntity)
                        buffer.RemoveAt(i);
                }
            }
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

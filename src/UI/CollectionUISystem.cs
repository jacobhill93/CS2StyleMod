using ZoneBuilder.Core;
using System;
using System.Linq;

namespace ZoneBuilder.UI
{
    // Exposes Mod.CollectionLibrary to the UI - list, create, rename,
    // delete, and (via the building picker) add/remove buildings.
    // Registered UpdateAt<_>(SystemUpdatePhase.UIUpdate) in Mod.cs, same
    // phase every other UI-binding system in the game uses.
    //
    // Built on ExtendedUISystemBase (adapted from RoadBuilder, MIT - see
    // that file's header) instead of raw UISystemBase - CreateBinding/
    // CreateTrigger replace what used to be manual ValueBinding +
    // ArrayWriter<T> + TriggerBinding + ValueReaders.Create wiring.
    // CollectionUIEntry still implements IJsonWritable itself and keeps
    // doing so deliberately: ExtendedUISystemBase's GenericUIWriter writes
    // a plain object's PUBLIC PROPERTY NAMES verbatim (PascalCase, no
    // camelCase conversion) via reflection, which would silently change
    // the JSON keys this system has always sent (id/name/entryCount/
    // prefabIds) - but it checks `obj is IJsonWritable` first and
    // delegates to the object's own Write() when present, so keeping
    // CollectionUIEntry's hand-written Write() is what keeps the wire
    // format - and therefore every TS consumer - unchanged.
    public partial class CollectionUISystem : ExtendedUISystemBase
    {
        private ValueBindingHelper<CollectionUIEntry[]> m_Collections;
        private CustomZones.CustomZoneBuildRequestSystem m_BuildRequestSystem;

        // Set by OnFinishEditingCollectionAssets, cleared by
        // OnDismissCollectionLevel1Warning or by GetCollectionLevel1Warning
        // itself once the gap is fixed - see that method's comment.
        private Guid? m_PendingLevel1WarningCollectionId;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_BuildRequestSystem = World.GetOrCreateSystemManaged<CustomZones.CustomZoneBuildRequestSystem>();

            m_Collections = CreateBinding("Collections", GetEntries());

            // Getter-based, not "set .Value on mutation" - see
            // ZoneBuildProgressEntry's comment for why that pattern exists;
            // reused here because GetCollectionLevel1Warning also needs to
            // self-clear once the gap it flagged is fixed, which a plain
            // mutation-driven binding can't express as cleanly.
            CreateBinding<CollectionLevel1WarningEntry>("CollectionLevel1Warning", GetCollectionLevel1Warning);

            // Takes a client-generated id (chunk 5) rather than letting
            // Collection's own Guid.NewGuid() ctor pick one, so the caller
            // knows which collection was just created without waiting on a
            // round trip through the Collections binding - needed to open
            // the building picker pre-targeted at it immediately.
            CreateTrigger<string, string>("CreateCollection", OnCreateCollection);
            CreateTrigger<string>("DeleteCollection", OnDeleteCollection);
            CreateTrigger<string, string>("RenameCollection", OnRenameCollection);

            // Called from the building picker (chunk 4), not this panel -
            // lives here anyway since this system already owns every other
            // Collection mutation + the refresh that keeps entryCount
            // current, same reasoning as the other triggers above.
            CreateTrigger<string, string>("AddBuildingToCollection", OnAddBuildingToCollection);
            CreateTrigger<string, string>("RemoveBuildingFromCollection", OnRemoveBuildingFromCollection);

            // Fired once, when the user clicks "Done" in the Assets tab's
            // picking mode (ShellFeature.finishAddingAssets) - see
            // DECISIONS.md "Level 1 warning moved to the collection, not
            // the zone attach" for why this replaced the old zone-attach-
            // time check entirely: that check never fired for a collection
            // built/edited standalone and attached to a zone later (or
            // never attached at all), which is the common case for how
            // collections actually get built in practice.
            CreateTrigger<string>("FinishEditingCollectionAssets", OnFinishEditingCollectionAssets);
            CreateTrigger("DismissCollectionLevel1Warning", OnDismissCollectionLevel1Warning);

            // Phase 5 (UI_UPGRADE_GUIDE.md) - minimal failure surfacing.
            // UIErrorState is shared with ZoneUISystem (see its own
            // comment) - exposed here, not there, simply because this
            // system was registered first; either would have worked.
            CreateBinding<UIErrorEntry>("UIError", () => UIErrorState.Current);
            CreateTrigger("DismissUIError", UIErrorState.Dismiss);
        }

        private void OnCreateCollection(string id, string name)
        {
            if (!Guid.TryParse(id, out var guid) || string.IsNullOrWhiteSpace(name))
                return;

            if (!TrySave(new Collection(guid, name), "create"))
                return;

            RefreshCollections();
        }

        private void OnDeleteCollection(string id)
        {
            if (!Guid.TryParse(id, out var guid))
                return;

            if (!TryDelete(guid))
                return;

            RebuildZonesReferencing(guid, m_BuildRequestSystem);
            RefreshCollections();
        }

        private void OnRenameCollection(string id, string newName)
        {
            if (!Guid.TryParse(id, out var guid) || string.IsNullOrWhiteSpace(newName))
                return;
            if (!Mod.CollectionLibrary.TryGet(guid, out var collection))
                return;

            collection.Name = newName;
            if (!TrySave(collection, "rename"))
                return;

            RefreshCollections();
        }

        private void OnAddBuildingToCollection(string collectionId, string prefabId)
        {
            if (!Guid.TryParse(collectionId, out var guid) || string.IsNullOrWhiteSpace(prefabId))
                return;
            if (!Mod.CollectionLibrary.TryGet(guid, out var collection))
                return;

            // Idempotent - repeat clicks (or the same building added twice
            // by mistake) shouldn't balloon the collection with duplicate
            // entries for the same prefab.
            if (collection.Entries.OfType<ExplicitAssetEntry>().Any(e => e.PrefabId == prefabId))
                return;

            collection.Add(new ExplicitAssetEntry(prefabId));
            if (!TrySave(collection, "add building to"))
                return;

            RebuildZonesReferencing(guid, m_BuildRequestSystem);
            RefreshCollections();
        }

        private void OnRemoveBuildingFromCollection(string collectionId, string prefabId)
        {
            if (!Guid.TryParse(collectionId, out var guid) || string.IsNullOrWhiteSpace(prefabId))
                return;
            if (!Mod.CollectionLibrary.TryGet(guid, out var collection))
                return;

            collection.RemoveExplicitAsset(prefabId);
            if (!TrySave(collection, "remove building from"))
                return;

            RebuildZonesReferencing(guid, m_BuildRequestSystem);
            RefreshCollections();
        }

        // Purely informational - the add/remove that got us here already
        // committed (decision #3 in UI_OVERHAUL_PLAN.md: one-click-
        // immediate, no batch confirm step), so there's nothing to cancel.
        // Checks the collection's actual final state, not just "did this
        // session add a non-L1 building" - catches the case where it was
        // already missing an L1 before this editing session even started.
        private void OnFinishEditingCollectionAssets(string collectionId)
        {
            if (!Guid.TryParse(collectionId, out var guid) || !Mod.CollectionLibrary.TryGet(guid, out var collection))
                return;

            var missing = m_BuildRequestSystem.FindZoneTypesMissingLevel1(collection);
            m_PendingLevel1WarningCollectionId = missing.Count > 0 ? guid : (Guid?)null;

            if (missing.Count > 0)
                Mod.log.Warn($"[CollectionUISystem] Collection '{collection.Name}' has no Level 1 building for: {string.Join(", ", missing)} - it won't spawn anything fresh on an empty lot for those zone types.");
        }

        private void OnDismissCollectionLevel1Warning()
        {
            m_PendingLevel1WarningCollectionId = null;
        }

        // Getter-based (see OnCreate's comment) specifically so it also
        // self-clears: if the user went back into the picker and fixed the
        // gap (added an L1 building) without explicitly dismissing first,
        // re-checking here rather than trusting the stale m_Pending* flag
        // means the banner doesn't linger past the point it's still true.
        private CollectionLevel1WarningEntry GetCollectionLevel1Warning()
        {
            if (!m_PendingLevel1WarningCollectionId.HasValue)
                return CollectionLevel1WarningEntry.Inactive;
            if (!Mod.CollectionLibrary.TryGet(m_PendingLevel1WarningCollectionId.Value, out var collection))
                return CollectionLevel1WarningEntry.Inactive;

            var missing = m_BuildRequestSystem.FindZoneTypesMissingLevel1(collection);
            if (missing.Count == 0)
            {
                m_PendingLevel1WarningCollectionId = null;
                return CollectionLevel1WarningEntry.Inactive;
            }

            return new CollectionLevel1WarningEntry(
                true,
                m_PendingLevel1WarningCollectionId.Value.ToString(),
                collection.Name,
                missing.Select(z => z.ToString()).ToArray());
        }

        private void RefreshCollections()
        {
            m_Collections.Value = GetEntries();
        }

        // The actual bug behind "I added a building to a collection and
        // nothing happened - it never spawned" (user feedback,
        // 2026-10-17): every mutation here only ever saved the Collection
        // itself and refreshed this system's own Collections binding -
        // nothing told any zone already using this collection to
        // rebuild. ZoneUISystem's own attach/detach handlers always
        // follow a save with exactly this request; a collection's
        // membership changing needs the same treatment for every zone
        // that references it, not just the zone-side attach/detach
        // mutations. Harmless no-op when no zone currently references
        // this collection (the loop below just finds nothing to request).
        private static void RebuildZonesReferencing(Guid collectionId, CustomZones.CustomZoneBuildRequestSystem buildRequestSystem)
        {
            foreach (var definition in Mod.CustomZoneDefinitionLibrary.GetAll())
            {
                if (definition.CollectionIds.Contains(collectionId))
                    buildRequestSystem.Request(definition, ZoneUISystem.ResolveCollections(definition));
            }
        }

        // Centralizes the try/catch every mutation above needs -
        // Mod.CollectionLibrary.Save/Delete have strong exception safety
        // (a failed write rolls back in-memory state and throws, see
        // ICollectionLibrary.cs) but deliberately don't log themselves
        // (Core has no logging dependency - see ARCHITECTURE.md), so this
        // is where a real, previously-uncaught failure (disk full, file
        // locked, permissions denied) actually gets surfaced instead of
        // propagating out of a UI trigger callback. Returns false on
        // failure so the caller skips its dependent RefreshCollections -
        // harmless either way now that the save itself rolled back
        // cleanly, but pointless to refresh with data that didn't change.
        private static bool TrySave(Collection collection, string action)
        {
            try
            {
                Mod.CollectionLibrary.Save(collection);
                return true;
            }
            catch (Exception ex)
            {
                var message = $"Failed to {action} collection '{collection.Id}' (\"{collection.Name}\") - change was not saved.";
                Mod.log.Error(ex, message);
                UIErrorState.Report(message);
                return false;
            }
        }

        private static bool TryDelete(Guid id)
        {
            try
            {
                Mod.CollectionLibrary.Delete(id);
                return true;
            }
            catch (Exception ex)
            {
                var message = $"Failed to delete collection '{id}' - it may still be present on disk.";
                Mod.log.Error(ex, message);
                UIErrorState.Report(message);
                return false;
            }
        }

        private static CollectionUIEntry[] GetEntries()
        {
            return Mod.CollectionLibrary.GetAll()
                .Select(c => new CollectionUIEntry(
                    c.Id.ToString(),
                    c.Name,
                    c.Entries.Count,
                    c.Entries.OfType<ExplicitAssetEntry>().Select(e => e.PrefabId).ToArray()))
                .ToArray();
        }
    }
}

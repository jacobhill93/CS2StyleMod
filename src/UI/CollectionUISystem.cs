using CS2StyleMod.Core;
using System;
using System.Linq;

namespace CS2StyleMod.UI
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

        protected override void OnCreate()
        {
            base.OnCreate();

            m_Collections = CreateBinding("Collections", GetEntries());

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

            RefreshCollections();
        }

        private void RefreshCollections()
        {
            m_Collections.Value = GetEntries();
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
                Mod.log.Error(ex, $"Failed to {action} collection '{collection.Id}' (\"{collection.Name}\") - change was not saved.");
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
                Mod.log.Error(ex, $"Failed to delete collection '{id}' - it may still be present on disk.");
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

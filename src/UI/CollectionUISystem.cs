using Colossal.UI.Binding;
using CS2StyleMod.Core;
using Game.UI;
using System;
using System.Linq;

namespace CS2StyleMod.UI
{
    // Exposes Mod.CollectionLibrary to the UI - list, create, rename,
    // delete. Deliberately minimal first slice: no entry editing yet
    // (adding/removing buildings within a collection, which needs the
    // building picker) - just enough to prove the binding pipeline
    // end-to-end and give the user a real, working piece. Registered
    // UpdateAt<_>(SystemUpdatePhase.UIUpdate) in Mod.cs, same phase every
    // other UI-binding system in the game uses.
    public partial class CollectionUISystem : UISystemBase
    {
        private const string Group = nameof(CS2StyleMod);

        private ValueBinding<CollectionUIEntry[]> m_Collections;

        protected override void OnCreate()
        {
            base.OnCreate();

            // ValueWriters.Create<T[]>() only works for types with a
            // pre-registered writer (primitives) - for a custom struct it
            // reflection-constructs ArrayWriter<T>() with no arguments,
            // which throws (MissingMethodException, confirmed via an
            // actual mod-load crash) since its real constructor needs an
            // explicit element writer. Construct it explicitly instead.
            m_Collections = new ValueBinding<CollectionUIEntry[]>(Group, "Collections", GetEntries(), new ArrayWriter<CollectionUIEntry>(new CollectionUIEntryWriter(), false));
            AddBinding(m_Collections);

            // Takes a client-generated id (chunk 5) rather than letting
            // Collection's own Guid.NewGuid() ctor pick one, so the caller
            // knows which collection was just created without waiting on a
            // round trip through the Collections binding - needed to open
            // the building picker pre-targeted at it immediately.
            AddBinding(new TriggerBinding<string, string>(Group, "CreateCollection", OnCreateCollection, ValueReaders.Create<string>(), ValueReaders.Create<string>()));
            AddBinding(new TriggerBinding<string>(Group, "DeleteCollection", OnDeleteCollection, ValueReaders.Create<string>()));
            AddBinding(new TriggerBinding<string, string>(Group, "RenameCollection", OnRenameCollection, ValueReaders.Create<string>(), ValueReaders.Create<string>()));

            // Called from the building picker (chunk 4), not this panel -
            // lives here anyway since this system already owns every other
            // Collection mutation + the refresh that keeps entryCount
            // current, same reasoning as the other triggers above.
            AddBinding(new TriggerBinding<string, string>(Group, "AddBuildingToCollection", OnAddBuildingToCollection, ValueReaders.Create<string>(), ValueReaders.Create<string>()));
            AddBinding(new TriggerBinding<string, string>(Group, "RemoveBuildingFromCollection", OnRemoveBuildingFromCollection, ValueReaders.Create<string>(), ValueReaders.Create<string>()));
        }

        private void OnCreateCollection(string id, string name)
        {
            if (!Guid.TryParse(id, out var guid) || string.IsNullOrWhiteSpace(name))
                return;

            Mod.CollectionLibrary.Save(new Collection(guid, name));
            RefreshCollections();
        }

        private void OnDeleteCollection(string id)
        {
            if (!Guid.TryParse(id, out var guid))
                return;

            Mod.CollectionLibrary.Delete(guid);
            RefreshCollections();
        }

        private void OnRenameCollection(string id, string newName)
        {
            if (!Guid.TryParse(id, out var guid) || string.IsNullOrWhiteSpace(newName))
                return;
            if (!Mod.CollectionLibrary.TryGet(guid, out var collection))
                return;

            collection.Name = newName;
            Mod.CollectionLibrary.Save(collection);
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
            Mod.CollectionLibrary.Save(collection);
            RefreshCollections();
        }

        private void OnRemoveBuildingFromCollection(string collectionId, string prefabId)
        {
            if (!Guid.TryParse(collectionId, out var guid) || string.IsNullOrWhiteSpace(prefabId))
                return;
            if (!Mod.CollectionLibrary.TryGet(guid, out var collection))
                return;

            collection.RemoveExplicitAsset(prefabId);
            Mod.CollectionLibrary.Save(collection);
            RefreshCollections();
        }

        private void RefreshCollections()
        {
            m_Collections.Update(GetEntries());
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

        // Explicit, not auto-resolved - same reasoning as the ArrayWriter
        // above, kept separate in case CollectionUIEntry ever needs
        // writing on its own (not just inside an array) elsewhere.
        private sealed class CollectionUIEntryWriter : IWriter<CollectionUIEntry>
        {
            public void Write(IJsonWriter writer, CollectionUIEntry value) => value.Write(writer);
        }
    }
}

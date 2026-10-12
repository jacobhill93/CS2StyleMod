using System;
using System.Collections.Generic;

namespace ZoneBuilder.Core
{
    // A named, user-defined set of buildings - the shared building block
    // behind both Custom Zones and District Themes. Identity is Id, not
    // Name: saves reference a Collection by Id, so renaming never breaks
    // anything already using it - see DECISIONS.md.
    public sealed class Collection
    {
        public Guid Id { get; }
        public string Name { get; set; }

        private readonly List<CollectionEntry> m_Entries = new List<CollectionEntry>();
        public IReadOnlyList<CollectionEntry> Entries => m_Entries;

        public Collection(string name) : this(Guid.NewGuid(), name)
        {
        }

        public Collection(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public void Add(CollectionEntry entry)
        {
            m_Entries.Add(entry);
        }

        // Scoped to ExplicitAssetEntry specifically (not a generic Remove)
        // since that's the only entry kind the building picker adds - a
        // VanillaSelectorEntry/AdoptedAssetEntry doesn't name one specific
        // prefab the same way, so "remove this building" doesn't apply to
        // them the same way.
        public void RemoveExplicitAsset(string prefabId)
        {
            m_Entries.RemoveAll(e => e is ExplicitAssetEntry explicitEntry && explicitEntry.PrefabId == prefabId);
        }

        // Shallow copy - CollectionEntry subtypes are effectively
        // immutable after construction (nothing mutates an entry's own
        // fields in place, only adds/removes whole entries), so sharing
        // entry references between the original and the clone is safe.
        // Exists so ICollectionLibrary.TryGet can hand out a value the
        // caller can freely mutate without that mutation silently
        // touching the library's own stored state before Save is called -
        // see JsonFileCollectionLibrary.TryGet's comment for why this
        // matters (it's what makes Save's rollback-on-failure actually
        // correct, not just structurally present).
        public Collection Clone()
        {
            var clone = new Collection(Id, Name);
            foreach (var entry in m_Entries)
                clone.Add(entry);
            return clone;
        }
    }
}

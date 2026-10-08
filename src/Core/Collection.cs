using System;
using System.Collections.Generic;

namespace CS2StyleMod.Core
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
    }
}

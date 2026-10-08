using System;
using System.Collections.Generic;

namespace CS2StyleMod.Core
{
    // A persisted, user-defined custom zone: a base vanilla zone category
    // plus the Collection(s) whose buildings it should offer. Identity is
    // Id, not Name - same rename-free principle as Collection, and for the
    // same reason: the in-game zone/building prefabs this eventually builds
    // need a stable name derived from Id, not from the user-facing Name, so
    // renaming a definition later doesn't orphan what was already built -
    // see DECISIONS.md.
    public sealed class CustomZoneDefinition
    {
        public Guid Id { get; }
        public string Name { get; set; }
        public ZoneType BaseZoneType { get; set; }

        private readonly List<Guid> m_CollectionIds = new List<Guid>();
        public IReadOnlyList<Guid> CollectionIds => m_CollectionIds;

        public CustomZoneDefinition(string name, ZoneType baseZoneType)
            : this(Guid.NewGuid(), name, baseZoneType)
        {
        }

        public CustomZoneDefinition(Guid id, string name, ZoneType baseZoneType)
        {
            Id = id;
            Name = name;
            BaseZoneType = baseZoneType;
        }

        public void AddCollection(Guid collectionId)
        {
            if (!m_CollectionIds.Contains(collectionId))
                m_CollectionIds.Add(collectionId);
        }

        public void RemoveCollection(Guid collectionId)
        {
            m_CollectionIds.Remove(collectionId);
        }
    }
}

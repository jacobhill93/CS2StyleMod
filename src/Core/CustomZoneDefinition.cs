using System;
using System.Collections.Generic;

namespace ZoneBuilder.Core
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

        // Which vanilla theme variant this zone's simulation template was
        // (or will be) cloned from - see Core.ZoneRegion for what this
        // does and doesn't affect. Defaults to Europe, matching the
        // behavior every zone had before this existed (GameZonePrefabResolver
        // picked whatever candidate its entity query happened to return
        // first, which was observed to always be an EU-themed one).
        public ZoneRegion Region { get; set; }

        private readonly List<Guid> m_CollectionIds = new List<Guid>();
        public IReadOnlyList<Guid> CollectionIds => m_CollectionIds;

        public CustomZoneDefinition(string name, ZoneType baseZoneType, ZoneRegion region = ZoneRegion.Europe)
            : this(Guid.NewGuid(), name, baseZoneType, region)
        {
        }

        public CustomZoneDefinition(Guid id, string name, ZoneType baseZoneType, ZoneRegion region = ZoneRegion.Europe)
        {
            Id = id;
            Name = name;
            BaseZoneType = baseZoneType;
            Region = region;
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

        // Shallow copy (CollectionIds are Guids, value types - a shallow
        // copy of the list is a true independent copy). Exists so
        // ICustomZoneDefinitionLibrary.TryGet can hand out a value the
        // caller can freely mutate without that mutation silently
        // touching the library's own stored state before Save is called -
        // see Collection.Clone's identical comment for the full reasoning.
        public CustomZoneDefinition Clone()
        {
            var clone = new CustomZoneDefinition(Id, Name, BaseZoneType, Region);
            foreach (var collectionId in m_CollectionIds)
                clone.AddCollection(collectionId);
            return clone;
        }
    }
}

using Colossal.UI.Binding;

namespace ZoneBuilder.UI
{
    // Bound value for "this collection has no Level 1 building for one or
    // more of its own zone types" - see CollectionUISystem.
    // OnFinishEditingCollectionAssets and CustomZoneBuilder.
    // FindZoneTypesMissingLevel1's own comments for why this fires when
    // the user finishes editing a collection's assets, not when a
    // collection gets attached to a zone (DECISIONS.md "Level 1 warning
    // moved to the collection, not the zone attach"). Purely informational,
    // not a confirm/cancel gate - the add/remove that triggered it already
    // committed, same as every other collection mutation.
    public readonly struct CollectionLevel1WarningEntry : IJsonWritable
    {
        public static readonly CollectionLevel1WarningEntry Inactive = new CollectionLevel1WarningEntry(false, "", "", System.Array.Empty<string>());

        private readonly bool m_Active;
        private readonly string m_CollectionId;
        private readonly string m_CollectionName;
        private readonly string[] m_MissingZoneTypes;

        public CollectionLevel1WarningEntry(bool active, string collectionId, string collectionName, string[] missingZoneTypes)
        {
            m_Active = active;
            m_CollectionId = collectionId;
            m_CollectionName = collectionName;
            m_MissingZoneTypes = missingZoneTypes;
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("active");
            writer.Write(m_Active);
            writer.PropertyName("collectionId");
            writer.Write(m_CollectionId);
            writer.PropertyName("collectionName");
            writer.Write(m_CollectionName);
            writer.PropertyName("missingZoneTypes");
            writer.ArrayBegin((uint)m_MissingZoneTypes.Length);
            foreach (var zoneType in m_MissingZoneTypes)
                writer.Write(zoneType);
            writer.ArrayEnd();
            writer.TypeEnd();
        }
    }
}

using Colossal.UI.Binding;

namespace ZoneBuilder.UI
{
    // Bound value for the "this collection has no Level 1 building"
    // confirmation prompt - see ZoneUISystem.OnAddCollectionToZone and
    // CustomZoneBuilder.HasLevel1Candidate's own comments for why this
    // check exists. Same hand-written IJsonWritable reasoning as
    // ZoneUIEntry (camelCase JSON key preservation).
    public readonly struct Level1WarningEntry : IJsonWritable
    {
        public static readonly Level1WarningEntry Inactive = new Level1WarningEntry(false, "", "", "", "");

        private readonly bool m_Active;
        private readonly string m_ZoneId;
        private readonly string m_ZoneName;
        private readonly string m_CollectionId;
        private readonly string m_CollectionName;

        public Level1WarningEntry(bool active, string zoneId, string zoneName, string collectionId, string collectionName)
        {
            m_Active = active;
            m_ZoneId = zoneId;
            m_ZoneName = zoneName;
            m_CollectionId = collectionId;
            m_CollectionName = collectionName;
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("active");
            writer.Write(m_Active);
            writer.PropertyName("zoneId");
            writer.Write(m_ZoneId);
            writer.PropertyName("zoneName");
            writer.Write(m_ZoneName);
            writer.PropertyName("collectionId");
            writer.Write(m_CollectionId);
            writer.PropertyName("collectionName");
            writer.Write(m_CollectionName);
            writer.TypeEnd();
        }
    }
}

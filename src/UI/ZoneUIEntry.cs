using Colossal.UI.Binding;

namespace ZoneBuilder.UI
{
    // Plain DTO for the UI, mirrors CollectionUIEntry's role for
    // CustomZoneDefinition. collectionIds (not just a count) is needed so
    // the attach/detach panel can show which collections are already
    // attached to this zone.
    public readonly struct ZoneUIEntry : IJsonWritable
    {
        private readonly string m_Id;
        private readonly string m_Name;
        private readonly string m_BaseZoneType;
        private readonly string m_Region;
        private readonly string[] m_CollectionIds;

        public ZoneUIEntry(string id, string name, string baseZoneType, string region, string[] collectionIds)
        {
            m_Id = id;
            m_Name = name;
            m_BaseZoneType = baseZoneType;
            m_Region = region;
            m_CollectionIds = collectionIds;
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("id");
            writer.Write(m_Id);
            writer.PropertyName("name");
            writer.Write(m_Name);
            writer.PropertyName("baseZoneType");
            writer.Write(m_BaseZoneType);
            writer.PropertyName("region");
            writer.Write(m_Region);
            writer.PropertyName("collectionIds");
            writer.ArrayBegin((uint)m_CollectionIds.Length);
            foreach (var id in m_CollectionIds)
                writer.Write(id);
            writer.ArrayEnd();
            writer.TypeEnd();
        }
    }
}

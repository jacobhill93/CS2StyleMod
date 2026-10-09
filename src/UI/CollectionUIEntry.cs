using Colossal.UI.Binding;

namespace CS2StyleMod.UI
{
    // Plain DTO for the UI - Core.Collection itself never crosses the
    // binding boundary (Id serializes as a string; JS has no Guid type).
    public readonly struct CollectionUIEntry : IJsonWritable
    {
        private readonly string m_Id;
        private readonly string m_Name;
        private readonly int m_EntryCount;
        private readonly string[] m_PrefabIds;

        // Just the ExplicitAssetEntry.PrefabIds (not every entry - a
        // Collection can also hold VanillaSelectorEntry/AdoptedAssetEntry,
        // which don't name one specific building the same way). Lets the
        // building picker show "already in this collection" per row
        // without a separate round trip.
        public CollectionUIEntry(string id, string name, int entryCount, string[] prefabIds)
        {
            m_Id = id;
            m_Name = name;
            m_EntryCount = entryCount;
            m_PrefabIds = prefabIds;
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("id");
            writer.Write(m_Id);
            writer.PropertyName("name");
            writer.Write(m_Name);
            writer.PropertyName("entryCount");
            writer.Write(m_EntryCount);
            writer.PropertyName("prefabIds");
            writer.ArrayBegin((uint)m_PrefabIds.Length);
            foreach (var prefabId in m_PrefabIds)
                writer.Write(prefabId);
            writer.ArrayEnd();
            writer.TypeEnd();
        }
    }
}

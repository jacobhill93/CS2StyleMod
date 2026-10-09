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

        public CollectionUIEntry(string id, string name, int entryCount)
        {
            m_Id = id;
            m_Name = name;
            m_EntryCount = entryCount;
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
            writer.TypeEnd();
        }
    }
}

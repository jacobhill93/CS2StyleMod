using Colossal.UI.Binding;

namespace CS2StyleMod.UI
{
    // Plain DTO for the UI, mirrors GameAdapters.BuildingBrowserEntry across
    // the binding boundary. assetPackIds is written as a real JSON array
    // (ArrayBegin/ArrayEnd, not a joined string) so client-side filtering
    // (planned next) can match pack ids exactly rather than substring a
    // display string.
    public readonly struct BuildingPickerEntry : IJsonWritable
    {
        private readonly string m_PrefabId;
        private readonly string m_ZoneType;
        private readonly int m_Level;
        private readonly int m_LotWidth;
        private readonly int m_LotDepth;
        private readonly float m_HeightMeters;
        private readonly string[] m_AssetPackIds;

        public BuildingPickerEntry(string prefabId, string zoneType, int level, int lotWidth, int lotDepth, float heightMeters, string[] assetPackIds)
        {
            m_PrefabId = prefabId;
            m_ZoneType = zoneType;
            m_Level = level;
            m_LotWidth = lotWidth;
            m_LotDepth = lotDepth;
            m_HeightMeters = heightMeters;
            m_AssetPackIds = assetPackIds;
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("prefabId");
            writer.Write(m_PrefabId);
            writer.PropertyName("zoneType");
            writer.Write(m_ZoneType);
            writer.PropertyName("level");
            writer.Write(m_Level);
            writer.PropertyName("lotWidth");
            writer.Write(m_LotWidth);
            writer.PropertyName("lotDepth");
            writer.Write(m_LotDepth);
            writer.PropertyName("heightMeters");
            writer.Write(m_HeightMeters);
            writer.PropertyName("assetPackIds");
            writer.ArrayBegin((uint)m_AssetPackIds.Length);
            foreach (var id in m_AssetPackIds)
                writer.Write(id);
            writer.ArrayEnd();
            writer.TypeEnd();
        }
    }
}

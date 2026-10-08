namespace CS2StyleMod.Core
{
    // A plain, game-independent description of a building that could spawn on
    // a lot - resolved from a CollectionEntry by an IBuildingCatalog.
    public sealed class BuildingCandidate
    {
        public string PrefabId { get; }
        public ZoneType ZoneType { get; }
        public int Level { get; }
        public LotSize LotSize { get; }
        public string AssetPackId { get; }

        public BuildingCandidate(string prefabId, ZoneType zoneType, int level, LotSize lotSize, string assetPackId = null)
        {
            PrefabId = prefabId;
            ZoneType = zoneType;
            Level = level;
            LotSize = lotSize;
            AssetPackId = assetPackId;
        }
    }
}

namespace CS2StyleMod.Core
{
    // Mod-supplied growable metadata for a building that isn't growable on its
    // own (e.g. a downloaded house with no zone), so it can spawn naturally
    // once included in a collection.
    public sealed class AdoptedAssetSettings
    {
        public ZoneType Category { get; }
        public int Level { get; }
        public LotSize LotSize { get; }
        public int? HouseholdCount { get; }

        public AdoptedAssetSettings(ZoneType category, int level, LotSize lotSize, int? householdCount = null)
        {
            Category = category;
            Level = level;
            LotSize = lotSize;
            HouseholdCount = householdCount;
        }
    }
}

using CS2StyleMod.Core;

namespace CS2StyleMod.CustomZones.GameAdapters
{
    // Richer than Core.BuildingCandidate on purpose - this is pure browse/
    // display data for the building picker UI (name, zone/level/lot size
    // for filtering, height for the still-open "size" filter question -
    // see DECISIONS.md, asset pack ids for that filter), not anything
    // Core.CandidateFilter's eligibility matching needs. Kept out of Core
    // for that reason: it's a GameAdapters-level concern, not game-
    // independent logic.
    public readonly struct BuildingBrowserEntry
    {
        public readonly string PrefabId;
        public readonly ZoneType ZoneType;
        public readonly int Level;
        public readonly LotSize LotSize;
        public readonly float HeightMeters;
        public readonly string[] AssetPackIds;

        public BuildingBrowserEntry(string prefabId, ZoneType zoneType, int level, LotSize lotSize, float heightMeters, string[] assetPackIds)
        {
            PrefabId = prefabId;
            ZoneType = zoneType;
            Level = level;
            LotSize = lotSize;
            HeightMeters = heightMeters;
            AssetPackIds = assetPackIds;
        }
    }
}

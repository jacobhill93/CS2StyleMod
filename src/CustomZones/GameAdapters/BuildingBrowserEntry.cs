using ZoneBuilder.Core;

namespace ZoneBuilder.CustomZones.GameAdapters
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
        // UIObject.m_Icon, empty string if unset - see GameBuildingCatalog
        // for why this is real per-building data (not a guess): vanilla
        // only populates this per zone/category, but Asset Icon Library
        // (if installed) writes a real coui://ail/... thumbnail URL into
        // this exact field per building it covers. Empty means "no icon
        // available" (no AIL, or AIL doesn't cover this prefab) - the UI
        // should render nothing, not a broken image, when this is empty.
        public readonly string IconPath;

        public BuildingBrowserEntry(string prefabId, ZoneType zoneType, int level, LotSize lotSize, float heightMeters, string[] assetPackIds, string iconPath)
        {
            PrefabId = prefabId;
            ZoneType = zoneType;
            Level = level;
            LotSize = lotSize;
            HeightMeters = heightMeters;
            AssetPackIds = assetPackIds;
            IconPath = iconPath;
        }
    }
}

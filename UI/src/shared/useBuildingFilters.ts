import { useMemo, useState } from "react";
import { ANY } from "shared/FilterDropdown";
import { sortZoneTypes } from "shared/zoneTypeLabels";

export interface BuildingEntry {
    prefabId: string;
    zoneType: string;
    level: number;
    lotWidth: number;
    lotDepth: number;
    heightMeters: number;
    assetPackIds: string[];
    // UIObject.m_Icon, empty string if unset - see GameBuildingCatalog.cs
    // (ResolveIconPath) for the full story. Empty means "no icon
    // available" (no Asset Icon Library installed, or it doesn't cover
    // this prefab) - render nothing, not a broken <img>, when empty.
    iconPath: string;
}

// Shared by the Phase 4 asset-picker dialog and the Phase 4b standalone
// Assets tab (UI_OVERHAUL_PLAN.md decision #2: same filter/search
// machinery, two different jobs - picking vs. browsing) - extracted out
// of the original BuildingPicker.tsx so both consumers stay in sync
// instead of carrying two copies of the same filtering logic.
export function useBuildingFilters(buildings: BuildingEntry[]) {
    const [nameFilter, setNameFilter] = useState("");
    const [zoneTypeFilter, setZoneTypeFilter] = useState(ANY);
    const [levelFilter, setLevelFilter] = useState(ANY);
    const [lotWidthFilter, setLotWidthFilter] = useState(ANY);
    const [lotDepthFilter, setLotDepthFilter] = useState(ANY);
    const [assetPackFilter, setAssetPackFilter] = useState(ANY);

    const zoneTypes = useMemo(() => sortZoneTypes(distinctStrings(buildings.map((b) => b.zoneType))), [buildings]);
    const levels = useMemo(() => distinctNumbers(buildings.map((b) => b.level)).map(String), [buildings]);
    const lotWidths = useMemo(() => distinctNumbers(buildings.map((b) => b.lotWidth)).map(String), [buildings]);
    const lotDepths = useMemo(() => distinctNumbers(buildings.map((b) => b.lotDepth)).map(String), [buildings]);
    const assetPacks = useMemo(() => distinctStrings(buildings.flatMap((b) => b.assetPackIds)), [buildings]);

    const filtered = useMemo(() => {
        const name = nameFilter.trim().toLowerCase();
        return buildings.filter((b) => {
            if (name && !b.prefabId.toLowerCase().includes(name)) return false;
            if (zoneTypeFilter && b.zoneType !== zoneTypeFilter) return false;
            if (levelFilter && b.level !== Number(levelFilter)) return false;
            if (lotWidthFilter && b.lotWidth !== Number(lotWidthFilter)) return false;
            if (lotDepthFilter && b.lotDepth !== Number(lotDepthFilter)) return false;
            if (assetPackFilter && !b.assetPackIds.includes(assetPackFilter)) return false;
            return true;
        });
    }, [buildings, nameFilter, zoneTypeFilter, levelFilter, lotWidthFilter, lotDepthFilter, assetPackFilter]);

    const clearFilters = () => {
        setNameFilter("");
        setZoneTypeFilter(ANY);
        setLevelFilter(ANY);
        setLotWidthFilter(ANY);
        setLotDepthFilter(ANY);
        setAssetPackFilter(ANY);
    };

    return {
        filtered,
        zoneTypes,
        levels,
        lotWidths,
        lotDepths,
        assetPacks,
        nameFilter,
        setNameFilter,
        zoneTypeFilter,
        setZoneTypeFilter,
        levelFilter,
        setLevelFilter,
        lotWidthFilter,
        setLotWidthFilter,
        lotDepthFilter,
        setLotDepthFilter,
        assetPackFilter,
        setAssetPackFilter,
        clearFilters,
    };
}

function distinctStrings(values: string[]): string[] {
    return Array.from(new Set(values)).sort();
}

function distinctNumbers(values: number[]): number[] {
    return Array.from(new Set(values)).sort((a, b) => a - b);
}

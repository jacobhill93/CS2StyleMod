import { Button } from "cs2/ui";
import { ANY, FilterDropdown } from "shared/FilterDropdown";
import { SearchBox } from "shared/SearchBox";
import styles from "./BuildingFilterBar.module.scss";
import { useBuildingFilters } from "shared/useBuildingFilters";

// The render half of useBuildingFilters - split out so both the Phase 4
// picker dialog and the Phase 4b Assets tab render an identical filter
// bar, not two hand-copied versions of the same markup. Search gets its
// own row (2026-10-11, user feedback in-game: it was too constrained
// sharing a row with five dropdowns + Clear) - the dropdowns/Clear sit
// on a second row below it.
export const BuildingFilterBar = (filters: ReturnType<typeof useBuildingFilters>) => {
    const anyActive = [
        filters.zoneTypeFilter,
        filters.levelFilter,
        filters.lotWidthFilter,
        filters.lotDepthFilter,
        filters.assetPackFilter,
    ].some((value) => value !== ANY);

    return (
        <div className={styles.bar}>
            <SearchBox value={filters.nameFilter} onChange={filters.setNameFilter} placeholder="Search buildings..." />
            <div className={styles.dropdowns}>
                <FilterDropdown label="Any zone" options={filters.zoneTypes} value={filters.zoneTypeFilter} onChange={filters.setZoneTypeFilter} />
                <FilterDropdown label="Any level" options={filters.levels} value={filters.levelFilter} onChange={filters.setLevelFilter} formatOption={(v) => `L${v}`} />
                <FilterDropdown label="Any width" options={filters.lotWidths} value={filters.lotWidthFilter} onChange={filters.setLotWidthFilter} formatOption={(v) => `${v} wide`} />
                <FilterDropdown label="Any depth" options={filters.lotDepths} value={filters.lotDepthFilter} onChange={filters.setLotDepthFilter} formatOption={(v) => `${v} deep`} />
                <FilterDropdown label="Any pack" options={filters.assetPacks} value={filters.assetPackFilter} onChange={filters.setAssetPackFilter} />
                <Button variant="flat" className={styles.clear} disabled={!anyActive} onSelect={filters.clearFilters}>
                    Clear filters
                </Button>
            </div>
        </div>
    );
};

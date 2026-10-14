import { Button } from "cs2/ui";
import classNames from "classnames";
import { FilterDropdown } from "shared/FilterDropdown";
import { useBuildingFilters } from "shared/useBuildingFilters";
import { TextInputTheme } from "vanilla/TextInputTheme";
import inputStyles from "shared/TextInput.module.scss";
import styles from "shared/BuildingFilterBar.module.scss";

// The render half of useBuildingFilters - split out so both the Phase 4
// picker dialog and the Phase 4b Assets tab render an identical filter
// bar, not two hand-copied versions of the same markup. Search gets its
// own row (2026-10-11, user feedback in-game: it was too constrained
// sharing a row with five dropdowns + Clear) - the dropdowns/Clear sit
// on a second row below it.
export const BuildingFilterBar = (filters: ReturnType<typeof useBuildingFilters>) => (
    <div className={styles.wrapper}>
        <input
            value={filters.nameFilter}
            onChange={(event) => filters.setNameFilter(event.target.value)}
            placeholder="Search name"
            className={classNames(TextInputTheme.input, inputStyles.input, styles.searchInput)}
        />
        <div className={styles.filterRow}>
            <FilterDropdown label="Any zone" options={filters.zoneTypes} value={filters.zoneTypeFilter} onChange={filters.setZoneTypeFilter} />
            <FilterDropdown label="Any level" options={filters.levels} value={filters.levelFilter} onChange={filters.setLevelFilter} formatOption={(v) => `L${v}`} />
            <FilterDropdown label="Any width" options={filters.lotWidths} value={filters.lotWidthFilter} onChange={filters.setLotWidthFilter} formatOption={(v) => `${v} wide`} />
            <FilterDropdown label="Any depth" options={filters.lotDepths} value={filters.lotDepthFilter} onChange={filters.setLotDepthFilter} formatOption={(v) => `${v} deep`} />
            <FilterDropdown label="Any pack" options={filters.assetPacks} value={filters.assetPackFilter} onChange={filters.setAssetPackFilter} />
            <Button onSelect={filters.clearFilters}>Clear</Button>
        </div>
    </div>
);

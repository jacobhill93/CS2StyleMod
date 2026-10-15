import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Scrollable } from "cs2/ui";
import { useMemo, useState } from "react";
import mod from "../../../../mod.json";
import { Icons } from "shared/icons";
import { BuildingEntry, useBuildingFilters } from "shared/useBuildingFilters";
import { BuildingFilterBar } from "shared/BuildingFilterBar";
import rowStyles from "shared/ListRow.module.scss";
import { BuildingThumbnail } from "shared/BuildingThumbnail";
import { useVirtualizedRows } from "shared/useVirtualizedRows";
import { CollectionEntry } from "shared/CollectionEntry";
import { TextButton } from "shared/TextButton";
import { AssetsTileGrid } from "./AssetsTileGrid";
import tabStyles from "./AssetsTab.module.scss";

// Must match the real rendered row height: ListRow.module.scss's .row
// padding (8px top + 8px bottom = base.$space-md each) plus
// BuildingThumbnail's 48px size = 64px. Kept in sync by hand, not
// measured - see useVirtualizedRows.ts's own comment for why.
const ROW_HEIGHT = 64;
const LIST_VIEWPORT_HEIGHT = 340;

const Buildings$ = bindValue<BuildingEntry[]>(mod.id, "Buildings", []);
const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);

interface AssetsTabProps {
    // Set by ShellFeature when arriving here via a Collection's
    // "+ Add Assets" - switches this tab from plain browsing into a
    // picker scoped to that collection (Add/Remove per row, Select All/
    // Unselect All, a "Done" button back to the collection). Absent when
    // the tab is opened directly from the tab bar, which stays the
    // original browse-only view (decision #2's original split between a
    // standalone Assets tab and a picker dialog, now expressed as one
    // component with two modes instead of two separate components/
    // panels - see CollectionsTab.tsx's comment for why).
    pickingForCollectionId?: string | null;
    onDonePicking?: () => void;
}

// UI_UPGRADE_GUIDE.md Phase 4c - grid confirmed in-game (2026-10-13,
// after several rounds of spacing/label fixes) and preferred over the
// row list, so it's now the default; "list" stays available via the
// toggle for anyone who wants it, and the row-list code path itself
// is still completely untouched by this toggle existing.
type ViewMode = "list" | "grid";

export const AssetsTab = ({ pickingForCollectionId, onDonePicking }: AssetsTabProps) => {
    const buildings = useValue(Buildings$);
    const collections = useValue(Collections$);
    const filters = useBuildingFilters(buildings);
    const { filtered } = filters;
    const [viewMode, setViewMode] = useState<ViewMode>("grid");

    const pickingCollection = pickingForCollectionId
        ? collections.find((c) => c.id === pickingForCollectionId)
        : undefined;
    const memberPrefabIds = useMemo(() => new Set(pickingCollection?.prefabIds ?? []), [pickingCollection]);

    const { scrollRef, onScroll, startIndex, endIndex, totalHeight } = useVirtualizedRows({
        itemCount: filtered.length,
        rowHeight: ROW_HEIGHT,
        viewportHeight: LIST_VIEWPORT_HEIGHT,
    });
    const visibleRows = filtered.slice(startIndex, endIndex);

    const onSelectAllFiltered = () => {
        if (!pickingForCollectionId) return;
        filtered.forEach((b) => {
            if (!memberPrefabIds.has(b.prefabId)) trigger(mod.id, "AddBuildingToCollection", pickingForCollectionId, b.prefabId);
        });
    };

    const onUnselectAllFiltered = () => {
        if (!pickingForCollectionId) return;
        filtered.forEach((b) => {
            if (memberPrefabIds.has(b.prefabId)) trigger(mod.id, "RemoveBuildingFromCollection", pickingForCollectionId, b.prefabId);
        });
    };

    return (
        <>
            {pickingForCollectionId && (
                <div className={rowStyles.detailHeader}>
                    <TextButton onSelect={onDonePicking}>Done</TextButton>
                    <span className={rowStyles.detailTitle}>Adding to "{pickingCollection?.name ?? ""}"</span>
                </div>
            )}
            <BuildingFilterBar {...filters} />
            <div className={tabStyles.countRow}>
                {/* Explicit {" "} rather than a plain space before "shown" -
                plain inter-expression whitespace here was rendering with no
                visible gap in-game (2026-10-13), a Gameface text-layout
                quirk, not a real JSX whitespace-trimming bug (this line's
                space isn't at a line boundary, so standard JSX rules keep
                it). */}
                <span className={tabStyles.countText}>
                    {filtered.length}/{buildings.length}{" "}shown
                </span>
                {pickingForCollectionId && (
                    <>
                        <TextButton onSelect={onSelectAllFiltered}>Select All</TextButton>
                        <TextButton onSelect={onUnselectAllFiltered}>Unselect All</TextButton>
                    </>
                )}
                {/* Side-by-side pair, whichever's active highlighted - same
                pattern as ZonesTab's NA/EU region toggle, swapped in for
                the single label-swapping button this used to be (user
                request, 2026-10-14). */}
                <TextButton selected={viewMode === "grid"} onSelect={() => setViewMode("grid")}>
                    Grid
                </TextButton>
                <TextButton selected={viewMode === "list"} onSelect={() => setViewMode("list")}>
                    List
                </TextButton>
            </div>
            {filtered.length === 0 && (
                <div className={tabStyles.emptyStateWithAction}>
                    <span className={rowStyles.emptyState}>No buildings match. Try a different search or filter.</span>
                    <TextButton onSelect={filters.clearFilters}>Clear filters</TextButton>
                </div>
            )}
            {filtered.length > 0 && viewMode === "grid" && (
                <AssetsTileGrid
                    buildings={filtered}
                    viewportHeight={LIST_VIEWPORT_HEIGHT}
                    pickingForCollectionId={pickingForCollectionId}
                    memberPrefabIds={memberPrefabIds}
                />
            )}
            {filtered.length > 0 && viewMode === "list" && (
                <Scrollable vertical ref={scrollRef} onScroll={onScroll} style={{ height: `${LIST_VIEWPORT_HEIGHT}px` }}>
                    <div style={{ position: "relative", height: `${totalHeight}px` }}>
                        {visibleRows.map((building, i) => {
                            const alreadyAdded = memberPrefabIds.has(building.prefabId);
                            const top = (startIndex + i) * ROW_HEIGHT;
                            return (
                                <div
                                    key={building.prefabId}
                                    className={rowStyles.row}
                                    style={{ position: "absolute", top: `${top}px`, left: 0, right: 0, boxSizing: "border-box" }}
                                >
                                    <BuildingThumbnail iconPath={building.iconPath} />
                                    <span className={rowStyles.primary}>{building.prefabId}</span>
                                    <span className={rowStyles.secondary}>
                                        {building.zoneType} L{building.level} {building.lotWidth}x{building.lotDepth} [{building.assetPackIds.join(", ")}]
                                    </span>
                                    {pickingForCollectionId && (
                                        <Button
                                            variant="icon"
                                            style={{ flexShrink: 0 }}
                                            src={alreadyAdded ? Icons.trash : Icons.plus}
                                            tooltipLabel={alreadyAdded ? "Remove" : "Add"}
                                            selected={alreadyAdded}
                                            onSelect={() =>
                                                trigger(mod.id, alreadyAdded ? "RemoveBuildingFromCollection" : "AddBuildingToCollection", pickingForCollectionId, building.prefabId)
                                            }
                                        />
                                    )}
                                </div>
                            );
                        })}
                    </div>
                </Scrollable>
            )}
        </>
    );
};

import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Scrollable } from "cs2/ui";
import { useMemo } from "react";
import mod from "../../../../mod.json";
import { Icons } from "shared/icons";
import { BuildingEntry, useBuildingFilters } from "shared/useBuildingFilters";
import { BuildingFilterBar } from "shared/BuildingFilterBar";
import rowStyles from "shared/ListRow.module.scss";
import { BuildingThumbnail } from "shared/BuildingThumbnail";
import { useVirtualizedRows } from "shared/useVirtualizedRows";

// Must match the real rendered row height: ListRow.module.scss's .row
// padding (8px top + 8px bottom = base.$space-md each) plus
// BuildingThumbnail's 48px size = 64px. Kept in sync by hand, not
// measured - see useVirtualizedRows.ts's own comment for why.
const ROW_HEIGHT = 64;
const LIST_VIEWPORT_HEIGHT = 340;

interface CollectionEntry {
    id: string;
    name: string;
    entryCount: number;
    prefabIds: string[];
}

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

export const AssetsTab = ({ pickingForCollectionId, onDonePicking }: AssetsTabProps) => {
    const buildings = useValue(Buildings$);
    const collections = useValue(Collections$);
    const filters = useBuildingFilters(buildings);
    const { filtered } = filters;

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
                <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "8px" }}>
                    <Button onSelect={onDonePicking}>Done</Button>
                    <span style={{ fontSize: "14px", fontWeight: "bold", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                        Adding to "{pickingCollection?.name ?? ""}"
                    </span>
                </div>
            )}
            <BuildingFilterBar {...filters} />
            <div style={{ display: "flex", alignItems: "center", gap: "4px", marginBottom: "8px" }}>
                <span style={{ fontSize: "13px", opacity: 0.8 }}>
                    {filtered.length}/{buildings.length} shown
                </span>
                {pickingForCollectionId && (
                    <>
                        <Button onSelect={onSelectAllFiltered}>Select All</Button>
                        <Button onSelect={onUnselectAllFiltered}>Unselect All</Button>
                    </>
                )}
            </div>
            <Scrollable vertical ref={scrollRef} onScroll={onScroll} style={{ height: `${LIST_VIEWPORT_HEIGHT}px` }}>
                {filtered.length === 0 && (
                    <div style={{ display: "flex", flexDirection: "column", gap: "6px", alignItems: "flex-start" }}>
                        <span className={rowStyles.emptyState}>No buildings match. Try a different search or filter.</span>
                        <Button onSelect={filters.clearFilters}>Clear filters</Button>
                    </div>
                )}
                {filtered.length > 0 && (
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
                )}
            </Scrollable>
        </>
    );
};

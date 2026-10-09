import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useMemo, useState } from "react";
import mod from "../../../mod.json";
import { ANY, FilterDropdown } from "shared/FilterDropdown";
import { useDraggable } from "shared/useDraggable";
import { DraggableHeader } from "shared/DraggableHeader";
import { useResizable } from "shared/useResizable";
import { ResizeHandle } from "shared/ResizeHandle";

interface BuildingEntry {
    prefabId: string;
    zoneType: string;
    level: number;
    lotWidth: number;
    lotDepth: number;
    heightMeters: number;
    assetPackIds: string[];
}

interface CollectionEntry {
    id: string;
    name: string;
    entryCount: number;
    prefabIds: string[];
}

const Buildings$ = bindValue<BuildingEntry[]>(mod.id, "Buildings", []);
// Same binding CollectionPanel reads - broadcasts aren't exclusive to one
// panel, and the picker needs to know which collections exist to offer as
// an "add to" target.
const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);

interface BuildingPickerProps {
    // Chunk 5: opened from CollectionPanel's "Create", pre-targeted at the
    // collection that was just created - the dropdown below still lets the
    // user retarget to a different existing collection without reopening.
    initialTargetCollectionId?: string;
    onClose?: () => void;
}

// All filtering is client-side over the one full list the backend sends
// (same approach Find It uses) - no server-side query, no refetch per
// keystroke. Not included: a "building size (low/med/high)" filter - still
// an open question (see DECISIONS.md "Building picker 'size' filter"),
// deferred rather than guessed at.
export const BuildingPicker = ({ initialTargetCollectionId, onClose }: BuildingPickerProps = {}) => {
    const buildings = useValue(Buildings$);
    const collections = useValue(Collections$);

    const [nameFilter, setNameFilter] = useState("");
    const [zoneTypeFilter, setZoneTypeFilter] = useState(ANY);
    const [levelFilter, setLevelFilter] = useState(ANY);
    const [lotWidthFilter, setLotWidthFilter] = useState(ANY);
    const [lotDepthFilter, setLotDepthFilter] = useState(ANY);
    const [assetPackFilter, setAssetPackFilter] = useState(ANY);
    const [targetCollectionId, setTargetCollectionId] = useState(initialTargetCollectionId ?? ANY);
    const { position, onHandleMouseDown: onDragMouseDown } = useDraggable({ x: 440, y: 100 });
    const { size, onHandleMouseDown: onResizeMouseDown } = useResizable({ width: 480, height: 520 });

    const collectionNameById = useMemo(() => {
        const map = new Map<string, string>();
        collections.forEach((c) => map.set(c.id, c.name));
        return map;
    }, [collections]);

    // Real state, not a transient "just clicked" flag - reflects whether
    // the building is actually in the targeted collection right now, so it
    // stays correct across retargeting, filtering, and reopening the
    // picker later, not just immediately after a click.
    const targetCollectionPrefabIds = useMemo(() => {
        const target = collections.find((c) => c.id === targetCollectionId);
        return new Set(target?.prefabIds ?? []);
    }, [collections, targetCollectionId]);

    const zoneTypes = useMemo(() => distinctStrings(buildings.map((b) => b.zoneType)), [buildings]);
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

    return (
        <div style={{ position: "absolute", left: `${position.x}px`, top: `${position.y}px`, width: `${size.width}px`, height: `${size.height}px` }}>
        <Panel
            header={<DraggableHeader onMouseDown={onDragMouseDown}>{`Buildings (${filtered.length}/${buildings.length})`}</DraggableHeader>}
            onClose={onClose}
            style={{ width: `${size.width}px`, height: `${size.height}px` }}
        >
            <div style={{ display: "flex", flexWrap: "wrap", gap: "4px", marginBottom: "8px" }}>
                <input
                    value={nameFilter}
                    onChange={(event) => setNameFilter(event.target.value)}
                    placeholder="Search name"
                    style={{ flex: "1 1 140px" }}
                />
                <FilterDropdown label="Any zone" options={zoneTypes} value={zoneTypeFilter} onChange={setZoneTypeFilter} />
                <FilterDropdown label="Any level" options={levels} value={levelFilter} onChange={setLevelFilter} formatOption={(v) => `L${v}`} />
                <FilterDropdown label="Any width" options={lotWidths} value={lotWidthFilter} onChange={setLotWidthFilter} formatOption={(v) => `${v} wide`} />
                <FilterDropdown label="Any depth" options={lotDepths} value={lotDepthFilter} onChange={setLotDepthFilter} formatOption={(v) => `${v} deep`} />
                <FilterDropdown label="Any pack" options={assetPacks} value={assetPackFilter} onChange={setAssetPackFilter} />
                <Button onSelect={clearFilters}>Clear</Button>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "4px", marginBottom: "8px" }}>
                <span style={{ fontSize: "12px" }}>Add to:</span>
                <FilterDropdown
                    label="Choose a collection"
                    options={collections.map((c) => c.id)}
                    value={targetCollectionId}
                    onChange={setTargetCollectionId}
                    formatOption={(id) => collectionNameById.get(id) ?? id}
                />
            </div>
            <Scrollable vertical style={{ maxHeight: `${size.height - 220}px` }}>
                {filtered.length === 0 && <div>No buildings match.</div>}
                {filtered.map((building) => {
                    const alreadyAdded = targetCollectionPrefabIds.has(building.prefabId);
                    return (
                        <div
                            key={building.prefabId}
                            style={{ display: "flex", alignItems: "center", gap: "6px", padding: "2px 0", fontSize: "12px" }}
                        >
                            <span style={{ flex: "1 1 auto", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                                {building.prefabId}
                            </span>
                            <span style={{ flex: "1 1 auto", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", textAlign: "right" }}>
                                {building.zoneType} L{building.level} {building.lotWidth}x{building.lotDepth} [{building.assetPackIds.join(", ")}]
                            </span>
                            <Button
                                style={{ flexShrink: 0 }}
                                disabled={targetCollectionId === ANY}
                                selected={alreadyAdded}
                                onSelect={() => trigger(mod.id, "AddBuildingToCollection", targetCollectionId, building.prefabId)}
                            >
                                {alreadyAdded ? "Added" : "Add"}
                            </Button>
                        </div>
                    );
                })}
            </Scrollable>
        </Panel>
        <ResizeHandle onMouseDown={onResizeMouseDown} />
        </div>
    );
};

function distinctStrings(values: string[]): string[] {
    return Array.from(new Set(values)).sort();
}

function distinctNumbers(values: number[]): number[] {
    return Array.from(new Set(values)).sort((a, b) => a - b);
}

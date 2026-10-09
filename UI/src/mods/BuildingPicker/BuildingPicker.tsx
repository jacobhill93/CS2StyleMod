import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Dropdown, DropdownToggle, Panel, Scrollable } from "cs2/ui";
import * as cs2ui from "cs2/ui";
import { ReactNode, useMemo, useState } from "react";
import mod from "../../../mod.json";

// cs2/ui's own ambient type declarations (UI/types/ui.d.ts) declare both a
// plain data interface `DropdownItem<T>` (item shape) and re-export the
// real component under that same name from its internal name
// `DropdownItem$1` - the two collide, and the named import only resolves
// to the type half ("only refers to a type, but is being used as a value
// here"). The component itself is real (same runtime window global every
// other cs2/ui import resolves to) - this just works around the broken
// named-export typing for it specifically.
const DropdownItem = cs2ui.DropdownItem as unknown as (props: {
    value: string;
    selected?: boolean;
    onChange?: (value: string) => void;
    children?: ReactNode;
}) => JSX.Element;

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
}

const Buildings$ = bindValue<BuildingEntry[]>(mod.id, "Buildings", []);
// Same binding CollectionPanel reads - broadcasts aren't exclusive to one
// panel, and the picker needs to know which collections exist to offer as
// an "add to" target.
const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);

// Empty string means "any" for every filter below.
const ANY = "";

interface FilterDropdownProps {
    label: string;
    options: string[];
    value: string;
    onChange: (value: string) => void;
    formatOption?: (value: string) => string;
}

// cs2/ui has no native <select> - raw <select>/<option> crashed the whole
// game UI render tree in-game (confirmed via UI.log: a TypeError inside the
// game's own core bundle, not ours - there's no error boundary around an
// individually appended mod panel, so one throw there takes down the
// entire HUD). Dropdown/DropdownToggle/DropdownItem (from the real cs2/ui
// type declarations, UI/types/ui.d.ts) is the actual supported mechanism
// for "select one of several mutually exclusive values" - this wraps it
// once since five filters need the same shape.
const FilterDropdown = ({ label, options, value, onChange, formatOption }: FilterDropdownProps) => {
    const format = formatOption ?? ((v: string) => v);
    return (
        <Dropdown
            content={
                <>
                    <DropdownItem value={ANY} selected={value === ANY} onChange={onChange}>{label}</DropdownItem>
                    {options.map((option) => (
                        <DropdownItem key={option} value={option} selected={value === option} onChange={onChange}>
                            {format(option)}
                        </DropdownItem>
                    ))}
                </>
            }
        >
            <DropdownToggle>{value === ANY ? label : format(value)}</DropdownToggle>
        </Dropdown>
    );
};

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

    const collectionNameById = useMemo(() => {
        const map = new Map<string, string>();
        collections.forEach((c) => map.set(c.id, c.name));
        return map;
    }, [collections]);

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
        <Panel
            header={`Buildings (${filtered.length}/${buildings.length})`}
            onClose={onClose}
            style={{ position: "absolute", top: "100px", left: "440px", width: "480px" }}
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
            <Scrollable vertical style={{ maxHeight: "360px" }}>
                {filtered.length === 0 && <div>No buildings match.</div>}
                {filtered.map((building) => (
                    <div
                        key={building.prefabId}
                        style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "2px 0", fontSize: "12px", gap: "6px" }}
                    >
                        <span>{building.prefabId}</span>
                        <span style={{ flex: "1 1 auto", textAlign: "right" }}>
                            {building.zoneType} L{building.level} {building.lotWidth}x{building.lotDepth} [{building.assetPackIds.join(", ")}]
                        </span>
                        <Button
                            disabled={targetCollectionId === ANY}
                            onSelect={() => trigger(mod.id, "AddBuildingToCollection", targetCollectionId, building.prefabId)}
                        >
                            Add
                        </Button>
                    </div>
                ))}
            </Scrollable>
        </Panel>
    );
};

function distinctStrings(values: string[]): string[] {
    return Array.from(new Set(values)).sort();
}

function distinctNumbers(values: number[]): number[] {
    return Array.from(new Set(values)).sort((a, b) => a - b);
}

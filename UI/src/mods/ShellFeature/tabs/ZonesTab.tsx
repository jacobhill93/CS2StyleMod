import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Scrollable } from "cs2/ui";
import { useState } from "react";
import classNames from "classnames";
import mod from "../../../../mod.json";
import { generateGuid } from "shared/generateGuid";
import { ANY, FilterDropdown } from "shared/FilterDropdown";
import { Icons } from "shared/icons";
import rowStyles from "shared/ListRow.module.scss";
import styles from "./ZonesTab.module.scss";
import inputStyles from "shared/TextInput.module.scss";
import { TextInputTheme } from "vanilla/TextInputTheme";

interface CollectionEntry {
    id: string;
    name: string;
    entryCount: number;
}

interface ZoneEntry {
    id: string;
    name: string;
    baseZoneType: string;
    region: string;
    collectionIds: string[];
}

// Mirrors UI.ZoneBuildProgressEntry (src/UI/ZoneBuildProgressEntry.cs) -
// a getter-based binding (see that file's comment), so this reflects
// CustomZoneBuildRequestSystem.CurrentProgress live, every UI tick,
// while a build/rebuild is spreading a large collection across many
// frames rather than freezing one - see DECISIONS.md "A 349-entry
// collection froze the game". Without this, a big attach just looks
// like the game hung.
interface BuildProgressEntry {
    active: boolean;
    zoneName: string;
    completed: number;
    total: number;
}

// Mirrors Core.ZoneType exactly (src/Core/Primitives.cs) - see ZonePanel's
// identical list for why this is fixed at creation only.
const BASE_ZONE_TYPES = [
    "ResidentialLow",
    "ResidentialMedium",
    "ResidentialHigh",
    "CommercialLow",
    "CommercialHigh",
    "Office",
    "Industrial",
];

// Mirrors Core.ZoneRegion exactly (src/Core/Primitives.cs) - literal
// enum member names, not display labels, since this goes straight to
// Enum.TryParse<ZoneRegion> on the C# side (case-sensitive, same
// convention BASE_ZONE_TYPES above already follows for ZoneType). Which
// vanilla theme (UI category/catalog grouping, not simulation behavior -
// see Core.ZoneRegion's own comment) the zone's underlying template is
// cloned from, fixed at creation same as base type - see
// ZoneUISystem.OnCreateZone's comment for why.
type ZoneRegion = "NorthAmerica" | "Europe";
const REGION_LABELS: Record<ZoneRegion, string> = { NorthAmerica: "NA", Europe: "EU" };

const Zones$ = bindValue<ZoneEntry[]>(mod.id, "Zones", []);
const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);
const BuildProgress$ = bindValue<BuildProgressEntry>(mod.id, "BuildProgress", {
    active: false,
    zoneName: "",
    completed: 0,
    total: 0,
});
// UI_OVERHAUL_PLAN.md Phase 2: ports ZonePanel's list/Create/delete-
// confirmation/empty-state into the shell's Zones tab, and adds the
// list->detail navigation the wireframe calls for (section B) - a zone
// row opens a detail view of its attached collections, replacing the old
// always-separate ZoneCollectionsPicker panel (its attach/detach trigger
// calls are ported in below, not rebuilt). Not yet confirmed in-game.
export const ZonesTab = () => {
    const zones = useValue(Zones$);
    const collections = useValue(Collections$);
    const buildProgress = useValue(BuildProgress$);
    const [selectedZoneId, setSelectedZoneId] = useState<string | null>(null);

    const selectedZone = zones.find((z) => z.id === selectedZoneId);
    if (selectedZoneId && !selectedZone) {
        // The selected zone was deleted out from under the detail view
        // (e.g. via a trigger from elsewhere) - fall back to the list
        // rather than rendering a detail view for a zone that's gone.
        setSelectedZoneId(null);
        return null;
    }

    return (
        <>
            <BuildProgressBanner progress={buildProgress} />
            {selectedZone ? (
                <ZoneDetail zone={selectedZone} collections={collections} onBack={() => setSelectedZoneId(null)} />
            ) : (
                <ZonesList zones={zones} onSelect={setSelectedZoneId} />
            )}
        </>
    );
};

// Shown above either sub-view, not just the list, since a build can be
// triggered from the detail view too (attaching a collection) - the
// user shouldn't lose the indicator just because they're looking at a
// zone's detail panel when a big one kicks off.
const BuildProgressBanner = ({ progress }: { progress: BuildProgressEntry }) => {
    if (!progress.active) return null;

    return (
        <div className={styles.buildProgressBanner}>
            <div className={styles.spinner} />
            <span className={styles.buildProgressText}>
                Building "{progress.zoneName}"... {progress.completed}/{progress.total}
            </span>
        </div>
    );
};

interface ZonesListProps {
    zones: ZoneEntry[];
    onSelect: (zoneId: string) => void;
}

const ZonesList = ({ zones, onSelect }: ZonesListProps) => {
    const [newName, setNewName] = useState("");
    const [newBaseZoneType, setNewBaseZoneType] = useState(ANY);
    const [newRegion, setNewRegion] = useState<ZoneRegion>("Europe");
    const [createError, setCreateError] = useState(false);
    const [confirmingDeleteId, setConfirmingDeleteId] = useState<string | null>(null);

    const onCreate = () => {
        const name = newName.trim();
        if (!name || newBaseZoneType === ANY) {
            setCreateError(true);
            return;
        }

        const id = generateGuid();
        trigger(mod.id, "CreateZone", id, name, newBaseZoneType, newRegion);
        setNewName("");
        setNewBaseZoneType(ANY);
        setNewRegion("Europe");
        setCreateError(false);
        onSelect(id);
    };

    return (
        <>
            <Scrollable vertical style={{ height: "280px" }}>
                {zones.length === 0 && (
                    <div className={rowStyles.emptyState}>
                        No zones yet. Create one below and attach collections to define which buildings it offers.
                    </div>
                )}
                {zones.map((zone) => (
                    <div
                        key={zone.id}
                        className={confirmingDeleteId === zone.id ? undefined : rowStyles.row}
                        style={confirmingDeleteId === zone.id ? { display: "flex", flexDirection: "column", gap: "4px", padding: "4px 0" } : undefined}
                    >
                        {confirmingDeleteId === zone.id ? (
                            <>
                                <span style={{ fontSize: "13px", width: "100%", whiteSpace: "normal", wordBreak: "break-word" }}>
                                    Delete "{zone.name}"? Its collections remain available for other zones.
                                </span>
                                <div style={{ display: "flex", gap: "4px", justifyContent: "flex-end" }}>
                                    <Button onSelect={() => setConfirmingDeleteId(null)}>Cancel</Button>
                                    <Button
                                        variant="icon"
                                        src={Icons.trash}
                                        tooltipLabel="Confirm delete"
                                        onSelect={() => {
                                            trigger(mod.id, "DeleteZone", zone.id);
                                            setConfirmingDeleteId(null);
                                        }}
                                    />
                                </div>
                            </>
                        ) : (
                            <>
                                <span onClick={() => onSelect(zone.id)} className={classNames(rowStyles.primary, rowStyles.clickable)}>
                                    {zone.name} ({zone.baseZoneType}, {REGION_LABELS[zone.region as ZoneRegion] ?? zone.region}, {zone.collectionIds.length} collection{zone.collectionIds.length === 1 ? "" : "s"})
                                </span>
                                <div className={rowStyles.actions}>
                                    <Button variant="icon" src={Icons.pencil} tooltipLabel="Edit" onSelect={() => onSelect(zone.id)} />
                                    <Button variant="icon" src={Icons.trash} tooltipLabel="Delete" onSelect={() => setConfirmingDeleteId(zone.id)} />
                                </div>
                            </>
                        )}
                    </div>
                ))}
            </Scrollable>
            <div style={{ display: "flex", flexDirection: "column", marginTop: "8px", gap: "4px" }}>
                <div style={{ display: "flex", gap: "4px" }}>
                    <input
                        value={newName}
                        onChange={(event) => {
                            setNewName(event.target.value);
                            if (createError) setCreateError(false);
                        }}
                        placeholder="New zone name"
                        className={classNames(TextInputTheme.input, inputStyles.input)}
                        style={{ flex: 1 }}
                    />
                    <FilterDropdown
                        label="Base type"
                        options={BASE_ZONE_TYPES}
                        value={newBaseZoneType}
                        onChange={(value) => {
                            setNewBaseZoneType(value);
                            if (createError) setCreateError(false);
                        }}
                    />
                    {(Object.keys(REGION_LABELS) as ZoneRegion[]).map((region) => (
                        <Button
                            key={region}
                            variant="flat"
                            selected={newRegion === region}
                            tooltipLabel={region === "NorthAmerica" ? "North America theme" : "Europe theme"}
                            onSelect={() => setNewRegion(region)}
                        >
                            {REGION_LABELS[region]}
                        </Button>
                    ))}
                    <Button onSelect={onCreate}>Create</Button>
                </div>
                {createError && <span style={{ fontSize: "12px", color: "#ff8080" }}>Name and base type are both required.</span>}
            </div>
        </>
    );
};

interface ZoneDetailProps {
    zone: ZoneEntry;
    collections: CollectionEntry[];
    onBack: () => void;
}

// Replaces the old always-separate ZoneCollectionsPicker panel - same
// attach/detach triggers, now scoped inline under the zone it edits
// instead of its own floating window.
const ZoneDetail = ({ zone, collections, onBack }: ZoneDetailProps) => {
    const [addTargetId, setAddTargetId] = useState(ANY);
    const [newCollectionName, setNewCollectionName] = useState("");

    const attachedIds = new Set(zone.collectionIds);
    const attached = collections.filter((c) => attachedIds.has(c.id));
    const unattached = collections.filter((c) => !attachedIds.has(c.id));
    const collectionNameById = new Map(collections.map((c) => [c.id, c.name] as const));

    const onCreateAndAttach = () => {
        const name = newCollectionName.trim();
        if (!name) return;

        const id = generateGuid();
        trigger(mod.id, "CreateCollection", id, name);
        trigger(mod.id, "AddCollectionToZone", zone.id, id);
        setNewCollectionName("");
    };

    return (
        <>
            <div style={{ display: "flex", alignItems: "center", gap: "6px", marginBottom: "8px" }}>
                <Button onSelect={onBack}>Back</Button>
                <span style={{ fontSize: "14px", fontWeight: "bold", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {zone.name}
                </span>
            </div>
            <Scrollable vertical style={{ height: "220px" }}>
                {attached.length === 0 && (
                    <div className={rowStyles.emptyState}>No collections attached yet.</div>
                )}
                {attached.map((collection) => (
                    <div key={collection.id} className={rowStyles.row}>
                        <span className={rowStyles.primary}>
                            {collection.name} ({collection.entryCount})
                        </span>
                        {/* Per-collection "Edit" (opening its own membership view) is
                            deferred to Phase 3 - that view doesn't exist yet. Only
                            detach is real today. */}
                        <Button
                            variant="icon"
                            src={Icons.trash}
                            tooltipLabel="Remove from zone"
                            onSelect={() => trigger(mod.id, "RemoveCollectionFromZone", zone.id, collection.id)}
                        />
                    </div>
                ))}
            </Scrollable>
            <div style={{ display: "flex", flexDirection: "column", gap: "6px", marginTop: "8px" }}>
                <div style={{ display: "flex", gap: "4px" }}>
                    <FilterDropdown
                        label="Add existing collection"
                        options={unattached.map((c) => c.id)}
                        value={addTargetId}
                        onChange={setAddTargetId}
                        formatOption={(id) => collectionNameById.get(id) ?? id}
                    />
                    <Button
                        disabled={addTargetId === ANY}
                        onSelect={() => {
                            trigger(mod.id, "AddCollectionToZone", zone.id, addTargetId);
                            setAddTargetId(ANY);
                        }}
                    >
                        Add
                    </Button>
                </div>
                <div style={{ display: "flex", gap: "4px" }}>
                    <input
                        value={newCollectionName}
                        onChange={(event) => setNewCollectionName(event.target.value)}
                        placeholder="New collection name"
                        className={classNames(TextInputTheme.input, inputStyles.input)}
                        style={{ flex: 1 }}
                    />
                    <Button onSelect={onCreateAndAttach}>Create Collection and Add to Zone</Button>
                </div>
            </div>
        </>
    );
};

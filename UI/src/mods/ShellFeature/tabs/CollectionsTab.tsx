import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Scrollable } from "cs2/ui";
import { useState } from "react";
import classNames from "classnames";
import mod from "../../../../mod.json";
import { generateGuid } from "shared/generateGuid";
import { Icons } from "shared/icons";
import rowStyles from "shared/ListRow.module.scss";
import { BuildingThumbnail } from "shared/BuildingThumbnail";
import styles from "./CollectionsTab.module.scss";
import { TextInput } from "shared/TextInput";
import { TextButton } from "shared/TextButton";
import { RenameableTitle } from "shared/RenameableTitle";
import { CollectionEntry } from "shared/CollectionEntry";

interface BuildingEntry {
    prefabId: string;
    zoneType: string;
    level: number;
    lotWidth: number;
    lotDepth: number;
    heightMeters: number;
    assetPackIds: string[];
    iconPath: string;
}

const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);
const Buildings$ = bindValue<BuildingEntry[]>(mod.id, "Buildings", []);

// Mirrors UI.CollectionLevel1WarningEntry (src/UI/CollectionLevel1WarningEntry.cs) -
// see that file's and CollectionUISystem.OnFinishEditingCollectionAssets's
// comments. Fires once, when the user finishes editing a collection's
// assets (the Assets tab's "Done" button) - not at zone-attach time
// anymore (DECISIONS.md "Level 1 warning moved to the collection, not the
// zone attach"). Purely informational: the edit that triggered it already
// committed, there's nothing to confirm/cancel.
interface CollectionLevel1WarningEntry {
    active: boolean;
    collectionId: string;
    collectionName: string;
    missingZoneTypes: string[];
}

const CollectionLevel1Warning$ = bindValue<CollectionLevel1WarningEntry>(mod.id, "CollectionLevel1Warning", {
    active: false,
    collectionId: "",
    collectionName: "",
    missingZoneTypes: [],
});

interface CollectionsTabProps {
    // "+ Add Assets" switches the shell to the Assets tab in picking
    // mode, scoped to this collection - handled by ShellFeature, not
    // here, since it also owns which tab is active (see
    // AssetsTab.tsx's pickingForCollectionId).
    onAddAssets: (collectionId: string) => void;
    // Set by ShellFeature when returning here from a finished picking
    // session ("Done" in the Assets tab), so the same collection's
    // detail view reopens instead of dropping back to the list - this
    // component remounts fresh every time the tab bar switches away and
    // back (tabs are conditionally rendered, not just hidden), so its
    // own selectedCollectionId state doesn't otherwise survive that trip.
    initialSelectedCollectionId?: string | null;
}

// UI_OVERHAUL_PLAN.md Phase 3: ports CollectionPanel's list/Create/
// delete-confirmation/empty-state into the shell's Collections tab, and
// adds the Collection detail/membership view (wireframe section D) that
// doesn't exist anywhere else in the mod - lists the collection's own
// prefabIds (cross-referenced against Buildings$ for display info) with
// a per-row remove action, and a "+ Add Assets" button that switches to
// the Assets tab in picking mode (see onAddAssets above) rather than
// opening a separate dialog - merged into one component after the
// separate AssetPickerDialog's first real in-game test surfaced a
// positioning bug (a position:absolute dialog nested inside the shell's
// own position:absolute wrapper resolved against the SHELL's box, not
// the screen) and it became clear the two views only differed by a
// handful of conditional props, not enough to justify a second floating
// panel and its own class of these bugs.
export const CollectionsTab = ({ onAddAssets, initialSelectedCollectionId }: CollectionsTabProps) => {
    const collections = useValue(Collections$);
    const buildings = useValue(Buildings$);
    const [selectedCollectionId, setSelectedCollectionId] = useState<string | null>(initialSelectedCollectionId ?? null);

    const selectedCollection = collections.find((c) => c.id === selectedCollectionId);
    if (selectedCollectionId && !selectedCollection) {
        setSelectedCollectionId(null);
        return null;
    }

    if (selectedCollection) {
        return (
            <CollectionDetail
                collection={selectedCollection}
                buildings={buildings}
                onBack={() => setSelectedCollectionId(null)}
                onAddAssets={() => onAddAssets(selectedCollection.id)}
            />
        );
    }

    return <CollectionsList collections={collections} onSelect={setSelectedCollectionId} />;
};

interface CollectionsListProps {
    collections: CollectionEntry[];
    onSelect: (collectionId: string) => void;
}

const CollectionsList = ({ collections, onSelect }: CollectionsListProps) => {
    const [newName, setNewName] = useState("");
    const [nameError, setNameError] = useState(false);
    const [confirmingDeleteId, setConfirmingDeleteId] = useState<string | null>(null);

    const onCreate = () => {
        const name = newName.trim();
        if (!name) {
            setNameError(true);
            return;
        }

        const id = generateGuid();
        trigger(mod.id, "CreateCollection", id, name);
        setNewName("");
        setNameError(false);
        onSelect(id);
    };

    return (
        <>
            <Scrollable vertical style={{ height: "280px" }}>
                {collections.length === 0 && (
                    <div className={rowStyles.emptyState}>
                        No collections yet. Create a reusable group of buildings below, then attach it to a zone.
                    </div>
                )}
                {collections.map((collection) => (
                    <div
                        key={collection.id}
                        className={confirmingDeleteId === collection.id ? rowStyles.confirmRow : rowStyles.row}
                    >
                        {confirmingDeleteId === collection.id ? (
                            <>
                                <span className={rowStyles.confirmText}>
                                    Delete "{collection.name}"? It'll be detached from any zones using it.
                                </span>
                                <div className={rowStyles.confirmActions}>
                                    <TextButton onSelect={() => setConfirmingDeleteId(null)}>Cancel</TextButton>
                                    <Button
                                        variant="icon"
                                        src={Icons.trash}
                                        tooltipLabel="Confirm delete"
                                        onSelect={() => {
                                            trigger(mod.id, "DeleteCollection", collection.id);
                                            setConfirmingDeleteId(null);
                                        }}
                                    />
                                </div>
                            </>
                        ) : (
                            <>
                                <span onClick={() => onSelect(collection.id)} className={classNames(rowStyles.primary, rowStyles.clickable)}>
                                    {collection.name} ({collection.entryCount})
                                </span>
                                <div className={rowStyles.actions}>
                                    <Button variant="icon" src={Icons.pencil} tooltipLabel="Edit" onSelect={() => onSelect(collection.id)} />
                                    <Button variant="icon" src={Icons.trash} tooltipLabel="Delete" onSelect={() => setConfirmingDeleteId(collection.id)} />
                                </div>
                            </>
                        )}
                    </div>
                ))}
            </Scrollable>
            <div className={rowStyles.createRowWrapper}>
                <div className={rowStyles.createRowGroup}>
                    <TextInput
                        value={newName}
                        onChange={(value) => {
                            setNewName(value);
                            if (nameError) setNameError(false);
                        }}
                        placeholder="New collection name"
                        className={rowStyles.createRowInput}
                    />
                    <TextButton onSelect={onCreate}>Create</TextButton>
                </div>
                {nameError && <span className={rowStyles.errorText}>Name required.</span>}
            </div>
        </>
    );
};

interface CollectionDetailProps {
    collection: CollectionEntry;
    buildings: BuildingEntry[];
    onBack: () => void;
    onAddAssets: () => void;
}

const CollectionDetail = ({ collection, buildings, onBack, onAddAssets }: CollectionDetailProps) => {
    const buildingByPrefabId = new Map(buildings.map((b) => [b.prefabId, b] as const));
    const members = collection.prefabIds.map((id) => buildingByPrefabId.get(id)).filter((b): b is BuildingEntry => b !== undefined);

    return (
        <>
            <div className={rowStyles.detailHeader}>
                <TextButton onSelect={onBack}>Back</TextButton>
                <RenameableTitle
                    name={collection.name}
                    onRename={(newName) => trigger(mod.id, "RenameCollection", collection.id, newName)}
                />
            </div>
            <CollectionLevel1WarningBanner collectionId={collection.id} />
            <Scrollable vertical style={{ height: "220px" }}>
                {members.length === 0 && (
                    <div className={rowStyles.emptyState}>No assets in this collection yet.</div>
                )}
                {members.map((building) => (
                    <div key={building.prefabId} className={rowStyles.row}>
                        <BuildingThumbnail iconPath={building.iconPath} />
                        <span className={rowStyles.primary}>{building.prefabId}</span>
                        <span className={rowStyles.secondary}>
                            {building.zoneType} L{building.level} {building.lotWidth}x{building.lotDepth}
                        </span>
                        <Button
                            variant="icon"
                            src={Icons.trash}
                            tooltipLabel="Remove from collection"
                            onSelect={() => trigger(mod.id, "RemoveBuildingFromCollection", collection.id, building.prefabId)}
                        />
                    </div>
                ))}
            </Scrollable>
            <div style={{ marginTop: "8px" }}>
                <TextButton onSelect={onAddAssets}>+ Add Assets</TextButton>
            </div>
        </>
    );
};

// Only rendered for the collection it's actually about - a warning raised
// for a different collection (e.g. still pending after switching detail
// views without dismissing it) shouldn't bleed into this one.
const CollectionLevel1WarningBanner = ({ collectionId }: { collectionId: string }) => {
    const warning = useValue(CollectionLevel1Warning$);
    if (!warning.active || warning.collectionId !== collectionId) return null;

    return (
        <div className={styles.level1WarningBanner}>
            <span className={styles.level1WarningText}>
                "{warning.collectionName}" has no Level 1 building for: {warning.missingZoneTypes.join(", ")} - it
                won't spawn anything fresh on an empty lot for those zone types.
            </span>
            <div className={rowStyles.confirmActions}>
                <TextButton onSelect={() => trigger(mod.id, "DismissCollectionLevel1Warning")}>OK</TextButton>
            </div>
        </div>
    );
};

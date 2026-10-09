import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useState } from "react";
import mod from "../../../mod.json";
import { generateGuid } from "shared/generateGuid";
import { useDraggable } from "shared/useDraggable";
import { DraggableHeader } from "shared/DraggableHeader";
import { useResizable } from "shared/useResizable";
import { ResizeHandle } from "shared/ResizeHandle";
import { Icons } from "shared/icons";

interface CollectionEntry {
    id: string;
    name: string;
    entryCount: number;
}

interface CollectionPanelProps {
    // Chunk 5: the building picker needs to know which collection was just
    // created to open pre-targeted at it, before the next Collections
    // binding update would otherwise tell it. Generating the id client-side
    // (rather than waiting on Collection's own Guid.NewGuid()) is what makes
    // that possible without a round trip.
    onCreated?: (id: string, name: string) => void;
    // The only way to reopen the picker for an already-created collection -
    // without this, Create was a one-time-only chance to add buildings
    // (same gap ZonePanel's own Edit button already solved for zones).
    onEdit?: (id: string, name: string) => void;
}

const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);

export const CollectionPanel = ({ onCreated, onEdit }: CollectionPanelProps = {}) => {
    const collections = useValue(Collections$);
    const [newName, setNewName] = useState("");
    const [nameError, setNameError] = useState(false);
    // Inline "are you sure?" replacing a row's own Edit/Delete buttons,
    // not cs2/ui's ConfirmationDialog/DialogStack - those are untested in
    // this codebase, and a hand-rolled confirmation using components
    // already proven safe here is lower risk than reaching for an unknown
    // interaction pattern, especially right before a session where no
    // in-game retest happens until next time.
    const [confirmingDeleteId, setConfirmingDeleteId] = useState<string | null>(null);
    const { position, onHandleMouseDown: onDragMouseDown } = useDraggable({ x: 100, y: 100 });
    const { size, onHandleMouseDown: onResizeMouseDown } = useResizable({ width: 360, height: 400 });

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
        onCreated?.(id, name);
    };

    return (
        <div style={{ position: "absolute", left: `${position.x}px`, top: `${position.y}px` }}>
        <Panel
            header={<DraggableHeader onMouseDown={onDragMouseDown}>Collections</DraggableHeader>}
            style={{ width: `${size.width}px` }}
        >
            <Scrollable vertical style={{ height: `${size.height - 140}px` }}>
                {collections.length === 0 && (
                    <div style={{ fontSize: "12px", opacity: 0.8 }}>
                        No collections yet. Create a reusable group of buildings below, then attach it to a zone.
                    </div>
                )}
                {collections.map((collection) => (
                    <div
                        key={collection.id}
                        style={
                            confirmingDeleteId === collection.id
                                ? { display: "flex", flexDirection: "column", gap: "4px", padding: "4px 0" }
                                : { display: "flex", alignItems: "center", gap: "6px", padding: "4px 0" }
                        }
                    >
                        {confirmingDeleteId === collection.id ? (
                            <>
                                {/* Stacked (message, then buttons on their own row) rather than
                                    side-by-side in one flex row - confirmed in-game that a long
                                    message next to fixed-width buttons didn't wrap the way a
                                    normal browser would, and ended up visually hidden behind them.
                                    width:100%/whiteSpace:normal are explicit, not relying on flex
                                    defaults, since the stacked layout alone wasn't enough - this
                                    was still clipped instead of wrapping on a second try. */}
                                <span style={{ fontSize: "12px", width: "100%", whiteSpace: "normal", wordBreak: "break-word" }}>
                                    Delete "{collection.name}"? It'll be detached from any zones using it.
                                </span>
                                <div style={{ display: "flex", gap: "4px", justifyContent: "flex-end" }}>
                                    <Button onSelect={() => setConfirmingDeleteId(null)}>Cancel</Button>
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
                                {/* minWidth:0 is required for a flex item to actually shrink/
                                    truncate instead of pushing the Delete button off-panel. */}
                                <span style={{ flex: "1 1 auto", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                                    {collection.name} ({collection.entryCount})
                                </span>
                                <div style={{ display: "flex", gap: "4px", flexShrink: 0 }}>
                                    {/* Icon-only (variant="icon", no text children), not icon+label -
                                        confirmed in-game that icon+label overlapped and got clipped off
                                        the panel edge in this tight a row. Matches how real CS2 mods
                                        handle dense list-row actions (Find It/Road Builder, both
                                        variant="icon" throughout - see DECISIONS.md). tooltipLabel keeps
                                        this accessible per the earlier wireframe review's rule that
                                        icon-only actions need a title/label. */}
                                    <Button variant="icon" src={Icons.pencil} tooltipLabel="Edit" onSelect={() => onEdit?.(collection.id, collection.name)} />
                                    <Button variant="icon" src={Icons.trash} tooltipLabel="Delete" onSelect={() => setConfirmingDeleteId(collection.id)} />
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
                            if (nameError) setNameError(false);
                        }}
                        placeholder="New collection name"
                        style={{ flex: 1 }}
                    />
                    <Button onSelect={onCreate}>Create</Button>
                </div>
                {nameError && <span style={{ fontSize: "11px", color: "#ff8080" }}>Name required.</span>}
            </div>
            <ResizeHandle onMouseDown={onResizeMouseDown} />
        </Panel>
        </div>
    );
};

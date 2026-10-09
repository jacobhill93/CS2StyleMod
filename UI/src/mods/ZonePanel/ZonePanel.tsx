import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useState } from "react";
import mod from "../../../mod.json";
import { generateGuid } from "shared/generateGuid";
import { ANY, FilterDropdown } from "shared/FilterDropdown";
import { useDraggable } from "shared/useDraggable";
import { DraggableHeader } from "shared/DraggableHeader";
import { useResizable } from "shared/useResizable";
import { ResizeHandle } from "shared/ResizeHandle";
import { Icons } from "shared/icons";

interface ZoneEntry {
    id: string;
    name: string;
    baseZoneType: string;
    collectionIds: string[];
}

interface ZonePanelProps {
    // Chunk 6: same "know the id immediately" reasoning as CollectionPanel -
    // both fire when the zone-collections picker should open, for a
    // just-created zone or an explicit "Edit" on an existing one.
    onCreated?: (id: string, name: string) => void;
    onEdit?: (id: string, name: string) => void;
}

// Mirrors Core.ZoneType exactly (src/Core/Primitives.cs) - a small, stable
// set of the game's own real zone categories, not something we invented.
// Fixed at creation only, not editable afterward - see ZoneUISystem's
// OnCreateZone comment for why (the underlying vanilla zone clone is
// permanent once built, same as vanilla CS2 itself has no "convert this
// zone to a different density" action).
const BASE_ZONE_TYPES = [
    "ResidentialLow",
    "ResidentialMedium",
    "ResidentialHigh",
    "CommercialLow",
    "CommercialHigh",
    "Office",
    "Industrial",
];

const Zones$ = bindValue<ZoneEntry[]>(mod.id, "Zones", []);

export const ZonePanel = ({ onCreated, onEdit }: ZonePanelProps = {}) => {
    const zones = useValue(Zones$);
    const [newName, setNewName] = useState("");
    const [newBaseZoneType, setNewBaseZoneType] = useState(ANY);
    const [createError, setCreateError] = useState(false);
    // Inline "are you sure?" replacing a row's own Edit/Delete buttons -
    // same reasoning as CollectionPanel, not cs2/ui's untested
    // ConfirmationDialog/DialogStack.
    const [confirmingDeleteId, setConfirmingDeleteId] = useState<string | null>(null);
    const { position, onHandleMouseDown: onDragMouseDown } = useDraggable({ x: 100, y: 440 });
    const { size, onHandleMouseDown: onResizeMouseDown } = useResizable({ width: 460, height: 400 });

    const onCreate = () => {
        const name = newName.trim();
        if (!name || newBaseZoneType === ANY) {
            setCreateError(true);
            return;
        }

        const id = generateGuid();
        trigger(mod.id, "CreateZone", id, name, newBaseZoneType);
        setNewName("");
        setNewBaseZoneType(ANY);
        setCreateError(false);
        onCreated?.(id, name);
    };

    return (
        <div style={{ position: "absolute", left: `${position.x}px`, top: `${position.y}px` }}>
        <Panel
            header={<DraggableHeader onMouseDown={onDragMouseDown}>Zones</DraggableHeader>}
            style={{ width: `${size.width}px` }}
        >
            <Scrollable vertical style={{ height: `${size.height - 140}px` }}>
                {zones.length === 0 && (
                    <div style={{ fontSize: "12px", opacity: 0.8 }}>
                        No zones yet. Create one below and attach collections to define which buildings it offers.
                    </div>
                )}
                {zones.map((zone) => (
                    <div
                        key={zone.id}
                        style={
                            confirmingDeleteId === zone.id
                                ? { display: "flex", flexDirection: "column", gap: "4px", padding: "4px 0" }
                                : { display: "flex", alignItems: "center", gap: "6px", padding: "4px 0" }
                        }
                    >
                        {confirmingDeleteId === zone.id ? (
                            <>
                                {/* Stacked, not side-by-side - see CollectionPanel's identical
                                    comment for why. */}
                                <span style={{ fontSize: "12px", width: "100%", whiteSpace: "normal", wordBreak: "break-word" }}>
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
                                {/* overflow/textOverflow/minWidth:0 is the standard flexbox
                                    truncation trio - without minWidth:0 a flex item refuses to
                                    shrink below its content width, which is what was pushing
                                    Edit/Delete off the edge of the panel for longer zone names. */}
                                <span style={{ flex: "1 1 auto", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                                    {zone.name} ({zone.baseZoneType}, {zone.collectionIds.length} collection{zone.collectionIds.length === 1 ? "" : "s"})
                                </span>
                                <div style={{ display: "flex", gap: "4px", flexShrink: 0 }}>
                                    <Button variant="icon" src={Icons.pencil} tooltipLabel="Edit" onSelect={() => onEdit?.(zone.id, zone.name)} />
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
                    <Button onSelect={onCreate}>Create</Button>
                </div>
                {createError && <span style={{ fontSize: "11px", color: "#ff8080" }}>Name and base type are both required.</span>}
            </div>
            <ResizeHandle onMouseDown={onResizeMouseDown} />
        </Panel>
        </div>
    );
};

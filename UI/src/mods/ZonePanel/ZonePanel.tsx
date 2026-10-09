import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useState } from "react";
import mod from "../../../mod.json";
import { generateGuid } from "shared/generateGuid";

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

const Zones$ = bindValue<ZoneEntry[]>(mod.id, "Zones", []);

export const ZonePanel = ({ onCreated, onEdit }: ZonePanelProps = {}) => {
    const zones = useValue(Zones$);
    const [newName, setNewName] = useState("");

    const onCreate = () => {
        const name = newName.trim();
        if (!name) return;

        const id = generateGuid();
        trigger(mod.id, "CreateZone", id, name);
        setNewName("");
        onCreated?.(id, name);
    };

    return (
        <Panel
            header="Zones"
            style={{ position: "absolute", top: "440px", left: "100px", width: "360px" }}
        >
            <Scrollable vertical style={{ maxHeight: "300px" }}>
                {zones.length === 0 && <div>No zones yet.</div>}
                {zones.map((zone) => (
                    <div
                        key={zone.id}
                        style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "4px 0" }}
                    >
                        <span>{zone.name} ({zone.collectionIds.length} collection{zone.collectionIds.length === 1 ? "" : "s"})</span>
                        <div style={{ display: "flex", gap: "4px" }}>
                            <Button onSelect={() => onEdit?.(zone.id, zone.name)}>Edit</Button>
                            <Button onSelect={() => trigger(mod.id, "DeleteZone", zone.id)}>Delete</Button>
                        </div>
                    </div>
                ))}
            </Scrollable>
            <div style={{ display: "flex", marginTop: "8px", gap: "4px" }}>
                <input
                    value={newName}
                    onChange={(event) => setNewName(event.target.value)}
                    placeholder="New zone name"
                    style={{ flex: 1 }}
                />
                <Button onSelect={onCreate}>Create</Button>
            </div>
        </Panel>
    );
};

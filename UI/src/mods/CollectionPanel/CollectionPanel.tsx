import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useState } from "react";
import mod from "../../../mod.json";
import { generateGuid } from "shared/generateGuid";

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

    const onCreate = () => {
        const name = newName.trim();
        if (!name) return;

        const id = generateGuid();
        trigger(mod.id, "CreateCollection", id, name);
        setNewName("");
        onCreated?.(id, name);
    };

    return (
        <Panel
            header="Collections"
            style={{ position: "absolute", top: "100px", left: "100px", width: "360px" }}
        >
            <Scrollable vertical style={{ maxHeight: "300px" }}>
                {collections.length === 0 && <div>No collections yet.</div>}
                {collections.map((collection) => (
                    <div
                        key={collection.id}
                        style={{ display: "flex", alignItems: "center", gap: "6px", padding: "4px 0" }}
                    >
                        {/* minWidth:0 is required for a flex item to actually shrink/
                            truncate instead of pushing the Delete button off-panel. */}
                        <span style={{ flex: "1 1 auto", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                            {collection.name} ({collection.entryCount})
                        </span>
                        <div style={{ display: "flex", gap: "4px", flexShrink: 0 }}>
                            <Button onSelect={() => onEdit?.(collection.id, collection.name)}>Edit</Button>
                            <Button onSelect={() => trigger(mod.id, "DeleteCollection", collection.id)}>Delete</Button>
                        </div>
                    </div>
                ))}
            </Scrollable>
            <div style={{ display: "flex", marginTop: "8px", gap: "4px" }}>
                <input
                    value={newName}
                    onChange={(event) => setNewName(event.target.value)}
                    placeholder="New collection name"
                    style={{ flex: 1 }}
                />
                <Button onSelect={onCreate}>Create</Button>
            </div>
        </Panel>
    );
};

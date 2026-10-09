import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useState } from "react";
import mod from "../../../mod.json";
import { generateGuid } from "shared/generateGuid";
import { useResizable } from "shared/useResizable";
import { ResizeHandle } from "shared/ResizeHandle";

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
}

const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);

export const CollectionPanel = ({ onCreated }: CollectionPanelProps = {}) => {
    const collections = useValue(Collections$);
    const [newName, setNewName] = useState("");
    const { size, onHandleMouseDown } = useResizable({ width: 360, height: 400 });

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
            draggable={true}
            initialPosition={{ x: 100, y: 100 }}
            style={{ width: `${size.width}px`, height: `${size.height}px`, position: "relative" }}
        >
            <Scrollable vertical style={{ maxHeight: `${size.height - 140}px` }}>
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
                        <Button style={{ flexShrink: 0 }} onSelect={() => trigger(mod.id, "DeleteCollection", collection.id)}>
                            Delete
                        </Button>
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
            <ResizeHandle onMouseDown={onHandleMouseDown} />
        </Panel>
    );
};

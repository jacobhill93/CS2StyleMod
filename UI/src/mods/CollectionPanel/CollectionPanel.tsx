import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import { useState } from "react";
import mod from "../../../mod.json";

interface CollectionEntry {
    id: string;
    name: string;
    entryCount: number;
}

const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);

export const CollectionPanel = () => {
    const collections = useValue(Collections$);
    const [newName, setNewName] = useState("");

    const onCreate = () => {
        if (!newName.trim()) return;
        trigger(mod.id, "CreateCollection", newName.trim());
        setNewName("");
    };

    return (
        <Panel
            header="Collections"
            style={{ position: "absolute", top: "100px", left: "100px", width: "320px" }}
        >
            <Scrollable vertical style={{ maxHeight: "300px" }}>
                {collections.length === 0 && <div>No collections yet.</div>}
                {collections.map((collection) => (
                    <div
                        key={collection.id}
                        style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "4px 0" }}
                    >
                        <span>{collection.name} ({collection.entryCount})</span>
                        <Button onSelect={() => trigger(mod.id, "DeleteCollection", collection.id)}>
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
        </Panel>
    );
};

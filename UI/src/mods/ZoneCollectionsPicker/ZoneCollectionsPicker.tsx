import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Panel, Scrollable } from "cs2/ui";
import mod from "../../../mod.json";

interface CollectionEntry {
    id: string;
    name: string;
    entryCount: number;
}

interface ZoneEntry {
    id: string;
    name: string;
    baseZoneType: string;
    collectionIds: string[];
}

interface ZoneCollectionsPickerProps {
    zoneId: string;
    onClose?: () => void;
}

const Collections$ = bindValue<CollectionEntry[]>(mod.id, "Collections", []);
// Same binding ZonePanel reads - this panel needs the live collectionIds
// for the one zone it's editing, to know which rows are already attached.
const Zones$ = bindValue<ZoneEntry[]>(mod.id, "Zones", []);

// Attach/detach existing Collections to one zone. Deliberately simpler than
// the building picker - there's no need to filter here, collections are
// few and already named by the user. Each Add/Remove click saves and
// rebuilds immediately (see ZoneUISystem) - no separate save step.
export const ZoneCollectionsPicker = ({ zoneId, onClose }: ZoneCollectionsPickerProps) => {
    const collections = useValue(Collections$);
    const zones = useValue(Zones$);
    const zone = zones.find((z) => z.id === zoneId);
    const attachedIds = new Set(zone?.collectionIds ?? []);

    return (
        <Panel
            header={zone ? `Collections for "${zone.name}"` : "Collections"}
            onClose={onClose}
            style={{ position: "absolute", top: "440px", left: "480px", width: "360px" }}
        >
            <Scrollable vertical style={{ maxHeight: "300px" }}>
                {collections.length === 0 && <div>No collections yet - create one first.</div>}
                {collections.map((collection) => {
                    const attached = attachedIds.has(collection.id);
                    return (
                        <div
                            key={collection.id}
                            style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "4px 0" }}
                        >
                            <span>{collection.name} ({collection.entryCount})</span>
                            <Button
                                selected={attached}
                                onSelect={() =>
                                    trigger(mod.id, attached ? "RemoveCollectionFromZone" : "AddCollectionToZone", zoneId, collection.id)
                                }
                            >
                                {attached ? "Remove" : "Add"}
                            </Button>
                        </div>
                    );
                })}
            </Scrollable>
        </Panel>
    );
};

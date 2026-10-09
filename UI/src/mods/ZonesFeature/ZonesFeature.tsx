import { useState } from "react";
import { ZonePanel } from "mods/ZonePanel/ZonePanel";
import { ZoneCollectionsPicker } from "mods/ZoneCollectionsPicker/ZoneCollectionsPicker";

// Shared parent for ZonePanel and ZoneCollectionsPicker, same reasoning as
// CollectionsFeature: "which zone is being edited" is ordinary lifted
// React state. Covers both entry points to the picker - a just-created
// zone, or an explicit "Edit" on an existing one.
export const ZonesFeature = () => {
    const [editingZoneId, setEditingZoneId] = useState<string | null>(null);

    return (
        <>
            <ZonePanel
                onCreated={(id) => setEditingZoneId(id)}
                onEdit={(id) => setEditingZoneId(id)}
            />
            {editingZoneId && (
                // key forces a fresh instance per zone switched to.
                <ZoneCollectionsPicker
                    key={editingZoneId}
                    zoneId={editingZoneId}
                    onClose={() => setEditingZoneId(null)}
                />
            )}
        </>
    );
};

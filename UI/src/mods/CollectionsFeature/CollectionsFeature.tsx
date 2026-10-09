import { useState } from "react";
import { CollectionPanel } from "mods/CollectionPanel/CollectionPanel";
import { BuildingPicker } from "mods/BuildingPicker/BuildingPicker";

// Chunk 5: connects CollectionPanel's "Create" to the building picker.
// CollectionPanel and BuildingPicker are two independent panels with no
// shared parent of their own (each was append()-ed to 'Game' separately
// through chunk 4) - this component is their actual shared parent, so
// "which collection was just created" can live as ordinary lifted React
// state instead of needing a cross-module signal or a round trip through
// the backend. Registered in index.tsx in place of the two separate
// appends.
export const CollectionsFeature = () => {
    const [pickerTarget, setPickerTarget] = useState<{ id: string; name: string } | null>(null);

    return (
        <>
            <CollectionPanel onCreated={(id, name) => setPickerTarget({ id, name })} />
            {pickerTarget && (
                // key forces a fresh BuildingPicker per created collection -
                // filters/target-dropdown reset rather than carrying over
                // stale state from whatever was open before.
                <BuildingPicker
                    key={pickerTarget.id}
                    initialTargetCollectionId={pickerTarget.id}
                    onClose={() => setPickerTarget(null)}
                />
            )}
        </>
    );
};

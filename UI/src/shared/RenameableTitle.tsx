import { Button } from "cs2/ui";
import { useEffect, useRef, useState } from "react";
import { Icons } from "shared/icons";
import { EllipsisTextInput } from "vanilla/EllipsisTextInput";

interface RenameableTitleProps {
    name: string;
    onRename: (newName: string) => void;
}

// UI_UPGRADE_GUIDE.md Phase 3 - the one functional gap: RenameCollection/
// RenameZone already exist C#-side (save + refresh, RenameZone also
// requests a rebuild so the in-game zone-tool display name updates) but
// had zero TS call sites. Phase 3a shipped this with a plain <input> +
// TextInputTheme.input (confirmed in-game); Phase 3b (here) swaps to the
// real vanilla EllipsisTextInput component for the native look - see
// UI/src/vanilla/EllipsisTextInput.ts for the verified prop contract.
// Shared between CollectionsTab's and ZonesTab's detail-view headers
// rather than duplicated - identical mechanics, only the commit callback
// (and therefore the trigger name/entity id) differs per caller.
export const RenameableTitle = ({ name, onRename }: RenameableTitleProps) => {
    const [isEditing, setIsEditing] = useState(false);
    const [editedName, setEditedName] = useState(name);
    const inputRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        if (isEditing) {
            inputRef.current?.focus();
            inputRef.current?.select();
        }
    }, [isEditing]);

    const startEditing = () => {
        setEditedName(name);
        setIsEditing(true);
    };

    // Trim; empty or unchanged cancels silently - the C# handlers already
    // guard IsNullOrWhiteSpace, but there's no reason to fire a trigger
    // that would be a no-op.
    const commit = () => {
        const trimmed = editedName.trim();
        if (trimmed && trimmed !== name) onRename(trimmed);
        setIsEditing(false);
    };

    // No Escape-cancels-discard behavior here, by accepted design, not
    // oversight: EllipsisTextInput's own internal "Back" action (visible
    // in the compiled source as a Back/Select hint tied to focus state)
    // treats Escape as its own built-in exit gesture, which still ends up
    // committing via the same onBlur path below - confirmed in-game
    // (2026-10-12) that this can't be overridden with a plain onKeyDown
    // handler (two attempts tried, neither changed the observed
    // behavior - the key is very likely consumed by the component's own
    // input-action handling before it ever reaches a DOM keydown
    // listener). Accepted as-is: Escape behaves like Enter/blur here,
    // unlike Phase 3a's plain-input version, which did support real
    // cancel-and-discard. A real cancel gesture would need deeper
    // reverse-engineering of this vanilla component with no guaranteed
    // payoff - not pursued.
    if (isEditing) {
        return (
            <EllipsisTextInput
                ref={inputRef}
                value={editedName}
                onChange={(event) => setEditedName(event.target.value)}
                onBlur={commit}
                onKeyDown={(event) => {
                    if (event.key === "Enter") commit();
                }}
            />
        );
    }

    return (
        <>
            <span
                style={{
                    flex: 1,
                    fontSize: "14px",
                    fontWeight: "bold",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                }}
            >
                {name}
            </span>
            <Button variant="icon" src={Icons.pencil} tooltipLabel="Rename" onSelect={startEditing} />
        </>
    );
};

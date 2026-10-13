import { Button } from "cs2/ui";
import { useEffect, useRef, useState } from "react";
import classNames from "classnames";
import { Icons } from "shared/icons";
import inputStyles from "shared/TextInput.module.scss";
import { TextInputTheme } from "vanilla/TextInputTheme";

interface RenameableTitleProps {
    name: string;
    onRename: (newName: string) => void;
}

// UI_UPGRADE_GUIDE.md Phase 3a - the one functional gap: RenameCollection/
// RenameZone already exist C#-side (save + refresh, RenameZone also
// requests a rebuild so the in-game zone-tool display name updates) but
// had zero TS call sites. Proven primitives only here - plain <input> +
// TextInputTheme.input + TextInput.module.scss, exactly as the create-row
// inputs already use (CollectionsTab.tsx). No EllipsisTextInput (Phase
// 3b, gated separately on in-game confirmation of this first).
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

    const cancel = () => setIsEditing(false);

    if (isEditing) {
        return (
            <input
                ref={inputRef}
                value={editedName}
                onChange={(event) => setEditedName(event.target.value)}
                onBlur={commit}
                onKeyDown={(event) => {
                    if (event.key === "Enter") commit();
                    else if (event.key === "Escape") cancel();
                }}
                className={classNames(TextInputTheme.input, inputStyles.input)}
                style={{ flex: 1, fontSize: "14px", fontWeight: "bold" }}
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

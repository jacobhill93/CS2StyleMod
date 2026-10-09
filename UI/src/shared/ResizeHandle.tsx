import { MouseEvent as ReactMouseEvent } from "react";

interface ResizeHandleProps {
    onMouseDown: (event: ReactMouseEvent) => void;
}

// Small corner grip, anchored to its nearest positioned ancestor - relies
// on Panel's own root establishing a positioning context (it manages its
// own header/content/close-button layout internally, so this is a
// reasonable bet, but it's the one part of this feature not confirmed
// in-game yet - verify the handle actually sits at Panel's true corner,
// not some inner content area's corner, before trusting this further).
export const ResizeHandle = ({ onMouseDown }: ResizeHandleProps) => (
    <div
        onMouseDown={onMouseDown}
        style={{
            position: "absolute",
            right: 0,
            bottom: 0,
            width: "14px",
            height: "14px",
            cursor: "nwse-resize",
            background: "linear-gradient(135deg, transparent 50%, rgba(255,255,255,0.5) 50%)",
        }}
    />
);

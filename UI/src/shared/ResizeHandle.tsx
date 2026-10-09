import { MouseEvent as ReactMouseEvent } from "react";

interface ResizeHandleProps {
    onMouseDown: (event: ReactMouseEvent) => void;
}

// Small corner grip, anchored to its nearest positioned ancestor - the
// outer position:absolute wrapper div each panel renders inside (see
// useDraggable's usage sites), confirmed working in-game.
//
// Back to inline styles, not the ResizeHandle.module.scss version -
// confirmed in-game (2026-10-09) that the SCSS/CSS-Modules build pipeline
// emits a correct CS2StyleMod.css at build time (verified: real extracted
// CSS, correctly scoped class names, hasCSS flag present in the bundle),
// but the handle was invisible/unfindable in-game - consistent with the
// stylesheet never actually being loaded at runtime despite building
// correctly, not with any error (none logged). The runtime-loading half
// of this mechanism isn't actually confirmed working, only the build half
// was - see DECISIONS.md. Reverting to the known-good inline approach
// here rather than leave the resize handle broken while that gets
// investigated separately.
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

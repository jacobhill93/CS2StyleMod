import { MouseEvent as ReactMouseEvent, ReactNode } from "react";

interface DraggableHeaderProps {
    onMouseDown: (event: ReactMouseEvent) => void;
    children: ReactNode;
}

// Passed as Panel's `header` prop - dragging starts from the header strip
// only (not the whole panel), standard window convention, and avoids
// accidentally starting a drag when clicking a button/input in the body.
export const DraggableHeader = ({ onMouseDown, children }: DraggableHeaderProps) => (
    <div onMouseDown={onMouseDown} style={{ cursor: "move" }}>
        {children}
    </div>
);

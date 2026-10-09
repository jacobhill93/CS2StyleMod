import { MouseEvent as ReactMouseEvent, useCallback, useRef, useState } from "react";

export interface Size {
    width: number;
    height: number;
}

// Drag-to-resize from a corner handle. cs2/ui's Panel has no native resize
// support (only a draggable *position*, via Panel's own draggable prop -
// see DECISIONS.md) - this is hand-built on plain mouse events
// (mousedown/mousemove/mouseup), not anything cs2/ui-specific. Lower risk
// than the earlier <select> incident: no new/unsupported host element,
// just event handlers and inline style changes, both things already used
// safely elsewhere in this UI.
export function useResizable(initialSize: Size, minSize: Size = { width: 220, height: 160 }) {
    const [size, setSize] = useState(initialSize);
    const dragRef = useRef<{ startX: number; startY: number; startWidth: number; startHeight: number } | null>(null);

    const onMouseMove = useCallback((event: MouseEvent) => {
        const drag = dragRef.current;
        if (!drag) return;
        setSize({
            width: Math.max(minSize.width, drag.startWidth + (event.clientX - drag.startX)),
            height: Math.max(minSize.height, drag.startHeight + (event.clientY - drag.startY)),
        });
    }, [minSize.width, minSize.height]);

    const onMouseUp = useCallback(() => {
        dragRef.current = null;
        window.removeEventListener("mousemove", onMouseMove);
        window.removeEventListener("mouseup", onMouseUp);
    }, [onMouseMove]);

    const onHandleMouseDown = useCallback((event: ReactMouseEvent) => {
        event.preventDefault();
        event.stopPropagation(); // don't also start a Panel drag-move
        dragRef.current = { startX: event.clientX, startY: event.clientY, startWidth: size.width, startHeight: size.height };
        window.addEventListener("mousemove", onMouseMove);
        window.addEventListener("mouseup", onMouseUp);
    }, [size.width, size.height, onMouseMove, onMouseUp]);

    return { size, onHandleMouseDown };
}

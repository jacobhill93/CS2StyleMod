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

    // rAF-batched - same reasoning as useDraggable (see its own comment):
    // a panel with a large list stuttered because every raw mousemove
    // triggered a full re-render. Capped to once per animation frame.
    const rafRef = useRef<number | null>(null);
    const pendingRef = useRef<Size | null>(null);

    const onMouseMove = useCallback((event: MouseEvent) => {
        const drag = dragRef.current;
        if (!drag) return;

        pendingRef.current = {
            width: Math.max(minSize.width, drag.startWidth + (event.clientX - drag.startX)),
            height: Math.max(minSize.height, drag.startHeight + (event.clientY - drag.startY)),
        };

        if (rafRef.current === null) {
            rafRef.current = requestAnimationFrame(() => {
                rafRef.current = null;
                if (pendingRef.current) setSize(pendingRef.current);
            });
        }
    }, [minSize.width, minSize.height]);

    const onMouseUp = useCallback(() => {
        dragRef.current = null;
        if (rafRef.current !== null) {
            cancelAnimationFrame(rafRef.current);
            rafRef.current = null;
        }
        if (pendingRef.current) {
            setSize(pendingRef.current);
            pendingRef.current = null;
        }
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

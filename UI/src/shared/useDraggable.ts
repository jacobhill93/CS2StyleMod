import { MouseEvent as ReactMouseEvent, useCallback, useRef, useState } from "react";

export interface Position {
    x: number;
    y: number;
}

// Hand-rolled panel movement, not cs2/ui's Panel `draggable` prop.
// Confirmed via UI/types/ui.d.ts: DraggablePanelProps is just
// `{ initialPosition?: Number2 }` - no onDrag/onMove/onPositionChanged of
// any kind, so a dragged position can never be read back out once the
// user lets go. In-game it also moved the game's own bottom ribbon to
// mid-screen on load, for reasons never fully root-caused. Three real,
// shipped third-party CS2 mods that register panels via the same
// moduleRegistry.append('Game', ...) pattern this mod uses (MeasureItCS2,
// City Watchdog, HallOfFame) were checked and all three independently
// avoid `draggable` entirely, hand-rolling plain mousedown/mousemove/
// mouseup on a positioning wrapper instead - this mirrors that. See
// DECISIONS.md "Draggable Panel broke the game's own HUD" for the history.
export function useDraggable(initialPosition: Position) {
    const [position, setPosition] = useState(initialPosition);
    const dragRef = useRef<{ startX: number; startY: number; startLeft: number; startTop: number } | null>(null);

    // rAF-batched, not applied straight from the raw mousemove handler -
    // confirmed in-game (2026-10-09) that a panel with a large list (the
    // building picker, up to ~4000 rows) stuttered while dragging, because
    // every raw mousemove (which can fire well over 60/sec) triggered a
    // full component re-render, re-reconciling the whole list each time.
    // Capping position updates to once per animation frame, the same
    // technique City Watchdog's own drag hook uses (found via the earlier
    // research pass), decouples re-render frequency from mouse-event
    // frequency.
    const rafRef = useRef<number | null>(null);
    const pendingRef = useRef<Position | null>(null);

    const onMouseMove = useCallback((event: MouseEvent) => {
        const drag = dragRef.current;
        if (!drag) return;

        pendingRef.current = {
            x: drag.startLeft + (event.clientX - drag.startX),
            y: drag.startTop + (event.clientY - drag.startY),
        };

        if (rafRef.current === null) {
            rafRef.current = requestAnimationFrame(() => {
                rafRef.current = null;
                if (pendingRef.current) setPosition(pendingRef.current);
            });
        }
    }, []);

    const onMouseUp = useCallback(() => {
        dragRef.current = null;
        if (rafRef.current !== null) {
            cancelAnimationFrame(rafRef.current);
            rafRef.current = null;
        }
        // Flush whatever the last pending frame didn't get to apply yet,
        // so letting go mid-frame doesn't leave the panel one tick behind
        // where the mouse actually ended up.
        if (pendingRef.current) {
            setPosition(pendingRef.current);
            pendingRef.current = null;
        }
        window.removeEventListener("mousemove", onMouseMove);
        window.removeEventListener("mouseup", onMouseUp);
    }, [onMouseMove]);

    const onHandleMouseDown = useCallback((event: ReactMouseEvent) => {
        event.preventDefault();
        dragRef.current = { startX: event.clientX, startY: event.clientY, startLeft: position.x, startTop: position.y };
        window.addEventListener("mousemove", onMouseMove);
        window.addEventListener("mouseup", onMouseUp);
    }, [position.x, position.y, onMouseMove, onMouseUp]);

    return { position, onHandleMouseDown };
}

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

    const onMouseMove = useCallback((event: MouseEvent) => {
        const drag = dragRef.current;
        if (!drag) return;
        setPosition({
            x: drag.startLeft + (event.clientX - drag.startX),
            y: drag.startTop + (event.clientY - drag.startY),
        });
    }, []);

    const onMouseUp = useCallback(() => {
        dragRef.current = null;
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

import { useRef, useState } from "react";

interface VirtualizedRowsOptions {
    itemCount: number;
    rowHeight: number;
    viewportHeight: number;
    overscan?: number;
}

// Second attempt (2026-10-11) - the first used cs2/ui's ScrollController
// (new ScrollController() passed as Scrollable's controller prop), which
// typechecked cleanly but crashed the entire game HUD on first in-game
// test, not just this panel (see DECISIONS.md "Assets tab list
// virtualization attempt - reverted"). Research afterward found no real
// CS2 mod constructs ScrollController directly - a strong signal it
// isn't meant to be used that way.
//
// This version avoids ScrollController entirely: Scrollable forwards a
// plain ref to its underlying HTMLDivElement (confirmed via its own
// RefAttributes<HTMLDivElement> type), and onScroll is an ordinary
// callback prop, not a class with its own construction/attachment
// lifecycle - both standard React patterns, not SDK-specific machinery.
// If onScroll turns out not to fire in this engine for some reason, the
// list just always shows its top slice while scrolling - a visible bug,
// not a crash, which is the actual risk being managed here.
export function useVirtualizedRows({ itemCount, rowHeight, viewportHeight, overscan = 4 }: VirtualizedRowsOptions) {
    const scrollRef = useRef<HTMLDivElement>(null);
    const [scrollTop, setScrollTop] = useState(0);

    const onScroll = () => {
        setScrollTop(scrollRef.current?.scrollTop ?? 0);
    };

    const visibleCount = Math.ceil(viewportHeight / rowHeight);
    const maxStartIndex = Math.max(0, itemCount - visibleCount);
    // Clamped against maxStartIndex, not just itemCount - without this,
    // a stale scrollTop from before a filter change shrank the list
    // (e.g. scrolled deep into ~4000 rows, then a search narrows it to
    // 5) produces a startIndex past the new itemCount and an empty
    // slice, even though real results exist.
    const startIndex = Math.max(0, Math.min(Math.floor(scrollTop / rowHeight) - overscan, maxStartIndex));
    const endIndex = Math.min(itemCount, startIndex + visibleCount + overscan * 2);

    return {
        scrollRef,
        onScroll,
        startIndex,
        endIndex,
        totalHeight: itemCount * rowHeight,
    };
}

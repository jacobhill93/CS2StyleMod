import { getModule } from "cs2/modding";

// One-off getModule export, same pattern as TextInputTheme.ts - one
// consumer (AssetsTileGrid.tsx) earns one export, not a
// VanillaComponentResolver resurrection (that file was deleted in the
// Phase 7.3 revert for having no consumer).
//
// UI_UPGRADE_GUIDE.md Phase 4c - the legitimate assetGridTheme use
// DECISIONS.md "Phase 7.3 reverted" explicitly left open: a real grid-
// of-tiles layout, not a retrofit onto list rows (which is what Phase
// 7.3 wrongly tried and reverted - see that entry for why .item is a
// fixed-size, centered-content tile, never row styling).
//
// Verified 2026-10-13 against the installed Content/Game/UI/index.css:
// classes.item resolves to "item_a5m item-focused_FuT item-hover_WK8
// item-active_Spn" (width/height: var(--assetMenuItemSize), 72rem under
// the active :root - the alternate 65rem value only applies under an
// .overwrite-legacy theme class this mod's shell is never inside).
// classes.row resolves to "row_xSL" - a horizontal flex row of items
// with real spacing rules confirmed in the same CSS: `.row_xSL+.row_xSL
// { margin-top: var(--assetMenuItemGap) }` (vertical gap between
// successive rows) and `.row_xSL>* { margin-left: var(--assetMenuItemGap)
// }` / `.row_xSL>*:first-child { margin-left: 0 }` (horizontal gap
// between items in the same row) - both referenced live via var(), not
// pinned to a literal pixel value, same as vanilla's own usage.
// classes.thumbnail resolves to "thumbnail_FDs"
// (width/height: var(--assetMenuImageSize), ~68rem under the same
// default theme) - sized for this tile, not this mod's own
// BuildingThumbnail.module.scss's normal fixed 48px (see
// BuildingThumbnail.tsx's optional size prop).
//
// Does NOT use classes.itemGrid ("item-grid_Yva") - that class computes
// its own width/height from --assetMenuColumns (9, tuned for vanilla's
// full-width asset menu) and --assetMenuRows, which would size it far
// wider than this mod's fixed 840px shell. Each virtual row is sized by
// this mod's own chunking logic instead (see AssetsTileGrid.tsx) - only
// classes.row and classes.item (and classes.thumbnail) are reused.
export const AssetGridTheme: Record<string, string> = getModule(
    "game-ui/game/components/item-grid/item-grid.module.scss",
    "classes"
);

import { trigger } from "cs2/api";
import { Scrollable } from "cs2/ui";
import classNames from "classnames";
import mod from "../../../../mod.json";
import { BuildingEntry } from "shared/useBuildingFilters";
import { BuildingThumbnail } from "shared/BuildingThumbnail";
import { useVirtualizedRows } from "shared/useVirtualizedRows";
import { AssetGridTheme } from "vanilla/AssetGridTheme";
import styles from "./AssetsTileGrid.module.scss";

// UI_UPGRADE_GUIDE.md Phase 4c - the legitimate assetGridTheme use
// DECISIONS.md "Phase 7.3 reverted" left open: real tiles, not rows.
// Opt-in alternate view alongside AssetsTab's existing row list (never
// replaces it) - see AssetsTab.tsx's viewMode toggle. Deliberately
// isolated to this one file + vanilla/AssetGridTheme.ts + its own
// .module.scss, so reverting this phase (if it doesn't pan out) is
// exactly "delete these three files and the toggle," same discipline
// as PlatterCompatibility.cs's own isolation.
//
// HAND-SYNCED CONSTANTS, NOT MEASURED - same discipline as AssetsTab.tsx's
// own ROW_HEIGHT. --assetMenuItemSize resolves to 72rem (verified - see
// AssetGridTheme.ts), but this mod's own layout stays in px (decided,
// DECISIONS.md "px vs rem") and there's no reliable rem->px conversion
// without actually measuring a rendered tile in-game.
//
// TILES_PER_ROW started at 6 (a guess that vanilla's own
// --assetMenuColumns:9 default was tuned for a much wider full-screen
// menu and wouldn't fit our 840px shell). The first in-game screenshot
// (2026-10-13) showed roughly the right THIRD of the panel empty with
// 6 columns - AssetGridTheme.row has no justify-content stretch rule,
// so it just left-packs tiles and leaves leftover space. Tried 9
// (vanilla's own column count) as the implied fix, but the user judged
// that too wide for our shell without even needing a rebuild to check;
// 8 columns was confirmed (via screenshot) to exactly fill the width.
//
// Then the user asked for the name label to move INSIDE the tile
// (grey backdrop should cover icon+label as one card, not just the
// icon with the label floating below in the dark panel background)
// and for bigger tiles with more breathing room, accepting fewer
// columns (7) to make space. TILE_SCALE grows the tile from the
// confirmed-correct 8-column size by 8/7 so 7 of the bigger tiles
// should still fill the same width (ignoring inter-tile gap overhead,
// which is small relative to tile size) - not yet confirmed in-game.
const TILES_PER_ROW = 7;
const TILE_SCALE = 8 / 7;
const TILE_WIDTH_CSS = `calc(var(--assetMenuItemSize) * ${TILE_SCALE})`;
const TILE_THUMBNAIL_CSS = `calc(var(--assetMenuImageSize) * ${TILE_SCALE})`;
// Extra height so the taller tile has room for the label stacked
// under the thumbnail (flex-direction: column override below), via
// the vanilla .item class's own justify-content:center distributing
// this extra space evenly above/below the thumbnail+label pair.
// First guess (22) was barely more than the label's own content
// height (margin-top 4 + ~16px of 12px text), leaving almost no
// slack to distribute - confirmed in-game (2026-10-13) the label
// sat crammed against the tile's bottom edge instead of having any
// visible breathing room. Bumped up to leave real slack - still a
// guess, not measured.
const TILE_LABEL_SPACE_PX = 34;
const TILE_HEIGHT_CSS = `calc(var(--assetMenuItemSize) * ${TILE_SCALE} + ${TILE_LABEL_SPACE_PX}px)`;

// TILE_ROW_HEIGHT_PX=100 was too small: rows visibly crowded together,
// tiles/thumbnails of one row bleeding into the label text of the row
// above/below. 140 (tile height + label space, when the label still
// lived outside the tile) was confirmed clean via screenshot - no
// overlap. Scaling that by TILE_SCALE (160) overshot the other way -
// confirmed in-game too much gap between rows. 130 was then confirmed
// too little. Splitting the difference, and nudged up a bit further
// for the TILE_LABEL_SPACE_PX increase above (taller tile needs a
// taller row to match) - still a guess, not measured.
const TILE_ROW_HEIGHT_PX = 150;

interface AssetsTileGridProps {
    buildings: BuildingEntry[];
    viewportHeight: number;
    pickingForCollectionId?: string | null;
    memberPrefabIds: Set<string>;
}

export const AssetsTileGrid = ({ buildings, viewportHeight, pickingForCollectionId, memberPrefabIds }: AssetsTileGridProps) => {
    const { scrollRef, onScroll, startIndex, endIndex, totalHeight } = useVirtualizedRows({
        itemCount: Math.ceil(buildings.length / TILES_PER_ROW),
        rowHeight: TILE_ROW_HEIGHT_PX,
        viewportHeight,
    });

    const visibleRowIndices = Array.from({ length: endIndex - startIndex }, (_, i) => startIndex + i);

    return (
        <Scrollable vertical ref={scrollRef} onScroll={onScroll} style={{ height: `${viewportHeight}px` }}>
            <div style={{ position: "relative", height: `${totalHeight}px` }}>
                {visibleRowIndices.map((rowIndex) => {
                    const rowBuildings = buildings.slice(rowIndex * TILES_PER_ROW, rowIndex * TILES_PER_ROW + TILES_PER_ROW);
                    const top = rowIndex * TILE_ROW_HEIGHT_PX;
                    return (
                        // marginTop: 0 overrides assetGridTheme.row's own
                        // ".row_xSL+.row_xSL { margin-top: var(--assetMenuItemGap) }"
                        // rule - vertical spacing between rows is already
                        // fully accounted for in TILE_ROW_HEIGHT_PX above
                        // (this mod's own virtualization math), so that
                        // rule would otherwise double up the gap on top
                        // of it. The horizontal margin-left rule between
                        // tiles within a row is NOT overridden - that one
                        // is wanted, nothing else here computes it.
                        <div
                            key={rowIndex}
                            className={AssetGridTheme.row}
                            style={{ position: "absolute", top: `${top}px`, left: 0, right: 0, marginTop: 0 }}
                        >
                            {rowBuildings.map((building) => {
                                const alreadyAdded = memberPrefabIds.has(building.prefabId);
                                return (
                                    // Plain div, not cs2/ui's Button - every existing
                                    // Button variant="icon" usage in this codebase
                                    // passes an src icon prop, never arbitrary
                                    // children, and Button's own internal structure
                                    // is unproven against being resized to a vanilla
                                    // tile class. A plain div matches vanilla's own
                                    // actual DOM shape for .item_a5m (confirmed from
                                    // its CSS, not Button-wrapped) and still gets
                                    // real :hover/:active states from that class's
                                    // own CSS regardless. "selected" is a plain,
                                    // unhashed literal class name (not part of
                                    // AssetGridTheme's exported classes object) the
                                    // compiled CSS's own selectors
                                    // (.item_a5m.selected) expect directly - a
                                    // reasonable inference from the CSS, not
                                    // confirmed against the vanilla component that
                                    // normally applies it, so treat the selected-tile
                                    // highlight as unverified until seen in-game.
                                    //
                                    // width/height/flexDirection override the
                                    // vanilla .item_a5m square (fixed at
                                    // --assetMenuItemSize via its own class,
                                    // inline style always wins) so there's room
                                    // for the label stacked under the thumbnail,
                                    // inside the same grey backdrop - user asked
                                    // for the label to read as part of the tile
                                    // card, not float below it in the dark panel
                                    // background (2026-10-13).
                                    <div
                                        key={building.prefabId}
                                        className={classNames(AssetGridTheme.item, alreadyAdded && "selected")}
                                        title={building.prefabId}
                                        style={{ width: TILE_WIDTH_CSS, height: TILE_HEIGHT_CSS, flexDirection: "column", cursor: "pointer" }}
                                        onClick={() => {
                                            if (!pickingForCollectionId) return;
                                            trigger(
                                                mod.id,
                                                alreadyAdded ? "RemoveBuildingFromCollection" : "AddBuildingToCollection",
                                                pickingForCollectionId,
                                                building.prefabId
                                            );
                                        }}
                                    >
                                        {/* div, not span - a span is inline by default, so its
                                        width:100%/text-align:center in AssetsTileGrid.module.scss
                                        weren't reliably applying (confirmed in-game 2026-10-13:
                                        no ellipsis truncation, text not centered) - a div is
                                        block-level by default, giving it a real constrained box.
                                        Label rendered BEFORE the thumbnail - user asked for the
                                        name on top, thumbnail below (2026-10-13). */}
                                        <div className={styles.tileLabel}>{building.prefabId}</div>
                                        <BuildingThumbnail iconPath={building.iconPath} size={TILE_THUMBNAIL_CSS} />
                                    </div>
                                );
                            })}
                        </div>
                    );
                })}
            </div>
        </Scrollable>
    );
};

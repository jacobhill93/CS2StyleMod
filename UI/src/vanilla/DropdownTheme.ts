import { getModule } from "cs2/modding";

// One-off getModule export, same pattern as AssetGridTheme.ts/
// TextInputTheme.ts - one consumer (FilterDropdown.tsx) earns one export.
//
// cs2/ui's Dropdown destructures its own `theme` prop as `theme: n = Oj`
// (confirmed via the installed Content/Game/UI/index.js) - a plain
// default parameter, not a merge. Passing ANY partial theme object
// REPLACES the whole thing, silently dropping every key not explicitly
// given - confirmed in-game 2026-10-13: passing only
// {dropdownPopup, dropdownMenu} broke option selection entirely, almost
// certainly by dropping `scrollable`/`dropdownItem` (and the Dropdown
// context every DropdownItem reads its own theme from) along with them.
//
// So: never pass a bare partial object to Dropdown's theme prop. Spread
// this real default theme first, then compose (not replace) only the
// specific key(s) actually being changed - see FilterDropdown.tsx.
//
// Verified 2026-10-13 against the installed index.js: this exact classes
// object - {dropdownToggle: "dropdown-toggle_bN9", label: "label_REA",
// indicator: "indicator_lzV", dropdownPopup: "dropdown-popup_duR",
// dropdownMenu: "dropdown-menu_rL4", scrollable: "scrollable_MVT",
// dropdownItem: "dropdown-item_t3P"} - matches what UI devtools showed
// live on an actual rendered FilterDropdown. dropdown-menu_rL4 itself
// (not dropdown-popup_duR) is the one with a solid white
// background-color + thin border - the real source of the "white box"
// around an open filter dropdown (not a focus ring, not CSS on the
// toggle button - both wrong guesses before this was actually inspected
// via devtools).
export const DropdownTheme: Record<string, string> = getModule("game-ui/common/input/dropdown/themes/default.module.scss", "classes");

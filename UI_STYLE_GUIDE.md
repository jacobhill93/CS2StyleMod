# UI Style Guide

How to make CS2StyleMod's UI look native, the way Road Builder and Find It do.
Derived from reading both mods' UI source (2026-10-12):

- **Road Builder** — https://github.com/JadHajjar/RoadBuilder-CSII (`RoadBuilder/UI`).
  **MIT licensed**: code may be copied/adapted, keeping the copyright notice
  (see [Licensing](#licensing)).
- **Find It** — https://github.com/JadHajjar/FindIt-CSII (`FindIt/UI`).
  **No license**: all rights reserved. Use as *inspiration only*: learn the
  patterns, never copy its code or SCSS.

Everything below that is attributed to "the game" (vanilla module paths, CSS
variables, icon paths) belongs to neither mod. It's the game's own UI, which
any mod can use.

---

## 1. Why the current UI doesn't look native

`UI/src/mods/CollectionPanel/CollectionPanel.tsx` (as of `505b2b2`) differs
from both reference mods in ways that no amount of layout tweaking fixes:

| Current | What the reference mods do |
|---|---|
| Inline `style={{...}}` objects | SCSS modules (`X.module.scss`) per component, all sizes in `rem` |
| Hardcoded position `top:100px; left:100px` | Either centered modal (`top:50%; left:50%; transform:translate(-50%,-50%)`) or docked into the game's own layout containers |
| Raw `<input>` | Vanilla text-input theme classes, or the game's `EllipsisTextInput` component |
| Hardcoded colours (none at all, so browser defaults) | Only the game's CSS variables (`var(--panelColorNormal)`, `var(--accentColorDark)`, ...) |
| Text-only buttons ("Delete", "Create") | Icon buttons (mask-image icons tinted by `background-color`) + tooltips |
| No toolbar entry | A floating toolbar button in `GameTopLeft` that toggles the panel |
| `cs2/ui` `Panel` only | `cs2/ui` (`Button`, `Scrollable`, `Tooltip`) **plus** non-exported vanilla components and SCSS themes pulled from the module registry |

The single biggest gap is the last row: **both mods reuse the game's own,
non-public UI components and style classes**. That's where the native look
comes from.

---

## 2. The core technique: reuse vanilla modules

The game's UI is a webpack bundle whose modules are reachable from mods by
path. Two ways to get them:

```ts
// a) One-off, at module scope
import { getModule } from "cs2/modding";
const AssetMenuTheme = getModule("game-ui/game/components/asset-menu/asset-menu.module.scss", "classes");

// b) Cached resolver singleton (Road Builder's VanillaComponentResolver, MIT)
VanillaComponentResolver.setRegistry(moduleRegistry); // in index.tsx register()
<VanillaComponentResolver.instance.EllipsisTextInput ... />
className={VanillaComponentResolver.instance.assetGridTheme.item}
```

**Adopt (b)**: copy Road Builder's `src/vanillacomponentresolver.tsx` into
`UI/src/vanilla/VanillaComponentResolver.tsx`, trimmed to what we use. The
resolver's comment credits Klyte as its original author; keep that comment.

### Vanilla modules worth using

From Road Builder's and Find It's registries (paths are the game's):

| Name | Module path | Export | Use for |
|---|---|---|---|
| `assetGridTheme` | `game-ui/game/components/item-grid/item-grid.module.scss` | `classes` | `.item` class on grid tiles: gives the vanilla asset-tile look |
| `EllipsisTextInput` | `game-ui/common/input/text/ellipsis-text-input/ellipsis-text-input.tsx` | `EllipsisTextInput` | Rename fields (collection name) |
| `TextInput` | `game-ui/common/input/text/text-input.tsx` | `TextInput` | Vanilla text input component |
| `TextInputTheme` | `game-ui/editor/widgets/item/editor-item.module.scss` | `classes` | `.input` class on plain `<input>`s (Road Builder's search box) |
| `textInputTheme` | `game-ui/game/components/selected-info-panel/shared-components/text-input/text-input.module.scss` | `classes` | Info-panel text input look |
| `Toggle` / `toggleTheme` | `game-ui/common/input/toggle/toggle.tsx`, `game-ui/menu/widgets/toggle-field/toggle-field.module.scss` | `Toggle`, `classes` | On/off settings |
| `Checkbox` / `checkboxTheme` | `game-ui/common/input/toggle/checkbox/checkbox.tsx`, `.../checkbox.module.scss` | `Checkbox`, `classes` | Multi-select in building picker |
| `Section` | `game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx` | `Section` | Tool-options rows |
| `ToolButton` / `toolButtonTheme` | `game-ui/game/components/tool-options/tool-button/tool-button.tsx`, `.module.scss` | `ToolButton`, `classes` | Tool-options icon buttons |
| `IntInput` | `game-ui/common/input/text/int-input.tsx` | `IntInput` | Numeric settings (level, household count) |
| `FOCUS_DISABLED` | `game-ui/common/focus/focus-key.ts` | `FOCUS_DISABLED` | Stop small buttons stealing gamepad/keyboard focus |
| `useScrollController` | `game-ui/common/hooks/use-scroll-controller.tsx` | `useScrollController` | Programmatic scrolling |
| Panel themes | `game-ui/common/panel/panel.module.scss`, `game-ui/common/panel/themes/default.module.scss` | `classes` | Vanilla panel chrome |
| Asset menu theme | `game-ui/game/components/asset-menu/asset-menu.module.scss` | `classes` | `.assetPanel`: the vanilla asset-menu look |
| Main screen theme | `game-ui/game/components/game-main-screen.module.scss` | `classes` | `.toolPanel`, `.toolMainColumn`: dock into vanilla layout above the toolbar |

Prop types for these are **guessed** (the game doesn't publish them). Road
Builder's resolver has typed versions of most; reuse them.

**Finding more:** run the game with UI developer mode and open
`http://localhost:9444/` in Chrome (per Road Builder's resolver comments) →
Sources → `index.js` → pretty-print → search for the `.tsx`/`.scss` path of
the vanilla element you want to imitate. Also lets you inspect the live DOM
and CSS of both vanilla panels and our own.

### Extending vanilla components

Mods can also wrap/replace vanilla components:

```ts
moduleRegistry.extend("game-ui/game/components/right-menu/right-menu.tsx", "RightMenu", Wrapper);
```

Road Builder and Find It both use this, e.g. to hide the vanilla right menu
while their tool is open. Find It also extends the vanilla toolbar toggles to
insert its button. Only do this when we genuinely need to change vanilla
UI; every `extend` is a game touchpoint and belongs in `TOUCHPOINTS.md`.

---

## 3. Design tokens: game CSS variables

Never hardcode colours. Both mods use the game's variables; most-used first:

| Variable | Role |
|---|---|
| `--panelColorNormal` | Main panel background |
| `--panelColorDark` | Header / darker strip background |
| `--panelBlur` | `backdrop-filter` for panels |
| `--panelRadius` | Panel corner radius |
| `--menuPanel1`, `--menuPanel2` | Inner sub-panel backgrounds (side detail panel, chips) |
| `--accentColorNormal` (+ `-hover`, `-pressed`) | Primary accent (toolbar button fill) |
| `--accentColorDark` | Selected tab / selected item / header underline |
| `--accentColorDark-focused` | Active/pressed button background |
| `--accentColorLight` | Focused input border |
| `--menuText1Normal` | Default text + icon tint |
| `--menuText2Normal` | Secondary text |
| `--menuTitleNormal` | Titles |
| `--normalTextColor` | Body text |
| `--textColorDim` | Dimmed labels, inactive tabs, empty states |
| `--normalTextColorLocked` | Disabled text/icons |
| `--negativeColor` | Negative/error state |
| `--animation-curve-active-n1-stroke` | Red-ish; Road Builder uses it for pressed delete buttons |
| `--stroke2` | Standard border width |
| `--menuControlBorder` | Control borders |
| `--toolbarHeight` | Offset panels above the bottom toolbar |
| `--fontSizeM`, `--fontSizeXL` | Font sizes |
| `--assetMenuItemSize`, `--assetMenuItemGap`, `--assetMenuColumns` | Vanilla asset-grid metrics |

Literal values the mods use alongside these (fine to reuse):
`rgba(255,255,255,0.1)` light-button background, `0.35` on hover;
`rgba(0,0,0,0.15)` input background; `rgba(255,255,255,0.2)` input border.

### Units and Coherent-specific CSS

- **Use `rem` for every size** (`8rem`, `1550rem`). The game scales `rem`
  with resolution; `px` doesn't scale.
- Typical spacing: 4–10rem padding/margins, 4rem button radius, 8rem panel
  radius, 14–20rem font sizes, 20–24rem icons.
- `coh-font-fit-mode: shrink;` with a `max-width`/`max-height`: shrinks text
  to fit instead of overflowing (Coherent Gameface only). Used on every
  label that can be long (building names, collection names).
- Gameface supports a subset of Chrome's CSS. Stick to flexbox; avoid CSS
  grid and exotic selectors. Verify in-game, not just in a browser.

---

## 4. Icons

- **Tint via mask, not `<img src>`**: render `<img style={{ maskImage: "url(...)" }} />`
  with `background-color: var(--menuText1Normal); mask-size: contain; mask-position: 50% 50%;`.
  The icon then follows theme colours and hover/disabled states.
- **Vanilla icons** are available by path, e.g. `Media/Glyphs/Close.svg`,
  `Media/Misc/Warning.svg`, `Media/Tools/Snap Options/All.svg`, and the icon
  library `coui://uil/Standard/...`, `coui://uil/Colored/...` (e.g.
  `coui://uil/Colored/HouseAlternative.svg`). Zone icons are a natural fit for
  us. Browse them via the localhost:9444 devtools.
- **Our own icons**: put SVGs in `UI/src/images/` and `import icon from "images/X.svg"`.
  Our `webpack.config.js` already emits them under `coui://ui-mods/images/`.
  (Alternative used by Find It: register a host in C# with
  `UIManager.defaultUISystem.AddHostLocation("cs2stylemod", <images dir>, false)`
  and reference `coui://cs2stylemod/X.svg`, which is useful when C# needs to send
  icon paths to the UI.)

---

## 5. Component recipes

These describe Road Builder's structure (MIT) and are safe to copy/adapt.

### Shared SCSS mixins: `UI/src/base.scss`

Copy Road Builder's `src/mods/base.scss` (with MIT notice). It defines:
`textInputBasic`, `baseButton`, `basicButton`, `lightButton`,
`blackButton`, `transparentButton`. Every button/input in our UI should
`@include` one of these rather than define its own colours.

### Large management panel (fits the Collection editor)

Road Builder's "Manage Roads" view (`ManageRoadsView.tsx/.module.scss`) is
the closest analogue to a collection editor: browse a set of items, select
one, edit it on the side.

```
┌─ header (--panelColorDark, bottom border --stroke2 --accentColorDark) ──────────┐
│ [mode tab][mode tab]   [category chips]        [filter btns] [search]   [ ✕ ]   │
├──────────────────────────────────────────────────────────────┬──────────────────┤
│ Scrollable grid of tiles (flex-wrap)                         │ detail panel     │
│ ┌────┐ ┌────┐ ┌────┐ ┌────┐                                  │ (--menuPanel1,   │
│ │thumb│ │    │ │    │ │    │  tile = assetGridTheme.item     │  425rem wide)    │
│ │name │ │    │ │    │ │    │  + own class, 100×120rem        │ thumbnail        │
│ └────┘ └────┘ └────┘ └────┘  selected → --accentColorDark   │ click-to-rename  │
│                                                              │ type chip        │
│                                                              │ divider, options │
│                                                              │ action buttons   │
└──────────────────────────────────────────────────────────────┴──────────────────┘
```

Key details:
- Panel: `position:absolute; top:50%; left:50%; transform:translate(-50%,-50%)`,
  fixed `rem` size, `background-color: var(--panelColorNormal)`,
  `backdrop-filter: var(--panelBlur)`, `border-radius: 8rem`,
  `pointer-events: auto`, `overflow: hidden`, and an entry animation (fade +
  2.5% slide over 0.2s).
- Mode tabs: 120×64rem, icon (20rem, masked) above uppercase bold 14rem
  label, `--textColorDim`; selected gets `--accentColorDark`.
- Category chips: 32rem square icon buttons, top corners rounded only; the
  selected chip expands to 200rem and reveals its label (width/opacity
  transition).
- Close button: `transparentButton`, icon `Media/Glyphs/Close.svg`.
- Grid: `Scrollable vertical smooth trackVisibility="scrollable"`, child div
  `display:flex; flex-wrap:wrap`.
- Tile: `assetGridTheme.item` + own class; 70rem thumbnail, 14rem name with
  `coh-font-fit-mode: shrink`; status icons stacked top-right (20rem,
  drop-shadow); click = select, double-click = primary action.
- Detail panel: name shown as text, becomes `EllipsisTextInput` on click
  (commit on blur); type shown as a chip (`--menuPanel2`, 4rem radius);
  `2rem rgba(255,255,255,0.4)` dividers; action buttons pinned to the bottom
  (`transparentButton`, icon + label, each wrapped in `Tooltip`). Road
  Builder marks delete with a `.danger` class whose `:active` background is
  `var(--animation-curve-active-n1-stroke)`.

### Search box

Road Builder's `SearchTextBox`: plain `<input>` with
`TextInputTheme.input` (from `editor-item.module.scss`) + `textInputBasic` mixin, a custom placeholder
`<span>` overlaid (translated `Editor.SEARCH_PLACEHOLDER`, so it reuses the
game's own "Search..." string), and a magnifier icon that swaps to a clear
button once text is entered (`Button variant="icon"`, `FOCUS_DISABLED`).

### Toolbar entry button

`moduleRegistry.append("GameTopLeft", ModIconButton)`: a
`Button variant="floating"` with `--accentColorNormal` fill (+ `-hover`,
`-pressed`), white masked icon; when the panel is open, invert to white
background with accent-coloured icon. Wrap in `Tooltip`.

### Docked asset-menu-style panel (Find It pattern, describe-only)

Find It places its panel exactly where the vanilla asset menu lives by
nesting inside the game's own layout classes:
`GameMainScreenTheme.toolMainColumn` → `GameMainScreenTheme.toolPanel` →
content with `AssetMenuTheme.assetPanel`, and offsets from the bottom using
`calc(2 * var(--toolbarHeight))`. That makes it look like part of the
vanilla build menu. This is a game-module technique, so we may use it, but
write our own implementation; don't copy Find It's files.
Relevant for a future in-tool building picker (e.g. "add this building to a
collection" while browsing), not for the main Collection editor.

---

## 6. Licensing

- **Road Builder (MIT)**: copying `base.scss`, `vanillacomponentresolver.tsx`,
  and component code is allowed. Keep `Copyright (c) 2024 JadHajjar` + the MIT
  permission notice: add `THIRD_PARTY_NOTICES.md` at repo root listing what
  was copied, and a one-line header comment in each copied file.
- **Find It (no license)**: do not copy code, SCSS, or images. Patterns
  described in this guide (which vanilla modules it uses, the docking
  approach) are fine to reimplement independently.
- **Game assets** (vanilla modules, `Media/...`, `coui://uil/...` icons):
  referenced at runtime from the user's own install, never copied into the
  repo.

---

## 7. Workflow for getting UI right (prompting notes)

What didn't work: wireframes + "make it look like Road Builder". Wireframes
specify layout; the gap is visual language, which lives in the details
above. What works better:

1. **Infrastructure first.** Add `base.scss`, the vanilla resolver, the
   images folder, and the toolbar button before touching panel layout.
2. **Build bottom-up, one component per change.** Button → search box →
   tile → detail panel → assembled panel. Each one must use only game CSS
   variables, `rem`, and a mixin or vanilla theme class.
3. **Name the reference precisely.** "Build `CollectionTile` following
   Road Builder's `ManageRoadConfigListItem.tsx` + `.module.scss` structure",
   not "make it look nice".
4. **Screenshot diff loop.** Give two screenshots (reference, ours) and ask:
   *"List the 10 most visible differences, ordered by impact. Fix the top 3.
   For each, name the reference file/class it comes from."* Repeat.
5. **Verify in-game**, ideally with localhost:9444 open to inspect computed
   styles when something looks off.
6. **Then apply the wireframe**: layout from the wireframe, visual language
   from this guide.

---

## 8. Suggested first steps for the Collection editor

1. Add `UI/src/base.scss` and `UI/src/vanilla/VanillaComponentResolver.tsx`
   (from Road Builder, MIT, trimmed), plus `THIRD_PARTY_NOTICES.md`.
2. Add a `GameTopLeft` toolbar button that toggles the editor (C# side: a
   `ValueBinding<bool>` for visibility + toggle trigger in
   `CollectionUISystem`).
3. Replace `CollectionPanel`'s shell with the management-panel layout:
   collections as tiles on the left, selected collection's details on the
   right (rename via `EllipsisTextInput`, entry count, actions: add
   buildings, build/rebuild zone, delete).
4. Replace the raw `<input>`/"Create" row with a header "+" icon button that
   creates "New collection" and immediately opens rename.
5. Move every inline style into `CollectionPanel.module.scss`.

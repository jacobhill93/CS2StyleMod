# UI Overhaul Plan

Goal: replace today's four independent floating panels (Collections,
Zones, BuildingPicker, ZoneCollectionsPicker) with a single consolidated
window, closer to Road Builder's actual shipped UI and the external
wireframe spec (`zone-builder-wireframes.md` content, referenced from
`interaction-rules.md`/`ui-spec.md` - all three were cross-labeled when
handed to us; treat by content, not filename). This is a real rewrite,
not an incremental patch - written as discrete phases so it can be picked
up step by step, by anyone (a future session included), without
re-deriving context each time.

**Read `DECISIONS.md` first** - specifically every entry from the
"wireframe review" one onward. It has the load-bearing findings this plan
assumes as given: `cs2/ui`'s `draggable` Panel is unusable (no way to
read a dragged position back out - confirmed, not guessed), icon+text
`Button` combos render broken in this environment (icon-only + `variant=
"icon"` + `tooltipLabel` is the confirmed-working pattern), `var()` only
works in longhand CSS properties, `ConfirmationDialog`/`DialogStack` is
still unverified (nobody reviewed this session used it), and
`ExtendedUISystemBase` (adapted from Road Builder, MIT) already replaces
the old manual `ValueBinding`/`TriggerBinding` wiring.

## What we're drawing from, concretely

**Road Builder** (github.com/JadHajjar/RoadBuilder-CSII, MIT - confirmed
by reading `LICENSE` directly):
- One `Router` component, one C#-owned mode enum
  (`roadBuilderToolMode$` → `Picker | ManageRoads | ActionSelection |
  Editing | EditingSingle | EditingNonExistent`), a `switch` renders a
  different fixed panel set per mode. Not four independently-visible
  panels - one coherent navigation state.
- Panels are plain (non-draggable) `Panel`, positioned with fixed
  `top/right/bottom` CSS offsets per mode - not user-draggable or
  resizable at all. They do account for the game's own FPS-meter overlay
  shifting position, which is worth copying regardless of the rest of
  this plan.
- `ExtendedUISystemBase` (already adapted into this repo) is Road
  Builder's own pattern for C#↔UI bindings.
- Reuses the vanilla game's own theme CSS classes via `getModule("game-ui/
  menu/themes/dropdown.module.scss", "classes")` and
  `VanillaComponentResolver` for pixel-perfect native-looking list
  rows/grids, instead of hand-styling everything. Their own code admits
  this is reverse-engineered ("the types of properties are just
  guessed... found by poking at devtools") - useful, but treat as a
  polish-phase risk, not a foundation to build Phase 1 on.
- Click-to-rename inline text pattern (`ManageRoadPanel.tsx`): click a
  label, it becomes a text input, commits on blur.
- Hand-rolled drag-and-drop for reordering (mousedown/mousemove/mouseup +
  a ghost element + drop-zone hook) - relevant only if/when we want
  drag-to-reorder buildings within a collection; not needed for the core
  rebuild.

**The wireframe spec** (`docs/wireframes.md`'s real content, sections A-J):
- Conceptual hierarchy Zone → Collections → Assets (matches our actual
  `CustomZoneDefinition` → `Collection` → `ExplicitAssetEntry` model
  exactly - no translation needed).
- List rows select to open a detail/membership sub-view, rather than
  every action living inline in the list row forever.
- A **Collection detail/membership view** (section D) we have never
  built: per-asset rows with their own remove action, instead of only
  ever reopening the full, unfiltered building picker.
- A bounded (not full-screen) asset picker dialog (section E) with
  checkbox multi-select, already-included assets pre-checked (not
  hidden), a live "N selected" counter, and `Add (N)` - different from
  our current one-click-immediate-add-per-row.
- Create/Edit as modal dialogs (sections F/G), same form component for
  both modes (title/submit label differ).
- Delete confirmation copy rules (section H): name the entity, say what's
  preserved vs removed. We already built this (hand-rolled, not
  `ConfirmationDialog`) - port forward as-is.
- Empty-state copy per view (section I) - we already built a version of
  this for the current panels; port the pattern, reword per new view.
- Loading/error/validation rules (section J) - partially built (inline
  name-required validation); loading states and save/delete error
  handling are not built at all yet anywhere in this mod.

## Decisions (locked in 2026-10-09)

1. **Fixed position, Road Builder-style - not user-draggable.** The new
   single window sits at a fixed screen offset per view, like Road
   Builder's own panels. `useDraggable`/`DraggableHeader` are NOT used on
   the new shell. (`useResizable`/`ResizeHandle` may still be worth
   reusing for the asset-picker dialog specifically, if it needs to be
   resizable independent of the main window - decide in Phase 4, not
   before.)
2. **Add a third "Assets" tab.** The tab bar is Zones / Collections /
   Assets, matching the wireframe literally - a standalone, filterable
   view of the full building catalog, independent of any collection
   context. This means the Assets tab's list and the add-to-collection
   picker dialog (Phase 4) are two different UIs over the same
   underlying data (`GetAllBuildings()`/the `Buildings` binding) - the
   tab is browse-only (no "Add to collection" affordance, no target-
   collection concept), the dialog is picker-only (opened already scoped
   to one collection). Don't try to merge them into one component; they
   have different jobs even though they share filters/search.
3. **Keep one-click-immediate-add, but add Select All / Unselect All.**
   No batch "N selected → Add (N)" confirm step - adding/removing stays
   instant per row. Add two new controls (likely in the picker dialog's
   filter bar, acting on the currently-*filtered* set of rows, not the
   whole catalog - filtering to one zone/level/pack combo then "Select
   All" is the obvious use case) that call `AddBuildingToCollection`/
   `RemoveBuildingFromCollection` once per visible row. Needs a bulk
   variant: either loop individual trigger calls client-side (simplest,
   fine for realistic list sizes after filtering) or add a real
   `AddBuildingsToCollection(collectionId, prefabId[])` trigger
   server-side if looping triggers turns out to be slow/janky once
   actually tried - don't build the server-side bulk trigger speculatively,
   confirm the simple client-side loop is insufficient first.
4. **Keep inline Create.** Edit stays "open the relevant detail/
   membership view," not a form modal, unless/until there's more to edit
   than rename + membership.

## Phased plan

Each phase should be buildable, testable in-game, and committable on its
own - same discipline as every chunk this session. Don't start a phase
until the previous one is confirmed working.

### Phase 0 - Decided (2026-10-09)

Done - see "Decisions" above.

### Phase 1 - Shell + navigation, no feature logic yet

- One new top-level component (e.g. `ShellFeature.tsx`) replacing
  `CollectionsFeature`/`ZonesFeature`'s separate `moduleRegistry.append`
  calls with a single one.
- One `Panel`, fixed `top/left` (or `top/right`) CSS offset, not
  `useDraggable` - matches decision #1. Check the game's own FPS-meter
  overlay offset handling in Road Builder's `SidePanel.module.scss`
  (`&.fpsLevel1/2/3`) before picking a fixed position, so the new window
  doesn't end up colliding with it the way nothing in this mod has had
  to consider before.
- Header, tab bar: Zones / Collections / Assets (decision #2), client-
  side `useState` for the active tab - no need for a C#-owned mode enum
  the way Road Builder has one, since our tabs aren't a sequential
  tool-mode workflow, just three independent views. Reconsider only if
  a later phase reveals real shared cross-tab state that needs a C#
  source of truth.
- Empty tab bodies, no list/CRUD/filter logic yet in any of the three.
  Confirm in-game: the shell opens at the right fixed position (doesn't
  overlap the FPS meter or other HUD), tabs switch, nothing else works
  yet.

### Phase 2 - Zones tab: list + detail

- Port `ZonePanel`'s list rendering, Create row, delete-confirmation,
  empty state into the Zones tab body.
- Add the list→detail navigation the wireframe calls for: clicking a
  zone row opens a detail view (wireframe section B) showing its
  attached collections, with `Edit`/`Remove` per collection and a
  `+ Create Collection and Add to Zone` shortcut (new - a combined
  create+attach action we don't have today; `CreateCollection` then
  `AddCollectionToZone` back to back covers it).
- This detail view functionally replaces today's separate
  `ZoneCollectionsPicker` panel - port its attach/detach logic in, don't
  rebuild it from scratch.
- Confirm in-game: create/select/edit-attach-collections/delete all work
  through the new nested list→detail flow, not just the flat list.

### Phase 3 - Collections tab: list + detail (the genuinely new piece)

- Port `CollectionPanel`'s list/Create/delete-confirmation/empty state
  into the Collections tab body.
- Build the **Collection detail/membership view** (wireframe section D) -
  this does not exist anywhere in the mod today. Lists the collection's
  own `ExplicitAssetEntry`s (need a binding for this - `CollectionUIEntry`
  already carries `prefabIds`, likely enough with a name/zone-type lookup
  against the existing `Buildings` binding) with a per-row remove action
  (`RemoveBuildingFromCollection`, already exists) and a
  `+ Add Assets` button opening the picker dialog (Phase 4).
- Confirm in-game: create/select/view membership/remove a single
  asset/delete all work.

### Phase 4 - Asset picker as a proper dialog

- Restyle `BuildingPicker`'s existing filter/search machinery (keep all
  of it - zone/level/lot/pack filters, search) as a bounded dialog
  (wireframe section E) opened from a Collection detail's `+ Add Assets`,
  rather than its own always-available floating panel.
- Keep one-click-immediate-add (decision #3) - no batch confirm step.
- Add **Select All** / **Unselect All**, scoped to the currently
  *filtered* rows, not the whole catalog. Start with the simplest
  possible implementation - loop `trigger("AddBuildingToCollection", ...)`
  /`RemoveBuildingFromCollection` once per visible filtered row,
  client-side. Only build a real server-side bulk trigger
  (`AddBuildingsToCollection(collectionId, prefabId[])`) if that loop is
  actually too slow/janky once tried in-game - don't build it
  speculatively (decision #3's own note).
- Decide whether this dialog reuses `useResizable`/`ResizeHandle` for its
  own sizing (optional per decision #1 - the main shell doesn't use it,
  this dialog still could).
- Confirm in-game: opens pre-scoped to the right collection, filters
  still work, Select All/Unselect All only touch the filtered set, adding
  reflects immediately in the Collection detail view underneath.

### Phase 4b - Assets tab (standalone browse view)

- Decision #2: a real third tab, not just the picker dialog. Reuses the
  same filter/search machinery and the same `Buildings` binding as the
  Phase 4 dialog, but browse-only - no target-collection concept, no
  Add/Remove actions at all. Keep this as a second, simpler consumer of
  the shared filter logic, not a variant mode bolted onto the picker
  dialog component itself (different enough jobs - see decision #2's own
  note - that forcing one component to do both will fight itself).
- Confirm in-game: tab lists/filters the full catalog correctly, no
  accidental way to add/remove anything from it.

### Phase 4c - Asset thumbnails (both Phase 4's picker dialog AND Phase 4b's Assets tab need this)

Raised late (2026-10-09, after the rest of this plan was already written)
- a real gap: nothing above puts a visual thumbnail next to a building
row, and a raw `PrefabId` like `CN_ResidentialLow01_L3_2x2` doesn't tell
a player what the building actually looks like. Needs a research pass
before implementation, not a guess - two candidate approaches, in
preference order:

1. **Read the prefab's own icon path directly**, via `GameBuildingCatalog`
   (C# side). The vanilla zoning/building tool already shows a thumbnail
   per building, so that data exists on the prefab somewhere - worth
   checking `Game.Prefabs.UIObject` first (already touched by
   `ZonePrefabCloner` for "UI category/icon" when cloning a zone, per
   `TOUCHPOINTS.md`) for an icon path/reference field, the same way every
   other game API this session was confirmed via reflection before
   trusting it. If real and accessible, add it to `BuildingBrowserEntry`/
   `BuildingPickerEntry` (a new field, e.g. `IconPath`) and render it as
   a plain `<img src=.../>` per row. Strictly the better option if it
   works: zero dependency, covers vanilla/DLC/Workshop assets uniformly,
   no coverage gaps.
2. **Asset Icon Library (AIL)** (github.com/JadHajjar/AssetIconLibrary-
   CSII, MIT, same author as Find It/Road Builder - already researched
   this session, see the icon-library research entry in `DECISIONS.md`).
   Purpose-built for exactly this (~8,000-34,000 per-asset thumbnails),
   but depends on AIL being installed and its own icon map actually
   covering the specific assets a player has. Two real integration
   mechanisms confirmed: `coui://ail/<path>` URLs from `.ail` JSON
   mapping files, or a `GetIconsMap(string style)` static hook a
   consuming mod implements on its own `IMod` class that AIL calls
   reflectively. Fall back to this only if option 1 turns out not to be
   real/accessible.

Confirm in-game: thumbnails actually render for a representative mix of
vanilla, DLC, and (if available) Workshop buildings - not just the first
few rows, since a partial-coverage icon source would need a visible
fallback (blank/placeholder, not a broken image) for prefabs it can't
resolve.

### Phase 5 - Loading, error, and validation states

- None of this exists yet anywhere in the mod (every trigger either
  silently succeeds or silently no-ops). Wireframe section J has the
  rules: keep form values on error, concise inline messages, don't close
  a confirm dialog until the backend actually confirms.
- Realistically low-value until a trigger can actually *fail* in a way
  worth surfacing - audit `CollectionUISystem`/`ZoneUISystem` for any
  real failure path (e.g. a duplicate name, a genuinely invalid base
  zone type) before inventing UI for failures that can't currently
  happen.

### Phase 6 - Polish

- Carry forward the icon work already done (icon-only buttons,
  `tooltipLabel`, Unified Icon Library) into the new shell - should be a
  near-direct port.
- Consider `getModule()`-based vanilla theme reuse (Road Builder pattern)
  for list rows, if the plain `cs2/ui` styling still looks visibly
  "off" once everything else is working. Treat as optional and
  lower-confidence (reverse-engineered API, could break on a game
  update) - verify each specific `getModule()` path against the actual
  installed game files the same way every other API in this project has
  been verified this session, don't copy Road Builder's exact calls on
  faith.
- Revisit `ConfirmationDialog`/`DialogStack` only if a real working
  example ever turns up (still unverified) - otherwise keep the
  hand-rolled confirmation pattern already proven working.

## What NOT to do

- Don't attempt all six phases in one sitting. Each one touches enough
  surface area (and the early ones remove/replace currently-working
  panels) that skipping the in-game confirmation step between phases is
  how this session's worst regressions happened (`<select>`, `draggable`
  Panel, icon+text buttons) - all three were shipped on an assumption
  that wasn't actually tested yet.
- Don't delete `CollectionPanel.tsx`/`ZonePanel.tsx`/`BuildingPicker.tsx`/
  `ZoneCollectionsPicker.tsx` until the new shell's equivalent is
  confirmed working in-game and committed - keep the old, working panels
  reachable (even if unregistered from `index.tsx`) as a fallback until
  then.

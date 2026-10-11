# UI Overhaul Plan

**Status (2026-10-11): Phases 0-4c and 6 are implemented and confirmed
working in-game (committed - the four old panels this plan replaces are
deleted, not just unregistered). Only Phase 5 (loading/error/validation
states) remains, and it's explicitly low-priority until a trigger can
actually fail in a way worth surfacing - see that section.** The rest of
this document is kept as the historical record of how each phase's
design actually played out (several reversed course after an in-game
test surfaced something the plan didn't anticipate) - read it for that
context, not as a live TODO list.

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
   context, browse-only by default (no "Add to collection" affordance).
   **Revised 2026-10-09**: originally planned as a separate always-
   browse-only component with its own add-to-collection picker dialog
   living elsewhere - reversed after the separate dialog's first in-game
   test hit a real positioning bug (see Phase 4/4b below for the full
   story). The Assets tab itself now doubles as the picker, gaining
   Add/Remove/Select-All controls only when entered via a Collection's
   "+ Add Assets" - still the same two jobs (browse vs. pick), just one
   component with a mode instead of two.
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

### Phase 4 / 4b - Asset picking, merged into the Assets tab (revised 2026-10-09)

Originally planned as two separate things - a bounded picker dialog
(Phase 4, opened from a Collection detail's `+ Add Assets`) and a
standalone browse-only Assets tab (Phase 4b) - per decision #2's
reasoning that they have different enough jobs to justify separate
components. **Built that way first, then merged into one component after
the first in-game test of the separate dialog surfaced a real bug**: a
`position:absolute` dialog nested inside the shell's own
`position:absolute` wrapper resolves against the SHELL's box, not the
screen, so it rendered on top of/inside the Collections panel instead of
as a proper overlay. Fixing it meant lifting the dialog out to be a
sibling of the shell, which also made clear the picker and the browse
tab only differed by a handful of conditional props (Add/Remove button
per row, Select All/Unselect All, a header) - not enough to justify a
second floating panel and its own class of positioning bugs.

Current shape: `AssetsTab` takes an optional `pickingForCollectionId` -
absent when opened from the tab bar (plain browse, decision #2's
original intent), set by `ShellFeature` when arriving via a Collection's
`+ Add Assets` (switches the active tab to Assets and shows the
Add/Remove/Select-All controls, scoped to that collection). A "Done"
button returns to the Collections tab, reopening the same collection's
detail view (`ShellFeature` keeps the collection id around across that
trip and hands it back to `CollectionsTab` as
`initialSelectedCollectionId`, since tabs unmount on switch and would
otherwise lose that selection).

Kept from the original Phase 4 plan: one-click-immediate-add (decision
#3, no batch confirm step), Select All/Unselect All scoped to the
*filtered* rows via a client-side loop of individual trigger calls (only
worth a real server-side bulk trigger if that loop proves too slow/janky
in practice - not built speculatively).

Confirm in-game: Assets tab opened directly from the tab bar is
browse-only (no Add/Remove anywhere); opened via "+ Add Assets" it's
scoped to the right collection, filters still work, Select All/Unselect
All only touch the filtered set, adding reflects immediately, and "Done"
returns to that same collection's detail view.

### Phase 4c - Asset thumbnails (both Phase 4's picker dialog AND Phase 4b's Assets tab need this)

Raised 2026-10-09 (after the rest of this plan was already written) as a
real gap: nothing above puts a visual thumbnail next to a building row,
and a raw `PrefabId` like `CN_ResidentialLow01_L3_2x2` doesn't tell a
player what the building actually looks like.

**Research pass done (2026-10-09, via live reflection against the
installed `Game.dll`, same discipline as every other API claim this
project makes) - the original preference order was wrong, and reversed:**

- `Game.Prefabs.UIObject.m_Icon` (`string`) is real and confirmed to
  exist (`ZonePrefabCloner`'s own `m_Group` field is a sibling on the
  same component). But it's populated with static category/zone-type
  icons (confirmed: `Content/Game/UI/Media/Game/Icons/Zone*.svg` on
  disk) - not a per-building render. Even where a building prefab does
  carry one, it'd be the same icon for every building of that zone type,
  which doesn't solve "which specific building is this" - the actual
  problem statement.
- The real mechanism vanilla uses for per-asset thumbnails is
  `Game.UI.Thumbnails.ThumbnailCache.GetThumbnail(Object obj, int width,
  int height, Camera camera)` - a **runtime 3D render** of the actual
  prefab via a Unity `Camera`, not a static file path. Confirmed via the
  same reflection pass (full method signature read directly off
  `Game.dll`). This is a meaningfully bigger lift than "read a field" -
  it needs a render target, a camera, and a texture handoff into
  something cohtml's `coui://` scheme can serve, none of which this
  session has verified end-to-end.

**Implemented (2026-10-09), confirmed in-game that it renders correctly
(placeholders show with no crash/broken images) - whether AIL's writes
are actually picked up is still a separate, open question (see below) -
second research pass flipped the approach again:** reading AIL's own actual source
(`IconReplacerSystem.cs`, fetched and read directly, not guessed from
the README) showed it doesn't just expose a map for other mods to query
- it **writes its resolved thumbnail URL directly into
`UIObject.m_Icon`** on every building prefab it covers
(`uIObject.m_Icon = thumbnail;`). That URL is the real
`coui://ail/<path>` scheme, built by `IconLibraryUtil.GetAvailableIcons()`
from `.ail`/`ail` mod folders, `.ail` JSON map files, *and* reflection-
based `GetIconsMap()`/`GetIconsRootFolders()` calls into every other
loaded mod's `IMod`-implementing class (`IconAPIUtil.LoadModIcons()`) -
all three of the README's integration paths funnel into the same field.

So the originally-ruled-out "read `UIObject.m_Icon` directly" option was
right after all, just for the wrong reason the first time: vanilla
leaves it empty per-building, but **AIL (if installed) populates it for
us** - no `GetIconsMap`/`.ail` integration needed on this mod's side at
all, just read the field AIL already writes into. Implemented as
`GameBuildingCatalog.ResolveIconPath` (same `prefab.TryGet<UIObject>`
pattern `ZonePrefabCloner` already uses for `m_Group`), threaded through
`BuildingBrowserEntry`/`BuildingPickerEntry`/the `Buildings` binding as
`iconPath`, rendered via a new `shared/BuildingThumbnail.tsx` (visible
placeholder box when empty, never a broken `<img>`) in the Assets tab
and the Collection detail's membership list.

**Risk resolved, confirmed in-game with AIL actually installed
(2026-10-12)**: `BuildingPickerUISystem` reads `m_Icon` once, in
`OnGameLoaded` - the worry was AIL populating it *after* that point
(mod load order between two independent mods isn't controlled), leaving
every icon permanently empty. Not the case - most buildings show real
AIL thumbnails. One real, separate gap: NA Old Town assets specifically
show no icon - believed to be AIL's own coverage gap for that pack
(newer/less common asset packs can lag behind in AIL's icon database),
not a timing issue, since unrelated buildings work fine. See
DECISIONS.md.

Fallback, only if AIL doesn't pan out: `Game.UI.Thumbnails.ThumbnailCache
.GetThumbnail(Object obj, int width, int height, Camera camera)` - a
genuine runtime 3D render, confirmed real via the same reflection pass
but never pursued further (needs a render target, a camera, and a
texture handoff into cohtml's `coui://` scheme, none of which is
verified end-to-end) - would need its own dedicated research pass before
any implementation attempt.

### Phase 5 - Loading, error, and validation states

**Audit done (2026-10-12), one real failure path found and fixed on the
C# side - see DECISIONS.md "Phase 5 audit found a real bug" for the
full story.** `JsonFileCollectionLibrary`/`JsonFileCustomZoneDefinitionLibrary`
did raw file I/O with zero exception handling on every single mutation
trigger - a genuinely possible failure (disk full, file locked,
permissions denied), not a hypothetical one. Fixed with strong exception
safety in Core and logging in the UI layer (`CollectionUISystem`/
`ZoneUISystem`'s new `TrySave`/`TryDelete` helpers). Caught and fixed a
second, deeper bug along the way: `TryGet` was returning the library's
live object, not a copy, which silently defeated the rollback for the
real `TryGet -> mutate -> Save` pattern every caller actually uses - see
the DECISIONS.md entry for why that one only showed up once the test
was written to match the real call pattern, not an idealized one.

**Still open - the duplicate-name check and the `RenameCollection`/
`RenameZone` dead-trigger cleanup, explicitly deferred by user request,
not forgotten.** No UI-visible error/loading state exists yet (the
C#-side fix above prevents the bad outcome, but a failed save today
still looks the same to the player as nothing happening - no toast, no
inline message). Wireframe section J's rules (keep form values on
error, concise inline messages, don't close a confirm dialog until the
backend confirms) remain unimplemented - revisit only if the current
"silently does nothing, logged for diagnosis" behavior turns out to be
insufficient in practice, same discipline as everything else in this
plan.

### Phase 6 - Polish

**Done and confirmed in-game (2026-10-09):**

- Icon work was already carried forward during Phases 2-4b (icon-only
  buttons, `tooltipLabel`, Unified Icon Library) - no separate porting
  step needed.
- New `shared/ListRow.module.scss` (`.row`/`.primary`/`.secondary`/
  `.actions`/`.emptyState`/`.clickable`) replaces the ad hoc inline
  style objects each tab had grown its own slightly different copy of -
  applied across Zones/Collections/Assets tabs and their detail views.
  Adds a subtle hover highlight and a row divider, and moves secondary/
  metadata text to `var(--textColorDim)` instead of a raw opacity value.
- `ShellFeature`'s header got a darker sub-background + bottom border
  separating it from the tab bar, matching Road Builder's real shipped
  header treatment (`.header`/`.title` in its `SidePanel.module.scss`,
  fetched and read directly, not copied from memory).
- Found and removed a real bug while doing this: the shell's `.body`
  class had carried a blanket `opacity: 0.8` forward from Phase 1, where
  it was meant to dim the "coming soon" placeholder text - but since it
  applied to the whole tab body, it was dimming every tab's real content
  once Phases 2-4b filled them in. Likely a real, measurable contributor
  to the "looks drab" feedback that kicked this phase off, not just a
  missing-polish gap.
- All new tokens (`--panelColorDark`, `--accentColorDark`, `--stroke2`,
  `--textColorDim`) confirmed real by grepping the actual installed
  game's compiled stylesheet (`Content/Game/UI/index.css`) for each one
  - not copied from Road Builder on faith.

**Deliberately not attempted in this pass:**

- `getModule()`-based vanilla theme reuse (Road Builder pattern) for
  pixel-perfect native list rows - still optional and lower-confidence
  (reverse-engineered API, could break on a game update). Worth
  revisiting only if the above still looks visibly "off" once confirmed
  in-game.
- `ConfirmationDialog`/`DialogStack` - still unverified, no real working
  example has turned up. Kept the hand-rolled confirmation pattern,
  which is proven working.

Confirm in-game: header/tab bar read clearly, list rows show a visible
hover highlight and aren't all full-opacity-identical anymore, nothing
regressed (especially the delete-confirmation rows, which intentionally
don't get the new row styling - their stacked layout is structurally
different).

## What NOT to do

- Don't attempt all six phases in one sitting. Each one touches enough
  surface area (and the early ones remove/replace currently-working
  panels) that skipping the in-game confirmation step between phases is
  how this session's worst regressions happened (`<select>`, `draggable`
  Panel, icon+text buttons) - all three were shipped on an assumption
  that wasn't actually tested yet. This discipline held: every phase
  above was confirmed in-game before the next one started, and the two
  real bugs that did slip through (the asset-picker dialog's positioning
  bug, the shell's leftover `opacity:0.8`) were both caught on the very
  next in-game test, not several phases later.
- (Historical, now done) `CollectionPanel.tsx`/`ZonePanel.tsx`/
  `BuildingPicker.tsx`/`ZoneCollectionsPicker.tsx` were kept unregistered
  as a fallback until the new shell's equivalents were confirmed working
  in-game and committed - that happened, and all four were deleted (see
  DECISIONS.md and the git history for that commit).

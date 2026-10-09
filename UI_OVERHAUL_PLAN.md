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

## Decisions to lock in before writing code

These are real forks, not details - pick one explicitly (a one-line
answer in this file, dated) before Phase 1 starts, so later phases aren't
built on an assumption that gets reversed halfway through.

1. **Single window, fixed layout (Road Builder-style) vs. keep user
   drag/resize?** Road Builder's panels are fixed-position, not
   user-movable. We spent real effort this session building and
   debugging hand-rolled drag/resize (`useDraggable`/`useResizable`,
   `UI/src/shared/`) - recommend keeping ONE outer window draggable
   (reuses that work, low cost, one panel instead of four) but dropping
   per-panel resize in favor of fixed internal layout once everything's
   consolidated into one shell, matching Road Builder's actual approach.
   **Not yet decided.**
2. **Does "Assets" need its own tab**, or does it only ever appear as the
   add-to-collection picker dialog? The wireframe shows both a tab (A)
   and a dialog (E) for assets, which is internally inconsistent with
   itself. Recommend: no separate Assets tab for v1 (nothing today needs
   browsing all assets outside the context of adding them to a
   collection) - just Zones and Collections as the two top-level views,
   Assets only ever as the picker dialog. **Not yet decided.**
3. **Asset picker: keep one-click-immediate-add, or switch to the
   wireframe's batch "N selected → Add (N)"?** We deliberately chose
   immediate-add earlier this session for lower friction on a repeated
   action (see `DECISIONS.md`). Recommend keeping it - the wireframe's
   batch pattern suits a one-time setup flow, ours suits "I'm quickly
   building a themed collection while half paying attention to the
   city." **Not yet decided, but leaning keep-current.**
4. **Create/Edit: modal dialogs (wireframe) or keep today's always-
   visible inline form?** Inline-create has been low-friction and
   already works. Recommend: keep inline Create (it's one text field +
   a button, a modal adds a step for no real benefit), but make Edit a
   proper modal/detail view **only** if Edit needs to hold more than a
   rename (e.g. once Zone base-type ever becomes editable, which
   `DECISIONS.md` currently says it deliberately isn't). For now, Edit
   stays "open the relevant detail/membership view," not a form modal.
   **Not yet decided.**

## Phased plan

Each phase should be buildable, testable in-game, and committable on its
own - same discipline as every chunk this session. Don't start a phase
until the previous one is confirmed working.

### Phase 0 - Decide the four forks above, write the answers into this file

No code. Just resolve the open questions, dated, so Phase 1 has a stable
target.

### Phase 1 - Shell + navigation, no feature logic yet

- One new top-level component (e.g. `ShellFeature.tsx`) replacing
  `CollectionsFeature`/`ZonesFeature`'s separate `moduleRegistry.append`
  calls with a single one.
- One `Panel`, one header ("CS2StyleMod" or similar), a tab bar (Zones /
  Collections, per decision #2), client-side `useState` for the active
  tab - no need for a C#-owned mode enum the way Road Builder has one,
  since our tabs aren't a sequential tool-mode workflow, just two
  independent lists. Reconsider only if Phase 1 reveals real shared
  cross-tab state that needs a C# source of truth.
- Reuse `useDraggable`/`DraggableHeader` on this one outer window per
  decision #1. Drop `useResizable`/`ResizeHandle` from it if decision #1
  says fixed layout (keep the files in `UI/src/shared/` regardless -
  possibly still useful for the asset-picker dialog's own sizing).
- Empty tab bodies, no list/CRUD logic yet. Confirm in-game: the shell
  opens, tabs switch, drag still works, nothing else does yet.

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
- Apply decision #3 (keep immediate-add, or switch to batch-select).
- Confirm in-game: opens pre-scoped to the right collection, filters
  still work, adding reflects immediately in the Collection detail view
  underneath.

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

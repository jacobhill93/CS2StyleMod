# UI Overhaul Plan

**Status (2026-10-12): done.** Phases 0-4c, 6, and most of 7 are built
and confirmed in-game; Phase 5 is intentionally minimal (see below);
Phase 7.3 was tried and reverted. The four old floating panels
(`CollectionPanel`/`ZonePanel`/`BuildingPicker`/`ZoneCollectionsPicker`)
are deleted, not just unregistered.

Condensed 2026-10-12 ahead of a new incoming UI guide - this is no
longer a live plan, just a compact index: what shipped, the durable
engine constraints worth not re-discovering, and where to find the full
story if needed. **As a working document for new UI work, this file is
now superseded by `UI_UPGRADE_GUIDE.md`** (not yet committed as of this
note) - `DECISIONS.md` remains the historical record either way. Phase
numbers/decision numbers below are still referenced by name in code
comments throughout `UI/src/` - kept stable for that reason even though
the surrounding prose is gone. Full blow-by-blow reasoning for anything
here lives in `DECISIONS.md`
(dated entries) and git history, not reproduced here.

## Durable engine constraints (confirmed, not guessed - read before writing new UI)

- `cs2/ui`'s `draggable` `Panel` is unusable - no way to read a dragged
  position back out.
- Icon+text `Button` combos render broken. Icon-only + `variant="icon"`
  + `tooltipLabel` is the confirmed-working pattern.
- `var()` only works in longhand CSS properties, never shorthand, and
  has no fallback-argument support.
- A plain-HTML `<select>` fallback once crashed the *entire* game HUD,
  not just this mod's panel - never reach for one.
- A `position:absolute` element nested inside another `position:absolute`
  wrapper resolves against that wrapper's box, not the screen - any new
  overlay/dialog must be a sibling of the shell, not a descendant.
- `ConfirmationDialog`/`DialogStack` are unverified - no working example
  has ever turned up. The hand-rolled confirm-row pattern is proven and
  what's actually used everywhere.
- `ExtendedUISystemBase` (adapted from Road Builder, MIT - see
  `THIRD_PARTY_NOTICES.md`) replaces manual `ValueBinding`/
  `TriggerBinding` wiring - use it for any new UI system.
- Reusing vanilla's own CSS classes via `getModule`/
  `VanillaComponentResolver` works, but verify every module path/class
  against the actually-installed game (grep `Content/Game/UI/index.js`
  or `index.css`, or the UI-devtools-mode `localhost:9444`) before
  trusting a reference mod's claim - two confirmed real, one confirmed
  wrong for our use case (see Phase 7 below).

## Decisions (locked 2026-10-09)

1. Fixed position per view, not user-draggable (`useDraggable` unused
   on the shell).
2. Three tabs - Zones / Collections / Assets. The Assets tab doubles as
   the "add to collection" picker (a `pickingForCollectionId` mode), not
   a separate dialog - reversed from an original two-component plan
   after the nested-`position:absolute` bug above.
3. One-click-immediate add/remove, no batch confirm step. Select
   All/Unselect All act on the currently *filtered* rows.
4. Inline create; edit opens the relevant detail/membership view, not a
   form modal.

## Phases

- **0** - the decisions above.
- **1** - `ShellFeature.tsx`: one fixed-position `Panel`, tab bar,
  replacing the four old floating panels' separate registrations. Done.
- **2** - Zones tab: list + list→detail (attached collections,
  attach/detach). Done.
- **3** - Collections tab: list + detail/membership view (new - didn't
  exist before this plan). Done.
- **4/4b** - Asset picking, merged into the Assets tab per decision #2.
  Done.
- **4c** - Asset thumbnails. Vanilla's real per-building thumbnail
  mechanism is a runtime 3D render (`ThumbnailCache.GetThumbnail`), never
  pursued - instead reads `UIObject.m_Icon`, which Asset Icon Library (if
  installed) writes a `coui://ail/...` URL into directly
  (`GameBuildingCatalog.ResolveIconPath`). Done, confirmed in-game with
  AIL installed; one known gap (NA Old Town assets) believed to be AIL's
  own coverage gap, not ours.
- **5** - Loading/error/validation states. C#-side exception safety done
  (`FolderCollectionLibrary`/`FolderCustomZoneDefinitionLibrary`'s
  `Save`/`Delete` roll back cleanly on a real I/O failure). UI-visible
  error/loading affordances (toasts, inline messages) still don't exist
  anywhere - deliberately low priority until a real failure is actually
  hard to diagnose without one.
- **6** - Polish. Shared `ListRow.module.scss` row styling, vanilla CSS
  tokens (confirmed real against the installed game's stylesheet, not
  copied from Road Builder on faith), header treatment. Done.
- **7** - Vanilla module reuse (`getModule`/`VanillaComponentResolver`).
  Input retrofit (`TextInputTheme` on all raw `<input>`s) done and
  confirmed working. List-row retrofit (`assetGridTheme.item`) tried,
  confirmed broken in-game ("everything looks smushed" - that class is a
  fixed-size centered grid tile, not row styling - there was never a
  real vanilla analogue for a row-shaped list here), fully reverted. Icon
  toolbar button still deferred (host dock too small, icon+text broken).
  See DECISIONS.md "Phase 7.3 reverted" and "Phase 7: vanilla module
  reuse" for the full story.

## What NOT to do

Don't attempt multiple phases in one sitting without an in-game
confirmation between them. Every regression this plan ever produced
(`<select>` crashing the HUD, the draggable-`Panel` dead end, icon+text
buttons, the nested-`position:absolute` dialog bug, Phase 7.3) was an
assumption shipped before it was actually tested - and every one was
caught fast specifically because the next phase wasn't started first.

# Decisions & open questions

## Decisions

### Assets-tab tile grid built, isolated, confirmed in-game, now the default (2026-10-13)
`UI_UPGRADE_GUIDE.md` Phase 4c - the one item on Phase 4's menu that's a
real redesign, not a retrofit: an opt-in grid view for the Assets tab,
using `assetGridTheme` (`item`/`row`/`thumbnail`) the way Phase 7.3
should have - on actual square tiles, not full-width rows. The user
explicitly asked up front whether this could be cleanly reverted if it
doesn't pan out, given how much higher-risk this item is than the rest
of Phase 4's menu.

**Built deliberately isolated**, same discipline as
`PlatterCompatibility.cs`: 3 new files
(`UI/src/vanilla/AssetGridTheme.ts`, `AssetsTileGrid.tsx`,
`AssetsTileGrid.module.scss`) plus a `viewMode` toggle in `AssetsTab.tsx`
that's the *only* thing touching the existing, working row-list code
path - that path is completely untouched and still available via the
toggle. Reverting (if it had not worked out) would have been exactly
"delete those 3 files and the toggle."

**Real engineering decisions made along the way:**
- `classes.itemGrid` deliberately not used - it sizes itself from
  `--assetMenuColumns` (9, tuned for vanilla's full-width asset menu),
  assumed too wide for this mod's fixed 840px shell. Sizing instead
  comes from hand-synced constants, corrected through several rounds of
  real in-game screenshots rather than guessed once and shipped:
  - `TILES_PER_ROW`: started at 6 (on the "9 is too wide" assumption) -
    screenshot showed the right third of the panel empty, since
    `classes.row` has no justify-content stretch rule and just
    left-packs tiles. Tried 9 (vanilla's own count) as the implied fix;
    user judged that too wide without needing a rebuild to check;
    settled on 8, confirmed via screenshot to exactly fill the width.
    Then, per a later user request to enlarge the tiles and give the
    label more room, corrected again to 7, with the tile itself scaled
    up by `TILE_SCALE = 8/7` to keep filling the same width.
  - `TILE_ROW_HEIGHT_PX` (vertical spacing): 100 too small (rows
    crowded/overlapped) -> 140 confirmed clean -> scaling naively by
    `TILE_SCALE` to 160 overshot (too much gap) -> 130 confirmed too
    little -> settled on 150.
  - `TILE_LABEL_SPACE_PX` (room inside the tile for the label): 22 left
    almost no slack versus the label's own content height, so the
    label sat crammed against the tile's edge instead of having visible
    padding - bumped to 34.
- The label moved from outside/below the tile (floating in the dark
  panel background) to inside the tile (sharing the grey backdrop, via
  a `width`/`height`/`flexDirection: column` inline-style override on
  the vanilla `.item_a5m` div - inline wins over the class's own fixed
  square sizing), per user request, and is rendered *above* the
  thumbnail, not below - also per explicit user request after the
  "text is at the bottom" feedback turned out to mean an ordering
  preference, not a padding bug.
- The label element is a `<div>`, not a `<span>` - a `<span>` is inline
  by default, and its `width: 100%`/`text-align: center` weren't
  reliably applying in Coherent Gameface (confirmed in-game: no
  ellipsis truncation, text not centered). A `<div>` is block-level by
  default, giving it a real constrained box; switching fixed both.
- `classes.row`'s own `.row_xSL+.row_xSL { margin-top:
  var(--assetMenuItemGap) }` rule is explicitly neutralized
  (`marginTop: 0`) on each row, since this mod's own absolute-position
  virtualization math already fully accounts for vertical spacing via
  `TILE_ROW_HEIGHT_PX` - letting both apply would double the gap. The
  horizontal `margin-left` rule between tiles in the same row is kept,
  since nothing else computes that.
- The tile container is a plain `<div>`, not `cs2/ui`'s `Button` -
  every existing `Button variant="icon"` usage in this codebase passes
  an `src` icon prop, never arbitrary children, and `Button`'s internal
  structure is unproven against being resized to a vanilla tile class.
  A plain div also matches `.item_a5m`'s own actual DOM shape (confirmed
  from its CSS, not guessed) and still gets real `:hover`/`:active`
  states from that class's CSS regardless of not being a `Button`.
- The "selected" class applied to an already-added building's tile is a
  plain, unhashed literal name (not part of `AssetGridTheme`'s exported
  classes object) inferred from the compiled CSS's own selectors
  (`.item_a5m.selected`) - a reasonable inference, not confirmed against
  whatever vanilla component normally applies it. Still unconfirmed.
- `BuildingThumbnail.tsx` gained an optional `size` prop (inline style,
  not a second className) so this tile grid can use vanilla's own
  `~68rem` thumbnail sizing instead of the component's normal fixed
  48px - inline rather than class-based specifically because two
  different SCSS modules' same-specificity classes would have an
  unpredictable cascade order between them, where an inline style wins
  unambiguously instead.

After all of the above was confirmed clean in-game (correct width fill,
no row overlap, ellipsis/centering working, label positioned as wanted),
the user preferred grid over the row list, so `viewMode` now defaults to
`"grid"` - `"list"` stays available via the toggle, and the row-list
code path remains untouched.

### EllipsisTextInput's Escape key can't be made to cancel - accepted as-is (2026-10-13)
`UI_UPGRADE_GUIDE.md` Phase 3b swapped the rename fields (`shared/
RenameableTitle.tsx`) from Phase 3a's plain `<input>` to the real
vanilla `EllipsisTextInput` component for the native look. Focus+select-
on-open and Enter-commits both worked immediately, confirmed in-game -
but Escape, which Phase 3a correctly treated as cancel-and-discard,
instead committed the typed name, same as Enter.

Two fix attempts, both confirmed to have zero effect in-game: first, a
plain `onKeyDown` branch calling a `cancel()` that skipped the rename
trigger; second, a ref-based flag meant to suppress the `onBlur`-
triggered commit specifically when Escape caused it. Neither changed
the observed behavior at all - not even partially - which points away
from a timing race and toward the key never reaching a DOM `keydown`
listener as plain React synthetic events in the first place. Reading
the compiled component (`Content/Game/UI/index.js`) shows it renders a
"Back"/"Select" hint tied to focus state - strong evidence this is the
game's own input-action system (gamepad/keyboard action bindings, not
raw DOM events) consuming Escape internally as its own "Back" gesture,
which this component apparently treats as "confirm and exit," not
"discard." Overriding that would need reverse-engineering how `Bp`/`_I`
(the component's internal focus-scope and input primitives, minified
names) actually implement that action - deeper investigation with no
guaranteed payoff.

**Accepted, user's call**: kept `EllipsisTextInput` for the native
look; Escape behaves like Enter/blur here instead of canceling. Not
pursued further. If a real cancel gesture is ever needed, the fallback
is reverting this one component back to Phase 3a's plain `<input>`,
which already supports it correctly.

### Migrated to one file per collection (and per custom zone) (2026-10-12)
Prompted by a direct question: "why cannot zone and collections just be
a separate file for each? would this not protect better against a
corrupted file." Correct instinct, and digging into it surfaced
something worse than what was being guarded against: `Load()` on both
the collections and custom-zone-definitions libraries had **zero
exception handling** around the JSON deserialize call. A genuinely
corrupted combined file wouldn't just lose data - the exception would
propagate straight out of the constructor, straight out of
`Mod.OnLoad`, likely taking down the *entire mod* (no UI systems ever
get registered - that all happens later in the same method). One
corrupted file risked everything, not just itself.

**Collections**: `ICollectionLibrary`'s old single-file implementation
(`JsonFileCollectionLibrary`) replaced with `FolderCollectionLibrary` -
one file per collection, `Collections/<Id>.json`, each with its own
independent write-then-replace atomic save (same pattern as before, now
scoped per-file instead of to the whole library). A bad file is now
isolated to itself: reported via a new `LoadFailures` list the
constructor populates, every other collection still loads normally.
`JsonFileCollectionLibrary`'s private DTOs/conversion logic extracted
into `CollectionFileFormat.cs` (shared with `CollectionImporter`, no
behavior change - confirmed by every existing test passing unmodified
before the rest of this work started). `LegacyCollectionsMigrator.cs`
handles the one-time upgrade: reads the old combined file, writes each
collection out as its own file (deliberately *not* just moving the old
file into the new folder and relying on `FolderCollectionLibrary`'s own
multi-collection-per-file tolerance - see that tolerance's own comment
for why that specific shortcut would make affected collections silently
undeletable), then renames the old file to `collections.json.migrated`
(kept as a recoverable trace, not deleted - same discipline as
`CollectionImporter`'s `Imports/Processed/`). A single bad *collection*
inside an otherwise-good legacy file is skipped and reported, not fatal
to the rest; only a genuinely unparseable top-level file aborts
migration entirely, leaving the legacy file untouched for manual
recovery.

**Custom zone definitions**: identical treatment -
`JsonFileCustomZoneDefinitionLibrary` replaced with
`FolderCustomZoneDefinitionLibrary` (`Zones/<Id>.json`),
`CustomZoneDefinitionFileFormat.cs`,
`LegacyCustomZoneDefinitionsMigrator.cs`. The original class's own
comment already said "mirrors JsonFileCollectionLibrary exactly" - the
same bug existed here for the same reason, confirmed before doing the
work, not assumed.

**Verified against the user's actual real data before ever letting the
real game touch it** (copies in a scratch location, not the live
files): both migrations ran clean - 4 collections (349, 5, 10, and 10
entries respectively, matching exactly), 1 zone definition, zero
failures, and a referential-integrity cross-check confirmed the
migrated zone's `CollectionIds` still resolve correctly against the
migrated collections (migration preserves original Ids - unlike
`CollectionImporter`'s imports, which deliberately assign fresh ones,
migration isn't creating new content, so existing references must keep
working). Scratch verification code deleted after confirming, never
part of the real suite.

62 Core tests now (up from 33 at the start of this session) - new
coverage for both folder libraries' per-file isolation and both
migrators' fatal-vs-per-item failure handling, mirroring each other's
test shape intentionally (standardized architecture, standardized
tests). Full build (C# + UI) succeeds, deployed.

**Not yet tested in-game** - the actual migration has only run against
copies in a dry run, never the real files yet (deliberately - the
natural trigger is the next real launch, same "no special action
needed" philosophy as every other boot-time piece this session).
Worth confirming on next launch: `Logs/ZoneBuilder.Mod.log` shows
"Migrated 4 collection(s)..." and "Migrated 1 custom zone
definition(s)...", the real `ModsData/ZoneBuilder/Collections/` and
`Zones/` folders contain one file each, `collections.json`/
`customZones.json` are gone (renamed to `.migrated`), and every
existing zone/collection in the Collections/Zones tabs still shows up
and still works exactly as before - this is meant to be invisible to
actually using the mod, not a visible change.

### Boot-time collection import from a fixed folder (2026-10-12)
Follow-up to the Reddit question about importing a Find It Collections
export, and the manual test that preceded this (hand-merging a real
10-asset subset of the user's own "Suburbs - Fisher Pass" collection
into their live `collections.json`, confirmed working in-game). That
test proved the schema/parsing side works; the real problem it exposed
is that hand-editing a single shared JSON file isn't something to ask of
anyone - one misplaced comma and the whole library fails to load.

**Deliberately the simplest version of this feature, not the most
complete one** - decided in conversation, not something to second-guess
later without a real reason to revisit:
- No export feature. The user can already find and copy a real
  `collections.json` (or a hand-trimmed piece of one) themselves.
- No in-game import button or live trigger. Files are dropped into a
  fixed folder *before* launching, and picked up once at `Mod.OnLoad` -
  "no async trigger," the user's own words, confirming this isn't meant
  to be a live, in-game action.
- Find It Collections' own export format is explicitly out of scope
  until that mod actually ships and the format can be compared against
  ours - this only understands our own schema today.

**Implementation**: `ModsData/ZoneBuilder/Imports/`, scanned once at
`OnLoad` right after `CollectionLibrary` is constructed
(`CollectionImporter.ImportFromFolder`, new, `src/Core/
CollectionImporter.cs`). Each `.json` file uses the exact same schema as
`collections.json` itself (`CollectionFileFormat`, extracted out of
`JsonFileCollectionLibrary`, which used to define these DTOs privately -
no behavior change there, confirmed by the existing `
JsonFileCollectionLibraryTests` all still passing unmodified) - so a
dropped-in file can be either a single-collection snippet (like the
manual test) or someone's entire exported library, both just work
identically.

Decisions made explicitly, not defaulted silently:
- **Every imported collection gets a fresh `Guid.NewGuid()`, never the
  file's own stored Id** (user's call) - an import always coexists
  rather than risking a silent overwrite if a stored Id happens to
  collide with something already in the library.
- **A malformed file is reported as a `Failure` and left in place**, not
  moved - the rest of the folder still gets processed, and the user can
  fix the file and it's retried on the next launch (user's call,
  matching the per-file try/catch discipline already used for Phase 5's
  file-I/O exception safety).
- **A successfully imported file gets moved to `Imports/Processed/`**
  (my default, not something the user was asked to confirm but flagged
  explicitly before building) - otherwise every launch would re-import
  and duplicate the same collections again. A same-named file already in
  `Processed/` gets a timestamp-suffixed name rather than being
  overwritten.

`Core` stays game-independent per `ARCHITECTURE.md` - `CollectionImporter`
is pure BCL + Newtonsoft.Json, no logging dependency (failures are
returned, not logged, from Core; `Mod.cs` logs them via `Mod.log.Warn`
with the actual exception attached). 6 new Core tests
(`CollectionImporterTests.cs`) cover: missing folder, a single-snippet
import with fresh-Id confirmation, a multi-collection file, Processed/
move-and-no-reimport-on-second-run, a malformed file left in place and
reported, and a malformed file not blocking a good file in the same
folder. All 39 Core tests pass; full build (C# + UI) succeeds.

**Confirmed in-game (2026-10-12)**: dropped a renamed copy of the
Desktop test file into `ModsData/ZoneBuilder/Imports/` and launched.
`Logs/ZoneBuilder.Mod.log` (routine INFO-level output goes here, not
`Player.log` - only warnings/exceptions get mirrored into the latter;
worth remembering next time a log line seems to be "missing") shows the
exact expected sequence: `Imported 1 collection(s) from ...\Imports`,
then `Collection library: ... (4 collection(s))` - the new import plus
the 3 already there. File correctly moved to `Imports/Processed/`.
In-game, "Imported Test - Fisher Pass" shows up as a second, independent
collection alongside the original manually-merged one - fresh-GUID
coexistence confirmed working exactly as designed, no collision.

### Cloned buildings show up fully in Find It, self-labeled, and can't be hidden from it (2026-10-12)
Prompted by a Reddit question about a separate "Find It Collections" addon
mod (importable/exportable building sets) and whether our per-zone
building clones would look like duplicates there.

**Confirmed in-game**: Find It shows a clone as a complete, independent,
fully-browsable entry - no dedup/grouping that hides or merges it with
the original. Display name is clean, not an exposed internal name:
`CustomZoneBuilder.EnsureBuildingCloned` already calls
`RegisterDisplayName(clonedBuildingEntity, $"{displayZoneName} -
{templateBuilding.name}")` for every cloned building, not just the zone
prefab (confirmed from the code, not just observed) - so Find It shows
e.g. "FindItTest - PittHouse15", which self-explains which zone a clone
belongs to. Net effect is actually good for players: no confusion about
which entry is the original vs. the zone-specific clone.

**Can a clone be hidden from Find It? No, and not because of a missing
API - structurally not possible without breaking the clone's actual
function.** `Game.Prefabs.UIObject.m_IsDebugObject` is a real vanilla
flag (confirmed via `D:\CS2-Decompiled\Game.Prefabs\UIObject.cs`) that
suppresses `UIObjectData`/UI-group registration in a release build -
looked promising at first. But Find It's own `PrefabIndexingSystem.cs`
(fetched from its GitHub source, not guessed) builds its building
catalog via `WithAll<BuildingData, SpawnableBuildingData,
PrefabData>().WithNone<SignatureBuildingData>()` - it never checks
`m_IsDebugObject` at all. Its inclusion criteria are exactly the
components a clone needs to function as a real growable building; the
only exclusion it applies (`SignatureBuildingData`) would misclassify
the clone as a landmark building if used to hide it, breaking its actual
spawn behavior. There's no separate "visible to Find It" vs. "usable by
our zone" axis to toggle - hiding one means breaking the other. Would
need a change on Find It's own side, not ours. Given the display-name
self-labeling above already avoids real confusion, not pursued further.

### Phase 7.3 reverted - assetGridTheme.item is a grid tile, not a row (2026-10-12)
Tested in-game: list rows across the Collections/Assets/Zones tabs
rendered as small smushed squares instead of full-width rows -
"everything looks smushed." Root cause, confirmed by grepping the
installed game's actual CSS for `.item_a5m` (the base class inside
`assetGridTheme.item`): `width/height: var(--assetMenuItemSize);
display:flex; justify-content:center; align-items:center` - a
**fixed-size, centered-content grid tile**, never meant for a full-width
row at all. Should have been caught before implementation, not after:
checking Road Builder's own `ManageRoadsView` (the UI_OVERHAUL_PLAN.md
reference for this exact kind of panel) would have shown its building-
list is a grid of square thumbnails, not rows like this mod's - there
was never a real vanilla analogue for a row-shaped list here, Road
Builder solves a visually different layout problem. `assetGridTheme`
was verified to genuinely *exist* (Phase 7.1's own discipline), but
"does this class exist" and "is this class meant for what I'm about to
use it for" are different questions, and only the first one got asked
before writing code.

Reverted cleanly: deleted `UI/src/vanilla/VanillaComponentResolver.tsx`
(no other consumer existed) and `UI/src/shared/rowClassName.ts`, un-did
the 5 call sites back to plain `rowStyles.row`, removed the now-stale
`assetGridTheme` references from `THIRD_PARTY_NOTICES.md`/
`TOUCHPOINTS.md`. `ListRow.module.scss`'s existing hand-rolled hover
styling (confirmed good in Phase 6) is untouched and still in place.
Phase 7.4 (the `TextInputTheme`/raw-`<input>` retrofit) is unaffected -
confirmed no visual issue reported for it - so that part stays.

A legitimate future use for `assetGridTheme.item` still exists (an
actual grid-of-tiles UI, if one ever gets built - e.g. Find It's docking
pattern mentioned in `UI_STYLE_GUIDE.md` section 5, still describe-only
and out of scope today) - just not for anything this mod currently
renders as a list.

**Confirmed in-game (2026-10-12)**: rows back to normal full-width
after the revert, no regression. The `TextInputTheme`/raw-`<input>`
retrofit (Phase 7.4) also confirmed fine - no visual issue reported for
it either. User is weighing a bigger follow-up: a real grid+detail-panel
redesign of the Assets tab specifically (where building thumbnails make
`assetGridTheme.item` a genuine fit, unlike the row-shaped lists here) -
not started, no immediate plan, current Assets tab is "functional
as-is."

### Phase 7: vanilla module reuse built, not yet confirmed in-game (2026-10-12)
Revisits the item Phase 6 of `UI_OVERHAUL_PLAN.md` explicitly deferred
("getModule()-based vanilla theme reuse... worth revisiting only if the
above still looks visibly 'off'") - triggered now by `UI_STYLE_GUIDE.md`
(a cloud session's research) plus a Reddit question. See that plan's
new "Phase 7" section for the full breakdown of what was built
(`THIRD_PARTY_NOTICES.md`, `UI/src/vanilla/VanillaComponentResolver.tsx`
trimmed to one entry, `TextInputTheme.ts`, the `ListRow.module.scss`/raw-
`<input>` retrofits) - this entry covers the two things worth a
dedicated record.

**Every vanilla module path/class claim was independently verified
against the actually-installed game before use, not trusted from the
style guide on faith** - matching Phase 6's own precedent of grepping
`Content/Game/UI/index.css` for its CSS tokens. Here: grepped the
installed `Content/Game/UI/index.js` directly for the literal module
path strings `game-ui/game/components/item-grid/item-grid.module.scss`
and `game-ui/editor/widgets/item/editor-item.module.scss`, and for each
one's `classes` object literal, confirming `classes.item` (item-grid)
and `classes.input` (editor-item) are real exports with the exact
hashed class names claimed. Additionally fetched Road Builder's actual
`TextInput.tsx` source from GitHub to confirm `editor-item.module.scss`
really is where its `TextInputTheme.input` comes from (a one-off
`getModule` call in Road Builder's own code, not routed through its
shared resolver, which doesn't register that module at all) - the style
guide's attribution turned out to be correct, but confirmed rather than
assumed.

### px vs rem in base.scss (2026-10-12)
`UI/src/shared/base.scss` already existed before `UI_STYLE_GUIDE.md`
was written (its own px-based `$space-*` spacing scale, deliberately
color-free - colors come from the game's own CSS vars instead). The
style guide's section 3 argues for `rem` everywhere, since Coherent/
Gameface scales `rem` with resolution and doesn't scale `px`. Decided
**not** to migrate the existing scale: it's already visually confirmed
in-game across every tab (Phase 6), and converting it wholesale risks
regressing confirmed-working spacing for a scaling concern that's never
actually been observed as a bug at any tested resolution - only a
theoretical one from the style guide's own general advice, not something
this project hit.

Instead, the one Road Builder-derived mixin Phase 7.4 actually needed
(`textInputBasic`) was added in its own clearly-labeled, rem-based
section of `base.scss` (matching Road Builder's own source values,
unconverted), coexisting with the px scale rather than unifying them.
Two unit systems in one file, in clearly separated sections, is an
accepted tradeoff here, not a defect to paper over. A full px→rem
migration stays a possible future phase, only if real multi-resolution
in-game testing ever surfaces an actual scaling bug - not built
speculatively ahead of that.

### Level 1 warning moved to the collection, not the zone attach (2026-10-12)
Follow-up to "Warn before attaching a collection with no Level 1
building" (below) - tested in-game and the warning never showed up. Root
cause wasn't a logic bug: the check only ever lived in
`ZoneUISystem.OnAddCollectionToZone`, which fires once, at the moment an
*existing* collection gets attached to a zone. The actual test (and, on
reflection, the natural way most people build a collection) was build a
collection standalone, populate it fully, save it - never attach it to
anything in the same session. Nothing wrong happened; there was just
nothing to check yet.

That surfaced the real design mismatch: the user's own framing was "the
point in the user journey where users are interacting with the assets,
issues surrounding their choices should be flagged ASAP" - attach time
is not that moment, asset-editing time is. Moved the whole feature:

- `CustomZoneBuilder.HasLevel1Candidate(ZoneType, IEnumerable<Collection>)`
  (needed a zone's base type to check against) replaced with
  `FindZoneTypesMissingLevel1(Collection)` - self-contained, no zone
  dependency. Finds every zone type the collection's *own* entries
  resolve to that has zero Level 1 candidates among them, so a mixed-
  zone-type collection gets checked per zone type, not as a single
  pass/fail.
- `ZoneUISystem.OnAddCollectionToZone` reverted to a plain, immediate
  attach - no check, no defer, no `Level1Warning` binding/triggers. A
  zone attach was never actually the right place for this once the check
  stopped needing a zone's base type to run.
- `CollectionUISystem` gained the check instead, fired once by a new
  `FinishEditingCollectionAssets` trigger from the Assets tab's "Done"
  button (`ShellFeature.finishAddingAssets`) - the actual moment the user
  finishes choosing a collection's assets, independent of whether/when
  it's ever attached to a zone. Purely informational now, not a confirm/
  cancel gate: add/remove already commits immediately per-row (decision
  #3 in `UI_OVERHAUL_PLAN.md`), so there's nothing left to cancel by the
  time this fires - one dismiss button, no "attach anyway."
- `Level1WarningEntry` replaced with `CollectionLevel1WarningEntry`
  (collection id/name + the list of missing zone types, no zone fields at
  all). Banner moved from `ZonesTab.tsx` to `CollectionsTab.tsx`'s
  `CollectionDetail`, filtered to the collection it's actually about so
  a still-pending warning doesn't bleed into a different collection's
  detail view.
- Incidentally fixes the one known rough edge the old version had (an
  empty collection always warned, even via "Create Collection and Add to
  Zone") - an empty collection has no zone types represented at all now,
  so there's nothing to flag.

Confirmed: full build (C# + UI) succeeds, all 33 Core tests pass.
**Not yet tested in-game** - the real test is adding only non-Level-1
assets to a collection, clicking "Done," and confirming the banner
appears naming the right missing zone type(s); and confirming it does
NOT appear when attaching/detaching that same collection from a zone
afterward (the old trigger point, now removed).

### Warn before attaching a collection with no Level 1 building (2026-10-12, superseded above)
Follow-up to "A collection with no Level 1 building can never spawn
anything" - that was found and diagnosed after the fact (a confusing
hour of "nothing spawns despite demand"), not caught up front. Rather
than build the "Historical" auto-apply feature a Reddit user requested
(explicitly rejected as a perpetual reactive system with real downsides
- see the Historical research above), the actual want behind
"historical/luxury neighborhood" requests turned out to connect
directly to this older, better-understood gap: a zone with no Level 1
candidate for its base type can never spawn anything from scratch
(`ZoneSpawnSystem` only ever considers Level 1 for a fresh spawn), which
is exactly what a naive "Level 5 luxury-only" collection would hit.

Added `CustomZoneBuilder.HasLevel1Candidate` (pure lookup, no mutation)
and wired it into `ZoneUISystem.OnAddCollectionToZone`: before
attaching, checks the *full* prospective set (every collection the zone
already has, plus the new one) for a Level 1 match - not just the new
collection in isolation, since the zone might already have a Level 1
candidate from an existing collection. If none exists, defers instead of
attaching immediately: stores the pending zone/collection pair, surfaces
a `Level1Warning` binding, and only commits
(`CommitAddCollectionToZone`) if the user clicks through via a new
`ConfirmAddCollectionDespiteWarning` trigger; `CancelPendingCollectionWarning`
discards it instead. Logged via `Mod.log.Warn` either way, consistent
with every other content-mismatch warning already in this codebase.

Deliberately a one-shot check triggered by a user action, not a
perpetual per-frame system - the user's own stated objection to the
Historical idea ("reactive feels flimsy and easy to break") doesn't
apply here. UI rendered as an inline banner (same pattern as the
existing "confirm delete" row and the build-progress banner), not a new
modal/overlay primitive - this codebase has a real prior incident
(`cs2/ui` has no native `<select>`, and a plain-HTML fallback once took
down the entire game HUD, not just this mod's panel) that argues for
staying inside already-proven UI patterns.

One known rough edge, not fixed: "Create Collection and Add to Zone"
(create a brand-new, empty collection and immediately attach it) goes
through the same check - a zone with nothing attached yet will trigger
this warning for an *empty* collection, which is technically correct
(nothing in an empty collection has Level 1 anything) but potentially
redundant-feeling in that specific flow. Left as-is rather than special-
cased, since it's accurate information either way and trivially
dismissed with one click; worth revisiting only if it turns out to
actually annoy people in practice.

Confirmed: full build (C# + UI) succeeds, all 33 Core tests pass.
**Not yet tested in-game** - the real test is attaching a Level-5-only
collection to a zone and confirming the warning banner appears, "Cancel"
truly does nothing, and "Attach Anyway" completes the attach exactly as
before this feature existed.

### Platter nudge on delete reverted - it crashes Platter's own zone cache (2026-10-12)
Tried adding the same `PlatterCompatibility.NudgeZoneGroupForPlatter`
call to `CustomZoneBuilder.RetireZone` (right after `RemovePrefab`, same
tick as the zone's own `Deleted` tag) to fix deleted zones lingering in
Platter's list - confirmed in-game that it *did* fix the lingering-list
symptom, but it also threw a nonfatal `ArgumentOutOfRangeException` in
Platter's own `P_ZoneCacheSystem.CacheZonePrefabs` every time.

Root cause, confirmed by reading both sides' decompiled source (not
guessed): `PrefabSystem.RemovePrefab` (`D:\CS2-Decompiled\Game.Prefabs\
PrefabSystem.cs`) is fully synchronous - it correctly re-indexes
whichever prefab got swapped into the removed slot, but the *removed*
entity itself isn't destroyed yet (still tagged `Deleted`, cleaned up
later), and its own `PrefabData.m_Index` is set to a sentinel,
`-1000000000`. Platter's `P_ZoneCacheSystem`'s zone queries
(`m_ZonesAllQuery`/`m_ZonesModifiedQuery`) don't exclude `Deleted`
entities - normally harmless, since by the time Platter's own cache
naturally refreshes the entity would already be fully destroyed and
just wouldn't match. Our nudge defeats that: it forces Platter to
rescan in the exact same tick as the removal, the one window where the
entity still exists with that invalid sentinel index, and Platter's
`PrefabSystem.GetPrefab<T>` call on it indexes `m_Prefabs[-1000000000]`
and throws.

This is a real, reproducible gap in Platter's own query (missing
`WithNone<Deleted>()`), not something fixable from our side without
reaching into Platter's code - and reaching in is exactly the kind of
workaround surface this project avoids for a dependency it doesn't own.
Reverted the delete-side nudge call; the create-side nudge (which has
no equivalent issue - a freshly-added prefab was never tagged `Deleted`)
stays as-is. Documented as a known gap: **deleted zones still linger in
Platter's own list until Platter's cache next refreshes on its own
(e.g. next load)** - acceptable, matches the user's original comfort
level ("I may be ok with that - we can be too crazy working around
platter code"). Worth flagging to Platter's author as a courtesy, same
as the original create-side report, since the underlying missing-
`Deleted`-exclusion bug is real regardless of us.

### The retire loop needed chunking too - a 349-building detach crashed the game natively (2026-10-12)
Follow-up to chunking the clone loop: detaching a 349-building collection
from a zone (not deleting the zone itself) crashed the game shortly
after. The user's own framing ("issue when i removed the collection...
but deleting the zone crashed") matched the log exactly: our own log
showed a clean "0 newly cloned, 0 total offered, 349 retired" from the
detach, then zone deletion proceeded and logged cleanly too (including
clearing the zone tool's own selection, already-fixed) - the crash
happened *after* that, with no exception in our own log at all. Deleting
the zone wasn't the cause; it's just where the already-accumulating
instability finally gave out.

`Player.log` told the real story: immediately after an `Unloading 349
unused Assets` line (same count as the retired buildings), it started
flooding with `Internal: JobTempAlloc has allocations that are more
than the maximum lifespan of 4 frames old - this is not allowed and
likely a leak`, dozens of times, ending in a genuine native crash
("Native Crash Reporting... fatal error in the mono runtime") with no
managed stack trace - the process died too abruptly to even finish
writing one.

Root cause: `RetireBuildingsNoLongerResolved` was still fully
synchronous even after `BuildOrRebuildIncremental` chunked the *clone*
loop - 349 `EntityManager.SetSharedComponent` calls in one uninterrupted
tick. A `SharedComponentData` change is a structural change (it can move
an entity to a different ECS chunk if the new value doesn't match its
neighbors) - doing 349 of those back-to-back, with no job-completion
sync point between them, is apparently enough to leak a `TempJob`
allocation the same underlying way 349 *clones* in one tick used to
freeze the game outright before that loop was chunked.

Fixed the same way: split `RetireBuildingsNoLongerResolved` into a pure
lookup (`FindBuildingsNoLongerResolved`, no mutation) and moved the
actual `SetSharedComponent` loop into `BuildOrRebuildIncremental`
itself, chunked by the same `entriesPerTick`, yielding `ZoneBuildProgress`
the same way the clone loop does. Confirmed: build succeeds, all 33
Core tests pass. **Not yet re-tested in-game** - the real test is
detaching a large collection again and confirming no `JobTempAlloc`
leak warnings appear in `Player.log` and the game stays stable
afterward, not just that it doesn't freeze during the detach itself.

### A 349-entry collection froze the game hard enough to need force-killing (2026-10-11)
User report: attaching a collection named "Suburbs - Fisher Pass" to a
newly created zone, on a playset with "a ton of mods and assets," froze
the game - not a crash, confirmed via `Player.log`: no exception, no
out-of-memory, just `TerminateGame is already in progress, please
wait.` repeated 12 times (the signature of a hung app being force-closed
after someone gave up waiting). Our own log showed the operation had
actually *finished* successfully ("349 newly cloned, 349 total
offered") ~90 seconds before `OnDispose` - the freeze happened during or
immediately after that work, not from a thrown exception.

Root cause, found by reading `GameBuildingCatalog.TryGetBuildingPrefab`:
it did a full linear scan of *every* registered building prefab in the
game (vanilla + every mod's content - easily tens of thousands on a
heavily-modded playset) per name lookup, with no cache at all. `CustomZoneBuilder.EnsureBuildingCloned`
calls it **twice** per collection entry (once to check "already cloned
in an earlier rebuild?", once to find the template to clone from) - so
attaching an N-entry collection costs roughly `2N * (total buildings in
the game)` comparisons, all synchronous, all in one frame. Invisible all
session with the handful-of-entries collections used in every other
test (4-16 entries) - catastrophic at 349 entries against a large
modded building population.

Fixed with a lazily-built, explicitly-invalidated name index
(`GameBuildingCatalog.m_NameIndex`) backing `TryGetBuildingPrefab`:
`CustomZoneBuilder.BuildOrRebuild` calls the new `InvalidateNameIndex()`
once at the top of every call (so a later rebuild still sees whatever an
earlier, separate rebuild cloned), and every lookup *within* that one
call reuses the same cached dictionary instead of re-scanning. Turns the
cost from O(entries × total buildings) into O(total buildings + entries)
- one full scan per rebuild instead of one per entry. Safe because a
building cloned partway through a rebuild is never looked up again
until some later, separate rebuild (`resolvedPrefabIds` already dedupes
within one call by the external template id). Confirmed: full rebuild
still succeeds, all 33 Core tests still pass. **Not yet re-tested
in-game against the actual 349-entry collection that triggered this** -
the fix is algorithmically sound and the existing automated coverage
still passes, but the real test is attaching that same collection again
and confirming it no longer hangs.

**Update: re-tested, still froze (~90s) - but the name-index fix was
real and did help.** Confirmed no longer needing a force-kill (recovered
on its own that time), and the log revealed the real remaining shape of
the problem: retiring that zone and re-attaching the *exact same* 349
buildings to a brand new zone moments later took **12 seconds**, not
90. Since the fixed lookup cache runs identically both times, an
algorithmic cost in our own code would cost the same either time - it
didn't, so the lookup fix had already done its job. The remaining ~90s
is a one-time-per-session cost tied to first use of each uniquely-
referenced building prefab (most likely the engine loading/decompressing
their mesh/texture/icon assets from disk the first time anything clones
from them, cached thereafter) - not something this mod causes or can
eliminate, confirmed indirectly by `Player.log` logging `Unloading 349
unused Assets` in the same window. See "Chunked, incremental zone
building" below for the actual mitigation - spreading the unavoidable
cost across frames instead of trying to eliminate it.

**Separately noticed, fixed (2026-10-11)**: `RetireZone` only removed
the *zone* prefab, not the buildings it had cloned - retiring a zone
orphaned its building clones rather than cleaning them up, so repeated
create/retire cycles would quietly accumulate dead prefabs over a long
session. Not the cause of the freeze above; fixed anyway since we were
already in this code. `RetireZone` now also calls `PrefabSystem.RemovePrefab`
on every building found under that zone's internal name prefix
(`GameBuildingCatalog.FindBuildingsByNamePrefix`) - the same real removal
API already confirmed correct for zones, not `RetireBuildingsNoLongerResolved`'s
softer "clear BuildingSpawnGroupData" approach, since that one exists for
a zone that's still alive and might re-resolve the building later, which
isn't possible once the zone itself is gone.

**Update (2026-10-12): the residual risk was real - confirmed in-game,
reverted.** Loading a save from a session where a zone (with grown
buildings) had been retired produced dozens of `[SceneFlow] [WARN]
Unknown prefab ID: BuildingPrefab:ZoneBuilder.Zone.<guid>.<name>` lines
plus `Unknown prefab ID: ZonePrefab:ZoneBuilder.Zone.<guid>` in
`Player.log`, every affected building rendering as a white box, and the
game taking long enough to load/resolve all of them that it read as a
full freeze. Root cause confirmed exactly as suspected: a building
instance already placed/grown in an earlier session has its prefab
reference baked into the save file by name - removing that prefab
(this fix) left nothing for the save to resolve it against. The zone
case's safety guarantee (a separate, zone-specific cell index,
independent of `PrefabSystem`'s own bookkeeping) has no equivalent for
buildings - an already-placed building genuinely depends on its prefab
staying registered, full stop. Reverted: `RetireZone` no longer touches
a zone's buildings at all, back to the original, already-safe behavior.
Orphaned-prefab accumulation (the thing this fix was trying to solve)
is a real but minor cost next to corrupting a save - not worth
revisiting without a way to first confirm no live instances exist
(not currently feasible without deeper investigation into how to query
that).

### Chunked, incremental zone building, with a "building zone..." UI indicator (2026-10-11)
Follow-up to the freeze above: once the remaining ~90s was understood to
be unavoidable, one-time, per-building-prefab engine cost rather than
anything fixable in our own algorithm, the right mitigation is spreading
it across many frames instead of doing all 349 (or however many) clones
synchronously in one. `CustomZoneBuilder.BuildOrRebuild` is now
`BuildOrRebuildIncremental`, a C# iterator yielding a `ZoneBuildProgress`
every `entriesPerTick` collection entries (5, by default -
`CustomZoneBuildRequestSystem.EntriesPerTick`) instead of only after
every entry is done. `CustomZoneBuildRequestSystem.OnUpdate` advances it
exactly one step (one `MoveNext()`) per tick, bounding how much of any
single frame a build can cost to roughly `EntriesPerTick * (per-entry
cost)` rather than `(all entries) * (per-entry cost)`.

Moved the Platter nudge call (`PlatterCompatibility.NudgeZoneGroupForPlatter`)
from `CustomZoneBuildRequestSystem` into `CustomZoneBuilder.BuildOrRebuildIncremental`
itself, right after the zone resolves, since it has to happen in the
exact same tick as the zone's own `Created` tag - true on the very first
`MoveNext()` of a new build regardless of how the rest of the work gets
chunked, but would have needed new coordination to guarantee from the
calling system otherwise.

Exposed via `CustomZoneBuildRequestSystem.CurrentProgress` (a nullable
`ZoneBuildProgress` - null means no build in flight), polled every UI
tick by a new getter-based binding (`ZoneUISystem`'s `"BuildProgress"`,
backed by `ZoneBuildProgressEntry` - same hand-written `IJsonWritable`
reasoning `ZoneUIEntry` already established, since `ExtendedUISystemBase`'s
generic reflection writer doesn't preserve camelCase keys). Getter-based
rather than the usual "set .Value on mutation" pattern every other
binding here uses, because progress changes continuously while a build
is running, not at one discrete mutation point. `ZonesTab.tsx` renders a
small spinner + "Building '<name>'... X/Y" banner above whichever
sub-view (list or detail) is currently showing, since a build can be
triggered from either (creating a zone, or attaching a collection from
the detail view).

Confirmed: full build (C# + UI) succeeds, all 33 Core tests still pass.
**Not yet re-tested in-game** - the real test is attaching "Suburbs -
Fisher Pass" (or an equally large collection) again and confirming the
game stays responsive throughout, with the banner visible, instead of
freezing.

### Renamed the mod from CS2StyleMod to Zone Builder (2026-10-11)
User settled on a name. Code-level scope only this pass (per explicit
instruction) - the C# namespace, mod id, deployed folder name, UI
bindings, and every internal reference, but deliberately not the local
project directory (`C:\Users\Jacob H\CS2StyleMod`) or the GitHub repo
name, both lower-risk to leave alone for now.

`ZoneBuilder` (no space) is the code-level identifier everywhere it has
to be one: namespace, mod id (`UI/mod.json`), both `.csproj` files and
the tests project folder, `CustomZoneBuilder.NamePrefix` (cloned prefabs
are now named `"ZoneBuilder.Zone.<guid>"`), and every doc reference to
an actual file/namespace path. `"Zone Builder"` (with the space) is used
only in the two places actually shown to a person: the in-game Options
menu title (`Setting.cs`'s `LocaleEN`) and the Paradox Mods listing name
(`PublishConfiguration.xml`'s `DisplayName`) - plus this README's own
title, for the same reason.

Migrated `ModsData/CS2StyleMod/` (the persisted `collections.json`/
`customZones.json`) to `ModsData/ZoneBuilder/` by copying rather than
moving, so the old folder survives untouched as a rollback point if
anything about the new path turns out wrong. Deleted the stale deployed
`Mods/CS2StyleMod/` folder so the game wouldn't see two copies of the
same mod after the rebuild.

One accepted, one-time side effect: since `NamePrefix` changed, every
zone/building prefab this mod had already cloned under the old
`"CS2StyleMod.Zone."`/`"CS2StyleMod.Building."`-style internal names
won't be found by name-matching lookups (`GameZonePrefabResolver.TryFindByName`)
under the new prefix - they'll each get freshly re-cloned under the new
name on next rebuild, leaving the old-named entities orphaned for that
session rather than reused. Acceptable for a pre-release, not-yet-
published mod being tested locally; would need a real migration path if
this happened after release.

Deliberately NOT renamed: `Properties/PublishConfiguration.xml`'s
`ExternalLink` still points at `github.com/jacobhill93/CS2StyleMod` -
that's the real, currently-existing repo; the blanket rename script
touched it too and it was manually reverted, since actually renaming the
GitHub repo (or the local directory) is a separate decision the user
hasn't made yet.

### The real bug: zones were never rebuilt on a fresh game load (2026-10-11)
What looked like three separate reports (a zone disappearing, a deleted
zone not disappearing, persistence confusion) turned out to trace back
to one real, more fundamental gap, found by reading the mod's own log
directly rather than continuing to theorize from user reports alone:
after `OnLoad` found an existing zone definition on disk (confirmed
correct, persisted data - `TestZoneEU` with the right name/region/id),
**zero `CustomZoneBuilder` activity appeared anywhere in that session's
log.** Nothing ever rebuilds a zone's actual in-game `ZonePrefab` clone
when the game loads fresh.

Root cause: a `CustomZoneDefinition` (our own JSON data) persists fine
across a restart - confirmed, the file had exactly the right data - but
its actual in-game prefab clone does not. That's live ECS state tied to
the current game session, never serialized into the save file at all.
`CustomZoneBuildRequestSystem.Request(...)` was only ever called from
`ZoneUISystem`'s four mutation triggers (create/rename/add-collection/
remove-collection) - nothing on load ever called it for definitions that
already existed. So a zone built in an earlier session simply had no
prefab in a new one: not deleted, not hidden, just never rebuilt - and
every "zones work" confirmation earlier in this session had happened
within one continuous play session, never actually exercising a full
quit+relaunch cycle until now.

Fixed: `ZoneUISystem.OnGameLoaded` now iterates every known definition
and requests a rebuild for each, the same call a create/rename/attach
trigger already makes. Safe to do unconditionally for every zone on
every load - `CustomZoneBuilder.BuildOrRebuild` is explicitly idempotent/
additive by design (same safety property a rebuild-from-a-UI-edit
already relied on).

This also means the separate delete/retire attempts below were never
actually wrong on their own terms - `TestZoneEU` was never deleted, so
neither fix was even being exercised by the report that triggered this
investigation. Whether `RetireZone`'s `UIGroupElement` buffer-removal
attempt actually works is **still unconfirmed** - this fix addresses a
different, independently-real bug found along the way, not a
replacement for testing that one.

Not yet confirmed in-game - needs a real restart test: create a zone,
fully quit, relaunch, check it's paintable without touching anything
else first.

### Deleting a zone left its prefab fully paintable in-game (2026-10-12)
User tested and confirmed: deleting a zone removed it from our own Zones
tab, but the underlying vanilla zone prefab stayed fully registered and
paintable in the game's own zone tool - you could keep zoning more of a
"deleted" zone. Root cause: `ZoneUISystem.OnDeleteZone` only ever
touched `Mod.CustomZoneDefinitionLibrary` (our own bookkeeping) - nothing
anywhere ever reached back into the actual registered `ZonePrefab`.

Fixed with the same non-destructive "retire, don't remove" philosophy
`RetireBuildingsNoLongerResolved` already uses for buildings: cells
already painted with the zone keep working (removing the prefab outright
via `PrefabSystem.RemovePrefab` risked orphaning them, so not used),
it just stops being *offered* going forward. `CustomZoneBuilder.RetireZone`
sets the cloned `ZonePrefab`'s own `active` field to `false` and calls
`PrefabSystem.AddOrUpdatePrefab` - both confirmed real via the same live
reflection against `Game.dll` this project always uses before trusting
an API, not guessed.

**Timing discipline, not skipped**: this project already paid for one
real bug from calling a `PrefabSystem`-touching operation from the wrong
place (`CustomZoneBuildRequestSystem`'s whole reason for existing - see
"A runtime-cloned prefab is only visible to init systems for one
frame"). Whether `AddOrUpdatePrefab` has its own timing requirement
(same as `AddPrefab`, or none at all) isn't verified either way - rather
than assume "probably fine, it's a different method," retirement is
routed through `CustomZoneBuildRequestSystem`'s existing request queue
(`RequestRetirement`, processed in the same `OnUpdate` alongside builds,
already registered `UpdateBefore<_, PrefabSystem>`) so it gets the exact
same safe timing guarantee as a build, at zero extra cost or risk.

Not yet confirmed in-game (implemented in response to the report, not
yet retested) - confirm a deleted zone actually disappears from the
zone-painting tool's palette, and that cells already painted with it
before deletion are unaffected.

### Retire, take two: `active=false` alone does not hide a zone from the picker (2026-10-11)
In-game testing disproved the attempt above: `active = false` +
`AddOrUpdatePrefab` left the deleted zone fully visible and paintable,
just with its icon replaced by the placeholder/"no picture" fallback -
something was reading the new `active` state (the icon), but whatever
actually decides which tiles the picker lists was not. Next attempt:
also remove the zone's entry from its UI group's (`ZonesResidential`)
`UIGroupElement` buffer directly. Also still visible in-game - but this
round added diagnostic logging to every decision point first, which
confirmed via the mod's own log that every step genuinely executed
(zone found, `active=false` set, buffer entry actually removed,
25→24 entries) with zero early returns. Also tried
`PrefabSystem.UpdateAvailable()` afterward (a real method, confirmed via
live reflection) on the theory that something was caching availability -
also confirmed via log to run cleanly, also no visible effect. Closing
and reopening the zone tool panel in-game (free, no code change) was
tested too: the icon reverted from the placeholder back to normal, and
the zone was still there - ruling out simple "stale UI, refreshes on
reopen" as the explanation.

Stopped guessing mechanisms from reflection-derived method names at this
point and read actual decompiled game source
(`D:\CS2-Decompiled\Game.Prefabs\UIGroupPrefab.cs`, `Locked.cs`,
`UnlockRequirement.cs`, `UnlockSystem.cs`) instead. Found
`UnlockSystem.IsLocked(prefab) => HasEnabledComponent<Locked>(prefab)` -
looked like the real "is this available" check - and tried
`EntityManager.SetComponentEnabled<Locked>(zoneEntity, true)` (the
inverse of `UnlockSystem.UnlockPrefab`'s own `SetComponentEnabled<Locked>(_, false)`).
Confirmed via log to execute with no warnings (the entity did carry a
`Locked` component). Still visible in-game, even after navigating away
to another mod and back - five mechanisms now confirmed-executed, zero
visible effect, which meant the picker's actual data source still
hadn't been found; mutating runtime ECS state by inference from names
had run out of road.

### Retire, take three: the real mechanism, found by reading the actual removal/UI-binding pipeline (2026-10-11)
Searched the decompile tree for every file referencing the
`Locked`/`UIGroupElement`/`UnlockRequirement` trio instead of guessing
another one, and found `Game.Prefabs.UIInitializeSystem` -
despite the name, its `OnUpdate` runs every frame (it's a
`GameSystemBase`, not a one-shot init), querying for any entity with
`PrefabData` + `Game.Common.Deleted`, and for each match removing it
from its group's `UIGroupElement` **and** `UnlockRequirement` buffers
automatically (`RemoveFrom`). None of the five prior attempts had ever
added `Deleted` - that tag is the actual thing this cleanup pipeline
watches for, not `active`, not `Locked`, not a buffer we edit by hand.

`Game.Prefabs.PrefabSystem.RemovePrefab(PrefabBase)` is what the game
itself calls "removing a prefab": it does
`EntityManager.AddComponent<Deleted>(entity)` and deregisters the prefab
from `PrefabSystem`'s own `m_Prefabs`/`m_Entities`/`m_PrefabIndices`.
This project had previously avoided calling it here on an *unverified*
suspicion that full removal would orphan cells already painted with
that zone type (see the "non-destructive" reasoning two sections up).
Checked that directly this time, in
`Game.Prefabs.ZoneSystem.InitializeZonePrefabs`: a painted cell's
`Cell.m_Zone.m_Index` is a separate, zone-specific index tracked by
`ZoneSystem` itself (`GetNextIndex`/`m_ZonePrefabs`), completely
unrelated to `PrefabSystem`'s own general prefab index - so
`RemovePrefab`'s internal reindexing of `m_Prefabs`/`m_Entities` cannot
touch it. `ZoneSystem` has its own explicit `Deleted`-tag handling
branch that just clears that zone's slot in `m_ZonePrefabs` to
`Entity.Null` and frees the index for reuse - exactly the wanted
behavior, and clearly a scenario the base game's own authors designed
for already, not an edge case being stumbled into.

Replaced all five prior mutations in `CustomZoneBuilder.RetireZone` with
the single call `m_PrefabSystem.RemovePrefab(zone)`.

**First in-game test of this found a second, real bug**: deleting a
zone while the zone tool still had it selected threw a `[CRITICAL]`
`KeyNotFoundException` from `Game.Rendering.NetColorSystem.OnUpdate` -
confirmed via its own decompiled source: every frame, while the zone
tool is the active tool, it unconditionally calls
`m_PrefabSystem.GetEntity(m_ZoneToolSystem.prefab)` with no
`Deleted`-awareness at all, and a stale selection pointing at a
just-removed prefab throws instead of returning `Entity.Null`. Fixed by
having `RetireZone` check `m_ZoneToolSystem.prefab == zone` and clear it
(set to `null`) before calling `RemovePrefab` - mirrors a UI discarding
a reference to something it's about to delete. Confirmed in-game:
clearing the selection force-closes the zone tool panel (acceptable UX,
per user), no error, and the zone is gone from the picker on reopen -
the bug is fixed.

### New feature: NA/EU region toggle on zone creation (2026-10-12)
User reported custom zones always default to the EU theme and asked for
an NA/EU toggle. Root cause, confirmed by reading
`GameZonePrefabResolver.TryFindVanillaZone`'s existing comment: it
always picked the *first* vanilla zone entity its query happened to
return for a given `Core.ZoneType` category - no region logic existed
at all, "any one will do," and that first match apparently always
landed on an EU-themed candidate.

**Researched the real mechanism before building anything** (live
reflection against the installed `Game.dll`, not guessed): `ZonePrefab`
itself carries no theme/region field. The real mechanism is a separate
component, `Game.Prefabs.ThemeObject` (confirmed real, a
`ComponentBase`-style prefab-authoring component - same category as
`UIObject`, accessed via `PrefabBase.TryGet<T>()`, not an ECS
`IComponentData` on the entity, since its one field,
`ThemePrefab m_Theme`, is a managed object reference ECS components
can't contain). `ThemePrefab` has an `assetPrefix` string field -
strongly inferred (not yet directly confirmed with live data) to be
`"NA"`/`"EU"`, matching the building-naming convention already
empirically confirmed this project (`NA_MixedLeft01` etc. - see
"Interaction between themes and the EU/NA theme setting and asset
packs").

Implemented: `Core.ZoneRegion` (`NorthAmerica`/`Europe`), a `Region`
property on `CustomZoneDefinition` (defaults to `Europe` - matches
every zone's actual observed behavior before this existed, both in a
fresh `new CustomZoneDefinition(...)` call and when loading an old save
file that predates this field - see `CustomZoneDefinitionDto.Region`'s
own comment), and `GameZonePrefabResolver.TryFindVanillaZone` now
prefers a candidate whose `ThemeObject.m_Theme.assetPrefix` matches the
requested region, falling back to the old "any match" behavior if no
themed candidate exists for that category (a safety net, not expected
to actually trigger, but zone creation shouldn't hard-fail over a
region preference). Region is fixed at creation, same as base zone
type, for the same reason (`BuildOrRebuild` only resolves the vanilla
template once, at first build - see `CustomZoneDefinition.Region`'s own
comment).

**What's confirmed vs. not**: the whole chain compiles cleanly against
the real `Game.dll` types (a genuine signal - `ThemeObject`/`ThemePrefab`
and `PrefabBase.TryGet<ThemeObject>` are real, not fabricated), and 33
Core tests cover `Region`'s persistence/backward-compat behavior. **Not
yet confirmed**: whether vanilla zone prefabs actually carry a
`ThemeObject` component at all (vs. the EU/NA distinction living
somewhere else entirely, e.g. purely in `UIObject.m_Group`'s target
group prefab identity), and whether `assetPrefix`'s literal values are
really `"NA"`/`"EU"` rather than something else. Both are strong,
evidence-backed inferences, not confirmed live data - the fallback to
"any match" means the worst case if this turns out wrong is the toggle
silently has no effect (zones keep defaulting to whatever they did
before), not a crash or a broken zone. Needs an in-game test: create
one zone as NA and one as EU (same base type), check the zone-painting
tool's category picker actually places them under different regional
groups.

### Phase 5 audit found a real bug: unguarded file I/O on every save (2026-10-12)
UI_OVERHAUL_PLAN.md's Phase 5 (loading/error/validation states) was
deliberately low-priority until a real failure path turned up. Audited
`CollectionUISystem`/`ZoneUISystem` and `JsonFileCollectionLibrary`/
`JsonFileCustomZoneDefinitionLibrary` for one - found a real one, not a
hypothetical: `WriteToDisk()` in both library classes did raw
`File.WriteAllText`/`Delete`/`Move` with zero exception handling, called
from every single mutation trigger (create/delete/rename/add/remove, on
both Collections and Zones). A genuinely possible failure (disk full,
the file locked by antivirus/backup software, permissions denied) would
throw uncaught from inside a UI trigger callback - and worse, since
`Save()`'s original ordering updated the in-memory dictionary *before*
attempting the write, a failure there left in-memory state ahead of what
was actually on disk: a crash right after would silently lose a change
the player believed had saved.

Fixed with strong exception safety in Core (`Save`/`Delete` either fully
succeed or throw and leave state exactly as it was before the call) and
logging moved to the UI layer (`CollectionUISystem.TrySave`/`TryDelete`,
`ZoneUISystem`'s equivalents, via `Mod.log.Error`) - deliberately not
logged inside Core itself, which has no game/logging dependency by
design (see ARCHITECTURE.md); confirmed `Colossal.Logging.ILog.Error(Exception,
object)` is a real overload via live reflection against the installed
`Colossal.Logging.dll` (not guessed) - needed a plain .NET 8 console app
to check, since PowerShell/.NET Framework's reflection can't load a type
with default interface methods (`TypeLoadException: Non-abstract,
non-.cctor method in an interface`) at all, reflection-only or not.

**A second, deeper bug found while writing the tests for the first fix**:
the initial rollback implementation looked structurally complete but was
silently a no-op for the actual real-world call pattern. Every real
caller does `TryGet(id, out var x); x.Mutate(...); Save(x);` - and
`TryGet` was returning the library's own live object reference, not a
copy. By the time `Save` ran, the caller's mutation had already landed
on the exact object still sitting in the dictionary, so "roll back to
the previous value" was rolling back to the *same, already-mutated*
object - structurally present, semantically useless. Caught by a test
that initially had the identical bug (it mutated its own local variable
directly instead of going through `TryGet`, so it couldn't fail even
against the broken implementation) - rewriting the test to match the
real `TryGet -> mutate -> Save` pattern immediately exposed it. Fixed by
adding `Collection.Clone()`/`CustomZoneDefinition.Clone()` and having
`TryGet` return a clone, not the live reference - now documented as part
of both interfaces' contracts, not just an implementation detail.

Confirmed via 6 new tests (not mocked - real file I/O, a read-only file
and a directory-as-path used to deterministically force real write
failures), all 30 tests passing. Not yet confirmed in-game (the failure
path itself is hard to trigger live - a locked file - but the normal
create/delete/rename/add/remove flows all still need an in-game pass to
confirm nothing regressed).

### Three polish items from a Road Builder compare/contrast (2026-10-12)
Did a code-level comparison against Road Builder's real shipped source
(already fetched/read this session) to find concrete, low-risk ways to
close the "looks drab" gap, rather than more speculative styling.
Implemented the three cheapest, lowest-risk findings:

1. **Entry animation** - the shell previously just snapped into
   existence on open (conditional render, no transition). Added a
   slide+fade `@keyframes shellEntry` on `.shell`, directly matching
   Road Builder's real `SidePanel.module.scss` `@keyframes entry`
   (`opacity: 0 -> 1`, slides in from `right: -10px`). Replays every
   open/close since the whole `.shell` div is freshly mounted each time
   (`shared/shellVisibility.ts`'s conditional render), not just once.
2. **Built-in game icons, not a third-party dependency** -
   `shared/icons.ts` previously sourced Plus/Trash/Pencil from Unified
   Icon Library (`coui://uil/...`), optional and silently absent if not
   installed. Switched to the game's own built-in assets
   (`Media/Glyphs/Plus.svg`, `Media/Glyphs/Trash.svg`,
   `Media/Editor/Edit.svg`) referenced by plain relative path, zero
   `coui://` scheme - confirmed two ways: the paths actually exist on
   disk in the installed game's `Content/Game/UI/Media/` folder, and
   Road Builder's real source uses this exact relative-path technique
   (`<img src="Media/Tools/Snap Options/All.svg" />`, read directly
   earlier this session) rather than any custom scheme, which works
   because mod panels render into the same document as the vanilla
   game UI. UIL is no longer referenced anywhere in this mod - removed
   from README's dependency table.
3. **Custom tab-selected/hover styling** - cs2/ui's `Button` `selected`
   prop styling is a black box (only its type declarations are visible,
   not its real implementation), so rather than guess at overriding it,
   layered our own conditional `className` on top instead - the same
   technique Road Builder's real `.mode` buttons use
   (`classNames(selectedCategory == x && styles.selected)` in their own
   source), with `!important` since this sits over the library's own
   rules, not replacing them. `ShellFeature`'s tab buttons now also get
   `styles.tabButton`/`styles.tabButtonSelected`, using
   `var(--accentColorDark)` for the selected state (same token Road
   Builder's own selected-state styling uses).

Not yet confirmed in-game.

### Assets tab virtualization, second attempt: plain ref + onScroll, no ScrollController (2026-10-11)
Follow-up to the entry below (first attempt crashed the whole HUD).
Research pass found no real CS2 mod constructs `ScrollController`
directly - Find It (a real asset browser with potentially thousands of
items) does its virtualization entirely server-side instead: C# tracks
a `ScrollIndex`, only sends the currently-visible data slice through the
binding, and the UI hand-rolls its own wheel/scrollbar-thumb interaction
calling a `SetScrollIndex` trigger back into C# - no `cs2/ui` `Scrollable`
or `ScrollController` involved at all. That's the proven pattern, but a
real rearchitecture (move filtering server-side, new binding/trigger,
custom scroll UI) - user chose a smaller, lower-risk experiment instead
("can't hurt"): rewrote `useVirtualizedRows.ts` to avoid `ScrollController`
entirely.

New approach: `Scrollable` forwards a plain ref to its underlying
`HTMLDivElement` (confirmed via its own `RefAttributes<HTMLDivElement>`
type) - attach a ref, read `.scrollTop` from it inside the existing
`onScroll` callback prop (an ordinary `() => void` callback, not a class
with its own construction/attachment lifecycle). Both are standard React
patterns, not SDK-specific machinery, which is the actual risk being
managed here, not just "does it work." If `onScroll` turns out not to
fire for some reason in this engine, the degraded behavior is the list
always showing its top slice while scrolling - a visible bug, not a
crash.

**Confirmed working in-game**: no crash, smooth scrolling through the
full unfiltered ~3962-row list, correct rows shown throughout.

### Assets tab list virtualization attempt - reverted, broke the whole HUD (2026-10-11)
User reported the Assets tab was "super laggy" - root cause diagnosed
correctly: with no filter applied it renders up to ~3962 rows into the
DOM simultaneously (thumbnail + two text spans + maybe a button each),
which this engine (cohtml/Gameface) handles considerably worse than a
desktop browser. Not new to this session - already flagged as a likely
cause of stutter even in the old pre-overhaul `BuildingPicker`. The
*diagnosis* is still believed correct; what follows is about the fix.

First attempt: hand-rolled virtualization (`shared/useVirtualizedRows.ts`,
now deleted), built against `cs2/ui`'s typed `Scrollable`
`controller?: ScrollController` prop and `ScrollController.container`/
`onScroll`, rendering only in-view rows via `position: absolute` inside
a full-height spacer div. Typechecked cleanly against `UI/types/ui.d.ts`
and compiled with zero errors - but **the type declarations describing
the shape of an API are not proof it's safe to call at runtime the way
they imply**, the same category of gap this project has hit before
(ArrayWriter's real constructor, IJsonWriter's real methods - things
only confirmed by checking actual behavior, not a header file). This
session's one other attempt to build real-shell UI on an unverified
assumption (the `draggable` Panel) took down the whole game HUD on first
test, not just the mod's own panel - **the same thing happened here**:
clicking the Assets tab made the entire HUD disappear.

The underlying reason this class of bug is so severe in this project,
confirmed earlier via `UI.log`: mod panels share the same `'Game'`
render root as the vanilla HUD, with no error boundary around an
individually appended panel - one uncaught throw anywhere in that tree
takes the whole thing down, not just the offending panel.

**Reverted immediately** (`git checkout` on `AssetsTab.tsx` back to the
last commit, deleted the new hook) rather than attempt a blind fix -
restore a known-good state first, root-cause properly before
reattempting, same discipline as the `draggable` Panel incident. Did
NOT revert the unrelated same-batch changes (panel width, thumbnail
size, filter bar row split) - those are plain CSS/JSX restructuring
with no new runtime API surface, much lower risk, and not a plausible
cause of a total HUD crash.

**Still needed before reattempting**: find out whether `new
ScrollController()` is actually safe to construct freestanding (vs.
needing to come from somewhere else - a hook, a ref callback, an
internal factory), ideally from a real working mod's source that uses
it, not from the ambient `.d.ts` alone. The lag itself is still real and
unfixed - revisit once that's answered, don't reattempt the same
construction pattern hoping it works differently.

### UniversalModMenu dock icon comes from the Paradox Mods publish listing, not mod code (2026-10-09)
Resolved the open question from the probe entry below (what controls the
dock icon shown next to a mod's `UniversalModMenu` entry) with direct
filesystem evidence, not more theorizing. Paradox's subscription cache
is `Cities Skylines II\.cache\Mods\mods_subscribed\<id>_<version>\` -
Node Controller (a real installed mod showing a distinct "NC" icon in
the dock) lives there with a `.metadata\metadata.json` containing
`displayName`, a `displayImagePath` (a published-store cover image URL),
author, description - all pulled from its Paradox Mods store listing at
subscribe time. No `mod.json` and no small/square icon file exists
anywhere in its installed folder.

Decisively confirmed by the *mismatch* between the two mods: Node
Controller's own shipped `NodeController.mjs` (decompiled and grepped
directly) never calls `moduleRegistry.append('UniversalModMenu', ...)`
at all - yet it still gets a custom dock icon. Our mod DOES call that
append (`UniversalModMenuToggle`, see the probe entry below) and still
shows the generic icon. The icon clearly isn't sourced from the
component registered on the hook at all. `.cache\Mods\local\` (where an
unpublished local dev mod would be tracked) is completely empty - no
metadata, no cover image, for any local mod.

**Conclusion**: a locally-built, unpublished mod (our current state)
cannot show a custom `UniversalModMenu` dock icon through any code-side
mechanism - confirmed, not just theorized. It's tied entirely to the
Paradox Mods publish/subscribe pipeline (the `Properties/Thumbnail.png`
this mod already has, referenced by `PublishConfiguration.xml`, is the
right asset for this - just untestable locally short of an actual
publish, even a private one).

### UniversalModMenu probed in-game: too small to host the real shell (2026-10-09)
Tried the native `'UniversalModMenu'` append hook (confirmed real: present
in our own `UI/types/modding.d.ts`'s `AppendHookTargets` union, and
documented at cs2.paradoxwikis.com/UI_Modding as "a single organized,
scrollable panel") as a possible native open/close mechanism for the
whole shell, since neither Road Builder nor Find It use it (checked
directly) and the docs don't specify sizing/layout. Registered a
deliberately plain probe component (`UniversalModMenuProbe.tsx`, no
Panel wrapper, no fixed width) alongside - not replacing -
`ShellFeature`'s existing `'Game'` append, specifically to observe the
real contract before committing anything to it.

**Confirmed in-game**: a mod icon appears in a bottom-right dock
alongside several *other* already-installed mods' icons (so the hook
does have real adoption - just not from the two mods researched
directly) - default/generic icon since `mod.json` has no icon field and
no `Thumbnail.png` exists on disk yet. Clicking it expands content in
that same bottom-right corner, in a genuinely small fixed box - small
enough that two plain sentences of unstyled text needed their own
internal scrollbar to read in full. The "scrollable panel" the wiki
describes is real, but it's a compact widget tray, not meaningful real
estate - nowhere near enough for `ShellFeature`'s tabs/filters/lists.

**Conclusion**: don't migrate `ShellFeature`'s actual content onto this
hook. Worth revisiting only as a *trigger* (a small "open ZoneBuilder"
entry that toggles the real panel's visibility elsewhere), not as the
container for it - see the open question below for what that would
need.

### Phase 4c implemented: Asset Icon Library writes directly into UIObject.m_Icon (2026-10-09)
Follow-up to the research entry below, which concluded `UIObject.m_Icon`
was the wrong data for per-building thumbnails and flipped Phase 4c's
preference to Asset Icon Library (AIL). Before implementing an AIL-
specific integration (`.ail` files or a `GetIconsMap` hook), read AIL's
actual source directly (`IconReplacerSystem.cs`,
`Utilities/IconLibraryUtil.cs`, `Utilities/IconAPIUtil.cs`, fetched and
read raw from GitHub, not inferred from its README) to confirm what it
actually does with the icons it resolves.

Finding: AIL doesn't just expose its icon map for other mods to query -
it writes its resolved URL straight into `UIObject.m_Icon` on every
building prefab it covers (`IconReplacerSystem.cs`:
`uIObject.m_Icon = thumbnail;`). The URL is a real `coui://ail/<path>`
(confirmed in `IconLibraryUtil.GetAvailableIcons()`'s `addIconName`
local function), built from three sources that all funnel into the same
field: `.ail`/`ail` mod folders, `.ail` JSON map files, and reflection-
based calls into every other loaded mod's `IMod` class looking for
`GetIconsMap()`/`GetIconsRootFolders()` (`IconAPIUtil.LoadModIcons()`).

This means the originally-ruled-out approach - just read
`UIObject.m_Icon` - was actually correct, for a reason the first
research pass couldn't have known without reading AIL's own code: it's
not static vanilla data, it's a field AIL actively populates at runtime
if installed. No `GetIconsMap`/`.ail` integration needed on this mod's
side - just read the same field `ZonePrefabCloner` already reads/writes
`m_Group` on. Implemented as `GameBuildingCatalog.ResolveIconPath`,
threaded through to the UI as `BuildingEntry.iconPath`, rendered via
`shared/BuildingThumbnail.tsx` (placeholder box when empty - most
buildings will have no icon unless AIL is installed and covers that
specific prefab).

**Risk resolved (2026-10-12), confirmed in-game with AIL actually
installed**: `BuildingPickerUISystem.OnGameLoaded` reads `m_Icon` exactly
once, and the worry was that AIL's own icon-population pass might run
*after* that point (mod execution order between two independent mods
isn't something this mod controls) - meaning every `iconPath` would come
back permanently empty despite AIL being installed. Not the case: user
confirmed most buildings show real AIL thumbnails. AIL's write clearly
lands before our one-time read, at least for the buildings it covers -
no need for a later/re-triggerable read point.

One real, separate gap found via the same report: NA Old Town assets
specifically show no icon (placeholder only) while most other buildings
do. Root cause is almost certainly **AIL's own coverage**, not anything
on this mod's side - AIL's icon database is pre-rendered/community-
contributed sets that can simply lag behind a newer or less common
theme/asset pack. Nothing to fix here; this is the expected "AIL doesn't
cover everything" case the placeholder design already accounts for.

### Phase 4c research: per-building thumbnails are a runtime render, not a static field (2026-10-09)
UI_OVERHAUL_PLAN.md's Phase 4c had two candidate approaches for asset
thumbnails, "read the prefab's own icon path" listed as preferred over
Asset Icon Library (AIL). Checked that assumption via live reflection
against the actually-installed `Game.dll` (found through
`CSII_TOOLPATH`/`CSII_MANAGEDPATH`'s real resolution chain in
`Mod.props`, same env vars the build itself uses - not guessed), using
`Assembly.ReflectionOnlyLoadFrom` with every DLL in the game's `Managed`
folder preloaded first (PowerShell/.NET Framework's reflection-only load
context needs every dependency preloaded or it throws rather than
resolving lazily).

Findings:
- `Game.Prefabs.UIObject` is real and does have a `public string m_Icon`
  field (confirmed alongside the already-used `m_Group` field - same
  component `ZonePrefabCloner.cs` already touches). But cross-checking
  against the actual installed game files
  (`Cities2_Data/Content/Game/UI/Media/Game/Icons/Zone*.svg`) shows this
  is populated with one icon per zone/category, not one per building -
  it wouldn't visually distinguish between different buildings of the
  same zone type, which was the entire point of adding thumbnails.
- The vanilla building browser's real per-asset thumbnails come from
  `Game.UI.Thumbnails.ThumbnailCache.GetThumbnail(Object obj, int width,
  int height, Camera camera)` - a genuine runtime 3D render via a Unity
  `Camera`, confirmed by reading the actual method signature off
  `Game.dll`. Meaningfully more involved than a field read: needs a
  render target/camera and a way to hand the resulting texture to
  cohtml's `coui://` layer, none of which is verified end-to-end yet.
  (Also found `Game.UI.GameUIResourceHandler` has a
  `TryThumbnailRequestAsync` method via its compiler-generated async
  state machine type, consistent with a thumbnail *request* pipeline
  existing - not chased further than that, see "what not to do" below.)

Conclusion: flipped Phase 4c's preference order - Asset Icon Library
(already-solved problem, MIT, same author as Find It/Road Builder) is
now the lead approach; reading `UIObject.m_Icon` is ruled out (real
field, wrong data); rendering via `ThumbnailCache` ourselves is now the
explicit fallback, and would need its own dedicated research pass (not
a guess from a method signature) if AIL's coverage ever proves
insufficient. Deliberately stopped short of reverse-engineering the
exact `coui://` scheme `GameUIResourceHandler`/`ThumbnailCache` use
internally - not needed to make this decision, and the session's own
rule for `getModule()`-style reverse-engineered APIs (Road Builder's
theme reuse, Phase 6) is to only go that deep when something else
forces the issue.

### Started a real mod dependency list - documentation only, mod.json schema still unconfirmed (2026-10-09)
Added a "Dependencies" section to README.md listing Unified Icon Library
as optional/recommended (not required - the mod works fully without it,
icons just don't show). Deliberately did NOT populate `UI/mod.json`'s own
`dependencies: []` array to match, even though that field exists and looks
like the obvious place for this: checked two real mods' actual `mod.json`
(this project's own, and Road Builder's, fetched directly from GitHub) and
both have `"dependencies": []` despite clear real relationships with other
systems - no confirmed example of what a *populated* entry looks like, or
whether it's a hard load-blocking requirement or a soft/recommended one.
Unified Icon Library's own README gives no guidance either ("install it
separately, the coui:// URLs just work" - no mod.json wiring mentioned at
all). Given a wrong guess here risks actual mod-loading behavior (a much
higher-stakes mistake than a UI bug), left it undeclared in `mod.json`
rather than guess the schema - the icons already work correctly without
it (confirmed: a plain runtime URL reference that degrades gracefully).
Revisit if/when a real example of a populated `dependencies` entry turns
up (a mod with a genuine hard prerequisite would be the thing to find and
read).

### First Unified Icon Library integration pass (2026-10-09)
Wired real icons (`coui://uil/Standard/<Name>.svg`, confirmed Apache-2.0
and purely optional - see the earlier icon-library research entry) into
every Create/Edit/Delete/Confirm/Add/Remove button across all four
panels, using `cs2/ui`'s `Button` directly - confirmed via `UI/types/
ui.d.ts` that `Button`'s real prop type already merges in
`Partial<LabeledIconButtonProps>` (`src`, optional `theme`), so no
separate `IconButton`/`MenuButton` component was needed, just an extra
`src` prop alongside the existing `children` text.

Only icon names the earlier research actually confirmed exist in UIL's
catalog were used (`Plus`, `Trash`, `Pencil`) - an unconfirmed name would
just 404 into a blank icon slot, worse than no icon at all. No formal
dependency declaration was added to `mod.json` - confirmed (via other
mods' actual deployed output) that this is a plain runtime URL reference
that 404s silently if UIL isn't installed, not an API call requiring
load-order coordination, so a hard/soft dependency entry isn't needed for
it to work; could add one later purely so a mod manager lists it as
recommended, but that's cosmetic, not functional.

Reused the Trash icon for both true deletion (Collection/Zone Delete,
Confirm) and relationship removal (Collection↔Zone detach, building
removed from a Collection) - reconsidered the earlier instinct to avoid
that overlap (see the wireframe-review entry's "Remove" vs "Delete"
naming decision) and concluded it doesn't actually apply here: that
naming rule is about not mislabeling one action as another, which is
carried entirely by the TEXT label (still "Remove" vs "Delete",
unchanged); within any single panel there's never a Trash-icon Remove
button sitting next to a Trash-icon Delete button for the same entity, so
there's nothing to actually confuse.

**In-game result: icons loaded correctly (confirms the mechanism works),
but icon+text combo buttons (passing both `src` and visible text
children) overlapped and got clipped off the panel edge** - on every such
button, not just the tight list rows, which suggests this isn't purely a
"not enough room" problem but that `cs2/ui`'s icon+label layout doesn't
render with correct spacing in this environment at all. Fixed by
switching every dense-row action (Edit/Delete/Confirm/Add/Remove) to
icon-only (`variant="icon"`, no text children, `tooltipLabel` for
accessibility per the wireframe review's own rule) - this exactly matches
how real CS2 mods handle these (Find It/Road Builder: "every clickable
control is `cs2/ui`'s `Button`, always `variant=\"icon\"`", confirmed via
earlier research, not newly assumed). Left `Create` as plain text with no
icon, rather than also guess at some OTHER icon+text combination that
might work - it's the one known-good state next to two now-confirmed
failure modes. Not yet re-confirmed in-game.

### Resize handle click-through: the z-index fix didn't actually work - real fix was removing the height mismatch (2026-10-09)
Follow-up correction to the anchoring fix below. That fix (moving
`ResizeHandle` to be `Panel`'s sibling instead of its child, to anchor it
to the full declared panel height rather than wherever `Panel`'s content
happened to end) visually worked but broke click-through; a same-day
z-index bump was applied as the fix and *claimed* to resolve it in the
commit message, without being retested in-game first. **It didn't work** -
confirmed on the next real test pass, the handle still wasn't clickable.
Guessing a z-index value against an opaque, unverifiable "maybe Panel's
own chrome has a higher internal z-index" theory was the wrong approach
twice in a row.

Real fix: stop creating the height mismatch in the first place, rather
than fighting its symptom. `Scrollable` now gets an exact `height` (not
`maxHeight`) equal to the intended content height, so `Panel`'s own
natural/auto height (header + that exact Scrollable height + the
create-row) always equals the full declared panel size with no leftover
gap - and `ResizeHandle` moved back to being `Panel`'s own child, the
exact arrangement already proven clickable before any of this. No z-index
needed. Lesson: when a fix is based on an unverifiable theory about a
third-party component's internals (here, "Panel might set its own
z-index"), say so plainly and treat it as unconfirmed until actually
retested - don't write the commit message as if it's settled.

### Adapted RoadBuilder's ExtendedUISystemBase (MIT) to cut C# binding boilerplate (2026-10-09)
A second external-mod research pass (Road Builder, github.com/JadHajjar/
RoadBuilder-CSII) found and confirmed a genuinely permissive **MIT**
license (verified by reading the actual `LICENSE` file, not just the
user's claim) - unlike Find It (no license, all-rights-reserved by
default) or the other mods checked earlier this session, Road Builder's
code can actually be adapted directly, with attribution.

Adapted `RoadBuilder/Systems/UI/ExtendedUISystemBase.cs` wholesale into
`src/UI/ExtendedUISystemBase.cs` (full MIT notice kept at the top of the
file, per the license's one real requirement). It gives `CreateBinding`/
`CreateTrigger` helpers that collapse what used to be manual `ValueBinding`
+ `ArrayWriter<T>` + `TriggerBinding` + `ValueReaders.Create<T>()` wiring
into one call each, via `GenericUIWriter`/`GenericUIReader` (reflection-
based `IWriter<T>`/`IReader<T>` that serializes any plain object's public
properties/fields automatically).

Before trusting any of it, verified every API it depends on actually
exists in this project's own installed game DLLs (not assumed just
because Road Builder uses it against presumably the same game version):
`Colossal.Reflection.ReflectionUtils.HasAttribute<T>(this MemberInfo,
bool)` (real extension method), `Colossal.UI.Binding.GetterValueBinding<T>`
and `Game.UI.UISystemBase.AddUpdateBinding` (both real), and
`Colossal.UI.Binding.ValueReaders`'s private static `s_Readers` field
(which `GenericUIReader` reflects into by exact name - fragile by nature,
but confirmed present with the expected `Dictionary<Type, object>` shape).
Also caught and fixed two mistakes introduced while adapting (not bugs in
the original): `IJsonWriter.ArrayBegin` takes `uint`, not `int` - fixed;
`IJsonWriter` has no `WriteEmptyArray()` - replaced with `ArrayBegin(0);
ArrayEnd();`. One revert: briefly "fixed" `IJsonReader.ReadArrayElement`'s
argument from the original's `(ulong)i` to `(uint)i`, assuming symmetry
with `ArrayBegin`'s `uint` - reflection on the real `IJsonReader` interface
confirmed `ReadArrayElement` actually takes `UInt64`, so that was a real
regression caught before it shipped, not a redundant check.

**Important compatibility decision**: `CollectionUIEntry`/`ZoneUIEntry`/
`BuildingPickerEntry` deliberately KEPT their own hand-written
`IJsonWritable.Write()` implementations rather than switching to
`GenericUIWriter`'s reflection. Reason: reflection writes a plain object's
public property names verbatim (PascalCase, C# convention, no camelCase
conversion) - switching would have silently changed the JSON keys every
TS consumer already expects (`id`/`name`/`entryCount`/`prefabIds` →
`Id`/`Name`/`EntryCount`/`PrefabIds`), breaking every panel's data binding.
`GenericUIWriter` checks `obj is IJsonWritable` first and delegates when
present, so keeping these three DTOs' existing `Write()` methods is what
keeps the wire format - and every TS file - unchanged while still letting
`CollectionUISystem`/`ZoneUISystem`/`BuildingPickerUISystem` (all three
migrated to `ExtendedUISystemBase`) drop their `ArrayWriter<T>`/
`XEntryWriter` wrapper classes and manual trigger-reader wiring entirely.

One real behavioral change, judged acceptable: `ValueBindingHelper<T>.Value`
is dirty-tracked, not applied immediately - the actual `Binding.Update()`
call happens on the next `OnUpdate()` tick (every frame, since these
systems run `UpdateAt<_>(SystemUpdatePhase.UIUpdate)`), not synchronously
inside the trigger handler like the old `.Update(...)` calls did. A
one-frame (~16ms) delay between a mutation and the UI reflecting it is
imperceptible and matches Road Builder's own shipped, working behavior -
not treated as a regression.

**Not yet confirmed in-game** - the previous build (hand-written
`IJsonWritable` + manual bindings, pre-migration) was mid-test when this
work started; next test pass should confirm Collections/Zones/the
building picker all still work identically (create/rename/delete/add/
remove/attach/detach), since the wire format is unchanged but every
binding's C#-side plumbing was rewritten.

### Four UI gaps closed from an external wireframe review (2026-10-09)
The user had a separate AI review a UI wireframe/spec for a comparable
"Zone Builder" mod concept and asked for an opinion on what to incorporate.
Agreed-on low-risk items, implemented this pass:
- **Delete confirmations** for Zone/Collection (previously instant,
  irreversible, no warning).
- **Remove asset from collection** - previously the only way to drop a
  single building was deleting the whole collection. `Collection` gained
  `RemoveExplicitAsset(prefabId)`, `CollectionUISystem` gained
  `RemoveBuildingFromCollection`, and the building picker's per-row button
  is now a real Add/Remove toggle (was Add-only, with an "Added" status
  label that did nothing on a second click).
- **Richer empty states** (explain + action, not a bare one-liner).
- **Inline validation** on both Create forms (previously silent no-op on
  empty name/missing base type).

Confirmation dialogs deliberately use a hand-rolled inline pattern (row's
Edit/Delete replaced by "are you sure?" text + Cancel/Confirm), not
`cs2/ui`'s `ConfirmationDialog`/`DialogStack`. Reason: two prior incidents
this session came from unverified assumptions about how a `cs2/ui`
interaction pattern behaves outside the vanilla game's own usage
(`<select>` crashed the whole HUD; `draggable` Panel broke the game's own
ribbon layout) - `ConfirmationDialog`'s own type shape hints it may expect
being invoked via the `DialogStack` context's `showDialog()`, not rendered
as ordinary conditional JSX, and that wasn't verified against any working
example before building on it. The hand-rolled version only uses `Button`
+ conditional rendering, both already proven safe in this exact codebase.
Worth trying `ConfirmationDialog` properly later if a real example of its
usage turns up, but not worth the risk right before a session with no
immediate in-game retest.

The wireframe's bigger structural suggestions (one tabbed window instead
of four floating panels; modal create/edit dialogs instead of always-
visible inline forms; batch-select "N selected" asset picker instead of
one-click-per-row) were deliberately NOT adopted - each is a real,
debatable tradeoff against what's already built and working, not a clear
improvement, and not something to decide unilaterally. Revisit only if the
user wants to reopen that specific question.

### Build both features, as one mod with per-feature toggles
- Both share the core (collections, adopted assets, building picker). Two
  mods would mean either duplicating it or a third library mod and
  lockstep releases on every game patch.
- Save-safety concerns are handled by the Custom Zones toggle instead.
- Code is split into modules so it could be repackaged as separate mods
  later without a rewrite.

### Use collections as the shared abstraction
Custom zones and district themes are two ways of applying a collection:
- custom zone = "this zone spawns only from collection X"
- district theme = "lots in this area spawn only from collections X, Y,
  restricted to zone types A, B"

### Adopted assets get a vanilla zone profile and are limited to themes
Unzoned assets (e.g. downloaded houses) are given growable data at load
time. By default they spawn only where a collection that includes them is
in use (custom zone or district theme), not citywide.

### Build order
1. Core: collections + adopted assets
2. Custom Zones: lower technical risk, likely reuses the vanilla spawner
   via zone/building prefab cloning
3. District Themes: requires replacing (or post-correcting)
   `ZoneSpawnSystem`, which is Burst-compiled and not easily patched

Real UI (`UI/core`'s collection editor, `UI/customZones`, `UI/districtThemes`)
is deferred until the above are mostly built - building it against a
stabilized backend beats co-evolving both, and the risk in this project is in
the GameAdapters, not the UI. Exception: small, throwaway test affordances
(e.g. a hardcoded `Collection` wired up in `Mod.cs`, a debug-only action) are
fine earlier, specifically to exercise a GameAdapter in-game once it exists.
These aren't meant to survive into the real UI.

### Fallback policy is a parameter, not hardcoded
`Core.CandidateFilter.GetEligibleBuildings` takes a `FallbackPolicy` (`None`
or `VanillaZone`) on every call rather than baking in one behavior. Satisfies
README's "fallback behaviour... configurable" for District Themes without
deciding yet what the default is or how it's exposed in UI (see open
questions).

### Collections persist in a global library, referenced by GUID
Collections live outside any save, in the mod's own user-data file(s) (same
`[FileLocation]` mechanism `Setting.cs` uses), not inside save-game data.
Zones/districts in a save store only a GUID reference, never a copy or the
display name:
- **Rename is free.** Changing a collection's display name doesn't affect
  any save that references it, since the reference is the GUID, not the
  name.
- **Delete degrades, doesn't block.** No attempt to warn at delete-time -
  that would mean scanning every save on disk, infeasible at "hundreds of
  saves" scale. A save that loads with a now-missing GUID falls back via
  `Core.FallbackPolicy` and surfaces one log/notification per missing
  reference per load, so it's not silently confusing.
- GUID, not a user-chosen name, since there's no near-term plan to support
  sharing collections between users (would need a different scheme to avoid
  collisions if that changes later).

This is what makes README.md's "editing a collection changes future spawns
everywhere... no repainting needed" promise hold: the library is the single
source of truth, saves only ever hold pointers into it.

### A custom zone is itself a persisted, GUID-identified thing (2026-10-08)
Collections alone weren't enough to satisfy the actual user workflow: "I
build a zone from a collection today; tomorrow I add newly-downloaded
Workshop assets to that collection and want the zone to pick them up." A
`Collection` has no memory of which zone(s) were ever built from it, and a
built zone is just a cloned prefab with no persisted link back to anything
- there was nothing to even trigger a re-sync against. `Core.CustomZoneDefinition`
(Id, Name, a base `Core.ZoneType`, and the `Collection` Id(s) it pulls
from) closes that gap, persisted the same GUID-keyed way as `Collection`
(`ICustomZoneDefinitionLibrary`/`JsonFileCustomZoneDefinitionLibrary`, own
file). Same rename-free principle applies, for a sharper reason than
Collections: the in-game zone/building prefabs this builds need a name
that's stable across renames, or a rename would orphan everything already
built under the old name. `CustomZoneBuilder` derives that internal name
from `CustomZoneDefinition.Id` (`ZoneBuilder.Zone.<guid>[.<template name>]`),
never from `Name` - see its own comments.

### Rebuilding a custom zone is additive; removing an asset doesn't touch what's already built
Requesting the same `CustomZoneDefinition` again (`CustomZoneBuilder.BuildOrRebuild`,
via `CustomZoneBuildRequestSystem`) finds what was built before by the
stable internal name above, rather than creating a duplicate or starting
over. Two cases:
- **A newly-resolved building that wasn't cloned before** gets cloned now
  (additive) - this is what makes "add Workshop assets to the collection,
  then ask for the zone to be rebuilt" work without ever repainting or
  losing anything already placed.
- **A building that was cloned before but isn't resolved anymore** (its
  source entry was removed from every referenced `Collection`) gets its
  `BuildingSpawnGroupData` cleared (set to zone index 0 - never assigned to
  any real zone) so it stops being offered for *new* construction. The
  prefab entity itself is never touched or removed, so any building
  already standing in the city (which references its own entity directly,
  not the zone's candidate list) is completely unaffected - it just keeps
  standing, and the game simply stops building more of that type going
  forward. Confirmed this is the desired behavior, not an accepted
  limitation: removing an asset is about curating *future* growth, not
  retroactively judging what a player already built, and it matches how
  District Themes already treats collection edits as affecting future
  spawns only, not existing buildings.

Consciously *not* built: zone deletion, or removing a `CustomZoneDefinition`
entirely once built. Not needed for the "create/add to" workflow above, and
removing an entire zone a player has already painted lots with is a much
bigger question (orphaned lots? forced re-zoning?) worth deferring until
someone actually needs it.

### Collection library migration is deferred, but with a real trigger point
No migration logic for `collections.json`'s `SchemaVersion` exists yet, and
that's fine for now: `PublishConfiguration.xml` has `AccessLevel="Private"`,
so right now it's only ever this machine's own file, fixable by hand if the
shape changes. The deferral isn't open-ended though - it needs to be built
*before* the first public release that changes `Collection`/`CollectionEntry`'s
shape, since at that point a player's old library file has to keep loading
under new code with no "just fix it by hand" option.

### Custom Zones needs no Harmony patches - prefab cloning is enough
Verified against the decompiled `Game.dll` (see `TOUCHPOINTS.md`):
- `PrefabSystem.AddPrefab`/`PrefabBase.Clone` is a real runtime API, used by
  the vanilla playset system and the in-game editor's own "duplicate prefab"
  tool - cloning a `ZonePrefab` and registering it gets a fully independent
  zone type for free; `ZoneSystem` assigns it a fresh index automatically.
- Building spawn selection (`ZoneSpawnSystem`) and level-up selection
  (`BuildingUpkeepSystem`) both filter candidates purely by matching a
  building prefab's `BuildingSpawnGroupData.m_ZoneType` (set from
  `SpawnableBuilding.m_ZoneType`) against the lot's zone. So: clone the
  zone, clone each collection building and repoint its zone reference, and
  vanilla's own (Burst-compiled, unpatchable) spawn/level-up systems
  naturally restrict themselves to the right buildings.
- This resolves three of the open questions below outright and confirms the
  "lower technical risk" guess in Build order.

**Update (2026-10-08): confirmed true, no caveats.** The clone only ever
looked permanently uninitialized because of *when* it was being created,
not because anything about initialization itself was broken - see "A
runtime-cloned prefab is only visible to init systems for one frame" below
for the real mechanism and fix. Once a clone is added at the right point in
the frame, vanilla's spawn/level-up systems restrict themselves to it with
zero further work, exactly as this decision originally claimed.

### A runtime-cloned prefab is only visible to init systems for one frame
**Root cause found (2026-10-08), via three parallel deep-read agents
(one on `ObjectInitializeSystem`'s own query/gating logic, one on
`PrefabSystem`'s boot-vs-runtime code paths, one comparing against
DillonN/specialized-industrial-zones' real published source) - the second
two converged independently on the identical mechanism with matching
`SystemOrder.cs`/`PrefabSystem.cs` line citations.** It was never a missing
initialization step. `Created`/`Updated` are ordinary zero-size tags
(`Game.Common.Created : IComponentData, IQueryTypeParameter` - nothing
automatic about them) that get added by `PrefabSystem.AddPrefab` and then
live for exactly one frame:

- `PrefabSystem.OnUpdate` drives the `PrefabUpdate` phase (the one pass
  `ZoneSystem.InitializeZonePrefabs`/`PrefabInitializeSystem`/
  `ObjectInitializeSystem` all run under) from a fixed point in
  `SystemUpdatePhase.MainLoop`.
- `LoadGameSystem` - which is what actually dispatches every system's
  `OnGameLoaded` callback - is registered in that *same* `MainLoop` phase,
  but *after* `PrefabSystem` (`Game.Common.SystemOrder.cs`: `PrefabSystem`
  added before `LoadGameSystem`, and `UpdateSystem.SystemData.CompareTo`
  sorts same-phase systems by registration order).
- So a clone added from `OnGameLoaded` always misses that frame's
  `PrefabUpdate` pass - it doesn't exist yet when `PrefabSystem` ticks.
- `PrepareCleanUpSystem` (matches `Any={Created, Updated, ...}`) and
  `CleanUpSystem` (strips exactly those types) are also scheduled in
  `MainLoop`/`Cleanup`, *after* `LoadGameSystem` - so by the next frame,
  before `PrefabUpdate` ever gets a second look, the tags are already gone.
  Permanently invisible to every `Created`-keyed system from then on.

This is also why none of the things tried under the old "root cause
unknown" writeup changed anything: `SystemUpdatePhase.PrefabUpdate` vs.
`MainLoop` + `UpdateBefore<_, PrefabSystem>` only controls *our own*
system's position, not `LoadGameSystem`'s (which is what actually calls
`OnGameLoaded`, fixed regardless); deferring 30 ticks doesn't help once the
tags are already stripped; and manually re-invoking
`UpdateSystem.Update(SystemUpdatePhase.PrefabUpdate)` right after
`AddPrefab` doesn't work because that call re-enters `Update()` from
*inside* the outer `Update(MainLoop)` loop already in progress, and
`Refresh()` clears the update list mid-iteration if it's dirty - corrupting
the outer loop rather than doing anything useful.

**The fix**: never call `AddPrefab`-triggering code from `OnGameLoaded` (or
any other arbitrary call site) directly. Register a dedicated system with
`updateSystem.UpdateBefore<_, PrefabSystem>(SystemUpdatePhase.MainLoop)`
(the exact registration DillonN/specialized-industrial-zones - a real
published mod that clones full `BuildingPrefab`s at runtime with zero
workaround - uses) and do the actual cloning from *that system's*
`OnUpdate`, not from `OnGameLoaded`. That lands `AddPrefab` immediately
before `PrefabSystem`'s own tick, in the same frame, every time - the one
window a clone needs to be visible during. `CustomZoneBuildRequestSystem`
is what makes this a property of the architecture rather than a thing every
future caller has to remember: it owns a pending-request queue, callable
from anywhere (including `OnGameLoaded`, a UI handler, wherever), and only
ever actually calls `CustomZoneBuilder.Build` from its own correctly-timed
`OnUpdate`.

Confirmed in-game (2026-10-08) via a throwaway experiment
(`DebugTimingExperimentSystem.cs`, since removed) that used *only*
`ZonePrefabCloner`/`SpawnableBuildingZoneLinker` - no manual
reinitialization of any kind - registered this way: the cloned zone got a
real `ZoneType` index, correct color/paint behavior, `Created`/`Updated`
cleanly removed by the real `CleanUpSystem`; the cloned building's
`ObjectGeometryData`, `SubMesh` buffer, `SubObject` buffer (42 entries), and
`BuildingSpawnGroupData.m_ZoneType` (matching the zone's own index exactly)
were all populated by the real game systems, no copying required.
`ManualPrefabInitializationWorkaround.cs` is deleted as of this fix - it
solved a real set of symptoms, but by reimplementing what the real systems
do rather than fixing why they didn't run; now that they do run, every
field it used to hand-copy (`ObjectGeometryData`, `BuildingData`, `SubMesh`,
`SubObject`, `BuildingSpawnGroupData`, the `ZoneSystem` zone-registry
reflection, `UpdateZoneColors()`) is the real systems' job again.

### A UI system's `OnCreate` runs before any prefab exists - query ECS state in `OnGameLoaded` instead (2026-10-08)
`BuildingPickerUISystem` (the building-picker backend, chunk 2) originally
called `GameBuildingCatalog.GetAllBuildings()` once in `OnCreate` and bound
the result directly. In-game, the panel rendered with a header and nothing
else - no exception anywhere in `ZoneBuilder.Mod.log`, just silently zero
rows. Root cause: `Game.UI.UISystemBase` derives from `Game.GameSystemBase`
(confirmed via reflection on `Game.dll`) same as every ordinary game system,
and `OnCreate` runs at world bootstrap, before `PrefabSystem` has registered
any prefab - vanilla, DLC, or Paradox Mods pack. Same general timing class as
"A runtime-cloned prefab is only visible to init systems for one frame"
above, just the read side instead of the write side: don't assume ECS/prefab
state exists just because a system's `OnCreate` has run. Fixed by keeping
`AddBinding` in `OnCreate` (with an empty initial array) but moving the real
`GetAllBuildings()` snapshot + `ValueBinding.Update(...)` into `OnGameLoaded`
- confirmed in-game afterward, full list populated correctly.

### `cs2/ui` has no native `<select>` - it crashes the entire game HUD, not just the mod panel (2026-10-08)
Chunk 3's first pass used plain `<select>`/`<option>` for the picker's
dropdown filters (zone type, level, lot width/depth, asset pack) - the same
"fall back to plain HTML where `cs2/ui` doesn't provide the primitive"
pattern that already worked for the name-search `<input>`. In-game result:
**the entire HUD disappeared**, not just our panel. Root-caused via
`UI.log`, not guessed: a `TypeError: Cannot read properties of undefined
(reading 'length')` repeating every UI update frame, thrown inside the
*game's own* core bundle (`assetdb://gameui/index.js`), not ours - every
mod panel appended via `moduleRegistry.append('Game', ...)` shares one
render tree with the vanilla HUD, with no error boundary around an
individual appended panel, so an uncaught throw in ours takes the whole
tree down. Confirmed via the real type declarations (`UI/types/ui.d.ts`,
shipped with the modding toolchain, not guessed from memory) that `cs2/ui`
has no `<select>` equivalent at all - it has a purpose-built
`Dropdown`/`DropdownToggle`/`DropdownItem` component set instead ("Used to
select between a number of mutually exclusive values", per its own doc
comment). Rewrote the filter bar on top of that; confirmed in-game
afterward - all five filters render, filter correctly, and the dropdown
popups layer above each other and the rest of the panel correctly.
**Lesson for any future UI work**: a broken mod panel is not necessarily
contained to that panel - verify new, previously-unused host elements
(raw HTML tags, not `cs2/ui` components) carefully, and `UI.log` (not just
`ZoneBuilder.Mod.log`) is where a render-time JS exception actually shows
up, with a full stack trace.

Also found and worked around in the same pass: `cs2/ui`'s own ambient type
declarations have a real bug, not just an omission - `export interface
DropdownItem<T>` (a plain data-shape type) and the actual `DropdownItem`
*component* (internally named `DropdownItem$1`, re-exported under the
`DropdownItem` alias at the bottom of the file) collide, and TypeScript
only resolves the name to its type half: `import { DropdownItem } from
"cs2/ui"` compiles, but using it as a JSX tag fails with "'DropdownItem'
only refers to a type, but is being used as a value here." The component
itself is real at runtime (same `window`-global mechanism every other
`cs2/ui` import resolves through, confirmed by the fix actually working
in-game) - worked around by importing the module as a namespace and
casting that one property (`cs2ui.DropdownItem as unknown as (props:
...) => JSX.Element`), not by trying to fix the shipped `.d.ts` itself.

### Real CSS (SCSS/CSS Modules) for actual visual polish - the build step already existed, just unused (2026-10-09)
Researched a real CS2 mod's UI source for design inspiration (not code
reuse - no license, see "no reuse license" entries elsewhere) before any
visual polish pass. Finding: genuine polish (hover states, transitions,
selection animations) is structurally impossible with inline React `style`
objects - there's no way to express `:hover`/`:active`/`transition`/
`@keyframes` that way, independent of which component library is used.
The mod studied ships real `.module.scss` files via webpack + css-loader
(CSS Modules) + sass-loader + MiniCssExtractPlugin, using plain `transition`,
`@keyframes`, `backdrop-filter`, and `filter` extensively with no related
rendering issues - confirming this is safe in cohtml, not just a browser
assumption.

Turned out `UI/webpack.config.js` already has this *entire* pipeline
configured (css-loader with `modules: {auto: true}`, sass-loader,
MiniCssExtractPlugin, a custom `CSSPresencePlugin` that injects a `hasCSS`
flag into the compiled module so the game knows whether to also load a
`.css` file) - `sass`/`sass-loader`/`css-loader`/`mini-css-extract-plugin`/
`classnames` are already real devDependencies too. This was scaffolded by
the official `@colossalorder/create-csii-ui-mod` tool when the UI project
was first created, just never used - every panel to date used inline
`style` objects exclusively. Added the one missing piece (`UI/types/
scss.d.ts`, a `*.module.scss` module declaration for TypeScript) and
converted `ResizeHandle` as a deliberately small, isolated proof of concept
- gave it a real `:hover` state (impossible before), confirmed at the
BUILD level: webpack emits a real `ZoneBuilder.css` with correctly scoped
class names (`.handle_Byk` etc.), it deploys alongside the `.mjs`, and
`hasCSS` is present in the built output.

**In-game result (2026-10-09): the handle was invisible/unfindable**, no
JS exception logged anywhere. First guess - that the stylesheet was never
actually loaded at runtime - was wrong, caught by a dedicated research
pass before building further on the wrong theory. Reverted `ResizeHandle`
to inline styles (the known-working version) as a stopgap while that
research ran.

**Actual root cause, confirmed from the game's own loader source and
`UI.log`, not guessed**: the CSS genuinely loads. The game's compiled UI
bundle (`Cities2_Data/Content/Game/UI/index.js`) reads the `hasCSS` flag,
replaces `.mjs`→`.css` in the same URL, and injects a `<link
rel="stylesheet">` - exactly the convention this mod's webpack output
already follows (confirmed identical, byte-for-byte, to a real installed
mod's deployed output, Find It's `FindIt.mjs`/`FindIt.css`). `UI.log`
itself proved the file loaded: it logged a parse warning quoting our
*exact* `:hover` rule back verbatim ("Custom CSS expressions are not
supported in shorthand declaration... background -
linear-gradient(...var(--accentColorNormal,rgba(...))...)"). The real bug
is a vendor-documented cohtml/Gameface engine limitation: CSS custom
properties (`var()`) only work in **longhand** properties
(`background-image`, `border-color`, etc.), never shorthand (`background`,
`border`, `transition`, `padding`...), and `var()`'s fallback argument
(the second value) isn't supported **at all**. The rule hit both at once.
Fixed by switching to `background-image` and dropping the fallback
(`--accentColorNormal` is always defined globally by the game, so no
fallback was needed anyway) - re-enabled `ResizeHandle`'s `.module.scss`
version with the fix, confirmed at the build level (deployed CSS now
reads `background-image:...` with a bare `var(--accentColorNormal)`, no
fallback). **Not yet re-confirmed in-game** - next test pass should
check the hover effect actually renders now.

**Lesson for any future `.module.scss` work in this codebase**: never use
`var()` inside a shorthand property, and never pass a `var()` fallback
argument - cohtml/Gameface silently drops the whole declaration if you do,
with only a log warning (not a build error) to find it by.

Added `UI/src/shared/base.scss` as a small shared foundation (a spacing
scale, a `@mixin interactive-transition`) for future `.module.scss` files
to `@use`. Deliberately keeps colors OUT of it - they should come from the
game's own live CSS custom properties (`var(--panelColorNormal)`,
`var(--accentColorNormal)`, etc. - matching this repo's own `UI/types/
ui.d.ts` theme interfaces, and confirmed in real use by the mod studied)
so panels keep matching whatever skin/theme the player has selected,
rather than a palette hardcoded at build time.

### Draggable Panel abandoned for good - hand-rolled drag instead, like every other third-party CS2 mod (2026-10-08/09)
Follow-up to the entry below: researched whether `draggable` Panel could
be made to work (the `PortalContainerProvider` theory) before trying
again. Verdict, not just a guess: `DraggablePanelProps` is `{
initialPosition?: Number2 }` and nothing else - no `onDrag`/`onMove`/
`onPositionChanged` of any kind, confirmed directly from `UI/types/ui.d.ts`.
It's write-once on mount; a dragged position can never be read back out,
which would make it useless for this mod's purposes even if the HUD bug
weren't a factor. Checked three real, shipped third-party CS2 mods that
register panels via the same `moduleRegistry.append('Game', ...)` pattern
this mod uses - MeasureItCS2, City Watchdog, HallOfFame - by cloning and
reading their actual source, not just their descriptions. All three
independently hand-roll both move and resize with plain `mousedown`/
`mousemove`/`mouseup` listeners on a positioning wrapper `div` around a
plain (non-`draggable`) `Panel`, and none references
`PortalContainerProvider`/`usePortalContainer` anywhere. MeasureItCS2's own
code comment gives the same "never reports where it ended up" reason for
rejecting `draggable`.

Implemented the same way: `useDraggable`/`DraggableHeader` (new, alongside
the existing `useResizable`/`ResizeHandle`) in `UI/src/shared/`, both
hooked into all four panels. Each panel is now a `position:absolute`
wrapper `div` (driven by `useDraggable`'s state) around a plain `Panel`
sized by `useResizable`; dragging starts only from the header strip
(`DraggableHeader`), not the whole panel, matching all three mods checked
and ordinary window convention. Not yet confirmed in-game as of this
writing - next test pass should check both the HUD stays intact (the
original symptom) and that drag/resize actually work as intended.

### Draggable Panel broke the game's own HUD - reverted, not yet root-caused (2026-10-08)
Tried making all four panels draggable (`cs2/ui`'s first-party `draggable`/
`initialPosition` props, confirmed real via `UI/types/ui.d.ts`) and
resizable (hand-built corner handle, no first-party support exists for
that). In-game: the game's own bottom ribbon moved to mid-screen
immediately on load, with no exception in `ZoneBuilder.Mod.log` or
`UI.log` - a silent layout side effect, not a crash. First fix attempt
(removing a speculative `position: "relative"` override added alongside
the resize handle) fixed the ribbon but then our own panels stopped
rendering at all. Reverted all four panels to the known-good manual
`style={{position:"absolute", top, left}}` positioning rather than keep
guessing live while blocking the user's HUD.

**Not actually root-caused** - this is a real open question, not a dead
end: `ui.d.ts` also declares `Portal`/`usePortalContainer`/
`PortalContainerProvider`, which the vanilla game likely wraps its own
draggable panels in somewhere up the tree; `moduleRegistry.append('Game',
...)` may bypass that entirely, leaving `draggable` Panel without a
container it expects. Worth testing explicitly (wrap our root component in
`PortalContainerProvider`) before trying `draggable` again. The shared
`useResizable`/`ResizeHandle` (`UI/src/shared/`) are left in the tree,
unused - the resize interaction itself was never actually confirmed broken
*or* working, since the drag regression blocked testing it at all.
`MeasureItCS2` (found via the icon-library research agent) has a more
complete hand-built drag/resize implementation worth reading before a
second attempt, including a buffered-position pattern to avoid snap-back.

### A zone's base type is chosen at creation, and fixed afterward (2026-10-08)
Zone creation originally hardcoded every new zone to `ResidentialLow` (a
deliberate stopgap, not a decision - "for now they can always show up in
residential" per the user). Replaced with a real picker: `ZonePanel` now
has a `FilterDropdown` of all seven `Core.ZoneType` values, and
`ZoneUISystem.OnCreateZone` takes the choice as a third parameter.

Deliberately NOT made editable after creation, for a real structural
reason, not just because it was easier: `CustomZoneBuilder.BuildOrRebuild`
finds-or-clones its zone by a stable internal name derived from
`definition.Id`, not `BaseZoneType` - so changing `BaseZoneType` later
wouldn't re-clone a different vanilla zone template. It would just change
which buildings the new eligibility filter (see next entry) accepts, while
the zone itself silently kept whatever density/look it was first cloned
as - a real mismatch bug, not a theoretical one. This also matches vanilla
CS2 itself: there's no "convert this zone to a different density" action
there either.

This surfaced a genuine architecture question from the user: if a
Collection holds Low+Medium+High residential buildings, does one zone
choose among all of them, pulling equally from each demand bar? **No** -
and can't, by the game's own design. Each `ZonePrefab` has exactly one
`ZoneData.m_AreaType`/density (see "Demand is shared with vanilla, by
design" below) - Low/Medium/High are three genuinely separate demand bars
in vanilla CS2, not one bar with a sub-dial. A single `CustomZoneDefinition`
can only ever target one of them. The supported way to get "one curated
building set, all densities": create three zones (one per density) and
attach the *same* Collection(s) to all three - the per-zone eligibility
filter below means each zone automatically pulls out just its own tier
from the shared pool. Not a limitation of this mod specifically; it's
how the simulation itself is structured.

### A collection's buildings aren't filtered by the zone's base type (found and fixed 2026-10-08)
Found via real in-game testing, not guessed: a "Main Zone" (`BaseZoneType =
ResidentialLow`) with two attached collections spawned a
`CN_BonusResidentialMedium01` building, and painting it drew down Low
demand while Medium-density buildings came out of the ground. Root-caused
by reading the actual persisted data (`customZones.json`/`collections.json`
on disk, not just the summary log line) rather than assuming: the two
attached collections held mostly non-Low buildings (one
`ResidentialMedium`, several `Office`, several `NA_MixedLeft01` mixed-use)
and exactly one real `ResidentialLow` building. `CustomZoneBuilder` was
cloning and offering *every* resolved entry regardless of whether its own
`ZoneType` matched the zone's `BaseZoneType` - nothing ever checked this,
because `Collection` is a deliberately zone-type-agnostic bag (see "Use
collections as the shared abstraction" above) and the building picker that
adds entries to one has no reason to know which zone(s) it'll end up
feeding. Fixed by checking `candidate.ZoneType != definition.BaseZoneType`
in `BuildOrRebuild` for both entry kinds - silently excluded for
`VanillaSelectorEntry` (a selector can resolve many candidates; mismatches
there are the expected common case for a collection shared across
zones of different types), warned about for `ExplicitAssetEntry` (a direct,
one-off user pick, not a broad filter). Already-cloned mismatched buildings
retire automatically on the next rebuild of that zone (same additive
rebuild/retire mechanism as removing an asset - see "Rebuilding a custom
zone is additive" above), not immediately - the user needs to trigger any
rebuild (attach/detach a collection, or rename) to see it take effect on a
zone that existed before this fix.

**This did not fully explain the "same building every time" observation**:
after this fix, "Main Zone" still has exactly one real `ResidentialLow`
candidate across both its attached collections, so it'll keep offering
that same single building until the user adds more Low-density buildings
to a collection feeding it - this is now understood to be a data/content
gap in the test collections, not a randomness bug in the game's own
building-selection logic, but that reasoning hasn't been independently
confirmed by reading `ZoneSpawnSystem.SelectBuilding`'s own code - revisit
if a zone with multiple same-type, same-level, same-lot-size candidates
still only ever offers one.

### Buffer gaps found via real `Collection` orchestration (2026-10-08, historical)
Before the timing root cause above was found, the first end-to-end proof
(one hardcoded building, via the now-deleted workaround) didn't generalize:
once `GameBuildingCatalog`/`CustomZoneBuilder` resolved real `Collection`
data against ~416 vanilla buildings, two more gaps turned up in the
workaround's manual copying - `Game.Prefabs.SubMesh` (the renderable mesh
reference; empty meant nothing to render at all) and `Game.Prefabs.SubObject`
(the prefab-side decorative-sub-object list read by
`Game.Objects.SubObjectSystem`; empty meant no fences/chimneys/etc. ever
spawned). A third bug - lot fences coming out partial/stopping halfway -
turned out to be copy *ordering*, not missing content: fence placement
reads the owner's `ObjectGeometryData` at placement time, which the
workaround was copying too late. All three are moot now that the real
systems run on their own, but the underlying facts remain useful context:
`SubMesh`/`SubObject` are genuine `DynamicBuffer<T>` components on a
building prefab, and fence coverage genuinely does depend on
`ObjectGeometryData` being correct by the time anything places them. Varied
fence *style* per lot (not coverage) is expected vanilla behavior, confirmed
by comparing a clone against a vanilla building of the same type.

### Signature buildings are excluded from the catalog, not just uncloneable
The 12 vanilla `...Signature01/02/03`/`...WaterfrontSignature0N` buildings
(both EU and NA residential-low themes) were resolving as ordinary
`VanillaSelectorEntry` candidates, then failing to clone -
`SpawnableBuildingZoneLinker` correctly requires a `SpawnableBuilding`
component on the prefab, and Signature buildings carry `SignatureBuilding`
instead (confirmed in `Game.Prefabs.SignatureBuilding.cs`); it sets
`SpawnableBuildingData` directly in its own `LateInitialize`, which is why
they passed `GameBuildingCatalog`'s `EntityQuery` (keyed on that component)
despite not actually being regular growables. Signature buildings are
one-per-city unique landmarks with their own unlock/leveling model
(`PlacementFlags.Unique`, fixed `m_Level = 5`) placed as rewards, not grown
- they should never spawn in a zone regardless of whether cloning them
could be made to work. Fixed at the source: `GameBuildingCatalog.TryDescribeBuilding`
now rejects any entity with `SignatureBuildingData` before it's ever
presented as a candidate, rather than letting it surface and fail later.

### Demand is shared with vanilla, by design
`ResidentialDemandSystem`/`CommercialDemandSystem`/`IndustrialDemandSystem`
bucket demand only by `AreaType` + `ZoneDensity` (derived from
`ZonePropertiesData`, not zone identity). A custom zone cloned from vanilla
Res Low competes in the *same* Res Low demand pool as vanilla Res Low -
confirmed as the desired end state, not just an accepted limitation: a
custom zone is meant to be "vanilla demand, restricted building list," not a
parallel economy with its own demand identity. No work needed to isolate
it - this is free, not deferred.

### Compatibility with Plop the Growables
District Themes only filter naturally spawned buildings and never remove
manually placed ones. Test alongside Plop the Growables, since both touch
zone checks and spawning.

### A collection with no Level 1 building can never spawn anything (2026-10-11)
User report: "Lowplusmedium" had plenty of Residential Low demand, a
collection attached, and painted cells - but nothing ever spawned, while
vanilla zones worked fine. Not a code bug: confirmed via decompiled
source (`Game.Simulation.ZoneSpawnSystem.SelectBuilding`,
`if (nativeArray3[j].m_Level != 1) continue;`) that an *initial* spawn on
an empty lot only ever considers Level 1 candidates - level-up
(`BuildingUpkeepSystem`) only applies to a building that's already
standing, so it can never be the thing that bootstraps an empty lot.
"Lowplusmedium"'s collection ("Low plus medium", see `collections.json`)
turned out to have zero Level 1 entries among its Residential Low
buildings - every one was L2-L5. Confirmed by having the user paint a
*different* zone ("Low," whose collection has two real L1 entries) -
it spawned correctly. This is the same underlying mechanism as
"Leveling gaps" below, just the more severe end of it: not "stuck
mid-level-up," but "can never start." No code fix needed here; this is
exactly the kind of content gap `FallbackPolicy`/a future UI warning
could help a user catch (see "Leveling gaps" below) - worth revisiting
once that's designed, e.g. warning when a zone's attached collections
have no Level 1 candidate for its base type at all.

### Platter (third-party mod) never picks up our custom zones (2026-10-11)
User report, confirmed real after ruling out a code bug on our side via
a full restart (which should have forced Platter's own unconditional
full re-cache to see the zone, and still didn't). Decompiled `Platter.dll`
(same ilspycmd technique used on `Game.dll`) to find the actual
mechanism rather than guessing:
- `Platter.Systems.P_ZoneCacheSystem.CacheZonePrefabs` builds its zone
  picker from `ZoneData`+`PrefabData` entities, excluding any without a
  `UIObjectData` component or whose `UIObject.m_Group` isn't a
  `UIAssetCategoryPrefab`. Added temporary diagnostic logging in
  `CustomZoneBuilder.BuildOrRebuild` to check both directly against our
  own cloned zone - both passed (`HasComponent<UIObjectData>=True`,
  group type `Game.Prefabs.UIAssetCategoryPrefab`), ruling this out.
- `P_ZoneCacheSystem.OnCreate` calls `RequireAnyForUpdate` **twice**,
  each with a single query (`m_ZonesModifiedQuery` - zone
  Created/Updated/Deleted - and separately `m_UIAssetCategoryPrefabCreatedQuery`).
  Multiple `RequireAnyForUpdate` calls AND together (confirmed
  empirically, not just inferred from the API shape - see below): its
  reactive `OnUpdate` only ever runs when *both* are simultaneously
  non-empty in the same frame. Our zone-only creates/rebuilds never
  touch a `UIAssetCategoryData`-tagged entity, so that condition is
  essentially never satisfied by anything we do.
- Confirmed empirically, not just reasoned from the IL: added a second
  temporary diagnostic (a direct compile-time reference to `Platter.dll`,
  reading its public `P_ZoneCacheSystem.ZoneUIData`/`CacheVersion`
  properties straight from our own `BuildOrRebuild`). Across this whole
  session - multiple zone creates, two full rebuild-on-load cycles, and
  a detach/reattach that settled a zone's real `ZoneData.m_ZoneType`
  index to a confirmed-correct, non-placeholder value - Platter's
  `CacheVersion` never moved off its initial value, and `ZoneUIData`
  never contained any of our zones, even looked up by the now-correct
  settled index. Its reactive cache genuinely never fires for us; it
  only ever gets populated by its own one-time `OnGameLoaded` full
  re-cache, which races against - and apparently loses to - our own
  `ZoneUISystem.OnGameLoaded` rebuild-on-load hook.
- Both temporary diagnostics (the extra logging in `BuildOrRebuild` and
  the `Platter` project reference in `ZoneBuilder.csproj`) were removed
  once this was confirmed - a real build-time dependency on another
  mod's DLL doesn't belong in this project, and the diagnostic call site
  would crash for any user who doesn't have Platter installed.

**Update (2026-10-11): fixed, via the fragile workaround - confirmed
working in-game, merged to `main`.** Tried for exactly the reason it was
initially passed over: real risk of touching shared vanilla entity state
we can't fully audit, explicitly scoped to a disposable branch first
(`experiment/platter-zone-nudge`) so it could be thrown away if anything
looked wrong. Tags the zone's UI group (e.g. "ZonesResidential") with
`Updated` right after a build, in the *same* tick (not delayed) -
confirmed via a temporary diagnostic query replicating Platter's own
shape that a one-tick delay (the first attempt) was the actual bug: a
zone's own `Created` tag is gone by the very next tick (stripped by
`CleanUpSystem`, `SystemUpdatePhase.Cleanup`, phase 31, the last phase of
every frame), so delaying the group's tag by a tick made it impossible
for `m_ZonesModifiedQuery` and `m_UIAssetCategoryPrefabCreatedQuery` to
ever be non-empty *simultaneously* - the exact condition
`RequireAnyForUpdate`-twice actually gates on. Tagging the group in the
same tick as the build (safe for this specific purpose - this nudge
never reads the zone's own settled state, and Platter's resulting
re-cache reads live data when it runs) closed that gap. Confirmed
in-game across several zone creates/rebuilds: Platter's zone picker now
shows our custom zones, no crash, no observed side effects on anything
else. Still not provably safe forever - the entity tagged is shared
vanilla infrastructure, and nothing rules out some other vanilla or
third-party system also watching the same generic `Updated` tag
reacting to it in a way this session's testing didn't happen to
exercise.

Deliberately isolated into its own file, `CustomZones/PlatterCompatibility.cs`
- a single static method, called from exactly one place
(`CustomZoneBuildRequestSystem.OnUpdate`), with no fields or other state
anywhere else in the codebase depending on it. If this needs to come out
- Platter ships its own fix, a game/Platter update changes the mechanics
it relies on, or a side effect turns up later - removal is: delete that
file, delete the one call site. The underlying gap was also reported
upstream to Platter's author - see its own GitHub issue for the current
status; if Platter ever fixes this on its own side, this workaround
becomes redundant and should be the first thing removed.

**Separately confirmed while investigating this**: `GameBuildingCatalog.TryMapToCoreZoneType`
crashed the whole `CustomZoneBuildRequestSystem.OnUpdate` with an
uncatchable `UnityEngine.Assertions.Assert` (`"Unknown Zone area
type:None"`) whenever our own zone-template search
(`GameZonePrefabResolver.TryFindVanillaZone`) happened to iterate past
Platter's own internal "PlatterUnzoned" `ZonePrefab` (`AreaType.None`)
before finding a real match - confirmed via `Game.Buildings.PropertyUtils.GetZoneDensity`'s
real source, which only handles Residential/Commercial/Industrial and
asserts on anything else instead of returning a sentinel. Non-deterministic
in practice (depends on unstable ECS entity iteration order), which is
why some zone creations crashed and others, created moments apart,
didn't. Fixed by checking `m_AreaType` before ever calling
`GetZoneDensity`, matching the intent the surrounding `switch`'s already-
correct `default: return false` case clearly had all along - this fix
is real and warranted regardless of the Platter investigation's outcome,
since any other mod (or future vanilla content) registering a
non-Residential/Commercial/Industrial `ZoneData` entity would trigger
the identical crash.

## Prior art reviewed

| Mod | Game | Relevance |
|---|---|---|
| Plop the Growables | CS2 | Manual placement anywhere, level lock. Doesn't control spawning. |
| Find It | CS2 | Asset browser; reference for picker UI. |
| Asset Packs Manager | CS2 | Pack management only; no spawning control. |
| Zone Tools / Zoning Toolkit | CS2 | Zone painting tools; no spawning control. |
| Vanilla 1.5.9 | CS2 | Asset pack filter in build menu (placement only, as far as known). |
| Building Themes / Building Themes 2 | CS1 | Direct precedent for district themes. |
| District Styles Plus | CS1 | "Restrict by zone only vs zone + level" option worth copying. |
| Ploppable RICO | CS1 | Precedent for per-asset growable settings (adopted assets). |

No existing CS2 mod found that provides per-district or per-zone building
selection (a Steam "Asset Selector for Zoning" request thread had no
existing-mod answers). Re-check the in-game Paradox Mods browser and the CS
Modding Discord before heavy investment.

## Compatibility watchlist

A player will never run this mod alone. This list tracks mods that touch
the same systems we do (zoning, building spawn/level-up, prefab
registration) and should be tested against before publishing - not a
prediction of actual conflicts, just scope for a pre-publish checklist.
Revisit seriously once District Themes' `ZoneSpawnSystem` adapter exists,
since that's the highest-risk touchpoint (see Open questions).

Researched via live Paradox Mods subscriber counts, October 2026 (see
agent research in session history). Sorted roughly by overlap risk, not
popularity.

| Mod | Why it's in scope | Popularity | Status |
|---|---|---|---|
| [Platter \[Beta\]](https://mods.paradoxplaza.com/mods/125278/Windows) ([source](https://github.com/lucarager/CS2-Platter)) | Not just a risk - a desired integration: players will want to build a custom zone here, then use Platter to stamp it as a reusable parcel. **Confirmed incompatible in-game (2026-10-11)**, contradicting the earlier source skim below - see "Platter never picks up our custom zones" above for the full investigation (decompiled, not guessed): Platter's reactive cache only updates when a `UIAssetCategoryData` entity *also* changes in the same frame, which our zone-only operations never trigger, and its one-time `OnGameLoaded` full re-cache races against our own load-time rebuild. A separate, unrelated crash (`AssertionException` on Platter's own internal "PlatterUnzoned" zone prefab) was found and fixed along the way, real regardless of the Platter question's outcome. **Update (2026-10-12)**: the `PlatterCompatibility` nudge fixed the create path - confirmed in-game, new custom zones now appear in Platter's list immediately. The same nudge on the *delete* path was tried, confirmed it fixed deleted zones lingering in Platter's list, but also confirmed to throw a nonfatal `ArgumentOutOfRangeException` in Platter's own `P_ZoneCacheSystem` every time (see "Platter nudge on delete reverted" above) - reverted. **Known, accepted gap: deleted zones still linger in Platter's own list until Platter's cache next refreshes on its own** (e.g. next load). | 136.5k subs, growing | **Partially compatible**: create confirmed working; delete has a known, accepted display-only gap (not pursued further - the fix requires changes on Platter's side, not ours) |
| [Zone Organizer](https://mods.paradoxplaza.com/mods/95965/Windows) | Splits residential zoning into new sub-categories (Low/Medium/High Density, Mixed Housing) - i.e. adds new zone types, same space as Custom Zones. | 267.2k subs | Untested |
| [Zoning Toolkit](https://mods.paradoxplaza.com/mods/75750/Windows) (retired by author, still widely installed) / [Zone Tools](https://mods.paradoxplaza.com/mods/128269/Windows) / [Easy Zoning](https://mods.paradoxplaza.com/mods/136261/Windows) (active successors, same lineage) | Per-road zoning-side control (left/right/both/none), affecting where buildings can spawn. | 115.4k / 64.6k / 18.3k subs | Untested |
| [Plop the Growables](https://mods.paradoxplaza.com/mods/75826/Windows) | Bypasses growable zone checks, disables despawn-on-rezone. Direct overlap with Custom Zones' spawn logic (see "Compatibility with Plop the Growables" above). | 446.6k subs | Untested |
| [Super Fast Building And Leveling](https://mods.paradoxplaza.com/mods/77321/Windows) | Patches construction/leveling speed directly - same neighborhood as District Themes' eventual `ZoneSpawnSystem` touchpoint. Explicitly recommends pairing with Plop the Growables. | 89.2k subs | Untested |
| [Realistic Population](https://thunderstore.io/c/cities-skylines-ii/p/WG/RealisticPopulation/) (Thunderstore only, appears unmaintained) | Harmony-patches household/job capacity per building level - leveling-adjacent simulation code. | ~7.8k downloads | Untested |
| [Better Bulldozer](https://mods.paradoxplaza.com/mods/75250/Windows) | Bypasses deletion restrictions on buildings/networks - adjacent to growable spawn/despawn logic, lower confidence of actual overlap than the rows above. | 864.6k subs | Untested |
| [Anarchy](https://mods.paradoxplaza.com/mods/74604/Windows) | Disables core placement/validation checks used across tools; not zoning-specific, but loaded so widely (likely on most test setups anyway) that it's worth a sanity check given any shared validation/Harmony patch points. | 1.1M subs | Untested |
| [Find It](https://mods.paradoxplaza.com/mods/77240/Windows) | Asset browser - reads `PrefabSystem`'s registered prefabs. We register through the standard `AddPrefab` API, so our cloned zones/buildings should just appear to it like any other prefab; confirmed it has no unusual registration hooks. | 755.6k subs | Untested, believed low-risk |
| [Zoning By-Law](https://github.com/FutureProg/ZoningByLawMod) (`Trejak.ZoningByLaw` - confirmed installed in the user's own test playset) | Different paradigm from Custom Zones, not a conflict: no new paintable zone color - instead a parallel ECS system, `ByLawZoneSpawnSystem : GameSystemBase`, that filters the *existing* building pool by player-defined rules (height, lot size/frontage min/max, land use, parking) at spawn time, queuing winners into `residentialQ`/`commercialQ`/`industrialQ` alongside vanilla `ZoneSpawnSystem.SpawnBuildingJob`. **Confirmed compatible in-game (2026-10-12)**: a lot painted with one of our custom zones consistently grows buildings from that zone's own collection, not something ByLaw's own selection picked instead. | Not researched (no public subscriber count found; real usage confirmed only via this user's own installed mod list) | **Confirmed compatible** |

Checked and deliberately excluded: Move It, Tree Controller, Traffic,
Advanced Line Tool, Recolor, Asset/Unified Icon Library,
ExtraLandscapingTools, ExtraAssetsImporter (decals/surfaces only, not
building prefabs despite the name), official region asset packs, and
InfiniteDemand (unmaintained since April 2024) - all popular, none touch
zoning, spawn, leveling, or prefab registration.

**Testing priority once there's something to test**: Platter is the first
thing to actually verify in-game - not because it's risky, but because the
source read suggests a genuine integration ("build a style here, Platter it
there") that's worth confirming works before assuming it does. Zone
Organizer and the Zoning Toolkit/Zone Tools/Easy Zoning lineage are still
the higher-risk-of-actual-conflict group, since they manipulate the same
zone-cell/zone-type space without the same generic-registration story
Platter and Find It share - worth the same source-reading treatment if
either turns out to matter. For District Themes, watch Super Fast Building
And Leveling and Realistic Population once that feature's `ZoneSpawnSystem`
adapter exists.

## Open questions (verify against game code)

- [x] Merge `experiment/platter-zone-nudge` to `main`? **Done (2026-10-11)** -
      merged after confirming in-game, isolated into its own file
      (`PlatterCompatibility.cs`) for easy removal, and reported upstream
      to Platter's author. See "Platter never picks up our custom zones."
- [x] Does demand computation include cloned zone prefabs, or only vanilla
      zones? **Resolved**: generic, keyed on `AreaType`/`ZoneDensity` only -
      see "Demand is shared with vanilla, not per-custom-zone" above.
- [x] Can zone and building prefabs be cloned and registered at runtime?
      **Resolved, yes** - see "Custom Zones needs no Harmony patches" above.
- [x] Do buildings level up in place or by prefab replacement? **Resolved**:
      prefab replacement, same Entity, same selection mechanism as initial
      spawn - see "Leveling gaps" below.
- [ ] Does CS2 load multiple DLLs from one mod folder? Partially looked into
      (`Game.Modding.ModManager` handles an arbitrary number of executable
      assets per mod, each independently gated by `isRequired`/`isMod`/
      `isUnique`/`canBeLoaded`), but those gates are defined in
      `Colossal.IO.AssetDatabase.ExecutableAsset`, which isn't in the
      current decompile (only `Game.dll` was decompiled, not
      `Colossal.IO.AssetDatabase.dll`). Still affects how module boundaries
      are enforced, but no longer blocking anything - Custom Zones'
      GameAdapters work as plain namespaces in one assembly regardless.
- [x] `ZonePrefabCloner`/`SpawnableBuildingZoneLinker` playtesting.
      **Resolved, fully confirmed in-game (2026-10-08)**: visible, named,
      correctly colored, paintable, spawns only the cloned building,
      correctly positioned, survives construction - see "A runtime-cloned
      prefab is never processed by any Created-tag-keyed system" above for
      what it actually took to get there (a workaround, not the clean
      zero-patches story originally hoped for).
- [ ] What happens to already-placed adopted buildings if the mod is
      removed?
- [ ] What do "unzoned" downloaded assets actually contain: buildings
      without spawnable data, plain static objects, or partial growables?
- [x] Interaction between themes and the EU/NA theme setting and asset
      packs. **Resolved (2026-10-08), empirically**: downloaded a real
      Content Creator Pack (China) and a real community pack (Detroit
      Bungalow Pack) specifically to test this. Both registered an
      `AssetPackElement`/`AssetPackPrefab` exactly like the Bridges & Ports
      DLC already did - this buffer isn't CO-curated-content-only, any
      named pack (official or community) uses it. Every building with zero
      `AssetPackElement` entries in that save was plain `NA_`/`EU_` vanilla.
      So `GameBuildingCatalog.ResolveAssetPackIds` now returns the real
      `AssetPackPrefab` name when present, falling back to a synthetic
      `"Vanilla (North America)"`/`"Vanilla (Europe)"` by name prefix, or
      plain `"Vanilla"` when there's no AssetPackElement and no region
      prefix - one mechanism covers DLC, official packs, and community
      packs; the vanilla fallback covers the rest. (First pass of the
      fallback returned nothing at all when there was no region prefix,
      which looked fine until actually retested in-game: 1082 buildings -
      every vanilla Office/Industrial building, e.g. `OfficeLow01_L1_3x4` -
      have no `NA_`/`EU_` variant at all, so they all fell through empty.
      Caught by rereading the real dump output, not assumed.)
      **One known gap, not yet seen**: a single Workshop building released
      without its own `AssetPackPrefab` (no formal "pack") would fall
      through to the vanilla-prefix check and, if its name doesn't start
      with `NA_`/`EU_` either, get zero asset-pack ids rather than being
      identified as community content. Not fixed since no such asset has
      been tested yet - revisit if the picker's "no asset pack" bucket ever
      turns out to contain non-vanilla content.
      Also checked and discarded as a signal: every building prefab's own
      `PrefabBase.asset?.GetMeta()` (`Colossal.IO.AssetDatabase.SourceMeta`)
      exposes `packageName`/`belongsToCurrentUser` - confirmed via live
      reflection on `Colossal.IO.AssetDatabase.dll` (not in the current
      Game.dll-only decompile) and confirmed compiling/running in-game, but
      the real data was inconsistent for this purpose: plenty of plain
      vanilla buildings had no backing `PrefabAsset` at all (`asset ==
      null`), others reported `packageName="Prefabs.cok"` with no way to
      tell that apart from a real pack's own `.cok` name without already
      knowing the mapping, and `belongsToCurrentUser` was false for every
      building including the downloaded community pack (it only means "the
      current Paradox account authored this," not "this was downloaded").
      `AssetPackElement` is the better-fitting, already-working signal;
      this mechanism isn't used by the real model.
- [ ] **Compatibility with other mods**: the Compatibility watchlist above
      is now populated with real, researched candidates (9 mods/lineages,
      sourced from live Paradox Mods data) - every row besides Platter is
      still "Untested" against a real conflict (does a zoning-grid-rework
      mod break anything, etc.). **Partial data point (2026-10-10)**: user
      loaded a real playset of ~40 code mods (Node Controller among them)
      alongside this one - no crash, no mod-loading errors from any mod.
      That's coexistence, not a row-by-row functional check - still worth
      doing individually for the rest, prioritizing the zoning-grid-rework
      group next. **Platter resolved (2026-10-11)**: confirmed
      incompatible (its reactive zone cache never fires for us) - see
      "Platter never picks up our custom zones" above.
- [ ] Fallback policy: `Core.FallbackPolicy` now models *that* it's
      configurable (`None` vs `VanillaZone`), but not what the default
      should be, whether it's set per-collection or per-district, or what
      the player-facing UI for it looks like.
- [ ] **Leveling gaps** (not decided, intentionally left open): building
      level-up (`BuildingUpkeepSystem.SelectSpawnableBuilding`) requires an
      exact match on zone + level + lot size + access flags among
      registered prefabs. A collection missing a level for some
      lot-size/access combination means affected buildings simply never
      level up past it - no error, no fallback, today. CS1's Building
      Themes has the identical limitation and never built an automatic
      fix: its answer was manual (a built-in "clone to another level"
      tool, or re-saving a duplicate via the asset editor at the missing
      level), plus a separate, coarser per-district "allow buildings not
      in any theme" checkbox closer to `FallbackPolicy` than to this
      specifically. Options on the table, none chosen: ship the silent cap
      as-is, extend `FallbackPolicy` to leveling (fall back to a vanilla
      building for just the missing level), or build a CS1-style manual
      gap-filling tool instead.
- [ ] Lot-matching rule `Core.CandidateFilter` currently assumes: building
      width must equal the lot's width exactly, depth only needs to fit.
      **Known wrong**: CS2 can subdivide a lot across multiple narrower
      buildings (e.g. a 4x6 lot filled by two 2x6 buildings side by side) -
      exact subdivision rule not yet investigated. Needs a dig into
      `ZoneSpawnSystem`/the Block-Cell subdivision logic in the decompiled
      source before `CandidateFilter` can be trusted for anything but
      single-building-per-lot cases.
- [ ] **Building picker "size" filter (2026-10-08)**: the full building
      picker (now the Assets tab/picking mode, see UI_OVERHAUL_PLAN.md)
      needs a "building size (low/med/
      high)" filter alongside zone type, lot width/depth, level, and asset
      pack. Not yet clarified whether this means the same thing as zone
      density (`Core.ZoneType`'s Low/Medium/High split, which the picker
      already filters by separately) or a distinct height/footprint tier
      (e.g. low-rise vs. high-rise) that could apply *within* one zone
      category, derived from `ObjectGeometryData.m_Size`/
      `BuildingData.m_LotSize` rather than stored directly. Deferred by
      user request - the Assets tab/picker is fully built now (zone/
      level/width/depth/pack filters all work), this is the one filter
      still not added. Revisit if it comes up again.

# Decisions & open questions

## Decisions

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
hook. Worth revisiting only as a *trigger* (a small "open CS2StyleMod"
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
from `CustomZoneDefinition.Id` (`CS2StyleMod.Zone.<guid>[.<template name>]`),
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
else - no exception anywhere in `CS2StyleMod.Mod.log`, just silently zero
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
`CS2StyleMod.Mod.log`) is where a render-time JS exception actually shows
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
BUILD level: webpack emits a real `CS2StyleMod.css` with correctly scoped
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
immediately on load, with no exception in `CS2StyleMod.Mod.log` or
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
  the `Platter` project reference in `CS2StyleMod.csproj`) were removed
  once this was confirmed - a real build-time dependency on another
  mod's DLL doesn't belong in this project, and the diagnostic call site
  would crash for any user who doesn't have Platter installed.

**Not fixed, and not clearly ours to fix**: this is an ordering race
between two independent mods' `OnGameLoaded` hooks, plus a reactive
cache gate on Platter's side that's narrower than "any zone change."
Neither mod controls the other's registration order. Options, none
taken: report upstream to Platter's author (the real fix - broaden
`RequireAnyForUpdate` to an actual OR, or re-run `CacheZonePrefabs` once
more a frame after load); do nothing further (documented limitation);
or a fragile workaround on our side (e.g. tagging a `UIAssetCategoryData`
entity `Updated` ourselves to force Platter's gate open, which depends
on Platter's exact internal query shape and could break on its next
update). Leaving this documented rather than building the fragile
option.

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
| [Platter \[Beta\]](https://mods.paradoxplaza.com/mods/125278/Windows) ([source](https://github.com/lucarager/CS2-Platter)) | Not just a risk - a desired integration: players will want to build a custom zone here, then use Platter to stamp it as a reusable parcel. **Confirmed incompatible in-game (2026-10-11)**, contradicting the earlier source skim below - see "Platter never picks up our custom zones" above for the full investigation (decompiled, not guessed): Platter's reactive cache only updates when a `UIAssetCategoryData` entity *also* changes in the same frame, which our zone-only operations never trigger, and its one-time `OnGameLoaded` full re-cache races against our own load-time rebuild. A separate, unrelated crash (`AssertionException` on Platter's own internal "PlatterUnzoned" zone prefab) was found and fixed along the way, real regardless of the Platter question's outcome. | 136.5k subs, growing | **Confirmed incompatible** (source-verified + reproduced in-game) |
| [Zone Organizer](https://mods.paradoxplaza.com/mods/95965/Windows) | Splits residential zoning into new sub-categories (Low/Medium/High Density, Mixed Housing) - i.e. adds new zone types, same space as Custom Zones. | 267.2k subs | Untested |
| [Zoning Toolkit](https://mods.paradoxplaza.com/mods/75750/Windows) (retired by author, still widely installed) / [Zone Tools](https://mods.paradoxplaza.com/mods/128269/Windows) / [Easy Zoning](https://mods.paradoxplaza.com/mods/136261/Windows) (active successors, same lineage) | Per-road zoning-side control (left/right/both/none), affecting where buildings can spawn. | 115.4k / 64.6k / 18.3k subs | Untested |
| [Plop the Growables](https://mods.paradoxplaza.com/mods/75826/Windows) | Bypasses growable zone checks, disables despawn-on-rezone. Direct overlap with Custom Zones' spawn logic (see "Compatibility with Plop the Growables" above). | 446.6k subs | Untested |
| [Super Fast Building And Leveling](https://mods.paradoxplaza.com/mods/77321/Windows) | Patches construction/leveling speed directly - same neighborhood as District Themes' eventual `ZoneSpawnSystem` touchpoint. Explicitly recommends pairing with Plop the Growables. | 89.2k subs | Untested |
| [Realistic Population](https://thunderstore.io/c/cities-skylines-ii/p/WG/RealisticPopulation/) (Thunderstore only, appears unmaintained) | Harmony-patches household/job capacity per building level - leveling-adjacent simulation code. | ~7.8k downloads | Untested |
| [Better Bulldozer](https://mods.paradoxplaza.com/mods/75250/Windows) | Bypasses deletion restrictions on buildings/networks - adjacent to growable spawn/despawn logic, lower confidence of actual overlap than the rows above. | 864.6k subs | Untested |
| [Anarchy](https://mods.paradoxplaza.com/mods/74604/Windows) | Disables core placement/validation checks used across tools; not zoning-specific, but loaded so widely (likely on most test setups anyway) that it's worth a sanity check given any shared validation/Harmony patch points. | 1.1M subs | Untested |
| [Find It](https://mods.paradoxplaza.com/mods/77240/Windows) | Asset browser - reads `PrefabSystem`'s registered prefabs. We register through the standard `AddPrefab` API, so our cloned zones/buildings should just appear to it like any other prefab; confirmed it has no unusual registration hooks. | 755.6k subs | Untested, believed low-risk |

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

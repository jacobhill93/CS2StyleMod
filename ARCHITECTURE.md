# Architecture & UI Overview

Zone Builder is a Cities: Skylines II mod: players define **Collections**
(curated sets of buildings) and **Custom Zones** (new zone types that only
spawn buildings from their attached Collections). This doc explains how
the mod is built, why a few non-obvious things are shaped the way they
are, and the durable constraints worth knowing before changing the code.

## Project layout

```
Mod.cs                   mod entry point
src/
  Core/                  Collections, zone definitions, candidate
                         filtering - plain C#, game-independent, unit
                         tested (tests/ZoneBuilder.Core.Tests)
  CustomZones/            depends on Core only
    GameAdapters/         every direct game-engine touchpoint
    Debug/                throwaway test harnesses (removed once each
                         served its purpose - see git history)
  DistrictThemes/        an empty stub (GameAdapters/README.md only) -
                         planned, never built. Not live code.
  UI/                    C# UI-binding systems (ExtendedUISystemBase),
                         one file per bound panel - talk to Core, never
                         to GameAdapters directly
UI/                       the TypeScript/React frontend - a separate
                         npm/webpack project (scaffolded from the
                         official create-csii-ui-mod template), built
                         as part of `dotnet build` (see
                         ZoneBuilder.csproj's BuildUI target)
  src/mods/ShellFeature/  the one registered UI shell - Zones/
                         Collections/Assets tabs
  src/shared/             cross-tab hooks/components
  src/vanilla/            typed accessors for reused base-game CSS/
                         components (getModule), one file per base-game
                         module actually consumed
```

**Why `GameAdapters/` exists:** the risk in this project is in the game
integration, not the UI - real UI work was deliberately deferred until
the backend (zone cloning, building spawning) was proven stable in-game.
Everything that reads or writes game-internal state goes through an
adapter; `CustomZones`' own logic talks to adapters, never to `Game.*`
types directly. This keeps the blast radius of a game-patch break
contained to one file.

## Core data model & persistence

- **`Collection`** - a named, reusable, zone-type-agnostic bag of
  building entries: `ExplicitAssetEntry` (one specific building) or
  `VanillaSelectorEntry` (a broader selector, e.g. "Residential Low, NA
  theme"). Referenced by GUID only, never copied - the same Collection
  can back many zones, and editing it changes every zone using it with
  no repainting needed.
- **`CustomZoneDefinition`** - a zone's own identity: `Id`, `Name`,
  `BaseZoneType`, `Region`, and the list of attached Collection GUIDs.
  `BaseZoneType`/`Region` are fixed at creation - the zone's internal
  template name is derived from `Id`, never from these, so changing them
  later wouldn't actually re-clone a different base-game template, just
  silently change eligibility while keeping the original density.
- **`Core.ZoneType`** - mirrors the base game's own zone categories:
  `ResidentialLow/Medium/High`, `CommercialLow/High`, `Office`,
  `Industrial`, `OfficeLow`, `OfficeHigh`. `OfficeLow`/`OfficeHigh` are
  appended at the end of the enum rather than placed next to `Office`
  deliberately - this enum is persisted by ordinal (Newtonsoft JSON, no
  `StringEnumConverter`), so inserting new members earlier would have
  silently corrupted any already-saved zone's `BaseZoneType`. Plain
  `Office` is kept, unused by new data, purely so an already-saved zone
  from before the Low/High split still deserializes correctly.
- **`Core.ZoneRegion`** - `NorthAmerica`/`Europe`. Purely which base-game
  theme/UI grouping a zone's clone displays under - not a different
  simulation profile.

**Storage**: one JSON file per Collection (`Collections/<Id>.json`) and
per zone definition (`Zones/<Id>.json`), not one combined file - each
write is atomic (write-then-replace), and a corrupted file only takes
down that one item (collected into a `LoadFailures` list), never the
whole library. This replaced an earlier single-combined-file design that
had a real failure mode: zero exception handling around `Load()` meant
one corrupted file crashed the entire mod before any UI system even
registered. A legacy migrator does the one-time upgrade from the old
format and renames (never deletes) the old file to `<name>.json.migrated`
as a recoverable trace. A separate boot-time importer
(`CollectionImporter`) scans `ModsData/ZoneBuilder/Imports/` for
hand-dropped collection files, always assigning a **fresh** GUID (never
the file's own stored id) to avoid collisions, and moves processed files
to `Imports/Processed/` so they aren't re-imported every launch.

`Save`/`Delete` on both libraries have real exception safety: a failed
disk write leaves in-memory state exactly as it was (an earlier version
updated memory *before* attempting the write, risking silent data loss
on failure), and `TryGet` returns a cloned copy, not the library's live
object reference, so a caller mutating what it got back can't corrupt
the library's own in-memory state before a save even happens.

**Schema migration**: every saved file carries a `SchemaVersion` integer.
On load, `SchemaMigration` (`src/Core/SchemaMigration.cs`) reads it and,
if it's behind `CurrentSchemaVersion`, runs the registered
version-to-version steps in order before the file is deserialized into
its current DTO shape - a renamed/restructured/retyped field can be
expressed as an explicit transform, rather than hoping a direct
deserialize into the current class happens to still work. A file
missing the `SchemaVersion` property entirely reads as version 0, never
silently assumed to already be current - if no migration step covers
the version actually found, loading that one file fails and is isolated
the same way a corrupted file already is, rather than risking a
half-correct load. No real migration has ever been needed yet (nothing
has changed shape since this was built), so each format's own step list
is currently empty - see `CollectionFileFormat`/
`CustomZoneDefinitionFileFormat`'s own `Migrations` field for where to
add one.

Each migration step is a plain JSON-string-in, JSON-string-out
transform, not a `Newtonsoft.Json.Linq.JObject`-based one, despite
`JObject` being the more natural way to express a generic rename -
`ZoneBuilder.csproj` (the net48, game-facing assembly) fails to compile
against `JObject` with a `CS1705` assembly-version mismatch against the
game's own bundled `Newtonsoft.Json.dll`. Plain
`JsonConvert.DeserializeObject<T>`/`SerializeObject` - the only two
members this mechanism actually uses - don't trigger it, matching every
other JSON call site in this codebase. A future migration step should
parse/rebuild its own JSON via a pair of purpose-built DTOs through
those same two methods, not `JObject`.

## The zone-building pipeline

A custom zone starts life as a **clone of one of the base game's own
`ZonePrefab`s** (`PrefabSystem.AddPrefab`/`PrefabBase.Clone` - the same
mechanism the game's own playset system and the editor's "duplicate
prefab" tool use), then becomes its own fully independent zone type -
distinctly paintable, with its own name, color, and building list, never
re-merged with or mistaken for the one it was cloned from. Building
eligibility needs **no Harmony patches**: the base game's own
`ZoneSpawnSystem` (spawn) and `BuildingUpkeepSystem` (level-up) - both
Burst-compiled and effectively unpatchable - already filter candidates
purely by matching a building prefab's `BuildingSpawnGroupData.m_ZoneType`
against the lot's zone. So cloning the zone and repointing each
Collection building's zone reference is sufficient; the base game's own
systems do the restricting for free.

### The one-frame timing constraint

This is the single most load-bearing fact in the codebase, and the
entire reason `CustomZoneBuildRequestSystem` exists as its own system
rather than being inlined wherever a zone needs building.

A runtime-cloned prefab's `Created`/`Updated` tags live for exactly one
frame. `PrefabSystem.OnUpdate` drives the `PrefabUpdate` phase (which
every base-game init system - `ZoneSystem.InitializeZonePrefabs`,
`PrefabInitializeSystem`, `ObjectInitializeSystem` - runs under) from a
fixed point in the game's `MainLoop`. `LoadGameSystem` (which dispatches
every system's `OnGameLoaded`) is registered in the *same* phase, but
*after* `PrefabSystem` - so a clone added from `OnGameLoaded`, or any
other arbitrary call site, always misses that frame's `PrefabUpdate`
pass. By next frame, `CleanUpSystem` has already stripped the tags, and
the clone is permanently invisible to every init system from then on -
no geometry, no `BuildingSpawnGroupData`, nothing.

Deferring by a few ticks doesn't help (tags are already gone);
re-invoking `UpdateSystem.Update(PrefabUpdate)` manually corrupts the
outer loop mid-iteration. The actual fix: a dedicated system registered
`UpdateBefore<_, PrefabSystem>(MainLoop)`, which only ever calls the
real cloning code from its own `OnUpdate` - landing immediately before
`PrefabSystem`'s own tick, every frame, guaranteed. That system is
`CustomZoneBuildRequestSystem`: a pending-request queue any caller can
enqueue into from anywhere, with correct timing baked into the
architecture rather than something every call site has to remember.

(The read side has an analogous trap: `UISystemBase` derives from
`GameSystemBase`, so `OnCreate` runs at world bootstrap *before* any
prefab is registered - querying prefab/ECS state there silently returns
nothing. Snapshot queries belong in `OnGameLoaded`.)

### Eligibility, rebuilds, and retirement

- A Collection's entries aren't inherently zone-type-filtered -
  `BuildOrRebuild` explicitly checks a candidate's zone type against the
  zone's `BaseZoneType`: silently excluded for a `VanillaSelectorEntry`
  (expected, a selector spans many types), warned for an
  `ExplicitAssetEntry` (a one-off pick, so a mismatch is likely a
  mistake).
- **Rebuilds are additive, never destructive.** Re-requesting the same
  `CustomZoneDefinition` finds prior work by a stable internal name
  (`ZoneBuilder.Zone.<guid>[.<template>]`, never derived from `Name` -
  this is what makes renames free). A newly-resolved building gets
  cloned; a building no longer resolved has its
  `BuildingSpawnGroupData` cleared so it stops being offered, but its
  prefab is never removed - anything already standing is unaffected.
- **Zones must be rebuilt on every fresh game load** - a
  `CustomZoneDefinition` persists as JSON, but its in-game `ZonePrefab`
  clone is live ECS state, never serialized into the save.
  `ZoneUISystem.OnGameLoaded` requests a rebuild for every known
  definition; safe because `BuildOrRebuild` is explicitly idempotent.
- **Retirement** is `PrefabSystem.RemovePrefab(zone)` (adds `Deleted`,
  which `UIInitializeSystem` watches and auto-removes from UI lists) -
  five other mechanisms were tried first and confirmed to have zero
  effect. Routed through the same request queue as a build, for the
  same guaranteed-safe timing. **Retirement never touches a zone's
  buildings** - that was tried once and reverted after it caused real
  in-game save corruption (an already-placed building references its
  prefab by name; removing the prefab leaves nothing to resolve).
  Orphaned building-prefab accumulation across create/retire cycles is
  an accepted minor cost.
- **Large collections need chunking.** A synchronous loop over ~350
  entries - either the clone loop or the retire loop's
  `EntityManager.SetSharedComponent` calls, a structural ECS change -
  is a real native-crash risk in this engine, not just a perf concern,
  invisible at small scale and catastrophic at realistic scale (a
  349-entry collection once force-killed the game). Both loops are
  chunked C# iterators advancing a few entries per tick via
  `CustomZoneBuildRequestSystem.OnUpdate`. Building-name lookups
  (`GameBuildingCatalog`) are similarly index-cached, not linearly
  scanned - a modded playset can have tens of thousands of registered
  prefabs.
- Demand is shared with the base game **by design** - demand systems
  bucket purely by `AreaType`+density, not zone identity, so a custom
  zone competes in the same demand pool as every base-game zone rather
  than running a parallel economy.

### Known limitations

- A zone can only spawn buildings fresh on an **empty lot** at Level 1
  (the base game's own spawn selection only considers Level 1 for a
  fresh spawn; level-up only applies to an already-standing building) - a
  Collection with no Level 1 entry for a zone type can never bootstrap
  there from scratch.
- A Collection missing a building for some lot-size/access combination
  at a given level means affected buildings simply never level up past
  it, with no fallback - unresolved, same limitation CS1's Building
  Themes had.
- Lot-matching assumes a building's width must equal the lot's width
  exactly (only depth needs to fit) - CS2 can actually subdivide a lot
  across multiple narrower buildings side by side; that subdivision
  rule was never investigated here.

## Compatibility

- **Platter** (zone-stamping mod) - confirmed genuinely incompatible
  without a workaround: its own cache system only reactively refreshes
  under a condition pure zone operations never trigger. Fixed via
  `CustomZones/PlatterCompatibility.cs`, a single static call from
  `CustomZoneBuildRequestSystem.OnUpdate` that nudges Platter's cache in
  the same tick a zone is built. Confirmed working for zone creation.
  The equivalent nudge for zone deletion was tried, fixed Platter's
  symptom, but triggered a separate exception inside Platter's own code
  (a real gap in Platter's own `Deleted`-entity handling) - reverted.
  Accepted, documented gap: a deleted zone lingers in Platter's list
  until Platter's own cache next refreshes on its own.
- **Zoning By-Law** - confirmed compatible in-game.
- **Find It** - believed low-risk (reads registered prefabs generically,
  no unusual hooks); this mod's clones appear as normal, independent,
  self-labeled entries with no dedup confusion.
- Broader third-party mod testing (Zone Organizer, Plop the Growables,
  Realistic Population, Anarchy, and others) is ongoing, not
  exhaustively covered here.

## UI architecture

The shell (`ShellFeature.tsx`) is one fixed-position `Panel` with a
client-side Zones/Collections/Assets tab bar, replacing an earlier
design of four independent always-visible floating panels. Each tab
consumes bindings from the matching C# system
(`ZoneUISystem`/`CollectionUISystem`/`BuildingPickerUISystem`) via
`ExtendedUISystemBase` (adapted from Road Builder, MIT-licensed - see
`THIRD_PARTY_NOTICES.md`), which wraps the game's own
`ValueBinding`/`TriggerBinding` plumbing behind a `CreateBinding`/
`CreateTrigger` API.

### Durable engine constraints (read before writing new UI)

Coherent Gameface (the UI engine CS2 embeds) has several real, confirmed
quirks worth not re-discovering:

- **Mod panels share one render tree with the base game's own HUD, with
  no error boundary.** An uncaught exception anywhere in a mod's own
  panel can take down the *entire* game HUD, not just that panel - confirmed three
  separate times in this codebase (a raw HTML `<select>`, a draggable
  `Panel`, and a first attempt at list virtualization each did this).
  `UI.log` (not the mod's own log file) is where the actual stack trace
  shows up.
- **No native `<select>`.** Use `cs2/ui`'s `Dropdown`/`DropdownToggle`/
  `DropdownItem` instead.
- **`cs2/ui`'s draggable `Panel` is unusable** - `DraggablePanelProps`
  only accepts a write-once initial position, with no way to read a
  dragged position back out, and using it broke the game's own HUD
  layout with no logged error. Every real third-party mod checked
  hand-rolls drag/resize with plain mouse listeners instead.
- **Icon+text `Button` combos render broken.** Icon-only (`variant="icon"`
  + `tooltipLabel`) is the confirmed-working pattern.
- **`var()` custom properties only work in longhand CSS properties**,
  never shorthand (`background`, `border`, `transition`, ...), and have
  no fallback-argument support - violating either silently drops the
  whole declaration, logged only to `UI.log`, never a build error.
- **A plain `<input placeholder="...">`'s native `placeholder` doesn't
  render** - it silently shows as a blank box. `shared/TextInput.tsx`
  overlays a `<span>` instead, shown only while the input is empty.
- **A plain space between a JSX expression and adjacent text can render
  with no visible gap** (e.g. `{count} shown`), even where it's not a
  real JSX whitespace-trimming issue. Force it with an explicit `{" "}`
  text node.
- **A stretched flex item's height: 100% does not reliably resolve the
  way it would in a real browser** - confirmed to blow up to something
  far larger than intended (and reportedly destabilized the game) rather
  than just filling its flex-stretched container. Use an explicit fixed
  height instead, or center a naturally-sized child within the stretched
  parent via `align-items: center`.
- **A library component's documented `theme` prop may *replace* its
  whole default theme object rather than merge with it.** Passing a
  bare partial `theme` to `cs2/ui`'s `Dropdown` broke option selection
  entirely - it silently dropped keys (`scrollable`, `dropdownItem`)
  the partial object never mentioned, apparently breaking whatever
  wiring depended on them. The safe pattern: `getModule` the real
  default theme object (same `vanilla/*.ts` pattern as below), spread
  it, and only override the specific key actually being changed.
- **Reusing the base game's own CSS classes/components via `getModule`
  works, but verify every module path and class against the actually-
  installed game** - grep `Content/Game/UI/index.js`/`index.css`, or use
  UI developer mode's `localhost:9444` devtools. A reference mod's claim
  about a module path, or a type declaration's shape, is not proof by
  itself - confirmed wrong at least twice in this codebase (a base-game
  tile class assumed to work for list rows, a `ScrollController` type
  that broke the whole HUD the moment it was actually instantiated).

### Base-game component/CSS reuse

A handful of `UI/src/vanilla/*.ts` files each export one `getModule`
result - one real consumer earns one export, not a wholesale resolver
file:

- **`TextInputTheme.ts`** - base-game text-input look, applied to every
  plain `<input>` alongside this mod's own `textInputBasic` SCSS mixin.
- **`EllipsisTextInput.ts`** - the real base-game rename-field component
  (focus/select-on-open, Enter/blur-away commit all work correctly via
  a plain forwarded ref). Known, accepted limitation: its internal
  focus-driven "Back" hint consumes Escape as its own exit gesture,
  which still ends up committing rather than canceling - two separate
  fix attempts had zero effect; accepted as-is.
- **`AssetGridTheme.ts`** - the real tile/row/thumbnail classes behind
  the Assets tab's grid view. An earlier attempt reused this same
  module's `item` class for ordinary list rows instead - reverted after
  it rendered everything as smushed squares, since that class is a
  fixed-size, centered-content tile, not row styling. The tile-grid use
  is correct; don't reach for it for row styling again.
- **`DropdownTheme.ts`** - the real default `Dropdown` theme object
  (see the `theme` prop pitfall above), composed with one additional
  override class to recolor the popup background without dropping any
  of cs2/ui's own default wiring.

### Hand-styled chrome

Not every piece of UI needs base-game-verification first. Where the
base game gives *working behavior* worth not reimplementing (an input's
focus handling, a tile's real DOM shape), verify against the real game
CSS/JS before trusting it. Where it's pure visual styling with no hidden
behavior at stake - button shape, dropdown chrome, card-style list rows
- this codebase hand-styles and corrects via real in-game screenshots
instead, the same way `cs2/ui`'s default pill-shaped `Button` was
flattened to match the rest of the shell's chrome (`base.scss`'s
`flatButtonChrome` mixin, layered over the library's own classes via a
plain additional `className`, never replacing them).

### Minimal failure surfacing

A failed Collection/zone save or delete (disk full, file locked,
permissions denied) used to only reach the log file - invisible to the
player. `UIErrorState` (a small static holder shared by
`CollectionUISystem`/`ZoneUISystem`) now captures the same message text
already sent to the log and surfaces it as one dismissible banner above
the shell's tab bar, via one binding/trigger pair.

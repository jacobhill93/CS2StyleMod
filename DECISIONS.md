# Decisions & open questions

## Decisions

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
| [Platter \[Beta\]](https://mods.paradoxplaza.com/mods/125278/Windows) ([source](https://github.com/lucarager/CS2-Platter)) | Not just a risk - a desired integration: players will want to build a custom zone here, then use Platter to stamp it as a reusable parcel. Checked its actual source (`P_ZoneCacheSystem.cs`): it builds its zone picker from *every* entity with `ZoneData + PrefabData`, same generic query the game itself uses - not a hardcoded vanilla list - gated only on the prefab having a `UIObjectData` component. Since `ZonePrefabCloner` clones the whole vanilla template (UI category/icon included), our custom zones should appear in Platter's picker for free, same mechanism as Find It. **Not yet confirmed in-game.** | 136.5k subs, growing | Likely compatible (source-verified), untested in-game |
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
      sourced from live Paradox Mods data) - but every row is still
      "Untested." Nothing has actually been checked for a real conflict
      yet. Revisit once there's a build worth installing alongside them,
      prioritizing the zoning-grid-rework group first.
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
      picker (UI feature, in progress) needs a "building size (low/med/
      high)" filter alongside zone type, lot width/depth, level, and asset
      pack. Not yet clarified whether this means the same thing as zone
      density (`Core.ZoneType`'s Low/Medium/High split, which the picker
      already filters by separately) or a distinct height/footprint tier
      (e.g. low-rise vs. high-rise) that could apply *within* one zone
      category, derived from `ObjectGeometryData.m_Size`/
      `BuildingData.m_LotSize` rather than stored directly. Deferred by
      user request - revisit once the rest of the picker is built.

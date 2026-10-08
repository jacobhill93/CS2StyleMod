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
- [ ] Interaction between themes and the EU/NA theme setting and asset
      packs.
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

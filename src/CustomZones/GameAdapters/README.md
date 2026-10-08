# CustomZones/GameAdapters

Every game touchpoint the Custom Zones feature depends on, one file per
touchpoint. Feature logic talks only to these adapters, never to game types
directly. Each adapter has an entry in `../../../TOUCHPOINTS.md`.

- `ZonePrefabCloner.cs` — clones a vanilla (or other custom) zone prefab to
  give a new custom zone its simulation profile.
- `SpawnableBuildingZoneLinker.cs` — clones a building prefab and repoints
  it at a custom zone, so vanilla's own spawn/level-up selection picks it up
  automatically.

That's genuinely all it takes - no further manual initialization needed, as
long as `PrefabSystem.AddPrefab` ends up getting called at the right point
in the frame. It used to look like a cloned prefab was never initialized at
all, root cause unknown, requiring a whole third adapter
(`ManualPrefabInitializationWorkaround.cs`) to hand-replicate what the real
systems should have done. Root cause found 2026-10-08 (see DECISIONS.md "A
runtime-cloned prefab is only visible to init systems for one frame"): it
was pure frame ordering, not a missing initialization step. That file is
deleted now - see `../CustomZoneBuildRequestSystem.cs` instead, which owns
the actual timing requirement (`AddPrefab` must happen from a system's
`OnUpdate` registered `UpdateBefore<_, PrefabSystem>(SystemUpdatePhase.MainLoop)`,
never from `OnGameLoaded` or any other arbitrary call site) so nothing else
has to remember it.

**Confirmed in-game (2026-10-08)**, both adapters together, called the
correct way: a cloned zone is visible, named, correctly colored, paintable,
and spawns only the cloned building, correctly positioned, surviving
construction - every single field (geometry, the `SubMesh`/`SubObject`
buffers, `BuildingSpawnGroupData`) populated by the real game systems, zero
manual copying. See TOUCHPOINTS.md for the full verification history and
DECISIONS.md for the confirmed root cause and fix.

- `GameBuildingCatalog.cs` — real `Core.IBuildingCatalog`. Resolves a
  `Collection`'s entries (vanilla selector / explicit asset) against the
  live game's registered buildings. Also exposes `TryGetBuildingPrefab`
  (not part of `IBuildingCatalog` - Core only deals in plain
  `BuildingCandidate` values) for feature logic that needs the actual
  prefab to clone, not just a description of it, and
  `FindBuildingsByNamePrefix` (used by the rebuild path below to find what
  it already cloned for a given zone). Excludes Signature buildings
  (one-per-city landmarks, not regular growables) from every result.
- `GameZonePrefabResolver.cs` — resolves `ZonePrefab`s: either a vanilla
  zone matching a `Core.ZoneType` category (to clone as a new custom zone's
  simulation template), or an already-registered zone by its exact internal
  name (to find a zone `CustomZoneBuilder` built in an earlier request, for
  rebuild). Reuses `GameBuildingCatalog`'s `AreaType`/density mapping
  (`internal`, not duplicated) rather than guessing the inverse direction
  itself.

`../CustomZoneBuilder.cs` (one level up - feature logic, not a
touchpoint-specific adapter) is what actually uses these: given a
`Core.CustomZoneDefinition` + its referenced `Collection`s, `BuildOrRebuild`
resolves every entry through `GameBuildingCatalog` and clones the zone and
each distinct building via `ZonePrefabCloner`/`SpawnableBuildingZoneLinker`.
Adopted assets are skipped with a warning - they need their own GameAdapter
to give an arbitrary building growable data first, not yet built.
`BuildOrRebuild` must only ever be called from
`../CustomZoneBuildRequestSystem.cs`'s own `OnUpdate` - never directly -
for the same timing reason as above.

**Rebuild-safe by construction (2026-10-08)**: every zone/building prefab
this builds gets a name derived from `CustomZoneDefinition.Id`
(`CS2StyleMod.Zone.<guid>[.<template prefab name>]`), never from its
user-facing `Name` - so calling `BuildOrRebuild` again for the same
definition (e.g. after editing one of its Collections) finds what it built
before by that stable name instead of creating a duplicate. A building
newly resolved that wasn't cloned before gets cloned (additive); one that
was cloned before but isn't resolved anymore gets its
`BuildingSpawnGroupData` cleared (zone index 0 - never a real zone) so it
stops being offered for new construction, without touching the entity or
anything already built from it - see DECISIONS.md "Removing an asset
doesn't touch what's already built."

**Confirmed in-game (2026-10-08)**, both the original build path (real
`Collection` data resolving the full ~416-building vanilla selector plus an
explicit asset - every building renders, sub-objects spawn correctly,
Signature buildings correctly excluded) and the rebuild path (requesting
the same definition three times in one load: fresh build, additive
rebuild adding the full selector, then a rebuild dropping the explicit
asset - confirmed via the zone showing variety and the dropped asset never
appearing in freshly-grown buildings). See TOUCHPOINTS.md/DECISIONS.md.

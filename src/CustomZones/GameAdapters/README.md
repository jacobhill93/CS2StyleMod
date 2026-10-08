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
  prefab to clone, not just a description of it. Excludes Signature
  buildings (one-per-city landmarks, not regular growables) from every
  result.

`../CustomZoneBuilder.cs` (one level up - feature logic, not a
touchpoint-specific adapter) is what actually uses this: given a vanilla
zone + a name + some `Collection`s, it resolves every entry through the
catalog above and clones the zone and each distinct building via the two
adapters in this folder. Adopted assets are skipped with a warning - they
need their own GameAdapter to give an arbitrary building growable data
first, not yet built. `Build()` must only ever be called from
`../CustomZoneBuildRequestSystem.cs`'s own `OnUpdate` - never directly -
for the same timing reason as above.

**Confirmed in-game (2026-10-08)**, with real `Collection` data resolving
the full ~416-building vanilla selector plus an explicit asset: every
building renders, their sub-objects (fences, chimneys, driveway props,
etc.) spawn correctly, and the 12 vanilla Signature buildings are
correctly excluded rather than surfacing as clone failures. Re-confirmed
again after `ManualPrefabInitializationWorkaround` was deleted and
`CustomZoneBuildRequestSystem` took over - identical results, zero manual
reinitialization. See TOUCHPOINTS.md/DECISIONS.md.

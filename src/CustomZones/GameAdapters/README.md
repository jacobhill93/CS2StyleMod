# CustomZones/GameAdapters

Every game touchpoint the Custom Zones feature depends on, one file per
touchpoint. Feature logic talks only to these adapters, never to game types
directly. Each adapter has an entry in `../../../TOUCHPOINTS.md`.

- `ZonePrefabCloner.cs` — clones a vanilla (or other custom) zone prefab to
  give a new custom zone its simulation profile.
- `SpawnableBuildingZoneLinker.cs` — clones a building prefab and repoints
  it at a custom zone, so vanilla's own spawn/level-up selection picks it up
  automatically.
- `ManualPrefabInitializationWorkaround.cs` — **required alongside both of
  the above**, not optional. A prefab cloned via `PrefabSystem.AddPrefab`
  after initial load never gets processed by the systems that would
  normally initialize it (root cause unknown - see DECISIONS.md). This
  patches that gap manually. Kept in its own file specifically so it can be
  deleted alone if the real cause is ever found, without touching the two
  adapters above.

**Confirmed in-game (2026-10-08)**, all three together: a cloned zone is
visible, named, correctly colored, paintable, and spawns only the cloned
building, correctly positioned, surviving construction. See TOUCHPOINTS.md
for the full verification history and DECISIONS.md for what the workaround
actually does and why it's needed.

That end-to-end proof was via a throwaway debug test harness with
hardcoded names (`CustomZones/Debug/`, since removed), not real
`Collection` data.

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
catalog above and clones the zone and each distinct building via the
three adapters in this folder. Adopted assets are skipped with a warning -
they need their own GameAdapter to give an arbitrary building growable
data first, not yet built.

**Confirmed in-game (2026-10-08)**, with real `Collection` data resolving
the full ~416-building vanilla selector plus an explicit asset, not just
the single hardcoded building the three adapters above were originally
proven with: every building renders, their sub-objects (fences, chimneys,
driveway props, etc.) spawn correctly, and the 12 vanilla Signature
buildings are correctly excluded rather than surfacing as clone failures.
See TOUCHPOINTS.md/DECISIONS.md for what else this round of testing found
and fixed in `ManualPrefabInitializationWorkaround.cs`.

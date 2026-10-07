# Architecture

Primary goal beyond functionality: **when a game patch breaks something,
root-cause analysis should be fast**, and one feature breaking must never
take down the other.

## Module layout

```
src/
  Core/              collections, adopted assets, settings, logging,
                     candidate-filter logic (no game-specific spawn code)
  CustomZones/       depends on Core only
    GameAdapters/    every game touchpoint used by this feature
  DistrictThemes/    depends on Core only
    GameAdapters/    every game touchpoint used by this feature
  UI/
    core/            collection editor, building picker
    customZones/
    districtThemes/
tools/
  touchpoint-dump/   dumps signatures of game types/methods we depend on
TOUCHPOINTS.md       inventory of game internals we depend on
```

### Rules

1. **Features never reference each other.** Anything shared moves into
   `Core`. Enforced by separate assemblies if CS2 loads multiple DLLs from a
   mod folder (to verify), otherwise by an architecture test over
   namespaces.
2. **All game-internal access goes through `GameAdapters/`**, one file per
   touchpoint (e.g. `CustomZones/GameAdapters/ZonePrefabCloner.cs`,
   `DistrictThemes/GameAdapters/SpawnSystemReplacement.cs`). Feature logic
   talks to adapters, never to game types directly.
3. **Core logic is game-independent and unit-tested.** Collection
   membership and the candidate filter ("given this lot and these
   collections, which buildings are eligible?") are plain C#, testable in CI
   without the game. Adapters stay as thin as possible.

## TOUCHPOINTS.md

Every adapter is listed with:

| Field | Example |
|---|---|
| Adapter | `DistrictThemes/GameAdapters/SpawnSystemReplacement.cs` |
| Feature | DistrictThemes |
| Game symbols | `Game.Simulation.ZoneSpawnSystem`, its job struct fields |
| Assumption | Vanilla system can be disabled and replaced by ours |
| Last verified | game version x.y.z |

After a game patch this is the RCA checklist.

## Startup self-checks and per-feature shutoff

On load, each enabled feature verifies its adapters' assumptions before
activating:

- patched methods exist with expected signatures
- expected components/fields exist
- systems to be replaced are registered

If any check fails, **only that feature disables itself**, logs exactly
which check failed, and shows an in-game notification. The other feature
continues normally.

Harmony patches are applied **per feature** with distinct Harmony IDs and
isolated error handling, so a failed patch is attributable and doesn't
block the other feature.

## Logging

- Startup: game version, mod version, enabled features, result of every
  self-check.
- Per-feature prefixes: `[Core]`, `[CZ]`, `[DT]`.
- Optional verbose mode logs spawn decisions, e.g.
  `[DT] lot 1234 in district "Old Town": 14 candidates -> picked X`.

## Save data

Each feature serializes its own components with its own version number.
Corrupt or outdated data in one feature can't affect the other; migrations
are per feature.

- Custom Zones: zone definitions referenced by zoned cells (save depends on
  the mod while in use).
- District Themes: theme assignment stored on district entities (removing
  the mod just drops the filter).

## Patch-day workflow

1. Run `tools/touchpoint-dump` against the new `Game.dll`.
2. Diff against the previous dump to see which adapters are affected,
   often before launching the game.
3. Launch and check startup self-check logs.
4. Fix affected adapters, update `Last verified` in `TOUCHPOINTS.md`.

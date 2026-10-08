# Touchpoints

Inventory of game internals each `GameAdapters/` adapter depends on. Updated
whenever an adapter is added or re-verified against a new game version. See
"Patch-day workflow" in [ARCHITECTURE.md](ARCHITECTURE.md).

| Adapter | Feature | Game symbols | Assumption | Last verified |
|---|---|---|---|---|
| `CustomZones/GameAdapters/ZonePrefabCloner.cs` | CustomZones | `Game.Prefabs.PrefabSystem.AddPrefab`, `Game.Prefabs.PrefabBase.Clone`, `Game.Prefabs.ZonePrefab`, `Game.Prefabs.UIObject.m_Group` | Cloning a `ZonePrefab` + `AddPrefab` registers a real, independent zone type. Needs `ManualPrefabInitializationWorkaround` alongside it (see below) - on its own the clone never initializes. | **In-game confirmed end-to-end**: visible, named, correctly colored, paintable, Steam build 25127643, 2026-10-08 |
| `CustomZones/GameAdapters/SpawnableBuildingZoneLinker.cs` | CustomZones | `Game.Prefabs.SpawnableBuilding.m_ZoneType`, `Game.Prefabs.BuildingSpawnGroupData`, `Game.Simulation.ZoneSpawnSystem.SelectBuilding`, `Game.Simulation.BuildingUpkeepSystem.SelectSpawnableBuilding` | Repointing `SpawnableBuilding.m_ZoneType` before registering is sufficient for both spawn and level-up selection to only offer that building on the target zone - neither Burst-compiled system needs patching. Also needs `ManualPrefabInitializationWorkaround` alongside it. | **In-game confirmed end-to-end**: spawns correctly, positioned correctly, survives construction, Steam build 25127643, 2026-10-08 |
| `CustomZones/GameAdapters/ManualPrefabInitializationWorkaround.cs` | CustomZones | `Game.Prefabs.PrefabInitializeSystem`, `Game.Prefabs.ZoneSystem` (private `m_ZonePrefabs`, private `UpdateZoneColors()`), `Game.Prefabs.ObjectInitializeSystem`, `ComponentBase.Initialize`/`LateInitialize`, `Game.Common.UpdateSystem.Update(SystemUpdatePhase)` | **Root cause still unknown** (see DECISIONS.md): a prefab added via `PrefabSystem.AddPrefab` after initial load never gets processed by any `Created`-tag-keyed system - not `PrefabInitializeSystem`, not `ZoneSystem`, not `ObjectInitializeSystem`. Confirmed this isn't a timing, phase-registration, or zone-type issue (tried immediate vs. deferred, `PrefabUpdate` vs. `MainLoop`, Residential vs. Industrial - identical failure every time), and confirmed calling `UpdateSystem.Update(SystemUpdatePhase.PrefabUpdate)` manually, directly, does not trigger it either, despite `PrefabSystem.OnUpdate()` calling that exact line unconditionally every tick. Workaround: call `ComponentBase.Initialize`/`LateInitialize` directly on every attached component (covers everything a component sets itself); reflect into `ZoneSystem`'s private zone registry and color-array refresh (bespoke to that system, not a component method); copy `ObjectGeometryData`/`BuildingData` straight from the template (computed by `ObjectInitializeSystem`, also never fires, and identical between clone and template anyway). | **In-game confirmed working**, all four prior gaps found this way (invisible zone, no spawning, buildings underground, vanishing after construction, missing paint-highlight color) are fixed, Steam build 25127643, 2026-10-08 |

Confirmed-in-game findings came from an actual debug playtest, using a
throwaway test harness (`CustomZones/Debug/DebugZoneCloneTestSystem.cs`,
removed once it had served its purpose) with hardcoded names rather than
real `Collection` data - not just from reading `D:\CS2-Decompiled` (an
`ilspycmd` decompile of `Game.dll`). This session has no way to launch CS2
itself, so playtesting was done by the user and reported back.

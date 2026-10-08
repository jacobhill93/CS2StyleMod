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

Not yet wired to anything *real*: this was proven via a throwaway debug
test harness with hardcoded names (`CustomZones/Debug/`, since removed now
that it's served its purpose - see TOUCHPOINTS.md/DECISIONS.md for what it
found), not from real `Collection` data. `IBuildingCatalog` (in
`../../Core`) still has no real implementation - that's the next step,
resolving a collection's entries into real prefabs via these adapters.

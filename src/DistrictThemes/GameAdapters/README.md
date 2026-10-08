# DistrictThemes/GameAdapters

Every game touchpoint the District Themes feature depends on, one file per
touchpoint (e.g. `SpawnSystemReplacement.cs`). Feature logic talks only to
these adapters, never to game types directly. Each adapter gets an entry in
`../../../TOUCHPOINTS.md`.

Not yet implemented — depends on `../../Core`. Built after Custom Zones per
`DECISIONS.md`'s build order, since it requires replacing (or
post-correcting) the Burst-compiled `ZoneSpawnSystem`.

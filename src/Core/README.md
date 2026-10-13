# Core

Game-independent, unit-testable logic shared by both features: collections,
adopted assets, settings, logging, and the candidate-filter ("given this lot
and these collections, which buildings are eligible?"). No game-specific
spawn code lives here — see `../CustomZones/GameAdapters` and
`../DistrictThemes/GameAdapters` for that.

- `Collection.cs`, `CollectionEntry.cs` — the three entry kinds from
  README.md's "Collections (core)" section.
- `AdoptedAssetSettings.cs` — mod-supplied growable metadata for unzoned
  assets.
- `BuildingCandidate.cs`, `IBuildingCatalog.cs` — plain building data and the
  seam a GameAdapter implements against real prefabs (fakeable in tests).
- `CandidateFilter.cs` — the eligibility logic itself, including the
  configurable fallback policy.
- `ICollectionLibrary.cs`, `FolderCollectionLibrary.cs`,
  `CollectionFileFormat.cs` — the global, cross-save collection library
  from `../../DECISIONS.md` ("Collections persist in a global library,
  referenced by GUID"). One file per collection (`<Id>.json`), not one
  combined file - see DECISIONS.md "Migrated to one file per collection"
  for why. Pure BCL + Newtonsoft - no game dependency; `Mod.cs` is the
  only place that decides *where* the folder lives (via
  `Colossal.PSI.Environment.EnvPath`) and wires it up.
  `LegacyCollectionsMigrator.cs` is the one-time upgrade path from the
  old single-file format. `CollectionImporter.cs` is the separate,
  boot-time-only mechanism for dropping in externally-sourced collection
  files - see its own header comment.
- `CustomZoneDefinition.cs`, `ICustomZoneDefinitionLibrary.cs`,
  `FolderCustomZoneDefinitionLibrary.cs`, `CustomZoneDefinitionFileFormat.cs`,
  `LegacyCustomZoneDefinitionsMigrator.cs` — a persisted, user-defined
  custom zone: a base `ZoneType` plus the `Collection` Id(s) it should pull
  buildings from. Same one-file-per-definition, rename-free, GUID-keyed
  persistence shape as `Collection` above - mirrors it exactly. What
  actually turns one into an in-game zone lives in
  `../CustomZones/CustomZoneBuilder.cs`.

All unit-tested without the game in `../../tests/ZoneBuilder.Core.Tests`
(run with `dotnet test tests/ZoneBuilder.Core.Tests`).

Not yet wired to anything real: nothing calls `ICollectionLibrary` from
in-game UI yet, and no GameAdapter implements `IBuildingCatalog`. The
lot-matching rule in `CandidateFilter` (exact width, depth fits) is an
assumption, unverified against the game — see `../../DECISIONS.md`.

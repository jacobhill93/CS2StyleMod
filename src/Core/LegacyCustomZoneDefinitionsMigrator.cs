using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // One-time migration from the old single-combined-file customZones.json
    // to FolderCustomZoneDefinitionLibrary's one-file-per-definition
    // layout - mirrors LegacyCollectionsMigrator exactly, see its own
    // comment and DECISIONS.md "Migrated to one file per collection" for
    // the full reasoning (applies identically here).
    public static class LegacyCustomZoneDefinitionsMigrator
    {
        public readonly struct DefinitionFailure
        {
            public readonly Guid? Id;
            public readonly string Name;
            public readonly Exception Error;

            public DefinitionFailure(Guid? id, string name, Exception error)
            {
                Id = id;
                Name = name;
                Error = error;
            }
        }

        public readonly struct Result
        {
            public readonly bool Attempted;
            public readonly int MigratedCount;
            public readonly IReadOnlyList<DefinitionFailure> DefinitionFailures;
            public readonly Exception FatalError;

            public Result(bool attempted, int migratedCount, IReadOnlyList<DefinitionFailure> definitionFailures, Exception fatalError)
            {
                Attempted = attempted;
                MigratedCount = migratedCount;
                DefinitionFailures = definitionFailures;
                FatalError = fatalError;
            }

            public static readonly Result NotAttempted = new Result(false, 0, Array.Empty<DefinitionFailure>(), null);
        }

        public static Result MigrateIfNeeded(string legacyFilePath, string definitionsFolderPath)
        {
            if (!File.Exists(legacyFilePath))
                return Result.NotAttempted;

            CustomZoneDefinitionFileFormat.LibraryFileDto dto;
            try
            {
                var json = File.ReadAllText(legacyFilePath);
                dto = CustomZoneDefinitionFileFormat.ParseLibraryFile(json);
            }
            catch (Exception ex)
            {
                return new Result(true, 0, Array.Empty<DefinitionFailure>(), ex);
            }

            var migratedCount = 0;
            var failures = new List<DefinitionFailure>();

            foreach (var definitionDto in dto?.Definitions ?? Enumerable.Empty<CustomZoneDefinitionFileFormat.CustomZoneDefinitionDto>())
            {
                try
                {
                    var definition = CustomZoneDefinitionFileFormat.FromDto(definitionDto);
                    WriteDefinitionFile(definitionsFolderPath, definition);
                    migratedCount++;
                }
                catch (Exception ex)
                {
                    failures.Add(new DefinitionFailure(definitionDto?.Id, definitionDto?.Name, ex));
                }
            }

            var backupPath = legacyFilePath + ".migrated";
            if (File.Exists(backupPath))
                backupPath = legacyFilePath + $".migrated-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(legacyFilePath, backupPath);

            return new Result(true, migratedCount, failures, fatalError: null);
        }

        private static void WriteDefinitionFile(string definitionsFolderPath, CustomZoneDefinition definition)
        {
            var dto = new CustomZoneDefinitionFileFormat.LibraryFileDto
            {
                SchemaVersion = CustomZoneDefinitionFileFormat.CurrentSchemaVersion,
                Definitions = new List<CustomZoneDefinitionFileFormat.CustomZoneDefinitionDto> { CustomZoneDefinitionFileFormat.ToDto(definition) },
            };
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);

            Directory.CreateDirectory(definitionsFolderPath);
            var finalPath = Path.Combine(definitionsFolderPath, $"{definition.Id}.json");
            var tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
    }
}

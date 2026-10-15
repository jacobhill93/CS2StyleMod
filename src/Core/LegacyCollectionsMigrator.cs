using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // One-time migration from the old single-combined-file collections
    // library to FolderCollectionLibrary's one-file-per-collection
    // layout - see DECISIONS.md "Migrated to one file per collection".
    // Deliberately splits into genuinely separate <Id>.json files up
    // front (doesn't just move the old file into the new folder and
    // rely on FolderCollectionLibrary's own multi-collection-per-file
    // tolerance) - that tolerance exists for a file manually dropped in
    // later, but Delete() only ever targets <Id>.json, so leaving a
    // shared file in place would make any of its collections silently
    // undeletable until something else happened to rewrite it.
    //
    // Pure BCL + Newtonsoft.Json, no game dependency - unit-testable by
    // pointing it at temp paths, same reasoning as the two classes this
    // sits alongside.
    public static class LegacyCollectionsMigrator
    {
        public readonly struct CollectionFailure
        {
            public readonly Guid? Id;
            public readonly string Name;
            public readonly Exception Error;

            public CollectionFailure(Guid? id, string name, Exception error)
            {
                Id = id;
                Name = name;
                Error = error;
            }
        }

        public readonly struct Result
        {
            // False if there was no legacy file to migrate at all - the
            // common case after the first launch post-migration.
            public readonly bool Attempted;
            public readonly int MigratedCount;
            public readonly IReadOnlyList<CollectionFailure> CollectionFailures;

            // Set only if the legacy file itself couldn't even be parsed
            // (corrupted top-level JSON) - migration aborted entirely,
            // the legacy file is left exactly as it was, nothing written
            // to the new folder. Not the same as CollectionFailures,
            // which is per-collection and still lets migration succeed
            // for everything else in the file.
            public readonly Exception FatalError;

            public Result(bool attempted, int migratedCount, IReadOnlyList<CollectionFailure> collectionFailures, Exception fatalError)
            {
                Attempted = attempted;
                MigratedCount = migratedCount;
                CollectionFailures = collectionFailures;
                FatalError = fatalError;
            }

            public static readonly Result NotAttempted = new Result(false, 0, Array.Empty<CollectionFailure>(), null);
        }

        public static Result MigrateIfNeeded(string legacyFilePath, string collectionsFolderPath)
        {
            if (!File.Exists(legacyFilePath))
                return Result.NotAttempted;

            CollectionFileFormat.LibraryFileDto dto;
            try
            {
                var json = File.ReadAllText(legacyFilePath);
                dto = CollectionFileFormat.ParseLibraryFile(json);
            }
            catch (Exception ex)
            {
                // Legacy file itself is unreadable/corrupted - leave it
                // exactly as-is for the user to inspect/recover by hand
                // rather than lose it. The mod boots with zero migrated
                // collections this session (same degraded-but-not-crashed
                // state FolderCollectionLibrary's own per-file isolation
                // gives everywhere else), not a crash.
                return new Result(true, 0, Array.Empty<CollectionFailure>(), ex);
            }

            var migratedCount = 0;
            var failures = new List<CollectionFailure>();

            foreach (var collectionDto in dto?.Collections ?? Enumerable.Empty<CollectionFileFormat.CollectionDto>())
            {
                try
                {
                    var collection = CollectionFileFormat.FromDto(collectionDto);
                    WriteCollectionFile(collectionsFolderPath, collection);
                    migratedCount++;
                }
                catch (Exception ex)
                {
                    failures.Add(new CollectionFailure(collectionDto?.Id, collectionDto?.Name, ex));
                }
            }

            // Renamed, not deleted - kept as a recoverable trace in case
            // anything above was wrong in a way nobody noticed yet, same
            // "don't destroy what might still be needed" discipline as
            // CollectionImporter's own Imports/Processed/ folder.
            var backupPath = legacyFilePath + ".migrated";
            if (File.Exists(backupPath))
                backupPath = legacyFilePath + $".migrated-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(legacyFilePath, backupPath);

            return new Result(true, migratedCount, failures, fatalError: null);
        }

        private static void WriteCollectionFile(string collectionsFolderPath, Collection collection)
        {
            var dto = new CollectionFileFormat.LibraryFileDto
            {
                SchemaVersion = CollectionFileFormat.CurrentSchemaVersion,
                Collections = new List<CollectionFileFormat.CollectionDto> { CollectionFileFormat.ToDto(collection) },
            };
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);

            Directory.CreateDirectory(collectionsFolderPath);
            var finalPath = Path.Combine(collectionsFolderPath, $"{collection.Id}.json");
            var tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
    }
}

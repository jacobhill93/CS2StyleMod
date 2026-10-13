using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // Boot-time-only import, deliberately no in-game button or live
    // trigger - the user drops exported/shared files into a fixed
    // folder before launching, and they're picked up automatically on
    // the next OnLoad. Each file uses the same schema as
    // FolderCollectionLibrary's own file (CollectionFileFormat), so a
    // single-collection snippet and someone's entire collections.json
    // both just work the same way.
    //
    // Pure BCL + Newtonsoft.Json, no game dependency - unit-testable by
    // pointing it at a temp folder, same reasoning as
    // FolderCollectionLibrary.
    public static class CollectionImporter
    {
        public readonly struct Failure
        {
            public readonly string FilePath;
            public readonly Exception Error;

            public Failure(string filePath, Exception error)
            {
                FilePath = filePath;
                Error = error;
            }
        }

        public readonly struct Result
        {
            public readonly int ImportedCollectionCount;
            public readonly IReadOnlyList<Failure> Failures;

            public Result(int importedCollectionCount, IReadOnlyList<Failure> failures)
            {
                ImportedCollectionCount = importedCollectionCount;
                Failures = failures;
            }
        }

        // Every imported collection gets a fresh Id, never the file's own
        // stored one - an import always coexists alongside whatever's
        // already in the library rather than risking a silent overwrite
        // of an existing collection that happens to share a stored Id
        // (e.g. the same file imported twice, or two people exporting
        // from related libraries).
        //
        // A file that fails to parse is left in place (not moved to
        // Processed/) and reported as a Failure - the caller decides how
        // to log it (Core has no logging dependency by design, see
        // ARCHITECTURE.md). One bad file never stops the rest of the
        // folder from being processed.
        public static Result ImportFromFolder(string importsFolderPath, ICollectionLibrary library)
        {
            if (!Directory.Exists(importsFolderPath))
                return new Result(0, Array.Empty<Failure>());

            var importedCount = 0;
            var failures = new List<Failure>();

            foreach (var filePath in Directory.GetFiles(importsFolderPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var dto = JsonConvert.DeserializeObject<CollectionFileFormat.LibraryFileDto>(json);

                    foreach (var collectionDto in dto?.Collections ?? Enumerable.Empty<CollectionFileFormat.CollectionDto>())
                    {
                        var collection = new Collection(collectionDto.Name);
                        foreach (var entryDto in collectionDto.Entries ?? Enumerable.Empty<CollectionFileFormat.EntryDto>())
                            collection.Add(CollectionFileFormat.FromDto(entryDto));

                        library.Save(collection);
                        importedCount++;
                    }

                    MoveToProcessed(importsFolderPath, filePath);
                }
                catch (Exception ex)
                {
                    failures.Add(new Failure(filePath, ex));
                }
            }

            return new Result(importedCount, failures);
        }

        private static void MoveToProcessed(string importsFolderPath, string filePath)
        {
            var processedFolderPath = Path.Combine(importsFolderPath, "Processed");
            Directory.CreateDirectory(processedFolderPath);

            var destPath = Path.Combine(processedFolderPath, Path.GetFileName(filePath));
            if (File.Exists(destPath))
            {
                var uniqueSuffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                destPath = Path.Combine(
                    processedFolderPath,
                    $"{Path.GetFileNameWithoutExtension(filePath)}.{uniqueSuffix}{Path.GetExtension(filePath)}");
            }

            File.Move(filePath, destPath);
        }
    }
}

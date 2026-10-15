using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // File-backed ICollectionLibrary, one file per collection
    // (<folder>/<Id>.json), not one combined file - see DECISIONS.md
    // "Migrated to one file per collection" for why. Pure BCL +
    // Newtonsoft.Json - no game dependency, so it's unit-testable by
    // pointing it at a temp folder. The only game-specific bit is
    // choosing *where* that folder lives, which happens at the call site
    // (Mod.cs), not here.
    public sealed class FolderCollectionLibrary : ICollectionLibrary
    {
        public readonly struct LoadFailure
        {
            public readonly string FilePath;
            public readonly Exception Error;

            public LoadFailure(string filePath, Exception error)
            {
                FilePath = filePath;
                Error = error;
            }
        }

        private readonly string m_FolderPath;
        private readonly Dictionary<Guid, Collection> m_Collections;

        // Populated once, at construction - a bad file is isolated to
        // itself (skipped, reported here), it never prevents every other
        // collection from loading, and never throws out of the
        // constructor the way a single combined file's one bad byte used
        // to risk taking down the whole mod's OnLoad. Caller (Mod.cs)
        // logs these - Core has no logging dependency by design, see
        // ARCHITECTURE.md.
        public IReadOnlyList<LoadFailure> LoadFailures { get; }

        public FolderCollectionLibrary(string folderPath)
        {
            m_FolderPath = folderPath;
            var failures = new List<LoadFailure>();
            m_Collections = Load(folderPath, failures);
            LoadFailures = failures;
        }

        public IReadOnlyList<Collection> GetAll() => m_Collections.Values.ToList();

        // Returns a clone, not the library's own live reference - see
        // Save's own comment for why (unchanged from the single-file
        // version this replaces).
        public bool TryGet(Guid id, out Collection collection)
        {
            if (m_Collections.TryGetValue(id, out var stored))
            {
                collection = stored.Clone();
                return true;
            }

            collection = null;
            return false;
        }

        // Strong exception safety, same guarantee as before, now scoped
        // to just this one collection's own file - either this fully
        // succeeds (in-memory state updated AND persisted), or it throws
        // and leaves in-memory state exactly as it was, never an
        // in-between "looks saved, isn't on disk" state. A write failure
        // here can no longer corrupt or block any *other* collection's
        // file, unlike the single combined file this replaces.
        public void Save(Collection collection)
        {
            var hadPrevious = m_Collections.TryGetValue(collection.Id, out var previous);
            m_Collections[collection.Id] = collection;

            try
            {
                WriteToDisk(collection);
            }
            catch
            {
                if (hadPrevious)
                    m_Collections[collection.Id] = previous;
                else
                    m_Collections.Remove(collection.Id);
                throw;
            }
        }

        public void Delete(Guid id)
        {
            if (!m_Collections.TryGetValue(id, out var removed))
                return;

            m_Collections.Remove(id);

            try
            {
                var path = GetFilePath(m_FolderPath, id);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                m_Collections[id] = removed;
                throw;
            }
        }

        private static string GetFilePath(string folderPath, Guid id) => Path.Combine(folderPath, $"{id}.json");

        // Tolerates a file holding more than one collection (not just
        // this library's own <Id>.json convention) so a file manually
        // dropped straight into the folder - bypassing the normal
        // Imports flow - still loads rather than being silently ignored.
        // Known rough edge, not fixed: Delete() only ever looks for
        // <Id>.json, so a collection loaded out of a multi-collection or
        // oddly-named file can't actually be deleted from disk that way
        // - it would reappear on the next launch. Doesn't affect
        // anything this library itself ever writes (always its own
        // <Id>.json, see WriteToDisk), only a manually-placed file -
        // acceptable since LegacyCollectionsMigrator (the one thing that
        // used to produce a multi-collection file) always splits into
        // genuinely separate files up front, specifically to avoid this.
        private static Dictionary<Guid, Collection> Load(string folderPath, List<LoadFailure> failures)
        {
            var result = new Dictionary<Guid, Collection>();

            if (!Directory.Exists(folderPath))
                return result;

            foreach (var filePath in Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var dto = CollectionFileFormat.ParseLibraryFile(json);

                    foreach (var collectionDto in dto?.Collections ?? Enumerable.Empty<CollectionFileFormat.CollectionDto>())
                    {
                        var collection = CollectionFileFormat.FromDto(collectionDto);
                        result[collection.Id] = collection;
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(new LoadFailure(filePath, ex));
                }
            }

            return result;
        }

        private void WriteToDisk(Collection collection)
        {
            var dto = new CollectionFileFormat.LibraryFileDto
            {
                SchemaVersion = CollectionFileFormat.CurrentSchemaVersion,
                Collections = new List<CollectionFileFormat.CollectionDto> { CollectionFileFormat.ToDto(collection) },
            };
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);

            Directory.CreateDirectory(m_FolderPath);

            // Write-then-replace so a crash mid-write can't corrupt this
            // collection's file - same pattern as before, now scoped to
            // one file instead of the whole library.
            var finalPath = GetFilePath(m_FolderPath, collection.Id);
            var tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
    }
}

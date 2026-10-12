using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // File-backed ICollectionLibrary. Pure BCL + Newtonsoft.Json - no game
    // dependency, so it's unit-testable by pointing it at a temp file. The
    // only game-specific bit is choosing *where* that file lives, which
    // happens at the call site (Mod.cs), not here.
    public sealed class JsonFileCollectionLibrary : ICollectionLibrary
    {
        private const int kCurrentSchemaVersion = 1;

        private readonly string m_FilePath;
        private readonly Dictionary<Guid, Collection> m_Collections;

        public JsonFileCollectionLibrary(string filePath)
        {
            m_FilePath = filePath;
            m_Collections = Load(filePath);
        }

        public IReadOnlyList<Collection> GetAll() => m_Collections.Values.ToList();

        // Returns a clone, not the library's own live reference - every
        // real caller follows a TryGet -> mutate -> Save pattern
        // (CollectionUISystem), and without this, that mutation would
        // land on the SAME object already sitting in m_Collections,
        // before Save's own rollback-on-failure logic ever runs. Found
        // via a failing test, not anticipated up front: Save's rollback
        // looked structurally complete but was silently a no-op for
        // exactly this pattern, since "the previous value" and "the
        // caller's already-mutated value" were the same object reference.
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

        // Strong exception safety: either this fully succeeds (in-memory
        // state updated AND persisted to disk), or it throws and leaves
        // in-memory state exactly as it was before the call - never the
        // in-between state of "looks saved in memory, isn't on disk"
        // that WriteToDisk's un-caught I/O exceptions (disk full, file
        // locked, permissions denied - all real, not hypothetical) used
        // to leave behind. Deliberately doesn't catch-and-swallow or log
        // here - Core has no game/logging dependency by design (see
        // ARCHITECTURE.md), callers decide what to do with the
        // exception (CollectionUISystem catches and logs via Mod.log).
        public void Save(Collection collection)
        {
            var hadPrevious = m_Collections.TryGetValue(collection.Id, out var previous);
            m_Collections[collection.Id] = collection;

            try
            {
                WriteToDisk();
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
                WriteToDisk();
            }
            catch
            {
                m_Collections[id] = removed;
                throw;
            }
        }

        private static Dictionary<Guid, Collection> Load(string filePath)
        {
            var result = new Dictionary<Guid, Collection>();

            if (!File.Exists(filePath))
                return result;

            var json = File.ReadAllText(filePath);
            var dto = JsonConvert.DeserializeObject<LibraryFileDto>(json) ?? new LibraryFileDto();

            foreach (var collectionDto in dto.Collections ?? Enumerable.Empty<CollectionDto>())
            {
                var collection = FromDto(collectionDto);
                result[collection.Id] = collection;
            }

            return result;
        }

        private void WriteToDisk()
        {
            var dto = new LibraryFileDto
            {
                SchemaVersion = kCurrentSchemaVersion,
                Collections = m_Collections.Values.Select(ToDto).ToList(),
            };
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);

            var directory = Path.GetDirectoryName(m_FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Write-then-replace so a crash mid-write can't corrupt the
            // library a hundred saves rely on.
            var tempPath = m_FilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Delete(m_FilePath);
            File.Move(tempPath, m_FilePath);
        }

        private static CollectionDto ToDto(Collection collection)
        {
            return new CollectionDto
            {
                Id = collection.Id,
                Name = collection.Name,
                Entries = collection.Entries.Select(ToDto).ToList(),
            };
        }

        private static Collection FromDto(CollectionDto dto)
        {
            var collection = new Collection(dto.Id, dto.Name);
            foreach (var entryDto in dto.Entries ?? Enumerable.Empty<EntryDto>())
                collection.Add(FromDto(entryDto));
            return collection;
        }

        private static EntryDto ToDto(CollectionEntry entry)
        {
            switch (entry)
            {
                case VanillaSelectorEntry selector:
                    return new EntryDto
                    {
                        Kind = EntryDto.KindVanillaSelector,
                        ZoneType = selector.ZoneType,
                        AssetPackId = selector.AssetPackId,
                    };
                case ExplicitAssetEntry explicitEntry:
                    return new EntryDto
                    {
                        Kind = EntryDto.KindExplicitAsset,
                        PrefabId = explicitEntry.PrefabId,
                    };
                case AdoptedAssetEntry adoptedEntry:
                    return new EntryDto
                    {
                        Kind = EntryDto.KindAdoptedAsset,
                        PrefabId = adoptedEntry.PrefabId,
                        ZoneType = adoptedEntry.Settings.Category,
                        Level = adoptedEntry.Settings.Level,
                        LotWidth = adoptedEntry.Settings.LotSize.Width,
                        LotDepth = adoptedEntry.Settings.LotSize.Depth,
                        HouseholdCount = adoptedEntry.Settings.HouseholdCount,
                    };
                default:
                    throw new NotSupportedException($"Unknown {nameof(CollectionEntry)} type: {entry.GetType()}");
            }
        }

        private static CollectionEntry FromDto(EntryDto dto)
        {
            switch (dto.Kind)
            {
                case EntryDto.KindVanillaSelector:
                    return new VanillaSelectorEntry(dto.ZoneType, dto.AssetPackId);
                case EntryDto.KindExplicitAsset:
                    return new ExplicitAssetEntry(dto.PrefabId);
                case EntryDto.KindAdoptedAsset:
                    var lotSize = new LotSize(dto.LotWidth ?? 0, dto.LotDepth ?? 0);
                    var settings = new AdoptedAssetSettings(dto.ZoneType ?? default, dto.Level ?? 0, lotSize, dto.HouseholdCount);
                    return new AdoptedAssetEntry(dto.PrefabId, settings);
                default:
                    throw new NotSupportedException($"Unknown collection entry kind in library file: '{dto.Kind}'");
            }
        }

        // DTOs for the JSON shape, kept explicit (rather than e.g.
        // TypeNameHandling) so the file format doesn't depend on our C#
        // class/namespace names and stays stable across refactors.
        private sealed class LibraryFileDto
        {
            public int SchemaVersion { get; set; } = kCurrentSchemaVersion;
            public List<CollectionDto> Collections { get; set; } = new List<CollectionDto>();
        }

        private sealed class CollectionDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public List<EntryDto> Entries { get; set; } = new List<EntryDto>();
        }

        private sealed class EntryDto
        {
            public const string KindVanillaSelector = "vanillaSelector";
            public const string KindExplicitAsset = "explicitAsset";
            public const string KindAdoptedAsset = "adoptedAsset";

            public string Kind { get; set; }
            public ZoneType? ZoneType { get; set; }
            public string AssetPackId { get; set; }
            public string PrefabId { get; set; }
            public int? Level { get; set; }
            public int? LotWidth { get; set; }
            public int? LotDepth { get; set; }
            public int? HouseholdCount { get; set; }
        }
    }
}

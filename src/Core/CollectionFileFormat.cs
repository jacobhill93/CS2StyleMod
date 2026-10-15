using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // The JSON shape shared by FolderCollectionLibrary's own file and
    // CollectionImporter's import files - same schema either way, so a
    // file dropped into the Imports folder can be either a single-
    // collection snippet or someone's entire exported collections.json,
    // both just work. Extracted out of FolderCollectionLibrary (which
    // used to define these privately) so CollectionImporter doesn't need
    // its own, slightly-different copy of the same parsing logic.
    //
    // Kept as explicit DTOs (rather than e.g. TypeNameHandling) so the
    // file format doesn't depend on our C# class/namespace names and
    // stays stable across refactors - same reasoning as before this was
    // split out.
    internal static class CollectionFileFormat
    {
        public const int CurrentSchemaVersion = 1;

        // No real schema change has ever happened to this format yet -
        // empty on purpose. Add a step here (SchemaMigrationStep(1, ...))
        // the next time CurrentSchemaVersion is bumped, rather than
        // trying to express the change as a change to CollectionDto/
        // EntryDto directly - see SchemaMigration.cs for why.
        private static readonly IReadOnlyList<SchemaMigrationStep> Migrations = Array.Empty<SchemaMigrationStep>();

        // The one place every load path (FolderCollectionLibrary,
        // LegacyCollectionsMigrator, CollectionImporter) should parse a
        // collections file from - runs it through SchemaMigration first,
        // so a file written by an older mod version is transformed
        // before DeserializeObject ever sees it, rather than each call
        // site deserializing directly and hoping the current DTO shape
        // happens to still match.
        public static LibraryFileDto ParseLibraryFile(string json)
        {
            json = SchemaMigration.Migrate(json, CurrentSchemaVersion, Migrations);
            return JsonConvert.DeserializeObject<LibraryFileDto>(json);
        }

        public sealed class LibraryFileDto
        {
            public int SchemaVersion { get; set; } = CurrentSchemaVersion;
            public List<CollectionDto> Collections { get; set; } = new List<CollectionDto>();
        }

        public sealed class CollectionDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public List<EntryDto> Entries { get; set; } = new List<EntryDto>();
        }

        public sealed class EntryDto
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

        public static CollectionDto ToDto(Collection collection)
        {
            return new CollectionDto
            {
                Id = collection.Id,
                Name = collection.Name,
                Entries = collection.Entries.Select(ToDto).ToList(),
            };
        }

        // Preserves the DTO's own Id - right for FolderCollectionLibrary
        // loading its own file back, wrong for importing someone else's
        // file (see CollectionImporter, which builds its own Collection
        // with a fresh Id instead of calling this).
        public static Collection FromDto(CollectionDto dto)
        {
            var collection = new Collection(dto.Id, dto.Name);
            foreach (var entryDto in dto.Entries ?? Enumerable.Empty<EntryDto>())
                collection.Add(FromDto(entryDto));
            return collection;
        }

        public static EntryDto ToDto(CollectionEntry entry)
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

        public static CollectionEntry FromDto(EntryDto dto)
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
    }
}

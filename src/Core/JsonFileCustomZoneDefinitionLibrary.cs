using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CS2StyleMod.Core
{
    // File-backed ICustomZoneDefinitionLibrary. Pure BCL + Newtonsoft.Json -
    // no game dependency, mirrors JsonFileCollectionLibrary exactly. Stored
    // in its own file, separate from collections.json - a CustomZoneDefinition
    // and a Collection are different kinds of thing (one references many of
    // the other), each with its own single-responsibility store.
    public sealed class JsonFileCustomZoneDefinitionLibrary : ICustomZoneDefinitionLibrary
    {
        private const int kCurrentSchemaVersion = 1;

        private readonly string m_FilePath;
        private readonly Dictionary<Guid, CustomZoneDefinition> m_Definitions;

        public JsonFileCustomZoneDefinitionLibrary(string filePath)
        {
            m_FilePath = filePath;
            m_Definitions = Load(filePath);
        }

        public IReadOnlyList<CustomZoneDefinition> GetAll() => m_Definitions.Values.ToList();

        public bool TryGet(Guid id, out CustomZoneDefinition definition) => m_Definitions.TryGetValue(id, out definition);

        public void Save(CustomZoneDefinition definition)
        {
            m_Definitions[definition.Id] = definition;
            WriteToDisk();
        }

        public void Delete(Guid id)
        {
            if (m_Definitions.Remove(id))
                WriteToDisk();
        }

        private static Dictionary<Guid, CustomZoneDefinition> Load(string filePath)
        {
            var result = new Dictionary<Guid, CustomZoneDefinition>();

            if (!File.Exists(filePath))
                return result;

            var json = File.ReadAllText(filePath);
            var dto = JsonConvert.DeserializeObject<LibraryFileDto>(json) ?? new LibraryFileDto();

            foreach (var definitionDto in dto.Definitions ?? Enumerable.Empty<CustomZoneDefinitionDto>())
            {
                var definition = FromDto(definitionDto);
                result[definition.Id] = definition;
            }

            return result;
        }

        private void WriteToDisk()
        {
            var dto = new LibraryFileDto
            {
                SchemaVersion = kCurrentSchemaVersion,
                Definitions = m_Definitions.Values.Select(ToDto).ToList(),
            };
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);

            var directory = Path.GetDirectoryName(m_FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Write-then-replace so a crash mid-write can't corrupt the
            // library every custom zone depends on.
            var tempPath = m_FilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Delete(m_FilePath);
            File.Move(tempPath, m_FilePath);
        }

        private static CustomZoneDefinitionDto ToDto(CustomZoneDefinition definition)
        {
            return new CustomZoneDefinitionDto
            {
                Id = definition.Id,
                Name = definition.Name,
                BaseZoneType = definition.BaseZoneType,
                CollectionIds = definition.CollectionIds.ToList(),
            };
        }

        private static CustomZoneDefinition FromDto(CustomZoneDefinitionDto dto)
        {
            var definition = new CustomZoneDefinition(dto.Id, dto.Name, dto.BaseZoneType);
            foreach (var collectionId in dto.CollectionIds ?? Enumerable.Empty<Guid>())
                definition.AddCollection(collectionId);
            return definition;
        }

        // DTOs for the JSON shape, kept explicit (rather than e.g.
        // TypeNameHandling) so the file format doesn't depend on our C#
        // class/namespace names and stays stable across refactors.
        private sealed class LibraryFileDto
        {
            public int SchemaVersion { get; set; } = kCurrentSchemaVersion;
            public List<CustomZoneDefinitionDto> Definitions { get; set; } = new List<CustomZoneDefinitionDto>();
        }

        private sealed class CustomZoneDefinitionDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public ZoneType BaseZoneType { get; set; }
            public List<Guid> CollectionIds { get; set; } = new List<Guid>();
        }
    }
}

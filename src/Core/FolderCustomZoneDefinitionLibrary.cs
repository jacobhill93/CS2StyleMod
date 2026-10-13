using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // File-backed ICustomZoneDefinitionLibrary, one file per definition
    // (<folder>/<Id>.json), mirrors FolderCollectionLibrary exactly -
    // see DECISIONS.md "Migrated to one file per collection" (the same
    // reasoning applies here: a corrupted file is isolated to itself,
    // never risks the whole mod failing to load).
    public sealed class FolderCustomZoneDefinitionLibrary : ICustomZoneDefinitionLibrary
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
        private readonly Dictionary<Guid, CustomZoneDefinition> m_Definitions;

        public IReadOnlyList<LoadFailure> LoadFailures { get; }

        public FolderCustomZoneDefinitionLibrary(string folderPath)
        {
            m_FolderPath = folderPath;
            var failures = new List<LoadFailure>();
            m_Definitions = Load(folderPath, failures);
            LoadFailures = failures;
        }

        public IReadOnlyList<CustomZoneDefinition> GetAll() => m_Definitions.Values.ToList();

        public bool TryGet(Guid id, out CustomZoneDefinition definition)
        {
            if (m_Definitions.TryGetValue(id, out var stored))
            {
                definition = stored.Clone();
                return true;
            }

            definition = null;
            return false;
        }

        public void Save(CustomZoneDefinition definition)
        {
            var hadPrevious = m_Definitions.TryGetValue(definition.Id, out var previous);
            m_Definitions[definition.Id] = definition;

            try
            {
                WriteToDisk(definition);
            }
            catch
            {
                if (hadPrevious)
                    m_Definitions[definition.Id] = previous;
                else
                    m_Definitions.Remove(definition.Id);
                throw;
            }
        }

        public void Delete(Guid id)
        {
            if (!m_Definitions.TryGetValue(id, out var removed))
                return;

            m_Definitions.Remove(id);

            try
            {
                var path = GetFilePath(m_FolderPath, id);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                m_Definitions[id] = removed;
                throw;
            }
        }

        private static string GetFilePath(string folderPath, Guid id) => Path.Combine(folderPath, $"{id}.json");

        // Same multi-definition-per-file tolerance and the same known
        // rough edge as FolderCollectionLibrary.Load - see its comment.
        private static Dictionary<Guid, CustomZoneDefinition> Load(string folderPath, List<LoadFailure> failures)
        {
            var result = new Dictionary<Guid, CustomZoneDefinition>();

            if (!Directory.Exists(folderPath))
                return result;

            foreach (var filePath in Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var dto = JsonConvert.DeserializeObject<CustomZoneDefinitionFileFormat.LibraryFileDto>(json);

                    foreach (var definitionDto in dto?.Definitions ?? Enumerable.Empty<CustomZoneDefinitionFileFormat.CustomZoneDefinitionDto>())
                    {
                        var definition = CustomZoneDefinitionFileFormat.FromDto(definitionDto);
                        result[definition.Id] = definition;
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(new LoadFailure(filePath, ex));
                }
            }

            return result;
        }

        private void WriteToDisk(CustomZoneDefinition definition)
        {
            var dto = new CustomZoneDefinitionFileFormat.LibraryFileDto
            {
                SchemaVersion = CustomZoneDefinitionFileFormat.CurrentSchemaVersion,
                Definitions = new List<CustomZoneDefinitionFileFormat.CustomZoneDefinitionDto> { CustomZoneDefinitionFileFormat.ToDto(definition) },
            };
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);

            Directory.CreateDirectory(m_FolderPath);

            var finalPath = GetFilePath(m_FolderPath, definition.Id);
            var tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
    }
}

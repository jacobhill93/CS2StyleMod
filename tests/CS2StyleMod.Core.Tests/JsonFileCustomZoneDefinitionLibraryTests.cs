using System;
using System.IO;
using CS2StyleMod.Core;
using Xunit;

namespace CS2StyleMod.Core.Tests
{
    public class JsonFileCustomZoneDefinitionLibraryTests : IDisposable
    {
        private readonly string m_FilePath;

        public JsonFileCustomZoneDefinitionLibraryTests()
        {
            m_FilePath = Path.Combine(Path.GetTempPath(), $"cs2stylemod-tests-{Guid.NewGuid()}.json");
        }

        public void Dispose()
        {
            if (File.Exists(m_FilePath))
                File.Delete(m_FilePath);
        }

        [Fact]
        public void MissingFileStartsAsAnEmptyLibrary()
        {
            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);

            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void SavedDefinitionIsFoundByIdAfterReopeningTheFile()
        {
            var collectionIdA = Guid.NewGuid();
            var collectionIdB = Guid.NewGuid();
            var definition = new CustomZoneDefinition("Mid Century Modern", ZoneType.ResidentialLow);
            definition.AddCollection(collectionIdA);
            definition.AddCollection(collectionIdB);

            new JsonFileCustomZoneDefinitionLibrary(m_FilePath).Save(definition);

            // A fresh instance simulates reopening the game.
            var reopened = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);
            Assert.True(reopened.TryGet(definition.Id, out var loaded));
            Assert.Equal("Mid Century Modern", loaded.Name);
            Assert.Equal(ZoneType.ResidentialLow, loaded.BaseZoneType);
            Assert.Equal(new[] { collectionIdA, collectionIdB }, loaded.CollectionIds);
        }

        [Fact]
        public void ConstructingWithoutAnExplicitRegionDefaultsToEurope()
        {
            var definition = new CustomZoneDefinition("Test", ZoneType.ResidentialLow);

            Assert.Equal(ZoneRegion.Europe, definition.Region);
        }

        [Fact]
        public void SavedDefinitionPersistsItsRegion()
        {
            var definition = new CustomZoneDefinition("North American Suburb", ZoneType.ResidentialLow, ZoneRegion.NorthAmerica);

            new JsonFileCustomZoneDefinitionLibrary(m_FilePath).Save(definition);

            var reopened = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);
            Assert.True(reopened.TryGet(definition.Id, out var loaded));
            Assert.Equal(ZoneRegion.NorthAmerica, loaded.Region);
        }

        [Fact]
        public void LoadingALibraryFileSavedBeforeRegionExistedDefaultsToEurope()
        {
            // Hand-written, deliberately missing the "Region" field - what
            // every library file saved before this feature existed
            // actually looks like. Must default to Europe, not
            // ZoneRegion's own zero-value (NorthAmerica) - see
            // CustomZoneDefinitionDto.Region's own comment for why.
            var id = Guid.NewGuid();
            var json = "{ \"SchemaVersion\": 1, \"Definitions\": [ "
                + $"{{ \"Id\": \"{id}\", \"Name\": \"Old Save\", \"BaseZoneType\": \"ResidentialLow\", \"CollectionIds\": [] }}"
                + " ] }";
            File.WriteAllText(m_FilePath, json);

            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);

            Assert.True(library.TryGet(id, out var loaded));
            Assert.Equal(ZoneRegion.Europe, loaded.Region);
        }

        [Fact]
        public void RenamingDoesNotChangeTheId()
        {
            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);
            var definition = new CustomZoneDefinition("Original Name", ZoneType.CommercialLow);
            library.Save(definition);
            var id = definition.Id;

            definition.Name = "Renamed";
            library.Save(definition);

            Assert.Single(library.GetAll());
            Assert.True(library.TryGet(id, out var loaded));
            Assert.Equal("Renamed", loaded.Name);
        }

        [Fact]
        public void AddCollectionIgnoresDuplicates()
        {
            var collectionId = Guid.NewGuid();
            var definition = new CustomZoneDefinition("Test", ZoneType.ResidentialLow);

            definition.AddCollection(collectionId);
            definition.AddCollection(collectionId);

            Assert.Equal(new[] { collectionId }, definition.CollectionIds);
        }

        [Fact]
        public void RemoveCollectionIsANoOpWhenNotPresent()
        {
            var definition = new CustomZoneDefinition("Test", ZoneType.ResidentialLow);

            definition.RemoveCollection(Guid.NewGuid());

            Assert.Empty(definition.CollectionIds);
        }

        [Fact]
        public void DeleteRemovesTheDefinitionAndPersists()
        {
            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);
            var definition = new CustomZoneDefinition("Temporary", ZoneType.Industrial);
            library.Save(definition);

            library.Delete(definition.Id);

            Assert.False(new JsonFileCustomZoneDefinitionLibrary(m_FilePath).TryGet(definition.Id, out _));
        }

        [Fact]
        public void DeletingAnUnknownIdIsANoOp()
        {
            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);

            library.Delete(Guid.NewGuid());

            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void TryGetOnMissingIdReturnsFalseWithoutThrowing()
        {
            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);

            var found = library.TryGet(Guid.NewGuid(), out var definition);

            Assert.False(found);
            Assert.Null(definition);
        }

        [Fact]
        public void FailedSaveOfANewDefinitionRollsBackInMemoryState()
        {
            // Pointed at a directory, not a file - see
            // JsonFileCollectionLibraryTests's identical test for why this
            // reliably fails WriteToDisk's File.Delete(m_FilePath) step.
            var library = new JsonFileCustomZoneDefinitionLibrary(Path.GetTempPath());
            var definition = new CustomZoneDefinition("Will Not Persist", ZoneType.ResidentialLow);

            Assert.ThrowsAny<Exception>(() => library.Save(definition));
            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void FailedSaveOfAnExistingDefinitionRollsBackToThePreviousValue()
        {
            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);
            var original = new CustomZoneDefinition("Original", ZoneType.ResidentialLow);
            library.Save(original);
            var id = original.Id;

            File.SetAttributes(m_FilePath, FileAttributes.ReadOnly);
            try
            {
                // TryGet -> mutate -> Save, the same pattern
                // ZoneUISystem.OnRenameZone actually uses - mutating
                // `original` directly wouldn't exercise TryGet's
                // defensive-copy behavior (see its own comment) at all,
                // and the rollback this is testing depends entirely on it.
                Assert.True(library.TryGet(id, out var toRename));
                toRename.Name = "Changed";
                Assert.ThrowsAny<Exception>(() => library.Save(toRename));

                Assert.True(library.TryGet(id, out var loaded));
                Assert.Equal("Original", loaded.Name);
            }
            finally
            {
                File.SetAttributes(m_FilePath, FileAttributes.Normal);
            }
        }

        [Fact]
        public void FailedDeleteRollsBackInMemoryState()
        {
            var library = new JsonFileCustomZoneDefinitionLibrary(m_FilePath);
            var definition = new CustomZoneDefinition("Persistent", ZoneType.Industrial);
            library.Save(definition);

            File.SetAttributes(m_FilePath, FileAttributes.ReadOnly);
            try
            {
                Assert.ThrowsAny<Exception>(() => library.Delete(definition.Id));
                Assert.True(library.TryGet(definition.Id, out _));
            }
            finally
            {
                File.SetAttributes(m_FilePath, FileAttributes.Normal);
            }
        }
    }
}

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
    }
}

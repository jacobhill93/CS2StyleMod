using System;
using System.IO;
using System.Linq;
using ZoneBuilder.Core;
using Xunit;

namespace ZoneBuilder.Core.Tests
{
    public class FolderCustomZoneDefinitionLibraryTests : IDisposable
    {
        private readonly string m_FolderPath;

        public FolderCustomZoneDefinitionLibraryTests()
        {
            m_FolderPath = Path.Combine(Path.GetTempPath(), $"cs2stylemod-tests-{Guid.NewGuid()}");
        }

        public void Dispose()
        {
            if (Directory.Exists(m_FolderPath))
                Directory.Delete(m_FolderPath, recursive: true);
        }

        [Fact]
        public void MissingFolderStartsAsAnEmptyLibrary()
        {
            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);

            Assert.Empty(library.GetAll());
            Assert.Empty(library.LoadFailures);
        }

        [Fact]
        public void SavedDefinitionIsFoundByIdAfterReopeningTheFolder()
        {
            var collectionIdA = Guid.NewGuid();
            var collectionIdB = Guid.NewGuid();
            var definition = new CustomZoneDefinition("Mid Century Modern", ZoneType.ResidentialLow);
            definition.AddCollection(collectionIdA);
            definition.AddCollection(collectionIdB);

            new FolderCustomZoneDefinitionLibrary(m_FolderPath).Save(definition);

            // A fresh instance simulates reopening the game.
            var reopened = new FolderCustomZoneDefinitionLibrary(m_FolderPath);
            Assert.True(reopened.TryGet(definition.Id, out var loaded));
            Assert.Equal("Mid Century Modern", loaded.Name);
            Assert.Equal(ZoneType.ResidentialLow, loaded.BaseZoneType);
            Assert.Equal(new[] { collectionIdA, collectionIdB }, loaded.CollectionIds);
        }

        [Fact]
        public void EachDefinitionGetsItsOwnFileNamedByItsId()
        {
            var definition = new CustomZoneDefinition("Solo", ZoneType.ResidentialLow);
            new FolderCustomZoneDefinitionLibrary(m_FolderPath).Save(definition);

            var expectedPath = Path.Combine(m_FolderPath, $"{definition.Id}.json");
            Assert.True(File.Exists(expectedPath));
            Assert.Single(Directory.GetFiles(m_FolderPath, "*.json"));
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

            new FolderCustomZoneDefinitionLibrary(m_FolderPath).Save(definition);

            var reopened = new FolderCustomZoneDefinitionLibrary(m_FolderPath);
            Assert.True(reopened.TryGet(definition.Id, out var loaded));
            Assert.Equal(ZoneRegion.NorthAmerica, loaded.Region);
        }

        [Fact]
        public void LoadingADefinitionFileSavedBeforeRegionExistedDefaultsToEurope()
        {
            // Hand-written, deliberately missing the "Region" field - what
            // every definition file saved before this feature existed
            // actually looks like. Must default to Europe, not
            // ZoneRegion's own zero-value (NorthAmerica) - see
            // CustomZoneDefinitionDto.Region's own comment for why.
            var id = Guid.NewGuid();
            Directory.CreateDirectory(m_FolderPath);
            var json = "{ \"SchemaVersion\": 1, \"Definitions\": [ "
                + $"{{ \"Id\": \"{id}\", \"Name\": \"Old Save\", \"BaseZoneType\": \"ResidentialLow\", \"CollectionIds\": [] }}"
                + " ] }";
            File.WriteAllText(Path.Combine(m_FolderPath, $"{id}.json"), json);

            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);

            Assert.True(library.TryGet(id, out var loaded));
            Assert.Equal(ZoneRegion.Europe, loaded.Region);
        }

        [Fact]
        public void RenamingDoesNotChangeTheId()
        {
            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);
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
        public void DeleteRemovesTheDefinitionAndItsFile()
        {
            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);
            var definition = new CustomZoneDefinition("Temporary", ZoneType.Industrial);
            library.Save(definition);
            var filePath = Path.Combine(m_FolderPath, $"{definition.Id}.json");

            library.Delete(definition.Id);

            Assert.False(File.Exists(filePath));
            Assert.False(new FolderCustomZoneDefinitionLibrary(m_FolderPath).TryGet(definition.Id, out _));
        }

        [Fact]
        public void DeletingAnUnknownIdIsANoOp()
        {
            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);

            library.Delete(Guid.NewGuid());

            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void TryGetOnMissingIdReturnsFalseWithoutThrowing()
        {
            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);

            var found = library.TryGet(Guid.NewGuid(), out var definition);

            Assert.False(found);
            Assert.Null(definition);
        }

        [Fact]
        public void FailedSaveOfANewDefinitionRollsBackInMemoryState()
        {
            // Pointed at a file, not a folder - see
            // FolderCollectionLibraryTests's identical test for why this
            // reliably fails Directory.CreateDirectory inside WriteToDisk.
            var blockingFilePath = Path.Combine(Path.GetTempPath(), $"cs2stylemod-tests-blocker-{Guid.NewGuid()}");
            File.WriteAllText(blockingFilePath, "not a folder");
            try
            {
                var library = new FolderCustomZoneDefinitionLibrary(blockingFilePath);
                var definition = new CustomZoneDefinition("Will Not Persist", ZoneType.ResidentialLow);

                Assert.ThrowsAny<Exception>(() => library.Save(definition));
                Assert.Empty(library.GetAll());
            }
            finally
            {
                File.Delete(blockingFilePath);
            }
        }

        [Fact]
        public void FailedSaveOfAnExistingDefinitionRollsBackToThePreviousValue()
        {
            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);
            var original = new CustomZoneDefinition("Original", ZoneType.ResidentialLow);
            library.Save(original);
            var id = original.Id;
            var filePath = Path.Combine(m_FolderPath, $"{id}.json");

            File.SetAttributes(filePath, FileAttributes.ReadOnly);
            try
            {
                // TryGet -> mutate -> Save, the same pattern
                // ZoneUISystem.OnRenameZone actually uses.
                Assert.True(library.TryGet(id, out var toRename));
                toRename.Name = "Changed";
                Assert.ThrowsAny<Exception>(() => library.Save(toRename));

                Assert.True(library.TryGet(id, out var loaded));
                Assert.Equal("Original", loaded.Name);
            }
            finally
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
        }

        [Fact]
        public void FailedDeleteRollsBackInMemoryState()
        {
            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);
            var definition = new CustomZoneDefinition("Persistent", ZoneType.Industrial);
            library.Save(definition);
            var filePath = Path.Combine(m_FolderPath, $"{definition.Id}.json");

            File.SetAttributes(filePath, FileAttributes.ReadOnly);
            try
            {
                Assert.ThrowsAny<Exception>(() => library.Delete(definition.Id));
                Assert.True(library.TryGet(definition.Id, out _));
            }
            finally
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
        }

        [Fact]
        public void AMalformedFileIsIsolatedAndReportedWithoutBreakingOtherDefinitions()
        {
            Directory.CreateDirectory(m_FolderPath);
            File.WriteAllText(Path.Combine(m_FolderPath, "broken.json"), "{ not valid json ");

            var good = new CustomZoneDefinition("Good", ZoneType.ResidentialLow);
            new FolderCustomZoneDefinitionLibrary(m_FolderPath).Save(good);

            var library = new FolderCustomZoneDefinitionLibrary(m_FolderPath);

            Assert.Equal("Good", Assert.Single(library.GetAll()).Name);
            var failure = Assert.Single(library.LoadFailures);
            Assert.Contains("broken.json", failure.FilePath);
            Assert.NotNull(failure.Error);
        }
    }
}

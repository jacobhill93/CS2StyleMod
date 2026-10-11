using System;
using System.IO;
using System.Linq;
using CS2StyleMod.Core;
using Xunit;

namespace CS2StyleMod.Core.Tests
{
    public class JsonFileCollectionLibraryTests : IDisposable
    {
        private readonly string m_FilePath;

        public JsonFileCollectionLibraryTests()
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
            var library = new JsonFileCollectionLibrary(m_FilePath);

            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void SavedCollectionIsFoundByIdAfterReopeningTheFile()
        {
            var collection = new Collection("Brownstones");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow, assetPackId: "NA"));
            collection.Add(new AdoptedAssetEntry("adopted-house", new AdoptedAssetSettings(ZoneType.ResidentialLow, 2, new LotSize(2, 3), householdCount: 4)));

            new JsonFileCollectionLibrary(m_FilePath).Save(collection);

            // A fresh instance simulates reopening the game.
            var reopened = new JsonFileCollectionLibrary(m_FilePath);
            Assert.True(reopened.TryGet(collection.Id, out var loaded));
            Assert.Equal("Brownstones", loaded.Name);
            Assert.Equal(2, loaded.Entries.Count);

            var selector = Assert.IsType<VanillaSelectorEntry>(loaded.Entries[0]);
            Assert.Equal(ZoneType.ResidentialLow, selector.ZoneType);
            Assert.Equal("NA", selector.AssetPackId);

            var adopted = Assert.IsType<AdoptedAssetEntry>(loaded.Entries[1]);
            Assert.Equal("adopted-house", adopted.PrefabId);
            Assert.Equal(4, adopted.Settings.HouseholdCount);
            Assert.Equal(3, adopted.Settings.LotSize.Depth);
        }

        [Fact]
        public void RenamingDoesNotChangeTheId()
        {
            var library = new JsonFileCollectionLibrary(m_FilePath);
            var collection = new Collection("Original Name");
            library.Save(collection);
            var id = collection.Id;

            collection.Name = "Renamed";
            library.Save(collection);

            Assert.Single(library.GetAll());
            Assert.True(library.TryGet(id, out var loaded));
            Assert.Equal("Renamed", loaded.Name);
        }

        [Fact]
        public void DeleteRemovesTheCollectionAndPersists()
        {
            var library = new JsonFileCollectionLibrary(m_FilePath);
            var collection = new Collection("Temporary");
            library.Save(collection);

            library.Delete(collection.Id);

            Assert.False(new JsonFileCollectionLibrary(m_FilePath).TryGet(collection.Id, out _));
        }

        [Fact]
        public void DeletingAnUnknownIdIsANoOp()
        {
            var library = new JsonFileCollectionLibrary(m_FilePath);

            library.Delete(Guid.NewGuid());

            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void TryGetOnMissingIdReturnsFalseWithoutThrowing()
        {
            var library = new JsonFileCollectionLibrary(m_FilePath);

            var found = library.TryGet(Guid.NewGuid(), out var collection);

            Assert.False(found);
            Assert.Null(collection);
        }

        [Fact]
        public void FailedSaveOfANewCollectionRollsBackInMemoryState()
        {
            // Pointed at a directory, not a file - WriteToDisk's
            // File.Delete(m_FilePath) step reliably fails (you can't
            // delete a directory via File.Delete), a deterministic way to
            // simulate a real write failure (disk full, file locked,
            // permissions denied) without racy file-attribute timing.
            var library = new JsonFileCollectionLibrary(Path.GetTempPath());
            var collection = new Collection("Will Not Persist");

            Assert.ThrowsAny<Exception>(() => library.Save(collection));
            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void FailedSaveOfAnExistingCollectionRollsBackToThePreviousValue()
        {
            var library = new JsonFileCollectionLibrary(m_FilePath);
            var original = new Collection("Original");
            library.Save(original);
            var id = original.Id;

            File.SetAttributes(m_FilePath, FileAttributes.ReadOnly);
            try
            {
                // TryGet -> mutate -> Save, the same pattern
                // CollectionUISystem.OnRenameCollection actually uses -
                // mutating `original` directly wouldn't exercise TryGet's
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
            var library = new JsonFileCollectionLibrary(m_FilePath);
            var collection = new Collection("Persistent");
            library.Save(collection);

            File.SetAttributes(m_FilePath, FileAttributes.ReadOnly);
            try
            {
                Assert.ThrowsAny<Exception>(() => library.Delete(collection.Id));
                Assert.True(library.TryGet(collection.Id, out _));
            }
            finally
            {
                File.SetAttributes(m_FilePath, FileAttributes.Normal);
            }
        }
    }
}

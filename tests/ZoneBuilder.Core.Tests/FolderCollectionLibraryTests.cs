using System;
using System.IO;
using System.Linq;
using ZoneBuilder.Core;
using Xunit;

namespace ZoneBuilder.Core.Tests
{
    public class FolderCollectionLibraryTests : IDisposable
    {
        private readonly string m_FolderPath;

        public FolderCollectionLibraryTests()
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
            var library = new FolderCollectionLibrary(m_FolderPath);

            Assert.Empty(library.GetAll());
            Assert.Empty(library.LoadFailures);
        }

        [Fact]
        public void SavedCollectionIsFoundByIdAfterReopeningTheFolder()
        {
            var collection = new Collection("Brownstones");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow, assetPackId: "NA"));
            collection.Add(new AdoptedAssetEntry("adopted-house", new AdoptedAssetSettings(ZoneType.ResidentialLow, 2, new LotSize(2, 3), householdCount: 4)));

            new FolderCollectionLibrary(m_FolderPath).Save(collection);

            // A fresh instance simulates reopening the game.
            var reopened = new FolderCollectionLibrary(m_FolderPath);
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
        public void EachCollectionGetsItsOwnFileNamedByItsId()
        {
            var collection = new Collection("Solo");
            new FolderCollectionLibrary(m_FolderPath).Save(collection);

            var expectedPath = Path.Combine(m_FolderPath, $"{collection.Id}.json");
            Assert.True(File.Exists(expectedPath));
            Assert.Single(Directory.GetFiles(m_FolderPath, "*.json"));
        }

        [Fact]
        public void RenamingDoesNotChangeTheId()
        {
            var library = new FolderCollectionLibrary(m_FolderPath);
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
        public void DeleteRemovesTheCollectionAndItsFile()
        {
            var library = new FolderCollectionLibrary(m_FolderPath);
            var collection = new Collection("Temporary");
            library.Save(collection);
            var filePath = Path.Combine(m_FolderPath, $"{collection.Id}.json");

            library.Delete(collection.Id);

            Assert.False(File.Exists(filePath));
            Assert.False(new FolderCollectionLibrary(m_FolderPath).TryGet(collection.Id, out _));
        }

        [Fact]
        public void DeletingAnUnknownIdIsANoOp()
        {
            var library = new FolderCollectionLibrary(m_FolderPath);

            library.Delete(Guid.NewGuid());

            Assert.Empty(library.GetAll());
        }

        [Fact]
        public void TryGetOnMissingIdReturnsFalseWithoutThrowing()
        {
            var library = new FolderCollectionLibrary(m_FolderPath);

            var found = library.TryGet(Guid.NewGuid(), out var collection);

            Assert.False(found);
            Assert.Null(collection);
        }

        [Fact]
        public void FailedSaveOfANewCollectionRollsBackInMemoryState()
        {
            // Pointed at a file, not a folder - Directory.CreateDirectory
            // inside WriteToDisk reliably fails when a file already
            // occupies that path, a deterministic way to simulate a real
            // write failure without racy file-attribute timing.
            var blockingFilePath = Path.Combine(Path.GetTempPath(), $"cs2stylemod-tests-blocker-{Guid.NewGuid()}");
            File.WriteAllText(blockingFilePath, "not a folder");
            try
            {
                var library = new FolderCollectionLibrary(blockingFilePath);
                var collection = new Collection("Will Not Persist");

                Assert.ThrowsAny<Exception>(() => library.Save(collection));
                Assert.Empty(library.GetAll());
            }
            finally
            {
                File.Delete(blockingFilePath);
            }
        }

        [Fact]
        public void FailedSaveOfAnExistingCollectionRollsBackToThePreviousValue()
        {
            var library = new FolderCollectionLibrary(m_FolderPath);
            var original = new Collection("Original");
            library.Save(original);
            var id = original.Id;
            var filePath = Path.Combine(m_FolderPath, $"{id}.json");

            File.SetAttributes(filePath, FileAttributes.ReadOnly);
            try
            {
                // TryGet -> mutate -> Save, the same pattern
                // CollectionUISystem.OnRenameCollection actually uses.
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
            var library = new FolderCollectionLibrary(m_FolderPath);
            var collection = new Collection("Persistent");
            library.Save(collection);
            var filePath = Path.Combine(m_FolderPath, $"{collection.Id}.json");

            File.SetAttributes(filePath, FileAttributes.ReadOnly);
            try
            {
                Assert.ThrowsAny<Exception>(() => library.Delete(collection.Id));
                Assert.True(library.TryGet(collection.Id, out _));
            }
            finally
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
        }

        [Fact]
        public void AMalformedFileIsIsolatedAndReportedWithoutBreakingOtherCollections()
        {
            Directory.CreateDirectory(m_FolderPath);
            File.WriteAllText(Path.Combine(m_FolderPath, "broken.json"), "{ not valid json ");

            var good = new Collection("Good");
            new FolderCollectionLibrary(m_FolderPath).Save(good);

            var library = new FolderCollectionLibrary(m_FolderPath);

            Assert.Equal("Good", Assert.Single(library.GetAll()).Name);
            var failure = Assert.Single(library.LoadFailures);
            Assert.Contains("broken.json", failure.FilePath);
            Assert.NotNull(failure.Error);
        }

        [Fact]
        public void AFileWithNoSchemaVersionIsIsolatedAndReportedRatherThanSilentlyTrustedAsCurrent()
        {
            // No "SchemaVersion" property at all reads as version 0 (see
            // SchemaMigrationTests) - with no migration registered from
            // 0 yet, this must fail loudly and be isolated the same way
            // a malformed file already is, not get deserialized as if it
            // were already CollectionFileFormat.CurrentSchemaVersion.
            Directory.CreateDirectory(m_FolderPath);
            File.WriteAllText(Path.Combine(m_FolderPath, "pre-versioning.json"), @"
                { ""Collections"": [
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""No version"", ""Entries"": [] }
                ] }");

            var good = new Collection("Good");
            new FolderCollectionLibrary(m_FolderPath).Save(good);

            var library = new FolderCollectionLibrary(m_FolderPath);

            Assert.Equal("Good", Assert.Single(library.GetAll()).Name);
            var failure = Assert.Single(library.LoadFailures);
            Assert.Contains("pre-versioning.json", failure.FilePath);
            Assert.Contains("schema version 0", failure.Error.Message);
        }

        [Fact]
        public void ATolerantlyLoadedMultiCollectionFileLoadsAllOfThem()
        {
            // Not something this library ever writes itself (always one
            // collection per file, see EachCollectionGetsItsOwnFileNamedByItsId),
            // but a manually-placed file holding more than one should
            // still load all of them rather than being rejected outright.
            Directory.CreateDirectory(m_FolderPath);
            File.WriteAllText(Path.Combine(m_FolderPath, "manual.json"), @"
                { ""SchemaVersion"": 1, ""Collections"": [
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""First"", ""Entries"": [] },
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""Second"", ""Entries"": [] }
                  ] }");

            var library = new FolderCollectionLibrary(m_FolderPath);

            Assert.Equal(new[] { "First", "Second" }, library.GetAll().Select(c => c.Name).OrderBy(n => n));
        }
    }
}

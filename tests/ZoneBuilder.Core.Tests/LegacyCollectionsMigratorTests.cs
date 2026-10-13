using System;
using System.IO;
using System.Linq;
using ZoneBuilder.Core;
using Xunit;

namespace ZoneBuilder.Core.Tests
{
    public class LegacyCollectionsMigratorTests : IDisposable
    {
        private readonly string m_LegacyFilePath;
        private readonly string m_CollectionsFolderPath;

        public LegacyCollectionsMigratorTests()
        {
            var root = Path.Combine(Path.GetTempPath(), $"cs2stylemod-migrate-tests-{Guid.NewGuid()}");
            m_LegacyFilePath = Path.Combine(root, "collections.json");
            m_CollectionsFolderPath = Path.Combine(root, "Collections");
        }

        public void Dispose()
        {
            var root = Path.GetDirectoryName(m_LegacyFilePath);
            if (root != null && Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }

        [Fact]
        public void NoLegacyFileMeansNothingAttempted()
        {
            var result = LegacyCollectionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_CollectionsFolderPath);

            Assert.False(result.Attempted);
            Assert.Equal(0, result.MigratedCount);
            Assert.False(Directory.Exists(m_CollectionsFolderPath));
        }

        [Fact]
        public void ALegacyFileWithMultipleCollectionsSplitsIntoOneFileEach()
        {
            var idA = Guid.NewGuid();
            var idB = Guid.NewGuid();
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            File.WriteAllText(m_LegacyFilePath, @"
                { ""SchemaVersion"": 1, ""Collections"": [
                    { ""Id"": """ + idA + @""", ""Name"": ""Alpha"", ""Entries"": [
                        { ""Kind"": ""explicitAsset"", ""PrefabId"": ""House1"" }
                      ] },
                    { ""Id"": """ + idB + @""", ""Name"": ""Beta"", ""Entries"": [] }
                  ] }");

            var result = LegacyCollectionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_CollectionsFolderPath);

            Assert.True(result.Attempted);
            Assert.Equal(2, result.MigratedCount);
            Assert.Empty(result.CollectionFailures);
            Assert.Null(result.FatalError);

            var library = new FolderCollectionLibrary(m_CollectionsFolderPath);
            Assert.Equal(new[] { "Alpha", "Beta" }, library.GetAll().Select(c => c.Name).OrderBy(n => n));
            // Original stored Ids are preserved - migration isn't an
            // "import," existing references (a CustomZoneDefinition's
            // CollectionIds) must keep resolving to the same collection.
            Assert.True(library.TryGet(idA, out var alpha));
            Assert.Equal("House1", Assert.IsType<ExplicitAssetEntry>(Assert.Single(alpha.Entries)).PrefabId);
        }

        [Fact]
        public void TheLegacyFileIsRenamedNotDeletedAfterMigrating()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            File.WriteAllText(m_LegacyFilePath, @"{ ""SchemaVersion"": 1, ""Collections"": [] }");

            LegacyCollectionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_CollectionsFolderPath);

            Assert.False(File.Exists(m_LegacyFilePath));
            Assert.True(File.Exists(m_LegacyFilePath + ".migrated"));
        }

        [Fact]
        public void MigrationNeverRunsTwice()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            File.WriteAllText(m_LegacyFilePath, @"
                { ""SchemaVersion"": 1, ""Collections"": [
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""Once"", ""Entries"": [] }
                  ] }");

            LegacyCollectionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_CollectionsFolderPath);
            // The legacy file is gone now (renamed) - a second call simulates
            // a second launch finding no legacy file left to migrate.
            var secondResult = LegacyCollectionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_CollectionsFolderPath);

            Assert.False(secondResult.Attempted);
            Assert.Single(new FolderCollectionLibrary(m_CollectionsFolderPath).GetAll());
        }

        [Fact]
        public void ACorruptedLegacyFileIsLeftInPlaceAndReportedAsFatal()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            File.WriteAllText(m_LegacyFilePath, "{ not valid json ");

            var result = LegacyCollectionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_CollectionsFolderPath);

            Assert.True(result.Attempted);
            Assert.NotNull(result.FatalError);
            Assert.Equal(0, result.MigratedCount);
            Assert.True(File.Exists(m_LegacyFilePath)); // untouched, not renamed
            Assert.False(Directory.Exists(m_CollectionsFolderPath));
        }

        [Fact]
        public void OneBadCollectionDoesNotBlockMigratingTheRest()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            // "unknownKind" isn't a real EntryDto.Kind - CollectionFileFormat.FromDto
            // throws NotSupportedException for it, simulating a single
            // corrupted entry inside an otherwise-valid legacy file.
            File.WriteAllText(m_LegacyFilePath, @"
                { ""SchemaVersion"": 1, ""Collections"": [
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""Bad"", ""Entries"": [
                        { ""Kind"": ""unknownKind"" }
                      ] },
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""Good"", ""Entries"": [] }
                  ] }");

            var result = LegacyCollectionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_CollectionsFolderPath);

            Assert.Equal(1, result.MigratedCount);
            var failure = Assert.Single(result.CollectionFailures);
            Assert.Equal("Bad", failure.Name);
            Assert.Equal("Good", Assert.Single(new FolderCollectionLibrary(m_CollectionsFolderPath).GetAll()).Name);
            // Still retired - everything extractable was extracted.
            Assert.False(File.Exists(m_LegacyFilePath));
        }
    }
}

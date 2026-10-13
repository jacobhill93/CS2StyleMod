using System;
using System.IO;
using System.Linq;
using ZoneBuilder.Core;
using Xunit;

namespace ZoneBuilder.Core.Tests
{
    public class LegacyCustomZoneDefinitionsMigratorTests : IDisposable
    {
        private readonly string m_LegacyFilePath;
        private readonly string m_DefinitionsFolderPath;

        public LegacyCustomZoneDefinitionsMigratorTests()
        {
            var root = Path.Combine(Path.GetTempPath(), $"cs2stylemod-migrate-zone-tests-{Guid.NewGuid()}");
            m_LegacyFilePath = Path.Combine(root, "customZones.json");
            m_DefinitionsFolderPath = Path.Combine(root, "Zones");
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
            var result = LegacyCustomZoneDefinitionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_DefinitionsFolderPath);

            Assert.False(result.Attempted);
            Assert.False(Directory.Exists(m_DefinitionsFolderPath));
        }

        [Fact]
        public void ALegacyFileWithMultipleDefinitionsSplitsIntoOneFileEach()
        {
            var idA = Guid.NewGuid();
            var idB = Guid.NewGuid();
            var collectionId = Guid.NewGuid();
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            File.WriteAllText(m_LegacyFilePath, @"
                { ""SchemaVersion"": 1, ""Definitions"": [
                    { ""Id"": """ + idA + @""", ""Name"": ""Suburbs"", ""BaseZoneType"": ""ResidentialLow"", ""Region"": ""NorthAmerica"", ""CollectionIds"": [""" + collectionId + @"""] },
                    { ""Id"": """ + idB + @""", ""Name"": ""Downtown"", ""BaseZoneType"": ""CommercialHigh"", ""CollectionIds"": [] }
                  ] }");

            var result = LegacyCustomZoneDefinitionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_DefinitionsFolderPath);

            Assert.True(result.Attempted);
            Assert.Equal(2, result.MigratedCount);
            Assert.Empty(result.DefinitionFailures);

            var library = new FolderCustomZoneDefinitionLibrary(m_DefinitionsFolderPath);
            Assert.Equal(new[] { "Downtown", "Suburbs" }, library.GetAll().Select(d => d.Name).OrderBy(n => n));
            Assert.True(library.TryGet(idA, out var suburbs));
            Assert.Equal(ZoneRegion.NorthAmerica, suburbs.Region);
            Assert.Equal(new[] { collectionId }, suburbs.CollectionIds);
        }

        [Fact]
        public void TheLegacyFileIsRenamedNotDeletedAfterMigrating()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            File.WriteAllText(m_LegacyFilePath, @"{ ""SchemaVersion"": 1, ""Definitions"": [] }");

            LegacyCustomZoneDefinitionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_DefinitionsFolderPath);

            Assert.False(File.Exists(m_LegacyFilePath));
            Assert.True(File.Exists(m_LegacyFilePath + ".migrated"));
        }

        [Fact]
        public void ACorruptedLegacyFileIsLeftInPlaceAndReportedAsFatal()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_LegacyFilePath)!);
            File.WriteAllText(m_LegacyFilePath, "{ not valid json ");

            var result = LegacyCustomZoneDefinitionsMigrator.MigrateIfNeeded(m_LegacyFilePath, m_DefinitionsFolderPath);

            Assert.True(result.Attempted);
            Assert.NotNull(result.FatalError);
            Assert.True(File.Exists(m_LegacyFilePath));
            Assert.False(Directory.Exists(m_DefinitionsFolderPath));
        }
    }
}

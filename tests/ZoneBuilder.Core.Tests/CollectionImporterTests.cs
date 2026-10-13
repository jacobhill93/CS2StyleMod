using System;
using System.IO;
using System.Linq;
using ZoneBuilder.Core;
using Xunit;

namespace ZoneBuilder.Core.Tests
{
    public class CollectionImporterTests : IDisposable
    {
        private readonly string m_ImportsFolder;
        private readonly string m_LibraryFilePath;

        public CollectionImporterTests()
        {
            m_ImportsFolder = Path.Combine(Path.GetTempPath(), $"cs2stylemod-import-tests-{Guid.NewGuid()}");
            m_LibraryFilePath = Path.Combine(Path.GetTempPath(), $"cs2stylemod-import-tests-library-{Guid.NewGuid()}");
        }

        public void Dispose()
        {
            if (Directory.Exists(m_ImportsFolder))
                Directory.Delete(m_ImportsFolder, recursive: true);
            if (Directory.Exists(m_LibraryFilePath))
                Directory.Delete(m_LibraryFilePath, recursive: true);
        }

        private void WriteImportFile(string fileName, string json)
        {
            Directory.CreateDirectory(m_ImportsFolder);
            File.WriteAllText(Path.Combine(m_ImportsFolder, fileName), json);
        }

        [Fact]
        public void MissingFolderImportsNothingWithoutThrowing()
        {
            var library = new FolderCollectionLibrary(m_LibraryFilePath);

            var result = CollectionImporter.ImportFromFolder(m_ImportsFolder, library);

            Assert.Equal(0, result.ImportedCollectionCount);
            Assert.Empty(result.Failures);
        }

        [Fact]
        public void ASingleCollectionSnippetIsImportedWithAFreshId()
        {
            var originalId = Guid.NewGuid();
            WriteImportFile("snippet.json", $@"
                {{
                  ""SchemaVersion"": 1,
                  ""Collections"": [
                    {{
                      ""Id"": ""{originalId}"",
                      ""Name"": ""Imported Test"",
                      ""Entries"": [
                        {{ ""Kind"": ""explicitAsset"", ""PrefabId"": ""PittHouse15"" }}
                      ]
                    }}
                  ]
                }}");
            var library = new FolderCollectionLibrary(m_LibraryFilePath);

            var result = CollectionImporter.ImportFromFolder(m_ImportsFolder, library);

            Assert.Equal(1, result.ImportedCollectionCount);
            Assert.Empty(result.Failures);

            var imported = Assert.Single(library.GetAll());
            Assert.Equal("Imported Test", imported.Name);
            Assert.NotEqual(originalId, imported.Id); // never trust the file's own stored Id
            var entry = Assert.IsType<ExplicitAssetEntry>(Assert.Single(imported.Entries));
            Assert.Equal("PittHouse15", entry.PrefabId);
        }

        [Fact]
        public void AFileWithMultipleCollectionsImportsAllOfThem()
        {
            WriteImportFile("whole-library.json", @"
                {
                  ""SchemaVersion"": 1,
                  ""Collections"": [
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""One"", ""Entries"": [] },
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""Two"", ""Entries"": [] }
                  ]
                }");
            var library = new FolderCollectionLibrary(m_LibraryFilePath);

            var result = CollectionImporter.ImportFromFolder(m_ImportsFolder, library);

            Assert.Equal(2, result.ImportedCollectionCount);
            Assert.Equal(new[] { "One", "Two" }, library.GetAll().Select(c => c.Name).OrderBy(n => n));
        }

        [Fact]
        public void SuccessfullyImportedFilesAreMovedToProcessedSoTheyDoNotReimport()
        {
            WriteImportFile("snippet.json", @"
                { ""SchemaVersion"": 1, ""Collections"": [
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""Imported"", ""Entries"": [] }
                  ] }");
            var library = new FolderCollectionLibrary(m_LibraryFilePath);

            CollectionImporter.ImportFromFolder(m_ImportsFolder, library);

            Assert.False(File.Exists(Path.Combine(m_ImportsFolder, "snippet.json")));
            Assert.True(File.Exists(Path.Combine(m_ImportsFolder, "Processed", "snippet.json")));

            // Re-running the scan (simulating a second launch) must not
            // duplicate the already-processed collection.
            var secondRun = CollectionImporter.ImportFromFolder(m_ImportsFolder, library);
            Assert.Equal(0, secondRun.ImportedCollectionCount);
            Assert.Single(library.GetAll());
        }

        [Fact]
        public void AMalformedFileIsReportedAsAFailureAndLeftInPlace()
        {
            WriteImportFile("broken.json", "{ this is not valid json ");
            var library = new FolderCollectionLibrary(m_LibraryFilePath);

            var result = CollectionImporter.ImportFromFolder(m_ImportsFolder, library);

            Assert.Equal(0, result.ImportedCollectionCount);
            var failure = Assert.Single(result.Failures);
            Assert.Contains("broken.json", failure.FilePath);
            Assert.NotNull(failure.Error);
            Assert.True(File.Exists(Path.Combine(m_ImportsFolder, "broken.json")));
            Assert.False(Directory.Exists(Path.Combine(m_ImportsFolder, "Processed")));
        }

        [Fact]
        public void AMalformedFileDoesNotStopOtherFilesInTheSameFolderFromImporting()
        {
            WriteImportFile("broken.json", "{ not valid ");
            WriteImportFile("good.json", @"
                { ""SchemaVersion"": 1, ""Collections"": [
                    { ""Id"": """ + Guid.NewGuid() + @""", ""Name"": ""Good"", ""Entries"": [] }
                  ] }");
            var library = new FolderCollectionLibrary(m_LibraryFilePath);

            var result = CollectionImporter.ImportFromFolder(m_ImportsFolder, library);

            Assert.Equal(1, result.ImportedCollectionCount);
            Assert.Single(result.Failures);
            Assert.Equal("Good", Assert.Single(library.GetAll()).Name);
            Assert.True(File.Exists(Path.Combine(m_ImportsFolder, "broken.json")));
            Assert.True(File.Exists(Path.Combine(m_ImportsFolder, "Processed", "good.json")));
        }
    }
}

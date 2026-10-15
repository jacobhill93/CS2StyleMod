using System;
using ZoneBuilder.Core;
using Xunit;

namespace ZoneBuilder.Core.Tests
{
    public class SchemaMigrationTests
    {
        [Fact]
        public void MissingSchemaVersionPropertyReadsAsVersionZero()
        {
            Assert.Equal(0, SchemaMigration.ReadVersion(@"{ ""Name"": ""no version field at all"" }"));
        }

        [Fact]
        public void AlreadyAtCurrentVersionNeedsNoMigrationAndIsUnchanged()
        {
            var json = @"{ ""SchemaVersion"": 1, ""Name"": ""already current"" }";

            var result = SchemaMigration.Migrate(json, currentVersion: 1, steps: Array.Empty<SchemaMigrationStep>());

            Assert.Equal(json, result);
            Assert.Equal(1, SchemaMigration.ReadVersion(result));
        }

        [Fact]
        public void AMissingVersionWithNoRegisteredStepFromZeroThrowsInsteadOfGuessing()
        {
            var json = @"{ ""Name"": ""no version field"" }";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SchemaMigration.Migrate(json, currentVersion: 1, steps: Array.Empty<SchemaMigrationStep>()));
            Assert.Contains("schema version 0", ex.Message);
        }

        [Fact]
        public void AnUnregisteredIntermediateVersionThrows()
        {
            var json = @"{ ""SchemaVersion"": 1 }";
            var steps = new[]
            {
                // Only covers 1 -> 2; migrating to 3 needs a 2 -> 3 step
                // this test deliberately never registers.
                new SchemaMigrationStep(1, j => @"{ ""SchemaVersion"": 2 }"),
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SchemaMigration.Migrate(json, currentVersion: 3, steps: steps));
            Assert.Contains("schema version 2", ex.Message);
        }

        [Fact]
        public void ChainsMultipleStepsInOrderUntilReachingCurrentVersion()
        {
            // Simulates a rename (v1 -> v2) followed by a restructure
            // (v2 -> v3) - the two kinds of change a plain C# DTO default
            // can't express, which is the whole reason this pipeline
            // exists instead of relying on Newtonsoft's own defaulting.
            var json = @"{ ""SchemaVersion"": 1, ""OldName"": ""value"" }";
            var steps = new[]
            {
                new SchemaMigrationStep(1, j => @"{ ""SchemaVersion"": 2, ""NewName"": ""value"" }"),
                new SchemaMigrationStep(2, j => @"{ ""SchemaVersion"": 3, ""Wrapped"": { ""Inner"": ""value"" } }"),
            };

            var result = SchemaMigration.Migrate(json, currentVersion: 3, steps: steps);

            Assert.Equal(3, SchemaMigration.ReadVersion(result));
            Assert.DoesNotContain("OldName", result);
            Assert.DoesNotContain("NewName", result);
            Assert.Contains(@"""Inner"": ""value""", result);
        }

        [Fact]
        public void AStepThatDoesNotAdvanceTheVersionItClaimsToThrows()
        {
            var json = @"{ ""SchemaVersion"": 1 }";
            var steps = new[]
            {
                // Bug: claims to migrate from 1, but forgets to bump
                // SchemaVersion - must be caught, not looped forever or
                // silently accepted as "done".
                new SchemaMigrationStep(1, j => j),
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SchemaMigration.Migrate(json, currentVersion: 2, steps: steps));
            Assert.Contains("did not advance", ex.Message);
        }
    }
}

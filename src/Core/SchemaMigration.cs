using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ZoneBuilder.Core
{
    // Generic version-by-version JSON migration pipeline, shared by every
    // persisted file format in this mod (CollectionFileFormat,
    // CustomZoneDefinitionFileFormat, and any future one). Each step is
    // raw-JSON-string-in, raw-JSON-string-out - deliberately NOT
    // Newtonsoft.Json.Linq.JObject, even though that would be the more
    // natural way to express a generic rename/restructure. The game's
    // own bundled Newtonsoft.Json.dll (which ZoneBuilder.csproj - the
    // net48 game-facing assembly - references) fails to compile against
    // JObject with a CS1705 assembly-version mismatch
    // (Newtonsoft.Json needs a newer System.Runtime than this project's
    // own reference resolves to); plain JsonConvert.DeserializeObject<T>/
    // SerializeObject - the only two members actually used here - don't
    // trigger it, matching every other JSON call site in this codebase.
    // A real future migration step can parse/rebuild its own JSON
    // however it needs to (typically a pair of purpose-built "OldShape"/
    // "NewShape" DTOs deserialized/reserialized via those same two safe
    // methods), it just has to do so as a string -> string transform.
    internal static class SchemaMigration
    {
        // Only used to read the "SchemaVersion" property in isolation,
        // without touching the rest of a file's shape - safe against any
        // future rename of other fields.
        private sealed class VersionProbe
        {
            public int? SchemaVersion { get; set; }
        }

        // 0 = genuinely no "SchemaVersion" property present in the JSON
        // at all - a file written before this mechanism existed, or
        // something hand-edited/foreign. Deliberately nullable on the
        // probe (not defaulted to CurrentSchemaVersion) so "missing"
        // reads as a real, distinct 0, not silently "already current" -
        // that's exactly the trap a plain non-nullable C# property
        // default would fall into the moment CurrentSchemaVersion is
        // ever bumped past 1.
        public static int ReadVersion(string json)
        {
            return JsonConvert.DeserializeObject<VersionProbe>(json)?.SchemaVersion ?? 0;
        }

        // Applies registered steps in sequence until the document's own
        // SchemaVersion reaches currentVersion. Throws (not a silent
        // best-effort) if no step covers the version actually found, or
        // if a step's own result doesn't advance the version it claims
        // to - callers (the Folder*Library/migrator/importer load paths)
        // already isolate a per-file failure from every other file, the
        // same way a corrupt/unparseable file already does, so failing
        // loudly here is safe and preferred over guessing.
        public static string Migrate(string json, int currentVersion, IReadOnlyList<SchemaMigrationStep> steps)
        {
            var version = ReadVersion(json);
            while (version < currentVersion)
            {
                var step = steps.FirstOrDefault(s => s.FromVersion == version);
                if (step == null)
                    throw new InvalidOperationException($"No migration registered from schema version {version} (current: {currentVersion}).");

                json = step.Migrate(json);
                var newVersion = ReadVersion(json);
                if (newVersion != step.FromVersion + 1)
                    throw new InvalidOperationException($"Migration from schema version {step.FromVersion} did not advance SchemaVersion to {step.FromVersion + 1} (got {newVersion}).");

                version = newVersion;
            }

            return json;
        }
    }

    // One version-to-version transform. Migrate must return JSON whose
    // own "SchemaVersion" is exactly FromVersion + 1.
    internal sealed class SchemaMigrationStep
    {
        public int FromVersion { get; }
        public Func<string, string> Migrate { get; }

        public SchemaMigrationStep(int fromVersion, Func<string, string> migrate)
        {
            FromVersion = fromVersion;
            Migrate = migrate;
        }
    }
}

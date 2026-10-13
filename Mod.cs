using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.PSI.Environment;
using ZoneBuilder.Core;
using ZoneBuilder.CustomZones;
using ZoneBuilder.UI;
using Game;
using Game.Common;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using System.IO;

namespace ZoneBuilder
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(ZoneBuilder)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        // The one place the global collection library's location is decided.
        // Kept out of the sibling "Mods" folder since that one's scanned by
        // the game for mod packages.
        public static ICollectionLibrary CollectionLibrary { get; private set; }

        // Same reasoning as CollectionLibrary above - see DECISIONS.md.
        public static ICustomZoneDefinitionLibrary CustomZoneDefinitionLibrary { get; private set; }

        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            // One file per collection (<Id>.json) under Collections/, not
            // one combined file - see DECISIONS.md "Migrated to one file
            // per collection" for why. legacyLibraryPath is the old
            // single-file format a pre-migration install still has on
            // disk; MigrateIfNeeded splits it into the new folder once,
            // then renames it to collections.json.migrated so this never
            // re-runs.
            var collectionsFolderPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(ZoneBuilder), "Collections");
            var legacyLibraryPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(ZoneBuilder), "collections.json");
            var migration = LegacyCollectionsMigrator.MigrateIfNeeded(legacyLibraryPath, collectionsFolderPath);
            if (migration.Attempted)
            {
                if (migration.FatalError != null)
                    log.Error(migration.FatalError, $"Could not read legacy collections file '{legacyLibraryPath}' - left untouched, nothing migrated this launch.");
                else
                    log.Info($"Migrated {migration.MigratedCount} collection(s) from the old single-file format to {collectionsFolderPath}.");
                foreach (var failure in migration.CollectionFailures)
                    log.Warn(failure.Error, $"Failed to migrate collection '{failure.Name}' ({failure.Id}) - not carried over, legacy file kept as a backup to recover it by hand.");
            }

            CollectionLibrary = new FolderCollectionLibrary(collectionsFolderPath);
            foreach (var failure in ((FolderCollectionLibrary)CollectionLibrary).LoadFailures)
                log.Warn(failure.Error, $"Failed to load collection file '{failure.FilePath}' - skipped, every other collection still loaded normally.");

            // Boot-time-only import - no in-game button, no live trigger.
            // Drop exported/shared collection files (same schema as our
            // own collection files, see CollectionFileFormat) into this
            // folder before launching and they're picked up here, moved
            // to Imports/Processed/ once done so they don't re-import
            // every boot. See DECISIONS.md.
            var importsPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(ZoneBuilder), "Imports");
            var importResult = CollectionImporter.ImportFromFolder(importsPath, CollectionLibrary);
            if (importResult.ImportedCollectionCount > 0)
                log.Info($"Imported {importResult.ImportedCollectionCount} collection(s) from {importsPath}");
            foreach (var failure in importResult.Failures)
                log.Warn(failure.Error, $"Failed to import '{failure.FilePath}' - left in place, fix its JSON and it'll be retried next launch.");

            log.Info($"Collection library: {collectionsFolderPath} ({CollectionLibrary.GetAll().Count} collection(s))");

            var zoneDefinitionsFolderPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(ZoneBuilder), "Zones");
            var legacyZonesPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(ZoneBuilder), "customZones.json");
            var zoneMigration = LegacyCustomZoneDefinitionsMigrator.MigrateIfNeeded(legacyZonesPath, zoneDefinitionsFolderPath);
            if (zoneMigration.Attempted)
            {
                if (zoneMigration.FatalError != null)
                    log.Error(zoneMigration.FatalError, $"Could not read legacy custom zones file '{legacyZonesPath}' - left untouched, nothing migrated this launch.");
                else
                    log.Info($"Migrated {zoneMigration.MigratedCount} custom zone definition(s) from the old single-file format to {zoneDefinitionsFolderPath}.");
                foreach (var failure in zoneMigration.DefinitionFailures)
                    log.Warn(failure.Error, $"Failed to migrate zone definition '{failure.Name}' ({failure.Id}) - not carried over, legacy file kept as a backup to recover it by hand.");
            }

            CustomZoneDefinitionLibrary = new FolderCustomZoneDefinitionLibrary(zoneDefinitionsFolderPath);
            foreach (var failure in ((FolderCustomZoneDefinitionLibrary)CustomZoneDefinitionLibrary).LoadFailures)
                log.Warn(failure.Error, $"Failed to load zone definition file '{failure.FilePath}' - skipped, every other zone definition still loaded normally.");

            log.Info($"Custom zone definition library: {zoneDefinitionsFolderPath} ({CustomZoneDefinitionLibrary.GetAll().Count} definition(s))");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            AssetDatabase.global.LoadSettings(nameof(ZoneBuilder), m_Setting, new Setting(this));

            // CustomZoneBuildRequestSystem owns the one timing requirement
            // a runtime-cloned prefab has - see DECISIONS.md "A
            // runtime-cloned prefab is only visible to init systems for one
            // frame". Must be registered exactly this way: MainLoop,
            // UpdateBefore<_, PrefabSystem>, so its OnUpdate always lands
            // immediately before PrefabSystem's own tick in the same frame.
            updateSystem.UpdateBefore<CustomZoneBuildRequestSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);

            // Same phase every UI-binding system in the game uses.
            updateSystem.UpdateAt<CollectionUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<BuildingPickerUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<ZoneUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));

            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}

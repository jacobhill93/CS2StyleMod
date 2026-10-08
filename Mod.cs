using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.PSI.Environment;
using CS2StyleMod.Core;
using CS2StyleMod.CustomZones.Debug;
using Game;
using Game.Common;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using System.IO;

namespace CS2StyleMod
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(CS2StyleMod)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        // The one place the global collection library's location is decided.
        // Kept out of the sibling "Mods" folder since that one's scanned by
        // the game for mod packages.
        public static ICollectionLibrary CollectionLibrary { get; private set; }

        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            var libraryPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(CS2StyleMod), "collections.json");
            CollectionLibrary = new JsonFileCollectionLibrary(libraryPath);
            log.Info($"Collection library: {libraryPath} ({CollectionLibrary.GetAll().Count} collection(s))");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            AssetDatabase.global.LoadSettings(nameof(CS2StyleMod), m_Setting, new Setting(this));

            // Throwaway debug affordance for verifying CustomZoneBuilder
            // with a real Collection - see its own comments. Same
            // registration pattern proven to matter: MainLoop +
            // UpdateBefore<_, PrefabSystem>, not PrefabUpdate.
            updateSystem.UpdateBefore<DebugCustomZoneBuilderTestSystem, PrefabSystem>(SystemUpdatePhase.MainLoop);
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

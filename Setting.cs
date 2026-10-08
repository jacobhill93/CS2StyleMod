using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;

namespace CS2StyleMod
{
    [FileLocation(nameof(CS2StyleMod))]
    [SettingsUIGroupOrder(kFeaturesGroup)]
    [SettingsUIShowGroupName(kFeaturesGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kFeaturesGroup = "Features";

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(kSection, kFeaturesGroup)]
        public bool EnableCustomZones { get; set; } = true;

        [SettingsUISection(kSection, kFeaturesGroup)]
        public bool EnableDistrictThemes { get; set; } = true;

        public override void SetDefaults()
        {
            EnableCustomZones = true;
            EnableDistrictThemes = true;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "CS2StyleMod" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kFeaturesGroup), "Features" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCustomZones)), "Custom Zones" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCustomZones)), "Create new zone types backed by collections." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableDistrictThemes)), "District Themes" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableDistrictThemes)), "Filter naturally spawned buildings per district using collections." },
            };
        }

        public void Unload()
        {
        }
    }
}

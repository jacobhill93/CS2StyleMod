using System.Collections.Generic;
using System.Linq;
using CS2StyleMod.Core;

namespace CS2StyleMod.Core.Tests
{
    // In-memory stand-in for the real game-prefab-backed catalog.
    internal sealed class FakeBuildingCatalog : IBuildingCatalog
    {
        private readonly List<BuildingCandidate> m_All;

        public FakeBuildingCatalog(IEnumerable<BuildingCandidate> all)
        {
            m_All = all.ToList();
        }

        public IEnumerable<BuildingCandidate> Resolve(VanillaSelectorEntry selector)
        {
            return m_All.Where(b =>
                (selector.ZoneType == null || b.ZoneType == selector.ZoneType) &&
                (string.IsNullOrEmpty(selector.AssetPackId) || b.AssetPackId == selector.AssetPackId));
        }

        public BuildingCandidate Resolve(ExplicitAssetEntry entry)
        {
            return m_All.Single(b => b.PrefabId == entry.PrefabId);
        }

        public BuildingCandidate Resolve(AdoptedAssetEntry entry)
        {
            return new BuildingCandidate(entry.PrefabId, entry.Settings.Category, entry.Settings.Level, entry.Settings.LotSize);
        }
    }
}

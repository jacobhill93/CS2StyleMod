using System.Collections.Generic;

namespace CS2StyleMod.Core
{
    // Resolves collection entries into concrete buildings. Implemented by a
    // GameAdapter against real game prefabs; fakeable in unit tests so
    // CandidateFilter stays testable without the game.
    public interface IBuildingCatalog
    {
        IEnumerable<BuildingCandidate> Resolve(VanillaSelectorEntry selector);
        BuildingCandidate Resolve(ExplicitAssetEntry entry);
        BuildingCandidate Resolve(AdoptedAssetEntry entry);
    }
}

using System.Collections.Generic;
using System.Linq;
using CS2StyleMod.Core;
using Xunit;

namespace CS2StyleMod.Core.Tests
{
    public class CandidateFilterTests
    {
        private static readonly LotSize TwoByFour = new LotSize(2, 4);

        [Fact]
        public void ExcludesBuildingsWithWrongZoneType()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("house1", ZoneType.ResidentialLow, 1, TwoByFour),
                new BuildingCandidate("shop1", ZoneType.CommercialLow, 1, TwoByFour),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "house1" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void ExcludesBuildingsTooDeepForTheLot()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("fits", ZoneType.ResidentialLow, 1, new LotSize(2, 3)),
                new BuildingCandidate("tooDeep", ZoneType.ResidentialLow, 1, new LotSize(2, 5)),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "fits" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void IncludesAdoptedAssetsUsingTheirOwnSettings()
        {
            var catalog = new FakeBuildingCatalog(System.Array.Empty<BuildingCandidate>());
            var collection = new Collection("Test");
            collection.Add(new AdoptedAssetEntry("adopted-house", new AdoptedAssetSettings(ZoneType.ResidentialLow, 1, TwoByFour, householdCount: 3)));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "adopted-house" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void DedupesTheSamePrefabReachedThroughMultipleEntries()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("house1", ZoneType.ResidentialLow, 1, TwoByFour),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            collection.Add(new ExplicitAssetEntry("house1"));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Single(result);
        }

        [Fact]
        public void NoFallbackReturnsEmptyWhenNothingMatches()
        {
            var catalog = new FakeBuildingCatalog(System.Array.Empty<BuildingCandidate>());
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);
            var vanilla = new[] { new BuildingCandidate("vanilla-house", ZoneType.ResidentialLow, 1, TwoByFour) };

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None, vanilla);

            Assert.Empty(result);
        }

        [Fact]
        public void VanillaZoneFallbackIsUsedWhenNothingMatches()
        {
            var catalog = new FakeBuildingCatalog(System.Array.Empty<BuildingCandidate>());
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);
            var vanilla = new[] { new BuildingCandidate("vanilla-house", ZoneType.ResidentialLow, 1, TwoByFour) };

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.VanillaZone, vanilla);

            Assert.Equal(new[] { "vanilla-house" }, result.Select(c => c.PrefabId));
        }

        [Fact]
        public void VanillaSelectorCanFilterByAssetPack()
        {
            var catalog = new FakeBuildingCatalog(new[]
            {
                new BuildingCandidate("packHouse", ZoneType.ResidentialLow, 1, TwoByFour, assetPackId: "NA"),
                new BuildingCandidate("otherHouse", ZoneType.ResidentialLow, 1, TwoByFour, assetPackId: "EU"),
            });
            var collection = new Collection("Test");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow, assetPackId: "NA"));
            var lot = new LotQuery(ZoneType.ResidentialLow, TwoByFour);

            var result = CandidateFilter.GetEligibleBuildings(lot, new[] { collection }, catalog, FallbackPolicy.None);

            Assert.Equal(new[] { "packHouse" }, result.Select(c => c.PrefabId));
        }
    }
}

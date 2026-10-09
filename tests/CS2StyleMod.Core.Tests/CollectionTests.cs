using CS2StyleMod.Core;
using Xunit;

namespace CS2StyleMod.Core.Tests
{
    public class CollectionTests
    {
        [Fact]
        public void RemoveExplicitAssetDropsOnlyTheMatchingPrefab()
        {
            var collection = new Collection("Row Houses");
            collection.Add(new ExplicitAssetEntry("House_A"));
            collection.Add(new ExplicitAssetEntry("House_B"));

            collection.RemoveExplicitAsset("House_A");

            var remaining = Assert.Single(collection.Entries);
            Assert.Equal("House_B", Assert.IsType<ExplicitAssetEntry>(remaining).PrefabId);
        }

        [Fact]
        public void RemoveExplicitAssetForAnUnknownPrefabIsANoOp()
        {
            var collection = new Collection("Row Houses");
            collection.Add(new ExplicitAssetEntry("House_A"));

            collection.RemoveExplicitAsset("House_Nonexistent");

            Assert.Single(collection.Entries);
        }

        [Fact]
        public void RemoveExplicitAssetDoesNotTouchOtherEntryKinds()
        {
            var collection = new Collection("Mixed");
            collection.Add(new VanillaSelectorEntry(ZoneType.ResidentialLow));
            collection.Add(new ExplicitAssetEntry("House_A"));

            collection.RemoveExplicitAsset("House_A");

            var remaining = Assert.Single(collection.Entries);
            Assert.IsType<VanillaSelectorEntry>(remaining);
        }
    }
}

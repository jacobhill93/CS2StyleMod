using System;

namespace CS2StyleMod.Core
{
    // One entry in a Collection. See the three kinds in README.md's
    // "Collections (core)" section.
    public abstract class CollectionEntry
    {
    }

    // "all Residential Low", "all Residential Low, NA theme", or a whole
    // asset pack - resolved against whatever prefabs exist at runtime.
    public sealed class VanillaSelectorEntry : CollectionEntry
    {
        public ZoneType? ZoneType { get; }
        public string AssetPackId { get; }

        public VanillaSelectorEntry(ZoneType? zoneType, string assetPackId = null)
        {
            if (zoneType == null && string.IsNullOrEmpty(assetPackId))
                throw new ArgumentException("A vanilla selector needs a zone type, an asset pack, or both.");

            ZoneType = zoneType;
            AssetPackId = assetPackId;
        }
    }

    // A specific hand-picked building, vanilla or from Paradox Mods.
    public sealed class ExplicitAssetEntry : CollectionEntry
    {
        public string PrefabId { get; }

        public ExplicitAssetEntry(string prefabId)
        {
            PrefabId = prefabId;
        }
    }

    // A building with no native growable data, given mod-supplied settings
    // so it can spawn naturally.
    public sealed class AdoptedAssetEntry : CollectionEntry
    {
        public string PrefabId { get; }
        public AdoptedAssetSettings Settings { get; }

        public AdoptedAssetEntry(string prefabId, AdoptedAssetSettings settings)
        {
            PrefabId = prefabId;
            Settings = settings;
        }
    }
}

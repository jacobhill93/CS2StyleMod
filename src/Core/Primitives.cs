namespace ZoneBuilder.Core
{
    // Mirrors the vanilla zone categories. Values/membership unverified against
    // game code yet - see DECISIONS.md's open questions.
    public enum ZoneType
    {
        ResidentialLow,
        ResidentialMedium,
        ResidentialHigh,
        CommercialLow,
        CommercialHigh,
        Office,
        Industrial,
    }

    // Which vanilla theme a custom zone's simulation template is cloned
    // from - purely a UI/catalog grouping choice (which UIGroupPrefab the
    // clone displays under in the zone-painting tool, mirroring
    // Game.Prefabs.ThemePrefab.assetPrefix - confirmed real via live
    // reflection against Game.dll, not guessed), not a change to
    // simulation behavior or which buildings spawn - that's entirely
    // controlled by the zone's attached Collections regardless of this
    // choice. See GameAdapters/GameZonePrefabResolver.cs and DECISIONS.md.
    public enum ZoneRegion
    {
        NorthAmerica,
        Europe,
    }

    public readonly struct LotSize
    {
        public int Width { get; }
        public int Depth { get; }

        public LotSize(int width, int depth)
        {
            Width = width;
            Depth = depth;
        }
    }
}

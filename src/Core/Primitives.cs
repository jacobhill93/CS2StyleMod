namespace ZoneBuilder.Core
{
    // Mirrors the vanilla zone categories - membership confirmed against
    // real building names (e.g. `OfficeLow01_L1_3x4`, see DECISIONS.md) and
    // GameBuildingCatalog.TryMapToCoreZoneType's decompiled-source-verified
    // AreaType/IsOffice/density mapping.
    //
    // OfficeLow/OfficeHigh are appended at the end, not alongside Office
    // where a reader would expect them - this enum is persisted directly
    // (Newtonsoft, no StringEnumConverter, so by ordinal) in every saved
    // CustomZoneDefinition's BaseZoneType. Inserting new members earlier
    // would silently shift the ordinal of Office/Industrial and corrupt
    // any already-saved zone using either. Office itself is kept
    // (never produced by new data after 2026-10-13 - see DECISIONS.md -
    // but still a valid BaseZoneType for anything saved before that).
    public enum ZoneType
    {
        ResidentialLow,
        ResidentialMedium,
        ResidentialHigh,
        CommercialLow,
        CommercialHigh,
        Office,
        Industrial,
        OfficeLow,
        OfficeHigh,
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

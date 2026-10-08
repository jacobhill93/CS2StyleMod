namespace CS2StyleMod.Core
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

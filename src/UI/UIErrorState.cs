namespace ZoneBuilder.UI
{
    public readonly struct UIErrorEntry
    {
        public bool Active { get; }
        public string Message { get; }

        public UIErrorEntry(bool active, string message)
        {
            Active = active;
            Message = message;
        }

        public static readonly UIErrorEntry Inactive = new UIErrorEntry(false, "");
    }

    // Phase 5 (UI_UPGRADE_GUIDE.md) - minimal failure surfacing. Shared
    // across CollectionUISystem and ZoneUISystem (registered as two
    // separate systems, Mod.cs) rather than each getting its own getter
    // binding, so the shell only needs the one banner slot the guide
    // actually calls for - the two systems could in principle both fail
    // in close succession, but reconciling two independently active
    // banners for an edge case this rare isn't worth the complexity.
    // CollectionUISystem exposes this as the "UIError" binding and
    // "DismissUIError" trigger the shell renders; ZoneUISystem just
    // writes to it the same way CollectionUISystem's own TrySave/
    // TryDelete do, via this static surface, without exposing anything
    // of its own.
    internal static class UIErrorState
    {
        private static UIErrorEntry s_Current = UIErrorEntry.Inactive;

        public static UIErrorEntry Current => s_Current;

        public static void Report(string message) => s_Current = new UIErrorEntry(true, message);

        public static void Dismiss() => s_Current = UIErrorEntry.Inactive;
    }
}

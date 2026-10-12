namespace ZoneBuilder.CustomZones
{
    // Plain progress snapshot yielded by CustomZoneBuilder.BuildOrRebuildIncremental
    // - see that method's own comment for why this exists. Completed ==
    // Total means "this step was the last one," not necessarily "every
    // entry actually cloned something" (skipped/mismatched entries still
    // count toward Completed).
    public readonly struct ZoneBuildProgress
    {
        public readonly string ZoneName;
        public readonly int Completed;
        public readonly int Total;

        public ZoneBuildProgress(string zoneName, int completed, int total)
        {
            ZoneName = zoneName;
            Completed = completed;
            Total = total;
        }
    }
}

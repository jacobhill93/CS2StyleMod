using Colossal.UI.Binding;

namespace ZoneBuilder.UI
{
    // Bound value for the "building zone..." indicator - mirrors
    // ZoneUIEntry's reasoning for a hand-written IJsonWritable.Write()
    // (camelCase JSON key preservation) rather than GenericUIWriter's
    // reflection. Unlike every other binding in this UI, this one is
    // read via a getter (ExtendedUISystemBase.CreateBinding<T>(key,
    // Func<T>)) so it reflects CustomZoneBuildRequestSystem.CurrentProgress
    // live, every UI tick, instead of only on an explicit mutation -
    // progress changes continuously while a build is running, with
    // nothing like RefreshZones() to call after each step.
    public readonly struct ZoneBuildProgressEntry : IJsonWritable
    {
        public static readonly ZoneBuildProgressEntry Inactive = new ZoneBuildProgressEntry(false, "", 0, 0);

        private readonly bool m_Active;
        private readonly string m_ZoneName;
        private readonly int m_Completed;
        private readonly int m_Total;

        public ZoneBuildProgressEntry(bool active, string zoneName, int completed, int total)
        {
            m_Active = active;
            m_ZoneName = zoneName;
            m_Completed = completed;
            m_Total = total;
        }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("active");
            writer.Write(m_Active);
            writer.PropertyName("zoneName");
            writer.Write(m_ZoneName);
            writer.PropertyName("completed");
            writer.Write(m_Completed);
            writer.PropertyName("total");
            writer.Write(m_Total);
            writer.TypeEnd();
        }
    }
}

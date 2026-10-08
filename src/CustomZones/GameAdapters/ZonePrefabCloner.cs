using Game.Prefabs;

namespace CS2StyleMod.CustomZones.GameAdapters
{
    // Touchpoint: PrefabSystem.AddPrefab + PrefabBase.Clone - the same
    // mechanism the game's own editor tooling uses to duplicate prefabs.
    // Gives a custom zone its simulation profile by cloning a vanilla (or
    // other custom) zone prefab wholesale: ZonePrefab's own fields
    // (m_AreaType, m_Office, m_Color, m_Edge) are plain values and survive
    // the clone as-is; ZoneData's runtime fields (its assigned zone-type
    // index, etc.) get filled in afterward by the game's own ZoneSystem,
    // exactly like any other newly-registered zone prefab. See
    // TOUCHPOINTS.md.
    public sealed class ZonePrefabCloner
    {
        private readonly PrefabSystem m_PrefabSystem;

        public ZonePrefabCloner(PrefabSystem prefabSystem)
        {
            m_PrefabSystem = prefabSystem;
        }

        // Returns null if registration failed - check the game's log for
        // why (e.g. a prefab name collision). Clones + registers directly
        // rather than through PrefabSystem.DuplicatePrefab, which discards
        // AddPrefab's success/failure.
        public ZonePrefab CloneZone(ZonePrefab templateZone, string newZoneName)
        {
            var clone = (ZonePrefab)templateZone.Clone(newZoneName);
            clone.Remove<ObsoleteIdentifiers>();

            // UIObject.m_Group is a UIGroupPrefab reference. Confirmed by
            // an actual in-game test: Clone()'s JsonUtility round-trip
            // does not preserve it, so without this the clone registers
            // fine but is invisible in every zone picker (UIObject's own
            // LateInitialize skips adding it to any category when m_Group
            // is null). Re-pointing it at the template's group puts the
            // clone in the same UI category as whatever it was cloned
            // from.
            if (templateZone.TryGet<UIObject>(out var templateUIObject) &&
                clone.TryGet<UIObject>(out var cloneUIObject))
            {
                cloneUIObject.m_Group = templateUIObject.m_Group;
            }

            return m_PrefabSystem.AddPrefab(clone) ? clone : null;
        }
    }
}

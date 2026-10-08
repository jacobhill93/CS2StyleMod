using Game.Prefabs;

namespace CS2StyleMod.CustomZones.GameAdapters
{
    // Touchpoint: SpawnableBuilding.m_ZoneType (a ZonePrefab reference) +
    // PrefabSystem.AddPrefab. This is what makes a building belong to a
    // custom zone: both initial spawn selection (ZoneSpawnSystem) and
    // level-up selection (BuildingUpkeepSystem) choose candidates purely
    // by comparing each building prefab's BuildingSpawnGroupData.m_ZoneType
    // (set from this field when the prefab is initialized) against the
    // lot's zone - no patch of either system is needed, just repointing
    // this reference before the building prefab is registered.
    //
    // The zone reference is set explicitly on the clone *before*
    // registering it, rather than trusting PrefabBase.Clone to carry it
    // over: Clone() round-trips every attached component through Unity's
    // JsonUtility, which isn't reliable for UnityEngine.Object references
    // like this one - and by the time AddPrefab runs, the resulting
    // entity's components are already baked, so mutating the field
    // afterward would be too late. See TOUCHPOINTS.md.
    public sealed class SpawnableBuildingZoneLinker
    {
        private readonly PrefabSystem m_PrefabSystem;

        public SpawnableBuildingZoneLinker(PrefabSystem prefabSystem)
        {
            m_PrefabSystem = prefabSystem;
        }

        // Clones templateBuilding and repoints its SpawnableBuilding
        // component at targetZone. Returns null if templateBuilding has no
        // SpawnableBuilding component (not a growable building) or if
        // registration failed.
        //
        // Reminder from DECISIONS.md: level-up only works if every level
        // (1-5) for a given lot size/access-flag combination exists in the
        // zone; otherwise a building just never levels up (no error).
        public BuildingPrefab CloneBuildingForZone(BuildingPrefab templateBuilding, ZonePrefab targetZone, string newBuildingName)
        {
            var clone = (BuildingPrefab)templateBuilding.Clone(newBuildingName);
            clone.Remove<ObsoleteIdentifiers>();

            if (!clone.TryGet<SpawnableBuilding>(out var spawnable))
                return null;

            spawnable.m_ZoneType = targetZone;

            // Same object-reference-doesn't-survive-Clone() problem as
            // above, confirmed in-game for ZonePrefabCloner's UIObject fix
            // - applied here too since building prefabs carry their own
            // UIObject for asset-browser visibility (Find It, the building
            // picker). Untested whether buildings actually need this (the
            // observed bug was on the zone side), but the mechanism is
            // identical, so fixing it preemptively rather than waiting to
            // rediscover the same failure.
            if (templateBuilding.TryGet<UIObject>(out var templateUIObject) &&
                clone.TryGet<UIObject>(out var cloneUIObject))
            {
                cloneUIObject.m_Group = templateUIObject.m_Group;
            }

            return m_PrefabSystem.AddPrefab(clone) ? clone : null;
        }
    }
}

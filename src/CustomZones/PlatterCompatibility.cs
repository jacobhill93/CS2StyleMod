using Game.Common;
using Game.Prefabs;
using Unity.Entities;

namespace ZoneBuilder.CustomZones
{
    // Third-party compatibility shim for Platter
    // (https://mods.paradoxplaza.com/mods/125278) - see DECISIONS.md
    // "Platter never picks up our custom zones" for the full
    // investigation, the risk this accepts, and why a one-tick delay was
    // tried first and made things worse.
    //
    // Platter's own reactive zone cache (Platter.Systems.P_ZoneCacheSystem)
    // only does a full, correct re-cache (reading everything fresh, not a
    // frozen snapshot) when a UIAssetCategoryData-tagged entity has
    // Created/Updated/Deleted in the exact same tick its own zone-change
    // query is also non-empty - confirmed empirically, not just inferred
    // from decompiled IL, that Platter's two RequireAnyForUpdate calls AND
    // together rather than OR. Tagging the zone's own UI group (e.g.
    // "ZonesResidential" - a widely-shared vanilla entity, not one this mod
    // owns) with Updated, in the same tick as the build, satisfies that.
    //
    // DELIBERATELY ISOLATED: this entire feature is this one file plus two
    // call sites - CustomZoneBuilder.BuildOrRebuildIncremental (right
    // after a zone is resolved, same tick as its own Created tag) and
    // CustomZoneBuilder.RetireZone (right after RemovePrefab, same tick
    // as the zone's own Deleted tag - added 2026-10-12 after a deleted
    // zone was found still sitting in Platter's own list, since the
    // build-path call alone only ever satisfied Platter's zone-change
    // condition on create, never on delete). If this needs to come out
    // later - Platter ships its own fix, a game or Platter update changes
    // the mechanics this relies on, or it turns out to cause some side
    // effect this session's testing didn't happen to exercise - deleting
    // this file and those two calls is the entire removal. No fields,
    // state, or other call sites anywhere else in the codebase depend on
    // this existing.
    internal static class PlatterCompatibility
    {
        public static void NudgeZoneGroupForPlatter(ZonePrefab zone, World world)
        {
            if (zone == null || !zone.TryGet<UIObject>(out var uiObject) || uiObject.m_Group == null)
                return;

            var prefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            var entityManager = world.EntityManager;

            if (!prefabSystem.TryGetEntity(uiObject.m_Group, out var groupEntity) || !entityManager.Exists(groupEntity))
                return;

            if (entityManager.HasComponent<Updated>(groupEntity))
                return;

            entityManager.AddComponent<Updated>(groupEntity);
            Mod.log.Info($"[PlatterCompatibility] Tagged zone group Entity {groupEntity.Index}:{groupEntity.Version} with Updated so Platter's own zone cache notices it.");
        }
    }
}

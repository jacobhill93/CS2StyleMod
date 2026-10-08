using Colossal.Entities;
using Game.Prefabs;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using CoreZoneType = CS2StyleMod.Core.ZoneType;

namespace CS2StyleMod.CustomZones.GameAdapters
{
    // Resolves ZonePrefabs against the live game - either a vanilla zone
    // matching a Core.ZoneType category (to use as a custom zone's
    // simulation template), or an already-registered zone by its exact
    // prefab name (to find a custom zone CustomZoneBuilder built in an
    // earlier request, for rebuild). Kept separate from GameBuildingCatalog
    // since zones and buildings are different prefab kinds with different
    // resolution needs, even though TryFindVanillaZone reuses its
    // AreaType/IsOffice/density -> Core.ZoneType mapping rather than
    // duplicating it.
    public sealed class GameZonePrefabResolver
    {
        private readonly PrefabSystem m_PrefabSystem;
        private readonly EntityManager m_EntityManager;
        private readonly EntityQuery m_ZoneQuery;

        public GameZonePrefabResolver(World world)
        {
            m_EntityManager = world.EntityManager;
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ZoneData>(),
                ComponentType.ReadOnly<PrefabData>());
        }

        // Picks the first registered vanilla zone matching the given
        // category - same "any one will do" approach the original debug
        // harnesses used, since the regional (EU/NA) variants only differ
        // in theme/catalog grouping, not simulation profile, as far as
        // we've found. Revisit if that ever turns out wrong.
        public bool TryFindVanillaZone(CoreZoneType zoneType, out ZonePrefab prefab)
        {
            var entities = m_ZoneQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var entity in entities)
                {
                    if (!m_EntityManager.TryGetComponent<ZoneData>(entity, out var zoneData) ||
                        !m_EntityManager.TryGetComponent<ZonePropertiesData>(entity, out var zoneProps))
                        continue;

                    if (!GameBuildingCatalog.TryMapToCoreZoneType(zoneData, zoneProps, out var candidateZoneType) || candidateZoneType != zoneType)
                        continue;

                    if (m_PrefabSystem.TryGetPrefab<ZonePrefab>(entity, out prefab))
                        return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            prefab = null;
            return false;
        }

        // Exact-name lookup, same technique as GameBuildingCatalog.TryGetBuildingPrefab
        // (iterate + match PrefabBase.name, not PrefabID - see its own
        // comment for why).
        public bool TryFindByName(string name, out ZonePrefab prefab, out Entity entity)
        {
            var entities = m_ZoneQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var candidateEntity in entities)
                {
                    if (!m_PrefabSystem.TryGetPrefab<ZonePrefab>(candidateEntity, out var candidatePrefab) || candidatePrefab.name != name)
                        continue;

                    prefab = candidatePrefab;
                    entity = candidateEntity;
                    return true;
                }
            }
            finally
            {
                entities.Dispose();
            }

            prefab = null;
            entity = Entity.Null;
            return false;
        }
    }
}

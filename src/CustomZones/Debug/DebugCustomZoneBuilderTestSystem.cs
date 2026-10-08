using Colossal.Entities;
using Colossal.Serialization.Entities;
using CS2StyleMod.Core;
using CS2StyleMod.CustomZones.GameAdapters;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using CoreZoneType = CS2StyleMod.Core.ZoneType;

namespace CS2StyleMod.CustomZones.Debug
{
    // Throwaway debug affordance - see DECISIONS.md "Build order" (UI is
    // deferred, but small test affordances are allowed earlier). Verifies
    // CustomZoneBuilder itself, not the lower-level adapters/workaround -
    // those were already proven in-game by the previous debug harness
    // (since removed; see TOUCHPOINTS.md). This one builds a real
    // Core.Collection in memory (not persisted via Mod.CollectionLibrary -
    // that's already separately unit-tested) with one VanillaSelectorEntry
    // (exercises multi-candidate resolution + dedup) and one
    // ExplicitAssetEntry (exercises single-candidate resolution), and runs
    // it through the real Collection -> GameBuildingCatalog ->
    // CustomZoneBuilder pipeline exactly as a real caller eventually would.
    // Delete once there's an actual real caller (settings UI, collection
    // editor).
    public partial class DebugCustomZoneBuilderTestSystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private GameBuildingCatalog m_Catalog;
        private CustomZoneBuilder m_Builder;
        private EntityQuery m_ZoneQuery;
        private bool m_HasRun;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Catalog = new GameBuildingCatalog(World);
            m_Builder = new CustomZoneBuilder(World, m_Catalog);
            m_ZoneQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ZoneData>(),
                ComponentType.ReadOnly<PrefabData>());
        }

        protected override void OnUpdate()
        {
        }

        protected override void OnGameLoaded(Context context)
        {
            base.OnGameLoaded(context);

            if (m_HasRun)
                return;
            m_HasRun = true;

            if (!TryFindVanillaResidentialLowZone(out var vanillaZone))
            {
                Mod.log.Warn("[CZB-Debug] No vanilla Residential Low zone prefab found - skipping.");
                return;
            }

            // Single-building case (ExplicitAssetEntry) confirmed clean
            // in-game: zone named/colored/paintable, the building renders,
            // and its sub-objects (fences/chimneys/etc.) spawn - see
            // ManualPrefabInitializationWorkaround's CopyAllBuffers.
            // Re-adding the full vanilla selector now to exercise the
            // ~428-building resolution (variety across spawns, and the 12
            // "Signature" buildings previously observed failing to clone).
            var collection = new Collection("Debug Test Collection");
            collection.Add(new ExplicitAssetEntry("EU_ResidentialLow01_L1_2x2"));
            collection.Add(new VanillaSelectorEntry(CoreZoneType.ResidentialLow));

            const string zoneName = "CS2StyleMod Builder Test Zone";
            var clonedZone = m_Builder.Build(vanillaZone, zoneName, new[] { collection });
            if (clonedZone == null)
            {
                Mod.log.Warn("[CZB-Debug] CustomZoneBuilder.Build returned null - see its own warnings above for why.");
                return;
            }

            Mod.log.Info($"[CZB-Debug] CustomZoneBuilder produced zone '{clonedZone.name}' - check the log above for how many buildings it cloned.");

            // Diagnostic: the exact same building worked cleanly through
            // the old hand-coded debug harness, but not through this new
            // orchestration path (missing name, nothing visible after
            // construction). Dump its key component state to find what's
            // actually different this time, rather than guess again.
            var expectedBuildingName = $"{zoneName} - EU_ResidentialLow01_L1_2x2";
            if (m_Catalog.TryGetBuildingPrefab(expectedBuildingName, out _, out var buildingEntity))
                LogDiagnosticSnapshot("Cloned building", buildingEntity);
            else
                Mod.log.Warn($"[CZB-Debug] Could not find cloned building named '{expectedBuildingName}' to inspect - name mismatch?");
        }

        private void LogDiagnosticSnapshot(string label, Entity entity)
        {
            var hasCreated = EntityManager.HasComponent<Created>(entity);
            var hasUpdated = EntityManager.HasComponent<Updated>(entity);
            var hasGeometry = EntityManager.TryGetComponent<ObjectGeometryData>(entity, out var geometry);
            var hasBuildingData = EntityManager.TryGetComponent<BuildingData>(entity, out var buildingData);
            var hasUIObject = EntityManager.TryGetComponent<UIObjectData>(entity, out var uiObject);
            var hasSpawnable = EntityManager.TryGetComponent<SpawnableBuildingData>(entity, out var spawnable);

            Mod.log.Warn($"[CZB-Debug] {label} diagnostic snapshot: HasComponent<Created>={hasCreated}, HasComponent<Updated>={hasUpdated}, " +
                $"ObjectGeometryData.m_Size={(hasGeometry ? geometry.m_Size.ToString() : "n/a")}, " +
                $"BuildingData.m_LotSize={(hasBuildingData ? buildingData.m_LotSize.ToString() : "n/a")}, " +
                $"UIObjectData.m_Group={(hasUIObject ? uiObject.m_Group.ToString() : "n/a")}, " +
                $"SpawnableBuildingData.m_Level={(hasSpawnable ? spawnable.m_Level.ToString() : "n/a")}, " +
                $"SpawnableBuildingData.m_ZonePrefab={(hasSpawnable ? spawnable.m_ZonePrefab.ToString() : "n/a")}.");
        }

        private bool TryFindVanillaResidentialLowZone(out ZonePrefab zonePrefab)
        {
            var entities = m_ZoneQuery.ToEntityArray(Allocator.Temp);
            var zoneDatas = m_ZoneQuery.ToComponentDataArray<ZoneData>(Allocator.Temp);
            try
            {
                for (var i = 0; i < zoneDatas.Length; i++)
                {
                    if (zoneDatas[i].m_AreaType != AreaType.Residential || zoneDatas[i].IsOffice())
                        continue;

                    if (m_PrefabSystem.TryGetPrefab<ZonePrefab>(entities[i], out zonePrefab))
                        return true;
                }
            }
            finally
            {
                entities.Dispose();
                zoneDatas.Dispose();
            }

            zonePrefab = null;
            return false;
        }
    }
}

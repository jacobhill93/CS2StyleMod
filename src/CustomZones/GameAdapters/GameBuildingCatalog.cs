using Colossal.Entities;
using CS2StyleMod.Core;
using Game.Buildings;
using Game.Prefabs;
using Game.Zones;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using CoreZoneType = CS2StyleMod.Core.ZoneType;

namespace CS2StyleMod.CustomZones.GameAdapters
{
    // Real Core.IBuildingCatalog implementation: resolves Core's
    // game-independent CollectionEntry kinds against the live game's
    // registered prefabs.
    //
    // Looks buildings up by iterating an EntityQuery and matching by
    // PrefabBase.name, rather than via PrefabSystem.TryGetPrefab(PrefabID,
    // out _) - that overload's PrefabID equality includes a content hash
    // derived from the prefab's asset GUID (see Game.Prefabs.PrefabID),
    // which isn't known in advance from a name alone, so a name-only
    // PrefabID never matches anything. TryGetPrefab(Entity, out _) (used
    // here, same as the other adapters) has no such issue.
    public sealed class GameBuildingCatalog : IBuildingCatalog
    {
        private readonly PrefabSystem m_PrefabSystem;
        private readonly EntityManager m_EntityManager;
        private readonly EntityQuery m_BuildingQuery;

        public GameBuildingCatalog(World world)
        {
            m_EntityManager = world.EntityManager;
            m_PrefabSystem = world.GetOrCreateSystemManaged<PrefabSystem>();
            m_BuildingQuery = m_EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpawnableBuildingData>(),
                ComponentType.ReadOnly<BuildingData>(),
                ComponentType.ReadOnly<PrefabData>());
        }

        public IEnumerable<BuildingCandidate> Resolve(VanillaSelectorEntry selector)
        {
            var results = new List<BuildingCandidate>();
            var buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buildingEntity in buildingEntities)
                {
                    if (!TryDescribeBuilding(buildingEntity, out var candidate))
                        continue;

                    if (selector.ZoneType.HasValue && candidate.ZoneType != selector.ZoneType.Value)
                        continue;

                    // Not yet implemented: candidate.AssetPackId is always
                    // null (see TryDescribeBuilding), so a selector that
                    // specifies an asset pack currently matches nothing.
                    // Needs reading the building prefab's AssetPackElement
                    // buffer and resolving each AssetPackPrefab's name.
                    if (!string.IsNullOrEmpty(selector.AssetPackId) && candidate.AssetPackId != selector.AssetPackId)
                        continue;

                    results.Add(candidate);
                }
            }
            finally
            {
                buildingEntities.Dispose();
            }

            return results;
        }

        public BuildingCandidate Resolve(ExplicitAssetEntry entry)
        {
            if (TryGetBuildingPrefab(entry.PrefabId, out var _, out var buildingEntity) && TryDescribeBuilding(buildingEntity, out var candidate))
                return candidate;

            throw new KeyNotFoundException($"No registered building prefab named '{entry.PrefabId}'.");
        }

        // Not part of IBuildingCatalog - a non-throwing counterpart to
        // Resolve(ExplicitAssetEntry) above, for callers where "not found"
        // is an ordinary case to skip rather than an exceptional one.
        // CustomZoneBuilder uses this to check a candidate's real ZoneType
        // against a zone's BaseZoneType before cloning it in - see
        // DECISIONS.md "A collection's buildings aren't filtered by the
        // zone's base type".
        public bool TryResolve(ExplicitAssetEntry entry, out BuildingCandidate candidate)
        {
            if (TryGetBuildingPrefab(entry.PrefabId, out _, out var buildingEntity))
                return TryDescribeBuilding(buildingEntity, out candidate);

            candidate = null;
            return false;
        }

        // Not part of IBuildingCatalog - Core deliberately only deals in
        // plain BuildingCandidate values, never real game objects. Feature
        // logic (e.g. CustomZoneBuilder) that needs the actual prefab to
        // clone calls this directly on the concrete adapter instead.
        public bool TryGetBuildingPrefab(string prefabId, out BuildingPrefab prefab, out Entity entity)
        {
            var buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buildingEntity in buildingEntities)
                {
                    if (!m_PrefabSystem.TryGetPrefab<BuildingPrefab>(buildingEntity, out var candidatePrefab) || candidatePrefab.name != prefabId)
                        continue;

                    prefab = candidatePrefab;
                    entity = buildingEntity;
                    return true;
                }
            }
            finally
            {
                buildingEntities.Dispose();
            }

            prefab = null;
            entity = Entity.Null;
            return false;
        }

        // No game lookup needed - an adopted asset's candidate is built
        // entirely from the mod-supplied settings on the entry itself,
        // same as FakeBuildingCatalog's test double.
        public BuildingCandidate Resolve(AdoptedAssetEntry entry)
        {
            return new BuildingCandidate(entry.PrefabId, entry.Settings.Category, entry.Settings.Level, entry.Settings.LotSize);
        }

        // Not part of IBuildingCatalog - this is richer, pure browse/display
        // data for the building picker UI, not anything CandidateFilter
        // needs. Reuses TryDescribeBuilding (same Signature-building
        // exclusion, same zone-type mapping) rather than duplicating it.
        public IEnumerable<BuildingBrowserEntry> GetAllBuildings()
        {
            var results = new List<BuildingBrowserEntry>();
            var buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buildingEntity in buildingEntities)
                {
                    if (!TryDescribeBuilding(buildingEntity, out var candidate))
                        continue;

                    var heightMeters = m_EntityManager.TryGetComponent<ObjectGeometryData>(buildingEntity, out var geometry)
                        ? geometry.m_Size.y
                        : 0f;

                    results.Add(new BuildingBrowserEntry(
                        candidate.PrefabId,
                        candidate.ZoneType,
                        candidate.Level,
                        candidate.LotSize,
                        heightMeters,
                        ResolveAssetPackIds(buildingEntity, candidate.PrefabId),
                        ResolveIconPath(buildingEntity)));
                }
            }
            finally
            {
                buildingEntities.Dispose();
            }

            return results;
        }

        // A building can belong to more than one asset pack (buffer, not a
        // single value) - e.g. a regional theme pack plus a DLC pack at
        // once. Resolves each referenced Game.Prefabs.AssetPackPrefab's own
        // name, same name-based identity the rest of this catalog already
        // uses (not PrefabID - see class comment for why).
        //
        // Empirically confirmed (2026-10-08, save with Bridges & Ports DLC,
        // the China Content Creator Pack, and the community-made "Detroit
        // Bungalow Pack" installed): AssetPackElement/AssetPackPrefab is set
        // uniformly for all three - DLC, an official Content Creator Pack,
        // and a community asset pack all register their own AssetPackPrefab,
        // there's nothing CO-official-only about this buffer. The only
        // buildings with zero AssetPackElement entries were plain NA_/EU_
        // vanilla - no orphaned, pack-less community building was found in
        // that save - so a vanilla fallback split by name prefix is the only
        // extra case needed today. See DECISIONS.md "Interaction between
        // themes and the EU/NA theme setting and asset packs" for the one
        // remaining known gap this doesn't cover (a single Workshop building
        // released without its own AssetPackPrefab).
        private string[] ResolveAssetPackIds(Entity buildingEntity, string prefabId)
        {
            if (m_EntityManager.TryGetBuffer<AssetPackElement>(buildingEntity, true, out var packs) && packs.Length > 0)
            {
                var result = new List<string>(packs.Length);
                foreach (var pack in packs)
                {
                    if (m_PrefabSystem.TryGetPrefab<AssetPackPrefab>(pack.m_Pack, out var packPrefab))
                        result.Add(packPrefab.name);
                }

                if (result.Count > 0)
                    return result.ToArray();
            }

            // Confirmed in-game (2026-10-08): only Residential/Commercial
            // vanilla buildings carry a region prefix - Office/Industrial
            // (e.g. "OfficeLow01_L1_3x4") have no NA_/EU_ variant at all, so
            // a prefix-only fallback left ~1082 of them with zero pack ids.
            // Everything without an AssetPackElement is vanilla regardless;
            // the region split is just a refinement when the prefix exists.
            if (prefabId.StartsWith("NA_", StringComparison.Ordinal))
                return new[] { "Vanilla (North America)" };
            if (prefabId.StartsWith("EU_", StringComparison.Ordinal))
                return new[] { "Vanilla (Europe)" };

            return new[] { "Vanilla" };
        }

        // Phase 4c (UI_OVERHAUL_PLAN.md): UIObject.m_Icon, confirmed real via
        // live reflection against Game.dll (same field ZonePrefabCloner
        // already reads/writes m_Group on) and empty by default for most
        // buildings in vanilla - it's a zone/category icon slot, not a
        // per-building one, normally. Asset Icon Library (MIT, if the
        // player has it installed) writes a real coui://ail/... thumbnail
        // URL into this exact field per building it covers (confirmed by
        // reading AIL's own IconReplacerSystem.cs source directly - see
        // DECISIONS.md). Known open risk, not yet confirmed in-game: this
        // is read once, in BuildingPickerUISystem.OnGameLoaded - if AIL
        // populates m_Icon *after* that point (mod load/update order
        // between two independent mods isn't something this mod controls),
        // the read here would be stale. Needs an in-game check with AIL
        // actually installed before trusting this works end to end.
        private string ResolveIconPath(Entity buildingEntity)
        {
            if (m_PrefabSystem.TryGetPrefab<BuildingPrefab>(buildingEntity, out var prefab) &&
                prefab.TryGet<UIObject>(out var uiObject) &&
                !string.IsNullOrEmpty(uiObject.m_Icon))
            {
                return uiObject.m_Icon;
            }

            return string.Empty;
        }

        // Not part of IBuildingCatalog. Used by CustomZoneBuilder's rebuild
        // path to find every building it already cloned for a given zone,
        // by the stable internal name prefix it clones them under - lets
        // rebuild tell "already have this one" from "need to clone this
        // one" without tracking any extra state of its own.
        public IEnumerable<(BuildingPrefab Prefab, Entity Entity)> FindBuildingsByNamePrefix(string namePrefix)
        {
            var results = new List<(BuildingPrefab, Entity)>();
            var buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var buildingEntity in buildingEntities)
                {
                    if (m_PrefabSystem.TryGetPrefab<BuildingPrefab>(buildingEntity, out var prefab) && prefab.name.StartsWith(namePrefix))
                        results.Add((prefab, buildingEntity));
                }
            }
            finally
            {
                buildingEntities.Dispose();
            }

            return results;
        }

        private bool TryDescribeBuilding(Entity buildingEntity, out BuildingCandidate candidate)
        {
            candidate = null;

            if (!m_PrefabSystem.TryGetPrefab<BuildingPrefab>(buildingEntity, out var prefab))
                return false;

            // Signature buildings (the 12 "...Signature01/02/03" vanilla
            // residentials, confirmed via Game.Prefabs.SignatureBuilding)
            // carry SpawnableBuildingData too - SignatureBuilding.LateInitialize
            // sets it directly - so they pass this query and showed up as
            // ordinary candidates, but their PrefabBase has a
            // SignatureBuilding component instead of SpawnableBuilding.
            // SpawnableBuildingZoneLinker requires the latter and correctly
            // rejects them, but only after they'd already been resolved as
            // candidates - hence 12 clone failures per run with nothing
            // actually wrong. They're also one-per-city unique landmarks
            // with their own unlock/leveling model (PlacementFlags.Unique,
            // fixed m_Level=5) - not a fit for "clone this growable into my
            // zone" regardless. Filtering them out here is the correct
            // place: nothing should ever present them as a candidate.
            if (m_EntityManager.HasComponent<SignatureBuildingData>(buildingEntity))
                return false;

            var spawnable = m_EntityManager.GetComponentData<SpawnableBuildingData>(buildingEntity);
            var buildingData = m_EntityManager.GetComponentData<BuildingData>(buildingEntity);

            if (!m_EntityManager.TryGetComponent<ZoneData>(spawnable.m_ZonePrefab, out var zoneData) ||
                !m_EntityManager.TryGetComponent<ZonePropertiesData>(spawnable.m_ZonePrefab, out var zoneProps))
                return false;

            if (!TryMapToCoreZoneType(zoneData, zoneProps, out var coreZoneType))
                return false;

            candidate = new BuildingCandidate(
                prefab.name,
                coreZoneType,
                spawnable.m_Level,
                new LotSize(buildingData.m_LotSize.x, buildingData.m_LotSize.y));
            return true;
        }

        // Inverse of how the game itself categorizes a zone for its own UI
        // (confirmed against ZoneOrganizer's IndexZones, which does this
        // exact AreaType/IsOffice/density switch): maps a live ZoneData +
        // ZonePropertiesData back to Core's own ZoneType enum. Returns
        // false for anything that doesn't correspond to one of Core's
        // seven categories (e.g. AreaType.None).
        // internal, not private - GameZonePrefabResolver reuses this exact
        // mapping to resolve a vanilla ZonePrefab by Core.ZoneType category
        // (the inverse direction), rather than duplicating the switch.
        internal static bool TryMapToCoreZoneType(ZoneData zoneData, ZonePropertiesData zoneProps, out CoreZoneType zoneType)
        {
            // Game.Buildings.PropertyUtils.GetZoneDensity only handles
            // Residential/Commercial/Industrial - anything else (confirmed
            // via decompiled source) hits a hard, uncatchable
            // UnityEngine.Assertions.Assert.IsTrue(false, "Unknown Zone
            // area type:...") instead of returning a sentinel. Found
            // in-game (2026-10-11): Platter registers its own internal
            // "PlatterUnzoned" ZonePrefab with AreaType.None, and this
            // method's query iterates every ZoneData+PrefabData entity in
            // the world, Platter's included - calling GetZoneDensity
            // before this already-correct default-case rejection ever ran
            // crashed CustomZoneBuildRequestSystem's whole OnUpdate with
            // an AssertionException the moment Platter was installed.
            // Guarding the call, not the switch below, which already
            // returns false for exactly this case once it's reached.
            if (zoneData.m_AreaType != AreaType.Residential &&
                zoneData.m_AreaType != AreaType.Commercial &&
                zoneData.m_AreaType != AreaType.Industrial)
            {
                zoneType = default;
                return false;
            }

            var density = PropertyUtils.GetZoneDensity(zoneData, zoneProps);
            var isOffice = zoneData.IsOffice();

            switch (zoneData.m_AreaType)
            {
                case AreaType.Residential when !isOffice && density == ZoneDensity.Low:
                    zoneType = CoreZoneType.ResidentialLow;
                    return true;
                case AreaType.Residential when !isOffice && density == ZoneDensity.Medium:
                    zoneType = CoreZoneType.ResidentialMedium;
                    return true;
                case AreaType.Residential when !isOffice:
                    zoneType = CoreZoneType.ResidentialHigh;
                    return true;
                case AreaType.Commercial when density == ZoneDensity.Low:
                    zoneType = CoreZoneType.CommercialLow;
                    return true;
                case AreaType.Commercial:
                    zoneType = CoreZoneType.CommercialHigh;
                    return true;
                case AreaType.Industrial when isOffice:
                    zoneType = CoreZoneType.Office;
                    return true;
                case AreaType.Industrial:
                    zoneType = CoreZoneType.Industrial;
                    return true;
                default:
                    zoneType = default;
                    return false;
            }
        }
    }
}

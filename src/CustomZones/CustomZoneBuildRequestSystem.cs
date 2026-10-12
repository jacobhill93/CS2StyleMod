using CS2StyleMod.Core;
using CS2StyleMod.CustomZones.GameAdapters;
using Game;
using Game.Common;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace CS2StyleMod.CustomZones
{
    // Owns the one timing requirement CustomZoneBuilder has: a
    // runtime-cloned prefab is only visible to the game's own
    // initialization systems during the exact frame it's added, one tick
    // before PrefabSystem's own update - see DECISIONS.md "A runtime-cloned
    // prefab is only visible to init systems for one frame" for the full
    // mechanism. Rather than trust every future caller (eventually a real
    // UI) to remember to call CustomZoneBuilder.BuildOrRebuild from the
    // right place, this system is registered UpdateBefore<_,
    // PrefabSystem>(MainLoop) and is the ONLY thing that ever calls it -
    // from its own OnUpdate. Everything else just calls Request(...), from
    // wherever it naturally runs (OnGameLoaded, a UI handler, anywhere),
    // and the actual build happens later, at the correct moment,
    // automatically. Also the one thing a caller needs for "create it when
    // I want, modify it when I want": requesting the same CustomZoneDefinition
    // again (e.g. after editing one of its Collections) rebuilds it in
    // place rather than creating a duplicate - see CustomZoneBuilder's own
    // comments for how.
    public partial class CustomZoneBuildRequestSystem : GameSystemBase
    {
        private readonly struct PendingRequest
        {
            public readonly CustomZoneDefinition Definition;
            public readonly IEnumerable<Collection> Collections;

            public PendingRequest(CustomZoneDefinition definition, IEnumerable<Collection> collections)
            {
                Definition = definition;
                Collections = collections;
            }
        }

        private readonly Queue<PendingRequest> m_PendingRequests = new Queue<PendingRequest>();
        private readonly Queue<Guid> m_PendingRetirements = new Queue<Guid>();

        // EXPERIMENTAL (branch experiment/platter-zone-nudge, 2026-10-11,
        // CONFIRMED WORKING IN-GAME) - see DECISIONS.md "Platter never
        // picks up our custom zones" for the full investigation. Platter's
        // own reactive zone cache (Platter.Systems.P_ZoneCacheSystem) only
        // does a full, correct re-cache (reading everything fresh -
        // m_ZonesAllQuery, not a frozen snapshot) when a
        // UIAssetCategoryData-tagged entity has Created/Updated/Deleted AT
        // THE SAME TIME its own zone-change query (m_ZonesModifiedQuery)
        // is non-empty - both conditions, same tick (confirmed by testing:
        // calling RequireAnyForUpdate twice ANDs the two groups together,
        // not OR). Tagging the zone's own UI group (e.g.
        // "ZonesResidential" - a widely-shared vanilla entity, not one we
        // own) with Updated, in the SAME tick as the build (so it overlaps
        // with the zone's own still-present Created tag), reliably
        // triggers Platter's full re-cache. Safe for this specific
        // purpose even though it skips the usual one-frame settling delay:
        // this nudge never reads the zone's own state, and Platter's
        // resulting re-cache reads zone data live when it runs, not from
        // a snapshot taken now. Still carries the audited risk noted in
        // DECISIONS.md - touches a shared vanilla entity's tag state every
        // build, with no way to fully rule out some other system also
        // watching the same generic tag reacting unexpectedly - confirmed
        // clean across this session's testing, not provably safe forever.
        private CustomZoneBuilder m_Builder;
        private PrefabSystem m_PrefabSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Builder = new CustomZoneBuilder(World, new GameBuildingCatalog(World));
        }

        // Safe to call from anywhere, at any time, including OnGameLoaded -
        // this only ever enqueues. The actual clone+AddPrefab work happens
        // later, from this system's own OnUpdate. The caller resolves
        // definition.CollectionIds into real Collections itself (e.g. via
        // Mod.CollectionLibrary) - this system deliberately doesn't reach
        // for that global itself, so a test/debug caller can pass synthetic
        // Collections directly without touching real persisted data.
        public void Request(CustomZoneDefinition definition, IEnumerable<Collection> collections)
        {
            m_PendingRequests.Enqueue(new PendingRequest(definition, collections));
        }

        // Same "safe to call from anywhere, actually runs later at the
        // right moment" contract as Request above - deactivates (doesn't
        // remove) a deleted definition's already-built zone prefab, see
        // CustomZoneBuilder.RetireZone's own comment for why this goes
        // through the queue rather than calling PrefabSystem.AddOrUpdatePrefab
        // directly from wherever Delete happens to be called from.
        public void RequestRetirement(Guid definitionId)
        {
            m_PendingRetirements.Enqueue(definitionId);
        }

        protected override void OnUpdate()
        {
            while (m_PendingRequests.Count > 0)
            {
                var request = m_PendingRequests.Dequeue();
                var zone = m_Builder.BuildOrRebuild(request.Definition, request.Collections);
                NudgeGroup(zone);
            }

            while (m_PendingRetirements.Count > 0)
            {
                m_Builder.RetireZone(m_PendingRetirements.Dequeue());
            }
        }

        // EXPERIMENTAL, see the field comment above - resolves the zone's
        // UI group (the same UIObject.m_Group lookup RetireZone already
        // does) and tags it Updated immediately, same tick as the build.
        private void NudgeGroup(ZonePrefab zone)
        {
            if (zone == null || !zone.TryGet<UIObject>(out var uiObject) || uiObject.m_Group == null)
                return;

            if (!m_PrefabSystem.TryGetEntity(uiObject.m_Group, out var groupEntity) || !EntityManager.Exists(groupEntity))
                return;

            if (EntityManager.HasComponent<Updated>(groupEntity))
                return;

            EntityManager.AddComponent<Updated>(groupEntity);
            Mod.log.Info($"[CustomZoneBuildRequestSystem] Experimental Platter nudge: tagged group Entity {groupEntity.Index}:{groupEntity.Version} with Updated (same tick as build).");
        }
    }
}

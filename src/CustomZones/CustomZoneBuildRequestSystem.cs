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

        // EXPERIMENTAL (branch experiment/platter-zone-nudge, 2026-10-11) -
        // see DECISIONS.md "Platter never picks up our custom zones" for
        // the full investigation. Platter's own reactive zone cache
        // (Platter.Systems.P_ZoneCacheSystem) only does a full, correct
        // re-cache when a UIAssetCategoryData-tagged entity also gets
        // Created/Updated/Deleted in the same frame - something our
        // zone-only operations never trigger on their own. Tagging a
        // zone's UI group (e.g. "ZonesResidential" - a widely-shared
        // vanilla entity, not one we own) with Updated after a build
        // completes should make Platter's own OnUpdate notice and re-scan
        // everything, including our by-then-existing zone. Deliberately
        // NOT done in the same tick as the build itself - that's the
        // exact same one-frame race already found and fixed once this
        // session (see "A runtime-cloned prefab is only visible to init
        // systems for one frame"): the zone's own real ZoneData.m_ZoneType
        // index needs a full frame to settle via ZoneSystem.InitializeZonePrefabs
        // before nudging anything that might re-read it. This is
        // explicitly a risk/reward experiment, not a committed fix - see
        // the DECISIONS.md entry for why touching a shared vanilla
        // entity's tag state can't be fully audited for side effects on
        // other vanilla or third-party systems that might also watch the
        // same generic Updated tag. Revert this whole branch if anything
        // looks wrong in-game.
        private readonly Queue<Entity> m_PendingGroupNudges = new Queue<Entity>();

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
            // Process last tick's nudges FIRST, before this tick's own
            // builds queue up MORE nudges for the tick after - keeps each
            // nudge waiting exactly one full frame, never zero, never
            // more than it has to.
            while (m_PendingGroupNudges.Count > 0)
            {
                var groupEntity = m_PendingGroupNudges.Dequeue();
                if (EntityManager.Exists(groupEntity) && !EntityManager.HasComponent<Updated>(groupEntity))
                {
                    EntityManager.AddComponent<Updated>(groupEntity);
                    Mod.log.Info($"[CustomZoneBuildRequestSystem] Experimental Platter nudge: tagged group Entity {groupEntity.Index}:{groupEntity.Version} with Updated.");
                }
            }

            while (m_PendingRequests.Count > 0)
            {
                var request = m_PendingRequests.Dequeue();
                var zone = m_Builder.BuildOrRebuild(request.Definition, request.Collections);
                EnqueueGroupNudge(zone);
            }

            while (m_PendingRetirements.Count > 0)
            {
                m_Builder.RetireZone(m_PendingRetirements.Dequeue());
            }
        }

        // EXPERIMENTAL, see the field comment above - resolves the zone's
        // UI group (the same UIObject.m_Group lookup RetireZone already
        // does) and queues it for a one-tick-delayed Updated tag, rather
        // than tagging it immediately in the same frame as the zone's own
        // creation.
        private void EnqueueGroupNudge(ZonePrefab zone)
        {
            if (zone == null || !zone.TryGet<UIObject>(out var uiObject) || uiObject.m_Group == null)
                return;

            if (m_PrefabSystem.TryGetEntity(uiObject.m_Group, out var groupEntity))
                m_PendingGroupNudges.Enqueue(groupEntity);
        }
    }
}

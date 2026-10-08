using CS2StyleMod.Core;
using CS2StyleMod.CustomZones.GameAdapters;
using Game;
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
        private CustomZoneBuilder m_Builder;

        protected override void OnCreate()
        {
            base.OnCreate();
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

        protected override void OnUpdate()
        {
            while (m_PendingRequests.Count > 0)
            {
                var request = m_PendingRequests.Dequeue();
                m_Builder.BuildOrRebuild(request.Definition, request.Collections);
            }
        }
    }
}

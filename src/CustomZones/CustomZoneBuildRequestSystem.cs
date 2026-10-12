using ZoneBuilder.Core;
using ZoneBuilder.CustomZones.GameAdapters;
using Game;
using System;
using System.Collections.Generic;

namespace ZoneBuilder.CustomZones
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

        // How many collection entries BuildOrRebuildIncremental processes
        // per OnUpdate tick before yielding - see its own comment for why
        // this exists at all. Picked conservatively: even a single entry
        // can be expensive on first use this session (engine-side asset
        // loading, not this mod's own cost - see DECISIONS.md), so this
        // bounds worst-case per-tick cost to roughly
        // EntriesPerTick * (that one-time per-entry cost) rather than
        // eliminating it outright.
        private const int EntriesPerTick = 5;

        private readonly Queue<PendingRequest> m_PendingRequests = new Queue<PendingRequest>();
        private readonly Queue<Guid> m_PendingRetirements = new Queue<Guid>();
        private CustomZoneBuilder m_Builder;
        private IEnumerator<ZoneBuildProgress> m_ActiveBuild;

        // null means no build in progress - ZoneUISystem polls this every
        // UI tick (see ExtendedUISystemBase.CreateBinding's getter
        // overload) to drive the "building zone..." indicator. Set
        // together with m_ActiveBuild, always - never let one change
        // without the other.
        public ZoneBuildProgress? CurrentProgress { get; private set; }

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

        // Unlike Request above, this is NOT queued - it's a pure,
        // immediate read (see CustomZoneBuilder.HasLevel1Candidate's own
        // comment), safe to call directly from a UI trigger without
        // waiting for a frame.
        public bool HasLevel1Candidate(Core.ZoneType baseZoneType, IEnumerable<Collection> collections)
        {
            return m_Builder.HasLevel1Candidate(baseZoneType, collections);
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
            // Advance the in-progress build exactly one step (one entry
            // chunk) this tick, or start the next queued one - never
            // both in the same tick, which is the entire point: bound
            // how much of this tick a build can cost.
            if (m_ActiveBuild != null)
            {
                AdvanceActiveBuild();
            }
            else if (m_PendingRequests.Count > 0)
            {
                var request = m_PendingRequests.Dequeue();
                m_ActiveBuild = m_Builder.BuildOrRebuildIncremental(request.Definition, request.Collections, EntriesPerTick).GetEnumerator();
                AdvanceActiveBuild();
            }

            // Cheap (one RemovePrefab each) and unrelated to any
            // in-progress build - no reason to make these wait.
            while (m_PendingRetirements.Count > 0)
            {
                m_Builder.RetireZone(m_PendingRetirements.Dequeue());
            }
        }

        private void AdvanceActiveBuild()
        {
            if (m_ActiveBuild.MoveNext())
            {
                CurrentProgress = m_ActiveBuild.Current;
            }
            else
            {
                m_ActiveBuild = null;
                CurrentProgress = null;
            }
        }
    }
}

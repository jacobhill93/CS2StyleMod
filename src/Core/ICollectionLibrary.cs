using System;
using System.Collections.Generic;

namespace CS2StyleMod.Core
{
    // The global, cross-save collection library - see DECISIONS.md
    // ("Collections persist in a global library, referenced by GUID").
    // Zones/districts in a save hold a Collection's Id; resolving that Id
    // through this interface is how a GameAdapter gets the actual
    // Collection to pass into CandidateFilter. A missing Id (deleted
    // collection) is the caller's job to treat as "skip it" - that's what
    // lets FallbackPolicy take over.
    public interface ICollectionLibrary
    {
        IReadOnlyList<Collection> GetAll();
        bool TryGet(Guid id, out Collection collection);

        // Insert, or update in place if a collection with this Id already exists.
        void Save(Collection collection);

        // No-op if the Id isn't present. No attempt to check whether anything
        // currently references it - see DECISIONS.md.
        void Delete(Guid id);
    }
}

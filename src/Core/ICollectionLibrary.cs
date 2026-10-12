using System;
using System.Collections.Generic;

namespace ZoneBuilder.Core
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

        // Returns a copy, not the library's own internal reference -
        // mutating it has no effect until passed back to Save. This
        // matters for exception safety: Save's rollback-on-failure
        // guarantee (below) depends on the library's stored value staying
        // untouched until a write actually succeeds.
        bool TryGet(Guid id, out Collection collection);

        // Insert, or update in place if a collection with this Id already
        // exists. Strong exception safety: a persistence failure (disk
        // full, file locked, permissions denied) throws and leaves the
        // library's state exactly as it was before the call - never a
        // partial "looks saved, isn't on disk" state. Callers should
        // catch and decide what to do (e.g. log and skip a dependent
        // refresh) rather than let it propagate uncaught.
        void Save(Collection collection);

        // No-op if the Id isn't present. No attempt to check whether anything
        // currently references it - see DECISIONS.md. Same exception-safety
        // guarantee as Save.
        void Delete(Guid id);
    }
}

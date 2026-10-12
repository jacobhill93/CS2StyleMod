using System;
using System.Collections.Generic;

namespace ZoneBuilder.Core
{
    // The global, cross-save library of custom zone definitions - same
    // persistence shape as ICollectionLibrary, and for the same reason
    // (see DECISIONS.md "Collections persist in a global library,
    // referenced by GUID"). A GameAdapter resolves a definition's
    // CollectionIds through ICollectionLibrary to get the actual
    // Collections to build from.
    public interface ICustomZoneDefinitionLibrary
    {
        IReadOnlyList<CustomZoneDefinition> GetAll();

        // Returns a copy, not the library's own internal reference - see
        // ICollectionLibrary.TryGet's identical comment for why.
        bool TryGet(Guid id, out CustomZoneDefinition definition);

        // Insert, or update in place if a definition with this Id already
        // exists. Strong exception safety: a persistence failure (disk
        // full, file locked, permissions denied) throws and leaves the
        // library's state exactly as it was before the call - never a
        // partial "looks saved, isn't on disk" state. Callers should
        // catch and decide what to do (e.g. log and skip a dependent
        // rebuild) rather than let it propagate uncaught.
        void Save(CustomZoneDefinition definition);

        // No-op if the Id isn't present. Same exception-safety guarantee as Save.
        void Delete(Guid id);
    }
}

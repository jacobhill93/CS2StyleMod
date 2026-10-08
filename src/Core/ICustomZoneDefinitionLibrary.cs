using System;
using System.Collections.Generic;

namespace CS2StyleMod.Core
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
        bool TryGet(Guid id, out CustomZoneDefinition definition);

        // Insert, or update in place if a definition with this Id already exists.
        void Save(CustomZoneDefinition definition);

        // No-op if the Id isn't present.
        void Delete(Guid id);
    }
}

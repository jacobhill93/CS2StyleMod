using CS2StyleMod.Core;
using CS2StyleMod.CustomZones;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2StyleMod.UI
{
    // Exposes Mod.CustomZoneDefinitionLibrary to the UI - list, create,
    // rename, delete, and attach/detach Collections. Every mutation here
    // saves AND immediately enqueues a CustomZoneBuildRequestSystem.Request
    // (never builds directly itself - that system owns the one timing
    // requirement a build has, see its own comments) so a zone is visible
    // and paintable in-game the moment it's created, and updates live as
    // collections are attached/detached, with no separate "save" step.
    //
    // Built on ExtendedUISystemBase - see CollectionUISystem's comment for
    // why ZoneUIEntry keeps its own hand-written IJsonWritable.Write()
    // rather than relying on GenericUIWriter's reflection (camelCase JSON
    // key preservation).
    public partial class ZoneUISystem : ExtendedUISystemBase
    {
        private ValueBindingHelper<ZoneUIEntry[]> m_Zones;
        private CustomZoneBuildRequestSystem m_BuildRequestSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_BuildRequestSystem = World.GetOrCreateSystemManaged<CustomZoneBuildRequestSystem>();

            m_Zones = CreateBinding("Zones", GetEntries());

            CreateTrigger<string, string, string>("CreateZone", OnCreateZone);
            CreateTrigger<string>("DeleteZone", OnDeleteZone);
            CreateTrigger<string, string>("RenameZone", OnRenameZone);
            CreateTrigger<string, string>("AddCollectionToZone", OnAddCollectionToZone);
            CreateTrigger<string, string>("RemoveCollectionFromZone", OnRemoveCollectionFromZone);
        }

        private void OnCreateZone(string id, string name, string baseZoneType)
        {
            if (!Guid.TryParse(id, out var guid) || string.IsNullOrWhiteSpace(name))
                return;
            if (!Enum.TryParse<ZoneType>(baseZoneType, out var zoneType))
                return;

            // Chosen once, at creation, not editable afterward - see
            // DECISIONS.md "A zone's base type is fixed at creation, not
            // editable afterward" for why: BuildOrRebuild finds-or-clones
            // its zone by a stable internal name derived from
            // definition.Id, not BaseZoneType, so changing it post-creation
            // wouldn't actually re-clone a different vanilla zone - it'd
            // just change which buildings the eligibility filter accepts
            // while the zone itself kept whatever density it was first
            // cloned as. Built immediately with zero collections so the
            // zone shell exists and is paintable right away, before any
            // building is attached.
            var definition = new CustomZoneDefinition(guid, name, zoneType);
            Mod.CustomZoneDefinitionLibrary.Save(definition);
            m_BuildRequestSystem.Request(definition, Enumerable.Empty<Collection>());
            RefreshZones();
        }

        private void OnDeleteZone(string id)
        {
            if (!Guid.TryParse(id, out var guid))
                return;

            // Deleting the definition doesn't retract what's already built
            // in-game - same "removing doesn't touch what's already built"
            // philosophy as retiring a building, see DECISIONS.md. A
            // zone/buildings already painted with stay as they are.
            Mod.CustomZoneDefinitionLibrary.Delete(guid);
            RefreshZones();
        }

        private void OnRenameZone(string id, string newName)
        {
            if (!Guid.TryParse(id, out var guid) || string.IsNullOrWhiteSpace(newName))
                return;
            if (!Mod.CustomZoneDefinitionLibrary.TryGet(guid, out var definition))
                return;

            definition.Name = newName;
            Mod.CustomZoneDefinitionLibrary.Save(definition);
            // Rebuild so the in-game display name (CustomZoneBuilder.RegisterDisplayName) picks up the rename too.
            m_BuildRequestSystem.Request(definition, ResolveCollections(definition));
            RefreshZones();
        }

        private void OnAddCollectionToZone(string zoneId, string collectionId)
        {
            if (!Guid.TryParse(zoneId, out var zoneGuid) || !Guid.TryParse(collectionId, out var collectionGuid))
                return;
            if (!Mod.CustomZoneDefinitionLibrary.TryGet(zoneGuid, out var definition))
                return;

            definition.AddCollection(collectionGuid);
            Mod.CustomZoneDefinitionLibrary.Save(definition);
            m_BuildRequestSystem.Request(definition, ResolveCollections(definition));
            RefreshZones();
        }

        private void OnRemoveCollectionFromZone(string zoneId, string collectionId)
        {
            if (!Guid.TryParse(zoneId, out var zoneGuid) || !Guid.TryParse(collectionId, out var collectionGuid))
                return;
            if (!Mod.CustomZoneDefinitionLibrary.TryGet(zoneGuid, out var definition))
                return;

            definition.RemoveCollection(collectionGuid);
            Mod.CustomZoneDefinitionLibrary.Save(definition);
            m_BuildRequestSystem.Request(definition, ResolveCollections(definition));
            RefreshZones();
        }

        private void RefreshZones()
        {
            m_Zones.Value = GetEntries();
        }

        private static IEnumerable<Collection> ResolveCollections(CustomZoneDefinition definition)
        {
            foreach (var collectionId in definition.CollectionIds)
            {
                if (Mod.CollectionLibrary.TryGet(collectionId, out var collection))
                    yield return collection;
            }
        }

        private static ZoneUIEntry[] GetEntries()
        {
            return Mod.CustomZoneDefinitionLibrary.GetAll()
                .Select(z => new ZoneUIEntry(
                    z.Id.ToString(),
                    z.Name,
                    z.BaseZoneType.ToString(),
                    z.CollectionIds.Select(id => id.ToString()).ToArray()))
                .ToArray();
        }
    }
}

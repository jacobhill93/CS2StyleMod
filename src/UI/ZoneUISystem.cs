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
            if (!TrySave(definition, "create"))
                return;

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
            if (!TryDelete(guid))
                return;

            RefreshZones();
        }

        private void OnRenameZone(string id, string newName)
        {
            if (!Guid.TryParse(id, out var guid) || string.IsNullOrWhiteSpace(newName))
                return;
            if (!Mod.CustomZoneDefinitionLibrary.TryGet(guid, out var definition))
                return;

            definition.Name = newName;
            if (!TrySave(definition, "rename"))
                return;

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
            if (!TrySave(definition, "attach a collection to"))
                return;

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
            if (!TrySave(definition, "detach a collection from"))
                return;

            m_BuildRequestSystem.Request(definition, ResolveCollections(definition));
            RefreshZones();
        }

        private void RefreshZones()
        {
            m_Zones.Value = GetEntries();
        }

        // Centralizes the try/catch every mutation above needs - see
        // CollectionUISystem.TrySave's identical reasoning: strong
        // exception safety lives in Core (ICustomZoneDefinitionLibrary),
        // but Core deliberately doesn't log (no logging dependency by
        // design), so this is where a real, previously-uncaught failure
        // (disk full, file locked, permissions denied) actually gets
        // surfaced, and where a now-pointless rebuild request is skipped
        // since nothing actually changed.
        private static bool TrySave(CustomZoneDefinition definition, string action)
        {
            try
            {
                Mod.CustomZoneDefinitionLibrary.Save(definition);
                return true;
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, $"Failed to {action} zone '{definition.Id}' (\"{definition.Name}\") - change was not saved.");
                return false;
            }
        }

        private static bool TryDelete(Guid id)
        {
            try
            {
                Mod.CustomZoneDefinitionLibrary.Delete(id);
                return true;
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, $"Failed to delete zone '{id}' - it may still be present on disk.");
                return false;
            }
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

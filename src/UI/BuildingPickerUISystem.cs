using Colossal.Serialization.Entities;
using ZoneBuilder.CustomZones.GameAdapters;
using Game;
using System.Linq;

namespace ZoneBuilder.UI
{
    // Exposes GameBuildingCatalog.GetAllBuildings() to the UI for the
    // building picker. Bare list only - no search/filter trigger bindings
    // yet, those are planned as client-side filtering in React over this
    // same full list, same approach Find It uses, not a server-side query.
    //
    // Built on ExtendedUISystemBase - see CollectionUISystem's comment for
    // why BuildingPickerEntry keeps its own hand-written
    // IJsonWritable.Write() rather than relying on GenericUIWriter's
    // reflection (camelCase JSON key preservation).
    public partial class BuildingPickerUISystem : ExtendedUISystemBase
    {
        private ValueBindingHelper<BuildingPickerEntry[]> m_Buildings;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Empty initial value is deliberate - OnCreate runs before any
            // prefab (vanilla, DLC, or Paradox Mods pack) is registered, so
            // querying GameBuildingCatalog here would always return zero
            // results (confirmed: this was the actual bug, an empty panel
            // with no error logged anywhere). Populated for real in
            // OnGameLoaded below, same timing class as every other
            // ECS-dependent system this session - see DECISIONS.md "A
            // runtime-cloned prefab is only visible to init systems for one
            // frame" for the general pattern this is an instance of.
            m_Buildings = CreateBinding("Buildings", System.Array.Empty<BuildingPickerEntry>());
        }

        protected override void OnGameLoaded(Context context)
        {
            base.OnGameLoaded(context);

            var catalog = new GameBuildingCatalog(World);
            var entries = catalog.GetAllBuildings()
                .Select(b => new BuildingPickerEntry(b.PrefabId, b.ZoneType.ToString(), b.Level, b.LotSize.Width, b.LotSize.Depth, b.HeightMeters, b.AssetPackIds, b.IconPath))
                .ToArray();

            m_Buildings.Value = entries;
        }
    }
}

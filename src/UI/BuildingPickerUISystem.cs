using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using CS2StyleMod.CustomZones.GameAdapters;
using Game;
using Game.UI;
using System.Linq;

namespace CS2StyleMod.UI
{
    // Exposes GameBuildingCatalog.GetAllBuildings() to the UI for the
    // building picker. Bare list only - no search/filter trigger bindings
    // yet, those are planned as client-side filtering in React over this
    // same full list, same approach Find It uses, not a server-side query.
    public partial class BuildingPickerUISystem : UISystemBase
    {
        private const string Group = nameof(CS2StyleMod);

        private ValueBinding<BuildingPickerEntry[]> m_Buildings;

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
            //
            // Same ValueWriters.Create<T[]>() limitation as
            // CollectionUISystem's Collections binding - see that class's
            // comment. Explicit ArrayWriter<BuildingPickerEntry> required.
            m_Buildings = new ValueBinding<BuildingPickerEntry[]>(Group, "Buildings", System.Array.Empty<BuildingPickerEntry>(), new ArrayWriter<BuildingPickerEntry>(new BuildingPickerEntryWriter(), false));
            AddBinding(m_Buildings);
        }

        protected override void OnGameLoaded(Context context)
        {
            base.OnGameLoaded(context);

            var catalog = new GameBuildingCatalog(World);
            var entries = catalog.GetAllBuildings()
                .Select(b => new BuildingPickerEntry(b.PrefabId, b.ZoneType.ToString(), b.Level, b.LotSize.Width, b.LotSize.Depth, b.HeightMeters, b.AssetPackIds))
                .ToArray();

            m_Buildings.Update(entries);
        }

        private sealed class BuildingPickerEntryWriter : IWriter<BuildingPickerEntry>
        {
            public void Write(IJsonWriter writer, BuildingPickerEntry value) => value.Write(writer);
        }
    }
}

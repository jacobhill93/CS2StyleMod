import { ModRegistrar } from "cs2/modding";
import { ShellFeature } from "mods/ShellFeature/ShellFeature";

// UI_OVERHAUL_PLAN.md Phase 1: one shell replaces the old separate
// CollectionsFeature/ZonesFeature appends and the four floating panels
// they owned (CollectionPanel/ZonePanel/BuildingPicker/
// ZoneCollectionsPicker) - deleted once the shell's equivalents were
// confirmed working in-game and committed.
const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Game', ShellFeature);
}

export default register;

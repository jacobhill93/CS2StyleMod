import { ModRegistrar } from "cs2/modding";
import { ShellFeature } from "mods/ShellFeature/ShellFeature";

// UI_OVERHAUL_PLAN.md Phase 1: one shell replaces the old separate
// CollectionsFeature/ZonesFeature appends. Those two (and the panels they
// own) stay in the repo unregistered, not deleted, until the shell's
// Zones/Collections tabs are confirmed working in-game - see the plan's
// "What NOT to do".
const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Game', ShellFeature);
}

export default register;

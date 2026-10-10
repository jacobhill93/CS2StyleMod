import { ModRegistrar } from "cs2/modding";
import { ShellFeature } from "mods/ShellFeature/ShellFeature";
import { UniversalModMenuToggle } from "mods/UniversalModMenuToggle/UniversalModMenuToggle";

// UI_OVERHAUL_PLAN.md Phase 1: one shell replaces the old separate
// CollectionsFeature/ZonesFeature appends and the four floating panels
// they owned (CollectionPanel/ZonePanel/BuildingPicker/
// ZoneCollectionsPicker) - deleted once the shell's equivalents were
// confirmed working in-game and committed.
//
// ShellFeature stays registered on 'Game' unconditionally - it now
// renders null while closed (shared/shellVisibility.ts) rather than
// being appended/removed, so its internal tab/selection state survives
// a close+reopen. UniversalModMenu couldn't host the real panel content
// (confirmed in-game too small - see DECISIONS.md), so it's used purely
// as the native open/close trigger instead.
const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Game', ShellFeature);
    moduleRegistry.append('UniversalModMenu', UniversalModMenuToggle);
}

export default register;

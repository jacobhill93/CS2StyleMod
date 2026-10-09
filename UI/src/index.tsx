import { ModRegistrar } from "cs2/modding";
import { CollectionPanel } from "mods/CollectionPanel/CollectionPanel";
import { BuildingPicker } from "mods/BuildingPicker/BuildingPicker";

const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Game', CollectionPanel);
    moduleRegistry.append('Game', BuildingPicker);
}

export default register;
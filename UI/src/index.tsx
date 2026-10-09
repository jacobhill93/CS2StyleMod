import { ModRegistrar } from "cs2/modding";
import { CollectionPanel } from "mods/CollectionPanel/CollectionPanel";

const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Game', CollectionPanel);
}

export default register;
import { ModRegistrar } from "cs2/modding";
import { CollectionsFeature } from "mods/CollectionsFeature/CollectionsFeature";

const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Game', CollectionsFeature);
}

export default register;

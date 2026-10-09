import { ModRegistrar } from "cs2/modding";
import { CollectionsFeature } from "mods/CollectionsFeature/CollectionsFeature";
import { ZonesFeature } from "mods/ZonesFeature/ZonesFeature";

const register: ModRegistrar = (moduleRegistry) => {

    moduleRegistry.append('Game', CollectionsFeature);
    moduleRegistry.append('Game', ZonesFeature);
}

export default register;

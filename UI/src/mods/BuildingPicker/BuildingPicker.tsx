import { bindValue, useValue } from "cs2/api";
import { Panel, Scrollable } from "cs2/ui";
import mod from "../../../mod.json";

interface BuildingEntry {
    prefabId: string;
    zoneType: string;
    level: number;
    lotWidth: number;
    lotDepth: number;
    heightMeters: number;
    assetPackIds: string[];
}

const Buildings$ = bindValue<BuildingEntry[]>(mod.id, "Buildings", []);

// Bare list only - proves the binding pipeline for GetAllBuildings() end to
// end. Search/filter controls and the "add to collection" action are
// planned next, as client-side React logic over this same full list.
export const BuildingPicker = () => {
    const buildings = useValue(Buildings$);

    return (
        <Panel
            header={`Buildings (${buildings.length})`}
            style={{ position: "absolute", top: "100px", left: "440px", width: "420px" }}
        >
            <Scrollable vertical style={{ maxHeight: "400px" }}>
                {buildings.map((building) => (
                    <div
                        key={building.prefabId}
                        style={{ display: "flex", justifyContent: "space-between", padding: "2px 0", fontSize: "12px" }}
                    >
                        <span>{building.prefabId}</span>
                        <span>
                            {building.zoneType} L{building.level} {building.lotWidth}x{building.lotDepth} [{building.assetPackIds.join(", ")}]
                        </span>
                    </div>
                ))}
            </Scrollable>
        </Panel>
    );
};

import { bindValue } from "cs2/api";

// Vanilla game binding, not ours - confirmed via Road Builder's real,
// shipped SidePanel.tsx (MIT, github.com/JadHajjar/RoadBuilder-CSII),
// which reads this exact group/name to shift its own fixed-position panel
// down whenever the player has the FPS meter on, so it never overlaps.
// Value is the FPS meter's verbosity level (0 = off, 1-3 = increasingly
// detailed/taller).
export const fpsMeterLevel$ = bindValue<number>("app", "fpsMode", 0);

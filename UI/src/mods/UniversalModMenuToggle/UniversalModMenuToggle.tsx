import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { shellVisible$, toggleShell } from "shared/shellVisibility";

// Native open/close entry point (UI_OVERHAUL_PLAN.md). Replaces the
// earlier UniversalModMenuProbe - confirmed in-game (2026-10-09) that
// the host box is too small for real content (see DECISIONS.md), so
// this is trigger-only: toggles ShellFeature's visibility, which stays
// registered separately on 'Game'. Plain text Button, no icon - icon+
// text Button combos are confirmed broken in this engine (see
// DECISIONS.md "icon overlap" entry), and the dock already showed a
// generic default icon for us without one declared in mod.json anyway.
export const UniversalModMenuToggle = () => {
    const visible = useValue(shellVisible$);
    return (
        <Button variant="flat" selected={visible} onSelect={toggleShell}>
            CS2StyleMod
        </Button>
    );
};

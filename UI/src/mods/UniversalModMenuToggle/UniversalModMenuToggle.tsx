import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { shellVisible$, toggleShell } from "shared/shellVisibility";
import zbIcon from "images/zb-icon.svg";

// Native open/close entry point (UI_OVERHAUL_PLAN.md). Replaces the
// earlier UniversalModMenuProbe - confirmed in-game (2026-10-09) that
// the host box is too small for real content (see ARCHITECTURE.md), so
// this is trigger-only: toggles ShellFeature's visibility, which stays
// registered separately on 'Game'.
//
// Icon-only (variant="icon" + src + tooltipLabel), not icon+text - icon+
// text Button combos are confirmed broken in this engine, same reason
// every other icon button in this codebase (trash/pencil/plus) is
// icon-only too. Previously plain text ("ZoneBuilder") specifically
// because no custom icon asset had ever been bundled yet - the webpack
// asset/resource rule for this existed unused from the start (see
// webpack.config.js). zb-icon.svg is a plain bold "ZB" glyph, solid
// fill on a transparent background, not yet confirmed whether this
// engine's Button masks/tints a custom src the same way it does for
// built-in Media/Glyphs/*.svg icons - needs an in-game look.
export const UniversalModMenuToggle = () => {
    const visible = useValue(shellVisible$);
    return <Button variant="icon" src={zbIcon} tooltipLabel="Zone Builder" selected={visible} onSelect={toggleShell} />;
};

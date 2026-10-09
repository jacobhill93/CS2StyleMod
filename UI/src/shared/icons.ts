// Unified Icon Library (github.com/algernon-A/UnifiedIconLibrary,
// Apache-2.0) - referenced by URL at runtime, nothing copied/bundled.
// Purely optional: the mod "doesn't do anything unless you have a mod
// that uses it" (its own README) - if a player doesn't have UIL
// installed, these coui:// paths 404 silently and cs2/ui's Button just
// renders its text label with no icon. No dependency declaration is
// needed for this to work - confirmed via other mods' actual deployed
// output that this is a plain runtime URL reference, not an API call.
//
// Only icon names actually confirmed to exist in UIL's real catalog are
// used here (via GitHub tree listing, not guessed) - an unconfirmed name
// would just 404 into a blank icon slot, worse than no icon at all.
const UIL_STYLE = "Standard";

function uilIcon(name: string): string {
    return `coui://uil/${UIL_STYLE}/${name}.svg`;
}

export const Icons = {
    plus: uilIcon("Plus"),
    trash: uilIcon("Trash"),
    pencil: uilIcon("Pencil"),
};

import { Theme } from "cs2/bindings";
import { getModule } from "cs2/modding";

// Copied from the pattern in RoadBuilder's TextInput.tsx (MIT - see
// THIRD_PARTY_NOTICES.md) - Road Builder itself reads this as a one-off
// getModule call, not through its VanillaComponentResolver, so this
// project does the same rather than forcing it into
// VanillaComponentResolver.tsx for no reason.
//
// Verified against this project's own installed game (2026-10-12, not
// copied from Road Builder on faith) - classes.input resolves to
// "input_Wfi" in the installed Content/Game/UI/index.js, confirmed by
// grepping the compiled bundle directly. See TOUCHPOINTS.md's UI section.
export const TextInputTheme: Theme | any = getModule("game-ui/editor/widgets/item/editor-item.module.scss", "classes");

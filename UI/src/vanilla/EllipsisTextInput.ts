import { getModule } from "cs2/modding";

// One-off getModule export, modelled on this repo's own TextInputTheme.ts
// (not a wholesale VanillaComponentResolver - that file was deleted in
// the Phase 7.3 revert for having no consumer; one consumer earns one
// export). Prop shape taken from Road Builder's own guessed type
// (MIT - see THIRD_PARTY_NOTICES.md) as a starting hypothesis, then
// corrected against this project's own installed game bundle - NOT
// trusted on Road Builder's say-so, since its own header comment admits
// "the types of properties are just guessed."
//
// Verified 2026-10-12 by reading the actual compiled component directly
// out of the installed Content/Game/UI/index.js:
//   var b$=Y.forwardRef((function({value:e,maxLength:t=64,theme:n=f$,
//   disableHint:s=!1,forceHint:i=!1,className:a,onFocus:o,onBlur:r,...l},c)
// Confirms: maxLength defaults to 64 (matches Road Builder's guess);
// disableHint/forceHint are real props Road Builder's own type omits
// entirely; onFocus/onBlur are wrapped internally (tracks an active-hint
// state) but still invoke whatever's passed in; everything else
// (onChange, onKeyDown, placeholder, etc.) spreads straight onto the
// underlying input via ...rest, not individually declared here. The
// forwardRef passes `ref` directly to that underlying input element, so
// unlike Road Builder's own usage (which walks `firstChild.firstChild`
// to reach a real DOM node), a plain ref here should already resolve to
// the actual <input> - treated as the working hypothesis, flagged as a
// finding if it turns out not to hold in-game.
// See TOUCHPOINTS.md's "UI vanilla module reuse" table for the raw grep
// evidence and verification date.
export interface EllipsisTextInputProps {
    value?: string;
    maxLength?: number;
    className?: string;
    disableHint?: boolean;
    forceHint?: boolean;
    onFocus?: (event: React.FocusEvent<HTMLInputElement>) => void;
    onBlur?: (event: React.FocusEvent<HTMLInputElement>) => void;
    onChange?: (event: React.ChangeEvent<HTMLInputElement>) => void;
    onKeyDown?: (event: React.KeyboardEvent<HTMLInputElement>) => void;
}

export const EllipsisTextInput: React.ForwardRefExoticComponent<
    EllipsisTextInputProps & React.RefAttributes<HTMLInputElement>
> = getModule("game-ui/common/input/text/ellipsis-text-input/ellipsis-text-input.tsx", "EllipsisTextInput");

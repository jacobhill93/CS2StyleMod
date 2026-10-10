import { bindLocalValue } from "cs2/api";

// Purely client-side, local-only state - no C# round-trip needed for
// "is the panel open", which isn't game-save data. bindLocalValue is a
// real cs2/api primitive (confirmed via UI/types/api.d.ts: LocalValueBinding
// implements the same ValueBinding<T> interface every C#-backed binding
// in this mod already uses, so useValue() works on it identically).
// Starts closed - a fresh load shouldn't cover the screen until the
// player opens it via the UniversalModMenu entry (UniversalModMenuToggle.tsx).
export const shellVisible$ = bindLocalValue(false);

export const toggleShell = () => shellVisible$.update(!shellVisible$.value);
export const closeShell = () => shellVisible$.update(false);

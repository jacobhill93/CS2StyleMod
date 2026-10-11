// Built-in game icons (2026-10-12), replacing the earlier Unified Icon
// Library (UIL)-sourced versions - zero dependency, always present,
// same approach Road Builder's real shipped source uses
// (`<img src="Media/Tools/Snap Options/All.svg" />`, fetched and read
// directly this session): a plain relative path, no coui:// scheme.
// Works because mod panels render into the same document as the
// vanilla game UI (confirmed via this project's own earlier findings -
// the shared 'Game' render root with no error boundary), so a relative
// URL resolves against the game's own UI bundle root
// (Content/Game/UI/), not anything mod-specific.
//
// Paths confirmed to exist by listing the actual installed game's
// Content/Game/UI/Media folder, not guessed - same discipline as every
// other game-file-dependent claim this project makes:
//   Media/Glyphs/Plus.svg
//   Media/Glyphs/Trash.svg
//   Media/Editor/Edit.svg
export const Icons = {
    plus: "Media/Glyphs/Plus.svg",
    trash: "Media/Glyphs/Trash.svg",
    pencil: "Media/Editor/Edit.svg",
};

// RFC4122-ish v4 GUID via Math.random - not cryptographically secure, which
// is fine for these ids (none are a security boundary). Deliberately not
// crypto.randomUUID(): cohtml's embedded JS runtime hasn't been confirmed
// to expose the Web Crypto API, and a raw <select> already taught this
// codebase not to assume a standard browser API just works here without
// checking (see DECISIONS.md).
export function generateGuid(): string {
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
        const r = (Math.random() * 16) | 0;
        const v = c === "x" ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

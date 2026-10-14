// UI_UPGRADE_GUIDE.md Phase 2 step 4 - one canonical definition, replacing
// three separately-declared (and drifted - ZonesTab.tsx's own copy was
// missing prefabIds) copies of the same wire shape. Field set is the
// full union actually written by CollectionUIEntry.cs's Write()
// (id, name, entryCount, prefabIds) - a tab that doesn't need prefabIds
// (ZonesTab) just doesn't read it, rather than redeclaring a narrower
// type.
export interface CollectionEntry {
    id: string;
    name: string;
    entryCount: number;
    prefabIds: string[];
}

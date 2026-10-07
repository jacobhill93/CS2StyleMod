# Decisions & open questions

## Decisions

### Build both features, as one mod with per-feature toggles
- Both share the core (collections, adopted assets, building picker). Two
  mods would mean either duplicating it or a third library mod and
  lockstep releases on every game patch.
- Save-safety concerns are handled by the Custom Zones toggle instead.
- Code is split into modules so it could be repackaged as separate mods
  later without a rewrite.

### Use collections as the shared abstraction
Custom zones and district themes are two ways of applying a collection:
- custom zone = "this zone spawns only from collection X"
- district theme = "lots in this area spawn only from collections X, Y,
  restricted to zone types A, B"

### Adopted assets get a vanilla zone profile and are limited to themes
Unzoned assets (e.g. downloaded houses) are given growable data at load
time. By default they spawn only where a collection that includes them is
in use (custom zone or district theme), not citywide.

### Build order
1. Core: collections + adopted assets
2. Custom Zones: lower technical risk, likely reuses the vanilla spawner
   via zone/building prefab cloning
3. District Themes: requires replacing (or post-correcting)
   `ZoneSpawnSystem`, which is Burst-compiled and not easily patched

### Compatibility with Plop the Growables
District Themes only filter naturally spawned buildings and never remove
manually placed ones. Test alongside Plop the Growables, since both touch
zone checks and spawning.

## Prior art reviewed

| Mod | Game | Relevance |
|---|---|---|
| Plop the Growables | CS2 | Manual placement anywhere, level lock. Doesn't control spawning. |
| Find It | CS2 | Asset browser; reference for picker UI. |
| Asset Packs Manager | CS2 | Pack management only; no spawning control. |
| Zone Tools / Zoning Toolkit | CS2 | Zone painting tools; no spawning control. |
| Vanilla 1.5.9 | CS2 | Asset pack filter in build menu (placement only, as far as known). |
| Building Themes / Building Themes 2 | CS1 | Direct precedent for district themes. |
| District Styles Plus | CS1 | "Restrict by zone only vs zone + level" option worth copying. |
| Ploppable RICO | CS1 | Precedent for per-asset growable settings (adopted assets). |

No existing CS2 mod found that provides per-district or per-zone building
selection (a Steam "Asset Selector for Zoning" request thread had no
existing-mod answers). Re-check the in-game Paradox Mods browser and the CS
Modding Discord before heavy investment.

## Open questions (verify against game code)

- [ ] Does demand computation include cloned zone prefabs, or only vanilla
      zones? (Blocking for Custom Zones.)
- [ ] Can zone and building prefabs be cloned and registered at runtime?
- [ ] Do buildings level up in place or by prefab replacement? (Affects
      whether filtering must also apply to upgrades.)
- [ ] Does CS2 load multiple DLLs from one mod folder? (Affects how module
      boundaries are enforced.)
- [ ] What happens to already-placed adopted buildings if the mod is
      removed?
- [ ] What do "unzoned" downloaded assets actually contain: buildings
      without spawnable data, plain static objects, or partial growables?
- [ ] Interaction between themes and the EU/NA theme setting and asset
      packs.
- [ ] Fallback policy when a collection has no building for a lot's
      size/level.

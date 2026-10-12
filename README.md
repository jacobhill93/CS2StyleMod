# Zone Builder

A Cities: Skylines II mod for controlling **which buildings grow where**.

> Status: Custom Zones is implemented and confirmed working in-game -
> Collections/Zones/Assets management UI, zone cloning, building
> leveling, asset thumbnails. District Themes is shelved on a separate
> unmerged branch (see [ARCHITECTURE.md](ARCHITECTURE.md)). See
> [DECISIONS.md](DECISIONS.md) for the full history.

## Features

One mod, two independently toggleable features built on a shared core.

### Collections (core)

A **collection** is a named, user-defined set of buildings. It can contain:

- vanilla selectors, e.g. "Residential Low, NA theme" or a whole asset pack
- individual hand-picked growables (vanilla or from Paradox Mods)
- **adopted assets**: buildings that aren't growable on their own (e.g.
  downloaded houses with no zone), given mod-supplied growable settings
  (category, density, level, household count) so they can spawn naturally

Collections are the building blocks both features use.

### Custom Zones

Create new zone types backed by collections, e.g. "Brownstones (Res Low)".

- Each custom zone derives its simulation profile from one vanilla zone
  (Res Low, Row, Res Med, Commercial Low, ...).
- Painted with the normal zoning tools: cell by cell, lot fill, marquee,
  etc. Vanilla zoning tool behaviour is unchanged.
- Only buildings from the zone's collection(s) grow there.
- Editing a collection changes future spawns everywhere that zone is used,
  with no repainting needed.
- **Caveat:** saves that use custom zones depend on the mod.

### District Themes

Assign a theme to a district: one or more collections, plus include/exclude
rules for which zone types (vanilla or custom) the theme applies to.

- Inside a themed district, newly spawned buildings are drawn only from the
  theme's buildings that match the lot's zone and size.
- Fallback behaviour when a theme has no matching building (e.g. fall back
  to vanilla) is configurable.
- Removing the mod degrades gracefully: districts just stop filtering.
- Only affects naturally spawned buildings. Manually placed buildings (e.g.
  via Plop the Growables) are never touched.

## Settings

Each feature can be enabled/disabled independently. With Custom Zones
disabled, the mod creates no zone or building clones, so saves don't
depend on it.

## Dependencies

None required - the mod works fully on its own. Button icons
(Create/Edit/Delete/Add/Remove) come from the game's own built-in
`Media/...` assets (confirmed present on disk, same technique Road
Builder's real shipped UI uses), not a third-party icon pack - no
dependency needed for them. One optional, recommended companion:

| Mod | Why | Required? |
|---|---|---|
| Asset Icon Library ([source](https://github.com/JadHajjar/AssetIconLibrary-CSII)) | Per-building thumbnails in the Assets tab and Collection membership lists. We don't call into AIL at all - it writes its resolved icon URL directly into the same `UIObject.m_Icon` field we read, so this works passively if AIL is installed and covers a given building. | No - a visible placeholder box renders instead of a thumbnail when it's missing or doesn't cover a prefab. |

This list is documentation only for now, not yet wired into `UI/mod.json`'s
own `dependencies` field - its real schema (hard load-blocking requirement
vs. a soft/recommended entry, and the exact entry format) hasn't been
confirmed from a real working example yet. See DECISIONS.md.

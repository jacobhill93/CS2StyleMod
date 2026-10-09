# CS2StyleMod

A Cities: Skylines II mod for controlling **which buildings grow where**.

> Status: design phase. No code yet. See [ARCHITECTURE.md](ARCHITECTURE.md)
> and [DECISIONS.md](DECISIONS.md).

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

None required - the mod works fully on its own. One optional, recommended
companion:

| Mod | Why | Required? |
|---|---|---|
| [Unified Icon Library](https://mods.paradoxplaza.com/mods/74417/Windows) ([source](https://github.com/algernon-A/UnifiedIconLibrary)) | Button icons (Create/Edit/Delete/Add/Remove) across the Collections/Zones UI. Referenced by URL (`coui://uil/...`) at render time, not an API call - if it's not installed, those URLs 404 silently and buttons just show their text label with no icon. | No - purely cosmetic, nothing breaks without it. |

This list is documentation only for now, not yet wired into `UI/mod.json`'s
own `dependencies` field - its real schema (hard load-blocking requirement
vs. a soft/recommended entry, and the exact entry format) hasn't been
confirmed from a real working example yet. See DECISIONS.md.

---
title: "New Virginia (worked example)"
description: "A protected PvE starter zone: 3 ore types, 15 basic plant species, fertility 20."
weight: 30
---

# New Virginia (zone_TM) — worked example

A low-tier **protected PvE** zone — a safe place where new characters gather. Its
configuration shows the simplest end of the spectrum: few ore types, large nodes, and
the basic plant mix.

| Fact | Value |
|---|---|
| Zone id | 0 |
| Type | PvE (protected) |
| Size | 2048 × 2048 tiles |
| Fertility | 20 |
| Plant rule set | 0 — 15 species (basic "tm pve" mix) |
| Ore types | 3 (titan, crude, liquizit) |
| Total ore nodes | 21 (7 per type) |

## Ore configuration

| Material | Nodes | Max tiles/node | Total per node | Min threshold |
|---|---|---|---|---|
| titan | 7 | 1000 | 15,000,000 | 0.5 |
| crude | 7 | 1257 | 30,000,000 | 0.5 |
| liquizit | 7 | 1000 | 15,000,000 | 0.5 |

Notes that stand out:

- **Large, sparse nodes** — 1,000–1,257 tiles each (walk radius
  `(int)√(tiles/π)×2` = 34–40 tiles), so each node is a big blob, and there are only 7
  of each type per zone. Steady-state stock is ~420 million units of material in total
  (105 M titan + 210 M crude + 105 M liquizit).
- **No rare types** — no epriton, no flux ore, so there are no keep-out zones around
  bases and no NPC-spawning ore sites.
- `minthreshold` 0.5 means a tile is only usable if the node's noise fill there is at
  least half of its maximum — the edges of each blob are thin.

Per-type yields (what one unit of each ore refines into) are in
[Ores](/content/ores/).

## Plant mix

Rule set 0 allows 15 species — the basic starter set: grass (a/b), pine, bushes (a/b),
rango, copper tree, reed, nanowheat, low-tier rustbush/slimeroot/electroplant, bonsai,
and low-tier iron tree. No walls, no devrinol, no high-tier harvestables.

With fertility 20, a 32×32 cube may hold at most ~20% blocking plants; across the full
2048×2048 map that's an upper bound of roughly 838,000 plant tiles (less in practice —
water and islands are not ground). Species selection is weighted by each rule's
`fertility`, so the basic grasses and bushes dominate while the low-tier harvestables
stay rare.

Full per-species rules (growRate, fruit, health) are in [Plants](/content/plants/).

## What this means for a player

- Safe starter economy: three common ores in big, easy-to-scan blobs.
- Harvestables here are the low-tier variants — high-value plants (high-tier
  electroplant/iron tree/rustbush/slimeroot) appear in later zones.
- Because the zone is protected, PvP risk is the only thing missing from the
  gathering loop (see [movement](/features/movement/) for zone entry).

<!-- Developer notes: zones row id 0 (zonetype 1 = PvE, protected 1, fertility 20,
     plantruleset 0); mineralconfigs 3 rows (titan/crude/liquizit, 7 nodes each);
     plantrules rulesetid 0 = 15 files, note "tm pve". Node radius formula and
     threshold semantics: see generation.md. -->

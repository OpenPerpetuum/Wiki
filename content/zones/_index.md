---
title: "Zones"
description: "How zone resources are generated: ore nodes, plant populations, and worked examples of real zones."
weight: 50
---

# Zones

A **zone** is a 2D tile map (usually 2048×2048) where robots gather resources. Every
zone has two resource systems that the server maintains automatically:

- **Ore deposits** — scattered nodes in the ground with a finite amount of material per
  tile. Mined out below a threshold, a node is removed and a new one is generated
  elsewhere.
- **Plants** — a living population that grows, fruits, and spreads tile by tile, kept
  near a per-zone **fertility target**.

This section explains the mechanics and shows real zones as worked examples.

| Page | Contents |
|---|---|
| [Map](/zones/map/) | All zones plotted on the server's x/y grid, colored by galaxy, with the known rift gate connections |
| [Generation](/zones/generation/) | The rules: how ore nodes and plant populations are created, grown, and replaced |
| [Zone index](/zones/zone-index/) | Every zone: type, protection, fertility, plant species count, ore configuration |
| [New Virginia](/zones/zone-tm/) | Worked example — a protected PvE starter zone (3 ore types, basic plants) |
| [Daoden](/zones/zone-asi/) | Worked example — an open PvP alpha-tier zone (8 ore types incl. rare flux ore, rich plant mix) |
| [zone_gamma_z106](/zones/zone-gamma-z106/) | Worked example — a high-tier PvP zone (reduced ore nodes, low fertility, tier-2 plants) |

Player-facing gathering mechanics (how to actually mine and harvest) are in
[Resource gathering](/features/gathering/) in the features section; the stat tables for
what each ore and plant yields are in [Content](/content/).

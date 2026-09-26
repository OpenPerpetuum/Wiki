---
title: "zone_gamma_z106 (worked example)"
description: "A terraformable high-tier PvP zone: reduced ore nodes, fertility 15, tier-2 plant mix."
weight: 32
---

# zone_gamma_z106 — worked example

A high-tier **open PvP** zone: terraformable, low fertility, tier-2 plant variants, and
a tighter ore economy. This is the "endgame" shape of a zone — fewer, more contested
resources.

| Fact | Value |
|---|---|
| Zone id | 106 |
| Type | PvP (open) |
| Size | 2048 × 2048 tiles |
| Fertility | 15 |
| Terraformable | yes (PBS tech limit 2) |
| Plant rule set | 22 — 17 species ("Gamma Plant Rules", tier-2 mix) |
| Ore types | 7 (titan, crude, stermonit, imentium, liquizit, gammaterial, energy mineral) |
| Total ore nodes | 44 |

## Ore configuration

| Material | Nodes | Max tiles/node | Total per node | Min threshold |
|---|---|---|---|---|
| titan | 7 | 600 | 125,000,000 | 0.5 |
| crude | 4 | 600 | 125,000,000 | 0.5 |
| stermonit | 7 | 600 | 125,000,000 | 0.5 |
| imentium | 7 | 600 | 125,000,000 | 0.5 |
| liquizit | 4 | 600 | 125,000,000 | 0.5 |
| gammaterial | 7 | 100 | 10,000,000 | 0.5 |
| energy mineral | 8 | 300 | 24,000,000 | 0.5 |

Notes that stand out:

- **Fewer nodes than alpha zones** (44 vs 64 in Daoden): crude and liquizit drop to
  just 4 nodes each. Same per-node totals as the alpha standard ores, so the steady
  state (~3.9 billion units total) is leaner and slower to recover.
- **Two exotic types**: **gammaterial** in tiny 100-tile nodes (walk radius 10 tiles —
  small, easy to spot, ~1.4 M per node) and **energy mineral** at 24 M per node.
- No epriton, no flux ore — the rare/economic ores stay in the alpha tier.

## Plant mix

Rule set 22 allows 17 species: the basics, plus the **tier-2 variants** — high iron
tree t2 and high rustbush t2 as wild harvestables, and the **tier-2 seeded plants**
(high electroplant t2, high slimeroot t2). The seeded ones are `playerSeeded` — they
**never** spawn naturally; they only exist in player gardens (see
[gathering → plants](/features/gathering/)).

Fertility is **15**, not 20: a 32×32 cube may hold at most ~15% blocking plants, so
plant coverage is visibly sparser than in alpha zones, and the fertility draw has
slightly less room to fill in before the cube "saturates".

Per-species rules (including the t2 fruit amounts) are in
[Plants](/content/plants/); per-ore yields in [Ores](/content/ores/).

## What this means for a player

- Leaner, slower economy: fewer nodes of the common ores means mining pressure shows
  up faster — a mined-out crude node takes longer for the zone to replace.
- Sparser vegetation: with 15% fertility, open ground is the norm and plant patches
  are the exception.
- High-value gathering here is the tier-2 iron tree / rustbush wild plants, plus
  running player gardens of the seeded t2 electroplant and slimeroot.
- Terraforming (PBS tech limit 2) means the ground itself can be reshaped by players
  (see [Power base stations](/features/pbs/)).

<!-- Developer notes: zones row id 106 (zonetype 2 = PvP, protected 0, fertility 15,
     terraformable 1, pbsTechLimit 2, plantruleset 22); mineralconfigs 7 rows (crude
     and liquizit 4 nodes each); plantrules rulesetid 22 = 17 files, note "Gamma
     Plant Rules"; electroplant_seeded_t2.txt / slimeroot_seeded_t2.txt set
     playerSeeded=n1 (inherited via source= from electroplant_hi.txt /
     slimeroot_hi.txt) -> excluded from natural spawns in
     PlantRuleExtensions.GetWinnerPlantTypeBasedOnFertility. -->

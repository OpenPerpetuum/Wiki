---
title: "Ore fields"
description: "The minerals and mineralconfigs tables, node-generation formulas, node lifecycle, and extraction mechanics."
weight: 30
---

# Ore fields

Ore data has two levels: **per-type** (`minerals` — what the material is and how much
one extraction yields) and **per-zone-per-type** (`mineralconfigs` — how many nodes,
how big, how full). Runtime node state lives in the `mineralnodes` table.

## `minerals`

| Column | Type | Used by | Meaning |
|---|---|---|---|
| `idx` | int | `(MaterialType) idx` | The `MaterialType` enum ordinal (1=Titan … 19=DHDT, 0=Undefined). All joins use `mineralconfigs.materialtype = minerals.idx`. |
| `name` | varchar | `ToMaterialType()` | Type name ("titan", "crude", …). |
| `definition` | int | `MaterialHelper` → item | The item definition produced by extraction (e.g. titan → def 168). |
| `amount` | int | `DrillerModule` | **Base units per extraction** (titan 1500, crude 1500, stermonit 975, liquizit 450, epriton 450, …). |
| `extractionType` | int | `TerrainsModule` | 0 = Solid (`OreLayer`), 1 = Liquid (`LiquidLayer`). |
| `enablereffectrequired` | bit | module gating | Whether an enabler effect is required to extract. |
| `geoscandocument` | int | scan items | Geo-scan document item for this material. |

## `mineralconfigs`

One row per (zone, material). Consumed by `MineralConfigurationReader` → one
`MineralLayer` per row:

| Column | Type | Used by | Meaning |
|---|---|---|---|
| `zoneId` | int | layer construction | Owning zone. |
| `materialtype` | int | → `minerals.idx` | The material. |
| `maxnodes` | int | `MineralLayer.LoadMineralNodes` | Nodes the zone maintains of this type. |
| `maxtilespernode` | int | generator | Max tiles per node blob; also drives the spacing radius (below). |
| `totalamountpernode` | int | generator | Total material in a fresh node, distributed over the blob's tiles. |
| `minthreshold` | double | generator | Minimum normalized per-tile fill (0–1); tiles below it are dropped. |

### Node generation formulas (`MineralNodeGeneratorBase.cs`)

For a new node of a type with the config above:

```
radius  = floor( sqrt(maxtilespernode / π) ) × 2
spacing = start position must be ≥ 2 × radius from the nearest existing node
blob    = random-walk flood fill (4-tile brush) up to maxtilespernode valid tiles
tile[i] = ( noise[i] / max(noise) ) normalized into [minthreshold, 1]
         × (tile[i] / Σtile) × totalamountpernode
         × uniform(0.9, 1.1)          // jitter, then truncated to uint
```

Keep-out zones around docking bases and teleports (`KeepOutDist`): epriton = 100 tiles
(`MINERAL_DISTANCE_FROM_BASE_MIN`), flux ore = 200; all other types = 0.

### Worked example — Daoden (zone_ASI) crude (id 2)

`maxnodes=8, maxtilespernode=1257, totalamountpernode=125,000,000, minthreshold=0.5`:

```
radius  = floor(√(1257/π)) × 2 = floor(20.0) × 2 = 40   → spacing ≥ 80 tiles
blob    = up to 1257 tiles
tile    = share ∈ [0.5, 1.0] × (125,000,000 / Σnormalized) × jitter
```

A full crude node holds 125 M units; at the base extraction of 1,500 units per drill
cycle that is ~83,000 cycles of material per node, ~666,000 across the zone's 8
nodes.

## Node state — `mineralnodes`

| Column | Meaning |
|---|---|
| `zoneid`, `materialtype` | Owning zone + type |
| `x, y, width, height` | Node bounding box (the blob's area) |
| `data` | Compressed `uint` per tile (row-major within the box) |

Lifecycle (`MineralNode.cs`, `MineralLayer.cs`):

- **Load**: on zone start, all stored nodes load; a node whose total is ≤ **1%** of
  `totalamountpernode` is deleted instead.
- **Top-up**: if fewer than `maxnodes` survived, new nodes generate (formula above).
- **Expiry**: an **untouched** node expires after **7 days** (`TimeTracker`); mining
  resets the clock. A node whose max tile value hits 0 also expires.
- **Persistence**: dirty nodes flush to the DB on a **30-minute** save timer.
- **Flux ore coupling**: decrease/expiry of a `FluxOre` node publishes
  `OreNpcSpawnMessage` → the NPC spawner listens (`OreNPCSpawner`).

## Extraction mechanics (`MineralExtractor.cs`, `DrillerModule.cs`)

The extraction amount is `minerals.amount` scaled by the robot's
`mining_amount_modifier` property (aggregated from `mining_amount_modifier`,
`effect_mining_amount_modifier`, `drone_amplification_mining_amount_modifier`,
`effect_excavator_mining_amount_modifier`).

- **Solid** (`OreLayer`): takes `min(tile value, amount)` from the **single tile** the
  robot stands on.
- **Liquid** (`LiquidLayer`): drains the **whole node**, nearest tiles first
  (priority queue by squared distance), until the amount is satisfied.
- **Gravel** (training zones only, `GravelLayer`): reads the tile value but does not
  decrement it — gravel nodes are inexhaustible.

Yield per type (refined products) is in [Ores](/content/ores/); per-zone node counts
are in [Zone index](/zones/zone-index/).

<!-- Developer notes: Zones/Terrains/Materials/Minerals/MineralConfiguration.cs
     (reader + config), MineralLayer.cs (LoadMineralNodes 1% delete + top-up,
     GenerateNewNode radius/tile/amount wiring, FluxOre events),
     Generators/MineralNodeGeneratorBase.cs (FindStartPosition, NormalizeNoise,
     CreateMineralNode, KeepOutDist), Generators/RandomWalkMineralNodeGenerator.cs
     (flood fill, 50k step cap), MineralNode.cs (uint[] values, 7-day expiry,
     30-min save), MineralNodeRepository.cs (mineralnodes table, compressed
     values), Minerals/MineralExtractor.cs (solid/liquid/gravel extraction),
     Modules/DrillerModule.cs (amount property + modifiers), Materials/MaterialHelper.cs
     (minerals.definition -> item), Zones/DistanceConstants.cs
     (MINERAL_DISTANCE_FROM_BASE_MIN = 100). -->

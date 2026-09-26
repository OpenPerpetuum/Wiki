---
title: "Generation"
description: "The rules for how zones generate ore nodes and plant populations."
weight: 20
---

# Zone resource generation

This page explains how the server keeps a zone's resources stocked. Both systems run
continuously in the background while the zone is loaded; nothing a player does is
required for them to tick.

## The zone's resource configuration

Each zone is configured in the database with two independent lists:

- **Ore configuration** (`mineralconfigs`) — one row per ore type present in the zone,
  with four knobs per type:
  | Column | Meaning |
  |---|---|
  | `maxnodes` | How many nodes of this type the zone maintains |
  | `maxtilespernode` | Maximum number of tiles a single node can cover |
  | `totalamountpernode` | Total material in a fresh node, distributed over its tiles |
  | `minthreshold` | Minimum per-tile fill ratio (0–1); tiles below it after mining are dropped |
- **Plant configuration** (`zones.fertility` + a plant rule set) — the zone's plant
  coverage target (a percentage) and the list of plant species allowed to grow there
  (each species' behaviour is a rule file, documented in
  [Plants](/content/plants/)).

Zones without an ore configuration (arenas, training zones, strongholds, some gamma
zones) have no ore layers at all.

## Ore nodes

### How a node is placed

When a zone loads, each ore type's layer counts its existing nodes (persisted in the
`mineralnodes` table) and generates new ones until `maxnodes` is reached. A new node is
placed by a **random walk**:

1. Pick a random passable tile.
2. It must be far from the nearest existing node of the same type — the exclusion
   radius is derived from `maxtilespernode` (`√(maxtilespernode/π) × 2`, then the start
   must be at least twice that away), so nodes never merge into one giant deposit.
3. From the start tile, a walk randomly floods outward (a 4-tile brush at a time),
   collecting up to `maxtilespernode` valid tiles into one connected blob.
4. The node's `totalamountpernode` material is spread over the blob: each tile's share
   is its (normalized) noise value times the total, with a ±10% random jitter, so no
   two nodes of the same type look alike.

A tile is valid for a node only if it is inside the zone, not an island, not
PBS-terraform-protected, and passable. Two rare types get **keep-out zones** around
docking bases and teleports: **epriton** keeps a minimum distance, and **flux ore**
keeps twice that distance — so flux ore is never found right next to a base.

### How a node is consumed

- Mining removes material tile by tile.
- A node that falls below **1% of its configured total** is deleted, and a fresh node
  is generated (back to step 1) to keep the zone at `maxnodes`.
- Flux ore nodes additionally drive **NPC spawning** — nodes being mined (or removed)
  publish events that the NPC system listens to, which is why flux ore sites get
  attention.

## Plants

### The plant scan

The zone is divided into **32×32 tiles**. A plant handler walks these tiles in order,
processing one per tick, paced so a **full pass over the zone takes about 8 hours**
(overridable per zone). Each 32×32 "cube" is processed by a fixed pipeline:

1. **Validate** — drop invalid plants (wrong ground, dead, out of place).
2. **Grow** — advance each plant through its growth stages (see
   [gathering](/features/gathering/) for `growRate` and fruiting).
3. **Spawn** — add new plants where the cube is under its fertility target.
4. **Damage walls** — plants' blocking height decays over time.
5. **Kill by distance** — enforce same-type spacing (`killDistance`).
6. **Renew material** — a small amount of fruit material regenerates.

### How a new plant is chosen

A tile can only sprout if the zone file gives it **spawn potential** (a per-tile byte
baked into the zone's plant layer — 0 means the tile never sprouts). On a tick, for
each eligible tile:

- **Zone fertility check first**: if the cube's blocking-plant coverage already meets
  `fertility%` of its ground tiles, nothing new spawns there.
- **Per-tile chance**: a random roll must beat the tile's spawn potential.
- **Species selection**:
  - If the tile has **no plant neighbours**, the species is a weighted random draw
    where each allowed species' weight is its `fertility` (player-seeded species are
    excluded from natural spawns).
  - If it **does**, it's a coin flip: 50% the currently *ruling* neighbour species
    (the most common one nearby), 50% a **spreading** draw weighted by
    `(neighbour count × spreading)` for each species — this is what makes clusters
    expand in their own kind.
- **Caps and spacing**: a species with a `maxAmount` cap is skipped once the zone has
  enough of it, and a tile within `killDistance` of another plant of the same type is
    skipped.

### What this means for players

- Plant density is bounded by the zone's fertility — you will never see a zone with
  100% plant coverage if its fertility is 20.
- Clusters are the norm: spreading makes each species expand its own patches, so a
  zone tends toward species territories rather than a salt-and-pepper mix.
- `killDistance` species (e.g. certain high-value plants) are evenly spaced, which
  makes their locations predictable once you find one.
- New nodes appear wherever the random walk lands — there is no fixed "ore spot" on a
  zone; scanning is required (see [gathering](/features/gathering/)).

<!-- Developer notes: TerrainsModule.cs (per-zone layer construction: OreLayer for
     Solid, LiquidLayer for Liquid, GravelLayer for TrainingZone; LoadMineralNodes
     top-up); MineralLayer.cs (LoadMineralNodes: delete <1% nodes, generate to
     maxnodes; FluxOre -> OreNpcSpawnMessage on decrease/expiry);
     Generators/MineralNodeGeneratorBase.cs (FindStartPosition keep-out + spacing,
     NormalizeNoise, CreateMineralNode jitter); RandomWalkMineralNodeGenerator.cs
     (flood-fill brush 4, 50k step cap); MineralNodeRepository.cs (mineralnodes table,
     compressed per-tile values); PlantHandler.cs (32x32 areas, 8h full pass,
     PlantsGrowthTimerOverrideMin, scanner modes); NatureCube.cs (ProcessAll pipeline,
     SpawnPlants fertility/spawn/killDistance logic, GetNewPlantRule 50/50 ruling vs
     spreading); PlantRuleExtensions.cs (fertility-weighted + spreading-weighted
     draws); PlantInfo.cs (per-tile spawn byte comes from the zone's plant layer
     file, never assigned by server code). -->

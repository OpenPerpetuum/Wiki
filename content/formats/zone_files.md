---
title: "Zone files"
description: "The binary terrain layer files: naming, struct layouts, flag bits, the save cycle, and what lives in the DB instead."
weight: 50
---

# Zone files

A zone's terrain is stored as a set of **binary layer files**, one per layer, next to
the zone data. The name is `{layer}.{zoneid:0000}.bin`
(`Zone.CreateTerrainDataFilename`) — e.g. `plants.0042.bin` for zone 42. Layers are
flat arrays of a fixed-size struct, **row-major, width × height** (default 2048×2048 =
4,194,304 tiles).

## The layers

| File | Struct | Bytes/tile | Contents |
|---|---|---|---|
| `blocks.NNNN.bin` | `BlockingInfo` | 2 | `flags` (byte) + `height` (byte) — obstacles, plants, decor, islands |
| `control.NNNN.bin` | `TerrainControlFlags` | 2 (or 1) | control bits per tile (see quirk below) |
| `plants.NNNN.bin` | `PlantInfo` | 7 | the per-tile plant state (below) |
| `altitude.NNNN.bin` | `ushort` | 2 | elevation; compared as `value / 4` in plant checks (client-coordinate scaling) |
| `altitude_blend.NNNN.bin` | `ushort` | 2 | terraformable zones only: per-tile blend mask (below) |
| `altitude_original.NNNN.bin` | `ushort` | 2 | terraformable zones only: pre-terraform elevation on disk — **not read by the server** (below) |

Derived layers (computed at load, **not** files): `SlopeLayer` (from altitude) and
`Passable` (from blocks + slope + DB passable positions, non-terraformable zones only).
On terraformable zones the altitude is wrapped in a `TerraformableAltitude`, which
computes a per-tile **barrier** range at load: `blend/65535` mixes the constants
1850 → original altitude (min) and 30000 → original altitude (max), then clamps the
range to always contain the current value. Terraformed altitude updates are clamped
into that barrier — so `blend = 0` leaves a tile freely terraformable between 1850 and
30000, while `blend = 65535` pins it to its original elevation. (The
`altitude_original.NNNN.bin` files on disk hold pre-terraform elevations for external
tooling; the server takes its original-altitude snapshot in memory from the loaded
`altitude.NNNN.bin` and never reads that filename.)

### `BlockingInfo` (2 bytes)

`flags` is `BlockingFlags` (byte):

| Bit | Name |
|---|---|
| 1 | Obstacle |
| 2 | Plant |
| 4 | Decor |
| 8 | Island |

`NonNaturally` = Decor \| Island \| Obstacle. `height` is the tile's walk-block height
(plant blocking height is written here by the plant growth code).

### `TerrainControlFlags` (control layer)

`ushort` flag set:

| Bit | Name |
|---|---|
| 1 | AntiPlant |
| 2 | TerraformProtected |
| 4 | SyndicateArea |
| 8 | ConcreteA |
| 16 | ConcreteB |
| 64 | Roaming |
| 128 | Highway |
| 256 | PBSHighway |
| 512 | PBSTerraformProtected |
| 1024 | NpcRestricted |

(512/1024 numbering follows `1 << n`; bit 32 is unused.) A tile is plant-allowed when
`!(AntiPlant || Roaming || Highway || PBSHighway || ConcreteA || ConcreteB)`.

**Storage quirk**: the loader accepts both **1 byte/tile** and **2 bytes/tile** control
files — if the file length equals the tile count, each byte is widened to a
`TerrainControlFlags`; otherwise the raw `ushort` array is read
(`LayerFileIO.LoadLayerData`). The live zones all use the **2-byte form**
(`control.0000.bin` = 8,388,608 B = 4,194,304 × 2); the 1-byte form (4 MB) is a
legacy fallback the loader still supports.

### `PlantInfo` (7 bytes) — the plant tile state

| Byte | Field | Meaning |
|---|---|---|
| 0 | `type` | `PlantType` (0 = empty; 1..19 per [Plant fields](/formats/plant-fields/)) |
| 1 | `state` | Growth state index (into the rule's `state_N` table) |
| 2 | `time` | Growth-tick accumulator (compared against the rule's `growRate`) |
| 3 | `spawn` | **Per-tile spawn probability, baked into the zone file; the server never writes it** (no assignment exists anywhere in the code — `PlantInfo.Clear()` deliberately preserves it). 0 = sterile tile: any plant already on it is immediately cleared during validation, and it never sprouts. Otherwise it is a per-attempt gate: during the plant regen pass a random empty tile is accepted only if `FastRandom.NextByte() < spawn`, i.e. per-tile pass chance ≈ `spawn/256`. |
| 4 | `health` | Current HP (ratio of the rule's per-state health) |
| 5 | `material` | Current fruit amount (0–255) |
| 6 | `groundType` | `GroundType` byte (0=Darkrocks … 17=Sand-ish, 20=undefined) — the terrain class the plant rule's `allowedTerrainTypes` checks against |

### Worked example — file sizes for a 2048×2048 zone (verified against live files)

```
blocks    4,194,304 tiles × 2 B = 8,388,608 B (8 MB)
plants    4,194,304 tiles × 7 B = 29,360,128 B (28 MB)
altitude  4,194,304 tiles × 2 B = 8,388,608 B (8 MB)
control   4,194,304 tiles × 2 B = 8,388,608 B (8 MB, 2-byte form; a 1-byte legacy file would be 4 MB)
```

**Live-data check** (`plants.0106.bin`, the terraformable gamma zone): all 4,194,304
tiles carry `spawn = 0x83` (131/256 ≈ 51% per-attempt chance) and `type = 0` except
10 tiles holding live plants — the spawn map is a full-zone baked probability field
independent of where plants currently are.

Not every zone has every file: 66 of the 84 zones ship a `plants.NNNN.bin`. The 18
without one are the superseded legacy gamma variants (`zone_tm_g_*`, `zone_ics_g_*`,
`zone_asi_g_*`, ids 20–43), which are not loaded. The layer directory is
`$GameRoot/layers/`.

## The save cycle (`IntervalLayerSaver.cs`)

- Each mutable layer (blocks, control, plants, altitude) is watched by an
  `IntervalLayerSaver<T>`; any tile/area write sets a dirty flag.
- The saver is registered as a process on a **2-hour** timer
  (`TerrainsModule`); a dirty layer is written when the tick fires.
- Writes go to a `.tmp<ticks>.bin` file, are **MD5-verified** against a re-read, and
  only then replace the real file.
- On server stop, dirty layers are saved once more.
- Mineral nodes are **not** in these files — they persist to the `mineralnodes` DB
  table (30-minute flush, see [Ore fields](/formats/ore-fields/)).

## What is and isn't a file

| Data | Where |
|---|---|
| Terrain layers (blocks/control/plants/altitude) | `.bin` files |
| Ore node state | DB `mineralnodes` |
| Plant species per zone | DB `plantrules` + rule **files** (`$GameRoot/plantrules/*.txt`) |
| Zone configuration (fertility, size, type, ruleset) | DB `zones` |
| Ore configuration | DB `mineralconfigs` |

<!-- Developer notes: Zones/ZoneExtensions.cs (LayerFileIO, CreateTerrainDataFilename,
     1-vs-2 byte control handling, safe zero-fill on size mismatch);
     Zones/Terrains/BlockingInfo.cs + BlockingFlags.cs; TerrainControlInfo.cs +
     TerrainControlFlags.cs; Materials/Plants/PlantInfo.cs; Zones/Terrains/GroundType.cs;
     Zones/Terrains/Layer.cs (RawData, SizeInBytes = Marshal.SizeOf<T>);
     Zones/Terrains/IntervalLayerSaver.cs (dirty flag, MD5 + tmp rename, stop-save);
     Bootstrapper/Modules/TerrainsModule.cs (layer load order: blocks, control, plants,
     altitude, blend; slope + passable derivation; 2h saver + 2h material process). -->

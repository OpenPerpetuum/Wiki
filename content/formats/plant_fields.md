---
title: "Plant fields"
description: "Plant rule file format (GenXY), every rule field with type, default, and consumer, plus the growth state machine."
weight: 20
---

# Plant fields

Plant behaviour is defined per species by a **rule file** (plain text) under
`$GameRoot/plantrules/`. Which files belong to a zone is the DB mapping
`plantrules(idx, plantrule, rulesetid)` → `zones.plantruleset`; loading and merging is
`PlantRuleLoader.cs`, and the field-to-object mapping is `PlantRule.cs`.

## File format

One `key=value` per line; `//`-style comments are stripped. Values use **GenXY token
characters** (`GenxyReader.cs` — the first character selects type and number base):

| Token | Parsed as | Example |
|---|---|---|
| `n` | int, decimal | `n125`, `n-1` |
| `N` | int[], decimal, comma-separated | `N5,5,5,5` |
| `f` | double, decimal | `f0.1` |
| `$` | string | `$prismocitae` |
| `i` | int, **hex** | `iFF` |
| `L` | long, **hex** | `L100` |
| `4` | int[], **hex** | `41,2,3` → [1, 2, 3] |

### ⚠ The "bare array" trap

Files also contain values like `blockingHeight=40,3,3,4` with **no explicit token**.
The parser does not special-case these: the leading `4` is consumed as the
`IntegerArray` (hex) token, and the rest is parsed as hex. So:

```
blockingHeight=40,3,3,4   →  token '4' + hex array "0,3,3,4"  →  [0, 3, 3, 4]
blockingHeight=N0,8,11,14 →  token 'N' + decimal array         →  [0, 8, 11, 14]
```

Both spellings end up as `int[]`, and the leading `0` is what the `4` "swallows". This
is why every bare array in the shipped files starts with `4` — it is a convention
that keeps the first element at the intended value. **When writing new rule files,
use the `N` prefix explicitly**; values ≥ 10 in a bare array would be parsed as hex
and silently change meaning.

### Inheritance

A `source=$other_rule.txt` key loads `other_rule.txt` first and applies the current
file's keys as overrides (`PlantRuleLoader.LoadRuleByName`). Example:
`irontree_t2.txt` is just:

```
source=$irontree_lo.txt
fruitAmount=n125
```

## Field reference

All fields read from the merged settings dictionary (`PlantRule.cs`). Missing keys fall
back to the default shown.

| Key | Type / default | Consumed by | Effect |
|---|---|---|---|
| `index` | int (0) | `PlantRule.Type` | The `PlantType` enum value for this species (1=GrassA … 19=Devrinol). Also the `PlantInfo.type` byte stored per tile. |
| `name` | string | client info payload | Display name. |
| `fertility` | int (0) | `PlantRuleExtensions.GetWinnerPlantTypeBasedOnFertility` | Weight in the weighted draw that picks a species for a tile with **no** plant neighbours. 0 = never spawns. |
| `spreading` | int (0) | `GetSpreadingBasedWinnerPlantType` | Weight multiplier when a tile **has** neighbours: candidate weight = `neighbourCount × spreading`. 0 = never wins the spreading draw. |
| `killDistance` | int (0) | `NatureCube.CheckKillDistance` | Minimum distance from any existing plant of the same type; 0/negative = no limit. |
| `growRate` | int (0) | `GrowPlantOnTile` | Number of cube ticks the `PlantInfo.time` byte must accumulate before the state machine rolls. Higher = slower. |
| `slope` / `minSlope` | int (0) | `ValidateTile` | Allowed slope band of the derived slope layer. Outside the band the plant is cleaned. |
| `allowedAltitudeLow/High` | int (0) | `ValidateTile` | Altitude band, compared against `Altitude / 4` (the `/4` matches client coordinates). Scaled by the zone's `plantaltitudescale`. |
| `allowedWaterLevelLow/High` | int (0) | `ValidateTile` | Band for `altitude/4 − waterLevel/4` — creates coastal vegetation. Also altitude-scaled. |
| `allowedTerrainTypes` | int[] (empty) | `ValidateTile` | Allowed `groundType` bytes (see [Zone files](/formats/zone-files/)). Empty = nowhere. Skipped entirely when `allowedOnNonNatural` is set. |
| `fruitingState` | int (−1) | `GrowPlantOnTile` | First state that produces fruit. −1 ⇒ `NotFruiting` (never harvestable). |
| `fruitDefinition` | int (−1) | harvesting | Item definition of the fruit. |
| `fruitMaterialName` | string | display | Fruit name (e.g. `$prismocitae`). |
| `fruitAmount` | byte (0), clamped 0–255 | `GrowPlantOnTile` | Max fruit per plant. First entry into the fruiting state fills 5–15% of it; each later growth tick adds 15–25%, capped at 100%. |
| `health` | byte[] per state | `GrowPlantOnTile`, combat | Hit points per growth state. On state change the current health **ratio** is carried over into the new state. |
| `blockingHeight` | int[] per state | `GrowPlantOnTile`, spawn counting | Blocking height per state; `>0` sets the `Plant` bit in the block layer (walkable=false) and makes the plant count toward the zone fertility target. |
| `maxAmount` | int (−1) | `SpawnPlants` | Per-zone cap on the number of this species; −1 = unlimited. |
| `playerSeeded` | bool (0) | `GetNewPlantRule`, fertility draw | Excluded from **all** natural spawns; only exists where a player planted it. |
| `damageScale` | double (1.0) | plant combat | Incoming damage multiplier. |
| `onlyOnUnprotectedZone` | bool (0) | spawn validation | Only grows where the zone is unprotected. |
| `allowedOnNonNatural` | bool (0) | `ValidateTile` | Skips the control-bit, terrain-type, and slope checks (grows on "artificial" tiles). |
| `placesConcrete` | bool (0) | `GrowPlantOnTile` (death) | On death, clears the ConcreteA/B control bits under the tile. |
| `state_N` | int[] | `GetNextState` | Next-state choices for state `N` — each element is picked **uniformly at random**. |
| `action_N` | int[] | `GetNextState` | Next `PlantType` choices for state `N` (uniform). An action value of `0` (NotDefined) means **death**. |

Consistency is enforced at load (`CheckConsistency`): `len(health) ==
len(blockingHeight) == number of states`.

## The growth state machine

Each 32×32 cube tick, for every plant tile (`GrowPlantOnTile`):

1. If `time < growRate`: `time++`, done.
2. Else: `time = 0`, roll `state_N` and `action_N` for the current state:
   - `action == 0` → the plant dies (kill signal `type:0, state:1` for the client);
     if `placesConcrete`, clear concrete under the tile.
   - else → `type = action`, `state = next`, `health = healthRatio ×
     Health[newState]` (damage ratio preserved).
   - Fruit: if `newState == fruitingState`, `material = fruitAmount ×
     uniform(0.05, 0.15)`; if `newState > fruitingState`, `material += fruitAmount ×
     uniform(0.15, 0.25)` (capped at `fruitAmount`); otherwise `material = 0`.
   - Block height = `blockingHeight[newState]`; the block layer's `Plant` bit follows
     `height > 0`.

### Worked example — `grass_a.txt`

```
index=n1            → GrassA
fertility=n1        → 1/… weight in the no-neighbour draw
spreading=n2        → cluster weight = 2 × neighbours
growRate=n1         → rolls a state every cube tick (fast)
maxAmount=n190      → at most 190 per cube
health=N5,5,5,5     → 5 HP in every state
blockingHeight=40,0,0,0 → parsed as [0,0,0,0]: grass NEVER blocks
fruitingState=n-1   → NotFruiting: grass is never harvestable
state_0=40,0,1      → [0,0,1]: 2/3 stay in state 0, 1/3 advance
state_1=41,1,2      → [1,1,2]
state_2=42,2,3      → [2,2,3]
state_3=43          → [3]
action_3=N1,1,1,0   → in the final state: 3/4 stay, 1/4 die
```

So a grass tile: advances roughly one state every 3 ticks on average, and in the last
state has a 25% chance per tick of dying and re-seeding — an ever-turnover cover
species with no fruit.

### Worked example — `wall.txt` (12 states, `N`-prefixed)

`blockingHeight=N0,0,2,3,4,7,9,10,10,13,13,14`, `health=N20,60,80,…,255`,
`state_7=N7,7,8,8,6` (can regress a state), `playerSeeded=n1`,
`onlyOnUnprotectedZone=n1`, `maxAmount=n15`: a player-planted fortification that only
exists in unprotected zones, capped at 15 per cube, that grows (and occasionally
shrinks) through 12 states up to height 14 and 255 HP.

## Where the per-tile state lives

The runtime state per tile is the 7-byte `PlantInfo` struct in the zone's plant layer
file (`type, state, time, spawn, health, material, groundType`) — see
[Zone files](/formats/zone-files/). The `spawn` byte is a per-tile spawn probability
baked into the zone file — `0` marks a sterile tile (plants on it are cleared during
validation, it never sprouts); any other value means a plant-regen attempt on that
tile passes a random gate with chance ≈ `spawn/256`. The server never writes the
byte. Live data: every tile of `plants.0106.bin` carries `0x83` (≈ 51%).

## Client payload

`PlantRuleExtensions.GetPlantInfoForClient` sends, per species: `index`, `mineral`
(= fruitDefinition), `name`, `blocks` (blockingHeight), `health`.

<!-- Developer notes: Materials/Plants/PlantRule.cs (field getters, GetNextState,
     CheckConsistency); PlantRuleLoader.cs (file load, source: override,
     plantAltitudeScale applied to allowedAltitudeLow/High + allowedWaterLevelLow);
     PlantRuleExtensions.cs (fertility + spreading weighted draws);
     NatureCube.cs (ValidateTile checks order: block flags -> spawn byte -> rule
     existence -> control bits -> altitude/4 -> water level -> terrain type ->
     slope; GrowPlantOnTile state machine + fruit math); Common/SettingsLoader.cs
     (line join with '#' then GenxyConverter.Deserialize); GenXY/GenxyReader.cs +
     GenxyToken.cs (token characters; '4' = hex int array, 'N' = decimal int array,
     'n' = int decimal). Rule files live at $GameRoot/plantrules (plantrules table
     maps rulesetid -> file names). -->

---
title: "Outposts & SAP"
description: "Outpost ownership, stability, and the four SAP activity types — how corporations take and hold open-PvP outposts."
weight: 16
---

# Outposts & SAP

On the open-PvP islands, **outposts can be owned by player corporations**.
Control is measured by a **stability** meter (0–150) shown above the outpost
in map view. The **SAP** (Service Access Point) system is how that meter
moves — and how outposts change hands.

## The rules

- When the **owning** corporation completes a SAP, stability goes **up** by
  the SAP's point value.
- When a **corporation allied to the owner** (mutual standing at +10 or higher)
  completes one, stability does **not move**.
- When **anyone else** completes one, stability goes **down** by the same
  amount.
- At **zero**, the outpost loses ownership and becomes neutral. Completing
  any SAP on a neutral outpost **captures** it (the completing corporation
  becomes the owner, starting at stability 1).
- **Decay**: if no SAP is completed for **5 days**, the outpost starts losing
  **5 stability per day** until one is (or until it is lost).

## The four SAP types

Every 8 hours an outpost puts up a new SAP — one of four types. Each type has
its own point value and its own completion method:

| Type | How it's done | Points | Reward flavor |
|---|---|---|---|
| **Passive** | Stand inside the SAP beam for **8 minutes**. Progress is individual (each player's own continuous time in the beam); the single highest personal progress takes it over. | +10 | Faction plasma (scales with stability), research kits, EP boosters. |
| **Active** | Target the SAP and keep a **SAP hacking module** running — about 120 module cycles (5 s each), i.e. ~10 minutes. Progress persists if you stop or leave; individual. | +15 | Plasma, kernels, research kits, and **reactor cores** (robot production). |
| **Specimen** | Bring **specimen items** to the SAP (within 7 m) and submit the 5–6 it asks for. Each submit has a 90-second cooldown; progress is individual. | +15 | Plasma, kernels, research kits. |
| **Destruction** | Shoot it down: **30,000 armor** and **150 resistance to all four damage types** (60% reduction), **6 m signature** — small weapons hit it best. Damage progress is **corporate, not individual**, so whole fleets can chip away at once; the corporation with the most damage when it dies applies the result. | ±15 | Faction **PvP ammo**, plasma, kernels. |

All four SAPs **broadcast the current top scorers** (top 10) to everyone
nearby, and they **expire after 2 hours** if not completed.

### Denying the enemy

Because destruction progress is corporate and the sign of the stability change
depends on who wins, a common tactic is to **deal enough damage to top the
leaderboard but not enough to finish the SAP**: if the owner's own crew then
finishes it, *your* corporation is still the winner, and the stability change
lands against the owner.

## Watching and predicting

- The **intrusion scanner charge** in a geoscanner, activated near the
  outpost, reports the **time until the next SAP** for outposts in range
  (on open-PvP islands the same charge reports NPC reinforcement status
  instead).
- SAPs announce themselves to nearby players a short while before they open.
- The SAP's top-scorer broadcast means you can **watch a contested SAP in
  real time** and decide whether to contest it.

## Other ways stability moves

- **SAP relics** — relic objects that spawn 90–350 m from the outpost (4-hour
  respawn, up to 12 hours old). Popping one applies **±1 stability instantly**
  (for your corporation), flags you PvP, and pays EP plus a plasma container.
- **Guardians and invaders** — the outpost's NPC bosses:
  - **Guard** NPCs hold close to the outpost (~35 m). Killing one **always
    reduces** stability by 3, whoever kills it; they respawn in ~6 hours.
  - **Invader** NPCs patrol out to ~300 m. Killing one **raises** stability by
    2 for the killing corporation; ~6 hour respawn.

## Why outposts matter

Owning an outpost gives the corporation its bonuses (production, defense
facilities, effects) and is the backbone of open-PvP territorial play — see
[PBS](/features/pbs/) for the structures that make an outpost an actual base
of operations.

<!--
Written from scratch against the server backend, 2026-09-27:
Zones/Intrusion/Outpost.cs (stability 0-150, capture at 1, ally threshold
+10, EP to participants), OutpostDecay.cs (5-day grace, -5/day), SAP.cs +
PassiveHackingSAP.cs / ActiveHackingSAP.cs / SpecimenProcessingSAP.cs /
DestructionSAP.cs (8-min passive, 120x5s active, 5-6 specimen items with 90s
cooldown, corporate destruction score, 2h expiry, top-10 broadcast),
entitydefaults options (#increase=n10/n15), siegeitems (def_specimen_sap_item
2-3 x 5-6), def_sap_destruction stats (30k armor, 150 res all types, 6m
signature), def_siege_hack_module (5s cycle), intrusionsitestabilitythreshold
+ intrusionsaps, intrusionloot (plasma/kernels/research kits/cores/PvP ammo
by stability tier), npcbossinfo + npcflock (guard -3 @ ~35m, invader +2 @
~300m, 6h respawn), OutpostRelicManager.cs + SAPRelic.cs (90-350m spawn,
4h respawn, 12h lifespan, +1 stability, PvP flag), Scanner.Intrusion.cs
(next-SAP-time scan), EntitiesModule.cs (8h SAP interval).
An earlier version of this page was adapted from the Open Perpetuum
community wiki (perpetuum.miraheze.org) and has been fully rewritten from
backend sources; no text was reused.
Note: the ingested text had several numbers this server contradicts (3-day
decay vs 5-day, 10-min passive vs 8-min, "no resistances" vs 150 all types,
3 km invader range vs 300 m).
-->

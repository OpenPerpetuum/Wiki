---
title: "Missions"
description: "Field missions: starting from zones or terminals, agents, participants, and delivery."
weight: 110
---

# Missions

**Missions** are structured objectives you take on in a zone — deliver items, hit targets,
use structures. They are how you progress, earn rewards, and drive a lot of zone content
(alarms, kiosks, and some PBS/transport hooks are mission-related).

> UI locations follow the [client UI overview](/features/ui/). Mechanics below are confirmed against
> the server.

## Starting a mission

You start a mission by choosing a **category** and a **level** (levels are clamped to a
valid range), at a **location**:

- If you're **docked**, the mission starts from your current base's location.
- If you're **in a zone**, you specify the location explicitly.
- Missions can also start **from a zone** directly (a zone-tied start) or **from a field
  terminal**.

Not every category/level is available everywhere — availability is content-driven. The
complete list of missions (type, category, level, duration, reward fee, and reward items)
is generated in [Missions](/content/missions/).

## What missions are for

Missions serve two purposes at once: they pay NIC, and they build your **relation**
with the issuing (contractor) corporation. A better relation is a key factor in
getting a **higher facility ratio** at that corporation's terminals — production and
other facility runs become cheaper and faster for you. Relations can also *drop*: some
missions conflict with another corporation's interests, and the brief notes when that
will cost you standing.

- **Training missions** — easy, highlighted for rookies; do all of them, the rewards
  are a nice early injection.
- **Regular missions** come in three flavors:
  - *Combat* — recon (scan enemy robots), bounty hunting (kill targets in a marked
    circle), destroy & recover (kill + retrieve + deliver), recon & recovery, special
    bounties (hard-to-reach targets).
  - *Industry* — geology (scan for a mineral with the right charge), harvesting, mineral
    exploitation, skilled exploitation (guarded dig sites — bring armed help).
  - *Logistics* — transportation (deliver a package between terminals) and retrieval
    (a big package through dangerous ground — escort it).
- There is no such thing as a **safe mission**: if you see enemies on the route and
  lack the gear to defend yourself, ask for help before you lose everything on the way.
- **Objectives only count in the marked area** — the mission map shows it as a red
  circle on the radar; killing or scanning elsewhere does nothing. Miss the deadline
  and the mission fails instantly.
- **Private transport contracts** — a Syndicate-run system (second tab of the mission
  window) where players post containers for other players to haul between terminals,
  with a set reward and a **collateral** the hauler stakes (refunded on success, lost if
  the container is destroyed; aborting mid-way refunds only half). Keep containers
  small enough that someone can actually carry them.

## Mission data & options

- **Mission data** — details of a mission.
- **Mission options** — the choices/variants available for a mission.
- **Mission supply** — the supplies a mission involves.
- **Start items** — the items a mission begins with.

## Running a mission

- **List running missions** — what you're currently doing.
- **Abort** — cancel a mission you're running.
- **Add a participant** — bring another character into the mission (multiplayer missions).
- **Mission log** — the event log for a mission.

## Delivery

- **Deliver** — complete a delivery target. This can be done **docked** (at a base) or
  **in a zone** (at a specific location/eid), depending on the mission.

## Agents

- **List agents** — the mission agents available (NPCs/factions that issue missions).

## Field terminals

- **Field terminal info** — inspect a field terminal in a zone (a structure that can start
  or service missions).

## Practical notes

- **Missions are the connective tissue** — alarms, kiosks, and some structures are
  mission objects (see [gathering](/features/gathering/) and [combat](/features/combat/)).
- **Multiplayer** — you can add participants to cooperate on a mission.
- **Delivery context matters** — some deliveries need you docked, some need you at a
  zone location.
- **Level is clamped** — requesting an out-of-range level is corrected, not an error.
- **Abort is your out** — if a mission goes bad, abort it.

## Mission structures

Missions are anchored in zones by **structures**. The server implements these types:

| Structure | What it does |
|---|---|
| **Simple switch** | instant interaction — approach and the mission target completes |
| **Alarm switch** | a timed task — stay within range for a period (from the mission's options), checked every 2 s |
| **Item supply** | timed like an alarm switch, then **spawns items into your robot's container** |
| **Kiosk** | an **item submission** point — hand the mission its required items |

<!-- TODO: the full mission list, categories, level ranges and rewards are generated at
     /content/missions/. Verified: mission structure types
     (MissionEngine/MissionStructures: SimpleSwitch, AlarmSwitch, ItemSupply, Kiosk);
     verify the mission UI.
     Developer notes: Missions/MissionStart.cs (category, level clamp, docked vs zone
     location), Missions/MissionDeliver.cs (docked vs zone delivery), MissionAbort.cs,
     MissionData.cs / MissionGetOptions.cs / MissionGetSupply.cs / MissionStartItems.cs,
     MissionListRunning.cs / MissionListAgents.cs / MissionLogList.cs,
     MissionPlayerAddsParticipant.cs, FieldTerminalInfo.cs, MissionStartFromZone.cs;
     Services/MissionEngine/* (MissionProcessor, mission structures incl. AlarmSwitch,
     Kiosk). -->

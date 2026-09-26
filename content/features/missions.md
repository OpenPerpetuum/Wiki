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

---
title: "Movement & zones"
description: "Zones, docking, the entry queue, teleports, spark teleports, and gates."
weight: 20
---

# Movement & zones

You are always in one of two states: **docked** at a base, or **undocked** in a zone
flying your active robot. Everything on this page is about moving between those states
and between zones.

> UI locations follow the [client UI overview](/features/ui/): the **Deploy/Dock button** is the
> center top-bar toggle. Mechanics below are confirmed against the server.

## Docked vs. undocked

- **Docked** — you are at a base. Your robots and items are in containers. You cannot
  fly, use teleports, or scan, but you can craft, trade, research, and use base facilities.
- **Undocked** — you are in a zone as your active robot. You can fly, scan, fight, and
  interact with zone objects, but most base-only activities are locked.

## Undocking

Press **Deploy** (center of the top bar). The server then checks all of the following:

1. You have an **active robot selected**.
2. That robot's class has the required **enabler extension** researched on your
   character (see [research](/features/research/)).
3. The robot's **energy system is intact**.
4. The robot's **container has free capacity**.
5. You are not inside an **undock cooldown** (a short timer after certain events).

If any check fails, the undock is refused.

## Docking back

- **Dock** at a base when you are within its docking range.
- **Force dock** is the emergency action: it pulls you out of the zone and docks you at
  the main base (TMA) immediately, even in combat. Use it when you're losing a fight.
- **SOS** — a zone help action that docks you at the current base and flags the incident.

## The zone entry queue

Some zones are busy and use an **entry queue** when you dock out / enter:

- Check the queue state (`zoneGetQueueInfo`).
- Set your preferred queue length (`zoneSetQueueLength`).
- Cancel waiting if you changed your mind (`zoneCancelEnterQueue`).

## Teleports

**Teleports** are structures inside a zone. To use one:

- You must be **within the teleport's range**.
- The **channel** (destination) must be **active** and valid.
- You must **not** be under *teleport sickness* — a short cooldown after any teleport,
  during which you cannot teleport again.
- Certain effects (e.g. *Nox* negation) or being **in PvP combat** can block teleports.

Teleport channels come in three kinds:

| Type | Effect |
|---|---|
| **Within zone** | Moves you to a random point in the same zone |
| **Another zone** | Moves you to a random point in a different zone |
| **Training exit** | Exits a training zone (with a reward level option) |

**Mobile teleports** are *owned* teleport items (e.g. mobile stronghold/world teleports).
Extra rules apply:

- Only the **owner or a member of the owner's gang** may use one.
- They **cannot be used while in PvP**.
- Each use applies a **cooldown** to that teleport.

You can list teleports and their channels, and query world-wide channels.

## Spark teleports

**Spark teleports** are a fast-travel system between bases:

- You **set** a spark destination at one base, then **use** it (while docked) to jump to
  another base instantly.
- You can **list, set, use, and delete** your spark teleports.
- The target base must **allow docking** for you.
- On arrival the server selects a suitable robot for you (one whose enabler extensions
  you have researched).
- The **number of spark teleports you may hold** is limited by an extension
  (see [research](/features/research/)); exceeding it is refused.

## Gates

**Gates** are zone structures that can be used from anywhere (not range-limited like
teleports). A base owner can **rename** its gates.

## Base information

You can inspect any base:

- **Info** — what it is and where.
- **Ownership** — who owns it.
- **Facilities** — what it offers (production, market, shop, …).
- **My items** — your items stored at that base.
- **Docking rights** — a base owner can set who may dock there.

## What can go wrong

| Symptom | Likely cause |
|---|---|
| Undock refused | No active robot, missing enabler extension, damaged energy system, full robot container, or undock cooldown |
| Teleport refused | Out of range, channel inactive, teleport sickness, Nox effect, or in PvP |
| Mobile teleport refused | Not owner/gang member, or in PvP |
| Spark teleport refused | Target base forbids docking, no valid robot to select, or spark-teleport limit reached |
| Stuck at a zone entry | Zone queue — check the queue state or cancel |

<!-- TODO(Phase 1): verify client UI flow (dock/undock buttons, teleport screen).
     Developer notes: Dock.cs / Undock.cs / ForceDock.cs (TMA = baseEid 561) /
     ZoneSOS.cs; Zone/TeleportUse.cs (TeleportPlayerValidator, three
     TeleportDescriptionType strategies, mobile-teleport gang/PVP rules);
     Sparks/SparkTeleportUse.cs (docked-only, extension-count limit);
     Zone/ZoneCancelEnterQueue.cs + ZoneEnterQueueService; GateSetName.cs. -->

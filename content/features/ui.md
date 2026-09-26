---
title: "Client UI overview"
description: "The client layout: top bar, dock button, action categories, and the undocked status panel. Modeled on an EVE Online-style interface."
weight: 5
---

# Client UI overview

The client is an **EVE Online-style** interface: a full-screen space/zone view with
floating, **resizable and movable windows** that can be **closed and reopened** at will.
Wiring is per-system — see the pages linked below.

> This layout is a working model provided by the project, used to describe where actions
> live. Exact screen names may differ slightly; the structure is the reference.

## The top bar

A persistent bar across the top of the screen.

| Area | Contents |
|---|---|
| **Left** | Main buttons: **Mail**, **Character**, **Corporation**, other **Options**, and **Disconnect** |
| **Center** | The **Deploy / Dock** button — your primary docked⇄undocked toggle (see [movement](/features/movement/)) |
| **Right** | Action categories as buttons: **Refinery**, **Recycling**, **Repair**, **Insurance**, **Market**, **Assignments** (transports), **Corp Storage**, **Equip**, … |

The right-side categories open their system windows:

| Button | Opens / does | Wiki page |
|---|---|---|
| Refinery | Process raw resources into components | [production](/features/production/) |
| Recycling | Break items down into base materials | [production](/features/production/) |
| Repair | Repair damaged robots/modules | [production](/features/production/) |
| Insurance | Production/robot insurance policies | [production](/features/production/) |
| Market | Market orders and direct buys | [market](/features/market/) |
| Assignments | Transport assignments (shipments) | [transport](/features/transport/) |
| Corp Storage | Corporation hangar/storage | [groups](/features/groups/) |
| Equip | Fit modules and ammo on your active robot | [robots](/features/robots/) |

## Docked state

While **docked**, the right-side action categories are the main workspace: you process,
trade, fit, and manage storage without leaving the base. The Deploy button undocks you
into the zone (see [movement](/features/movement/) for the undock preconditions).

## Undocked state

While **undocked** (flying your active robot in a zone):

- The **top-right** area gains an **expandable actions button**: click it and a list of
  extra zone actions expands downward (scanning, harvest/interact actions, zone tools —
  the actions documented under [gathering](/features/gathering/) and [combat](/features/combat/)).
- The **main status panel** shows your robot's condition: **shield, armor/hull, energy**,
  and related combat status — the same data your [combat](/features/combat/) page refers to
  (e.g. the "energy system intact" undock check).
- Base-only categories (refinery, market, …) are unavailable until you dock again.

## Common windows

Most systems open in a standard floating window: title bar, close button, resizable
edges, tab or list content. Repeated systems (inventory/containers, market, production
lines) can have multiple windows open at once.

- **Inventory / containers** — list a container, move/stack/pack items, rename, trash —
  see [items](/features/items/).
- **Character window** (top-left button) — profile, extensions (research), settings,
  credits — see [getting-started](/features/getting-started/) and [research](/features/research/).
- **Corporation window** (top-left button) — corporation info, hangar, documents, votes,
  bulletin — see [groups](/features/groups/).
- **Mail window** (top-left button) — personal and mass mail — see [social](/features/social/).

## Convention used in this wiki

- **"Top bar → X"** = the button named X on the top bar.
- **"Undocked actions"** = the expandable list at top-right while in a zone.
- **Deploy/Dock button** = the center top-bar toggle.

<!-- Note: this page is the agreed UI model (project-provided). If the real client
     differs in button names or placement, update this page first — every other
     features/ page references the conventions defined here. -->

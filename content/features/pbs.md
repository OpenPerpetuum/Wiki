---
title: "Power base stations"
description: "PBS structures: deployment, connections, territories, effects, and feeding."
weight: 100
---

# Power base stations (PBS)

A **PBS** (Power Base Station) is a placeable structure in a zone. PBS objects can be
**connected** into a **network**, which defines **territories** and enables **effects**.
This is a corporation/structure activity — most actions require the right corporation
role.

> UI locations follow the [client UI overview](/features/ui/). The in-client name for "PBS" should
> be confirmed. Mechanics below are confirmed against the server.

## What a PBS does

- It is a **node** in a PBS **network**.
- Connected nodes form a shared **territory** the owning corporation controls.
- A network can project **effects** (buffs/controls) over its territory.
- Some PBS structures can be **fed** items (fuel) to operate.

## Structure families

Every PBS structure comes in **small / medium / large** tiers. You carry the
**capsule**, research the item (CPRG), and **deploy** the object — deployment is
privilege-gated (see below). The families:

| Family | Structures | Role |
|---|---|---|
| Power | reactor, energy well, core battery, core transmitter | generate, store, and distribute the **core energy** everything runs on |
| Docking | docking base, expiring docking base, control tower | where players dock; the control tower extends the base's reach |
| Production | mill, refinery, reprocessor, repair, prototyper, research lab, calibration (CPRG) forge, research-kit forge, production upgrade | the [production](/features/production/) facilities, in the zone instead of at a home base |
| Defense | turrets (laser / rail / missile / electronic warfare), armor repairer | automated defense of the territory |
| Effects | aura emitter, effect supplier, maskertower, mining tower | project the territory's effect bonuses; the mining tower extracts ore automatically |
| Transport | highway node | links to the zone's **highway** (fast travel lines) |
| Construction | construction module | the builder that places the other structures |

### Aura emitter effects

The aura emitter is the main territory buff. Each tier projects one of three
**level-scaled base bonuses**:

- **small** — sensors lvl1 / engineering lvl1 / industry lvl1
- **medium** — the same three at lvl2
- **large** — the same three at lvl3

Sensors extend scanning/recon range, engineering speeds construction, industry speeds
production.

### The expiring docking base

A special PBS base with a fixed **7-day lifetime** (168 h in the content). It **cannot
be deconstructed** — it either **expires or is destroyed**. The remaining lifetime is
announced by an in-game announcer (base channel topic, mail, and MOTD); when it runs
out the base is killed and its contents are lost. This is the "rent" model for PBS
docking: you don't pay rent, you get a timebox.

## Deployment

- **Check deployment** — before placing, verify a PBS can be deployed at a position
  (slope, blocking, and privilege checks). This requires a corporation role
  (**CEO / deputy / edit-PBS**).
- Deployment uses the structure's **construction radius** and **blocking radius**.

## Network & connections

- **Get network** — view the PBS network you're part of.
- **Node info** — details of a PBS node.
- **Rename a node**.
- **Make a connection** — link two PBS nodes. (Connecting into a network that already has
  a docking base triggers a specific ownership-transfer case.)
- **Break a connection** — unlink two nodes.
- **Set connection weight** — adjust a connection's strength/priority.

## Control & state

- **Set online** — bring a node online/offline.
- **Set an effect** — configure the effect a node projects.
- **Set base deconstruction** — flag a base for deconstruction.

## Territory

- **Get territories** — list the territories your network controls.
- **Set territory visibility** — control whether the territory is visible to others.
- **Set standing limit** — restrict access by standing.
- **Set reinforce offset** — adjust reinforcement behaviour.

## Feeding

- **Feedable info** — what a PBS needs and its current feed state.
- **Feed items** — submit items to power/operate the PBS.

The feedable structures are the **reactor** and the **mining tower**. Feeding converts
items with an energy value (the `corecalories` per piece) into the node's core energy
buffer, up to its maximum — any player in range can feed a node. What you can burn:

| Fuel | Core per piece |
|---|---|
| Reactor booster class C | 8,000,000 |
| Reactor booster class B | 4,000,000 |
| Reactor booster class A | 2,000,000 |
| SAP specimen (intrusion) | 600,000 (flux variant: 28,000) |
| Gamma energy block | 2,000 |
| Core booster ammo | 1,000 |
| Raw ore (espitium) | 400 |
| Raw ore (vitricyl / prilumium / chollonin) | 300 |
| Raw ore (axicol) | 40 |

Boosters are the efficient fuel; raw ore is the cheap fallback.

## Reimburse

- **Get / set reimburse info** — configure reimbursement for PBS-related costs.

## Logs

- **PBS logs** — review PBS activity (connections, deployments, etc.).

## Practical notes

- **This is an officer activity** — deployment and most controls need a corporation role.
- **Connections define territory** — the network you build is what you control.
- **Check deployment first** — it tells you why a placement would fail (slope, blocking,
  privileges) before you commit.
- **Feeding keeps it running** — reactors and mining towers burn fuel items (boosters
  or raw ore) to hold their core buffer.
- **The expiring base is your timebox** — 7 days, no deconstruction; plan for the kill.
- **Territory visibility & standing** let you open or close your zone to others.

<!-- TODO: only the in-client name for PBS and the UI remain (structure list, network
     editor, feeding screen).
     Confirmed: 416 def_pbs_* definitions, S/M/L tiers with capsule/cprg/pr/object
     variants; aura emitter #effect=N93..101 = pbs sensors/engineering/industry lvl1-3
     (EffectType enum); feedable = reactor + mining tower (IPBSFeedable), fuel =
     definitionconfig.corecalories (boosters A/B/C 2/4/8M, SAP 600k/28k, gamma block
     2k, ore espitium 400 / vitricyl+prilumium+chollonin 300 / axicol 40);
     ExpiringPBSDockingBase: lifetime 168 h from definitionconfig.lifetime, no
     deconstruction, announcer channel topic + mail, kill on expiry. -->
     Developer notes: Zone/PBS/PBSCheckDeployment.cs (construction/blocking radius,
     CEO/deputy/editPBS role), PBSMakeConnection.cs / PBSBreakConnection.cs,
     PBSSetConnectionWeight.cs, PBSSetOnline.cs, PBSSetEffect.cs, PBSSetBaseDeconstruct.cs,
     PBSGetNetwork.cs, PBSNodeInfo.cs, PBSRenameNode.cs, PBSGetTerritories.cs,
     PBSSetTerritoryVisibility.cs, PBSSetStandingLimit.cs, PBSSetReinforceOffset.cs,
     PBSFeedableInfo.cs / PBSFeedItemsHander.cs, PBSGetReimburseInfo.cs /
     PBSSetReimburseInfo.cs, PbsGetLog.cs; Zones/PBS/* (IPBSObject, ConnectionHandler). -->

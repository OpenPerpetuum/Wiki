---
title: "Protection levels"
description: "Alpha, beta and gamma — the three protection levels every zone has, what each one allows, and how to tell them apart in the client."
weight: 20
---

# Protection levels

Every zone has a protection level: **alpha**, **beta** or **gamma**. The level
is set per zone in the server database (the zone's *protected* and
*terraformable* flags) and decides what you may do there — above all whether
other players can attack you, and whether the terrain can be reshaped.

| Level | Flag on the zone | PvP | Blobs | Terraforming | Where you'll find it |
|---|---|---|---|---|---|
| **Alpha** | protected | not possible | not formed | not possible | [Alpha islands](/zones/map/#alpha), [training](/zones/zone-training/), strongholds, [PvP arena](/zones/zone-pvp-arena/) |
| **Beta** | open, standard terrain | open | open | not possible | The PvP twins of the [Alpha islands](/zones/map/#alpha) and the gamma belt |
| **Gamma** | open, terraformable | open | open | possible | The gate islands of the three galaxies and the frontier belt |

## Alpha — protected

In an alpha zone players simply **cannot damage each other**: attacking
firearms are disabled against other players, and the damage effects that
stack on you in combat (blobs) are not formed either. It is the safe
economy: mine and harvest without looking over your shoulder. The training
zone, the three starter islands and their PvE companions, the strongholds
and the PvP arena all run as alpha — the arena keeps its name for what
happens *inside* the scripted event, not for free-for-all.

In the client, an alpha zone shows up with the **blue diodes** on the bot
status.

## Beta — open PvP, standard terrain

A beta zone is a fully open PvP island with standard terrain: you can be
attacked at any time, blobs apply, and the island's ground is what the server
generated it to be — no player reshaping. The PvP twins of the starter
islands (the "real" islands of the three galaxies and the tc transit zones of
the frontier belt) are beta.

In the client, a beta zone shows up with the **orange diodes**.

## Gamma — open PvP, terraformable

A gamma zone is a beta zone plus one more thing: **the terrain is
terraformable**. Players with terraforming tools can reshape the ground —
dig, level and build the island's surface — which changes where nodes,
plants and bases can sit. The eight gate islands of each galaxy and the
islands of the frontier belt are gamma.

In the client, a gamma zone shows up with the **yellow stripes** above and
below the zone name.

## What this changes for a player

- **Risk budget** — alpha zones cost nothing in safety; beta and gamma cost
  the full PvP risk. Plan fuel, escorts and repair capacity accordingly.
- **Yield vs. risk** — the open islands carry the better material (epriton
  and the high-tier harvestables appear outside the protected islands), which
  is why the risk is the price of the yield.
- **Terrain stability** — on gamma islands the ground can change under a
  base: a reshaped surface can move the resources a base feeds on, so
  long-term territory work needs to account for terraforming by others.
- **Check before you commit** — the diodes on the bot status tell you the
  level of the zone you are in; a misread diode is the classic way to arrive
  in a beta zone with an unarmored hauler.

Every zone's level is listed in the [zone index](/zones/zone-index/)
(*Protection* column) and in the tooltips on the
[world map](/zones/map/).

<!-- Developer notes: the three levels are derived from two zone flags
     (src/Perpetuum/Zones/ZoneConfiguration.cs: IsAlpha => Protected,
     IsBeta => !Protected && !Terraformable, IsGamma => Terraformable).
     Protected zones skip blob formation on unit moves (Zone.cs,
     ZoneExtensions.Unit.cs). Diode colors (blue/orange) and the yellow
     beta/gamma stripes are client-side zone markers. -->

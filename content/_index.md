---
title: "Open Perpetuum Wiki"
description: "Your guide to the Open Perpetuum server: what you can do, how to start, and where to dig deeper."
---

# Open Perpetuum

A persistent-universe MMO: you pilot **robots** across a galaxy of zones, gather
resources, build and defend **power base stations (PBS)**, contest **intrusion sites**,
trade, and research your way up from a starter bot to a fully fitted war machine.

## Watch first

Guides from the Open Perpetuum Project. Click a card to play it here.

<div class="video-grid">
  <div class="video-card" role="button" tabindex="0" aria-label="Play video: Perpetuum Online Tutorial Walkthrough. + Extension Advice and lots more." data-id="mHd3nOHvEuM" data-title="Perpetuum Online Tutorial Walkthrough. + Extension Advice and lots more.">
    <img src="https://i.ytimg.com/vi/mHd3nOHvEuM/hqdefault.jpg" alt="Perpetuum Online Tutorial Walkthrough. + Extension Advice and lots more."
         width="480" height="270" referrerpolicy="origin">
    <span class="video-play" aria-hidden="true">&#9654;</span>
    <div class="video-title">Perpetuum Online Tutorial Walkthrough. + Extension Advice and lots more.</div>
  </div>
  <div class="video-card" role="button" tabindex="0" aria-label="Play video: Syndicate Careers Help - Artifacting" data-id="lQgrO6RNBSg" data-title="Syndicate Careers Help - Artifacting">
    <img src="https://i.ytimg.com/vi/lQgrO6RNBSg/hqdefault.jpg" alt="Syndicate Careers Help - Artifacting"
         width="480" height="270" referrerpolicy="origin">
    <span class="video-play" aria-hidden="true">&#9654;</span>
    <div class="video-title">Syndicate Careers Help — Artifacting</div>
  </div>
  <div class="video-card" role="button" tabindex="0" aria-label="Play video: Open Perpetuum Tutorial - How to run multiple clients" data-id="sRBouSF8Gu4" data-title="Open Perpetuum Tutorial - How to run multiple clients">
    <img src="https://i.ytimg.com/vi/sRBouSF8Gu4/hqdefault.jpg" alt="Open Perpetuum Tutorial - How to run multiple clients"
         width="480" height="270" referrerpolicy="origin">
    <span class="video-play" aria-hidden="true">&#9654;</span>
    <div class="video-title">Open Perpetuum Tutorial — How to run multiple clients</div>
  </div>
</div>

<script>
    // Click-to-play: swap the thumbnail card for the YouTube embed on demand
    // (keeps the home page light until a video is actually wanted).
    (function () {
        var cards = document.querySelectorAll(".video-card");
        function activate(card) {
            if (card.querySelector("iframe")) return;
            var f = document.createElement("iframe");
            f.src = "https://www.youtube.com/embed/" + card.getAttribute("data-id") + "?autoplay=1&rel=0";
            f.title = card.getAttribute("data-title");
            f.allow = "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture";
            f.allowFullscreen = true;
            card.innerHTML = "";
            card.appendChild(f);
            card.removeAttribute("tabindex");
            card.removeAttribute("role");
            card.removeAttribute("aria-label");
            card.style.cursor = "default";
        }
        cards.forEach(function (card) {
            card.addEventListener("click", function () { activate(card); });
            card.addEventListener("keydown", function (e) {
                if (e.key === "Enter" || e.key === " ") { e.preventDefault(); activate(card); }
            });
        });
    })();
</script>

## New here?

1. [**Getting started**](/features/getting-started/) — account, character, your first
   robot, and how to get your first credits.
2. [**FAQ**](/features/faq/) — the tutorial, picking a faction, what to do next.
3. [**Movement & zones**](/features/movement/) — how you get around (teleports,
   interzones, docking).
4. [**Gathering**](/features/gathering/) — ores and plants, the foundation of the
   economy.

## What you can do

**<span class="menuhead"><svg class="menuicon" aria-hidden="true"><use href="#mi-radar"/></svg> In the field</span>**
- [Gathering & scanning](/features/gathering/) — mine ore, harvest plants, scan for nodes
- [Combat](/features/combat/) — PvP and NPC combat, damage types, loot and insurance
- [NPCs & PVE hunting](/features/npcs/) — ranks, weaknesses, spawns, TAPs, tactics
- [Exploration](/features/exploration/) — artifacts and relics under the surface
- [Missions](/features/missions/) — repeatable jobs for credits, items and EP
- [Movement & zones](/features/movement/) — travel, teleports, zone types

**<span class="menuhead"><svg class="menuicon" aria-hidden="true"><use href="#mi-arm"/></svg> Industry & progress</span>**
- [Robots & fitting](/features/robots/) — bodies, classes and per-model stats
- [Modules & fitting](/features/modules/) — the module families and fitting rules
- [Production](/features/production/) — the item lifecycle: reverse engineering, mill, prototypes
- [Research](/features/research/) — extensions and the tech tree
- [Sparks](/features/sparks/) — your agent's nanobot and its ability bonuses
- [Items & inventory](/features/items/) — containers, stacks, packing

**<span class="menuhead"><svg class="menuicon" aria-hidden="true"><use href="#mi-panel"/></svg> Trade & economy</span>**
- [Market & trade](/features/market/) — buy, sell and trade with other players
- [Transport](/features/transport/) — hire couriers or move cargo yourself
- [Shop](/content/shop/) — server-side shop items (generated table)

**<span class="menuhead"><svg class="menuicon" aria-hidden="true"><use href="#mi-star"/></svg> Big play</span>**
- [Power base stations](/features/pbs/) — build and run a PBS: reactors, production,
  defense, docking
- [Outposts & SAP](/features/outposts/) — outpost ownership, stability and the four
  SAP activity types
- [Intrusion](/features/intrusion/) — contest sites with SAPs, raise stability, unlock
  facilities
- [Terraforming & zone resources](/zones/) — how zones grow ore and plants, and
  terraformable zones (see the [worked zone examples](/zones/zone-tm/) and
  [gamma zone 106](/zones/zone-gamma-z106/))

**<span class="menuhead"><svg class="menuicon" aria-hidden="true"><use href="#mi-robots"/></svg> With others</span>**
- [Groups](/features/groups/) — corporations, alliances, gangs
- [Social](/features/social/) — mail, channels, friends, standings

**<span class="menuhead"><svg class="menuicon" aria-hidden="true"><use href="#mi-crystal"/></svg> Knowledge</span>**
- [FAQ](/features/faq/) — early-game questions
- [Abbreviations](/features/abbreviations/) — the jargon (EP, NIC, CT, TAP, …)
- [Lore](/features/lore/) — the setting: Nia, the Nians, the Syndicate
- [Client & PC setup](/features/client-setup/) — running the client, multi-boxing rules,
  UI scaling

## Data reference

- [**Content tables**](/content/) — every ore, plant, item, robot, extension, recipe,
  mission, tech-tree node and shop item, generated from the live database
- [**Zones**](/zones/) — the full zone index, resource-generation rules and the
  [zone map](/zones/map/)
  (every zone on the grid with its teleport links)
- [**Formats**](/formats/) — developer reference: stat fields and zone file formats
- [**Server & reference**](/features/server/) — server info, high scores, reference data

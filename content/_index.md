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

**<span class="menuhead"><svg class="menuicon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5.5"/><circle cx="12" cy="12" r="1.6"/><path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M18.4 5.6l-1.8 1.8M7.4 16.6l-1.8 1.8"/></svg> In the field</span>**
- [Gathering & scanning](/features/gathering/) — mine ore, harvest plants, scan for nodes
- [Combat](/features/combat/) — PvP and NPC combat, damage types, loot and insurance
- [NPCs & PVE hunting](/features/npcs/) — ranks, weaknesses, spawns, TAPs, tactics
- [Exploration](/features/exploration/) — artifacts and relics under the surface
- [Missions](/features/missions/) — repeatable jobs for credits, items and EP
- [Movement & zones](/features/movement/) — travel, teleports, zone types

**<span class="menuhead"><svg class="menuicon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M5 21h14M5 21l2.5-3h9L19 21M9 18l-1.5-4L12 10l1-4 3 .8-.8 4-3.2 3.4L11 18"/></svg> Industry & progress</span>**
- [Robots & fitting](/features/robots/) — bodies, classes and per-model stats
- [Modules & fitting](/features/modules/) — the module families and fitting rules
- [Production](/features/production/) — the item lifecycle: reverse engineering, mill, prototypes
- [Research](/features/research/) — extensions and the tech tree
- [Sparks](/features/sparks/) — your agent's nanobot and its ability bonuses
- [Items & inventory](/features/items/) — containers, stacks, packing

**<span class="menuhead"><svg class="menuicon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="3.5" width="16" height="17" rx="1.5"/><path d="M12 6.5v6M9.5 8.5h5M12 12.5l-1.8 4.5M12 12.5l1.8 4.5"/></svg> Trade & economy</span>**
- [Market & trade](/features/market/) — buy, sell and trade with other players
- [Transport](/features/transport/) — hire couriers or move cargo yourself
- [Shop](/content/shop/) — server-side shop items (generated table)

**<span class="menuhead"><svg class="menuicon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2.5 14.2 9.8 21.5 12 14.2 14.2 12 21.5 9.8 14.2 2.5 12 9.8 9.8Z"/></svg> Big play</span>**
- [Power base stations](/features/pbs/) — build and run a PBS: reactors, production,
  defense, docking
- [Outposts & SAP](/features/outposts/) — outpost ownership, stability and the four
  SAP activity types
- [Intrusion](/features/intrusion/) — contest sites with SAPs, raise stability, unlock
  facilities
- [Terraforming & zone resources](/zones/) — how zones grow ore and plants, and
  terraformable zones (see the [worked zone examples](/zones/zone-tm/) and
  [gamma zone 106](/zones/zone-gamma-z106/))

**<span class="menuhead"><svg class="menuicon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="9" width="6.5" height="9" rx="1"/><path d="M7.2 9V6.2h0.1M7.2 6.2a1 1 0 1 0 .1 0M4 12H2.2M10.5 12H12.3"/><rect x="13.5" y="9" width="6.5" height="9" rx="1"/><path d="M16.8 9V6.2h0.1M16.8 6.2a1 1 0 1 0 .1 0M13.5 12h-1.8M19.5 12h1.8"/></svg> With others</span>**
- [Groups](/features/groups/) — corporations, alliances, gangs
- [Social](/features/social/) — mail, channels, friends, standings

**<span class="menuhead"><svg class="menuicon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2.5 19 12l-7 9.5L5 12Z"/><path d="M5 12h14M12 2.5 9 12l3 9.5"/></svg> Knowledge</span>**
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

---
title: "Intrusion"
description: "The intrusion mode: NPC sites, SAP items, defense thresholds, facility upgrades, and stability."
weight: 150
---

# Intrusion

**Intrusion** is a mode built around **NPC sites** — player-run or contestable sites with
**stability**, **defense**, and **facilities**. You invest items, raise defenses, and
upgrade facilities to control and profit from a site.

> The in-client name for "intrusion" should be confirmed. UI locations follow the
> [client UI overview](/features/ui/). Mechanics below are confirmed against the server.

## Sites & stability

- **Site info** — details of an intrusion site.
- **Stability** — the site's core stat, a value from **0 to 150**. A new owner starts at
  **1**. Stability drives everything:
  - **Bonus thresholds** (content, `intrusionsitestabilitythreshold`): facility bonuses
    unlock at stability **20 / 60 / 80**; a stack of aura effects (intrusion core,
    detection, geoscan, harvester, industrial, masking, mining, repair, signals — each
    lvl1–3, plus combined lvl4 effects) unlocks in tiers at **10, 20, 40, 50, 70, 80 and
    100**; **docking rights** unlock at **50**; the defense-node limit rises at **40**.
  - **Production points** = `stability / 10` (max 15) — the currency for upgrading
    facilities (one point per level).
  - **Decay**: if no successful SAP happens for **5 days**, the site starts losing
    **5 stability per day** until action is taken again.
  - **Loss**: stability hitting **0** makes the owner lose the site (it becomes
    unowned).
- **Stability log** — review changes to a site's stability.

### Who moves stability

Stability changes come from completed **SAPs** (below) and the system decay. The effect
depends on *who* wins the SAP relative to the current owner:

- **Owner wins** → stability **gains** the SAP's value.
- **An ally wins** (mutual standing ≥ 10) → **no change**.
- **A hostile corp wins** → stability **loses** the SAP's value.
- **Unowned site** → the first player-corp winner **takes ownership** (stability
  resets to 1).

## SAPs (site attack points)

A **SAP** is a temporary objective that spawns at a site. Completing it drops a **loot
container** at the SAP's position, changes stability, and pays **120 EP** to every
participating character. **NPC cultists contest the SAPs too** — if they fill the score
first, the SAP counts against your side. There are four SAP types:

| SAP | How to complete | Stability value |
|---|---|---|
| **Active hacking** | use a hacking module on the SAP (up to 120 module uses) | +15 |
| **Passive hacking** | stand within range 5 of the SAP for **8 minutes** | +10 |
| **Destruction** | damage the SAP to death (NPC damage counts too) | +15 |
| **Specimen processing** | submit **5–6 SAP specimens** (in batches of 2–3, 1.5 min cooldown, range 7) | +15 |

- **Submit a SAP item** — in a zone, submit a specimen to a specimen-processing SAP.
- **SAP item info** — inspect SAP item data (yours and general).

SAP specimens are the consumables of the mode; they also carry a large core value, so
they double as [PBS reactor fuel](/features/pbs/).

## Defense & effects

- **Set defense threshold** — configure the site's defense threshold (when the site's
  automated defense engages).
- **Set effect bonus** — set the aura/effect bonus configuration on a site (from the
  stability-unlocked aura set).
- **Upgrade a facility** — improve a site facility: costs **1 production point**
  (i.e. 10 stability), CEO / deputy / accountant only, capped at a maximum level.
- **Set a site message** — set a public message for the site.

## Logs

- **My intrusion log** — your intrusion activity.
- **Public log** — the public intrusion feed.
- **My sites log** — activity on your sites.
- **Stability log** — stability changes (see above).

## Practical notes

- **Stability is the core stat** — it gates the bonus tiers, produces the upgrade
  currency, and is what hostile SAPs strip away.
- **SAPs are contested** — the NPC cultists race you; a lost SAP costs you stability.
- **Don't let a site sit idle** — 5 days without a successful SAP starts the daily
  decay.
- **Logs tell the story** — use the my/public/stability logs to track what's happening
  across your sites.

<!-- TODO: only the in-client name for "intrusion" and the UI remain.
     Confirmed: stability 0-150, start 1 (Outpost.cs constants); bonus thresholds from
     intrusionsitestabilitythreshold (facility 20/60/80; aura effects 50-74 at tiers
     10/20/40/50/70/80/100; docking rights 50; defense nodes 40); production points =
     stability/10, facility upgrade costs 1 point, CEO/Deputy/Accountant; decay -5/day
     after 5 days idle (OutpostDecay.cs); owner-win +, ally (standing>=10) 0,
     hostile -; unowned -> winner takes it; stability 0 -> ownership lost; 120 EP per
     SAP participant (EP_WINNER, EpForActivityType.Intrusion); 4 SAP types: active
     hacking (120 module uses, +15), passive (8 min in range 5, +10), destruction
     (kill it, +15), specimen processing (5-6 specimens in 2-3 batches, 1.5 min cd,
     range 7, +15; siegeitems table: def_specimen_sap_item 2-3); SAP increase values
     from entitydefaults #increase option; NPC sap guard/invade units contest SAPs.
     Developer notes: Intrusion/IntrusionSAPSubmitItem.cs (SpecimenProcessingSAP,
     zone submit), IntrusionSAPGetItemInfo.cs / IntrusionSapItemInfo.cs,
     IntrusionSetDefenseThreshold.cs, IntrusionSiteSetEffectBonus.cs,
     IntrusionUpgradeFacility.cs, SetIntrusionSiteMessage.cs; GetIntrusionSiteInfo.cs,
     GetIntrusionLog.cs / GetIntrusionPublicLog.cs / GetIntrusionMySitesLog.cs /
     GetIntrusionStabilityLog.cs, GetStabilityBonusThresholds.cs;
     EventProcessors/AffectOutpostStability.cs; Zones/Intrusion/Outpost.cs,
     OutpostDecay.cs, SAP.cs + *SAP.cs subclasses. -->

# Category tree (target structure)

Locked target structure for the wiki. This is the skeleton only — **no content has
been ingested from the community wiki backup**, and none will be: every `[planned]`
page is to be written originally (the backup is reference material for *shape* and
*coverage*, not source text).

License: the wiki is **Apache-2.0** (see `LICENSE/`). All formerly ingested
feature pages have been rewritten from scratch or from backend sources, so no
copyleft-licensed text remains in the repository. See IMPROVEMENT-037 in the
backlog.

Legend: `[existing]` page exists today · `[planned]` page to be written later ·
`(catalog)` generated pages · `(sub)` sub-list under its parent in the left menu.

## Top-level menu

1. **New here**
   - Getting started `[existing]`
   - First hours (survival guide) `[planned]`
   - FAQ `[existing]`
   - Client & PC setup `[existing]`
     - Linux setup `[planned] (sub)`
     - UI scaling `[planned] (sub)`
     - Multi-boxing `[planned] (sub)`
     - Reshader `[planned] (sub)`
   - Abbreviations `[existing]` (moved here from Knowledge)
   - Home base (respawn / declared terminal) `[planned]`

2. **Zone map** — top-level entry, always visible `[existing]`

3. **In the field**
   - Gathering `[existing]`
   - Combat `[existing]`
     - Damage & application `[planned] (sub)`
     - Resistances `[planned] (sub)`
   - Missions `[existing]`
   - Movement `[existing]`
   - NPCs `[existing]`
     - NPC ranks `[planned] (sub)`
     - Tactics `[planned] (sub)`
     - Bosses `[planned] (sub)`
   - Exploration `[existing]`
   - Probes `[existing]`
   - Field guides (combat / exploration / industry) `[planned]`

4. **How it works** — *new group* (all planned; the group goes live when its first
   pages exist)
   - Damage & application `[planned]`
   - Fitting `[planned]`
   - Powergrid `[planned]`
   - Energy (core) `[planned]`
   - Resistances `[planned]`
   - Detection & stealth (EWAR) `[planned]`
   - Range & falloff `[planned]`
   - Speed `[planned]`
   - Remote assistance `[planned]`
   - Forms `[planned]`
   - NEXUS / squad bonuses `[planned]`

5. **Industry & progress**
   - Robots `[existing]` — to become a per-robot catalog `[planned (catalog)]`
     (one generated page per robot: class, chassis stats, extension requirements &
     bonuses, where to buy; prose descriptions written originally)
   - Modules `[existing]` — to become per-family pages `[planned]`
     (weapons, armor, shield, energy, electronics, EWar, industrial, NEXUS, special)
   - Production `[existing]`
   - Research `[existing]`
   - Items & inventory `[existing]`
   - Sparks `[existing]`
   - Calibration / prototyping `[planned]`
   - Reverse engineering `[planned]`

6. **Trade**
   - Market `[existing]`
   - Transport (CT capsules) `[existing]`
   - Item shop `[existing]` — 11 category pages `[existing (catalog)]`

7. **Big play**
   - PBS `[existing]`
   - Outposts `[existing]`
   - SAP `[planned]`
   - Intrusion `[existing]`
   - Relations / territorial warfare `[planned]`

8. **With others**
   - Corps `[existing]`
     - Creation & management `[planned] (sub)`
     - CEO takeover / logo editor `[planned] (sub)`
   - Squads (NEXUS) `[planned]`
   - Social `[existing]`

9. **World & lore** — *renamed from "Knowledge"* (lore stays; abbreviations move out)
   - Lore `[existing]`
   - Nia / Discovery of Nia `[planned]`
   - Syndicate `[planned]`
   - Zone guides (per-zone prose alongside the generated zone data pages) `[planned]`

10. **Data reference**
    - Zone map `[existing]`
    - Content (catalog index) `[existing]`
    - Stat reference `[existing]` *(added to the menu — currently only linked from
      the item catalog)*
    - Zones (data) `[existing]`
    - All features `[existing]`
    - Server `[existing]`
    - Formats (developer) `[existing]`

## Implementation notes

- The tree changes nothing that exists yet: no page moves until a `[planned]` page
  is actually written, so no URLs break. The live site keeps its current 8 groups.
- When the first "How it works" pages land, insert the group between *In the field*
  and *Industry & progress*; when "Knowledge" becomes "World & lore", update
  `content/menu/knowledge.md` → `/menu/world-and-lore/` and re-link.
- Abbreviations moves to *New here* only when the group gains its planned pages
  (moving it now would be a pointless URL churn).
- Per-robot pages and module family pages are generator work (P1/P3 of the
  reorganisation plan); everything else is original prose.
- Left menu, `/menu/<slug>/` overview pages and the search index all follow the
  tree; overview pages get one line per child (as today).

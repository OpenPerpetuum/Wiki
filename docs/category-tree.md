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

The sidenav is a high-level view by default: three top-level links (**Home**,
**World**, **All features** — always visible) plus four collapsible groups.
Each group header links to its overview page under `/menu/`; the caret
collapses the group's items (JS auto-expands the group containing the current
page). Group labels and the overview pages live in `config.toml`
(`extra.ui.nav_*`) and `content/menu/`.

0. Top-level links (always visible, above the groups)
   - **Home** — quick link to `/` `[existing]`
   - **World** (zone map) — top-level entry with map sub-anchors (training,
     starter islands, beta, gamma tiers, protection); collapsed by default
     `[existing]`
   - **All features** — quick link to the full system list `/features/`
     `[existing]`

1. **Start** (`/menu/start/`)
   - Getting started `[existing]`
   - First hours (survival guide) `[planned]`
   - FAQ `[existing]`
   - Client & PC setup `[existing]`
     - Linux setup `[planned] (sub)`
     - UI scaling `[planned] (sub)`
     - Multi-boxing `[planned] (sub)`
     - Reshader `[planned] (sub)`
   - Home base (respawn / declared terminal) `[planned]`

2. **Play** (`/menu/play/`) — merges the old *In the field*, *Big play*, and
   *With others* groups
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
   - PBS `[existing]`
   - Outposts `[existing]`
   - SAP `[planned]`
   - Intrusion `[existing]`
   - Relations / territorial warfare `[planned]`
   - Groups (corps / alliances) `[existing]`
     - Creation & management `[planned] (sub)`
     - CEO takeover / logo editor `[planned] (sub)`
   - Squads (NEXUS) `[planned]`
   - Social `[existing]`
   - Field guides (combat / exploration / industry) `[planned]`

3. **Systems** (`/menu/systems/`) — merges the old *Industry & progress* and
   *Trade* groups
   - Robots `[existing]` — to become a per-robot catalog `[planned (catalog)]`
     (one generated page per robot: class, chassis stats, extension requirements &
     bonuses, where to buy; prose descriptions written originally)
   - Production `[existing]`
   - Research `[existing]`
   - Items & inventory `[existing]`
   - Modules `[existing]` — to become per-family pages `[planned]`
     (weapons, armor, shield, energy, electronics, EWar, industrial, NEXUS, special)
   - Sparks `[existing]`
   - Calibration / prototyping `[planned]`
   - Reverse engineering `[planned]`
   - Market `[existing]`
   - Transport (CT capsules) `[existing]`
   - Item shop `[existing]` — 11 category pages `[existing (catalog)]`

4. **Reference** (`/menu/reference/`) — merges the old *Knowledge* and
   *Data reference* groups
   - Content (catalog index) `[existing]`
   - Zones (data) `[existing]`
   - Extension tree `[existing]`
   - Extensions `[existing]`
   - Stat reference `[existing]` *(added to the menu — currently only linked from
     the item catalog)*
   - Abbreviations `[existing]`
   - Lore `[existing]`
     - Nia / Discovery of Nia `[planned]`
     - Syndicate `[planned]`
   - Zone guides (per-zone prose alongside the generated zone data pages) `[planned]`
   - Server `[existing]`
   - Formats (developer) `[existing]`

**How it works** — *new group* (all planned; the group goes live when its first
pages exist; insert between **Play** and **Systems**):
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

## Implementation notes

- The 4-group menu is live: the sidenav groups are collapsible and the old 8
  groups were merged into the 4 overview pages (the former `/menu/` pages for
  *in-the-field*, *industry*, *trade*, *big-play*, *with-others*, *knowledge*,
  and *data-reference* were replaced by `/menu/start|play|systems|reference/`).
- When the first "How it works" pages land, add the group to the sidenav
  between **Play** and **Systems** (new `nav_*` string + `/menu/` page +
  search-index regen).
- Per-robot pages and module family pages are generator work (P1/P3 of the
  reorganisation plan); everything else is original prose.
- Left menu, `/menu/<slug>/` overview pages and the search index all follow the
  tree; overview pages get one line per child (as today).

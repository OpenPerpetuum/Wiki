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

The sidenav has the **Home** quick link on top, then the main items: Start,
World, Play, Systems, Reference. Groups are **open by default** (root -> sub
always visible); 2nd-level sub-lists (Gamma's tiers, item-shop categories,
Content tables' sections, Zones' pages) start collapsed, unless the current
page lives inside them (e.g. an item-shop category page opens Item shop,
a tech-tree node page opens Content tables). Caret buttons collapse/expand
any section; on load the sidenav is scrolled so the active entry is visible.
Group headers (except World) link to their overview page under `/menu/`.
Labels live in `config.toml` (`extra.ui.nav_*`), overview pages in
`content/menu/`.

0. **Home** — quick link to `/`, above all groups `[existing]`

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

2. **World** (zone map `/zones/map/`) — 2nd top-level item, styled like a
   group; sub-items are the map anchors (training, starter islands, beta,
   protection), and the 2nd-level *Gamma* sub-list (T0–T4) starts collapsed
   `[existing]`

3. **Play** (`/menu/play/`) — merges the old *In the field*, *Big play*, and
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

4. **Systems** (`/menu/systems/`) — merges the old *Industry & progress* and
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

5. **Reference** (`/menu/reference/`) — merges the old *Knowledge* and
   *Data reference* groups
   - Content tables `[existing]` — 2nd-level sub-list: Items, Ores, Plants,
     Deployables, Robots, Missions, Recipes, Tech tree, Stat reference
   - Zones (data) `[existing]` — 2nd-level sub-list: Generation, Zone index
   - All features (full system list `/features/`) `[existing]`
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

**How it works** — *new group* (all planned; the group goes live when its
first pages exist; insert between **Play** and **Systems**):
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

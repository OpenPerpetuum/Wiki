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
World, Play, Systems — plus a **Dev** group that is only visible in **dev
mode** (the Play/Dev knob in the top bar; dev mode also switches the site
to the "matrix" theme: near-black, glowing bright green). Groups are
**open by default** (root -> sub always visible); 2nd-level sub-lists
start collapsed, unless the current page lives inside them (e.g. a
shop category page opens Item shop, a tech-tree node page opens Tech
tree). Caret buttons collapse/expand any section; on load the sidenav is
scrolled so the active entry is visible. Group headers (except World)
link to their overview page under `/menu/` (Dev's links to the developer
home). Labels live in `config.toml` (`extra.ui.nav_*`), overview pages in
`content/menu/`.

0. **Home** — quick link to `/`, above all groups `[existing]`

1. **Start** (`/menu/start/`)
   - Getting started `[existing]`
   - FAQ `[existing]` — includes the abbreviations table (merged in; the
     standalone Abbreviations page is gone)
   - Lore `[existing]`
   - Client & PC setup `[existing]`

2. **World** (zone map `/zones/map/`) — 2nd top-level item, styled like a
   group; sub-items are the map anchors (training, starter islands, beta,
   protection), and the 2nd-level *Gamma* sub-list (T0–T4) starts collapsed
   `[existing]`
   - Training
   - How it works `(sub)` — Protection levels, Zone generation
   - Alpha, Beta, Gamma (T0–T4 sub-list)
   - All zones (zone index)

3. **Play** (`/menu/play/`)
   - Gathering `(sub)` — Ores, Plants
   - Combat `(sub)` — NPCs & PVE hunting
   - Missions `(sub)` — Mission table
   - Movement & zones `(sub)` — Exploration, Proximity probes
   - Bases & sites `(sub)` — PBS, Outposts & SAP, Intrusion, Deployables
   - With others `(sub)` — Groups, Social

4. **Systems** (`/menu/systems/`)
   - Robots & fitting `(sub)` — Robot stat tables
   - Production `(sub)` — Recipes
   - Research `(sub)` — Tech tree
   - Character `(sub)` — Extensions, Sparks
   - Items & inventory `(sub)` — Modules, Stat reference
   - Market & trade `(sub)` — Transport, Item shop (11 category pages,
     3rd-level sub-list)

5. **Dev** (developer home `/features/architecture/`) — visible **only in
   dev mode** (top-bar knob; matrix theme)
   - Developer home — high-level architecture of the open reimplementation
   - Server & reference (DB schema / SQL)
   - Formats (file formats)

## Implementation notes

- The 4-group menu (Start, World, Play, Systems) plus the hidden Dev group
  is live: the sidenav groups are collapsible and the former `/menu/` pages
  were merged into `/menu/start|play|systems/` (`/menu/reference/` was
  deleted with the Reference group).
- Dev mode is a client-side toggle (`html[data-mode=dev]`, persisted in
  `localStorage` under `wiki-mode`): it reveals the Dev sidenav group and
  applies the matrix theme (all in `static/style.css`, the knob in
  `templates/base.html`).
- Per-robot pages and module family pages are generator work (P1/P3 of the
  reorganisation plan); everything else is original prose.
- Left menu, `/menu/<slug>/` overview pages and the search index all follow the
  tree; overview pages get one line per child (as today).

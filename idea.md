# Idea: Game Features & Content Reference Documentation

Status: COMPLETE — all four phases done (Phase 4, 2026-09-25). Phase history: Phase 3 (2026-09-25); Phase 2b (2026-09-25); Phase 2 (2026-09-24); Phase 1 (2026-07-07).
- Zola site scaffolded (`wiki/config.toml`, `wiki/content/`), CI in `.github/workflows/wiki.yml`
- `content/features/index.md`: complete player-action inventory (394 player-accessible
  commands out of 639, mapped to 17 systems) — coverage verified programmatically
- 17 system pages + 1 reference page under `content/features/` — **all 18 written**,
  grounded in the server implementation (handlers + zone terrain code). `ui.md` is the
  agreed EVE-style client model (project-provided); every page references its conventions.
  Each page carries a `TODO(Phase 1)` note listing what still needs content data
  (Phase 2) or client confirmation.
- Plant stat fields (growRate, fertility, spreading, killDistance, …) documented from
  `PlantRule.cs` + `NatureCube.cs` in gathering.md; full field reference deferred to
  Phase 4 `formats/`
- Phase 1 (player wiki) is **complete** pending the per-page `TODO(Phase 1)`
  follow-ups (client confirmation).
- **Phase 2 (generated content tables) is complete:**
  - `tools/generate/Perpetuum.WikiGenerate/` — C# (.NET 8) read-only generator;
    `regenerate.sh` + `README.md` document how to run it. Verified against the live DB
    (docker `perpetuumsa`) and the `planttrules` files.
  - Generated + committed under `wiki/content/content/`: `ores.md`, `plants.md`
    (all 69 species rules: growRate/fertility/spreading/killDistance/fruit + per-zone
    fertility), `deployables.md`, `items.md` (full catalog by family), `robots.md`
    (chassis + named templates), `index.md`. Every page states its source tables + date.
  - Plant rule file format (newline `key=value`, `n/N/f/i/L/$` tokens, `source:` overrides)
    parsed per `PlantRuleLoader.cs`; GenXY inline options parsed per the content guide.
  - Category/attribute flag masks live in `Model.cs` (`Flags`), verified against
    `src/Perpetuum.ExportedTypes` + the `categoryFlags`/`attributeFlags` tables.
  - **Phase 3 — zone generation section done (2026-09-25):** `zones/` with
    `_index.md` (landing), `generation.md` (ore-node random-walk placement + plant
    fertility/spreading pipeline, grounded in `TerrainsModule.cs`,
    `MineralNodeGeneratorBase.cs`, `RandomWalkMineralNodeGenerator.cs`, `MineralLayer.cs`,
    `PlantHandler.cs`, `NatureCube.cs`, `PlantRuleExtensions.cs`), generated
    `zone-index.md` (all 84 zones: type/protection/fertility/species/ore config, via
    `--zones-out`), and three worked examples verified against the live DB:
    `zone_tm` (protected PvE, 3 ores), `zone_asi` (open PvP, 8 ores incl. epriton +
    flux-ore keep-out + NPC-spawn coupling), `zone_gamma_z106` (terraformable PvP,
    fertility 15, tier-2 + player-seeded plants). Note: Zola slugifies underscores to
    hyphens, so the example URLs are `/zones/zone-tm/` etc.
  - **Phase 2b — economy/progression tables added (2026-09-25):** `extensions.md`
    (358 skills: rank, price, bonus, prerequisites), `techtree.md` (658 nodes: unlocked
    item, enabler extension, point prices), `missions.md` (322 missions + rewards),
    `shop.md` (842 vendor items), `recipes.md` (1,877 craftable items + components).
    The DB-answerable gaps in the feature pages are now linked and their `TODO` notes
    narrowed to what genuinely needs code/client confirmation (EP rates, tax/margin
    constants, insurance premiums, UI flows).
- **Local Zola build verified** (v0.23.6): 23 pages, 2 sections, 0 orphans, `zola check`
  passes. Templates + minimal CSS + elasticlunr search live under `wiki/templates/` and
  `wiki/static/`. CI (`.github/workflows/wiki.yml`, pinned to Zola v0.23.6) deploys to
  GitHub Pages.
  - Zola 0.23 gotchas handled: front matter is **YAML** (not TOML); no `get_path`/`url`
    funcs or config `links` — nav is hardcoded in `templates/base.html`; section landing
    pages must be `_index.md`; search uses the built-in `search_index.en.js` (elasticlunr).
- **Phase 4 — formats/ section done (2026-09-25):** dev-facing field reference —
  `plant-fields.md` (GenXY file format incl. the bare-`4` hex-array trap, every rule
  field with type/default/consumer, growth state machine + fruit math with grass_a and
  wall worked examples), `ore-fields.md` (minerals/mineralconfigs columns, node
  generation formulas, mineralnodes lifecycle, solid vs liquid extraction),
  `deployable-fields.md` (flag identification, capsule↔object targetdefinition,
  runtime aggregate fields incl. despawn_time), `zone-files.md` (binary layer structs:
  BlockingInfo/TerrainControlFlags/PlantInfo byte layouts, 1-vs-2-byte control quirk,
  save cycle). All cross-links are site-absolute (`/section/page/`) — Zola pretty URLs
  make plain relative links resolve under the referring page's directory (fixed
  site-wide, 26 files).
- **All server-side data TODOs resolved (2026-09-25, against a fresh migration):**
  transport (full lifecycle table), combat (PvP flag, loot, insurance), research (EP
  1440/day, EP cost formula), market (tax 12%, listing fee), production (refine/
  recycle/repair formulas, CPRG), PBS (416 defs, fuel table), intrusion (stability
  0–150, 4 SAP types, bonus thresholds, decay), probes (visibility probe stats,
  mining probe tiers), groups (role gates, document types), missions (4 structure
  types), items (stacking/packing rules, gift pool), robots (slot flags + fit
  rule), server (no real-money store on this deployment), social (standing ±10).
- Remaining: per-page client-UI verifications only (UI flows, in-client names),
  and the deploy follow-ups (enable GitHub Pages, set `base_url` for the hosted
  site).

## Purpose

Create a player-facing + developer-facing reference documentation set under `wiki/` that
covers:

1. **Every gameplay action a player can take** (zone, space, market, tech tree, crafting,
   fleet operations, etc.).
2. **The full stats of every content entity**: items, deployables, plants, ores.
3. **Per-zone resource generation patterns**: examples of how ores, plants and deployables
   spawn, with the underlying file-format/stat definitions that drive them.
4. **File format / stat field documentation**: what fields like `growRate`, `fertility`,
   `spreading` (and their siblings) mean, where they live (DB tables vs. serialized
   zone/sector files), and how the server interprets them at runtime.

## Why

- `docs/db_structure/database_schema_documentation.md` documents schema, but not gameplay
  semantics. E.g. `fertility` (int, default 60) and `spreadingang` (bit, default 0) are
  listed with no explanation of what they do to plant growth/spread behaviour.
- `docs/file_formats/` covers a3m and gbf file structures but not the *content* fields
  players see (growRate, spread rate, harvest yields, deployable stats).
- `docs/content/claude_game_content_guide.md` is a procedural guide for *creating* content;
  there is no reference for *what exists* and *how it behaves*.
- New contributors and modders currently have to reverse-engineer behaviour from code
  (zone update loops, plant growth, ore regeneration) to answer basic questions like
  "how does a plant spread?" or "what makes a sector's ore respawn?".

## Tech Stack & Deployment

**Generator: Zola** (Rust, single static binary, fastest of the static generators).

Why Zola over MkDocs Material / Docusaurus / Hugo:
- Content is ~90% generated Markdown tables + prose — Zola's sweet spot; rebuilds a
  large corpus (hundreds of entity/zone pages) in milliseconds, which matters once
  content is regenerated wholesale from the DB.
- Built-in search (minisearch), Mermaid diagrams, KaTeX, syntect code highlighting —
  the commonly-missing features are already there.
- No Python/Node stage in CI: one static binary, zero build artifacts to cache.
- Genuinely missing vs. Material (versioning plugin ecosystem, theme polish) is covered:
  theme gaps via template/partials overrides; versioning not needed for a single-version
  reference.

Layout:

```
wiki/
├── idea.md                          ← this file
├── config.toml                      ← Zola config, nav, search, mermaid
├── templates/                       ← custom entity/index templates (stat-table layouts)
├── static/                          ← images, CSS overrides
├── tools/generate/                  ← DB → Markdown generator (writes into content/)
└── content/
    ├── features/                    ← Phase 1: player wiki (what players can do)
    │   ├── index.md                 ← exhaustive list of player actions
    │   ├── movement.md              ← zone travel, warp, sector transitions
    │   ├── gathering.md             ← ore mining, plant harvesting, deployable stripping
    │   ├── combat.md                ← robot combat, NPC attacks, fleet rules
    │   ├── crafting.md              ← robot assembly, item crafting, tech tree
    │   ├── trading.md               ← market, exchange, trade commands
    │   ├── deployables.md           ← placement, removal, ownership, fees
    │   ├── territory.md             ← flags, territories, zone control
    │   └── accounts.md              ← login, mail, friends, clan/guild, settings
    ├── content/                     ← Phase 2: entity stat references (generated)
    │   ├── ores.md
    │   ├── plants.md
    │   ├── deployables.md
    │   ├── items.md
    │   └── robots.md
    ├── zones/                       ← Phase 3: per-zone generation patterns
    │   ├── index.md
    │   ├── generation.md
    │   └── zone_XX.md               ← worked examples (low/mid/high)
    └── formats/                     ← Phase 4: dev-facing stat & file format semantics
        ├── plant_fields.md
        ├── ore_fields.md
        ├── deployable_fields.md
        └── zone_files.md
```

Deployment (static hosting):
- `zola build` → `public/` → GitHub Pages (or any static host).
- CI (`.github/workflows/wiki.yml`): install Zola binary (`curl -s https://go.zola.dev/install.sh | bash`),
  `zola build`, publish `public/`.
- **Generated Markdown is committed to git**; the build needs no DB access. The generator
  runs locally when content changes, and diffs of generated tables are reviewable.
- Nav design: 4 top sections → index/landing pages → entity pages. Use custom index
  templates (search-as-navigation) rather than a flat mega-sidebar, since hundreds of
  entity/zone pages would make the sidebar unwieldy.

## Proposed Structure (per-section content)

See the tree above for placement; sections are phased (see Phasing):

## Content Requirements (per doc)

### features/*.md (player wiki — Phase 1, the first focus)
- **Player-facing voice**: in-game terms ("harvest a plant", "deploy a beacon"), not
  internal code terms. No handler names, no error codes, no DB references in the body.
- What the player does, how (in-client steps), and why they'd do it.
- Preconditions in player terms (level, zone type, ownership, membership).
- Costs and rewards in player terms (credits, XP, items, time).
- Common mistakes / edge cases a player would actually hit ("why can't I do this?").
- Dev links (handler, service, DB objects) go in a collapsed "For developers" footnote at
  the bottom, kept out of the player reading flow.

### content/*.md
- Complete tables generated from the database (or from content SQL in
  `docs/db_structure/stored_procedures/` + definitions tables), not hand-typed.
- For each entity: name, definition id (resolved, not hardcoded in prose),
  full stat breakdown, dependencies (what it requires / what requires it),
  where it spawns / can be obtained.
- Note the authoritative source (table names) so the doc can be regenerated.

### zones/*.md
- Zone purpose and theme, sector layout.
- Example generation pattern: which ores/plants spawn, at what density,
  regeneration timing — with a worked example (e.g. "a low zone has X ore
  sectors, Y plant sectors; typical harvest loop is ...").
- Links to the stat field docs so readers understand what the numbers mean.

### formats/*.md
- Field name, type, default, range.
- Where it is stored (DB table.column vs. zone file vs. item definition extension).
- How the runtime uses it (code path: zone update tick, growth calculation,
  spread decision).
- Worked numeric examples (e.g. "a plant with fertility=60, growRate=N takes
  approximately T ticks to become harvestable").
- Cross-reference to `docs/file_formats/` for raw file structures.

## Source Material (where the answers come from)

| Topic | Source |
|---|---|
| DB schema, defaults | `docs/db_structure/database_schema_documentation.md`, `views/`, `functions/` |
| Content definitions | `docs/content/claude_game_content_guide.md`, definition/extension tables |
| Zone state file format | `docs/file_formats/a3m_specification.md`, `gbf_specification.md` |
| Growth/spread behaviour | zone update code (search `growRate`, `fertility`, `spreading` in `src/`) |
| Player commands | `Commands.cs`, `Perpetuum.RequestHandlers/*` |
| Architecture | `docs/codebase/ARCHITECTURE.md`, `STRUCTURE.md` |

## Phasing

**Phase 1 — Player wiki (first focus):**
1. Scaffold Zola site (config.toml, nav, CI build, GitHub Pages deploy of a stub site).
2. Inventory all request handlers (`Commands.cs`) to produce the exhaustive player-action
   list — this is the `features/index.md` backbone.
3. Write the `features/` pages in player voice (one per system), using the handler
   inventory to verify coverage: every player-visible command must map to a doc section.
4. Ship the site live with Phase 1 content before starting Phase 2.

**Phase 2 — Entity stat references (generated):**
5. Build `tools/generate/` (language: Rust to match Zola, or C# to match the server —
   decide at implementation; C# reuses existing DB connection patterns).
6. Generate `content/` tables from the database; each generated page states its source
   table(s) so it can be regenerated; no hand-transcribed IDs.

**Phase 3 — Zone generation patterns:**
7. Write `zones/generation.md` (rules, from generation code + zone files).
8. Pick 2–3 representative zones (low/mid/high), document as worked examples verified
   against zone files.

**Phase 4 — Dev-facing format docs:**
9. Trace each stat field (`growRate`, `fertility`, `spreading`, ...) from DB column →
   zone update code → effect, writing `formats/` with worked numeric examples.
10. Cross-link: every doc links to the content/format docs its numbers come from, and
    vice versa.

Phase 1 deliberately needs no DB access or generator — it is hand-written prose driven
by the handler inventory, so it can ship fastest and validate the Zola pipeline end to end.

## Open Questions

- ~~Generator choice~~ — resolved: Zola, static hosting via GitHub Pages.
- ~~Audience~~ — resolved: Phase 1 is player-facing; Phases 2–4 add developer depth on
  top of it (generated stat tables, field semantics) without rewriting player docs.
- Generator language for `tools/generate/` (Phase 2): Rust (single-binary, matches Zola)
  vs. C# (reuses the server's existing SQL Server connection patterns). Decide at Phase 2.
- How exhaustive should `zones/` be — all zones, or representative examples + an index?
  Recommend index + 3 worked examples initially.
- Player wiki prose should be verified against actual client behaviour (screenshots / in-game
  steps), not just handler code — handlers say what the server accepts, the client says
  how the player gets there. Flag any mismatch.

## Out of Scope

- Protocol packet documentation (belongs in `docs/file_formats/` if ever needed).
- Balance opinions or tuning recommendations — this is a reference, not a design doc.
- Changes to production code or content SQL (documentation only).

## Backlog Note

Suggested backlog entry (for `docs/backlog/improvements.md` when approved):

```md
## IMP-XXX - Game features & content reference wiki

Status: TODO
Priority: MEDIUM
Area: Documentation

### Problem
No player/modder reference for gameplay actions, entity stats, or zone
generation patterns; stat fields (growRate, fertility, spreading) are only
documented at schema level.

### Impact
High onboarding cost for contributors/modders; repeated reverse-engineering
of zone behaviour.

### Proposed Fix
Create `wiki/` as a Zola static site per `wiki/idea.md`, deployed to GitHub Pages:
Phase 1 player wiki (features/), then generated content/ tables, zones/ examples,
formats/ field semantics.

### Notes
Phase 1 is player-facing and needs no DB access — handler inventory drives coverage.
Deploy pipeline validated in Phase 1 before generated content lands.
```

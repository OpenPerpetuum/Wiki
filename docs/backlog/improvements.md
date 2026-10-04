# Wiki backlog

Persistent backlog for the wiki (this repository). Server-side backlog items live in the
PerpetuumServer2 repository; only wiki-scoped items belong here.

Statuses: TODO · IN_PROGRESS · BLOCKED · DONE · DEFERRED
Priorities: CRITICAL · HIGH · MEDIUM · LOW

---

## IMPROVEMENT-034 - Wiki: Publish and Client-UI Verification Pass

Status: DONE
Priority: HIGH
Area: wiki

### Problem
The wiki existed only as local markdown; it needed a static site, a deployment path,
and a verification pass that the player-facing claims match the client.

### What was done
- Zola 0.23 static site (Docker build, `make build` / `make serve` on port 8085).
- CI workflow (`.github/workflows/wiki.yml`) building on `main`.
- Full generated content pipeline (C# generator against the game DB).
- Player-facing presentation pass, client display names, search, themes, sidenav.

### Notes
- GitHub Pages hosting itself is still pending: the repository must be hosted,
  `feature/implementation` merged to `main`, and `base_url` set in `config.toml`.
- See IMPROVEMENT-036 for the community-wiki ingest that followed.

---

## IMPROVEMENT-035 - Wiki: Player-facing Presentation Pass (Raw Backend Terms, Thin Index)

Status: DONE
Priority: HIGH
Area: wiki

### Problem
Generated pages read like a database dump: internal definition names, raw stat keys,
no scale context, thin index, backend terminology in player-facing prose.

### What was done
- Client display names for items/robots/zones (GBF dictionary, later made static —
  see IMPROVEMENT-036 notes for the 2026-07-22 snapshot).
- One page per item (4,514 pages) with category/sub-category index, tier ordering.
- CT capsule production sections + mermaid transport diagrams (finished product on
  the right).
- Shop restructured into per-category pages with per-item "Where to buy"
  (unlimited stock shown as ∞).
- Robots page with class strengths/weaknesses and per-family tables.
- Tech tree: index + 7 category pages with clickable mermaid chain diagrams +
  658 per-node detail pages.
- Stat reference page (`/content/stat-reference/`): units, catalog ranges, chassis
  capacity table, worked powergrid percentage example.
- Engineering notation in all generated numbers (Md.Num).
- Left sidenav (collapsible groups, persisted state), bottom navigation,
  sci-fi dark theme with light toggle, accent palettes, colorblind modes,
  search with keyboard shortcuts.

### Notes
- All items in the original presentation list are complete; this entry is kept
  as the historical record of the pass.

---

## IMPROVEMENT-036 - Wiki: Ingest Community Wiki (perpetuum.miraheze.org) Content

Status: DONE
Priority: HIGH
Area: wiki

### Problem
The OPP community wiki holds the best existing player-facing prose (feature pages,
lore, FAQ). The new wiki needed that content under a permissive license.

### Reuse policy
- **OPP community wiki (perpetuum.miraheze.org)**: under a copyleft
  (share-alike) license. A brief early ingestion was fully **rewritten from
  scratch** (backend-verified or original prose), so no adapted text remains
  in the repository. All art/images from it were **removed**. No further
  ingestion from it.
- **GBF archive**: strings and facts are fine; art assets are NOT extracted
  (publisher copyright + official ToU §3).
- **Vanilla wiki (wiki.perpetuum-online.com)**: no license = all rights reserved;
  not reusable.
- **Official site (perpetuum-online.com)**: ToU §3 forbids copying; not usable.
- **Gameplay screenshots**: permitted with guardrails — capture only, never
  extract; 3D scenes/UI over flat art; no debug views; non-commercial documentation.
- **YouTube embeds**: OK.
- Wiki content is **Apache-2.0** (see `LICENSE/`); the copyleft attribution
  footer was removed once every adapted page had been rewritten (2026).

### Follow-up work (committed on `feature/implementation`, 2026-07-22)
- Collapsible `<details>` sidenav groups (persisted open state, force-open via
  longest-link match); Zone Map as a top-level menu entry; icon visibility fixed
  (CSS alpha-mask placeholder, cyan favicon).
- Engineering notation in all generated tables; vendor qty shows ∞ for unlimited
  stock; mermaid transport diagrams put the finished product on the right.
- Tech tree restructured into an index + 7 category pages + 658 node pages.
- Stat reference page with units and scale context.
- **Static client display names**: `generator/Perpetuum.WikiGenerate/ClientNames.cs`
  snapshots the 4,266 non-derivable client strings, so the 900 MB GBF archive is no
  longer needed at build time (verified byte-identical output before removing the
  parser).
- Stale-page cleanup: the generator now deletes its owned artifacts before writing
  (removed 1,329 ghost item pages that had accumulated since the first implementation).

### Notes
- A3M format must not be mentioned publicly in the wiki.
- "Definition" is not a player-facing term — use display names.
---

## IMPROVEMENT-037 - Category tree reorganisation (structure only)

Status: TODO
Priority: MEDIUM
Area: wiki

### Problem
The prose layer is thin compared to the data layer: combat mechanics, module
families, per-robot detail, corps, zones and lore are each collapsed into a
single list page. The target structure is defined in `docs/category-tree.md`.

### Scope (this entry)
- Structure only: new "How it works" group, "Knowledge" → "World & lore",
  sub-lists (client setup, NPC, corp), per-robot catalog, module family pages,
  stat reference in the menu.
- **No ingestion from the community wiki backup** — all planned pages are to be
  written originally. The backup is used for shape/coverage reference only.

### License (resolved 2026)
- The wiki is now **Apache-2.0** (`LICENSE/`): every formerly adapted feature
  page was rewritten from scratch (backend-verified or original prose), so no
  copyleft-licensed text remains and the attribution footer was removed. Do
  not add share-alike-licensed content — it would break the permissive
  license.

### Notes
- No page moves until planned pages exist (no URL churn, no broken links).
- Per-robot pages and module family pages are generator work; the rest is
  original prose.

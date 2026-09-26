# Wiki Content Generator (Phase 2)

Generates the stat-table pages under `wiki/content/content/` from the Perpetuum
database. The generated Markdown is **committed to git**; the Zola site build never
needs a database.

## What it generates

| Page | Source tables |
|---|---|
| `ores.md` | `minerals`, `mineralconfigs`, `zones` (+ `entitydefaults` for ore item names) |
| `plants.md` | `plantrules` + the plant rule **files** (`$GameRoot/plantrules/*.txt`), `zones` |
| `deployables.md` | `entitydefaults` (deployable category / attribute flag), `aggregatevalues` |
| `items.md` | `entitydefaults` (enabled, non-hidden) grouped by category family, `aggregatevalues` |
| `robots.md` | `entitydefaults` (complete robots), `robottemplates` |
| `extensions.md` | `extensions`, `extensioncategories`, `extensionprerequire` |
| `techtree.md` | `techtree`, `techtreegroups`, `techtreenodeprices`, `techtreetreepointtypes` |
| `missions.md` | `missions`, `missiontypes`, `missionrewards` |
| `shop.md` | `itemshop` |
| `recipes.md` | `components`, `itemresearchlevels` |
| `zone-index.md` (in `zones/`) | `zones`, `mineralconfigs`, `plantrules` |
| `_index.md` | section landing page |

Each generated page carries a header stating its source tables and generation date.

## How to run

From the repository root (this directory's parent):

```bash
make generate \
  WIKI_DB="Server=...;Database=perpetuumsa;User Id=sa;Password=...;TrustServerCertificate=True" \
  WIKI_PLANTRULES=/path/to/GameRoot/plantrules
```

`$PERPETUUM_CONNECTIONSTRING` is used when `WIKI_DB` is not set. Output goes to
`content/content/`; the zone pages (`zone-index.md`, `map.md` — the per-zone
table and the x/y zone map) go to `content/zones/`. Client display names are
read from `../Perpetuum.gbf` (override with `WIKI_GBF=...`).

## Notes & constraints

- **Read-only**: the generator issues SELECTs only.
- **Plant rules are files, not DB rows**: `plantrules.plantrule` is a file name under
  the GameRoot's `plantrules/` directory. The `--plantrules` argument points at that
  directory; `source:` overrides between rule files are resolved (mirrors
  `PlantRuleLoader.cs`).
- **Flags**: category flag masks and attribute flag bit positions are defined in
  `Model.cs` (`Flags`) and verified against `src/Perpetuum.ExportedTypes` and the
  `categoryFlags`/`attributeFlags` tables. If content adds new families, update both.
- **Determinism**: output is sorted (definitions by name, stats by field name); only
  the generation date in headers changes between runs.
- **Scale**: `items.md` is a full catalog (~2–3 MB of Markdown). That is intentional —
  it is the "every item, its stats" reference, and Zola's search is the navigation aid.

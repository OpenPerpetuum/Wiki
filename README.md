# OpenPerpetuum Wiki

The player- and developer-facing wiki for the Open Perpetuum MMO server, built
with [Zola](https://www.getzola.org/).

## Sections

- **Features** — how the gameplay systems work (robots, combat, market, missions, ...)
- **Content** — generated stat catalogs: items (one page per item), ores, plants,
  deployables, robots, extensions, tech tree, missions, recipes, shop
- **Zones** — resource generation mechanics, zone index, worked examples, and the
  zone map (all zones on the x/y grid with their teleport connections)
- **Formats** — file format and field reference (zone files, stat fields)

## Building

The site build needs no database — the generated markdown is committed to git.
All commands run from this directory; the Zola build runs in a Docker container
(pinned `ghcr.io/getzola/zola:v0.23.6`), so no local Zola install is required.

```bash
make build   # build the static site into public/
make serve   # live-reload server on http://localhost:8085 (WIKI_PORT=NNNN to override)
make clean   # remove public/
```

## Regenerating the content pages

Only needed when the game database changes. Requires the .NET 8 SDK, a reachable
game database, and the plant rule files from a GameRoot:

```bash
make generate \
  WIKI_DB="Server=...;Database=perpetuumsa;User Id=sa;Password=...;TrustServerCertificate=True" \
  WIKI_PLANTRULES=/path/to/GameRoot/plantrules
```

Client display names are read from the client string archive at `../Perpetuum.gbf`
(override with `WIKI_GBF=...`). The layout assumption is that this repository
sits as `wiki/` directly inside the PerpetuumServer2 repository.

Commit the regenerated pages — the site build always uses the committed markdown.

See [generator/README.md](generator/README.md) for what each generated page is
built from, and [idea.md](idea.md) for the original design notes.

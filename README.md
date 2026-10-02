# OpenPerpetuum Wiki

The player- and developer-facing wiki for the Open Perpetuum MMO server, built
with [Zola](https://www.getzola.org/).

License: **Apache-2.0** (see `LICENSE/`).

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

Client display names are a static snapshot of the official client's English
string dictionary (`generator/Perpetuum.WikiGenerate/ClientNames.cs`, 4,266
entries) — no client archive is needed. The snapshot predates the end of
official client development, so it does not need refreshing; entities the
client never named fall back to a name derived from the internal identifier.

Commit the regenerated pages — the site build always uses the committed markdown.

See [generator/README.md](generator/README.md) for what each generated page is
built from, and [idea.md](idea.md) for the original design notes.

## Zone map terrain

The zone teleport maps (`static/zonemaps/*.svg`) show each zone's real
terrain. The **layer data is not in this repo** — only the small SVGs are
committed, plus derived 512×512 PNGs for the zones whose layer data is not
fetchable in CI (see below). The PNGs for the other zones are generated at
build time by `tools/gen_zone_teleport_maps.py` (pure standard-library
Python, one process per core) from the game's layer files, sourced in order:

1. a local PerpetuumServer2 checkout (sibling `../PerpetuumServer2` or the
   `server/` submodule) when it has `custom-layers/` — `make zonemaps` uses
   it directly, no download
2. otherwise `tools/fetch_zone_layers.sh` fetches what is publicly
   available into `.assets/` (the same sources the PerpetuumServer2 CI
   uses): the original zones' `.bin` layers from the Dedicated Server
   installer (Steam app 693060, anonymous) and the latest gamma/custom
   zones from the public Google Drive archive

The remaining zones (classic 2048×2048 worlds) ship only in the game
client's `Perpetuum.gbf`, which needs a Steam account — not available to CI
yet. For those, `static/zonemaps-fallback/<zone>/{height,color}.png` holds
previously derived terrain PNGs committed to the repo (~11 MB); the tool
keeps them instead of downgrading to the procedural placeholder. Once a
Steam account can fetch the client data, regenerate locally and delete the
fallback directory.

After re-running `make generate` (new zones or moved teleports), re-run
`make zonemaps` to refresh the map backgrounds and links.

## CI and action pinning

GitHub Actions in `.github/workflows/` are pinned to commit SHAs with
[ratchet](https://github.com/sethvargo/ratchet) (the `# ratchet:...` comment
records the original tag constraint). CI lints the pins on every run; to
refresh them to the latest matching tags:

```bash
docker run --rm -v "${PWD}:${PWD}" -w "${PWD}" ghcr.io/sethvargo/ratchet:latest update .github/workflows/wiki.yml
```

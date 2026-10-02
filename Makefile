# Open Perpetuum Wiki — build tooling.
#
#   make build     build the static site into public/ (Docker, pinned Zola image)
#   make serve     local live-reload server on :8085 (Docker; override WIKI_PORT=NNNN)
#   make test      build + run the Playwright browser tests (needs node +
#                  npm install; CHROMIUM_PATH=... to skip the Playwright
#                  Chromium download)
#   make zonemaps  regenerate the zone teleport map SVGs (heightmap +
#                  clickable teleports; pure-stdlib Python in a container —
#                  no database needed)
#   make generate  regenerate the generated content pages from the database
#   make clean     remove the build output
#
# The site build (build/serve) never needs a database: the generated markdown is
# committed to git. `make generate` is only needed when the database content
# changes; it requires the .NET 8 SDK and a reachable game database.

ZOLA_IMAGE := ghcr.io/getzola/zola:v0.23.6
PYTHON_IMAGE := python:3.12-slim
WIKI_PORT  ?= 8085

# Generator inputs (make generate).
#   WIKI_DB          SQL Server connection string (falls back to $PERPETUUM_CONNECTIONSTRING)
#   WIKI_PLANTRULES  directory containing the plant rule files; defaults to the
#                    server submodule (run `git submodule update --init server` first)
WIKI_DB         ?= $(PERPETUUM_CONNECTIONSTRING)
# ?= would not override an empty WIKI_PLANTRULES from the environment, so use ifdef.
ifdef WIKI_PLANTRULES
else
WIKI_PLANTRULES = $(abspath server/src/Perpetuum.ServerService2/data/plantrules)
endif
DOTNET          ?= $(shell command -v dotnet 2>/dev/null || echo $(HOME)/.dotnet/dotnet)
TTY_FLAG        := $(shell [ -t 0 ] && echo -it || echo -i)

.PHONY: all build serve test zonemaps generate clean

all: build

# Post-process the generated zone teleport maps (static/zonemaps/*.svg):
# REAL terrain backgrounds from the game's layer files (custom-layers/*.bin
# or the .gbf archives in the sibling PerpetuumServer2 checkout — see the
# script header), emphasized zone border, and clickable teleport links.
# Pure standard-library Python — no database, no pip installs.
zonemaps:
	docker run --rm -v "$PWD:/src" -w /src \
		-v "$(shell cd .. && pwd)/PerpetuumServer2:/assets:ro" \
		-e OP_ASSETS_DIR=/assets $(PYTHON_IMAGE) python3 tools/gen_zone_teleport_maps.py

# Browser tests (tests/): mermaid syntax of every diagram + sidenav behaviour,
# run against the built site. One-time setup: npm install.
test: build
	npm test

# Build the static site with the pinned official Zola container (no local
# install). The container runs as root (the image has no alpine coreutils),
# so if a build is ever interrupted and leaves root-owned files in public/,
# `make clean` removes them via a container.
build:
	docker run --rm \
		-v "$$PWD:/src" \
		$(ZOLA_IMAGE) -r /src build --force -o /src/public

# Local live-reload server: http://localhost:$(WIKI_PORT)
serve:
	docker run --rm $(TTY_FLAG) \
		-u "$$(id -u):$$(id -g)" \
		-p "$(WIKI_PORT):1111" \
		-v "$$PWD:/src" \
		$(ZOLA_IMAGE) -r /src serve -i 0.0.0.0 --no-port-append -u "http://localhost:$(WIKI_PORT)/"

# Regenerate the generated pages (content/content + zone index + zone map +
# search index) from the database. See generator/README.md for the table
# mapping. (After a generate, re-run `make zonemaps` to refresh the teleport
# map backgrounds/links.)
generate:
	@test -n "$(WIKI_DB)" || { echo "error: set WIKI_DB (or PERPETUUM_CONNECTIONSTRING)"; exit 1; }
	@test -d "$(WIKI_PLANTRULES)" || { echo "error: WIKI_PLANTRULES is not a directory: $(WIKI_PLANTRULES)"; \
	  echo "hint: run 'git submodule update --init server' (or set WIKI_PLANTRULES to a GameRoot plantrules dir)"; exit 1; }
	$(DOTNET) build -c Release generator/Perpetuum.WikiGenerate/Perpetuum.WikiGenerate.csproj
	$(DOTNET) run -c Release --no-build --project generator/Perpetuum.WikiGenerate -- \
		--connection "$(WIKI_DB)" \
		--plantrules "$(WIKI_PLANTRULES)" \
		--out content/content \
		--zones-out content/zones

clean:
	rm -rf public 2>/dev/null || true
	if [ -d public ]; then docker run --rm -v "$$PWD/public:/out" alpine:3.20 sh -c "rm -rf /out/*"; rm -rf public; fi

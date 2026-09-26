# Open Perpetuum Wiki — build tooling.
#
#   make build     build the static site into public/ (Docker, pinned Zola image)
#   make serve     local live-reload server on :8085 (Docker; override WIKI_PORT=NNNN)
#   make generate  regenerate the generated content pages from the database
#   make clean     remove the build output
#
# The site build (build/serve) never needs a database: the generated markdown is
# committed to git. `make generate` is only needed when the database content
# changes; it requires the .NET 8 SDK and a reachable game database.
#
# Layout assumption: this repository is checked out as `wiki/` directly inside
# the PerpetuumServer2 repository, so the client string archive is found at
# ../Perpetuum.gbf (override with WIKI_GBF=...).

ZOLA_IMAGE := ghcr.io/getzola/zola:v0.23.6
WIKI_PORT  ?= 8085

# Generator inputs (make generate).
#   WIKI_DB          SQL Server connection string (falls back to $PERPETUUM_CONNECTIONSTRING)
#   WIKI_PLANTRULES  directory containing the plant rule files ($GameRoot/plantrules)
#   WIKI_GBF         client string archive (default: ../Perpetuum.gbf)
WIKI_DB         ?= $(PERPETUUM_CONNECTIONSTRING)
WIKI_PLANTRULES ?=
WIKI_GBF        ?= ../Perpetuum.gbf
DOTNET          ?= $(shell command -v dotnet 2>/dev/null || echo $(HOME)/.dotnet/dotnet)
TTY_FLAG        := $(shell [ -t 0 ] && echo -it || echo -i)

.PHONY: all build serve generate clean

all: build

# Build the static site with the pinned official Zola container (no local install).
build:
	docker run --rm \
		-v "$$PWD:/src" \
		$(ZOLA_IMAGE) -r /src build --force -o /src/public

# Local live-reload server: http://localhost:$(WIKI_PORT)
serve:
	docker run --rm $(TTY_FLAG) \
		-p "$(WIKI_PORT):1111" \
		-v "$$PWD:/src" \
		$(ZOLA_IMAGE) -r /src serve -i 0.0.0.0 --no-port-append -u "http://localhost:$(WIKI_PORT)/"

# Regenerate the generated pages (content/content + zone index + zone map +
# search index) from the database. See generator/README.md for the table mapping.
generate:
	@test -n "$(WIKI_DB)" || { echo "error: set WIKI_DB (or PERPETUUM_CONNECTIONSTRING)"; exit 1; }
	@test -n "$(WIKI_PLANTRULES)" || { echo "error: set WIKI_PLANTRULES to the GameRoot plantrules dir"; exit 1; }
	@test -d "$(WIKI_PLANTRULES)" || { echo "error: WIKI_PLANTRULES is not a directory: $(WIKI_PLANTRULES)"; exit 1; }
	$(DOTNET) build -c Release generator/Perpetuum.WikiGenerate/Perpetuum.WikiGenerate.csproj
	$(DOTNET) run -c Release --no-build --project generator/Perpetuum.WikiGenerate -- \
		--connection "$(WIKI_DB)" \
		--plantrules "$(WIKI_PLANTRULES)" \
		--gbf "$(WIKI_GBF)" \
		--out content/content \
		--zones-out content/zones

clean:
	rm -rf public

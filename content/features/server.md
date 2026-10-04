---
title: "Server & reference"
description: "Server info, high scores, reference data downloads, and the in-game store."
weight: 170
---

# Server & reference

This page covers the "meta" systems: server/system info, high scores, the reference data
the client downloads, and the in-game store.

> UI locations follow the [client UI overview](/features/ui/). Mechanics below are confirmed against
> the server.

## Server & system info

- **Server info** — general server state/info.
- **System info** — system-level details.
- **Server shutdown state** — whether a scheduled shutdown is in effect (read-only for
  players; the shutdown itself is an admin action).

## High scores

- **Server high scores** — the top results across the server.
- **My high scores** — your personal bests.

See also [combat](/features/combat/) for how kills feed these.

## Reference data

The client downloads static reference data from the server to render content correctly.
These are **read-only lookups**, not player actions:

- **Effects** — the effect definitions.
- **Enums** — enumeration values.
- **Entity defaults** — base definitions for entities.
- **Aggregate fields** — aggregated field data.
- **Definition config units** — per-definition configuration.
- **Distances** — distance constants used by the client.
- **Commands** — the list of known commands (a client-side reference).
- **Rifts** — rift definitions (see also the zone rift list).

You don't "use" these; the client fetches them on demand. They're listed here because
they appear in the command inventory.

## The store

The in-game store (a payment integration):

- **List products** — see what's for sale.
- **Start a transaction** — begin a purchase.
- **Finish a transaction** — complete a purchase.

Store purchases are real-money (or equivalent) transactions; the exact products and
pricing are store content.

## Practical notes

- **Reference data is auto-fetched** — you never trigger it manually; it keeps the client
  in sync with the server's content.
- **High scores are your bragging rights** — check your personal bests and the server's.
- **Store transactions are two-step** — start, then finish; don't assume a purchase is
  complete until it's finished.
- **Shutdown state is informational** — it tells you if a maintenance window is coming.

<!-- TODO: only the reference-data payload list (client-side) remains.
     Confirmed: the real-money store is NOT active on this deployment — the Steam
     transaction commands (steamGetProducts / steamStartTransaction /
     steamFinishTransaction) are declared in Commands.cs but no handler is registered
     in the bootstrapper, and the store content tables (storeitems, storecategories)
     are empty.
     Developer notes: ServerInfoGet.cs / SystemInfo.cs / ServerShutDownState.cs;
     GetHighScores.cs / GetMyHighScores.cs; GetEffects.cs / GetEnums.cs /
     GetEntityDefaults.cs / GetAggregateFields.cs / GetDefinitionConfigUnits.cs /
     GetDistances.cs / GetCommands.cs / GetRifts.cs; SteamGetProducts.cs /
     SteamStartTransaction.cs / SteamFinishTransaction.cs. -->

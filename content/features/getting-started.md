---
title: "Getting started"
description: "Creating a character, receiving your starter robot, and character basics."
weight: 10
---

# Getting started

This page covers the first steps: creating a character, getting your first robot,
and the character-level settings you'll touch early on.

> UI locations follow the [client UI overview](/features/ui/). Mechanics below are confirmed
> against the server implementation.

## Creating a character

- You can **check whether a name is free** before creating (`characterCheckNick`).
- **Create the character** — it is attached to your account. An account can hold several
  characters; you play one at a time.
- **Select a character** to play it. Switching characters deselects the current one.
- You can **rename** a character later (name must be available).
- You can **delete** a character you no longer use.

## Your first robot

- While **docked at a base**, you can **request the starter robot** (base facilities —
  see the [client UI overview](/features/ui/) for where base actions live).
  - The base must still have starter robots in stock — at popular bases this can run out,
    in which case the request fails.
  - There is a **5-minute cooldown** between starter-robot requests per character.
  - The new robot is delivered to the base's public container.

- While docked you can also **request an "infinite box"**: a special container added to
  your inventory for a **5,000 credit fee**. This is a convenience container, not a
  source of free resources.

## Character settings

- **Avatar** — how your character looks.
- **Mood message** — a short line shown to other players.
- **Private note** — a note only you see on one of your own characters.
- **Home base** — set or clear your home base; it is your default dock when the game
  needs to put you somewhere safe.
- **Settings** — per-character preferences (get/set).
- **Block trades** — turn trading on or off for a character.
- **Credits** — transfer credits between your own characters; view the transaction
  history.
- **Account level** — change session e-mail/password, view the account's transaction
  history and the EP-for-activity history.

## Leaving / re-entering

- **Sign out** ends the session; **select character** starts it again.
- If the server kicks you (e.g. forced to base), you reappear docked — see [movement](/features/movement/).

## What to do next

1. Select your starter robot and learn the [hangar & fitting](/features/robots/) screen.
2. Undock and fly into the zone — see [movement](/features/movement/).
3. Scan the ground and start collecting resources — see [gathering](/features/gathering/).
4. Research your first extensions — see [research](/features/research/).

<!-- TODO(Phase 1): verify client UI flow (account creation screen, character wizard).
     UI model: see features/ui.md (EVE-style, project-provided).
     Developer notes: Perpetuum.RequestHandlers.Characters/* (CharacterCreate,
     CharacterSelect, CharacterRename, CharacterSetHomeBase, ...);
     RequestStarterRobot (5-minute cooldown, base stock), RequestInfiniteBox
     (5000 credits, docked-only). -->

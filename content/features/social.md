---
title: "Social"
description: "Communication and reputation: mail, chat channels, friends, standings, the yellow pages, news, and polls."
weight: 140
---

# Social

This page covers communication and reputation: **mail**, **chat channels**, **friends**,
**standings**, and the public **yellow pages / news / polls**.

> UI locations follow the [client UI overview](/features/ui/) (the **Mail** button top-left; chat and
> social have their own windows). Mechanics below are confirmed against the server.

## Mail

**Personal mail:**
- **List** mail, **open** a message, **send** mail, **delete** a message.
- **New count** — how many unread messages you have.
- **Folders** — view **used folders**, **move** a message to a folder, **delete** a folder.

**Mass mail (corporation):** sent to your whole corp:
- **Send**, **list**, **open**, **delete**, **new count**.

## Chat channels

**Channels** are named chat rooms:

- **Create** a channel, **join** one, **leave** one, **talk** in it.
- **List** channels (all, or just yours).
- **Moderation** (as a channel owner/officer): **kick**, **ban** / **unban**, **set a
  member's role**, **global mute**, view **muted** / **banned** members.
- **Settings**: **set a password**, **set a topic**, **notification** preferences.

**Direct chat** — a private message between characters.

## Friends

- **Send a friend request**, **reply** to one, **confirm** a pending request.
- **Delete** a friend, **block** a character.
- **List** your friends.

Blocking a character stops them from trading/messaging you (see
[getting started](/features/getting-started/) for the per-character trade block).

## Standings (reputation)

**Standing** is a numeric reputation between entities (characters, corporations,
alliances):

- **List** your standings, view **standing history**.
- **Set standing** — where permitted, set a standing value. Constraints:
  - the **target** must be a character, a (private) corporation, or a (private) alliance,
  - the **source** must likewise be a valid entity,
  - you can't set a standing on yourself.
- **Default standings** — the baseline standings for default alliances/corporations.

Standing is a value from **-10 to +10** (0 = neutral, the default; setting 0 clears the
entry). **Reputation** for an entity is the aggregate of everyone's standing toward it.
Positive standing matters in concrete places — e.g. during [intrusion](/features/intrusion/)
a corporation with mutual standing ≥ 10 counts as an **ally** whose SAP win doesn't
damage your site.

Standing affects how factions and other players treat you and your corporation
(see [groups](/features/groups/)).

## Yellow pages

A public directory (corporation-run listings):
- **Get** a listing, **submit** one, **delete** one, **search** the directory.

## News & polls

- **News** — view **categories** and the **news list**; **new count** for unread items.
- **Polls** — **view** a poll and **answer** it.

## Practical notes

- **Mail vs. chat** — mail is persistent and private; channels are live group rooms.
- **Mass mail is a corp tool** — officers use it to broadcast to members.
- **Standings are directional** — A's standing with B is separate from B's with A, and
  it follows your corporation.
- **Blocking is your shield** — block spammers or hostile traders.
- **Channels can be locked** — passwords and roles gate who can join/talk.

<!-- TODO: only the mail/channel/social UI remains.
     Confirmed: standing range [-10,10], clamp in StandingHandler.SetStanding, 0 =
     neutral/clears the row (standings table empty on this deployment); reputation =
     aggregate toward a target (GetReputationFor); mission standing changes clamp to
     [0,10] (missionstandingchange/missionrequiredstanding); ally check = mutual
     standing >= 10 (intrusion).
     Developer notes: Mails/* (list/open/send/delete/folders, mass-mail variants);
     Channels/* (create/join/leave/talk, moderation, password/topic); Socials/* (friend
     request/reply/confirm/delete/block/list); Chat.cs; Standings/SetStanding.cs
     (entity-category + private-corp/alliance constraints, self-excluded),
     StandingHistory.cs, GetStandingForDefault*.cs; Corporations/YellowPages/*;
     GetNews.cs / NewsCategory.cs / FreshNewsCount.cs; PollGet.cs / PollAnswer.cs. -->

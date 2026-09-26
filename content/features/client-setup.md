---
title: "Client & PC setup"
description: "Running the client on Windows and Linux, multi-boxing within the rules, UI scaling, and launching without admin rights."
weight: 24
---

# Client & PC setup

Practical notes for running the Perpetuum client.

## The rules first

Multi-boxing (several clients controlling several characters) is allowed
**within limits**. Fair-play rules to know before you do it:

- **No automation or multi-client control software** — ever.
- **No more than 5 simultaneously active accounts.**
- No accounts registered with fake, invalid or unreachable email addresses —
  the address must be one you own.

Violations can mean an immediate, permanent ban.

## Windows: multiple clients

1. Install the Perpetuum Steam client.
2. Check that the game folder contains a `steam_appid.txt` whose content is
   `223410`.
3. Copy the entire `Perpetuum` folder once per additional client.
4. Run `Perpetuum.exe` in each copy (optionally add the copies to Steam as
   non-Steam games).

**Disk space:** `Layers.gbf` and `Perpetuum.gbf` are large and identical in
every copy — make them symbolic links (`mklink`) or shortcuts so they are
stored once.

## Linux: multiple clients

Each client copy needs a **different Proton version**, or it refuses to run:

1. Install Steam and Perpetuum as usual; verify `steam_appid.txt` (`223410`).
2. Copy the `Perpetuum` folder per client.
3. While logged into Steam, install several recent Proton versions (via Steam,
   by hand, or GloriousEggroll builds).
4. Add each copy to Steam as a non-Steam game pointing at its `Perpetuum.exe`.
5. Force a **different Proton version** per copy in its launch options.
6. Start the real Steam app first, then launch the copies.

Troubleshooting:

- Selecting a Proton version does **not** install it — you have to launch the
  game once with it selected.
- If the real Steam app won't start, start it in Safe mode first.
- Since Proton 5.0, DirectX 9/12 emulation runs on **Vulkan**. No
  Vulkan-capable GPU? Add `PROTON_USE_WINED3D=1 %command%` to each copy's
  launch options.
- Empty text box with a red warning: install `libfreetype6:i386`
  (Debian/Ubuntu-based distros).

## UI scaling

The client uses a fixed-pixel UI with no dynamic scaling. Workarounds:

- **Low-resolution fullscreen** — go fullscreen at a lower resolution than your
  display and let the UI stretch to fill it (windowed and borderless modes
  always use the native resolution). Lowering the display's own resolution does
  the same.
- **UI scaler apps** — e.g. Lossless Scaler (Steam, Windows): run the client
  windowed at your base size, set a custom scale ratio in the scaler, trigger
  the scaling and focus the client window. Expect the usual smoothness-vs-text
  crispness trade-off.
- **OS magnifier** — the Windows Magnifier (or equivalent) for the occasional
  close read.

## Launching without admin rights

1. Copy the client files out of the Steam folder to a writable location.
2. Create a `Perpetuum.bat` there containing
   `cmd /min /C "set __COMPAT_LAYER=RUNASINVOKER && start Perpetuum.exe"`.
3. Launch through the batch file.

<!--
Developer notes: rewritten 2026 in original prose. An earlier version was adapted from the
Open Perpetuum community wiki (perpetuum.miraheze.org: Multi-Boxing, Linux
Setup, Client UI Scaling, Launching without Admin Rights); that text was
fully replaced, not reused. These are client-side procedures for the retail Steam client build;
nothing here is verifiable from this server's backend, and no server feature is
claimed. The Reshader (graphics mod) was intentionally left out: it targets the
retail client build and is not an OPP-server feature.
-->

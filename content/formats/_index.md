---
title: "Formats"
description: "Developer reference: how every stat field is stored, parsed, and used by the server."
weight: 60
---

# Formats

Developer-facing field reference. Each page traces a stat field from its storage
(DB column, rule-file key, or binary layer byte) through the code that consumes it,
with worked numeric examples. Player-facing "what does it mean in game" summaries live
in the [features](/features/) and [content](/content/) sections — this is where the
exact types, defaults, formulas, and code paths are.

| Page | Contents |
|---|---|
| [Plant fields](/formats/plant-fields/) | Plant rule file format (GenXY), every rule field with type/default/consumer, the growth state machine, validation checks |
| [Ore fields](/formats/ore-fields/) | `minerals` + `mineralconfigs` columns, the node-generation formulas, node lifecycle and persistence, extraction mechanics |
| [Deployable fields](/formats/deployable-fields/) | How deployables are identified, capsule↔object pairing, and the aggregate fields that drive placed objects |
| [Zone files](/formats/zone-files/) | The binary terrain layer files: struct layouts, flag bits, save cycle, and what lives in the DB instead |

Conventions:

- **GenXY** is the settings serialization used for rule files and `entitydefaults.options`
  (token characters per `GenxyToken.cs`; see [Plant fields → file format](/formats/plant-fields/)).
- Code paths are relative to `src/`.
- Where a value is a **constant** (not data), the file and constant name are given.

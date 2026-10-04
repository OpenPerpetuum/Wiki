---
title: "Deployable fields"
description: "How deployables are identified, capsule-object pairing, and the aggregate fields that drive placed zone objects."
weight: 40
---

# Deployable fields

A **deployable** is an item that can be placed into a zone as a unit (mobile teleports,
landmines, field effect emitters, PBS structures, …). It is identified by flags,
paired with a capsule↔object definition, and driven at runtime by aggregate fields.

## Identification

- **Category flag** `cf_deployable_structure` = `0x998` (bitmask) on
  `entitydefaults.categoryflags` — the set the entity registry scans to register
  deployer item classes (`EntitiesModule.cs`, e.g. `PlantSeedDeployer` is registered
  for this category).
- **Attribute flag** `deployable` = bit **23** on `entitydefaults.attributeflags`
  (mask `1 << 23`).
- The full catalog (with stats) is generated at [Deployables](/content/deployables/).

## Capsule ↔ object pairing

Deployables come in pairs: a **capsule** (the item you carry in inventory) and the
**object** (the unit that appears in the zone). The pair is the DB column
`entitydefaults.targetdefinition`, which is **bidirectional**:

- capsule.`targetdefinition` → the object's definition
- object.`targetdefinition` → the capsule's definition

`PBSHelper.GetPBSObjectDefinitionFromCapsule` / `ItemDeployerHelper` resolve either
direction. Example pair: `def_mobile_field_eccm_capsule` ⇄ `def_mobile_field_eccm`.

## Deployment flow

`ItemDeployer.cs` (base for non-PBS deployables):

1. `Deploy(zone, player)` computes the spawn position at the player's center.
2. `CreateDeployableItem` builds the **object** unit (not the capsule) and saves it.
3. A 5-second "deploy beam" is rendered.
4. On transaction commit the object is added to the zone (`ZoneEnterType.Deploy`).

PBS structures use the separate `PBSDeployer` / `PBSObjectHelper` path; when a PBS
object is deconstructed it is removed from the zone and its **capsule** is added to
loot (`PBSObjectHelper.cs`).

## Runtime aggregate fields

These fields (from `aggregatevalues`, per definition) drive a placed object once it is
in the zone. The consumer is given.

| Field | Meaning | Consumer |
|---|---|---|
| `despawn_time` | Lifetime of the placed object, **milliseconds**. On expiry the unit is removed from the zone (or killed, if remote-controlled). | `UnitDespawnHelper.Create(unit, TimeSpan.FromMilliseconds(despawn_time))` → applies `effect_despawn_timer`. |
| `armor_max` | Max armor of the placed unit. | unit combat |
| `resist_chemical/explosive/kinetic/thermal` | Damage resistances. | unit combat |
| `signature_radius` | Scan signature size. | scanner / stealth |
| `stealth_strength` | Stealth strength. | scanner / stealth |
| `effect_*` (e.g. `effect_field_sensor_strength_modifier`, `effect_stealth_strength_modifier`, `effect_field_reactor_radiation_modifier`) | Field-effect modifiers emitted by the object. | `FieldEffectGenerators/*` (e.g. `FieldEccmEffectGeneratorDeployer` reads `despawn_time` and the effect fields via `GetPropertyModifier`). |
| `mobile_teleport_cooldown` | Cooldown (ms) between uses of a mobile teleport. | mobile teleport unit |
| `trigger_mass` | Mass threshold that arms a landmine trigger. | landmine unit |
| `damage_kinetic/thermal/explosive/chemical/toxic` | Damage a landmine deals per attribute. | landmine unit |

`despawn_time` is the defining field: every placed object gets a
`UnitDespawnHelper` whose `IntervalTimer` (650 ms tick) re-checks the
`effect_despawn_timer` effect and, when it is gone, runs the despawn strategy.

### Worked example — `def_mobile_field_eccm`

From [Deployables](/content/deployables/):

```
armor_max=15000
resist_chemical=150  resist_explosive=150  resist_kinetic=150  resist_thermal=150
signature_radius=20
stealth_strength=80  (object) / 50 (armored capsule)
despawn_time=900000          → 900 s = 15 min lifetime
effect_field_sensor_strength_modifier=100
effect_field_reactor_radiation_modifier=0.75
```

Deploying the capsule spawns the object at the robot's position; it lives 15 minutes,
has 15,000 armor with 150% resist, and while alive it emits a field that boosts sensor
strength by 100% and reduces reactor radiation by 25%.

<!-- Developer notes: EntitiesModule.cs:449 (ByCategoryFlags<PlantSeedDeployer>
     cf_deployable_structure); ExportedTypes/CategoryFlags.cs (cf_deployable_structure
     = 0x998), ExportedTypes/AttributeFlags.cs (deployable = 23);
     EntityFramework/DefinitionConfig.cs (targetdefinition column, TargetEntityDefault);
     Deployers/ItemDeployer.cs (Deploy flow, 5s beam, ZoneEnterType.Deploy);
     Zones/PBS/PBSHelper.cs (capsule<->object), Zones/PBS/PBSObjectHelper.cs
     (deconstruct -> capsule loot); Units/UnitDespawnHelper.cs (despawn_time ->
     effect_despawn_timer, 650ms tick, CancellableDespawnHelper for strongholds);
     Zones/FieldEffectGenerators/FieldEccmEffectGeneratorDeployer.cs
     (GetPropertyModifier(AggregateField.despawn_time) + effect_* fields). -->

# Card Timeline Authoring

## Purpose

Every completed card animation owns a unique `animation_id` and timeline. Runtime combat values may enrich that timeline, but may never select a different card animation.

```text
card_id -> animation_id -> variant -> timeline events -> original impact gates
```

## Supported event types

| Type | Purpose |
|---|---|
| `pose` | Tween the character rig to a named pose |
| `eye` | Enable or disable the Sharingan indicator |
| `vfx` | Spawn a local-only procedural effect at a public rig anchor |
| `camera` | Apply a local flash, dim, or shake effect |
| `cutin` | Play an optional local-only side Cut-in |
| `impact` | Pause for the authoritative original game impact event |
| `return_idle` | Return to the standard sword-ready idle |

## Runtime parameter bindings

Only these already-calculated, read-only values may be bound into VFX or camera parameters:

- `final_damage`
- `hit_count`
- `target_count`
- `energy_spent`
- `strength`
- `exhausted_card_count`
- `lethal`

A binding has the following form:

```json
{
  "params": {"branches": 7},
  "param_bindings": {
    "branches": {
      "source": "strength",
      "scale": 0.4,
      "offset": 7.0,
      "min": 7,
      "max": 13,
      "integer": true
    }
  }
}
```

The resolved value is:

```text
clamp(source * scale + offset, min, max)
```

Bindings are allowed only on `vfx` and `camera` events. They cannot change:

- `animation_id`;
- event type or event ordering;
- animation duration;
- number of authoritative impact gates;
- gameplay damage, Block, status, targets, energy, cards, or random state.

## Current card-local bindings

### Cleave

`target_count` controls only:

- the number of visible target impact rings;
- the branch density of the ground Chidori fan.

The Cleave animation remains `cleave_chidori_ground_arc` for every target count and damage value.

### Heavy Blade

`strength` controls only:

- blade-charge spark count;
- vertical slash width;
- lightning-pillar branch count;
- local camera-shake amplitude within a capped range.

The Heavy Blade animation remains `heavy_blade_lightning_execution` at every Strength value.

### Future mappings

- Whirlwind: `energy_spent` controls its own sword-dance loop presentation;
- Fiend Fire: `exhausted_card_count` controls its own card afterimages and flame rhythm;
- multi-hit cards: `hit_count` controls only impact gates and card-specific loop segments.

## Impact rule

An `impact` event is a gate, not a damage command. In game-integrated mode the Director pauses at the gate until the adapter forwards the corresponding original game impact index.

If the original impact does not arrive before the configured timeout, the custom timeline fails and the original visual path remains authoritative.

## Accessibility

Every damage-card timeline must provide `low_flash`. Cut-ins and high-opacity flashes should normally set `suppress_in_low_flash: true`. Low-flash mode may reduce particles and shake, but must not change the card animation identity or impact timing.

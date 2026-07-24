# Persistent visual state lifecycle

This document describes local-only visual states created by card timelines. These states never represent or modify gameplay data.

## Event model

Timelines may use:

- `state_install`: create a pending state owned by the current playback generation;
- `state_pulse`: animate an existing state without changing game state;
- `state_clear`: explicitly release one state;
- `release_combat_resources`: cancel transient playback and clear every committed state.

A state installed by a timeline remains pending until that timeline completes. Completion commits the state. Cancellation, missing assets or an original-impact timeout roll back pending states from that generation.

Committed states survive later card animations. They are removed only by an explicit visual-state clear, a verified game-state removal hook, or combat resource release.

## Current states

### Flame Barrier

`flame_barrier_guard` is installed by `flame_barrier_uchiha_fire_guard` and remains visible after card playback. A future verified reactive-damage hook may call `PulseVisualState` when the original game reports a Flame Barrier reaction. The pulse is visual only.

### Burning Pact

`burning_pact_channel` exists only during the card animation. The timeline installs, pulses and clears it before completion. It must never remain after the card finishes.

### Limit Break

`limit_break_overdrive` is committed after the original Limit Break impact. It is a subtle Sharingan/curse-mark aura and may be refreshed by later verified strength-state events. It does not store or calculate Strength.

## C# bridge

`GodotVisualSceneHost` exposes:

```text
PulseVisualState(stateId, parameters)
ClearVisualState(stateId)
ReleaseCombatResources()
```

Only verified game visual hooks may call these methods. Parameters are presentation values and must not be written back into gameplay state.

## Tests

Open and run in MegaDot:

```text
SasukeIronclad/scenes/runtime/combat_resource_release_test.tscn
```

The test verifies:

1. Flame Barrier commits one persistent state;
2. a later Strike does not clear it;
3. explicit pulse and clear work;
4. cancelling Limit Break after state installation rolls the pending state back;
5. completing Limit Break commits its state;
6. Burning Pact clears its temporary channel without clearing Limit Break;
7. combat release leaves zero state, VFX and Cut-in nodes.

Success output:

```text
RELEASE_OK states=0 transients=0
```

The hosted GitHub workflow validates the data and source contracts but does not execute MegaDot scenes because the runner does not contain MegaDot, BaseLib or the game assemblies.

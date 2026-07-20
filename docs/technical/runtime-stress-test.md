# Runtime stress test

## Purpose

`runtime_stress_test.tscn` exercises the local-only animation runtime without executing any gameplay logic. It is intended to catch temporary-node leaks, Cut-in cleanup failures and cancellation-state bugs before game hooks are enabled.

## Scene

```text
SasukeIronclad/scenes/runtime/runtime_stress_test.tscn
```

The scene performs 100 iterations across the currently registered graybox timelines. Every third iteration is cancelled shortly after playback begins; the remaining iterations run to completion in the fast variant.

## Covered timelines

- Strike
- Defend
- Bash
- Anger
- Cleave
- Thunderclap
- Heavy Blade
- Whirlwind
- Fiend Fire

Whirlwind and Fiend Fire use four local preview impacts so the repeated-impact cleanup path is exercised.

## What is measured

After every iteration, the harness waits for queued frees and short-lived tweens, then checks:

- no node named `VFX_*` remains;
- no node named `AdvancedVFX_*` remains;
- the Cut-in root is hidden;
- the runtime node count has not grown beyond a two-node scheduling allowance;
- completed timelines return success;
- cancelled timelines return to the sword-ready idle state through `cancel_current()`.

A successful run prints:

```text
STRESS_OK iterations=100 baseline_nodes=<n> peak_nodes=<n>
```

A failed run exits with code 1 and prints one or more `STRESS_FAILED` diagnostics.

## Running in MegaDot

Open the project with the configured MegaDot 4.5.1 Mono executable, set the stress scene as the current scene, and run it. For command-line or headless validation, use the MegaDot executable configured in `Directory.Build.props` and pass the project path plus the stress scene using the command-line options supported by that MegaDot build.

The exact command is intentionally not hard-coded because the executable name and supported headless flags differ between local MegaDot distributions. Record the verified invocation in `docs/technical/local-asset-audit.md` once the development machine is available.

## CI boundary

GitHub Actions currently performs static contract checks only:

- the stress scene and script must exist;
- the iteration count must remain 100;
- both completion and cancellation paths must be present;
- cleanup methods must clear base VFX, advanced VFX, camera state and Cut-ins;
- transient-node counting and success/failure exit paths must remain present.

The hosted CI runner does not currently have MegaDot, the game assemblies or BaseLib, so it cannot execute the Godot scene. A green GitHub Actions result does not replace the local MegaDot stress run.

## Release gate

Before a playable build is published:

1. run the 100-iteration scene in normal mode;
2. run it again with low-flash enabled for all timelines;
3. repeat with the maximum tested Whirlwind and Fiend Fire impact counts;
4. capture the final `STRESS_OK` output and development environment versions;
5. verify that the game-integrated scene also releases all resources when combat ends.

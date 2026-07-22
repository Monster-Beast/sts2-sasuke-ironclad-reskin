# Combined title and animation presentation canary

This stage verifies the two reviewed local presentation layers in the same exact-build game process:

- display-only Sasuke card titles on the six reviewed UI surfaces;
- anchored Sasuke card animation timelines with optional fail-safe replacement of the local Ironclad visual.

It remains an explicit canary for `public-beta` build `24251656`. It does not promote the production integration contract or enable the blocked `form_removed` and `character_state` bindings.

## Enable

Build and install the Mod, disable runtime observation, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -Combined `
  -SessionLabel "combined-run-1" `
  -ReplaceOriginal
```

`-Combined` is explicit documentation of the test intent. The marker enables both reviewed layers, preserves the approved local calibration (`1.2 / 0 / -150`) and writes the exact-build replacement acknowledgement.

The session label is restricted to 1-48 ASCII letters, digits, dots, underscores and hyphens and must start with a letter or digit. The runtime gate independently validates it so a manually edited marker cannot inject an unsafe file name.

## Event journal

Each successful canary startup creates a JSONL journal under:

```text
mods/SasukeIronclad/canary-output/
```

The startup status exposes only its file name and generated session ID. The journal is bounded to 20,000 events and records presentation identity and transitions only. It does not serialize:

- game objects or callback argument graphs;
- absolute paths;
- card rules text, costs, damage, block or energy;
- player names, account identifiers or multiplayer network data.

Representative event types are:

```text
session_start
combat_started
card_play_requested
anchor_bound
playback_started
replacement_hidden
original_impact_forwarded
title_applied
playback_completed
unreviewed_card_fallback
replacement_restored
combat_ended
session_stop
```

Unexpected adapter, anchor or playback failures are recorded separately and still invoke the existing original-visual fallback path.

## Deterministic checkpoints

Use the checked-in checkpoint command instead of a handwritten PowerShell function:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary-checkpoint.ps1 `
  -GamePath "$gamePath" `
  -Name "01-combined-active" `
  -OutputDirectory ".\runtime-canary-checkpoints"
```

Each checkpoint contains:

```text
checkpoint.json
process.json
status.txt
runtime-canary-status.json                  (when present)
runtime-canary-anchor-status.json           (when present)
runtime-canary-replacement-status.json      (when present)
the current runtime-canary-*.jsonl journal  (when present)
```

An empty game-process result is written as `[]`, so a clean exit always has a concrete process snapshot. The process snapshot omits executable paths. `status.txt` replaces the local game and Mod roots with `<GAME_PATH>` and `<MOD_PATH>`, while `checkpoint.json` records only checkpoint metadata and file names.

## Analysis

After a clean exit, analyze the journal:

```powershell
$journal = Get-ChildItem `
  "$gamePath\mods\SasukeIronclad\canary-output" `
  -Filter "runtime-canary-combined-run-1-*.jsonl" `
  -File |
Sort-Object LastWriteTime -Descending |
Select-Object -First 1

python .\tools\analyze_runtime_canary_journal.py `
  --input $journal.FullName `
  --require-combined `
  --output .\runtime-canary-combined-review
```

The analyzer validates contiguous sequence numbers, one session per file, schema version and path redaction. A combined run passes only when it contains:

- exactly one `session_start` and `session_stop`;
- both title and animation layers enabled;
- at least one `title_applied` event;
- at least one `playback_started` event;
- at least one combat cleanup event;
- replacement activation when replacement was requested;
- no hard adapter, anchor or playback failure.

An unreviewed-card fallback is expected and is reported separately from a hard failure.

## First local test matrix

The first combined run should cover:

1. hand titles for Strike, Defend and Bash;
2. deck view and card tooltip titles;
3. one Defend animation using local timeline timing;
4. one Strike or Bash animation synchronized to an original damage event;
5. original-Ironclad concealment after verified playback starts;
6. restoration after an unreviewed card;
7. card reward and compendium titles;
8. combat end, room transition and clean process exit.

The exact game methods remain postfix-only. Internal card IDs, rules, save identity and gameplay state remain unchanged.

## Remaining boundary

Passing this canary proves that the two reviewed presentation layers coexist in one local exact-build process. It does not establish:

- multiplayer local-player-only behavior;
- compatibility with another Mod that manages the same visual node or title label;
- other resolutions or UI scales;
- missing-resource and forced-failure behavior in the real game;
- default-public branch compatibility;
- production readiness or final authored art quality.

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

New checkpoints also record:

```text
process_query_succeeded
game_process_count
auxiliary_process_count
process_absent_at_capture
journal_session_id
journal_event_count
journal_last_sequence
journal_last_event_type
```

This distinguishes a verified empty process query from a failed WMI query and makes the journal boundary independently reviewable.

## Analysis

After a clean exit, analyze the journal together with the checkpoint directory:

```powershell
$journal = Get-ChildItem `
  "$gamePath\mods\SasukeIronclad\canary-output" `
  -Filter "runtime-canary-combined-run-1-*.jsonl" `
  -File |
Sort-Object LastWriteTime -Descending |
Select-Object -First 1

python .\tools\analyze_runtime_canary_journal.py `
  --input $journal.FullName `
  --checkpoint-root ".\runtime-canary-checkpoints\combined-run-1" `
  --require-combined `
  --output .\runtime-canary-combined-review
```

The analyzer validates contiguous sequence numbers, one session per file, schema version and path redaction. A combined core run passes only when it contains:

- exactly one `session_start`;
- either one runtime `session_stop` or a checkpoint proving the game process is absent;
- both title and animation layers enabled;
- at least one `title_applied` event;
- at least one `playback_started` event;
- at least one combat cleanup event;
- replacement activation when replacement was requested;
- no hard adapter, anchor or playback failure.

The game runtime does not consistently emit `AppDomain.ProcessExit` early enough to append `session_stop`. A checkpoint with a successful process query and zero `SlayTheSpire2.exe` processes is therefore accepted as external closure evidence. The report exposes whether closure came from `runtime_session_stop` or `clean_process_checkpoint`.

An unreviewed-card fallback is expected and is reported separately from a hard failure.

To require all six reviewed title surfaces in the same session, add:

```powershell
--require-all-title-surfaces
```

The six surfaces are:

```text
card_art
hand
deck_list
reward
compendium
tooltip
```

## Reviewed combined run

The first user-provided combined archive is recorded at:

```text
SasukeIronclad/data/reviews/public-beta-24251656-combined-presentation-review.json
```

It established, in one exact-build game process:

- both title and animation layers enabled;
- nine successful title applications;
- title evidence on `card_art`, `hand` and `deck_list`;
- three completed animations for Strike and Defend;
- two original damage-impact forwards for Strike;
- exact local-player anchor binding;
- three successful original-Ironclad hide transitions;
- restoration after one unreviewed card;
- four later cards intentionally left on the original presentation for that combat;
- combat-end cleanup;
- zero hard failures and zero analysis errors;
- a final checkpoint with `process_count=0` and `process.json=[]`.

The original analyzer returned `partial` only because the runtime journal lacked `session_stop`. The final checkpoint proves the game process had exited, so the combined core coexistence result is accepted. The remaining follow-up is limited to combined evidence for `reward`, `compendium` and `tooltip`; a full combat stability rerun is not required.

## Focused UI-surface follow-up

For the follow-up, enable another combined session and cover only the missing UI surfaces:

1. open the compendium and select a reviewed card;
2. hover or open a tooltip for a reviewed card;
3. finish one short combat and inspect a card reward containing a reviewed card when available;
4. exit normally and save one clean-process checkpoint.

Then analyze with:

```powershell
python .\tools\analyze_runtime_canary_journal.py `
  --input $journal.FullName `
  --checkpoint-root "$checkpointRoot" `
  --require-combined `
  --require-title-surface compendium `
  --require-title-surface tooltip `
  --require-title-surface reward `
  --output .\runtime-canary-combined-ui-review
```

This is a targeted UI evidence run. It does not need to repeat the earlier multi-combat replacement matrix.

## Remaining boundary

Passing the complete combined canary proves that the two reviewed presentation layers coexist in one local exact-build process. It does not establish:

- multiplayer local-player-only behavior;
- compatibility with another Mod that manages the same visual node or title label;
- other resolutions or UI scales;
- missing-resource and forced-failure behavior in the real game;
- default-public branch compatibility;
- production readiness or final authored art quality.

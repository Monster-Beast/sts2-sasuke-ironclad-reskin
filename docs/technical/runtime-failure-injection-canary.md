# Runtime presentation failure-injection canary

This stage deliberately exercises the original-presentation recovery path for the
exact reviewed `public-beta` build. The injected failures are local, cosmetic,
one-shot and in-memory only. They do not edit the game, PCK, DLL, card model,
combat state or save data.

The exact reviewed target remains:

```text
branch          public-beta
Steam buildid   24251656
sts2.dll SHA    ee45848ff6319dfc7af2538d3a52d05d82bef35ee4c5fd0400dc9efe8f9054aa
MVID            a49d3537-5a42-4dcd-9877-663e394f2b44
BaseLib         v3.3.8
```

## Safety boundary

A failure scenario is rejected unless all of these are true:

- the exact-build canary gate passes;
- the animation layer and local-player anchor are enabled;
- original-Ironclad replacement is explicitly acknowledged;
- the selected card is in the reviewed active-card scope;
- Demon Form is not selected;
- `failure_injection_once` is true;
- the exact failure acknowledgement is present;
- runtime observation is disabled.

After one injected failure, replacement is disabled for the rest of the current
combat. The original Ironclad must remain visible while all original gameplay
methods continue normally.

## Scenarios

### `missing_timeline`

The next matching `CanPlay` result is forced to return `false` in memory. No
resource is renamed, deleted or modified. The normal missing-asset fallback path
must run before the original character is hidden.

Expected evidence:

```text
trigger_stage                              can_play
original_visual_hidden_at_trigger          false
replacement_ever_hidden                    false
restore_count_after_fault                   0
original_visibility_restored_after_fault   null
recovery_confirmed                         true
```

### `forced_playback_failure`

The reviewed timeline starts and the original Ironclad is hidden. On the next
Godot process frame, the host emits a synthetic asynchronous playback failure.
The existing playback-fallback listener must restore the captured original
visibility and disable replacement for the rest of the combat.

Expected evidence:

```text
trigger_stage                              after_replacement_hidden
original_visual_hidden_at_trigger          true
replacement_ever_hidden                    true
restore_count_after_fault                  >= 1
original_visibility_restored_after_fault   true
recovery_confirmed                         true
```

### `anchor_invalidation`

The reviewed timeline starts and the original Ironclad is hidden. On the next
Godot process frame, the overlay forgets only its own anchor reference and emits
the normal anchor-invalidated notification. It does not remove or free the game
node. The replacement controller must restore the Ironclad immediately.

Expected evidence is the same post-hide restoration shape as the forced playback
failure.

## Enable one scenario

Build and install the latest branch first. Use one complete game restart per
scenario.

Missing timeline:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "$gamePath" `
  -AnimationsOnly `
  -SessionLabel "failure-missing-1" `
  -ReplaceOriginal `
  -FailureScenario missing_timeline `
  -FailureCardId Strike
```

Forced playback failure:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "$gamePath" `
  -AnimationsOnly `
  -SessionLabel "failure-playback-1" `
  -ReplaceOriginal `
  -FailureScenario forced_playback_failure `
  -FailureCardId Strike
```

Anchor invalidation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "$gamePath" `
  -AnimationsOnly `
  -SessionLabel "failure-anchor-1" `
  -ReplaceOriginal `
  -FailureScenario anchor_invalidation `
  -FailureCardId Strike
```

The control script writes the exact-build acknowledgement automatically. Manual
marker editing is not an accepted test procedure.

## Real-game procedure

For each scenario:

1. launch through the Mod loader with BaseLib and SasukeIronclad enabled;
2. enter an Ironclad combat;
3. play the selected reviewed card once;
4. verify the original card effect completes normally;
5. verify the original Ironclad is visible after the injected failure;
6. play another reviewed card and verify the combat remains on original
   presentation rather than hiding the Ironclad again;
7. finish or leave the combat normally;
8. exit the game without entering another combat;
9. save one clean-process checkpoint.

For `missing_timeline`, the selected target card must be the first card played
in the fresh game process. Do not play another reviewed card first: a successful
earlier timeline is allowed to hide the original Ironclad and therefore cannot
prove the required pre-hide failure path. If the target card is not in the
opening hand, end the turn without playing a card and wait for it. After the
fault and one same-combat fallback card, leave or finish that combat and exit the
game; a later combat may legitimately use replacement again and would make the
process-level snapshot unsuitable for this assertion.

The expected visual difference is:

```text
missing_timeline          original Ironclad never hidden
forced_playback_failure   briefly replaced, then immediately restored
anchor_invalidation       briefly replaced, then immediately restored
```

## Status and checkpoint evidence

The new diagnostic file is:

```text
runtime-canary-failure-status.json
```

A passing post-hide fault should contain values equivalent to:

```json
{
  "requested": true,
  "triggered": true,
  "trigger_count": 1,
  "original_visual_hidden_at_trigger": true,
  "recovery_confirmed": true,
  "replacement_active_after_fault": false,
  "replacement_ever_hidden": true,
  "restore_count_after_fault": 1,
  "original_visibility_restored_after_fault": true,
  "anchor_bound_after_fault": false,
  "overlay_visible_after_fault": false,
  "replacement_disabled_for_combat": true
}
```

Save evidence after the game exits:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary-checkpoint.ps1 `
  -GamePath "$gamePath" `
  -Name "01-failure-and-clean-exit" `
  -OutputDirectory "$checkpointRoot"
```

The checkpoint tool copies the failure status together with the startup, anchor,
replacement, process and JSONL evidence.

## Analysis

```powershell
$startupStatus = Get-Content `
  "$gamePath\mods\SasukeIronclad\runtime-canary-status.json" `
  -Raw | ConvertFrom-Json

$journal = Join-Path `
  "$gamePath\mods\SasukeIronclad\canary-output" `
  $startupStatus.event_file

& $pythonExe `
  .\tools\analyze_runtime_canary_failure.py `
  --input "$journal" `
  --checkpoint-root "$checkpointRoot" `
  --expected-scenario forced_playback_failure `
  --output .\runtime-canary-failure-review
```

Change `--expected-scenario` for the other two runs. A pass requires:

- one exact scenario trigger;
- the expected hard event and reason marker;
- no unrelated hard failures;
- original-presentation recovery confirmed;
- captured original visibility verified after post-hide faults;
- replacement inactive after the fault;
- anchor and overlay cleared;
- replacement disabled for the current combat;
- runtime `session_stop` or a clean-process checkpoint.

The analyzer succeeds only when all requirements pass. Confirm all three signals:

```text
process exit code                         0
console marker                            RUNTIME_CANARY_FAILURE_PASS status=passed
runtime-canary-failure-review.json        "status": "passed", "passed": true
```

`partial` and `invalid` evidence both print
`RUNTIME_CANARY_FAILURE_NOT_PASSED` and return a non-zero exit code. Keep the
generated review for diagnosis, but do not count that run as accepted evidence.

## Reviewed real-game results

The tracked, path-redacted result record is stored at:

```text
SasukeIronclad/data/reviews/public-beta-24251656-failure-injection-review.json
```

The raw checkpoints and game logs remain local because they contain
machine-specific paths. The tracked record preserves their file names, sizes and
SHA-256 digests together with each analyzer result and the user's visual verdict.

Current exact-build result:

- `missing_timeline` passed after one partial run caused by playing Bash before
  the target Strike and continuing into later combats;
- `forced_playback_failure` did not trigger in the first real-game run: Strike
  and the follow-up Defend both completed Sasuke playback, while the diagnostic
  remained armed;
- the first playback run's local game log recorded repeated MonoMod JIT
  `ArgumentException` frames at the Godot-to-C# host dispatcher, proving the
  post-hide safety fault could not rely solely on `_Process` to consume its
  pending trigger;
- the fault is now consumed synchronously after active-card state is registered,
  with `_Process` retained as a fallback. A clean build from commit `8b143fc`
  passed the strict real-game rerun: Strike triggered exactly one fault, the
  original presentation was restored, and the user's follow-up Defend showed no
  Sasuke. Dispatcher exceptions persisted but no longer gated recovery;
- `anchor_invalidation` also passed on the same clean build: Strike invalidated
  the overlay anchor exactly once after replacement was hidden, the captured
  original visibility was restored, and the user's follow-up Defend showed no
  Sasuke;
- all three deliberate failure scenarios now pass strict analysis and direct
  visual review. The production profile remains unpromoted because the broader
  compatibility boundaries below are separate evidence requirements.

## Remaining boundary

Passing these three scenarios proves the existing local failure paths restore the
original Ironclad under deliberate resource, playback and anchor faults. It does
not promote the production profile and does not establish multiplayer, multi-Mod,
other-resolution or default-public compatibility. `form_removed`,
`character_state` and Demon Form replacement remain blocked.

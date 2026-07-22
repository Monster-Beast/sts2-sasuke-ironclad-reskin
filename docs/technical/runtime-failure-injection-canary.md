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
BaseLib         v3.3.7
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
trigger_stage                       can_play
original_visual_hidden_at_trigger   false
replacement_ever_hidden             false
restore_count_after_fault            0
recovery_confirmed                  true
```

### `forced_playback_failure`

The reviewed timeline starts and the original Ironclad is hidden. On the next
Godot process frame, the host emits a synthetic asynchronous playback failure.
The existing playback-fallback listener must restore the captured original
visibility and disable replacement for the rest of the combat.

Expected evidence:

```text
trigger_stage                       after_replacement_hidden
original_visual_hidden_at_trigger   true
replacement_ever_hidden             true
restore_count_after_fault           >= 1
recovery_confirmed                  true
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
8. exit the game;
9. save one clean-process checkpoint.

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
- replacement inactive after the fault;
- anchor and overlay cleared;
- replacement disabled for the current combat;
- runtime `session_stop` or a clean-process checkpoint.

## Remaining boundary

Passing these three scenarios proves the existing local failure paths restore the
original Ironclad under deliberate resource, playback and anchor faults. It does
not promote the production profile and does not establish multiplayer, multi-Mod,
other-resolution or default-public compatibility. `form_removed`,
`character_state` and Demon Form replacement remain blocked.

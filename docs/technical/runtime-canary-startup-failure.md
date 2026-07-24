# Runtime canary startup failure test

This test proves that the exact-build runtime canary fails closed before it
installs any presentation patch when a reviewed method signature does not match.
It is separate from the in-combat presentation failure matrix: a correct startup
failure creates no canary session and no JSONL journal.

The reviewed runtime remains:

```text
branch          public-beta
Steam buildid   24251656
sts2.dll SHA    ee45848ff6319dfc7af2538d3a52d05d82bef35ee4c5fd0400dc9efe8f9054aa
MVID            a49d3537-5a42-4dcd-9877-663e394f2b44
BaseLib         v3.3.8
```

This evidence does not promote the production integration profile.

## Scenario

The only approved startup scenario is:

```text
method_signature_mismatch
```

It is fixed to the already reviewed `card_visual_request` binding. The selected
target is taken from the tracked exact-build binding review; the control script
does not accept an arbitrary Harmony target or method name.

The resolver first confirms that the loaded method's unmodified signature
matches the reviewed signature. Only after that baseline match is confirmed does
the one-shot diagnostic alter one in-memory comparison operand. The normal exact
signature guard must then reject the target with this reason:

```text
Canary binding card_visual_request method signature does not match the reviewed target (fault_injection:method_signature_mismatch).
```

The checked-in review, observation manifest, game assembly, PCK and game files
remain unchanged.

## Dedicated marker mode

The marker uses a distinct mode:

```text
local_startup_failure_only
```

This is a compatibility safety boundary. Older Mod DLLs may ignore unknown JSON
properties. An older DLL that does not implement startup failure injection must
reject the unfamiliar mode instead of ignoring the new fields and enabling an
ordinary presentation canary.

The control script writes a marker equivalent to:

```json
{
  "schema_version": 1,
  "enabled": true,
  "mode": "local_startup_failure_only",
  "expected_branch": "public-beta",
  "expected_build_id": "24251656",
  "session_label": "startup-signature-v338-1",
  "enable_animations": true,
  "enable_titles": false,
  "hide_original_visual": false,
  "replacement_acknowledgement": "",
  "failure_injection_scenario": "none",
  "failure_injection_card_id": "",
  "failure_injection_once": true,
  "failure_injection_acknowledgement": "",
  "startup_failure_injection_scenario": "method_signature_mismatch",
  "startup_failure_injection_binding_id": "card_visual_request",
  "startup_failure_injection_once": true,
  "startup_failure_injection_acknowledgement": "public-beta-24251656-local-startup-method-signature-mismatch"
}
```

Startup failure injection and the existing in-combat presentation failure
injection are mutually exclusive. Original-visual replacement is also disabled.

## Enable the test

Build and install the exact commit before writing the marker. Then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "$gamePath" `
  -AnimationsOnly `
  -SessionLabel "startup-signature-v338-1" `
  -StartupFailureScenario method_signature_mismatch
```

There is deliberately no `-ReplaceOriginal`, `-FailureScenario` or
`-FailureCardId` argument. The binding and startup scenario are fixed by the
reviewed control path.

Changing the marker still requires a complete game restart.

## Expected startup status

The Mod initializer must run, establish the real signature baseline, consume the
one-shot diagnostic and reject target resolution before creating a runtime
session or calling the patch installer.

`runtime-canary-status.json` must contain values equivalent to:

```json
{
  "schema_version": 1,
  "mod_initializer_reached": true,
  "enabled": false,
  "animations_enabled": false,
  "titles_enabled": false,
  "requested_session_label": "startup-signature-v338-1",
  "session_id": null,
  "event_file": null,
  "failure_injection_scenario": "none",
  "failure_injection_card_id": null,
  "patched_binding_ids": [],
  "startup_failure_injection_requested": true,
  "startup_failure_injection_scenario": "method_signature_mismatch",
  "startup_failure_injection_binding_id": "card_visual_request",
  "startup_failure_injection_armed": false,
  "startup_failure_injection_triggered": true,
  "startup_failure_injection_trigger_count": 1,
  "startup_failure_injection_trigger_stage": "method_signature_comparison",
  "startup_failure_injection_baseline_match_confirmed": true,
  "patch_install_attempted": false,
  "reasons": [
    "Canary binding card_visual_request method signature does not match the reviewed target (fault_injection:method_signature_mismatch)."
  ]
}
```

`baseline_match_confirmed` is essential. Without it, a genuine unreviewed binary
drift could be mistaken for a successfully triggered diagnostic.

The disabled layer flags describe what was actually installed, not what the
marker requested for target selection. No presentation layer was installed.

## Minimal real-game procedure

The user performs only this gameplay probe:

1. launch the game and enter one Ironclad combat;
2. make the first actual card played **打击（Strike）**;
3. confirm its normal damage completes, the original Ironclad remains visible
   and Sasuke does not appear;
4. exit the game immediately without entering another combat.

If **打击（Strike）** is not available, end turns without playing another card
until it is available.

The requested reply is only:

```text
已退出，打击时没有佐助
```

or, if the safety assertion failed:

```text
已退出，打击时有佐助
```

No follow-up **防御（Defend）** play is required for this startup test.

## Clean-process checkpoint

After the game exits, save one checkpoint:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary-checkpoint.ps1 `
  -GamePath "$gamePath" `
  -Name "01-startup-failure-clean-exit" `
  -OutputDirectory "$checkpointRoot"
```

Unlike a normal canary run, there is no journal identity that can correlate the
checkpoint to a session. The checkpoint therefore must copy the machine-readable
`SasukeIronclad.canary.json` marker as well as the startup status.

A strict checkpoint has all of these properties:

- exactly one checkpoint carries the expected `session_label` marker;
- `canary_marker_present` is true and `observation_marker_present` is false;
- `SasukeIronclad.canary.json`, `runtime-canary-status.json`, `status.txt` and
  `process.json` are present and listed in `copied_files`;
- `process_query_succeeded` and `process_absent_at_capture` are true;
- `game_process_count` is zero and agrees with `process.json`;
- `event_file`, `journal_session_id`, `journal_event_count`,
  `journal_last_sequence` and `journal_last_event_type` are null;
- no JSONL is copied;
- no anchor, replacement or in-combat failure status is copied;
- the startup status timestamp is not later than the checkpoint timestamp.

Old JSONL files may remain in the installed Mod's `canary-output` directory. The
control script intentionally preserves historical journals. They are not part of
this run and must not be copied into this checkpoint.

## Strict analysis

Run the dedicated analyzer without an `--input` journal:

```powershell
python .\tools\analyze_runtime_canary_startup_failure.py `
  --checkpoint-root "$checkpointRoot" `
  --expected-scenario method_signature_mismatch `
  --expected-session-label "startup-signature-v338-1" `
  --output "$reviewRoot"
```

A pass requires all marker, startup-status, no-install and clean-process
requirements. Confirm all three signals:

```text
process exit code                                      0
console marker                                         RUNTIME_CANARY_STARTUP_FAILURE_PASS status=passed
runtime-canary-startup-failure-review.json             "status": "passed", "passed": true
```

`partial` and `invalid` evidence print
`RUNTIME_CANARY_STARTUP_FAILURE_NOT_PASSED` and return a non-zero exit code.

The regression test covers the passing shape and rejects, among other cases:

- the ordinary `local_visual_only` mode;
- a wrong build, scenario, binding, acknowledgement or session label;
- simultaneous replacement or presentation failure injection;
- a missing baseline match or a trigger that is absent, repeated or still armed;
- successful target resolution or any patch-install attempt;
- a created runtime session, event file or patched binding;
- a missing marker, observation marker, copied transition status or JSONL;
- a failed process query, inconsistent process counts or a running game;
- stale timestamps, duplicate matching checkpoints and malformed JSON.

## Evidence boundary

The strict analyzer proves:

- the intended exact-build diagnostic marker was active;
- the unmodified reviewed signature matched before the diagnostic perturbation;
- exact signature resolution failed once at the expected comparison;
- no runtime session, journal or presentation patch was installed;
- the game process exited before capture.

It cannot prove that **打击（Strike）** was played. Adding a card-play observer
would contradict the zero-patch startup assertion. The user's visual verdict is
therefore separate evidence and must be stored in the tracked, path-redacted
review together with checkpoint, analyzer, installed DLL/PCK and archive hashes.

Passing this test closes only the exact-build startup signature fail-closed
boundary. Multiplayer, multi-Mod ownership, other resolutions, default-public
compatibility, blocked Demon Form/form-removal/character-state bindings and final
authored assets remain separate requirements.

# Read-only runtime observation

This phase observes real calls in the current Steam `public-beta` build so the twelve pending game bindings can be reviewed with runtime evidence. It does **not** install the Sasuke visual/title integration.

## Safety boundary

Runtime observation is disabled unless all of the following are true:

1. `SasukeIronclad.observe.json` exists beside the installed Mod DLL;
2. the marker explicitly requests `mode: read_only`;
3. the marker acknowledges the exact branch and Steam buildid;
4. the loaded `sts2.dll` SHA-256 and MVID match the observation manifest;
5. the loaded BaseLib version matches the pending profile;
6. the latest-Beta attestation is no more than 72 hours old;
7. every required MethodDef token resolves to the reviewed declaring type and full signature.

Any mismatch disables every probe. The production integration contract remains `pending_local_audit`, and all visual/title bindings remain `pending_review`.

The observer uses exact Harmony prefix/postfix probes only. It does not replace return values, change arguments, skip original methods, alter save data, alter gameplay state, or change multiplayer messages.

## Data recorded

Each JSONL event may contain:

- UTC timestamp, session ID, sequence and call ID;
- enter/return phase and elapsed time;
- reviewed target ID, binding IDs and MethodDef token;
- declaring type and method name;
- argument **types**;
- numeric, Boolean or enum argument summaries;
- related STS2 card/power model type names found through a bounded field walk;
- a bounded caller stack containing method names only.

It does not record:

- localized card-title strings;
- arbitrary string argument values;
- method return values;
- absolute paths or source file names;
- complete object serialization;
- credentials, Steam identity or multiplayer payload bodies.

The session stops recording after the configured event limit. Failure to write one event disables further recording rather than affecting the game.

## Current exact scope

The checked-in manifest is restricted to:

```text
branch       public-beta
Steam build  24251656
BaseLib      v3.3.8
profile      public-beta-24251656-ee45848ff631
```

The active card scope contains the ten model types present in this build. Cleave, Heavy Blade and Limit Break remain design-only until a future Beta exposes matching card models.

A new Steam Beta, different DLL hash/MVID, different BaseLib version, or an expired attestation requires a fresh metadata audit before runtime observation can be enabled again.

## Build and install on Windows

From the repository root:

```powershell
$gamePath = "E:\steam\steamapps\common\Slay the Spire 2"
$godotExe = "C:\megadot\MegaDot_v4.5.1-stable_mono_win64.exe"

dotnet publish .\SasukeIronclad.csproj `
  --configuration Release `
  -p:Sts2Path="$gamePath" `
  -p:GodotPath="$godotExe"
```

The project copies its DLL and manifest to:

```text
<game>\mods\SasukeIronclad
```

and exports `SasukeIronclad.pck` there when the configured MegaDot executable is available.

Confirm the installed directory contains at least:

```text
SasukeIronclad.dll
SasukeIronclad.json
SasukeIronclad.pck
```

## Observation run 1

Create an explicit marker:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-observation.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -SessionLabel "observed-run-1" `
  -MaxEvents 20000
```

Launch the game. The Mod log should state:

```text
observation enabled=True
```

Exercise as many distinct surfaces as practical:

1. start an Ironclad combat;
2. play Strike, Defend and Bash;
3. play additional observed cards when available, especially a multi-hit or area attack;
4. obtain/use Flame Barrier or Demon Form when practical, then finish combat so power-removal events occur;
5. open the hand, deck list and card hover/inspection UI;
6. open a card reward;
7. open the card library/compendium and a card detail panel;
8. finish a combat;
9. return to the main menu and fully exit the game.

A run may be partial. Missing surfaces remain unreviewed rather than being guessed.

## Observation run 2

Overwrite the marker with an independent session label:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-observation.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -SessionLabel "observed-run-2" `
  -MaxEvents 20000
```

Repeat the relevant gameplay/UI actions and fully exit again. Two distinct session IDs are required; copying the same log twice is rejected by the analyzer.

## Disable observation

After the second run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-observation.ps1 `
  -Action disable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2"
```

Existing logs are preserved under:

```text
<game>\mods\SasukeIronclad\observation-output
```

Show the current marker and logs with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-observation.ps1 `
  -Action status `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2"
```

## Analyze two logs

```powershell
$logRoot = "E:\steam\steamapps\common\Slay the Spire 2\mods\SasukeIronclad\observation-output"
$logs = Get-ChildItem $logRoot -Filter "runtime-observation-*.jsonl" |
  Sort-Object LastWriteTime |
  Select-Object -Last 2

python .\tools\analyze_runtime_observation.py `
  --manifest .\SasukeIronclad\data\runtime_observation_targets.json `
  --input $logs[0].FullName `
  --input $logs[1].FullName `
  --output .\runtime-observation-review
```

Outputs:

```text
runtime-observation-review/runtime-observation-review.json
runtime-observation-review/runtime-observation-review.md
```

Possible statuses:

- `needs_second_run`: fewer than two independent logs;
- `partial_observation`: valid logs, but some binding surfaces were not observed in every run;
- `pending_manual_review`: all twelve surfaces have runtime evidence in both runs;
- `invalid`: fingerprint, privacy, session, sequence, target or call-pair validation failed.

Even `pending_manual_review` never fills a Binding automatically. A reviewer must still compare ordering, model types and caller stacks, select the narrowest valid MethodDef, verify original fallback and local-visual-only behavior, and keep the production integration disabled until the complete real-game regression passes.

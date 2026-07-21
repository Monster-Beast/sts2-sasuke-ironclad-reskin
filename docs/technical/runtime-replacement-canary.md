# Runtime original-visual replacement canary

This stage promotes the reviewed local-player overlay into an explicit replacement
experiment for the exact `public-beta` build only. It still does not promote the
production integration profile.

## Approved local calibration

The user-reviewed local setup is recorded in:

```text
SasukeIronclad/data/reviews/public-beta-24251656-anchor-calibration.json
```

The current defaults are:

```text
anchor type   MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals
anchor name   Ironclad
scale         1.2
offset X      0
offset Y      -150
```

These values were approved on one display and UI-scale setup. Other resolutions
must retain explicit overrides.

## Explicit opt-in

Replacement is never enabled by a normal animation canary. It requires:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -AnimationsOnly `
  -ReplaceOriginal
```

The script writes both:

```json
{
  "hide_original_visual": true,
  "replacement_acknowledgement": "public-beta-24251656-local-ironclad-replacement"
}
```

The exact-build gate rejects manually incomplete or stale acknowledgements.

## Activation order

The original Ironclad is not hidden at game startup. Replacement activation is
allowed only after all of these conditions are true:

1. the exact reviewed Beta fingerprint matches;
2. the reviewed local-card-play postfix fires;
3. a unique local `Player` relationship resolves an `NCreatureVisuals` anchor;
4. the anchor type is exactly `MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals`;
5. the anchor name is exactly `Ironclad`;
6. the selected Sasuke timeline exists and starts successfully;
7. the original anchor is currently visible and is not already hidden by the
   game or another Mod.

Only then is the original `Visible` value captured and set to `false`.

## Restoration guarantees

The captured original visibility is restored when any of these occurs:

- a local card is outside the reviewed current-Beta replacement scope;
- Demon Form is played while its persistent form-removal evidence is still blocked;
- animation mapping or resource fallback;
- synchronous or asynchronous playback failure;
- original-impact forwarding failure;
- verified anchor loss or transform failure;
- a caught canary adapter exception;
- combat end;
- Mod patch reset or process shutdown;
- canary session disposal.

A failed replacement attempt disables replacement for the rest of the current
combat. This prevents repeated character flicker and preserves the complete
original presentation for all remaining cards in that combat. The original game
method has already completed because every reviewed adapter remains postfix-only.

## Diagnostics

The control script shows three files:

```text
runtime-canary-status.json
runtime-canary-anchor-status.json
runtime-canary-replacement-status.json
```

A successful replacement activation should contain:

```json
{
  "requested": true,
  "active": true,
  "ever_hidden": true,
  "last_transition": "original_visual_hidden_after_verified_overlay_playback",
  "target_type": "MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals",
  "target_name": "Ironclad",
  "original_visible_before_hide": true,
  "anchor_bound": true,
  "overlay_visible": true
}
```

After a completed combat or shutdown, `active` should become `false` and
`restore_count` should increase. `ever_hidden` remains true so the successful
replacement is not lost from the diagnostic history.

## Remaining limits

This replacement canary is not a production release because it still needs:

- several combat and room-transition runs;
- reviewed and unreviewed card fallback verification;
- multiplayer local-player-only verification;
- different resolutions and UI scales;
- multi-Mod visibility ownership checks;
- missing-resource and forced playback-failure tests;
- a separate audit and profile for the default public game branch;
- final authored Sasuke art and animation assets instead of the graybox rig.

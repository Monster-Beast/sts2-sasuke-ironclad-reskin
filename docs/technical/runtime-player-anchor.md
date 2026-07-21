# Runtime local-player anchor canary

This stage moves the Sasuke graybox overlay from the viewport origin to the
local Ironclad combat visual without promoting the production integration.

## Safety boundary

The anchor canary is still an overlay-only experiment:

- it requires the exact reviewed `public-beta` build fingerprint;
- it requires an explicit `SasukeIronclad.canary.json` marker;
- it uses the reviewed local-card-play postfix only;
- it never changes game arguments, return values, card IDs or combat state;
- it does not hide, free, reparent or modify the original Ironclad visual;
- it remains hidden until a unique local-player combat visual is proven;
- ambiguous multiplayer candidates fail closed instead of choosing the first
  player-looking node.

The production integration contract remains `pending_local_audit` with no
profiles.

## Resolution strategy

On the first reviewed local card-play callback, the resolver performs two
bounded read-only searches:

1. recover concrete `Player` references from the callback instance and
   arguments through a small allowlist of relationship members;
2. scan the current Godot scene for `NCreatureVisuals` or equivalent
   player-visual candidates and recover their associated `Player` references.

A candidate is accepted only when it is uniquely related by reference equality
to the local `Player`. If the callback does not expose a player reference, the
single-player fallback accepts exactly one Player-owned visual candidate. Two
or more candidates leave the overlay hidden.

The resolver excludes every node owned by the Sasuke Mod, caps scene and object
traversal, and never records object contents or absolute paths.

## Transform tracking

After a unique anchor is found, the overlay host tracks the anchor each frame:

- canvas-space global position;
- global rotation;
- global scale multiplied by the explicit canary scale;
- a Z index one step above the original visual.

The optional X/Y offsets are calibration values in canvas pixels. The overlay
host starts hidden and is also hidden immediately if the anchor leaves the
scene tree.

## Local controls

Example animation-only run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -AnimationsOnly `
  -AnchorScale 1.0 `
  -AnchorOffsetX 0 `
  -AnchorOffsetY 0
```

The marker always sets:

```json
{
  "anchor_to_local_player": true,
  "anchor_scale": 1.0,
  "anchor_offset_x": 0.0,
  "anchor_offset_y": 0.0
}
```

Scale is restricted to `0.25..3.0`; each offset is restricted to
`-1000..1000`.

## Diagnostics

After launching the game and playing one reviewed local Ironclad card, inspect:

```text
<game>/mods/SasukeIronclad/runtime-canary-anchor-status.json
```

A successful result contains:

```json
{
  "attempted": true,
  "bound": true,
  "overlay_visible": true,
  "original_visual_hidden": false
}
```

The document also reports the safe anchor type/name, global position,
calibration values, candidate count and local-player reference count. It never
contains an absolute path.

When `bound` is false, the graybox remains hidden and the `reasons` array
explains whether no candidate, no local-player relationship or multiple
similarly ranked candidates were found.

## Promotion rule

A successful anchor does not authorize hiding the original Ironclad. Before a
true replacement can be considered, the anchored overlay must pass:

- multiple combats and room transitions;
- single-player and multiplayer local-only verification;
- different resolutions and UI scales;
- multi-Mod compatibility;
- missing-resource and thrown-adapter fallback tests;
- exact default-public-version audit separate from the Beta profile.

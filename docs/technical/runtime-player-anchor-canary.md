# Runtime local-player anchor canary

This stage moves the Sasuke graybox overlay from the viewport origin to the
local Ironclad combat visual without promoting the production integration.

## Safety boundary

The anchor canary is an overlay-only experiment unless the separate explicit
replacement switch is supplied:

- it requires the exact reviewed `public-beta` build fingerprint;
- it requires an explicit `SasukeIronclad.canary.json` marker;
- it uses the reviewed local-card-play postfix only;
- it never changes game arguments, return values, card IDs or combat state;
- normal overlay mode does not hide, free, reparent or modify the original
  Ironclad visual;
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

## Reviewed local result

The successful local run resolved:

```text
strategy                     callback_local_player_reference
anchor type                  MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals
anchor name                  Ironclad
candidate count              2
local Player references      1
observed global position     (480, 740)
```

After visual calibration, the user approved:

```text
scale       1.2
offset X    0
offset Y   -150
```

The evidence is stored in:

```text
SasukeIronclad/data/reviews/public-beta-24251656-anchor-calibration.json
```

It is a single-display calibration rather than a universal value for every
resolution and UI scale.

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

The approved animation-only overlay run is now the default:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -AnimationsOnly
```

Equivalent explicit values are:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -AnimationsOnly `
  -AnchorScale 1.2 `
  -AnchorOffsetX 0 `
  -AnchorOffsetY -150
```

Scale is restricted to `0.25..3.0`; each offset is restricted to
`-1000..1000`.

## Diagnostics

After launching the game and playing one reviewed local Ironclad card, inspect:

```text
<game>/mods/SasukeIronclad/runtime-canary-anchor-status.json
```

A successful overlay result contains:

```json
{
  "attempted": true,
  "bound": true,
  "overlay_visible": true,
  "replacement_requested": false,
  "original_visual_hidden": false
}
```

The document also reports the safe anchor type/name, global position,
calibration values, candidate count and local-player reference count. It never
contains an absolute path.

When `bound` is false, the graybox remains hidden and the `reasons` array
explains whether no candidate, no local-player relationship or multiple
similarly ranked candidates were found.

## Replacement stage

The accepted calibration is now eligible for a separate explicit replacement
canary:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\runtime-canary.ps1 `
  -Action enable `
  -GamePath "E:\steam\steamapps\common\Slay the Spire 2" `
  -AnimationsOnly `
  -ReplaceOriginal
```

That mode is documented in `runtime-replacement-canary.md`. It captures the
original visibility and restores it on fallback, anchor loss, combat end and
session disposal. It remains a canary and does not promote production bindings.

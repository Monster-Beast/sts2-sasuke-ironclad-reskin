# Hebi Sasuke Turnaround V1

## Purpose

M2 formal visual-direction source for the front, side, and back construction of
the combat character rig. This image is concept reference only; it is excluded
from the Godot export and does not replace the tested runtime rig by itself.

## Provenance

- Created from scratch with OpenAI's built-in image generation tool on
  2026-07-23 under user-directed Codex prompting.
- No game files, anime frames, manga panels, official illustrations, or
  third-party Mod assets were supplied as inputs.
- Output dimensions: `1536x1024`.
- Source SHA-256:
  `4f4be40e8542a2d8850bee1a3b852e863826b0581e9ed7ba3d8dd778375a9bdf`.
- [OpenAI's Terms of Use](https://openai.com/policies/terms-of-use/) assign
  OpenAI's rights in output to the user to the extent permitted by law. This
  project remains a free, non-commercial fan work; all third-party character
  and franchise rights remain with their respective owners.

## Final prompt

```text
Use case: stylized-concept
Asset type: premium 2D game character design sheet for a spectacular combat reskin
Primary request: create an exceptionally handsome, lavish, high-impact Hebi-era Sasuke Uchiha character turnaround, optimized for visual wow factor
Subject: the same character shown in full-body front, strict side, and back views at matching scale; sharp youthful face, intense red Sharingan eyes, dramatic layered raven-black hair, elegant open high-collar silver-gray combat tunic with complex dark indigo panels, sculpted black forearm guards, flowing layered waist cloth, richly braided purple rope belt with subtle metallic clasps, fitted navy shinobi trousers and refined black sandals; an ornate straight Kusanagi-style sword with silver-black hilt and sleek scabbard; small controlled blue-white lightning filaments around the sword hand and blade; subtle purple curse-mark filigree as a fashion-like accent along one collar and forearm, not a transformed body
Style/medium: top-tier original anime-inspired 2D game concept art, luxurious painterly rendering, crisp elegant linework, strong silhouette, rich material definition, sophisticated costume design, dramatic but readable; original fan-art interpretation created from scratch, not copied or traced from any frame or existing asset
Composition/framing: wide cinematic design sheet with three evenly spaced full-body views on one ground line; front view slightly heroic, side and back accurate enough for rig construction; entire hair, hands, sword, and feet visible; faint oversized red eye-ring motif and electric calligraphy strokes in the background for presentation energy without obscuring the views
Lighting/mood: cool moonlit rim lighting with controlled crimson eye accents, stylish, dangerous, composed, elite
Color palette: silver gray, ink black, deep indigo, royal purple, blue-white lightning, restrained crimson
Materials/textures: layered woven cloth, satin highlights, braided rope, brushed steel, lacquered scabbard, subtle battle wear
Constraints: one character identity repeated consistently across all three views; costume and sword construction consistent; no extra character; no cropped body; no comedy; no chibi; no photorealism; no UI; no card frame; no watermark; no written labels or paragraphs
Avoid: plain generic clothing, flat lighting, stiff mannequin feeling, muddy silhouette, oversized armor, adult cloak, Rinnegan, giant wings, full monster transformation
```

## Rig extraction targets

- Preserve the high collar, layered waist cloth, braided purple belt, forearm
  guards, and straight sword as the primary silhouette anchors.
- Keep blue-white lightning, crimson eye glow, and purple curse-mark patterns as
  separately switchable visual layers for low-flash and state handling.
- Do not raster-crop this sheet into the game. Rebuild the production rig from
  original layered source parts so animation pivots and fallback behavior stay
  deterministic.

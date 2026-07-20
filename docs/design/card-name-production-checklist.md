# Full Card Name Production Checklist

Use this checklist after the installed-game card ID audit is complete.

## Scope

Cover every current Ironclad-related title surface:

- starting cards;
- normal reward-pool cards;
- multiplayer-only cards;
- cards created by other cards;
- cards produced by transformation effects;
- temporary or special Ironclad cards;
- upgraded title variants;
- Stable/Beta compatibility aliases when IDs differ.

## Per-card fields

Each naming entry must contain:

- verified original `card_id`;
- original `zh-CN` and `en-US` title;
- Sasuke `zh-CN` and `en-US` display title;
- normal, multiplayer, derived, transformed or temporary classification;
- semantic gameplay anchor;
- card-art concept using the same Sasuke technique;
- rename approval status;
- installed-game version where the ID was verified.

## Naming rules

1. Name from the gameplay function, not from a literal translation of the original English noun.
2. Use a technique family consistently: grass-cutter sword, Chidori, Fire Style, Sharingan, curse mark, snake techniques or body flicker.
3. Avoid assigning the same signature technique to unrelated cards unless they form an intentional family.
4. Generated cards must remain understandable together with the card that creates them.
5. Keep names short enough for the card-title banner in both supported locales.
6. Keep the `+` suffix visible after upgrade.
7. Do not insert damage, cost or rules into the naming catalog.
8. Unknown and removed IDs must fall back to the original title.

## Transformation example

```text
Primal Force / 原始力量
→ future Sasuke-themed source-card title
→ transforms attacks into GIANT_ROCK
→ GIANT_ROCK displays as 千鸟锐枪 / Chidori Spear
```

The transformation still targets the original `GIANT_ROCK` identity. Only the title and matching original artwork are replaced.

## Release checks

- no current card title falls back unexpectedly;
- no duplicate Sasuke title exists within one locale;
- all source/generated pairs are semantically coherent;
- all titles fit the target card banner;
- game rules, upgrades and transformations are unchanged;
- Stable and Beta ID differences are explicitly recorded;
- unresolved IDs remain provisional rather than guessed.

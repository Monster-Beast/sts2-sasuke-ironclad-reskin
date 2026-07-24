# Card Renaming System

## Goal

The Sasuke reskin may replace every player-facing card title with a Sasuke-themed name, including generated and transformed cards whose original names do not map cleanly to a Sasuke technique.

This remains a cosmetic feature. A renamed card keeps its original internal identity and all gameplay behavior.

## Identity boundary

```text
original card_id
→ original cost, type, rarity, rules, upgrade and transformation behavior
→ Sasuke display-name resolver
→ localized player-facing title
```

The rename layer must never change:

- `card_id` or resource identity;
- damage, block, cost, targeting or card type;
- upgrade state or upgrade result;
- exhaust, ethereal, retain, innate or other keywords;
- generated-card and transformation relationships;
- save data, deck serialization, RNG or multiplayer synchronization;
- rules text used by the game engine.

If the rename catalog, locale or future UI hook fails, the original game name is displayed.

## Data source

`SasukeIronclad/data/card_name_overrides.json` owns display names.

Each entry records:

- stable original `card_id`;
- original Chinese and English names;
- Sasuke Chinese and English display names;
- `normal` or `derived` card kind;
- semantic gameplay anchor used for naming;
- matching card-art concept;
- approval and game-ID verification status.

Normal entries must cover every card currently present in `card_visual_map.json`. Derived entries may exist before they receive a dedicated card animation, but unverified IDs must remain marked provisional.

## First approved names

| Internal card | Chinese display name | English display name |
|---|---|---|
| Strike | 草薙·瞬斩 | Kusanagi Flash |
| Defend | 草薙·剑御 | Kusanagi Guard |
| Bash | 写轮眼·破势 | Sharingan Breaker |
| Anger | 影手里剑 | Afterimage Shuriken |
| Cleave | 千鸟流·横扫 | Chidori Stream Sweep |
| Thunderclap | 千鸟流·雷震 | Chidori Stream Burst |
| Heavy Blade | 雷遁·草薙断 | Lightning Blade Execution |
| Flame Barrier | 火遁·炎阵 | Fire Style: Flame Ward |
| Whirlwind | 千鸟流·剑刃风暴 | Chidori Blade Storm |
| Burning Pact | 咒印·献契 | Curse Mark Covenant |
| Demon Form | 咒印·二阶段 | Curse Mark Stage Two |
| Limit Break | 写轮眼·极限解放 | Sharingan Overdrive |
| Fiend Fire | 火遁·龙火歼灭 | Dragon Flame Annihilation |

## Derived-card example

The generated `GIANT_ROCK` card is provisionally renamed:

```text
巨石 / Giant Rock
→ 千鸟锐枪 / Chidori Spear
```

The reason is semantic rather than literal: the original card is a straightforward high-damage single-target attack, so the Sasuke title and art use a compressed lightning spear while preserving the original generated-card identity and damage values.

The ID stays provisional until confirmed against the installed game version.

## Localization and upgrades

Supported locales start with:

- `zh-CN`;
- `en-US`.

Unknown Chinese locales fall back to the configured Chinese name; unknown English locales fall back to English; all other locales fall back to the configured default and then to the original name.

Upgraded cards keep the game-visible `+` suffix:

```text
草薙·瞬斩+
Kusanagi Flash+
```

The rename layer does not create a second upgrade system.

## Future UI hook

The verified game adapter should obtain the final title through:

```csharp
VisualRegistry.ResolveCardDisplayName(cardId, locale, upgraded, originalName)
```

Only title-rendering surfaces may call the resolver. Gameplay models, rule evaluation, save logic and network messages must continue using the original card identity.

## Full-card-pool production

After the local card/resource audit is complete:

1. export the current Ironclad normal, multiplayer, generated and transformed card IDs;
2. create one naming entry for every title surface;
3. group cards by semantic function rather than literal English words;
4. align each approved name with its card-art brief and animation concept;
5. block release when a current card falls back to an unrelated Ironclad title;
6. keep removed or Beta-only IDs in a versioned compatibility section rather than silently reusing them.

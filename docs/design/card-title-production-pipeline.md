# Card title production pipeline

## Scope

Card renaming is a display-only presentation feature. The original card ID, rules text, cost, upgrade state, generation graph, save identity and multiplayer identity remain authoritative.

## Resolution order

```text
verified original card_id
→ explicit Sasuke title override
→ locale fallback
→ upgraded `+` suffix
→ surface layout policy
→ original title fallback
```

Derived and transformed cards never inherit the source card's title automatically. They receive an explicit title only when their own effect semantics have been reviewed.

## Supported surfaces

- card art title;
- combat hand;
- deck list;
- card reward;
- compendium;
- tooltip.

Each surface defines a width budget, line count, minimum scale and final original-title fallback. Actual UI hooks remain `pending_local_audit` until the installed build is inspected.

## Production checklist

1. Export installed card IDs and original localized titles.
2. Record the effect semantics without rewriting rules text.
3. Choose a Sasuke technique family: sword, lightning, fire, Sharingan, curse mark, shuriken or snake.
4. Approve unique zh-CN and en-US titles.
5. Produce a card-art brief with composition and upgrade-only visual delta.
6. Run `card_name_preview.tscn` for every surface and upgraded state.
7. Run both card-title validation workflows.
8. Bind verified UI title surfaces only after the local symbol audit.

## Preview

Open:

```text
SasukeIronclad/scenes/runtime/card_name_preview.tscn
```

The preview switches locale, title surface and upgraded state. It reports `OK`, `SCALE`, `WRAP` or `ORIGINAL FALLBACK` without invoking gameplay code.

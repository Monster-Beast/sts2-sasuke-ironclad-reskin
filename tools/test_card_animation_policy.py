#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "SasukeIronclad/data/card_animation_manifest.json"


def load_manifest() -> dict:
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def select_animation_id(card: dict, **runtime_values: int | bool) -> str:
    """Offline invariant model: runtime values never select AnimationId."""
    _ = runtime_values
    return card["animation_id"]


def select_variant(card: dict, **runtime_values: int | bool) -> str:
    variants = set(card["variants"])
    if runtime_values.get("low_flash") and "low_flash" in variants:
        return "low_flash"
    if runtime_values.get("fast") and "fast" in variants:
        return "fast"
    if runtime_values.get("lethal") and "lethal" in variants:
        return "lethal"

    empowered = (
        card["card_id"] == "Heavy Blade" and runtime_values.get("strength", 0) > 0 and "high_strength" in variants
    ) or (
        card["card_id"] == "Whirlwind" and runtime_values.get("energy_spent", 0) > 1 and "x_energy" in variants
    ) or (
        card["card_id"] == "Fiend Fire" and runtime_values.get("exhausted_card_count", 0) > 1 and "hand_count" in variants
    )
    if empowered:
        return "empowered"
    if runtime_values.get("upgraded") and "upgraded" in variants:
        return "upgraded"
    return "base"


def main() -> int:
    data = load_manifest()
    cards = {entry["card_id"]: entry for entry in data["animations"]}

    for card_id, card in cards.items():
        base = select_animation_id(card, final_damage=1, hit_count=1, strength=0)
        high_damage = select_animation_id(card, final_damage=999, hit_count=1, strength=0)
        many_hits = select_animation_id(card, final_damage=1, hit_count=99, strength=0)
        empowered = select_animation_id(card, final_damage=999, hit_count=99, strength=99)
        assert base == high_damage == many_hits == empowered, card_id

        if card["is_damage_card"]:
            assert card["unique_timeline"] is True, card_id
            assert {"base", "low_flash"}.issubset(card["variants"]), card_id
            assert card["hit_sync"] == "original_hit_events", card_id
            assert card["damage_role"] == "variant_parameter_only", card_id

    animation_ids = [card["animation_id"] for card in cards.values()]
    assert len(animation_ids) == len(set(animation_ids)), "animation_id values must be unique"

    assert cards["Strike"]["animation_id"] != cards["Heavy Blade"]["animation_id"]
    assert cards["Strike"]["animation_id"] != cards["Fiend Fire"]["animation_id"]
    assert cards["Whirlwind"]["animation_id"] != cards["Thunderclap"]["animation_id"]

    assert select_variant(cards["Heavy Blade"], strength=10) == "empowered"
    assert select_variant(cards["Whirlwind"], energy_spent=3) == "empowered"
    assert select_variant(cards["Fiend Fire"], exhausted_card_count=5) == "empowered"
    assert select_variant(cards["Strike"], strength=99, final_damage=999) == "base"
    assert select_variant(cards["Heavy Blade"], strength=10, low_flash=True) == "low_flash"

    print(f"OK: animation identity and card-local variant invariants hold for {len(cards)} timelines.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

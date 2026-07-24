#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "SasukeIronclad/data/card_animation_manifest.json"


def load_manifest() -> dict:
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def select_animation_id(card: dict, **runtime_values: int | bool) -> str:
    _ = runtime_values
    return card["animation_id"]


def select_content_variant(card: dict, **runtime_values: int | bool) -> str:
    variants = set(card["variants"])
    if runtime_values.get("lethal") and "lethal" in variants:
        return "lethal"

    empowered = (
        card["card_id"] == "Heavy Blade" and runtime_values.get("strength", 0) > 0 and "high_strength" in variants
    ) or (
        card["card_id"] == "Whirlwind" and runtime_values.get("energy_spent", 0) > 1 and "x_energy" in variants
    ) or (
        card["card_id"] == "Fiend Fire" and runtime_values.get("exhausted_card_count", 0) > 1 and "hand_count" in variants
    ) or (
        card["card_id"] == "Limit Break" and runtime_values.get("strength", 0) >= 5 and "high_strength" in variants
    )
    if empowered:
        return "empowered"
    if runtime_values.get("upgraded") and "upgraded" in variants:
        return "upgraded"
    return "base"


def select_presentation(card: dict, **runtime_values: int | bool) -> tuple[str, bool, bool]:
    return (
        select_content_variant(card, **runtime_values),
        bool(runtime_values.get("fast")),
        bool(runtime_values.get("low_flash")),
    )


def main() -> int:
    data = load_manifest()
    cards = {entry["card_id"]: entry for entry in data["animations"]}

    for card_id, card in cards.items():
        base = select_animation_id(card, final_damage=1, hit_count=1, strength=0)
        high_damage = select_animation_id(card, final_damage=999, hit_count=1, strength=0)
        many_hits = select_animation_id(card, final_damage=1, hit_count=99, strength=0)
        empowered = select_animation_id(card, final_damage=999, hit_count=99, strength=99)
        assert base == high_damage == many_hits == empowered, card_id

        assert card["unique_timeline"] is True, card_id
        expected_sync = "original_hit_events" if card["is_damage_card"] else "timeline_local_events"
        assert card["hit_sync"] == expected_sync, card_id
        assert card["damage_role"] == "variant_parameter_only", card_id
        if card["is_damage_card"]:
            assert {"base", "low_flash"}.issubset(card["variants"]), card_id

    non_damage_cards = {
        "Defend", "Flame Barrier", "Burning Pact", "Demon Form", "Limit Break"
    }
    assert {card_id for card_id, card in cards.items() if not card["is_damage_card"]} == non_damage_cards

    animation_ids = [card["animation_id"] for card in cards.values()]
    assert len(animation_ids) == len(set(animation_ids)), "animation_id values must be unique"
    assert cards["Strike"]["animation_id"] != cards["Heavy Blade"]["animation_id"]
    assert cards["Strike"]["animation_id"] != cards["Fiend Fire"]["animation_id"]
    assert cards["Whirlwind"]["animation_id"] != cards["Thunderclap"]["animation_id"]

    assert select_content_variant(cards["Heavy Blade"], strength=10) == "empowered"
    assert select_content_variant(cards["Whirlwind"], energy_spent=3) == "empowered"
    assert select_content_variant(cards["Fiend Fire"], exhausted_card_count=5) == "empowered"
    assert select_content_variant(cards["Limit Break"], strength=8) == "empowered"
    assert select_content_variant(cards["Limit Break"], strength=2) == "base"
    assert select_content_variant(cards["Strike"], strength=99, final_damage=999) == "base"

    layered = select_presentation(
        cards["Heavy Blade"],
        strength=10,
        low_flash=True,
        fast=True,
    )
    assert layered == ("empowered", True, True)
    lethal_accessible = select_presentation(
        cards["Fiend Fire"],
        lethal=True,
        low_flash=True,
    )
    assert lethal_accessible == ("lethal", False, True)

    selector_source = (ROOT / "SasukeIroncladCode/Runtime/CardAnimationSelector.cs").read_text(encoding="utf-8")
    assert "SelectContentVariant" in selector_source
    assert "FastMode = context.FastMode" in selector_source
    assert "LowFlashMode = context.LowFlashMode" in selector_source
    assert "RequiresOriginalImpactSync = spec.IsDamageCard && string.Equals" in selector_source
    assert '"original_hit_events"' in selector_source
    assert "return CardAnimationVariant.LowFlash" not in selector_source
    assert "return CardAnimationVariant.Fast" not in selector_source

    host_source = (ROOT / "SasukeIroncladCode/Adapters/GodotVisualSceneHost.cs").read_text(encoding="utf-8")
    assert '["fast_mode"] = selection.FastMode' in host_source
    assert '["low_flash"] = selection.LowFlashMode' in host_source
    assert '["external_impact_sync"] = selection.RequiresOriginalImpactSync' in host_source
    assert '["external_impact_sync"] = true' not in host_source

    registry_source = (ROOT / "SasukeIroncladCode/Visuals/VisualRegistry.cs").read_text(encoding="utf-8")
    assert 'animation.IsDamageCard' in registry_source
    assert '"timeline_local_events"' in registry_source

    director_source = (ROOT / "SasukeIronclad/scripts/runtime/animation_director.gd").read_text(encoding="utf-8")
    for contract in ["_is_fast_mode", "_is_low_flash_mode", "content_scale * fast_scale"]:
        assert contract in director_source

    print(
        f"OK: animation identity, layered presentation and damage-only original-impact sync hold for {len(cards)} timelines."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

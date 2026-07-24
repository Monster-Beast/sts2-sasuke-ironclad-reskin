#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAME_MAP = ROOT / "SasukeIronclad/data/card_name_overrides.json"
SURFACES = ROOT / "SasukeIronclad/data/card_title_surfaces.json"
BRIEFS = ROOT / "SasukeIronclad/data/card_art_briefs.json"
INHERITANCE = ROOT / "SasukeIronclad/data/card_name_inheritance.json"

REQUIRED_SURFACES = {"card_art", "hand", "deck_list", "reward", "compendium", "tooltip"}
FORBIDDEN_BRIEF_FIELDS = {"cost", "damage", "block", "rules_text", "description", "upgrade_effect"}


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def title_units(value: str) -> float:
    total = 0.0
    for character in value:
        codepoint = ord(character)
        if character == " ":
            total += 0.5
        elif codepoint >= 0x2E80:
            total += 2.0
        elif character in {"·", ":", "-", "+"}:
            total += 0.75
        else:
            total += 1.0
    return total


def walk_keys(value):
    if isinstance(value, dict):
        for key, nested in value.items():
            yield key
            yield from walk_keys(nested)
    elif isinstance(value, list):
        for nested in value:
            yield from walk_keys(nested)


def main() -> int:
    names = load(NAME_MAP)
    surfaces = load(SURFACES)
    briefs = load(BRIEFS)
    inheritance = load(INHERITANCE)

    assert surfaces["schema_version"] == 1
    assert surfaces["gameplay_changes"] is False
    assert surfaces["fallback"] == "original_title"
    surface_items = surfaces["surfaces"]
    assert {item["id"] for item in surface_items} == REQUIRED_SURFACES
    assert len(surface_items) == len({item["id"] for item in surface_items})

    for surface in surface_items:
        assert surface["hook_status"] == "pending_local_audit"
        assert surface["max_lines"] in {1, 2}
        assert 0.6 <= float(surface["minimum_scale"]) <= 1.0
        assert set(surface["width_units"]) == {"zh-CN", "en-US"}
        assert "original_title" in surface["overflow_order"]

    name_cards = {item["card_id"]: item for item in names["cards"]}
    brief_items = briefs["briefs"]
    brief_by_card = {item["card_id"]: item for item in brief_items}
    assert set(brief_by_card) == set(name_cards)
    assert len(brief_items) == len({item["brief_id"] for item in brief_items})
    assert briefs["gameplay_changes"] is False
    assert not (set(walk_keys(briefs)) & FORBIDDEN_BRIEF_FIELDS)

    for card_id, name in name_cards.items():
        brief = brief_by_card[card_id]
        assert brief["brief_id"].strip()
        assert brief["family"].strip()
        assert brief["focal_action"].strip()
        assert brief["composition"].strip()
        assert brief["upgrade_delta"].strip()
        assert brief["status"] == name["rename_status"]

    card_art_surface = next(item for item in surface_items if item["id"] == "card_art")
    overflow_cards: list[tuple[str, str, float, float]] = []
    for card_id, item in name_cards.items():
        for locale in ["zh-CN", "en-US"]:
            limit = float(card_art_surface["width_units"][locale])
            upgraded = item["display_name"][locale] + "+"
            units = title_units(upgraded)
            maximum_supported = limit / float(card_art_surface["minimum_scale"]) * int(card_art_surface["max_lines"])
            if units > maximum_supported:
                overflow_cards.append((card_id, locale, units, maximum_supported))
    assert not overflow_cards, overflow_cards

    assert inheritance["schema_version"] == 1
    assert inheritance["gameplay_changes"] is False
    assert inheritance["strategy"] == "explicit_override_then_original"
    assert inheritance["rules"]["unknown"] == "original"
    assert inheritance["guarantees"]["never_copy_source_title_implicitly"] is True
    assert inheritance["guarantees"]["internal_ids_unchanged"] is True
    assert inheritance["examples"][0]["card_id"] == "GIANT_ROCK"

    required_files = [
        "SasukeIronclad/scenes/runtime/card_name_preview.tscn",
        "SasukeIronclad/scripts/runtime/card_name_preview.gd",
        "SasukeIroncladCode/Visuals/CardTitleConfiguration.cs",
        "SasukeIroncladCode/Runtime/CardTitlePresentationService.cs",
    ]
    assert all((ROOT / path).exists() for path in required_files)

    preview_source = (ROOT / "SasukeIronclad/scripts/runtime/card_name_preview.gd").read_text(encoding="utf-8")
    for contract in ["_estimate_title_units", "ORIGINAL FALLBACK", "Preview upgraded (+)", "rename_status"]:
        if contract == "Preview upgraded (+)":
            scene_source = (ROOT / "SasukeIronclad/scenes/runtime/card_name_preview.tscn").read_text(encoding="utf-8")
            assert contract in scene_source
        else:
            assert contract in preview_source

    service_source = (ROOT / "SasukeIroncladCode/Runtime/CardTitlePresentationService.cs").read_text(encoding="utf-8")
    for contract in ["ICardTitleSurfaceAdapter", "original_title", "pending_local_audit", "CardTitlePresentation"]:
        assert contract in service_source
    assert "HarmonyPatch" not in service_source

    print(
        f"OK: {len(name_cards)} card titles, {len(brief_items)} art briefs, "
        f"{len(surface_items)} display surfaces, and derived-card fallback rules validated."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

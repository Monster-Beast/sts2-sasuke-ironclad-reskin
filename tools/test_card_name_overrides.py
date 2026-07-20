#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAME_MAP = ROOT / "SasukeIronclad/data/card_name_overrides.json"
CARD_MAP = ROOT / "SasukeIronclad/data/card_visual_map.json"

FORBIDDEN_GAMEPLAY_FIELDS = {
    "cost",
    "damage",
    "block",
    "description",
    "rules_text",
    "effect",
    "upgrade_effect",
    "energy",
    "rarity",
    "card_type",
}


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def normalize_locale(policy: dict, locale: str | None) -> str | None:
    if not locale:
        return policy["default_locale"]
    normalized = locale.replace("_", "-")
    if normalized.casefold().startswith("zh"):
        return "zh-CN"
    if normalized.casefold().startswith("en"):
        return "en-US"
    return None


def remove_upgrade_suffix(name: str | None) -> str | None:
    if name and name.endswith("+"):
        return name[:-1]
    return name


def apply_upgrade_suffix(name: str, upgraded: bool) -> str:
    if not upgraded or not name or name.endswith("+"):
        return name
    return f"{name}+"


def resolve(data: dict, card_id: str, locale: str | None, upgraded: bool = False, fallback: str | None = None) -> str:
    policy = data["policy"]
    cards = {item["card_id"]: item for item in data["cards"]}
    item = cards.get(card_id)
    normalized_locale = normalize_locale(policy, locale)
    fallback_name = remove_upgrade_suffix(fallback) or card_id

    if item and normalized_locale:
        names = item["display_name"]
        name = names.get(normalized_locale) or fallback_name
    else:
        name = fallback_name
    return apply_upgrade_suffix(name, upgraded)


def walk_keys(value):
    if isinstance(value, dict):
        for key, nested in value.items():
            yield key
            yield from walk_keys(nested)
    elif isinstance(value, list):
        for nested in value:
            yield from walk_keys(nested)


def main() -> int:
    data = load(NAME_MAP)
    card_map = load(CARD_MAP)
    policy = data["policy"]

    assert data["schema_version"] == 1
    assert data["gameplay_changes"] is False
    for key in [
        "display_only",
        "internal_card_id_unchanged",
        "preserve_rules_text",
        "preserve_upgrade_state",
        "fallback_to_original_name",
        "allow_derived_cards",
    ]:
        assert policy[key] is True, key

    locales = policy["supported_locales"]
    assert locales == ["zh-CN", "en-US"]
    assert policy["default_locale"] in locales

    cards = data["cards"]
    card_ids = [item["card_id"] for item in cards]
    assert len(card_ids) == len(set(card_ids))
    by_id = {item["card_id"]: item for item in cards}

    normal_ids = {item["card_id"] for item in cards if item["card_kind"] == "normal"}
    mapped_ids = {item["card_id"] for item in card_map["cards"]}
    assert normal_ids == mapped_ids

    for locale in locales:
        display_names = []
        for item in cards:
            original = item["original_name"][locale].strip()
            renamed = item["display_name"][locale].strip()
            assert original and renamed
            if item["rename_status"] == "approved":
                assert original.casefold() != renamed.casefold(), (item["card_id"], locale)
            display_names.append(renamed.casefold())
            assert item["semantic_anchor"].strip()
            assert item["art_concept"].strip()
        assert len(display_names) == len(set(display_names)), locale

    assert by_id["GIANT_ROCK"]["card_kind"] == "derived"
    assert by_id["GIANT_ROCK"]["game_id_verified"] is False
    assert by_id["GIANT_ROCK"]["display_name"]["zh-CN"] == "千鸟锐枪"
    assert by_id["GIANT_ROCK"]["display_name"]["en-US"] == "Chidori Spear"

    assert resolve(data, "Strike", "zh-CN") == "草薙·瞬斩"
    assert resolve(data, "Strike", "en-GB", upgraded=True) == "Kusanagi Flash+"
    assert resolve(data, "Strike", "ja-JP", fallback="ストライク") == "ストライク"
    assert resolve(data, "Strike", "ko-KR", upgraded=True, fallback="타격+") == "타격+"
    assert resolve(data, "UNKNOWN_CARD", "zh-CN", fallback="原始名称") == "原始名称"
    assert resolve(data, "UNKNOWN_CARD", "zh-CN", upgraded=True, fallback="原始名称+") == "原始名称+"

    resolver_source = (ROOT / "SasukeIroncladCode/Runtime/CardDisplayNameResolver.cs").read_text(encoding="utf-8")
    assert "TryNormalizeSupportedLocale" in resolver_source
    assert "RemoveUpgradeSuffix" in resolver_source
    assert "EndsWith('+')" in resolver_source

    title_service = (ROOT / "SasukeIroncladCode/Runtime/CardTitlePresentationService.cs").read_text(encoding="utf-8")
    assert "localeSupported" in title_service
    assert "originalDisplay" in title_service
    assert "catch" in title_service and "return false;" in title_service

    all_keys = set(walk_keys(data))
    assert not (all_keys & FORBIDDEN_GAMEPLAY_FIELDS), all_keys & FORBIDDEN_GAMEPLAY_FIELDS

    print(f"OK: {len(cards)} display-only card names ({len(normal_ids)} normal, {len(cards) - len(normal_ids)} derived) validated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

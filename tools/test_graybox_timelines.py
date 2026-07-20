#!/usr/bin/env python3
from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "SasukeIronclad/data/timeline_catalog.json"
MANIFEST = ROOT / "SasukeIronclad/data/card_animation_manifest.json"

ALLOWED_EVENTS = {
    "pose",
    "eye",
    "vfx",
    "advanced_vfx",
    "camera",
    "cutin",
    "impact",
    "impact_loop",
    "return_idle",
}
ALLOWED_BINDING_SOURCES = {
    "final_damage",
    "hit_count",
    "target_count",
    "energy_spent",
    "strength",
    "exhausted_card_count",
    "lethal",
}
REQUIRED_GRAYBOX = {
    "Strike",
    "Defend",
    "Bash",
    "Cleave",
    "Heavy Blade",
    "Whirlwind",
    "Fiend Fire",
}


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    catalog = load(CATALOG)
    manifest = load(MANIFEST)
    manifest_by_card = {item["card_id"]: item for item in manifest["animations"]}

    entries = catalog.get("entries", [])
    assert catalog.get("schema_version") == 1
    assert {entry["card_id"] for entry in entries} == REQUIRED_GRAYBOX
    assert len({entry["animation_id"] for entry in entries}) == len(entries)

    timelines: dict[str, dict] = {}
    for entry in entries:
        relative = entry["path"].removeprefix("res://")
        path = ROOT / relative
        assert path.exists(), f"missing timeline: {path}"
        timeline = load(path)
        timelines[entry["card_id"]] = timeline
        assert timeline["schema_version"] == 1
        assert timeline["animation_id"] == entry["animation_id"]
        assert timeline["card_id"] == entry["card_id"]
        assert manifest_by_card[entry["card_id"]]["animation_id"] == entry["animation_id"]
        events = timeline["events"]
        assert len(events) >= 5
        times = [int(event["time_ms"]) for event in events]
        assert times == sorted(times), f"unordered timeline: {entry['animation_id']}"
        assert times[-1] <= int(timeline["duration_ms"])
        assert all(event["type"] in ALLOWED_EVENTS for event in events)
        assert any(event["type"] in {"impact", "impact_loop"} for event in events)
        overrides = timeline.get("variant_overrides", {})
        assert "fast" in overrides and "low_flash" in overrides

        for event in events:
            bindings = event.get("param_bindings", {})
            for param_name, binding in bindings.items():
                assert isinstance(param_name, str) and param_name
                assert binding["source"] in ALLOWED_BINDING_SOURCES
                assert float(binding.get("min", -1e12)) <= float(binding.get("max", 1e12))
                assert event["type"] in {"vfx", "advanced_vfx", "camera", "impact_loop"}

            if event["type"] == "impact_loop":
                assert event["count_source"] == "hit_count"
                assert 0 <= int(event.get("min_count", 0)) <= int(event.get("max_count", 64)) <= 64
                assert 1 <= int(event.get("detailed_visual_count", 8)) <= 32
                assert int(event.get("fast_interval_ms", 1)) <= int(event.get("interval_ms", 1))
                assert event.get("effect")

    cleave_effects = {
        event.get("effect")
        for event in timelines["Cleave"]["events"]
        if event["type"] == "vfx"
    }
    assert {"chidori_ground_fan", "multi_target_impact"}.issubset(cleave_effects)
    cleave_bindings = [
        binding
        for event in timelines["Cleave"]["events"]
        for binding in event.get("param_bindings", {}).values()
    ]
    assert any(binding["source"] == "target_count" for binding in cleave_bindings)

    heavy = timelines["Heavy Blade"]
    assert "empowered" in heavy["variant_overrides"]
    assert any(event["type"] == "cutin" for event in heavy["events"])
    assert any(
        event["type"] == "vfx" and event.get("effect") == "focused_lightning_pillar"
        for event in heavy["events"]
    )
    heavy_bindings = [
        binding
        for event in heavy["events"]
        for binding in event.get("param_bindings", {}).values()
    ]
    assert len(heavy_bindings) >= 3
    assert all(binding["source"] == "strength" for binding in heavy_bindings)

    whirlwind = timelines["Whirlwind"]
    whirlwind_loop = next(event for event in whirlwind["events"] if event["type"] == "impact_loop")
    assert whirlwind_loop["count_source"] == "hit_count"
    assert {"energy_spent"} == {
        binding["source"]
        for binding in whirlwind_loop.get("param_bindings", {}).values()
    }
    assert any(
        event["type"] == "advanced_vfx" and event.get("effect") == "chidori_spin_ring"
        for event in whirlwind["events"]
    )

    fiend_fire = timelines["Fiend Fire"]
    fiend_loop = next(event for event in fiend_fire["events"] if event["type"] == "impact_loop")
    assert fiend_loop["count_source"] == "hit_count"
    afterimage_event = next(
        event
        for event in fiend_fire["events"]
        if event["type"] == "advanced_vfx" and event.get("effect") == "card_afterimage_orbit"
    )
    assert {
        binding["source"]
        for binding in afterimage_event.get("param_bindings", {}).values()
    } == {"exhausted_card_count"}
    assert any(event["type"] == "cutin" and event.get("style") == "dragon_fire" for event in fiend_fire["events"])

    required_files = [
        "SasukeIronclad/scenes/runtime/sasuke_character_rig.tscn",
        "SasukeIronclad/scenes/runtime/animation_director.tscn",
        "SasukeIronclad/scenes/runtime/vfx_director.tscn",
        "SasukeIronclad/scenes/runtime/advanced_vfx_director.tscn",
        "SasukeIronclad/scenes/runtime/camera_effect_director.tscn",
        "SasukeIronclad/scenes/runtime/cutin_director.tscn",
        "SasukeIronclad/scenes/runtime/graybox_preview.tscn",
        "SasukeIronclad/scripts/runtime/sasuke_character_rig.gd",
        "SasukeIronclad/scripts/runtime/animation_director.gd",
        "SasukeIronclad/scripts/runtime/vfx_director.gd",
        "SasukeIronclad/scripts/runtime/advanced_vfx_director.gd",
        "SasukeIronclad/scripts/runtime/camera_effect_director.gd",
        "SasukeIronclad/scripts/runtime/cutin_director.gd",
        "SasukeIronclad/scripts/runtime/graybox_preview.gd",
    ]
    assert all((ROOT / path).exists() for path in required_files)

    director_source = (ROOT / "SasukeIronclad/scripts/runtime/animation_director.gd").read_text(encoding="utf-8")
    assert "_execute_impact_loop" in director_source
    assert "_wait_for_original_impact" in director_source
    assert "detailed_visual_count" in director_source

    print("OK: 7 graybox timelines, authoritative impact loops, card-local bindings, and preview runtime validated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

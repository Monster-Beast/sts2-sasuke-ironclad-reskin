#!/usr/bin/env python3
from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "SasukeIronclad/data/timeline_catalog.json"
MANIFEST = ROOT / "SasukeIronclad/data/card_animation_manifest.json"

ALLOWED_EVENTS = {
    "pose", "eye", "vfx", "advanced_vfx", "camera", "cutin",
    "impact", "impact_loop", "state_install", "state_pulse",
    "state_clear", "return_idle",
}
ALLOWED_BINDING_SOURCES = {
    "final_damage", "hit_count", "target_count", "energy_spent",
    "strength", "exhausted_card_count", "lethal",
}
REQUIRED_GRAYBOX = {
    "Strike", "Defend", "Bash", "Anger", "Cleave", "Thunderclap",
    "Heavy Blade", "Flame Barrier", "Whirlwind", "Burning Pact",
    "Limit Break", "Fiend Fire",
}


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def event_effects(timeline: dict, event_type: str = "advanced_vfx") -> set[str]:
    return {
        event.get("effect")
        for event in timeline["events"]
        if event["type"] == event_type and event.get("effect")
    }


def bindings(timeline: dict) -> list[dict]:
    return [
        binding
        for event in timeline["events"]
        for binding in event.get("param_bindings", {}).values()
    ]


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
        path = ROOT / entry["path"].removeprefix("res://")
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
            for param_name, binding in event.get("param_bindings", {}).items():
                assert isinstance(param_name, str) and param_name
                assert binding["source"] in ALLOWED_BINDING_SOURCES
                assert float(binding.get("min", -1e12)) <= float(binding.get("max", 1e12))
                assert event["type"] in {
                    "vfx", "advanced_vfx", "camera", "impact_loop", "state_install"
                }
            if event["type"] == "impact_loop":
                assert event["count_source"] == "hit_count"
                assert 0 <= int(event.get("min_count", 0)) <= int(event.get("max_count", 64)) <= 64
                assert 1 <= int(event.get("detailed_visual_count", 8)) <= 32
                assert int(event.get("fast_interval_ms", 1)) <= int(event.get("interval_ms", 1))
                assert event.get("effect")
            if event["type"].startswith("state_"):
                assert event.get("state_id"), f"missing state_id in {entry['animation_id']}"

    assert {
        "afterimage_split", "shuriken_fan_launch", "shuriken_cross_impact"
    }.issubset(event_effects(timelines["Anger"]))
    assert sum(event["type"] == "impact" for event in timelines["Anger"]["events"]) == 1

    assert {"chidori_ground_fan", "multi_target_impact"}.issubset(
        event_effects(timelines["Cleave"], "vfx")
    )
    assert any(binding["source"] == "target_count" for binding in bindings(timelines["Cleave"]))

    assert {"chidori_ring_burst", "vulnerable_marks"}.issubset(
        event_effects(timelines["Thunderclap"])
    )
    assert all(binding["source"] == "target_count" for binding in bindings(timelines["Thunderclap"]))

    heavy = timelines["Heavy Blade"]
    assert "empowered" in heavy["variant_overrides"]
    assert any(event["type"] == "cutin" for event in heavy["events"])
    assert "focused_lightning_pillar" in event_effects(heavy, "vfx")
    assert all(binding["source"] == "strength" for binding in bindings(heavy))

    flame = timelines["Flame Barrier"]
    flame_installs = [event for event in flame["events"] if event["type"] == "state_install"]
    assert len(flame_installs) == 1
    assert flame_installs[0]["state_id"] == "flame_barrier_guard"
    assert flame_installs[0]["style"] == "fire_guard"
    assert any(event["type"] == "state_pulse" for event in flame["events"])
    assert not any(event["type"] == "state_clear" for event in flame["events"])
    assert {"fire_seal_sparks", "fire_half_ring"}.issubset(event_effects(flame))

    whirlwind_loop = next(event for event in timelines["Whirlwind"]["events"] if event["type"] == "impact_loop")
    assert whirlwind_loop["count_source"] == "hit_count"
    assert {binding["source"] for binding in whirlwind_loop.get("param_bindings", {}).values()} == {"energy_spent"}

    pact = timelines["Burning Pact"]
    pact_state_ids = [event["state_id"] for event in pact["events"] if event["type"].startswith("state_")]
    assert pact_state_ids.count("burning_pact_channel") >= 3
    assert any(event["type"] == "state_clear" for event in pact["events"])
    assert {
        "curse_mark_spread", "pact_card_afterimages", "pact_consumption_burst", "chakra_return"
    }.issubset(event_effects(pact))
    assert {binding["source"] for binding in bindings(pact)} == {"exhausted_card_count"}

    limit_break = timelines["Limit Break"]
    limit_install = next(event for event in limit_break["events"] if event["type"] == "state_install")
    assert limit_install["state_id"] == "limit_break_overdrive"
    assert limit_install["style"] == "curse_overdrive"
    assert "empowered" in limit_break["variant_overrides"]
    assert {binding["source"] for binding in bindings(limit_break)} == {"strength"}
    assert {"sharingan_overdrive", "power_afterimages"}.issubset(event_effects(limit_break))

    fiend_loop = next(event for event in timelines["Fiend Fire"]["events"] if event["type"] == "impact_loop")
    assert fiend_loop["count_source"] == "hit_count"
    afterimages = next(
        event for event in timelines["Fiend Fire"]["events"]
        if event["type"] == "advanced_vfx" and event.get("effect") == "card_afterimage_orbit"
    )
    assert {binding["source"] for binding in afterimages.get("param_bindings", {}).values()} == {"exhausted_card_count"}

    required_files = [
        "SasukeIronclad/scenes/runtime/sasuke_character_rig.tscn",
        "SasukeIronclad/scenes/runtime/animation_director.tscn",
        "SasukeIronclad/scenes/runtime/vfx_director.tscn",
        "SasukeIronclad/scenes/runtime/advanced_vfx_director.tscn",
        "SasukeIronclad/scenes/runtime/state_visual_director.tscn",
        "SasukeIronclad/scenes/runtime/camera_effect_director.tscn",
        "SasukeIronclad/scenes/runtime/cutin_director.tscn",
        "SasukeIronclad/scenes/runtime/graybox_preview.tscn",
        "SasukeIronclad/scenes/runtime/runtime_stress_test.tscn",
        "SasukeIronclad/scenes/runtime/combat_resource_release_test.tscn",
        "SasukeIronclad/scripts/runtime/sasuke_character_rig.gd",
        "SasukeIronclad/scripts/runtime/sasuke_stateful_character_rig.gd",
        "SasukeIronclad/scripts/runtime/animation_director.gd",
        "SasukeIronclad/scripts/runtime/stateful_animation_director.gd",
        "SasukeIronclad/scripts/runtime/vfx_director.gd",
        "SasukeIronclad/scripts/runtime/advanced_vfx_director.gd",
        "SasukeIronclad/scripts/runtime/stateful_advanced_vfx_director.gd",
        "SasukeIronclad/scripts/runtime/state_visual_director.gd",
        "SasukeIronclad/scripts/runtime/combat_resource_release_test.gd",
    ]
    assert all((ROOT / path).exists() for path in required_files)

    base_director = (ROOT / "SasukeIronclad/scripts/runtime/animation_director.gd").read_text(encoding="utf-8")
    assert "_execute_impact_loop" in base_director
    assert "_wait_for_original_impact" in base_director

    state_director = (ROOT / "SasukeIronclad/scripts/runtime/stateful_animation_director.gd").read_text(encoding="utf-8")
    for contract in [
        "commit_generation", "rollback_generation", "release_combat_resources",
        "state_install", "state_pulse", "state_clear",
    ]:
        assert contract in state_director

    state_runtime = (ROOT / "SasukeIronclad/scripts/runtime/state_visual_director.gd").read_text(encoding="utf-8")
    for contract in ["active_state_count", "clear_all", "queue_free", "committed"]:
        assert contract in state_runtime

    release_test = (ROOT / "SasukeIronclad/scripts/runtime/combat_resource_release_test.gd").read_text(encoding="utf-8")
    for contract in [
        "flame_barrier_guard", "limit_break_overdrive", "burning_pact_channel",
        "release_combat_resources", "RELEASE_OK", "get_tree().quit(0)", "get_tree().quit(1)",
    ]:
        assert contract in release_test

    print("OK: 12 graybox timelines, persistent-state lifecycle, impact loops, and release contracts validated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

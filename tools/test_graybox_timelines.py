#!/usr/bin/env python3
from __future__ import annotations
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "SasukeIronclad/data/timeline_catalog.json"
MANIFEST = ROOT / "SasukeIronclad/data/card_animation_manifest.json"

ALLOWED_EVENTS = {"pose", "eye", "vfx", "camera", "impact", "return_idle"}
REQUIRED_GRAYBOX = {"Strike", "Defend", "Bash"}


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

    for entry in entries:
        relative = entry["path"].removeprefix("res://")
        path = ROOT / relative
        assert path.exists(), f"missing timeline: {path}"
        timeline = load(path)
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
        assert sum(1 for event in events if event["type"] == "impact") >= 1
        overrides = timeline.get("variant_overrides", {})
        assert "fast" in overrides and "low_flash" in overrides

    required_files = [
        "SasukeIronclad/scenes/runtime/sasuke_character_rig.tscn",
        "SasukeIronclad/scenes/runtime/animation_director.tscn",
        "SasukeIronclad/scenes/runtime/vfx_director.tscn",
        "SasukeIronclad/scenes/runtime/camera_effect_director.tscn",
        "SasukeIronclad/scenes/runtime/graybox_preview.tscn",
        "SasukeIronclad/scripts/runtime/sasuke_character_rig.gd",
        "SasukeIronclad/scripts/runtime/animation_director.gd",
        "SasukeIronclad/scripts/runtime/vfx_director.gd",
        "SasukeIronclad/scripts/runtime/camera_effect_director.gd",
        "SasukeIronclad/scripts/runtime/graybox_preview.gd",
    ]
    assert all((ROOT / path).exists() for path in required_files)
    print("OK: 3 graybox timelines and the runnable preview scene validated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

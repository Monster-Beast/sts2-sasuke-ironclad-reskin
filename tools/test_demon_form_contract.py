#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TIMELINE = ROOT / "SasukeIronclad/data/timelines/demon_form_curse_mark_stage_two.json"


def main() -> int:
    timeline = json.loads(TIMELINE.read_text(encoding="utf-8"))
    events = timeline["events"]

    assert timeline["animation_id"] == "demon_form_curse_mark_stage_two"
    assert timeline["card_id"] == "Demon Form"
    assert {"fast", "low_flash"}.issubset(timeline["variant_overrides"])

    form_install = next(event for event in events if event["type"] == "form_install")
    state_install = next(event for event in events if event["type"] == "state_install")
    assert form_install["form_id"] == "curse_mark_stage_two"
    assert state_install["state_id"] == "demon_form_stage_two_aura"
    assert state_install["style"] == "curse_stage_two_aura"
    assert any(event["type"] == "cutin" and event.get("style") == "curse_stage_two" for event in events)
    assert not any(event["type"] == "form_clear" for event in events)

    required_sources = {
        "SasukeIronclad/scripts/runtime/transformable_character_rig.gd": [
            "extends SasukeStatefulGrayboxRig",
            "curse_mark_stage_two",
            "demon_transform_reveal",
            "demon_idle",
            "reset_to_idle",
        ],
        "SasukeIronclad/scripts/runtime/form_visual_director.gd": [
            "install_form",
            "commit_generation",
            "rollback_generation",
            "clear_form",
            "active_form_id",
        ],
        "SasukeIronclad/scripts/runtime/stateful_animation_director.gd": [
            "form_install",
            "form_clear",
            "form_visual_director.commit_generation",
            "form_visual_director.rollback_generation",
            "form_visual_director.clear_all",
        ],
        "SasukeIronclad/scripts/runtime/demon_form_advanced_vfx_director.gd": [
            "curse_mark_consume",
            "_spawn_curse_mark_consume",
            "_active_effects.append",
            "_release_effect",
        ],
        "SasukeIronclad/scripts/runtime/form_lifecycle_test.gd": [
            "demon_form_curse_mark_stage_two",
            "curse_mark_stage_two",
            "demon_form_stage_two_aura",
            "FORM_OK",
            "release_combat_resources",
        ],
        "SasukeIroncladCode/Runtime/CombatVisualLifecycleCoordinator.cs": [
            "VisualRemovalRequested",
            "CombatEnded",
            "ClearVisualState",
            "ClearVisualForm",
            "ReleaseCombatResources",
        ],
    }

    for relative, contracts in required_sources.items():
        source = (ROOT / relative).read_text(encoding="utf-8")
        for contract in contracts:
            assert contract in source, f"{relative}: missing {contract}"

    required_files = [
        "SasukeIronclad/scenes/runtime/form_visual_director.tscn",
        "SasukeIronclad/scenes/runtime/form_lifecycle_test.tscn",
        "SasukeIronclad/scripts/runtime/transform_state_visual_director.gd",
        "SasukeIroncladCode/Adapters/ICombatVisualLifecycleSource.cs",
        "SasukeIroncladCode/Adapters/VisualRemovalRequest.cs",
    ]
    assert all((ROOT / path).exists() for path in required_files)

    print("OK: Demon Form transaction, alternate idle, removal adapter, and cleanup contracts validated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

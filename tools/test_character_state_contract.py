#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def main() -> int:
    required = [
        "SasukeIronclad/scripts/runtime/character_state_rig.gd",
        "SasukeIronclad/scripts/runtime/character_state_director.gd",
        "SasukeIronclad/scripts/runtime/character_state_runtime.gd",
        "SasukeIronclad/scripts/runtime/character_state_machine_test.gd",
        "SasukeIronclad/scenes/runtime/character_state_director.tscn",
        "SasukeIronclad/scenes/runtime/character_state_machine_test.tscn",
        "SasukeIroncladCode/Adapters/CharacterVisualRequest.cs",
        "SasukeIroncladCode/Adapters/ICharacterVisualEventSource.cs",
        "SasukeIroncladCode/Runtime/CharacterVisualEventCoordinator.cs",
    ]
    assert all((ROOT / path).exists() for path in required)

    base_rig = read("SasukeIronclad/scripts/runtime/sasuke_stateful_character_rig.gd")
    transform_rig = read("SasukeIronclad/scripts/runtime/transformable_character_rig.gd")
    state_rig = read("SasukeIronclad/scripts/runtime/character_state_rig.gd")
    rig_scene = read("SasukeIronclad/scenes/runtime/sasuke_character_rig.tscn")

    assert "extends SasukeGrayboxRig" in base_rig
    assert "extends SasukeStatefulGrayboxRig" in transform_rig
    assert "extends SasukeTransformableRig" in state_rig
    assert "character_state_rig.gd" in rig_scene

    for pose in [
        "combat_entry_start",
        "combat_entry_land",
        "idle_sharingan_alert",
        "demon_idle_sharingan_alert",
        "hit_light",
        "hit_heavy",
        "demon_hit_light",
        "demon_hit_heavy",
        "death_ready",
        "death",
        "demon_death_ready",
        "demon_death",
        "victory_sheathe",
        "demon_victory_sheathe",
    ]:
        assert f'"{pose}"' in state_rig, pose

    state_director = read("SasukeIronclad/scripts/runtime/character_state_director.gd")
    runtime_extension = read("SasukeIronclad/scripts/runtime/character_state_runtime.gd")
    runtime_scene = read("SasukeIronclad/scenes/runtime/animation_director.tscn")
    timeline_director = read("SasukeIronclad/scripts/runtime/stateful_animation_director.gd")

    for state_id in [
        "combat_entry",
        "idle_neutral",
        "idle_sword_ready",
        "idle_sharingan_alert",
        "hit_light",
        "hit_heavy",
        "victory",
        "death",
    ]:
        assert f'"{state_id}"' in state_director, state_id

    assert state_director.index('"death": 100') > state_director.index('"victory": 80')
    assert "_restart_idle_loop" in state_director
    assert "_rng.randf_range(2.8, 5.2)" in state_director
    assert "resolve_character_state_pose" in state_director
    assert "character_terminal" in timeline_director
    assert "set_card_animation_active(true)" in timeline_director
    assert "set_card_animation_active(false)" in timeline_director
    assert "character_state_director.release_combat_resources()" in timeline_director
    assert "play_character_state" in timeline_director
    assert "CharacterStateDirector" in runtime_scene
    assert "character_state_director_path" in runtime_scene

    assert "camera_director.configure(rig, false)" in runtime_extension
    assert '_current_state == "victory"' in runtime_extension
    assert 'normalized == "death"' in runtime_extension
    assert 'forced_params["force"] = true' in runtime_extension

    request = read("SasukeIroncladCode/Adapters/CharacterVisualRequest.cs")
    source = read("SasukeIroncladCode/Adapters/ICharacterVisualEventSource.cs")
    coordinator = read("SasukeIroncladCode/Runtime/CharacterVisualEventCoordinator.cs")
    visual_host = read("SasukeIroncladCode/Adapters/IVisualSceneHost.cs")
    godot_host = read("SasukeIroncladCode/Adapters/GodotVisualSceneHost.cs")

    for state_name in [
        "CombatEntry",
        "IdleNeutral",
        "IdleSwordReady",
        "IdleSharinganAlert",
        "HitLight",
        "HitHeavy",
        "Death",
        "Victory",
    ]:
        assert state_name in request
    assert "CharacterVisualRequested" in source
    assert "PlayCharacterState" in coordinator
    assert "PlayCharacterState(CharacterVisualRequest request)" in visual_host
    assert '"play_character_state"' in godot_host
    assert "Math.Clamp(request.Intensity, 0.4f, 2.0f)" in godot_host

    new_cs_sources = "\n".join([request, source, coordinator, visual_host, godot_host])
    assert "[HarmonyPatch" not in new_cs_sources

    test_source = read("SasukeIronclad/scripts/runtime/character_state_machine_test.gd")
    for contract in [
        "combat_entry",
        "hit_light",
        "hit_heavy",
        "demon_form_curse_mark_stage_two",
        "victory",
        "death",
        "release_combat_resources",
        "CHAR_STATE_OK",
        "get_tree().quit(0)",
        "get_tree().quit(1)",
    ]:
        assert contract in test_source

    print("OK: form-aware entry, idle, hit, death, victory, and semantic adapter contracts validated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

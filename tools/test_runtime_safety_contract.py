#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8")


def main() -> int:
    catalog = json.loads(read("SasukeIronclad/data/timeline_catalog.json"))
    entries = catalog["entries"]

    for entry in entries:
        relative = entry["path"].removeprefix("res://")
        path = ROOT / relative
        assert path.exists(), path
        assert path.stem == entry["animation_id"], (path, entry["animation_id"])
        timeline = json.loads(path.read_text(encoding="utf-8"))
        assert timeline["animation_id"] == entry["animation_id"]
        assert timeline["card_id"] == entry["card_id"]

    defend = next(entry for entry in entries if entry["card_id"] == "Defend")
    assert defend["animation_id"] == "defend_wire_parry_guard"
    assert defend["path"].endswith("/defend_wire_parry_guard.json")
    assert "defend_wire_parry_stance.json" not in json.dumps(catalog)

    playback = read("SasukeIroncladCode/Runtime/CardVisualPlaybackService.cs")
    assert "_activeByCard" not in playback
    assert "private AnimationPlaybackHandle? _activeHandle" in playback
    assert "private AnimationContext? _activeContext" in playback
    assert playback.index("ReleaseActive();") < playback.index("CardAnimationSelector.TrySelect")
    assert "string.Equals(cardId, context.CardId, StringComparison.Ordinal)" in playback
    assert "impactIndex < 0" in playback
    assert "finally" in playback and "handle.MarkReleased();" in playback
    assert "IVisualSceneHostNotifications" in playback
    assert "PlaybackCompleted += OnPlaybackCompleted" in playback
    assert "PlaybackFailed += OnPlaybackFailed" in playback
    assert "asynchronous visual playback failed" in playback
    assert "IDisposable" in playback
    assert "SafeFallback" in playback

    notifications = read("SasukeIroncladCode/Adapters/IVisualSceneHostNotifications.cs")
    assert "event Action<AnimationPlaybackHandle>? PlaybackCompleted" in notifications
    assert "event Action<AnimationPlaybackHandle, string>? PlaybackFailed" in notifications

    host = read("SasukeIroncladCode/Adapters/GodotVisualSceneHost.cs")
    assert "IVisualSceneHostNotifications" in host
    assert "Dictionary<Guid, AnimationPlaybackHandle>" not in host
    assert "private AnimationPlaybackHandle? _activeHandle" in host
    assert "timeline_completed" in host
    assert "timeline_failed" in host
    assert "Callable.From<string>(OnTimelineCompleted)" in host
    assert "Callable.From<string, string>(OnTimelineFailed)" in host
    assert "PlaybackCompleted?.Invoke(handle)" in host
    assert "PlaybackFailed?.Invoke(handle, failureReason)" in host
    assert "RetireCompletedHandle" in host
    assert "ReleaseActiveHandle(cancelDirector: true)" in host
    assert "handle.MarkReleased();" in host
    assert "finally" in host and "_runtimeRoot?.QueueFree();" in host
    assert '["fast_mode"] = selection.FastMode' in host
    assert '["low_flash"] = selection.LowFlashMode' in host

    director = read("SasukeIronclad/scripts/runtime/animation_director.gd")
    assert "func _fail_active_timeline" in director
    assert "return -2 if generation != _play_generation else -1" in director
    reset_index = director.index("await rig.reset_to_idle")
    completion_index = director.index("timeline_completed.emit", reset_index)
    generation_check_index = director.index("if generation != _play_generation", reset_index)
    assert reset_index < generation_check_index < completion_index
    assert "_is_fast_mode" in director
    assert "_is_low_flash_mode" in director
    assert "content_scale * fast_scale" in director

    stateful = read("SasukeIronclad/scripts/runtime/stateful_animation_director.gd")
    assert "var _transaction_counter := 0" in stateful
    assert "var _active_transaction := -1" in stateful
    assert "commit_generation(transaction)" in stateful
    assert "rollback_generation(transaction)" in stateful
    assert "commit_generation(_play_generation)" not in stateful
    assert "rollback_generation(_play_generation)" not in stateful
    assert "resolve_character_state_pose(\"idle_sword_ready\")" in stateful

    state_runtime = read("SasukeIronclad/scripts/runtime/state_visual_director.gd")
    assert '"had_previous": true' in state_runtime
    assert '"previous_form_id"' not in state_runtime
    assert "previous_anchor" in state_runtime
    assert "_create_state(" in state_runtime
    assert "cancelled replacement removed the previous Flame Barrier state" in read(
        "SasukeIronclad/scripts/runtime/combat_resource_release_test.gd"
    )

    selector = read("SasukeIroncladCode/Runtime/CardAnimationSelector.cs")
    assert "SelectContentVariant" in selector
    assert "FastMode = context.FastMode" in selector
    assert "LowFlashMode = context.LowFlashMode" in selector
    assert "return CardAnimationVariant.Fast" not in selector
    assert "return CardAnimationVariant.LowFlash" not in selector

    print(
        "OK: single-active playback, asynchronous failure fallback, host handle "
        "retirement, generation-safe completion, layered accessibility, state "
        "replacement rollback, independent state/Form transactions, and stable "
        "timeline paths validated."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

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

    # Every active timeline resource must use the stable animation ID as its
    # filename. This catches the historical Defend guard/stance mismatch.
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
    assert "ReleaseActive();" in playback
    assert playback.index("ReleaseActive();") < playback.index("CardAnimationSelector.TrySelect")
    assert "string.Equals(cardId, context.CardId, StringComparison.Ordinal)" in playback
    assert "impactIndex < 0" in playback
    assert "finally" in playback and "handle.MarkReleased();" in playback
    assert "SafeFallback" in playback

    host = read("SasukeIroncladCode/Adapters/GodotVisualSceneHost.cs")
    assert "Dictionary<Guid, AnimationPlaybackHandle>" not in host
    assert "private AnimationPlaybackHandle? _activeHandle" in host
    assert "timeline_completed" in host
    assert "timeline_failed" in host
    assert "Callable.From<string>(OnTimelineCompleted)" in host
    assert "Callable.From<string, string>(OnTimelineFailed)" in host
    assert "RetireCompletedHandle" in host
    assert "ReleaseActiveHandle(cancelDirector: true)" in host
    assert "handle.MarkReleased();" in host
    assert "finally" in host and "_runtimeRoot?.QueueFree();" in host

    director = read("SasukeIronclad/scripts/runtime/animation_director.gd")
    assert "func _fail_active_timeline" in director
    assert "return -2 if generation != _play_generation else -1" in director
    assert "A same-card request can start while the old coroutine is returning to idle" in director
    reset_index = director.index("await rig.reset_to_idle")
    completion_index = director.index("timeline_completed.emit", reset_index)
    generation_check_index = director.index("if generation != _play_generation", reset_index)
    assert reset_index < generation_check_index < completion_index

    stateful = read("SasukeIronclad/scripts/runtime/stateful_animation_director.gd")
    assert "var _transaction_counter := 0" in stateful
    assert "var _active_transaction := -1" in stateful
    assert "commit_generation(transaction)" in stateful
    assert "rollback_generation(transaction)" in stateful
    assert "commit_generation(_play_generation)" not in stateful
    assert "rollback_generation(_play_generation)" not in stateful
    assert "_active_transaction" in stateful
    assert "resolve_character_state_pose(\"idle_sword_ready\")" in stateful

    print(
        "OK: single-active playback, host handle retirement, generation-safe "
        "timeline completion, independent state/Form transactions, and stable "
        "timeline paths validated."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

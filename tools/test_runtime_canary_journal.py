#!/usr/bin/env python3
from __future__ import annotations

import json
import tempfile
from pathlib import Path

from analyze_runtime_canary_journal import analyze_files

ROOT = Path(__file__).resolve().parents[1]


def write_events(path: Path, events: list[dict]) -> None:
    path.write_text(
        "".join(json.dumps(event, separators=(",", ":")) + "\n" for event in events),
        encoding="utf-8",
    )


def event(sequence: int, event_type: str, **values: object) -> dict:
    document = {
        "schema_version": 1,
        "generated_at_utc": "2026-07-22T00:00:00+00:00",
        "session_id": "combined-ci-20260722T000000000Z-abcdef",
        "sequence": sequence,
        "combat_index": values.pop("combat_index", 0),
        "event_type": event_type,
    }
    document.update(values)
    return document


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="sasuke-canary-journal-") as temporary:
        root = Path(temporary)
        success_path = root / "success.jsonl"
        success_events = [
            event(1, "session_start", animations_enabled=True, titles_enabled=True, replacement_requested=True),
            event(2, "title_applied", card_id="Strike", surface_id="hand"),
            event(3, "combat_started", combat_index=1, card_id="Strike"),
            event(4, "anchor_bound", combat_index=1),
            event(5, "playback_started", combat_index=1, card_id="Strike", animation_id="strike_kusanagi_draw_slash"),
            event(6, "replacement_hidden", combat_index=1, replacement_active=True),
            event(7, "original_impact_forwarded", combat_index=1, card_id="Strike", impact_index=0),
            event(8, "playback_completed", combat_index=1, card_id="Strike"),
            event(9, "unreviewed_card_fallback", combat_index=1, reason="card_not_in_reviewed_replacement_scope"),
            event(10, "replacement_restored", combat_index=1, replacement_active=False, restore_count=1),
            event(11, "combat_ended", combat_index=1),
            event(12, "session_stop"),
        ]
        write_events(success_path, success_events)
        success = analyze_files([success_path], require_combined=True)
        assert success["status"] == "passed", success
        assert success["sessions"][0]["expected_fallback_count"] == 1
        assert success["sessions"][0]["hard_failure_count"] == 0
        assert success["sessions"][0]["title_surfaces"] == ["hand"]
        assert success["sessions"][0]["animation_cards"] == ["Strike"]

        failure_path = root / "failure.jsonl"
        failure_events = success_events[:-1] + [
            event(12, "playback_fallback", combat_index=1, card_id="Strike", reason="original_impact_timeout"),
            event(13, "session_stop"),
        ]
        write_events(failure_path, failure_events)
        failure = analyze_files([failure_path], require_combined=True)
        assert failure["status"] == "partial", failure
        assert failure["sessions"][0]["hard_failure_count"] == 1

        invalid_path = root / "invalid.jsonl"
        invalid_events = [
            event(1, "session_start", animations_enabled=True, titles_enabled=True),
            event(3, "title_applied", card_id="C:\\Users\\private\\card", surface_id="hand"),
        ]
        write_events(invalid_path, invalid_events)
        invalid = analyze_files([invalid_path], require_combined=True)
        assert invalid["status"] == "invalid", invalid
        assert any("absolute local path" in error for error in invalid["errors"])
        assert any("sequence is not contiguous" in error for error in invalid["errors"])

    checkpoint_source = (ROOT / "tools/runtime-canary-checkpoint.ps1").read_text(encoding="ascii")
    assert '$statusText.Replace($resolvedModDirectory, "<MOD_PATH>")' in checkpoint_source
    assert '$statusText.Replace($resolvedGamePath, "<GAME_PATH>")' in checkpoint_source
    assert '"[]" + [Environment]::NewLine' in checkpoint_source
    assert "ExecutablePath" not in checkpoint_source.split("Select-Object", 1)[1].split(")", 1)[0]

    print(
        "RUNTIME_CANARY_JOURNAL_TEST_OK combined=true fallback_classified=true "
        "redaction=true checkpoint_redaction=true sequence=true"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

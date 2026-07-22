#!/usr/bin/env python3
from __future__ import annotations

import json
import tempfile
from pathlib import Path

from analyze_runtime_canary_journal import ALL_TITLE_SURFACES, analyze_files

ROOT = Path(__file__).resolve().parents[1]
REVIEW_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-combined-presentation-review.json"
DOC_PATH = ROOT / "docs/technical/runtime-combined-presentation-canary.md"
EXPECTED_ARCHIVE_HASH = "a483b1a9b4de1745a2d6d65e58d10961be0a03fd3592f8bd3e2f509f004ef951"
EXPECTED_JOURNAL_HASH = "6f76c9d51576d9696a2734e77888deaa29a17f3f6a19500349f51d10619ca4fc"


def write_events(path: Path, events: list[dict]) -> None:
    path.write_text(
        "".join(json.dumps(event, separators=(",", ":")) + "\n" for event in events),
        encoding="utf-8",
    )


def write_checkpoint(
    root: Path,
    event_file: str,
    *,
    checkpoint_name: str = "04-clean-process-exit",
    process_query_succeeded: bool | None = True,
    processes: list[dict] | None = None,
) -> Path:
    directory = root / checkpoint_name
    directory.mkdir(parents=True, exist_ok=True)
    process_document = processes or []
    (directory / "process.json").write_text(
        json.dumps(process_document, indent=2) + "\n",
        encoding="utf-8",
    )
    checkpoint = {
        "schema_version": 1,
        "generated_at_utc": "2026-07-22T00:01:00+00:00",
        "name": checkpoint_name,
        "process_count": len(process_document),
        "game_process_count": sum(
            1
            for process in process_document
            if str(process.get("Name") or "").casefold() == "slaythespire2.exe"
        ),
        "event_file": event_file,
        "journal_event_count": 11,
        "journal_last_sequence": 11,
        "journal_last_event_type": "combat_ended",
        "copied_files": ["checkpoint.json", "process.json", event_file],
    }
    if process_query_succeeded is not None:
        checkpoint["process_query_succeeded"] = process_query_succeeded
    (directory / "checkpoint.json").write_text(
        json.dumps(checkpoint, indent=2) + "\n",
        encoding="utf-8",
    )
    return directory


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


def combined_events(*, include_session_stop: bool, include_all_surfaces: bool) -> list[dict]:
    events = [
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
    ]
    if include_all_surfaces:
        for surface in sorted(ALL_TITLE_SURFACES.difference({"hand"})):
            events.append(
                event(
                    len(events) + 1,
                    "title_applied",
                    card_id="Strike",
                    surface_id=surface,
                )
            )
    if include_session_stop:
        events.append(event(len(events) + 1, "session_stop"))
    return events


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="sasuke-canary-journal-") as temporary:
        root = Path(temporary)

        runtime_stop_path = root / "runtime-stop.jsonl"
        write_events(
            runtime_stop_path,
            combined_events(include_session_stop=True, include_all_surfaces=True),
        )
        runtime_stop = analyze_files(
            [runtime_stop_path],
            require_combined=True,
            required_title_surfaces=set(ALL_TITLE_SURFACES),
        )
        assert runtime_stop["status"] == "passed", runtime_stop
        assert runtime_stop["sessions"][0]["closure"]["mode"] == "runtime_session_stop"
        assert runtime_stop["sessions"][0]["expected_fallback_count"] == 1
        assert runtime_stop["sessions"][0]["hard_failure_count"] == 0

        checkpoint_path = root / "checkpoint-close.jsonl"
        checkpoint_events = combined_events(include_session_stop=False, include_all_surfaces=True)
        write_events(checkpoint_path, checkpoint_events)
        checkpoint_root = root / "checkpoint-evidence"
        write_checkpoint(checkpoint_root, checkpoint_path.name)
        checkpoint_close = analyze_files(
            [checkpoint_path],
            require_combined=True,
            checkpoint_roots=[checkpoint_root],
            required_title_surfaces=set(ALL_TITLE_SURFACES),
        )
        assert checkpoint_close["status"] == "passed", checkpoint_close
        checkpoint_session = checkpoint_close["sessions"][0]
        assert checkpoint_session["session_stop_count"] == 0
        assert checkpoint_session["closure"]["mode"] == "clean_process_checkpoint"
        assert checkpoint_session["closure"]["checkpoint_names"] == ["04-clean-process-exit"]
        assert checkpoint_close["warnings"]

        open_path = root / "open.jsonl"
        write_events(open_path, combined_events(include_session_stop=False, include_all_surfaces=True))
        open_result = analyze_files(
            [open_path],
            require_combined=True,
            required_title_surfaces=set(ALL_TITLE_SURFACES),
        )
        assert open_result["status"] == "partial", open_result
        assert open_result["sessions"][0]["requirements"]["session_closed"] is False

        surface_path = root / "surface-partial.jsonl"
        write_events(surface_path, combined_events(include_session_stop=True, include_all_surfaces=False))
        surface_result = analyze_files(
            [surface_path],
            require_combined=True,
            required_title_surfaces=set(ALL_TITLE_SURFACES),
        )
        assert surface_result["status"] == "partial", surface_result
        assert set(surface_result["sessions"][0]["missing_title_surfaces"]) == ALL_TITLE_SURFACES.difference({"hand"})

        failure_path = root / "failure.jsonl"
        failure_events = combined_events(include_session_stop=False, include_all_surfaces=True) + [
            event(17, "playback_fallback", combat_index=1, card_id="Strike", reason="original_impact_timeout"),
            event(18, "session_stop"),
        ]
        write_events(failure_path, failure_events)
        failure = analyze_files(
            [failure_path],
            require_combined=True,
            required_title_surfaces=set(ALL_TITLE_SURFACES),
        )
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
    analyzer_source = (ROOT / "tools/analyze_runtime_canary_journal.py").read_text(encoding="utf-8")
    documentation = DOC_PATH.read_text(encoding="utf-8")
    review = json.loads(REVIEW_PATH.read_text(encoding="utf-8"))

    assert '$statusText.Replace($resolvedModDirectory, "<MOD_PATH>")' in checkpoint_source
    assert '$statusText.Replace($resolvedGamePath, "<GAME_PATH>")' in checkpoint_source
    assert '"[]" + [Environment]::NewLine' in checkpoint_source
    assert "process_query_succeeded" in checkpoint_source
    assert "game_process_count" in checkpoint_source
    assert "journal_last_event_type" in checkpoint_source
    assert "ExecutablePath" not in checkpoint_source.split("Select-Object", 1)[1].split(")", 1)[0]
    assert '"--checkpoint-root"' in analyzer_source
    assert '"--require-all-title-surfaces"' in analyzer_source
    assert '"clean_process_checkpoint"' in analyzer_source
    assert "--checkpoint-root" in documentation
    assert "reward", "compendium"

    assert review["schema_version"] == 1
    assert review["status"] == "combined_core_passed_with_exit_and_surface_followup"
    assert review["source"]["archive_sha256"] == EXPECTED_ARCHIVE_HASH
    assert review["session"]["journal_sha256"] == EXPECTED_JOURNAL_HASH
    assert review["session"]["event_count"] == 38
    assert set(review["title_layer"]["observed_surfaces"]) == {"card_art", "deck_list", "hand"}
    assert set(review["title_layer"]["pending_combined_surfaces"]) == {"compendium", "reward", "tooltip"}
    assert review["fallback_classification"]["hard_failure_count"] == 0
    assert review["checkpoints"]["clean_exit_process_count"] == 0
    assert review["checkpoints"]["external_clean_process_exit_evidence_passed"] is True
    assert review["conclusions"]["combined_title_and_animation_coexistence_passed"] is True
    assert review["conclusions"]["all_six_title_surfaces_combined_passed"] is False
    assert review["conclusions"]["full_combat_retest_required"] is False
    assert review["conclusions"]["production_profile_ready"] is False

    print(
        "RUNTIME_CANARY_JOURNAL_TEST_OK combined=true checkpoint_closure=true "
        "surface_coverage=true reviewed_evidence=true fallback_classified=true "
        "redaction=true checkpoint_redaction=true sequence=true production=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

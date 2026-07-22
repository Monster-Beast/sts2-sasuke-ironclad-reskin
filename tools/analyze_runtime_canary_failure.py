#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Iterable

from analyze_runtime_canary_journal import (
    HARD_FAILURE_EVENTS,
    read_checkpoint_evidence,
    read_journal,
)

SCENARIOS = {
    "missing_timeline",
    "forced_playback_failure",
    "anchor_invalidation",
}
EXPECTED_HARD_EVENTS = {
    "missing_timeline": {"playback_fallback", "playback_request_failed"},
    "forced_playback_failure": {"playback_fallback"},
    "anchor_invalidation": {"anchor_invalidated"},
}
REASON_MARKERS = {
    "missing_timeline": "fault_injection:missing_timeline",
    "forced_playback_failure": "fault_injection:forced_playback_failure",
    "anchor_invalidation": "fault_injection:anchor_invalidation",
}
FAILURE_STATUS_FILE = "runtime-canary-failure-status.json"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate one-shot exact-build presentation failure recovery evidence."
    )
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--checkpoint-root", action="append", required=True, type=Path)
    parser.add_argument("--expected-scenario", required=True, choices=sorted(SCENARIOS))
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def iter_checkpoint_paths(roots: list[Path]) -> Iterable[Path]:
    seen: set[Path] = set()
    for root in roots:
        candidate = root.expanduser()
        paths: Iterable[Path]
        if candidate.is_file() and candidate.name == "checkpoint.json":
            paths = [candidate]
        elif candidate.is_dir():
            paths = candidate.rglob("checkpoint.json")
        else:
            continue
        for path in paths:
            resolved = path.resolve()
            if resolved not in seen:
                seen.add(resolved)
                yield path


def read_failure_statuses(
    roots: list[Path],
    event_file_name: str,
) -> tuple[list[dict[str, Any]], list[str]]:
    statuses: list[dict[str, Any]] = []
    errors: list[str] = []
    for checkpoint_path in iter_checkpoint_paths(roots):
        try:
            checkpoint = json.loads(checkpoint_path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError) as error:
            errors.append(
                f"{checkpoint_path.parent.name}: invalid checkpoint: {type(error).__name__}"
            )
            continue
        if not isinstance(checkpoint, dict) or checkpoint.get("schema_version") != 1:
            errors.append(f"{checkpoint_path.parent.name}: unsupported checkpoint schema")
            continue
        if str(checkpoint.get("event_file") or "") != event_file_name:
            continue

        status_path = checkpoint_path.with_name(FAILURE_STATUS_FILE)
        if not status_path.is_file():
            errors.append(
                f"{checkpoint_path.parent.name}: missing {FAILURE_STATUS_FILE}"
            )
            continue
        try:
            status = json.loads(status_path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError) as error:
            errors.append(
                f"{checkpoint_path.parent.name}: invalid failure status: {type(error).__name__}"
            )
            continue
        if not isinstance(status, dict) or status.get("schema_version") != 1:
            errors.append(
                f"{checkpoint_path.parent.name}: unsupported failure status schema"
            )
            continue
        statuses.append(
            {
                "checkpoint_name": str(
                    checkpoint.get("name") or checkpoint_path.parent.name
                ),
                "checkpoint_generated_at_utc": checkpoint.get("generated_at_utc"),
                "status": status,
            }
        )
    statuses.sort(key=lambda item: str(item.get("checkpoint_generated_at_utc") or ""))
    return statuses, errors


def reason_contains(event: dict[str, Any], marker: str) -> bool:
    return marker in str(event.get("reason") or "")


def analyze_failure(
    journal_path: Path,
    checkpoint_roots: list[Path],
    expected_scenario: str,
) -> dict[str, Any]:
    events, errors = read_journal(journal_path)
    checkpoint_evidence, checkpoint_errors = read_checkpoint_evidence(
        checkpoint_roots, journal_path.name
    )
    failure_statuses, failure_errors = read_failure_statuses(
        checkpoint_roots, journal_path.name
    )
    errors.extend(checkpoint_errors)
    errors.extend(failure_errors)

    clean_exit_checkpoints = [
        item for item in checkpoint_evidence if item["clean_game_exit_observed"]
    ]
    session_stop_count = sum(
        1 for event in events if event.get("event_type") == "session_stop"
    )
    session_closed = session_stop_count == 1 or bool(clean_exit_checkpoints)
    if session_stop_count not in {0, 1}:
        errors.append(f"{journal_path.name}: session_stop count must be zero or one")

    hard_failures = [
        event for event in events if event.get("event_type") in HARD_FAILURE_EVENTS
    ]
    expected_types = EXPECTED_HARD_EVENTS[expected_scenario]
    expected_hard_failures = [
        event for event in hard_failures if event.get("event_type") in expected_types
    ]
    unexpected_hard_failures = [
        event for event in hard_failures if event.get("event_type") not in expected_types
    ]
    reason_marker = REASON_MARKERS[expected_scenario]
    primary_fault_events = [
        event for event in hard_failures if reason_contains(event, reason_marker)
    ]

    latest_status = failure_statuses[-1]["status"] if failure_statuses else None
    status_requirements = {
        "requested": bool(latest_status and latest_status.get("requested") is True),
        "scenario_matches": bool(
            latest_status
            and str(latest_status.get("scenario") or "") == expected_scenario
        ),
        "target_card_present": bool(
            latest_status and str(latest_status.get("target_card_id") or "")
        ),
        "one_shot_triggered": bool(
            latest_status
            and latest_status.get("triggered") is True
            and latest_status.get("trigger_count") == 1
            and latest_status.get("armed") is False
        ),
        "recovery_confirmed": bool(
            latest_status and latest_status.get("recovery_confirmed") is True
        ),
        "replacement_inactive": bool(
            latest_status
            and latest_status.get("replacement_active_after_fault") is False
        ),
        "anchor_cleared": bool(
            latest_status and latest_status.get("anchor_bound_after_fault") is False
        ),
        "overlay_hidden": bool(
            latest_status and latest_status.get("overlay_visible_after_fault") is False
        ),
        "combat_failed_closed": bool(
            latest_status
            and latest_status.get("replacement_disabled_for_combat") is True
        ),
    }
    if latest_status and expected_scenario == "missing_timeline":
        status_requirements["pre_hide_failure"] = (
            latest_status.get("original_visual_hidden_at_trigger") is False
            and latest_status.get("replacement_ever_hidden") is False
            and int(latest_status.get("restore_count_after_fault") or 0) == 0
        )
    elif latest_status:
        status_requirements["post_hide_restoration"] = (
            latest_status.get("original_visual_hidden_at_trigger") is True
            and latest_status.get("replacement_ever_hidden") is True
            and int(latest_status.get("restore_count_after_fault") or 0) >= 1
        )

    journal_requirements = {
        "session_start": sum(
            1 for event in events if event.get("event_type") == "session_start"
        )
        == 1,
        "session_closed": session_closed,
        "failure_status_present": bool(failure_statuses),
        "expected_hard_event_present": bool(expected_hard_failures),
        "scenario_reason_present": bool(primary_fault_events),
        "no_unexpected_hard_failures": not unexpected_hard_failures,
        "replacement_disabled_event": any(
            event.get("event_type") == "replacement_disabled_for_combat"
            and reason_contains(event, reason_marker)
            for event in events
        ),
    }
    if expected_scenario != "missing_timeline":
        journal_requirements["replacement_hidden_before_fault"] = any(
            event.get("event_type") == "replacement_hidden" for event in events
        )
        journal_requirements["replacement_restored_after_fault"] = any(
            event.get("event_type") == "replacement_restored"
            and reason_contains(event, reason_marker)
            for event in events
        )

    passed = (
        not errors
        and all(journal_requirements.values())
        and all(status_requirements.values())
    )
    status = "invalid" if errors else ("passed" if passed else "partial")
    return {
        "schema_version": 1,
        "status": status,
        "expected_scenario": expected_scenario,
        "file_name": journal_path.name,
        "event_count": len(events),
        "session_stop_count": session_stop_count,
        "closure": {
            "closed": session_closed,
            "mode": (
                "runtime_session_stop"
                if session_stop_count == 1
                else (
                    "clean_process_checkpoint"
                    if clean_exit_checkpoints
                    else "open"
                )
            ),
            "checkpoint_names": [
                item["checkpoint_name"] for item in clean_exit_checkpoints
            ],
        },
        "hard_failure_count": len(hard_failures),
        "expected_hard_failures": expected_hard_failures,
        "unexpected_hard_failures": unexpected_hard_failures,
        "primary_fault_event_count": len(primary_fault_events),
        "failure_status_evidence": failure_statuses,
        "journal_requirements": journal_requirements,
        "status_requirements": status_requirements,
        "passed": passed,
        "errors": errors,
    }


def main() -> int:
    args = parse_args()
    result = analyze_failure(
        args.input,
        args.checkpoint_root,
        args.expected_scenario,
    )
    args.output.mkdir(parents=True, exist_ok=True)
    output_path = args.output / "runtime-canary-failure-review.json"
    output_path.write_text(
        json.dumps(result, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(
        "RUNTIME_CANARY_FAILURE_OK "
        f"status={result['status']} scenario={result['expected_scenario']} "
        f"output={output_path.as_posix()}"
    )
    return 0 if result["status"] != "invalid" else 1


if __name__ == "__main__":
    raise SystemExit(main())

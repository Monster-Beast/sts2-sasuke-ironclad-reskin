#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

WINDOWS_ABSOLUTE = re.compile(r"^[A-Za-z]:[\\/]")
UNIX_PRIVATE_PREFIXES = ("/home/", "/Users/", "/mnt/", "/tmp/", "/var/", "/etc/")
HARD_FAILURE_EVENTS = {
    "adapter_exception",
    "anchor_invalidated",
    "anchor_resolution_failed",
    "playback_fallback",
    "playback_request_failed",
    "replacement_hide_failed",
    "replacement_target_invalid",
}
EXPECTED_FALLBACK_EVENTS = {
    "blocked_card_fallback",
    "card_play_original_fallback",
    "unreviewed_card_fallback",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate and summarize privacy-safe runtime canary JSONL journals."
    )
    parser.add_argument("--input", action="append", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--require-combined", action="store_true")
    return parser.parse_args()


def iter_strings(value: Any) -> Iterable[str]:
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for item in value.values():
            yield from iter_strings(item)
    elif isinstance(value, list):
        for item in value:
            yield from iter_strings(item)


def contains_private_absolute_path(document: dict[str, Any]) -> bool:
    for value in iter_strings(document):
        if WINDOWS_ABSOLUTE.match(value) or value.startswith(UNIX_PRIVATE_PREFIXES):
            return True
    return False


def read_journal(path: Path) -> tuple[list[dict[str, Any]], list[str]]:
    events: list[dict[str, Any]] = []
    errors: list[str] = []
    if not path.is_file():
        return events, [f"missing input: {path.name}"]

    for line_number, raw_line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), start=1):
        line = raw_line.strip()
        if not line:
            continue
        try:
            document = json.loads(line)
        except json.JSONDecodeError as error:
            errors.append(f"{path.name}:{line_number}: invalid JSON: {error.msg}")
            continue
        if not isinstance(document, dict):
            errors.append(f"{path.name}:{line_number}: event must be a JSON object")
            continue
        if document.get("schema_version") != 1:
            errors.append(f"{path.name}:{line_number}: unsupported schema_version")
        if contains_private_absolute_path(document):
            errors.append(f"{path.name}:{line_number}: absolute local path detected")
        events.append(document)

    expected_sequence = list(range(1, len(events) + 1))
    actual_sequence = [event.get("sequence") for event in events]
    if actual_sequence != expected_sequence:
        errors.append(f"{path.name}: sequence is not contiguous from 1")
    session_ids = {event.get("session_id") for event in events if event.get("session_id")}
    if len(session_ids) != 1:
        errors.append(f"{path.name}: expected exactly one session_id")
    return events, errors


def analyze_files(paths: list[Path], require_combined: bool = False) -> dict[str, Any]:
    sessions: list[dict[str, Any]] = []
    errors: list[str] = []
    all_hard_failures: list[dict[str, Any]] = []

    for path in paths:
        events, file_errors = read_journal(path)
        errors.extend(file_errors)
        counts = Counter(str(event.get("event_type", "")) for event in events)
        start = next((event for event in events if event.get("event_type") == "session_start"), None)
        session_id = start.get("session_id") if start else (events[0].get("session_id") if events else None)
        hard_failures = [event for event in events if event.get("event_type") in HARD_FAILURE_EVENTS]
        expected_fallbacks = [event for event in events if event.get("event_type") in EXPECTED_FALLBACK_EVENTS]
        all_hard_failures.extend(hard_failures)

        combats: dict[int, Counter[str]] = defaultdict(Counter)
        for event in events:
            combat_index = int(event.get("combat_index") or 0)
            if combat_index > 0:
                combats[combat_index][str(event.get("event_type", ""))] += 1

        title_surfaces = sorted({
            str(event["surface_id"])
            for event in events
            if event.get("event_type") == "title_applied" and event.get("surface_id")
        })
        animation_cards = sorted({
            str(event["card_id"])
            for event in events
            if event.get("event_type") == "playback_started" and event.get("card_id")
        })
        animation_ids = sorted({
            str(event["animation_id"])
            for event in events
            if event.get("event_type") == "playback_started" and event.get("animation_id")
        })

        combined = bool(start and start.get("animations_enabled") is True and start.get("titles_enabled") is True)
        replacement_requested = bool(start and start.get("replacement_requested") is True)
        requirements = {
            "session_start": counts["session_start"] == 1,
            "session_stop": counts["session_stop"] == 1,
            "combined_layers": combined,
            "title_applied": counts["title_applied"] > 0,
            "playback_started": counts["playback_started"] > 0,
            "combat_ended": counts["combat_ended"] > 0,
            "replacement_hidden": (not replacement_requested) or counts["replacement_hidden"] > 0,
            "no_hard_failures": not hard_failures,
        }
        if not require_combined:
            requirements["combined_layers"] = True

        sessions.append({
            "file_name": path.name,
            "session_id": session_id,
            "event_count": len(events),
            "event_counts": dict(sorted(counts.items())),
            "combat_count": len(combats),
            "combats": {
                str(index): dict(sorted(counter.items()))
                for index, counter in sorted(combats.items())
            },
            "combined": combined,
            "replacement_requested": replacement_requested,
            "title_surfaces": title_surfaces,
            "animation_cards": animation_cards,
            "animation_ids": animation_ids,
            "hard_failure_count": len(hard_failures),
            "expected_fallback_count": len(expected_fallbacks),
            "requirements": requirements,
            "passed": all(requirements.values()),
        })

    if errors:
        status = "invalid"
    elif all(session["passed"] for session in sessions):
        status = "passed"
    else:
        status = "partial"

    return {
        "schema_version": 1,
        "status": status,
        "require_combined": require_combined,
        "session_count": len(sessions),
        "sessions": sessions,
        "hard_failures": all_hard_failures,
        "errors": errors,
    }


def main() -> int:
    args = parse_args()
    result = analyze_files(args.input, require_combined=args.require_combined)
    args.output.mkdir(parents=True, exist_ok=True)
    output_path = args.output / "runtime-canary-journal-review.json"
    output_path.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(
        "RUNTIME_CANARY_JOURNAL_OK "
        f"status={result['status']} sessions={result['session_count']} "
        f"output={output_path.as_posix()}"
    )
    return 0 if result["status"] != "invalid" else 1


if __name__ == "__main__":
    raise SystemExit(main())

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
ALL_TITLE_SURFACES = {
    "card_art",
    "hand",
    "deck_list",
    "reward",
    "compendium",
    "tooltip",
}
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
    parser.add_argument(
        "--checkpoint-root",
        action="append",
        default=[],
        type=Path,
        help="Directory or checkpoint.json used as external clean-process-exit evidence.",
    )
    parser.add_argument(
        "--require-title-surface",
        action="append",
        default=[],
        help="Require one title surface to appear in each analyzed session.",
    )
    parser.add_argument(
        "--require-all-title-surfaces",
        action="store_true",
        help="Require card_art, hand, deck_list, reward, compendium and tooltip.",
    )
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


def iter_checkpoint_paths(roots: list[Path]) -> Iterable[Path]:
    seen: set[Path] = set()
    for root in roots:
        candidate = root.expanduser()
        paths: Iterable[Path]
        if candidate.is_file():
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


def read_checkpoint_evidence(
    roots: list[Path],
    event_file_name: str,
) -> tuple[list[dict[str, Any]], list[str]]:
    evidence: list[dict[str, Any]] = []
    errors: list[str] = []

    for checkpoint_path in iter_checkpoint_paths(roots):
        try:
            document = json.loads(checkpoint_path.read_text(encoding="utf-8-sig"))
        except (OSError, json.JSONDecodeError) as error:
            errors.append(f"{checkpoint_path.name}: invalid checkpoint: {type(error).__name__}")
            continue
        if not isinstance(document, dict) or document.get("schema_version") != 1:
            errors.append(f"{checkpoint_path.name}: unsupported checkpoint schema")
            continue
        if str(document.get("event_file") or "") != event_file_name:
            continue

        process_path = checkpoint_path.with_name("process.json")
        process_document: list[dict[str, Any]] = []
        process_parse_succeeded = False
        if process_path.is_file():
            try:
                raw_processes = json.loads(process_path.read_text(encoding="utf-8-sig"))
                if isinstance(raw_processes, list) and all(isinstance(item, dict) for item in raw_processes):
                    process_document = raw_processes
                    process_parse_succeeded = True
                else:
                    errors.append(f"{checkpoint_path.parent.name}: process.json must contain an array")
            except (OSError, json.JSONDecodeError) as error:
                errors.append(
                    f"{checkpoint_path.parent.name}: invalid process.json: {type(error).__name__}"
                )

        query_value = document.get("process_query_succeeded")
        legacy_inference = not isinstance(query_value, bool)
        process_query_succeeded = (
            bool(query_value) if isinstance(query_value, bool) else process_parse_succeeded
        )

        game_count_value = document.get("game_process_count")
        if isinstance(game_count_value, int) and game_count_value >= 0:
            game_process_count = game_count_value
        else:
            game_process_count = sum(
                1
                for process in process_document
                if str(process.get("Name") or "").casefold() == "slaythespire2.exe"
            )

        process_count_value = document.get("process_count")
        process_count = (
            process_count_value
            if isinstance(process_count_value, int) and process_count_value >= 0
            else len(process_document)
        )
        clean_game_exit_observed = process_query_succeeded and game_process_count == 0

        evidence.append(
            {
                "checkpoint_name": str(document.get("name") or checkpoint_path.parent.name),
                "generated_at_utc": document.get("generated_at_utc"),
                "process_query_succeeded": process_query_succeeded,
                "legacy_process_query_inference": legacy_inference,
                "process_count": process_count,
                "game_process_count": game_process_count,
                "clean_game_exit_observed": clean_game_exit_observed,
                "journal_event_count": document.get("journal_event_count"),
                "journal_last_sequence": document.get("journal_last_sequence"),
                "journal_last_event_type": document.get("journal_last_event_type"),
            }
        )

    evidence.sort(key=lambda item: str(item.get("generated_at_utc") or ""))
    return evidence, errors


def analyze_files(
    paths: list[Path],
    require_combined: bool = False,
    checkpoint_roots: list[Path] | None = None,
    required_title_surfaces: set[str] | None = None,
) -> dict[str, Any]:
    sessions: list[dict[str, Any]] = []
    errors: list[str] = []
    warnings: list[str] = []
    all_hard_failures: list[dict[str, Any]] = []
    roots = checkpoint_roots or []
    required_surfaces = {
        surface.strip()
        for surface in (required_title_surfaces or set())
        if surface and surface.strip()
    }

    for path in paths:
        events, file_errors = read_journal(path)
        errors.extend(file_errors)
        counts = Counter(str(event.get("event_type", "")) for event in events)
        start = next((event for event in events if event.get("event_type") == "session_start"), None)
        session_id = start.get("session_id") if start else (events[0].get("session_id") if events else None)
        hard_failures = [event for event in events if event.get("event_type") in HARD_FAILURE_EVENTS]
        expected_fallbacks = [event for event in events if event.get("event_type") in EXPECTED_FALLBACK_EVENTS]
        all_hard_failures.extend(hard_failures)

        checkpoint_evidence, checkpoint_errors = read_checkpoint_evidence(roots, path.name)
        errors.extend(checkpoint_errors)
        clean_exit_checkpoints = [
            item for item in checkpoint_evidence if item["clean_game_exit_observed"]
        ]
        runtime_session_stop = counts["session_stop"] == 1
        session_stop_shape_valid = counts["session_stop"] in {0, 1}
        session_closed = runtime_session_stop or bool(clean_exit_checkpoints)
        if session_closed and not runtime_session_stop:
            warnings.append(
                f"{path.name}: runtime session_stop was absent; clean process-exit checkpoint evidence closed the session"
            )

        combats: dict[int, Counter[str]] = defaultdict(Counter)
        for event in events:
            combat_index = int(event.get("combat_index") or 0)
            if combat_index > 0:
                combats[combat_index][str(event.get("event_type", ""))] += 1

        title_surfaces = sorted(
            {
                str(event["surface_id"])
                for event in events
                if event.get("event_type") == "title_applied" and event.get("surface_id")
            }
        )
        missing_title_surfaces = sorted(required_surfaces.difference(title_surfaces))
        animation_cards = sorted(
            {
                str(event["card_id"])
                for event in events
                if event.get("event_type") == "playback_started" and event.get("card_id")
            }
        )
        animation_ids = sorted(
            {
                str(event["animation_id"])
                for event in events
                if event.get("event_type") == "playback_started" and event.get("animation_id")
            }
        )

        combined = bool(start and start.get("animations_enabled") is True and start.get("titles_enabled") is True)
        replacement_requested = bool(start and start.get("replacement_requested") is True)
        requirements = {
            "session_start": counts["session_start"] == 1,
            "session_stop_shape": session_stop_shape_valid,
            "session_closed": session_closed,
            "combined_layers": combined,
            "title_applied": counts["title_applied"] > 0,
            "required_title_surfaces": not missing_title_surfaces,
            "playback_started": counts["playback_started"] > 0,
            "combat_ended": counts["combat_ended"] > 0,
            "replacement_hidden": (not replacement_requested) or counts["replacement_hidden"] > 0,
            "no_hard_failures": not hard_failures,
        }
        if not require_combined:
            requirements["combined_layers"] = True

        closure_mode = "runtime_session_stop" if runtime_session_stop else (
            "clean_process_checkpoint" if clean_exit_checkpoints else "open"
        )
        sessions.append(
            {
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
                "required_title_surfaces": sorted(required_surfaces),
                "missing_title_surfaces": missing_title_surfaces,
                "animation_cards": animation_cards,
                "animation_ids": animation_ids,
                "hard_failure_count": len(hard_failures),
                "expected_fallback_count": len(expected_fallbacks),
                "session_stop_count": counts["session_stop"],
                "closure": {
                    "closed": session_closed,
                    "mode": closure_mode,
                    "checkpoint_names": [
                        item["checkpoint_name"] for item in clean_exit_checkpoints
                    ],
                },
                "checkpoint_evidence": checkpoint_evidence,
                "requirements": requirements,
                "passed": all(requirements.values()),
            }
        )

    if errors:
        status = "invalid"
    elif sessions and all(session["passed"] for session in sessions):
        status = "passed"
    else:
        status = "partial"

    return {
        "schema_version": 1,
        "status": status,
        "require_combined": require_combined,
        "required_title_surfaces": sorted(required_surfaces),
        "session_count": len(sessions),
        "sessions": sessions,
        "hard_failures": all_hard_failures,
        "warnings": warnings,
        "errors": errors,
    }


def main() -> int:
    args = parse_args()
    required_surfaces = set(args.require_title_surface)
    if args.require_all_title_surfaces:
        required_surfaces.update(ALL_TITLE_SURFACES)
    result = analyze_files(
        args.input,
        require_combined=args.require_combined,
        checkpoint_roots=args.checkpoint_root,
        required_title_surfaces=required_surfaces,
    )
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

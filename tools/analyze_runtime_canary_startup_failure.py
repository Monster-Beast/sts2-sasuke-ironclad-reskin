#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime
from pathlib import Path
from typing import Any, Iterable


SCENARIOS = {"method_signature_mismatch"}
EXPECTED_MODE = "local_startup_failure_only"
EXPECTED_BRANCH = "public-beta"
EXPECTED_BUILD_ID = "24251656"
EXPECTED_BINDING_ID = "card_visual_request"
EXPECTED_TRIGGER_STAGE = "method_signature_comparison"
EXPECTED_ACKNOWLEDGEMENT = (
    "public-beta-24251656-local-startup-method-signature-mismatch"
)
EXPECTED_REASON = (
    "Canary binding card_visual_request method signature does not match "
    "the reviewed target (fault_injection:method_signature_mismatch)."
)
SESSION_LABEL_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,47}$")

MARKER_FILE = "SasukeIronclad.canary.json"
STARTUP_STATUS_FILE = "runtime-canary-status.json"
PROCESS_FILE = "process.json"
STATUS_TEXT_FILE = "status.txt"
FORBIDDEN_RUNTIME_STATUS_FILES = {
    "runtime-canary-anchor-status.json",
    "runtime-canary-replacement-status.json",
    "runtime-canary-failure-status.json",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Validate an exact-build runtime canary startup failure that rejects "
            "a deliberately mismatched reviewed method signature before patch install."
        )
    )
    parser.add_argument(
        "--checkpoint-root",
        action="append",
        required=True,
        type=Path,
        help="Directory or checkpoint.json containing clean-process startup evidence.",
    )
    parser.add_argument(
        "--expected-scenario",
        required=True,
        choices=sorted(SCENARIOS),
    )
    parser.add_argument("--expected-session-label", required=True)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def iter_checkpoint_paths(
    roots: list[Path],
) -> tuple[list[Path], list[str]]:
    seen: set[Path] = set()
    paths: list[Path] = []
    errors: list[str] = []
    for root in roots:
        candidate = root.expanduser()
        discovered: Iterable[Path]
        if candidate.is_file() and candidate.name == "checkpoint.json":
            discovered = [candidate]
        elif candidate.is_dir():
            discovered = candidate.rglob("checkpoint.json")
        else:
            errors.append(f"checkpoint root is missing or unsupported: {candidate.name}")
            continue
        for path in discovered:
            resolved = path.resolve()
            if resolved in seen:
                continue
            seen.add(resolved)
            paths.append(path)
    paths.sort(key=lambda path: path.parent.name)
    return paths, errors


def read_json_object(
    path: Path,
    label: str,
    errors: list[str],
) -> dict[str, Any] | None:
    if not path.is_file():
        return None
    try:
        document = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"{label}: invalid JSON: {type(error).__name__}")
        return None
    if not isinstance(document, dict):
        errors.append(f"{label}: JSON root must be an object")
        return None
    return document


def read_process_array(
    path: Path,
    label: str,
    errors: list[str],
) -> list[dict[str, Any]] | None:
    if not path.is_file():
        return None
    try:
        document = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"{label}: invalid process JSON: {type(error).__name__}")
        return None
    if not isinstance(document, list) or not all(
        isinstance(item, dict) for item in document
    ):
        errors.append(f"{label}: process.json must contain an array of objects")
        return None
    return document


def file_evidence(path: Path) -> dict[str, Any] | None:
    if not path.is_file():
        return None
    try:
        content = path.read_bytes()
    except OSError:
        return None
    return {
        "file_name": path.name,
        "size_bytes": len(content),
        "sha256": hashlib.sha256(content).hexdigest(),
    }


def is_empty(value: Any) -> bool:
    return value is None or value == ""


def is_non_negative_int(value: Any) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def parse_timestamp(value: Any) -> datetime | None:
    if not isinstance(value, str) or not value.strip():
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None
    return parsed if parsed.tzinfo is not None else None


def marker_matches_session(marker: dict[str, Any] | None, session_label: str) -> bool:
    return bool(marker and marker.get("session_label") == session_label)


def analyze_startup_failure(
    checkpoint_roots: list[Path],
    expected_scenario: str,
    expected_session_label: str,
) -> dict[str, Any]:
    checkpoint_paths, errors = iter_checkpoint_paths(checkpoint_roots)
    if expected_scenario not in SCENARIOS:
        errors.append(f"unsupported expected scenario: {expected_scenario}")
    if not SESSION_LABEL_PATTERN.fullmatch(expected_session_label):
        errors.append("expected session label is invalid")

    inventories: list[dict[str, Any]] = []
    matching: list[tuple[Path, dict[str, Any], dict[str, Any]]] = []
    for checkpoint_path in checkpoint_paths:
        label = checkpoint_path.parent.name
        checkpoint = read_json_object(
            checkpoint_path,
            f"{label}/checkpoint.json",
            errors,
        )
        marker = read_json_object(
            checkpoint_path.with_name(MARKER_FILE),
            f"{label}/{MARKER_FILE}",
            errors,
        )
        inventories.append(
            {
                "checkpoint_name": label,
                "marker_present": marker is not None,
                "session_label": marker.get("session_label") if marker else None,
            }
        )
        if checkpoint is not None and marker_matches_session(
            marker, expected_session_label
        ):
            matching.append((checkpoint_path, checkpoint, marker or {}))

    matching.sort(
        key=lambda item: str(item[1].get("generated_at_utc") or "")
    )
    selected = matching[-1] if matching else None

    requirements: dict[str, bool] = {
        "one_matching_checkpoint": len(matching) == 1,
        "checkpoint_schema_supported": False,
        "marker_copied": False,
        "marker_schema_supported": False,
        "marker_dedicated_startup_mode": False,
        "marker_exact_build": False,
        "marker_session_matches": False,
        "marker_animations_only": False,
        "marker_replacement_disabled": False,
        "marker_presentation_failure_disabled": False,
        "marker_startup_scenario_matches": False,
        "marker_startup_binding_matches": False,
        "marker_startup_one_shot": False,
        "marker_startup_acknowledgement_matches": False,
        "startup_status_copied": False,
        "startup_status_schema_supported": False,
        "mod_initializer_reached": False,
        "canary_disabled": False,
        "presentation_layers_disabled": False,
        "no_runtime_session": False,
        "no_event_journal_identity": False,
        "no_patched_bindings": False,
        "presentation_failure_status_disabled": False,
        "startup_status_session_matches": False,
        "startup_failure_requested": False,
        "startup_failure_scenario_matches": False,
        "startup_failure_binding_matches": False,
        "startup_failure_triggered_once": False,
        "startup_failure_stage_matches": False,
        "baseline_signature_match_confirmed": False,
        "target_resolution_failed": False,
        "patch_install_not_attempted": False,
        "exact_signature_mismatch_reason": False,
        "checkpoint_marker_present": False,
        "observation_marker_absent": False,
        "process_query_succeeded": False,
        "game_process_absent": False,
        "process_snapshot_consistent": False,
        "checkpoint_reports_process_absent": False,
        "checkpoint_has_no_journal_identity": False,
        "no_jsonl_copied": False,
        "no_runtime_transition_statuses": False,
        "required_checkpoint_files_recorded": False,
        "startup_precedes_checkpoint": False,
    }

    evidence: dict[str, Any] = {
        "checkpoint_inventory": inventories,
        "matching_checkpoint_count": len(matching),
        "selected_checkpoint": None,
        "marker": None,
        "startup_status": None,
        "process": None,
        "files": [],
    }

    if selected is not None:
        checkpoint_path, checkpoint, marker = selected
        checkpoint_dir = checkpoint_path.parent
        label = checkpoint_dir.name
        status_path = checkpoint_dir / STARTUP_STATUS_FILE
        process_path = checkpoint_dir / PROCESS_FILE
        status_text_path = checkpoint_dir / STATUS_TEXT_FILE
        marker_path = checkpoint_dir / MARKER_FILE
        status = read_json_object(
            status_path,
            f"{label}/{STARTUP_STATUS_FILE}",
            errors,
        )
        processes = read_process_array(
            process_path,
            f"{label}/{PROCESS_FILE}",
            errors,
        )

        copied_raw = checkpoint.get("copied_files")
        copied_files = (
            {str(item) for item in copied_raw}
            if isinstance(copied_raw, list)
            and all(isinstance(item, str) for item in copied_raw)
            else set()
        )
        required_files = {
            MARKER_FILE,
            STARTUP_STATUS_FILE,
            PROCESS_FILE,
            STATUS_TEXT_FILE,
        }
        jsonl_files = sorted(path.name for path in checkpoint_dir.glob("*.jsonl"))
        forbidden_statuses_present = sorted(
            file_name
            for file_name in FORBIDDEN_RUNTIME_STATUS_FILES
            if (checkpoint_dir / file_name).is_file() or file_name in copied_files
        )

        checkpoint_schema_ok = checkpoint.get("schema_version") == 1
        marker_schema_ok = marker.get("schema_version") == 1
        status_schema_ok = bool(status and status.get("schema_version") == 1)

        requirements.update(
            {
                "checkpoint_schema_supported": checkpoint_schema_ok,
                "marker_copied": marker_path.is_file()
                and MARKER_FILE in copied_files,
                "marker_schema_supported": marker_schema_ok,
                "marker_dedicated_startup_mode": marker.get("enabled") is True
                and marker.get("mode") == EXPECTED_MODE,
                "marker_exact_build": marker.get("expected_branch")
                == EXPECTED_BRANCH
                and str(marker.get("expected_build_id") or "")
                == EXPECTED_BUILD_ID,
                "marker_session_matches": marker.get("session_label")
                == expected_session_label,
                "marker_animations_only": marker.get("enable_animations") is True
                and marker.get("enable_titles") is False,
                "marker_replacement_disabled": marker.get("hide_original_visual")
                is False
                and is_empty(marker.get("replacement_acknowledgement")),
                "marker_presentation_failure_disabled": marker.get(
                    "failure_injection_scenario"
                )
                == "none"
                and is_empty(marker.get("failure_injection_card_id"))
                and marker.get("failure_injection_once") is True
                and is_empty(marker.get("failure_injection_acknowledgement")),
                "marker_startup_scenario_matches": marker.get(
                    "startup_failure_injection_scenario"
                )
                == expected_scenario,
                "marker_startup_binding_matches": marker.get(
                    "startup_failure_injection_binding_id"
                )
                == EXPECTED_BINDING_ID,
                "marker_startup_one_shot": marker.get(
                    "startup_failure_injection_once"
                )
                is True,
                "marker_startup_acknowledgement_matches": marker.get(
                    "startup_failure_injection_acknowledgement"
                )
                == EXPECTED_ACKNOWLEDGEMENT,
                "startup_status_copied": status_path.is_file()
                and STARTUP_STATUS_FILE in copied_files,
                "startup_status_schema_supported": status_schema_ok,
                "checkpoint_marker_present": checkpoint.get(
                    "canary_marker_present"
                )
                is True,
                "observation_marker_absent": checkpoint.get(
                    "observation_marker_present"
                )
                is False,
                "process_query_succeeded": checkpoint.get(
                    "process_query_succeeded"
                )
                is True,
                "checkpoint_reports_process_absent": checkpoint.get(
                    "process_absent_at_capture"
                )
                is True,
                "checkpoint_has_no_journal_identity": all(
                    is_empty(checkpoint.get(key))
                    for key in (
                        "event_file",
                        "journal_session_id",
                        "journal_event_count",
                        "journal_last_sequence",
                        "journal_last_event_type",
                    )
                ),
                "no_jsonl_copied": not jsonl_files
                and not any(name.lower().endswith(".jsonl") for name in copied_files),
                "no_runtime_transition_statuses": not forbidden_statuses_present,
                "required_checkpoint_files_recorded": required_files
                <= copied_files
                and all((checkpoint_dir / name).is_file() for name in required_files),
            }
        )

        if status is not None:
            reasons = status.get("reasons")
            requirements.update(
                {
                    "mod_initializer_reached": status.get(
                        "mod_initializer_reached"
                    )
                    is True,
                    "canary_disabled": status.get("enabled") is False,
                    "presentation_layers_disabled": status.get(
                        "animations_enabled"
                    )
                    is False
                    and status.get("titles_enabled") is False,
                    "no_runtime_session": is_empty(status.get("session_id")),
                    "no_event_journal_identity": is_empty(status.get("event_file")),
                    "no_patched_bindings": status.get("patched_binding_ids") == [],
                    "presentation_failure_status_disabled": status.get(
                        "failure_injection_scenario"
                    )
                    == "none"
                    and is_empty(status.get("failure_injection_card_id")),
                    "startup_status_session_matches": status.get(
                        "requested_session_label"
                    )
                    == expected_session_label,
                    "startup_failure_requested": status.get(
                        "startup_failure_injection_requested"
                    )
                    is True,
                    "startup_failure_scenario_matches": status.get(
                        "startup_failure_injection_scenario"
                    )
                    == expected_scenario,
                    "startup_failure_binding_matches": status.get(
                        "startup_failure_injection_binding_id"
                    )
                    == EXPECTED_BINDING_ID,
                    "startup_failure_triggered_once": status.get(
                        "startup_failure_injection_armed"
                    )
                    is False
                    and status.get("startup_failure_injection_triggered") is True
                    and status.get("startup_failure_injection_trigger_count") == 1,
                    "startup_failure_stage_matches": status.get(
                        "startup_failure_injection_trigger_stage"
                    )
                    == EXPECTED_TRIGGER_STAGE,
                    "baseline_signature_match_confirmed": status.get(
                        "startup_failure_injection_baseline_match_confirmed"
                    )
                    is True,
                    "target_resolution_failed": reasons == [EXPECTED_REASON],
                    "patch_install_not_attempted": status.get(
                        "patch_install_attempted"
                    )
                    is False,
                    "exact_signature_mismatch_reason": reasons
                    == [EXPECTED_REASON],
                }
            )

        if processes is not None:
            game_process_count = sum(
                1
                for process in processes
                if str(process.get("Name") or "").casefold()
                == "slaythespire2.exe"
            )
            process_count = checkpoint.get("process_count")
            checkpoint_game_count = checkpoint.get("game_process_count")
            checkpoint_auxiliary_count = checkpoint.get("auxiliary_process_count")
            counts_valid = (
                is_non_negative_int(process_count)
                and is_non_negative_int(checkpoint_game_count)
                and is_non_negative_int(checkpoint_auxiliary_count)
            )
            requirements["game_process_absent"] = (
                game_process_count == 0 and checkpoint_game_count == 0
            )
            requirements["process_snapshot_consistent"] = bool(
                counts_valid
                and process_count == len(processes)
                and checkpoint_game_count == game_process_count
                and checkpoint_auxiliary_count
                == len(processes) - game_process_count
            )

        status_timestamp = parse_timestamp(
            status.get("generated_at_utc") if status else None
        )
        checkpoint_timestamp = parse_timestamp(checkpoint.get("generated_at_utc"))
        requirements["startup_precedes_checkpoint"] = bool(
            status_timestamp
            and checkpoint_timestamp
            and status_timestamp <= checkpoint_timestamp
        )

        files = [
            evidence_item
            for evidence_item in (
                file_evidence(marker_path),
                file_evidence(status_path),
                file_evidence(checkpoint_path),
                file_evidence(process_path),
                file_evidence(status_text_path),
            )
            if evidence_item is not None
        ]
        evidence.update(
            {
                "selected_checkpoint": {
                    "name": str(checkpoint.get("name") or label),
                    "generated_at_utc": checkpoint.get("generated_at_utc"),
                    "copied_files": sorted(copied_files),
                    "event_file": checkpoint.get("event_file"),
                    "process_count": checkpoint.get("process_count"),
                    "game_process_count": checkpoint.get("game_process_count"),
                    "auxiliary_process_count": checkpoint.get(
                        "auxiliary_process_count"
                    ),
                    "jsonl_files": jsonl_files,
                    "forbidden_statuses_present": forbidden_statuses_present,
                },
                "marker": {
                    "mode": marker.get("mode"),
                    "expected_branch": marker.get("expected_branch"),
                    "expected_build_id": marker.get("expected_build_id"),
                    "session_label": marker.get("session_label"),
                    "startup_failure_injection_scenario": marker.get(
                        "startup_failure_injection_scenario"
                    ),
                    "startup_failure_injection_binding_id": marker.get(
                        "startup_failure_injection_binding_id"
                    ),
                },
                "startup_status": (
                    {
                        "generated_at_utc": status.get("generated_at_utc"),
                        "enabled": status.get("enabled"),
                        "requested_session_label": status.get(
                            "requested_session_label"
                        ),
                        "session_id": status.get("session_id"),
                        "event_file": status.get("event_file"),
                        "patched_binding_ids": status.get("patched_binding_ids"),
                        "startup_failure_injection_scenario": status.get(
                            "startup_failure_injection_scenario"
                        ),
                        "startup_failure_injection_binding_id": status.get(
                            "startup_failure_injection_binding_id"
                        ),
                        "startup_failure_injection_trigger_count": status.get(
                            "startup_failure_injection_trigger_count"
                        ),
                        "startup_failure_injection_trigger_stage": status.get(
                            "startup_failure_injection_trigger_stage"
                        ),
                        "startup_failure_injection_baseline_match_confirmed": status.get(
                            "startup_failure_injection_baseline_match_confirmed"
                        ),
                        "patch_install_attempted": status.get(
                            "patch_install_attempted"
                        ),
                        "reasons": status.get("reasons"),
                    }
                    if status
                    else None
                ),
                "process": {
                    "snapshot_entry_count": len(processes)
                    if processes is not None
                    else None,
                    "game_process_count": sum(
                        1
                        for process in processes or []
                        if str(process.get("Name") or "").casefold()
                        == "slaythespire2.exe"
                    ),
                },
                "files": files,
            }
        )

    passed = not errors and all(requirements.values())
    status_value = "invalid" if errors else ("passed" if passed else "partial")
    unmet_requirements = [
        name for name, satisfied in requirements.items() if not satisfied
    ]
    return {
        "schema_version": 1,
        "status": status_value,
        "passed": passed,
        "expected": {
            "scenario": expected_scenario,
            "session_label": expected_session_label,
            "binding_id": EXPECTED_BINDING_ID,
            "mode": EXPECTED_MODE,
        },
        "evidence": evidence,
        "requirements": requirements,
        "unmet_requirements": unmet_requirements,
        "errors": errors,
        "scope": {
            "proves_startup_fail_closed": passed,
            "proves_card_was_played": False,
            "requires_separate_user_visual_verdict": True,
        },
    }


def main() -> int:
    args = parse_args()
    result = analyze_startup_failure(
        args.checkpoint_root,
        args.expected_scenario,
        args.expected_session_label,
    )
    args.output.mkdir(parents=True, exist_ok=True)
    output_path = args.output / "runtime-canary-startup-failure-review.json"
    output_path.write_text(
        json.dumps(result, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    marker = (
        "RUNTIME_CANARY_STARTUP_FAILURE_PASS"
        if result["passed"]
        else "RUNTIME_CANARY_STARTUP_FAILURE_NOT_PASSED"
    )
    print(
        f"{marker} status={result['status']} "
        f"scenario={result['expected']['scenario']} "
        f"session={result['expected']['session_label']} "
        f"output={output_path.as_posix()}"
    )
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

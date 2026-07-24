#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Callable

from analyze_runtime_canary_startup_failure import (
    EXPECTED_ACKNOWLEDGEMENT,
    EXPECTED_BINDING_ID,
    EXPECTED_BUILD_ID,
    EXPECTED_MODE,
    EXPECTED_REASON,
    EXPECTED_TRIGGER_STAGE,
    analyze_startup_failure,
)


ROOT = Path(__file__).resolve().parents[1]
ANALYZER = ROOT / "tools/analyze_runtime_canary_startup_failure.py"
REVIEW = (
    ROOT
    / "SasukeIronclad/data/reviews"
    / "public-beta-24251656-startup-failure-review.json"
)
SCENARIO = "method_signature_mismatch"
SESSION_LABEL = "startup-signature-fixture-1"

Fixture = dict[str, Any]
Mutation = Callable[[Fixture], None]


def run_analyzer_cli(
    checkpoint_root: Path,
    output: Path,
    *,
    session_label: str = SESSION_LABEL,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            sys.executable,
            str(ANALYZER),
            "--checkpoint-root",
            str(checkpoint_root),
            "--expected-scenario",
            SCENARIO,
            "--expected-session-label",
            session_label,
            "--output",
            str(output),
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )


def base_fixture(checkpoint_name: str) -> Fixture:
    marker = {
        "schema_version": 1,
        "enabled": True,
        "mode": EXPECTED_MODE,
        "expected_branch": "public-beta",
        "expected_build_id": EXPECTED_BUILD_ID,
        "session_label": SESSION_LABEL,
        "enable_animations": True,
        "enable_titles": False,
        "low_flash": True,
        "fast_mode": False,
        "anchor_to_local_player": True,
        "anchor_scale": 1.2,
        "anchor_offset_x": 0.0,
        "anchor_offset_y": -150.0,
        "hide_original_visual": False,
        "replacement_acknowledgement": "",
        "failure_injection_scenario": "none",
        "failure_injection_card_id": "",
        "failure_injection_once": True,
        "failure_injection_acknowledgement": "",
        "startup_failure_injection_scenario": SCENARIO,
        "startup_failure_injection_binding_id": EXPECTED_BINDING_ID,
        "startup_failure_injection_once": True,
        "startup_failure_injection_acknowledgement": EXPECTED_ACKNOWLEDGEMENT,
    }
    status = {
        "schema_version": 1,
        "generated_at_utc": "2026-07-23T14:00:00+00:00",
        "mod_initializer_reached": True,
        "enabled": False,
        "animations_enabled": False,
        "titles_enabled": False,
        "requested_session_label": SESSION_LABEL,
        "session_id": None,
        "event_file": None,
        "failure_injection_scenario": "none",
        "failure_injection_card_id": None,
        "patched_binding_ids": [],
        "startup_failure_injection_requested": True,
        "startup_failure_injection_scenario": SCENARIO,
        "startup_failure_injection_binding_id": EXPECTED_BINDING_ID,
        "startup_failure_injection_armed": False,
        "startup_failure_injection_triggered": True,
        "startup_failure_injection_trigger_count": 1,
        "startup_failure_injection_trigger_stage": EXPECTED_TRIGGER_STAGE,
        "startup_failure_injection_baseline_match_confirmed": True,
        "patch_install_attempted": False,
        "reasons": [EXPECTED_REASON],
    }
    checkpoint = {
        "schema_version": 1,
        "generated_at_utc": "2026-07-23T14:01:00+00:00",
        "name": checkpoint_name,
        "canary_marker_present": True,
        "observation_marker_present": False,
        "process_query_succeeded": True,
        "process_count": 0,
        "game_process_count": 0,
        "auxiliary_process_count": 0,
        "process_absent_at_capture": True,
        "event_file": None,
        "journal_session_id": None,
        "journal_event_count": None,
        "journal_last_sequence": None,
        "journal_last_event_type": None,
        "copied_files": [
            "SasukeIronclad.canary.json",
            "runtime-canary-status.json",
            "status.txt",
            "process.json",
        ],
    }
    return {
        "marker": marker,
        "status": status,
        "checkpoint": checkpoint,
        "processes": [],
        "extra_files": {},
        "raw_marker": None,
        "raw_status": None,
        "raw_checkpoint": None,
        "raw_processes": None,
    }


def write_fixture(
    root: Path,
    checkpoint_name: str,
    mutation: Mutation | None = None,
) -> Path:
    fixture = base_fixture(checkpoint_name)
    if mutation is not None:
        mutation(fixture)
    checkpoint_dir = root / checkpoint_name
    checkpoint_dir.mkdir(parents=True, exist_ok=True)

    write_json_or_raw(
        checkpoint_dir / "SasukeIronclad.canary.json",
        fixture["marker"],
        fixture["raw_marker"],
    )
    write_json_or_raw(
        checkpoint_dir / "runtime-canary-status.json",
        fixture["status"],
        fixture["raw_status"],
    )
    write_json_or_raw(
        checkpoint_dir / "checkpoint.json",
        fixture["checkpoint"],
        fixture["raw_checkpoint"],
    )
    write_json_or_raw(
        checkpoint_dir / "process.json",
        fixture["processes"],
        fixture["raw_processes"],
    )
    (checkpoint_dir / "status.txt").write_text(
        "Runtime presentation canary marker: ENABLED\n",
        encoding="utf-8",
    )
    for name, content in fixture["extra_files"].items():
        (checkpoint_dir / name).write_text(content, encoding="utf-8")
    return checkpoint_dir


def write_json_or_raw(path: Path, document: Any, raw: str | None) -> None:
    if document is None and raw is None:
        return
    if raw is not None:
        path.write_text(raw, encoding="utf-8")
        return
    path.write_text(
        json.dumps(document, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def set_path(section: str, key: str, value: Any) -> Mutation:
    def mutate(fixture: Fixture) -> None:
        fixture[section][key] = value

    return mutate


def remove_marker(fixture: Fixture) -> None:
    fixture["marker"] = None
    fixture["checkpoint"]["canary_marker_present"] = False
    fixture["checkpoint"]["copied_files"].remove(
        "SasukeIronclad.canary.json"
    )


def enable_presentation_failure(fixture: Fixture) -> None:
    fixture["marker"].update(
        {
            "failure_injection_scenario": "forced_playback_failure",
            "failure_injection_card_id": "Strike",
            "failure_injection_acknowledgement": (
                "public-beta-24251656-local-visual-failure-injection"
            ),
        }
    )


def enable_replacement(fixture: Fixture) -> None:
    fixture["marker"]["hide_original_visual"] = True
    fixture["marker"]["replacement_acknowledgement"] = (
        "public-beta-24251656-local-ironclad-replacement"
    )


def leave_game_running(fixture: Fixture) -> None:
    fixture["processes"] = [
        {"Name": "SlayTheSpire2.exe", "ProcessId": 4242, "WorkingSetSize": 1}
    ]
    fixture["checkpoint"].update(
        {
            "process_count": 1,
            "game_process_count": 1,
            "auxiliary_process_count": 0,
            "process_absent_at_capture": False,
        }
    )


def inconsistent_process_counts(fixture: Fixture) -> None:
    fixture["checkpoint"]["process_count"] = 1
    fixture["checkpoint"]["auxiliary_process_count"] = 1


def add_runtime_status(fixture: Fixture) -> None:
    name = "runtime-canary-anchor-status.json"
    fixture["extra_files"][name] = "{}\n"
    fixture["checkpoint"]["copied_files"].append(name)


def add_jsonl(fixture: Fixture) -> None:
    name = "runtime-canary-unexpected.jsonl"
    fixture["extra_files"][name] = "{}\n"
    fixture["checkpoint"]["copied_files"].append(name)


def add_journal_identity(fixture: Fixture) -> None:
    fixture["checkpoint"].update(
        {
            "event_file": "runtime-canary-unexpected.jsonl",
            "journal_session_id": "unexpected",
            "journal_event_count": 1,
            "journal_last_sequence": 1,
            "journal_last_event_type": "session_start",
        }
    )


def remove_required_copy_record(fixture: Fixture) -> None:
    fixture["checkpoint"]["copied_files"].remove("status.txt")


def malformed_status(fixture: Fixture) -> None:
    fixture["status"] = None
    fixture["raw_status"] = "{not-json\n"


def malformed_process(fixture: Fixture) -> None:
    fixture["processes"] = None
    fixture["raw_processes"] = "{}\n"


def assert_negative_case(
    root: Path,
    name: str,
    mutation: Mutation,
    expected_false_requirement: str,
) -> None:
    case_root = root / name
    write_fixture(case_root, "01-clean-exit", mutation)
    result = analyze_startup_failure([case_root], SCENARIO, SESSION_LABEL)
    assert result["passed"] is False, (name, result)
    assert result["status"] in {"partial", "invalid"}, (name, result)
    assert result["requirements"][expected_false_requirement] is False, (
        name,
        expected_false_requirement,
        result,
    )

    completed = run_analyzer_cli(case_root, root / f"{name}-review")
    assert completed.returncode == 1, (name, completed)
    assert "RUNTIME_CANARY_STARTUP_FAILURE_NOT_PASSED" in completed.stdout, (
        name,
        completed,
    )


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="sasuke-startup-failure-") as temporary:
        root = Path(temporary)
        passing_root = root / "passing"
        write_fixture(passing_root, "01-clean-exit")
        passing = analyze_startup_failure(
            [passing_root],
            SCENARIO,
            SESSION_LABEL,
        )
        assert passing["status"] == "passed", passing
        assert passing["passed"] is True
        assert all(passing["requirements"].values()), passing
        assert passing["unmet_requirements"] == []
        assert passing["scope"] == {
            "proves_startup_fail_closed": True,
            "proves_card_was_played": False,
            "requires_separate_user_visual_verdict": True,
        }

        completed = run_analyzer_cli(passing_root, root / "passing-review")
        assert completed.returncode == 0, completed
        assert (
            "RUNTIME_CANARY_STARTUP_FAILURE_PASS status=passed"
            in completed.stdout
        )
        review = json.loads(
            (
                root
                / "passing-review/runtime-canary-startup-failure-review.json"
            ).read_text(encoding="utf-8")
        )
        assert review["passed"] is True
        assert review["expected"]["mode"] == EXPECTED_MODE
        assert review["evidence"]["matching_checkpoint_count"] == 1
        assert len(review["evidence"]["files"]) == 5

        negative_cases: list[tuple[str, Mutation, str]] = [
            (
                "marker_absent",
                remove_marker,
                "one_matching_checkpoint",
            ),
            (
                "legacy_mode",
                set_path("marker", "mode", "local_visual_only"),
                "marker_dedicated_startup_mode",
            ),
            (
                "wrong_build",
                set_path("marker", "expected_build_id", "24251657"),
                "marker_exact_build",
            ),
            (
                "wrong_session",
                set_path("marker", "session_label", "another-session"),
                "one_matching_checkpoint",
            ),
            (
                "titles_enabled",
                set_path("marker", "enable_titles", True),
                "marker_animations_only",
            ),
            (
                "replacement_enabled",
                enable_replacement,
                "marker_replacement_disabled",
            ),
            (
                "presentation_failure_enabled",
                enable_presentation_failure,
                "marker_presentation_failure_disabled",
            ),
            (
                "marker_wrong_scenario",
                set_path(
                    "marker",
                    "startup_failure_injection_scenario",
                    "none",
                ),
                "marker_startup_scenario_matches",
            ),
            (
                "marker_wrong_binding",
                set_path(
                    "marker",
                    "startup_failure_injection_binding_id",
                    "original_impact",
                ),
                "marker_startup_binding_matches",
            ),
            (
                "marker_not_one_shot",
                set_path("marker", "startup_failure_injection_once", False),
                "marker_startup_one_shot",
            ),
            (
                "marker_wrong_ack",
                set_path(
                    "marker",
                    "startup_failure_injection_acknowledgement",
                    "wrong",
                ),
                "marker_startup_acknowledgement_matches",
            ),
            (
                "initializer_not_reached",
                set_path("status", "mod_initializer_reached", False),
                "mod_initializer_reached",
            ),
            (
                "canary_enabled",
                set_path("status", "enabled", True),
                "canary_disabled",
            ),
            (
                "runtime_session_created",
                set_path("status", "session_id", "unexpected-session"),
                "no_runtime_session",
            ),
            (
                "event_file_created",
                set_path(
                    "status",
                    "event_file",
                    "runtime-canary-unexpected.jsonl",
                ),
                "no_event_journal_identity",
            ),
            (
                "binding_patched",
                set_path(
                    "status",
                    "patched_binding_ids",
                    [EXPECTED_BINDING_ID],
                ),
                "no_patched_bindings",
            ),
            (
                "status_wrong_session",
                set_path(
                    "status",
                    "requested_session_label",
                    "another-session",
                ),
                "startup_status_session_matches",
            ),
            (
                "startup_not_requested",
                set_path(
                    "status", "startup_failure_injection_requested", False
                ),
                "startup_failure_requested",
            ),
            (
                "status_wrong_scenario",
                set_path(
                    "status", "startup_failure_injection_scenario", "none"
                ),
                "startup_failure_scenario_matches",
            ),
            (
                "status_wrong_binding",
                set_path(
                    "status",
                    "startup_failure_injection_binding_id",
                    "original_impact",
                ),
                "startup_failure_binding_matches",
            ),
            (
                "still_armed",
                set_path(
                    "status", "startup_failure_injection_armed", True
                ),
                "startup_failure_triggered_once",
            ),
            (
                "not_triggered",
                set_path(
                    "status", "startup_failure_injection_triggered", False
                ),
                "startup_failure_triggered_once",
            ),
            (
                "trigger_count_two",
                set_path(
                    "status", "startup_failure_injection_trigger_count", 2
                ),
                "startup_failure_triggered_once",
            ),
            (
                "wrong_trigger_stage",
                set_path(
                    "status",
                    "startup_failure_injection_trigger_stage",
                    "before_manifest_identity",
                ),
                "startup_failure_stage_matches",
            ),
            (
                "baseline_not_confirmed",
                set_path(
                    "status",
                    "startup_failure_injection_baseline_match_confirmed",
                    False,
                ),
                "baseline_signature_match_confirmed",
            ),
            (
                "patch_install_attempted",
                set_path("status", "patch_install_attempted", True),
                "patch_install_not_attempted",
            ),
            (
                "wrong_reason",
                set_path("status", "reasons", ["different failure"]),
                "exact_signature_mismatch_reason",
            ),
            (
                "observation_marker_present",
                set_path("checkpoint", "observation_marker_present", True),
                "observation_marker_absent",
            ),
            (
                "process_query_failed",
                set_path("checkpoint", "process_query_succeeded", False),
                "process_query_succeeded",
            ),
            (
                "game_still_running",
                leave_game_running,
                "game_process_absent",
            ),
            (
                "process_counts_inconsistent",
                inconsistent_process_counts,
                "process_snapshot_consistent",
            ),
            (
                "checkpoint_process_absence_false",
                set_path("checkpoint", "process_absent_at_capture", False),
                "checkpoint_reports_process_absent",
            ),
            (
                "journal_identity_present",
                add_journal_identity,
                "checkpoint_has_no_journal_identity",
            ),
            (
                "jsonl_copied",
                add_jsonl,
                "no_jsonl_copied",
            ),
            (
                "transition_status_copied",
                add_runtime_status,
                "no_runtime_transition_statuses",
            ),
            (
                "required_copy_not_recorded",
                remove_required_copy_record,
                "required_checkpoint_files_recorded",
            ),
            (
                "status_after_checkpoint",
                set_path(
                    "status",
                    "generated_at_utc",
                    "2026-07-23T14:02:00+00:00",
                ),
                "startup_precedes_checkpoint",
            ),
            (
                "malformed_status",
                malformed_status,
                "startup_status_schema_supported",
            ),
            (
                "malformed_process",
                malformed_process,
                "process_snapshot_consistent",
            ),
        ]
        for name, mutation, expected_false_requirement in negative_cases:
            assert_negative_case(
                root,
                name,
                mutation,
                expected_false_requirement,
            )

        duplicate_root = root / "duplicate_matching_checkpoints"
        write_fixture(duplicate_root, "01-clean-exit")
        write_fixture(duplicate_root, "02-clean-exit")
        duplicate = analyze_startup_failure(
            [duplicate_root],
            SCENARIO,
            SESSION_LABEL,
        )
        assert duplicate["passed"] is False, duplicate
        assert duplicate["requirements"]["one_matching_checkpoint"] is False
        duplicate_cli = run_analyzer_cli(
            duplicate_root,
            root / "duplicate-review",
        )
        assert duplicate_cli.returncode == 1, duplicate_cli

    review = json.loads(REVIEW.read_text(encoding="utf-8"))
    assert review["schema_version"] == 1
    assert review["status"] == "startup_signature_mismatch_fail_closed_passed"
    assert review["test"]["user_visual_verdict_zh"] == "已退出，打击时没有佐助"
    assert review["test"]["analyzer_status"] == "passed"
    assert review["test"]["patch_install_attempted"] is False
    assert review["test"]["runtime_session_created"] is False
    assert review["test"]["event_journal_created"] is False
    assert review["conclusions"]["startup_signature_mismatch_fail_closed"] is True
    assert review["conclusions"]["original_presentation_visual_passed"] is True
    assert review["conclusions"]["production_profile_ready"] is False
    assert review["source_policy"]["raw_game_log_committed"] is False
    assert review["safety"]["affects_gameplay"] is False

    print(
        "RUNTIME_CANARY_STARTUP_FAILURE_TEST_OK "
        f"negative_cases={len(negative_cases) + 1} "
        "dedicated_mode=true exact_build=true baseline_match=true "
        "resolver_failed=true patch_install=false session=false journal=false "
        "clean_exit=true user_visual_verdict_separate=true"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

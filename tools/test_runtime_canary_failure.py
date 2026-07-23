#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path

from analyze_runtime_canary_failure import analyze_failure

ROOT = Path(__file__).resolve().parents[1]
ANALYZER = ROOT / "tools/analyze_runtime_canary_failure.py"
REVIEW = (
    ROOT
    / "SasukeIronclad/data/reviews"
    / "public-beta-24251656-failure-injection-review.json"
)


def run_analyzer_cli(
    journal: Path,
    checkpoint_root: Path,
    scenario: str,
    output: Path,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            sys.executable,
            str(ANALYZER),
            "--input",
            str(journal),
            "--checkpoint-root",
            str(checkpoint_root),
            "--expected-scenario",
            scenario,
            "--output",
            str(output),
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=False,
    )


def write_events(path: Path, scenario: str, *, unexpected: bool = False) -> None:
    marker = f"fault_injection:{scenario}"
    events: list[dict] = [
        {
            "schema_version": 1,
            "generated_at_utc": "2026-07-22T00:00:00+00:00",
            "session_id": f"failure-{scenario}-fixture",
            "sequence": 1,
            "combat_index": 0,
            "event_type": "session_start",
            "animations_enabled": True,
            "titles_enabled": False,
            "replacement_requested": True,
        },
        {
            "schema_version": 1,
            "generated_at_utc": "2026-07-22T00:00:01+00:00",
            "session_id": f"failure-{scenario}-fixture",
            "sequence": 2,
            "combat_index": 1,
            "event_type": "combat_started",
            "card_id": "Strike",
        },
        {
            "schema_version": 1,
            "generated_at_utc": "2026-07-22T00:00:02+00:00",
            "session_id": f"failure-{scenario}-fixture",
            "sequence": 3,
            "combat_index": 1,
            "event_type": "anchor_bound",
        },
    ]
    if scenario == "missing_timeline":
        events.extend(
            [
                _event(4, scenario, "playback_fallback", reason=marker),
                _event(
                    5,
                    scenario,
                    "replacement_disabled_for_combat",
                    reason=f"playback_fallback:{marker}",
                ),
                _event(
                    6,
                    scenario,
                    "playback_request_failed",
                    reason="reviewed_timeline_request_failed",
                ),
            ]
        )
    else:
        events.extend(
            [
                _event(4, scenario, "playback_started", card_id="Strike"),
                _event(5, scenario, "replacement_hidden", card_id="Strike"),
            ]
        )
        if scenario == "forced_playback_failure":
            events.append(
                _event(
                    6,
                    scenario,
                    "playback_fallback",
                    reason=f"asynchronous visual playback failed: {marker}",
                )
            )
            failure_reason = f"playback_fallback:asynchronous visual playback failed: {marker}"
        else:
            events.append(
                _event(6, scenario, "anchor_invalidated", reason=marker)
            )
            failure_reason = f"anchor_invalidated:{marker}"
        events.extend(
            [
                _event(
                    7,
                    scenario,
                    "replacement_disabled_for_combat",
                    reason=failure_reason,
                ),
                _event(
                    8,
                    scenario,
                    "replacement_restored",
                    reason=failure_reason,
                ),
            ]
        )
    if unexpected:
        events.append(
            _event(
                len(events) + 1,
                scenario,
                "adapter_exception",
                reason="unexpected_fixture_failure",
            )
        )
    events.append(_event(len(events) + 1, scenario, "combat_ended"))
    path.write_text(
        "".join(json.dumps(event, separators=(",", ":")) + "\n" for event in events),
        encoding="utf-8",
    )


def _event(
    sequence: int,
    scenario: str,
    event_type: str,
    **values: object,
) -> dict:
    event = {
        "schema_version": 1,
        "generated_at_utc": "2026-07-22T00:00:03+00:00",
        "session_id": f"failure-{scenario}-fixture",
        "sequence": sequence,
        "combat_index": 1,
        "event_type": event_type,
    }
    event.update(values)
    return event


def write_checkpoint(
    root: Path,
    journal_name: str,
    scenario: str,
    *,
    recovery_confirmed: bool = True,
) -> None:
    checkpoint = root / f"checkpoint-{scenario}"
    checkpoint.mkdir(parents=True, exist_ok=True)
    (checkpoint / "process.json").write_text("[]\n", encoding="utf-8")
    (checkpoint / "checkpoint.json").write_text(
        json.dumps(
            {
                "schema_version": 1,
                "generated_at_utc": "2026-07-22T00:01:00+00:00",
                "name": checkpoint.name,
                "process_query_succeeded": True,
                "process_count": 0,
                "game_process_count": 0,
                "event_file": journal_name,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    missing = scenario == "missing_timeline"
    (checkpoint / "runtime-canary-failure-status.json").write_text(
        json.dumps(
            {
                "schema_version": 1,
                "generated_at_utc": "2026-07-22T00:00:10+00:00",
                "requested": True,
                "scenario": scenario,
                "target_card_id": "Strike",
                "armed": False,
                "triggered": True,
                "trigger_count": 1,
                "trigger_stage": (
                    "can_play" if missing else "after_replacement_hidden"
                ),
                "original_visual_hidden_at_trigger": not missing,
                "recovery_confirmed": recovery_confirmed,
                "replacement_active_after_fault": False,
                "replacement_ever_hidden": not missing,
                "restore_count_after_fault": 0 if missing else 1,
                "original_visibility_restored_after_fault": None if missing else True,
                "anchor_bound_after_fault": False,
                "overlay_visible_after_fault": False,
                "replacement_disabled_for_combat": True,
                "last_transition": "failure_recovery_confirmed:fixture",
                "reasons": ["fixture"],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="sasuke-canary-failure-") as temporary:
        root = Path(temporary)
        for scenario in [
            "missing_timeline",
            "forced_playback_failure",
            "anchor_invalidation",
        ]:
            journal = root / f"{scenario}.jsonl"
            checkpoint_root = root / f"{scenario}-evidence"
            write_events(journal, scenario)
            write_checkpoint(checkpoint_root, journal.name, scenario)
            result = analyze_failure(journal, [checkpoint_root], scenario)
            assert result["status"] == "passed", result
            assert result["passed"] is True
            assert result["primary_fault_event_count"] == 1
            assert result["status_requirements"]["recovery_confirmed"] is True
            completed = run_analyzer_cli(
                journal,
                checkpoint_root,
                scenario,
                root / f"{scenario}-review",
            )
            assert completed.returncode == 0, completed
            assert "RUNTIME_CANARY_FAILURE_PASS status=passed" in completed.stdout

        unexpected_journal = root / "unexpected.jsonl"
        unexpected_root = root / "unexpected-evidence"
        write_events(unexpected_journal, "forced_playback_failure", unexpected=True)
        write_checkpoint(
            unexpected_root,
            unexpected_journal.name,
            "forced_playback_failure",
        )
        unexpected = analyze_failure(
            unexpected_journal,
            [unexpected_root],
            "forced_playback_failure",
        )
        assert unexpected["status"] == "partial", unexpected
        assert unexpected["unexpected_hard_failures"]
        unexpected_completed = run_analyzer_cli(
            unexpected_journal,
            unexpected_root,
            "forced_playback_failure",
            root / "unexpected-review",
        )
        assert unexpected_completed.returncode == 1, unexpected_completed
        assert (
            "RUNTIME_CANARY_FAILURE_NOT_PASSED status=partial"
            in unexpected_completed.stdout
        )

        unrecovered_journal = root / "unrecovered.jsonl"
        unrecovered_root = root / "unrecovered-evidence"
        write_events(unrecovered_journal, "anchor_invalidation")
        write_checkpoint(
            unrecovered_root,
            unrecovered_journal.name,
            "anchor_invalidation",
            recovery_confirmed=False,
        )
        unrecovered = analyze_failure(
            unrecovered_journal,
            [unrecovered_root],
            "anchor_invalidation",
        )
        assert unrecovered["status"] == "partial", unrecovered
        assert unrecovered["status_requirements"]["recovery_confirmed"] is False

    review = json.loads(REVIEW.read_text(encoding="utf-8"))
    assert review["schema_version"] == 1
    assert (
        review["status"]
        == "missing_timeline_passed_forced_playback_trigger_regression_open"
    )
    attempts = {
        attempt["session_label"]: attempt for attempt in review["attempts"]
    }
    assert attempts["failure-missing-v338-2"]["analyzer_status"] == "partial"
    assert attempts["failure-missing-v338-2"]["passed"] is False
    assert attempts["failure-missing-v338-3"]["analyzer_status"] == "passed"
    assert attempts["failure-missing-v338-3"]["passed"] is True
    failed_playback = attempts["failure-playback-v338-1"]
    assert failed_playback["analyzer_status"] == "partial"
    assert failed_playback["primary_fault_event_count"] == 0
    assert failed_playback["passed"] is False
    assert review["conclusions"]["forced_playback_failure_regression_open"] is True
    assert review["conclusions"]["production_profile_ready"] is False
    assert review["source_policy"]["raw_game_logs_committed"] is False
    assert review["safety"]["affects_gameplay"] is False

    control_source = (ROOT / "tools/runtime-canary.ps1").read_text(encoding="ascii")
    checkpoint_source = (ROOT / "tools/runtime-canary-checkpoint.ps1").read_text(encoding="ascii")
    host_source = (ROOT / "SasukeIroncladCode/Adapters/GodotVisualSceneHost.cs").read_text(encoding="utf-8")
    session_source = (ROOT / "SasukeIroncladCode/Runtime/RuntimeCanarySession.cs").read_text(encoding="utf-8")
    replacement_source = (ROOT / "SasukeIroncladCode/Runtime/RuntimeOriginalVisualReplacementController.cs").read_text(encoding="utf-8")
    gate_source = (ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryGate.cs").read_text(encoding="utf-8")

    for scenario in [
        "missing_timeline",
        "forced_playback_failure",
        "anchor_invalidation",
    ]:
        assert scenario in control_source
        assert scenario in host_source or scenario in gate_source
    assert "public-beta-24251656-local-visual-failure-injection" in control_source
    assert "public-beta-24251656-local-visual-failure-injection" in gate_source
    assert "runtime-canary-failure-status.json" in control_source
    assert "runtime-canary-failure-status.json" in checkpoint_source
    assert "RuntimeCanaryFailureDiagnostics.NotifyOriginalVisualHidden" in replacement_source
    assert "RuntimeCanaryFailureDiagnostics.NotifyReplacementRestored" in replacement_source
    assert "OriginalVisibilityRestored" in replacement_source
    assert "ConfigureFailureDiagnostics" in host_source
    assert "host.ConfigureFailureDiagnostics(_modAssemblyPath, optIn);" in session_source
    assert "typeof(GodotVisualSceneHost).Assembly.Location" not in host_source
    assert "internal bool TryProcessPendingFailureInjection()" in host_source
    assert "_ = TryProcessPendingFailureInjection();" in host_source
    active_card_index = session_source.index("_activeCardId = cardId;")
    synchronous_pump_index = session_source.index(
        "if (_sceneHost.TryProcessPendingFailureInjection())"
    )
    flame_barrier_index = session_source.index(
        "if (string.Equals(cardId, FlameBarrierCardId, StringComparison.Ordinal))"
    )
    assert active_card_index < synchronous_pump_index < flame_barrier_index
    assert "File.Delete" not in host_source
    assert "File.Move" not in host_source

    print(
        "RUNTIME_CANARY_FAILURE_TEST_OK scenarios=3 one_shot=true "
        "recovery_required=true visibility_verified=true "
        "unexpected_failure_rejected=true in_memory_only=true"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

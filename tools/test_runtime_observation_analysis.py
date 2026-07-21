#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "tools/analyze_runtime_observation.py"
MANIFEST_PATH = ROOT / "SasukeIronclad/data/runtime_observation_targets.json"


def load_module():
    spec = importlib.util.spec_from_file_location("analyze_runtime_observation", MODULE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def write_jsonl(path: Path, events: list[dict]) -> None:
    path.write_text("".join(json.dumps(event) + "\n" for event in events), encoding="utf-8")


def build_events(manifest: dict, session_id: str, *, wrong_runtime: bool = False, leak_string: bool = False) -> list[dict]:
    fingerprint = manifest["fingerprint"]
    runtime = {
        "branch": manifest["branch"],
        "steam_build_id": fingerprint["steam_build_id"],
        "sts2_sha256": fingerprint["sts2_sha256"],
        "module_mvid": fingerprint["module_mvid"],
        "baselib_version": fingerprint["baselib_version"],
    }
    if wrong_runtime:
        runtime["steam_build_id"] = "1"

    sequence = 1
    call_id = 0
    events: list[dict] = [
        {
            "schema_version": 1,
            "timestamp_utc": "2026-07-21T09:00:00+00:00",
            "session_id": session_id,
            "event_sequence": sequence,
            "phase": "session_start",
            "thread_id": 1,
            "runtime": runtime,
            "target_count": len(manifest["targets"]),
            "max_events": 20000,
        }
    ]
    for target in manifest["targets"]:
        call_id += 1
        sequence += 1
        argument = {
            "index": 0,
            "type": "System.Int32",
            "safe_value": str(call_id),
        }
        if leak_string and call_id == 1:
            argument = {
                "index": 0,
                "type": "System.String",
                "safe_value": "forbidden raw title",
            }
        base = {
            "schema_version": 1,
            "timestamp_utc": "2026-07-21T09:00:01+00:00",
            "session_id": session_id,
            "call_id": call_id,
            "target_id": target["id"],
            "binding_ids": target["binding_ids"],
            "metadata_token": target["metadata_token"],
            "declaring_type": target["declaring_type"],
            "method_name": "Fixture",
            "thread_id": 1,
            "instance_type": target["declaring_type"],
            "arguments": [argument],
            "related_model_types": ["MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad"],
        }
        events.append(
            {
                **base,
                "event_sequence": sequence,
                "phase": "enter",
                "caller_stack": ["MegaCrit.Sts2.Core.Nodes.Fixture.Caller"],
            }
        )
        sequence += 1
        events.append(
            {
                **base,
                "event_sequence": sequence,
                "phase": "return",
                "elapsed_ms": 0.25,
            }
        )
    sequence += 1
    events.append(
        {
            "schema_version": 1,
            "timestamp_utc": "2026-07-21T09:00:02+00:00",
            "session_id": session_id,
            "event_sequence": sequence,
            "phase": "session_stop",
            "thread_id": 1,
        }
    )
    return events


def main() -> int:
    module = load_module()
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        run1 = root / "run-1.jsonl"
        run2 = root / "run-2.jsonl"
        write_jsonl(run1, build_events(manifest, "observed-run-1"))
        write_jsonl(run2, build_events(manifest, "observed-run-2"))

        valid = module.combine(MANIFEST_PATH, [run1, run2])
        assert valid["status"] == "pending_manual_review"
        assert valid["session_count"] == 2
        assert valid["all_required_bindings_observed_in_all_sessions"] is True
        assert valid["auto_selection_forbidden"] is True
        assert valid["runtime_bindings_remain_disabled"] is True
        assert all(item["review"]["status"] == "unreviewed" for item in valid["bindings"].values())
        assert all(item["review"]["selected_target_id"] == "" for item in valid["bindings"].values())

        duplicate_session = module.combine(MANIFEST_PATH, [run1, run1])
        assert duplicate_session["status"] == "invalid"
        assert any("reuse a session_id" in item for item in duplicate_session["errors"])

        bad = root / "bad.jsonl"
        write_jsonl(bad, build_events(manifest, "observed-bad", wrong_runtime=True, leak_string=True))
        invalid = module.combine(MANIFEST_PATH, [run1, bad])
        assert invalid["status"] == "invalid"
        assert any("runtime fingerprint" in item for item in invalid["errors"])
        assert any("raw string" in item for item in invalid["errors"])

    print("RUNTIME_OBSERVATION_ANALYSIS_OK sessions=2 bindings=12 auto_selection=false privacy=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

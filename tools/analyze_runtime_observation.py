#!/usr/bin/env python3
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

TARGET_ID_RE = re.compile(r"^[a-z0-9][a-z0-9._-]{2,95}$")
METHOD_TOKEN_RE = re.compile(r"^0x06[0-9A-Fa-f]{6}$")
ALLOWED_PHASES = {"session_start", "enter", "return", "session_stop"}
DISALLOWED_EVENT_KEYS = {
    "absolute_path",
    "file_path",
    "return_value",
    "result_value",
    "localized_title",
    "raw_string",
}


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalize(value: object) -> str:
    return str(value or "").strip()


def load_json_lines(path: Path) -> list[dict[str, Any]]:
    events: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8-sig") as stream:
        for line_number, line in enumerate(stream, start=1):
            stripped = line.strip()
            if not stripped:
                continue
            try:
                value = json.loads(stripped)
            except json.JSONDecodeError as exc:
                raise ValueError(f"{path.name}:{line_number}: invalid JSONL: {exc}") from exc
            if not isinstance(value, dict):
                raise ValueError(f"{path.name}:{line_number}: event must be a JSON object")
            events.append(value)
    if not events:
        raise ValueError(f"{path.name}: observation log is empty")
    return events


def expected_runtime(manifest: dict[str, Any]) -> dict[str, str]:
    fingerprint = manifest.get("fingerprint", {})
    return {
        "branch": normalize(manifest.get("branch")),
        "steam_build_id": normalize(fingerprint.get("steam_build_id")),
        "sts2_sha256": normalize(fingerprint.get("sts2_sha256")).lower(),
        "module_mvid": normalize(fingerprint.get("module_mvid")).lower(),
        "baselib_version": normalize(fingerprint.get("baselib_version")),
    }


def validate_manifest(manifest: dict[str, Any]) -> tuple[dict[str, dict[str, Any]], list[str]]:
    errors: list[str] = []
    if manifest.get("schema_version") != 1 or manifest.get("status") != "observation_only":
        errors.append("unsupported runtime observation manifest")
    if manifest.get("gameplay_changes") is not False:
        errors.append("runtime observation manifest permits gameplay changes")
    policy = manifest.get("policy", {})
    for key in (
        "read_only_only",
        "exact_fingerprint_required",
        "fail_closed_on_target_mismatch",
        "unpatch_on_reset",
    ):
        if policy.get(key) is not True:
            errors.append(f"observation safety policy missing {key}")
    if policy.get("default_enabled") is not False:
        errors.append("runtime observation must be disabled by default")
    if policy.get("capture_argument_values") is not False or policy.get("capture_absolute_paths") is not False:
        errors.append("runtime observation privacy policy is unsafe")

    required_bindings = [normalize(item) for item in manifest.get("required_binding_ids", [])]
    if len(required_bindings) != 12 or len(set(required_bindings)) != 12:
        errors.append("manifest must contain twelve unique binding IDs")

    targets: dict[str, dict[str, Any]] = {}
    covered: set[str] = set()
    for target in manifest.get("targets", []):
        target_id = normalize(target.get("id"))
        if not TARGET_ID_RE.fullmatch(target_id) or target_id in targets:
            errors.append(f"invalid or duplicate target ID: {target_id}")
            continue
        token = normalize(target.get("metadata_token"))
        if not METHOD_TOKEN_RE.fullmatch(token):
            errors.append(f"target {target_id} does not contain a MethodDef token")
        binding_ids = [normalize(item) for item in target.get("binding_ids", [])]
        if not binding_ids or len(binding_ids) != len(set(binding_ids)):
            errors.append(f"target {target_id} has invalid binding IDs")
        unknown = sorted(set(binding_ids) - set(required_bindings))
        if unknown:
            errors.append(f"target {target_id} references unknown bindings: {', '.join(unknown)}")
        covered.update(binding_ids)
        targets[target_id] = target
    if covered != set(required_bindings):
        errors.append("observation targets do not cover all required binding IDs")
    return targets, errors


def validate_event_privacy(event: dict[str, Any], source: str, errors: list[str]) -> None:
    lowered = {str(key).lower() for key in event}
    disallowed = sorted(lowered.intersection(DISALLOWED_EVENT_KEYS))
    if disallowed:
        errors.append(f"{source}: event contains forbidden fields: {', '.join(disallowed)}")
    for argument in event.get("arguments", []) or []:
        argument_type = normalize(argument.get("type"))
        safe_value = argument.get("safe_value")
        if safe_value is not None and argument_type in {"System.String", "string"}:
            errors.append(f"{source}: raw string argument value was recorded")
    for frame in event.get("caller_stack", []) or []:
        text = normalize(frame)
        if ":\\" in text or text.startswith("/") or "/home/" in text or "\\Users\\" in text:
            errors.append(f"{source}: caller stack contains an absolute path")


def analyze_log(
    path: Path,
    events: list[dict[str, Any]],
    manifest: dict[str, Any],
    targets: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    errors: list[str] = []
    warnings: list[str] = []
    expected = expected_runtime(manifest)
    sequences: list[int] = []
    session_ids: set[str] = set()
    starts = 0
    stops = 0
    enters: dict[int, dict[str, Any]] = {}
    returns: dict[int, dict[str, Any]] = {}
    target_phase_counts: dict[str, collections.Counter[str]] = collections.defaultdict(collections.Counter)
    binding_targets: dict[str, set[str]] = collections.defaultdict(set)
    related_types: dict[str, set[str]] = collections.defaultdict(set)
    stacks: dict[str, collections.Counter[tuple[str, ...]]] = collections.defaultdict(collections.Counter)
    argument_shapes: dict[str, collections.Counter[tuple[str, ...]]] = collections.defaultdict(collections.Counter)
    runtime_seen: dict[str, str] | None = None

    for index, event in enumerate(events, start=1):
        source = f"{path.name}:event-{index}"
        if event.get("schema_version") != 1:
            errors.append(f"{source}: unsupported event schema")
        phase = normalize(event.get("phase"))
        if phase not in ALLOWED_PHASES:
            errors.append(f"{source}: unsupported phase {phase}")
            continue
        validate_event_privacy(event, source, errors)
        try:
            sequence = int(event.get("event_sequence"))
        except (TypeError, ValueError):
            errors.append(f"{source}: invalid event_sequence")
            sequence = -1
        sequences.append(sequence)
        session_id = normalize(event.get("session_id"))
        if not session_id:
            errors.append(f"{source}: missing session_id")
        else:
            session_ids.add(session_id)

        if phase == "session_start":
            starts += 1
            runtime = event.get("runtime") or {}
            runtime_seen = {
                "branch": normalize(runtime.get("branch")),
                "steam_build_id": normalize(runtime.get("steam_build_id")),
                "sts2_sha256": normalize(runtime.get("sts2_sha256")).lower(),
                "module_mvid": normalize(runtime.get("module_mvid")).lower(),
                "baselib_version": normalize(runtime.get("baselib_version")),
            }
            if runtime_seen != expected:
                errors.append(f"{source}: runtime fingerprint does not match the exact observation manifest")
            continue
        if phase == "session_stop":
            stops += 1
            continue

        target_id = normalize(event.get("target_id"))
        target = targets.get(target_id)
        if target is None:
            errors.append(f"{source}: target_id is not present in the reviewed observation manifest: {target_id}")
            continue
        if normalize(event.get("metadata_token")) != normalize(target.get("metadata_token")):
            errors.append(f"{source}: metadata token does not match target {target_id}")
        if normalize(event.get("declaring_type")) != normalize(target.get("declaring_type")):
            errors.append(f"{source}: declaring type does not match target {target_id}")
        event_bindings = {normalize(item) for item in event.get("binding_ids", [])}
        target_bindings = {normalize(item) for item in target.get("binding_ids", [])}
        if event_bindings != target_bindings:
            errors.append(f"{source}: binding IDs do not match target {target_id}")
        target_phase_counts[target_id][phase] += 1
        for binding_id in target_bindings:
            binding_targets[binding_id].add(target_id)
        related_types[target_id].update(normalize(item) for item in event.get("related_model_types", []) if normalize(item))
        stack = tuple(normalize(item) for item in event.get("caller_stack", []) if normalize(item))
        if stack:
            stacks[target_id][stack] += 1
        shape = tuple(normalize(item.get("type")) for item in event.get("arguments", []) or [])
        argument_shapes[target_id][shape] += 1

        try:
            call_id = int(event.get("call_id"))
        except (TypeError, ValueError):
            errors.append(f"{source}: invalid call_id")
            continue
        if phase == "enter":
            if call_id in enters:
                errors.append(f"{source}: duplicate enter call_id {call_id}")
            enters[call_id] = event
        else:
            if call_id in returns:
                errors.append(f"{source}: duplicate return call_id {call_id}")
            returns[call_id] = event

    if starts != 1:
        errors.append(f"{path.name}: expected exactly one session_start, found {starts}")
    if stops == 0:
        warnings.append(f"{path.name}: no session_stop event; the game may not have exited cleanly")
    elif stops != 1:
        errors.append(f"{path.name}: expected at most one session_stop, found {stops}")
    if len(session_ids) != 1:
        errors.append(f"{path.name}: expected one session_id, found {len(session_ids)}")
    if sequences != sorted(sequences) or len(sequences) != len(set(sequences)) or any(value <= 0 for value in sequences):
        errors.append(f"{path.name}: event_sequence values are not strictly increasing and unique")

    orphan_returns = sorted(set(returns) - set(enters))
    open_calls = sorted(set(enters) - set(returns))
    if orphan_returns:
        errors.append(f"{path.name}: return events without enter events: {orphan_returns[:10]}")
    if open_calls:
        warnings.append(f"{path.name}: {len(open_calls)} calls did not record a return event")
    for call_id in sorted(set(enters).intersection(returns)):
        if normalize(enters[call_id].get("target_id")) != normalize(returns[call_id].get("target_id")):
            errors.append(f"{path.name}: call_id {call_id} changed target_id between enter and return")

    target_summaries: dict[str, Any] = {}
    for target_id in sorted(target_phase_counts):
        target = targets[target_id]
        stack_examples = [
            {"frames": list(stack), "count": count}
            for stack, count in stacks[target_id].most_common(5)
        ]
        target_summaries[target_id] = {
            "binding_ids": list(target.get("binding_ids", [])),
            "enter_count": target_phase_counts[target_id]["enter"],
            "return_count": target_phase_counts[target_id]["return"],
            "related_model_types": sorted(related_types[target_id]),
            "argument_type_shapes": [
                {"types": list(shape), "count": count}
                for shape, count in argument_shapes[target_id].most_common(5)
            ],
            "caller_stack_examples": stack_examples,
        }

    return {
        "path_name": path.name,
        "sha256": sha256(path),
        "session_id": next(iter(session_ids), ""),
        "runtime": runtime_seen or {},
        "event_count": len(events),
        "session_start_count": starts,
        "session_stop_count": stops,
        "enter_count": len(enters),
        "return_count": len(returns),
        "open_call_count": len(open_calls),
        "targets": target_summaries,
        "binding_targets": {key: sorted(value) for key, value in sorted(binding_targets.items())},
        "errors": errors,
        "warnings": warnings,
    }


def combine(manifest_path: Path, input_paths: list[Path]) -> dict[str, Any]:
    manifest = load_json(manifest_path)
    targets, manifest_errors = validate_manifest(manifest)
    logs: list[dict[str, Any]] = []
    global_errors = list(manifest_errors)
    global_warnings: list[str] = []

    for path in input_paths:
        try:
            log = analyze_log(path, load_json_lines(path), manifest, targets)
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            global_errors.append(str(exc))
            continue
        logs.append(log)
        global_errors.extend(log["errors"])
        global_warnings.extend(log["warnings"])

    session_ids = [normalize(log.get("session_id")) for log in logs if normalize(log.get("session_id"))]
    if len(logs) < 2:
        global_warnings.append("At least two independent observation logs are required before binding review.")
    if len(session_ids) != len(set(session_ids)):
        global_errors.append("observation logs reuse a session_id; independent sessions are required")

    expected = expected_runtime(manifest)
    if any(log.get("runtime") != expected for log in logs):
        global_errors.append("one or more logs do not match the manifest runtime fingerprint")

    required_bindings = [normalize(item) for item in manifest.get("required_binding_ids", [])]
    binding_review: dict[str, Any] = {}
    for binding_id in required_bindings:
        sessions: list[dict[str, Any]] = []
        for log in logs:
            target_ids = log.get("binding_targets", {}).get(binding_id, [])
            sessions.append(
                {
                    "session_id": log.get("session_id", ""),
                    "observed": bool(target_ids),
                    "target_ids": target_ids,
                }
            )
        binding_review[binding_id] = {
            "observed_in_all_sessions": len(logs) >= 2 and all(item["observed"] for item in sessions),
            "sessions": sessions,
            "review": {
                "status": "unreviewed",
                "selected_target_id": "",
                "reviewer": "",
                "evidence": [],
                "notes": "",
            },
        }

    target_review: dict[str, Any] = {}
    for target_id, target in sorted(targets.items()):
        sessions = []
        all_related: set[str] = set()
        for log in logs:
            summary = log.get("targets", {}).get(target_id)
            sessions.append(
                {
                    "session_id": log.get("session_id", ""),
                    "observed": summary is not None,
                    "enter_count": int((summary or {}).get("enter_count", 0)),
                    "return_count": int((summary or {}).get("return_count", 0)),
                    "caller_stack_examples": (summary or {}).get("caller_stack_examples", []),
                    "argument_type_shapes": (summary or {}).get("argument_type_shapes", []),
                    "related_model_types": (summary or {}).get("related_model_types", []),
                }
            )
            all_related.update((summary or {}).get("related_model_types", []))
        target_review[target_id] = {
            "binding_ids": list(target.get("binding_ids", [])),
            "declaring_type": normalize(target.get("declaring_type")),
            "method_signature": normalize(target.get("method_signature")),
            "metadata_token": normalize(target.get("metadata_token")),
            "observed_in_all_sessions": len(logs) >= 2 and all(item["observed"] for item in sessions),
            "related_model_types": sorted(all_related),
            "sessions": sessions,
            "review": {
                "status": "unreviewed",
                "reviewer": "",
                "evidence": [],
                "notes": "",
            },
        }

    all_bindings_observed = bool(binding_review) and all(
        item["observed_in_all_sessions"] for item in binding_review.values()
    )
    if global_errors:
        status = "invalid"
    elif len(logs) < 2:
        status = "needs_second_run"
    elif all_bindings_observed:
        status = "pending_manual_review"
    else:
        status = "partial_observation"

    return {
        "schema_version": 1,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": status,
        "auto_selection_forbidden": True,
        "runtime_bindings_remain_disabled": True,
        "profile_id": normalize(manifest.get("profile_id")),
        "runtime": expected,
        "source": {
            "manifest": manifest_path.name,
            "manifest_sha256": sha256(manifest_path),
            "logs": [
                {
                    "file_name": log["path_name"],
                    "sha256": log["sha256"],
                    "session_id": log["session_id"],
                }
                for log in logs
            ],
        },
        "session_count": len(logs),
        "all_required_bindings_observed_in_all_sessions": all_bindings_observed,
        "logs": logs,
        "bindings": binding_review,
        "targets": target_review,
        "errors": sorted(set(global_errors)),
        "warnings": sorted(set(global_warnings)),
    }


def markdown(result: dict[str, Any]) -> str:
    lines = [
        "# Runtime observation review",
        "",
        f"- Status: `{result['status']}`",
        f"- Profile: `{result['profile_id']}`",
        f"- Sessions: `{result['session_count']}`",
        f"- All required bindings observed in every session: `{result['all_required_bindings_observed_in_all_sessions']}`",
        "- Automatic target selection: `forbidden`",
        "- Runtime bindings: `disabled`",
        "",
    ]
    if result["errors"]:
        lines.extend(["## Errors", ""])
        lines.extend(f"- {item}" for item in result["errors"])
        lines.append("")
    if result["warnings"]:
        lines.extend(["## Warnings", ""])
        lines.extend(f"- {item}" for item in result["warnings"])
        lines.append("")
    lines.extend(["## Binding coverage", ""])
    for binding_id, item in result["bindings"].items():
        observed = "yes" if item["observed_in_all_sessions"] else "no"
        targets = sorted({target for session in item["sessions"] for target in session["target_ids"]})
        lines.append(f"- `{binding_id}`: all sessions={observed}; targets={', '.join(targets) or 'none'}")
    lines.extend(
        [
            "",
            "This report is evidence for manual review only. It does not modify the pending profile or production integration contract.",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Analyze two or more redacted runtime observation JSONL logs without selecting game bindings."
    )
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--input", type=Path, action="append", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--strict", action="store_true")
    args = parser.parse_args()

    result = combine(args.manifest, args.input)
    args.output.mkdir(parents=True, exist_ok=True)
    json_path = args.output / "runtime-observation-review.json"
    markdown_path = args.output / "runtime-observation-review.md"
    json_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    markdown_path.write_text(markdown(result), encoding="utf-8")

    marker = "RUNTIME_OBSERVATION_INVALID" if result["status"] == "invalid" else "RUNTIME_OBSERVATION_OK"
    print(
        f"{marker} status={result['status']} sessions={result['session_count']} "
        f"all_bindings={result['all_required_bindings_observed_in_all_sessions']} output={json_path}"
    )
    if args.strict and result["status"] == "invalid":
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

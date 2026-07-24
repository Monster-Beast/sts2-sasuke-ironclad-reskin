#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

REQUIRED_BRANCH = "public-beta"


def load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def build_id(value: object, label: str) -> int:
    text = str(value or "").strip()
    if not text.isdigit() or int(text) <= 0:
        raise ValueError(f"{label} is not a valid Steam buildid")
    return int(text)


def evaluate(contract: dict[str, Any], remote_build_id: str) -> tuple[dict[str, Any], int]:
    remote = build_id(remote_build_id, "remote buildid")
    policy = contract.get("policy", {})
    if policy.get("required_branch") != REQUIRED_BRANCH or policy.get("latest_beta_only") is not True:
        raise ValueError("integration contract is not restricted to rolling public-beta")

    profiles: list[dict[str, Any]] = list(contract.get("profiles", []))
    rows: list[dict[str, Any]] = []
    current_verified: list[str] = []
    current_pending: list[str] = []
    superseded_verified: list[str] = []
    invalid_profiles: list[str] = []

    for profile in profiles:
        profile_id = str(profile.get("id", "")).strip() or "<missing-id>"
        branch = str(profile.get("branch", "")).strip()
        fingerprint_build_raw = profile.get("fingerprint", {}).get("steam_build_id")
        attestation = profile.get("beta_attestation", {})
        attested_build_raw = attestation.get("remote_build_id")
        status = str(profile.get("status", "")).strip()
        attestation_status = str(attestation.get("status", "")).strip()

        try:
            fingerprint_build = build_id(fingerprint_build_raw, f"profile {profile_id} fingerprint buildid")
            attested_build = build_id(attested_build_raw, f"profile {profile_id} attested buildid")
        except ValueError:
            invalid_profiles.append(profile_id)
            continue

        valid_shape = (
            branch == REQUIRED_BRANCH
            and fingerprint_build == attested_build
            and status in {"pending_review", "verified"}
            and attestation_status in {"pending_review", "verified"}
        )
        if not valid_shape:
            invalid_profiles.append(profile_id)
            continue

        relation = "current" if attested_build == remote else (
            "superseded" if attested_build < remote else "ahead_of_remote"
        )
        rows.append(
            {
                "id": profile_id,
                "status": status,
                "attestation_status": attestation_status,
                "build_id": str(attested_build),
                "relation": relation,
            }
        )
        if relation == "current" and status == "verified" and attestation_status == "verified":
            current_verified.append(profile_id)
        elif relation == "current":
            current_pending.append(profile_id)
        elif relation == "superseded" and status == "verified":
            superseded_verified.append(profile_id)
        elif relation == "ahead_of_remote":
            invalid_profiles.append(profile_id)

    contract_status = str(contract.get("status", "")).strip()
    active_required = contract_status == "verified"
    reasons: list[str] = []
    exit_code = 0

    if invalid_profiles:
        reasons.append("Invalid or ahead-of-remote profiles: " + ", ".join(sorted(set(invalid_profiles))))
        exit_code = 2
    if superseded_verified:
        reasons.append("Verified superseded profiles must be removed or demoted: " + ", ".join(sorted(superseded_verified)))
        exit_code = max(exit_code, 3)
    if active_required and len(current_verified) != 1:
        reasons.append(
            "A verified contract requires exactly one verified profile for the remote public-beta build; "
            f"found {len(current_verified)}."
        )
        exit_code = max(exit_code, 4)
    if not active_required and current_pending:
        reasons.append("Current beta has pending profiles: " + ", ".join(sorted(current_pending)))
    if not profiles:
        reasons.append("No game integration profiles are committed; real game bindings remain disabled.")
    if not reasons:
        reasons.append("Profile inventory matches the current remote public-beta build.")

    report = {
        "schema_version": 1,
        "contract_status": contract_status,
        "required_branch": REQUIRED_BRANCH,
        "remote_build_id": str(remote),
        "active_profile_required": active_required,
        "current_verified_profiles": sorted(current_verified),
        "current_pending_profiles": sorted(current_pending),
        "superseded_verified_profiles": sorted(superseded_verified),
        "invalid_profiles": sorted(set(invalid_profiles)),
        "profiles": sorted(rows, key=lambda row: (int(row["build_id"]), row["id"])),
        "healthy": exit_code == 0,
        "reasons": reasons,
    }
    return report, exit_code


def markdown(report: dict[str, Any]) -> str:
    lines = [
        "# Slay the Spire 2 public-beta Profile 状态",
        "",
        f"- 远端 buildid：`{report['remote_build_id']}`",
        f"- Contract：`{report['contract_status']}`",
        f"- 健康：`{report['healthy']}`",
        "",
    ]
    for reason in report["reasons"]:
        lines.append(f"- {reason}")
    lines.extend(
        [
            "",
            "| Profile | 状态 | Attestation | Build | 与远端关系 |",
            "|---|---|---|---:|---|",
        ]
    )
    for profile in report["profiles"]:
        lines.append(
            f"| `{profile['id']}` | {profile['status']} | {profile['attestation_status']} | "
            f"{profile['build_id']} | {profile['relation']} |"
        )
    if not report["profiles"]:
        lines.append("| - | - | - | - | 当前没有 Profile |")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Compare committed integration profiles with the current remote public-beta buildid."
    )
    parser.add_argument("--contract", type=Path, required=True)
    parser.add_argument("--remote-build-id", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    try:
        report, exit_code = evaluate(load(args.contract), args.remote_build_id)
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        print(f"BETA_PROFILE_STATUS_ERROR {exc}", file=sys.stderr)
        return 2

    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "beta-profile-status.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    (args.output / "beta-profile-status.md").write_text(markdown(report), encoding="utf-8")
    print(
        f"BETA_PROFILE_STATUS healthy={str(report['healthy']).lower()} "
        f"remote_build={report['remote_build_id']} profiles={len(report['profiles'])}"
    )
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())

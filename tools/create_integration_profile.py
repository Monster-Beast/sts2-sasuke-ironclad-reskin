#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT_PATH = ROOT / "SasukeIronclad/data/game_integration_contract.json"


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def safe_profile_id(branch: str, build_id: str, assembly_hash: str) -> str:
    raw = f"{branch}-{build_id}-{assembly_hash[:12]}".lower()
    value = re.sub(r"[^a-z0-9._-]+", "-", raw).strip("-")
    if not value:
        raise ValueError("could not derive a profile id")
    return value


def report_digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Create a pending-review integration profile from two equivalent local audit runs."
    )
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--comparison", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--profile-id")
    args = parser.parse_args()

    report = load(args.report)
    comparison = load(args.comparison)
    contract = load(CONTRACT_PATH)

    if report.get("schemaVersion") != 1:
        raise SystemExit("unsupported audit report schema")
    if comparison.get("schemaVersion") != 1 or comparison.get("equivalent") is not True:
        raise SystemExit("two equivalent audit runs are required before creating a profile template")

    session = str(report.get("session", ""))
    compared_sessions = {
        str(comparison.get("firstSession", "")),
        str(comparison.get("secondSession", "")),
    }
    if not session or session not in compared_sessions:
        raise SystemExit("the selected report was not part of the supplied audit comparison")

    game = report.get("game", {})
    branch = str(report.get("branch", "")).strip()
    build_id = str(game.get("steamBuildId") or "").strip()
    assembly_hash = str(game.get("sts2AssemblySha256") or "").strip().lower()
    module_mvid = str(game.get("moduleVersionId") or "").strip().lower()
    baselib_version = str(game.get("baseLibVersion") or "").strip()

    missing = [
        name
        for name, value in [
            ("branch", branch),
            ("steamBuildId", build_id),
            ("sts2AssemblySha256", assembly_hash),
            ("moduleVersionId", module_mvid),
            ("baseLibVersion", baselib_version),
        ]
        if not value or value == "unknown"
    ]
    if missing:
        raise SystemExit("audit report is missing exact fingerprint fields: " + ", ".join(missing))
    if not re.fullmatch(r"[0-9a-f]{64}", assembly_hash):
        raise SystemExit("sts2 assembly SHA-256 is invalid")
    if not re.fullmatch(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", module_mvid):
        raise SystemExit("module MVID is invalid")

    profile_id = args.profile_id or safe_profile_id(branch, build_id, assembly_hash)
    visual_bindings = [
        {
            "id": event_id,
            "declaring_type": "",
            "method_signature": "",
            "metadata_token": "",
            "status": "pending_review",
            "fallback": "original_visual",
        }
        for event_id in contract["required_visual_events"]
    ]
    title_bindings = [
        {
            "surface_id": surface_id,
            "declaring_type": "",
            "method_signature": "",
            "metadata_token": "",
            "status": "pending_review",
            "fallback": "original_title",
        }
        for surface_id in contract["required_title_surfaces"]
    ]

    template = {
        "schema_version": 1,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "evidence": {
            "report_session": session,
            "comparison_sessions": sorted(compared_sessions),
            "report_sha256": report_digest(args.report),
            "comparison_sha256": report_digest(args.comparison),
            "two_runs_equivalent": True,
            "manual_symbol_review_required": True,
        },
        "profile": {
            "id": profile_id,
            "status": "pending_review",
            "branch": branch,
            "fingerprint": {
                "steam_build_id": build_id,
                "sts2_sha256": assembly_hash,
                "module_mvid": module_mvid,
                "baselib_version": baselib_version,
            },
            "visual_bindings": visual_bindings,
            "title_bindings": title_bindings,
        },
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(template, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        f"INTEGRATION_PROFILE_TEMPLATE_OK profile={profile_id} "
        f"visuals={len(visual_bindings)} titles={len(title_bindings)} output={args.output}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

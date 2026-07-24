#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_module():
    path = ROOT / "tools/check_latest_beta_profiles.py"
    spec = importlib.util.spec_from_file_location("beta_profiles", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def make_profile(profile_id: str, build: str, status: str = "verified") -> dict:
    return {
        "id": profile_id,
        "status": status,
        "branch": "public-beta",
        "fingerprint": {
            "steam_build_id": build,
            "sts2_sha256": "a" * 64,
            "module_mvid": "11111111-2222-3333-4444-555555555555",
            "baselib_version": "v1",
        },
        "beta_attestation": {
            "status": "verified",
            "branch": "public-beta",
            "installed_build_id": build,
            "remote_build_id": build,
            "checked_at_utc": "2026-07-21T00:00:00+00:00",
            "source": "steamcmd_app_info_print",
            "steamcmd_output_sha256": "b" * 64,
        },
        "visual_bindings": [],
        "title_bindings": [],
    }


def make_contract(status: str, profiles: list[dict]) -> dict:
    return {
        "schema_version": 1,
        "gameplay_changes": False,
        "status": status,
        "policy": {"required_branch": "public-beta", "latest_beta_only": True},
        "profiles": profiles,
    }


def main() -> int:
    module = load_module()

    empty, code = module.evaluate(make_contract("pending_local_audit", []), "200")
    assert code == 0 and empty["healthy"] is True

    current, code = module.evaluate(
        make_contract("verified", [make_profile("current", "200")]), "200")
    assert code == 0 and current["current_verified_profiles"] == ["current"]

    old, code = module.evaluate(
        make_contract("verified", [make_profile("old", "199")]), "200")
    assert code != 0 and old["superseded_verified_profiles"] == ["old"]

    pending, code = module.evaluate(
        make_contract("pending_local_audit", [make_profile("next", "200", "pending_review")]), "200")
    assert code == 0 and pending["current_pending_profiles"] == ["next"]

    duplicate, code = module.evaluate(
        make_contract("verified", [make_profile("a", "200"), make_profile("b", "200")]), "200")
    assert code != 0 and len(duplicate["current_verified_profiles"]) == 2

    future, code = module.evaluate(
        make_contract("pending_local_audit", [make_profile("future", "201", "pending_review")]), "200")
    assert code != 0 and future["invalid_profiles"] == ["future"]

    print("LATEST_BETA_PROFILES_OK empty=true current=true old_rejected=true duplicate_rejected=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "SasukeIronclad/data/game_integration_contract.json"
SCOPE = ROOT / "SasukeIronclad/data/current_beta_card_scope.json"
PROFILE = ROOT / "SasukeIronclad/data/integration_profiles/public-beta-24251656-ee45848ff631.pending-review.json"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    scope = json.loads(SCOPE.read_text(encoding="utf-8"))
    template = json.loads(PROFILE.read_text(encoding="utf-8"))
    profile = template.get("profile", {})
    evidence = template.get("evidence", {})

    require(contract.get("status") == "pending_local_audit", "runtime contract must remain disabled")
    require(contract.get("profiles") == [], "pending profile must not enter the runtime contract")
    require(scope.get("status") == "pending_review", "card scope must remain pending_review")
    require(len(scope.get("active_cards", [])) == 10, "active beta card count changed")
    require(len(scope.get("design_only_absent_cards", [])) == 3, "design-only card count changed")
    require(scope.get("evidence", {}).get("two_runs_equivalent") is True, "scope lacks two-run evidence")

    require(template.get("schema_version") == 1, "unsupported profile template schema")
    require(profile.get("status") == "pending_review", "profile template must remain pending_review")
    require(profile.get("branch") == "public-beta", "profile branch changed")
    require(profile.get("card_scope_file") == "SasukeIronclad/data/current_beta_card_scope.json", "profile scope link changed")
    scope_fingerprint = scope.get("fingerprint", {})
    profile_fingerprint = profile.get("fingerprint", {})
    require(profile_fingerprint.get("steam_build_id") == scope.get("steam_build_id"), "buildid mismatch")
    for key in ["sts2_sha256", "module_mvid", "baselib_version", "baselib_manifest_sha256"]:
        require(profile_fingerprint.get(key) == scope_fingerprint.get(key), f"fingerprint mismatch: {key}")

    require(evidence.get("two_runs_equivalent") is True, "profile lacks two-run evidence")
    require(evidence.get("active_card_scope_verified") is True, "profile lacks card-scope evidence")
    require(evidence.get("active_card_count") == 10, "profile active card count changed")
    require(evidence.get("design_only_absent_card_count") == 3, "profile design-only count changed")
    require(evidence.get("manual_symbol_review_required") is True, "manual review requirement was removed")

    visual_ids = {item.get("id") for item in profile.get("visual_bindings", [])}
    title_ids = {item.get("surface_id") for item in profile.get("title_bindings", [])}
    require(visual_ids == set(contract.get("required_visual_events", [])), "visual binding template changed")
    require(title_ids == set(contract.get("required_title_surfaces", [])), "title binding template changed")
    for binding in profile.get("visual_bindings", []) + profile.get("title_bindings", []):
        require(binding.get("status") == "pending_review", "an unreviewed binding was enabled")
        require(not binding.get("metadata_token"), "an unreviewed binding contains a token")
        require(not binding.get("declaring_type"), "an unreviewed binding contains a declaring type")
        require(not binding.get("method_signature"), "an unreviewed binding contains a signature")

    print("PENDING_BETA_PROFILE_OK build=24251656 active=10 design_only=3 bindings=12 fail_closed=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "SasukeIronclad/data/game_integration_contract.json"
BETA_POLICY = ROOT / "SasukeIronclad/data/latest_beta_policy.json"
AUDIT_QUERIES = ROOT / "tools/game_audit/audit-queries.json"
METADATA_SCANNER = ROOT / "tools/game_audit/MetadataScanner.cs"
MAIN = ROOT / "SasukeIroncladCode/MainFile.cs"
REQUIRED_FILES = [
    ROOT / "SasukeIroncladCode/Runtime/GameIntegrationGate.cs",
    ROOT / "SasukeIroncladCode/Runtime/GameIntegrationBootstrap.cs",
    ROOT / "SasukeIroncladCode/Runtime/RuntimeBuildFingerprintCollector.cs",
    ROOT / "SasukeIroncladCode/Visuals/GameIntegrationConfiguration.cs",
    ROOT / "tools/latest_beta_guard.py",
    ROOT / "tools/create_integration_profile.py",
    ROOT / "tools/build_audit_review.py",
    ROOT / "tools/validate_real_audit_evidence.py",
    ROOT / "tools/compile_reviewed_profile.py",
]

REQUIRED_VISUALS = {
    "card_visual_request",
    "original_impact",
    "state_removed",
    "form_removed",
    "combat_ended",
    "character_state",
}
REQUIRED_TITLES = {"card_art", "hand", "deck_list", "reward", "compendium", "tooltip"}
TARGET_CARD_TYPES = {
    "MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad",
    "MegaCrit.Sts2.Core.Models.Cards.DefendIronclad",
    "MegaCrit.Sts2.Core.Models.Cards.Bash",
    "MegaCrit.Sts2.Core.Models.Cards.Anger",
    "MegaCrit.Sts2.Core.Models.Cards.Cleave",
    "MegaCrit.Sts2.Core.Models.Cards.Thunderclap",
    "MegaCrit.Sts2.Core.Models.Cards.HeavyBlade",
    "MegaCrit.Sts2.Core.Models.Cards.FlameBarrier",
    "MegaCrit.Sts2.Core.Models.Cards.Whirlwind",
    "MegaCrit.Sts2.Core.Models.Cards.BurningPact",
    "MegaCrit.Sts2.Core.Models.Cards.DemonForm",
    "MegaCrit.Sts2.Core.Models.Cards.LimitBreak",
    "MegaCrit.Sts2.Core.Models.Cards.FiendFire",
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    require(CONTRACT.is_file(), "game integration contract is missing")
    require(BETA_POLICY.is_file(), "latest beta policy is missing")
    require(AUDIT_QUERIES.is_file(), "audit query policy is missing")
    require(METADATA_SCANNER.is_file(), "metadata scanner is missing")
    for path in REQUIRED_FILES:
        require(path.is_file(), f"required integration component is missing: {path.relative_to(ROOT)}")

    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    beta_policy = json.loads(BETA_POLICY.read_text(encoding="utf-8"))
    audit_queries = json.loads(AUDIT_QUERIES.read_text(encoding="utf-8"))

    require(contract.get("schema_version") == 1, "unsupported integration contract schema")
    require(contract.get("gameplay_changes") is False, "integration contract enables gameplay changes")
    require(contract.get("status") == "pending_local_audit", "repository contract must remain disabled")
    require(contract.get("profiles") == [], "repository must not ship an unaudited game profile")
    require(set(contract.get("required_visual_events", [])) == REQUIRED_VISUALS, "visual event contract changed")
    require(set(contract.get("required_title_surfaces", [])) == REQUIRED_TITLES, "title surface contract changed")

    policy = contract.get("policy", {})
    for key in [
        "exact_build_fingerprint_required",
        "unverified_bindings_disabled",
        "fallback_to_original_on_mismatch",
        "multiplayer_local_visuals_only",
        "latest_beta_only",
        "remote_beta_attestation_required",
        "stale_profiles_disabled",
    ]:
        require(policy.get(key) is True, f"integration safety flag is not enabled: {key}")
    require(policy.get("required_branch") == "public-beta", "integration branch must be public-beta")
    attestation_hours = int(policy.get("max_beta_attestation_age_hours", 0))
    require(1 <= attestation_hours <= 168, "beta attestation age window is unsafe")

    require(beta_policy.get("schema_version") == 1, "unsupported beta policy schema")
    require(beta_policy.get("app_id") == "2868840", "wrong Steam app id")
    require(beta_policy.get("required_branch") == "public-beta", "beta policy branch drifted")
    require(beta_policy.get("tracking_mode") == "rolling_latest", "beta policy is not rolling latest")
    require(beta_policy.get("remote_build_source") == "steamcmd_app_info_print", "wrong remote build source")
    require(beta_policy.get("profile_must_match_attested_build") is True, "profile/build match is not required")
    require(beta_policy.get("stale_profiles_disabled") is True, "stale profiles are not disabled")
    require(beta_policy.get("max_attestation_age_hours") == attestation_hours, "policy age windows disagree")

    require(audit_queries.get("schemaVersion") == 1, "unsupported audit query schema")
    priority_queries = [item for item in audit_queries.get("queries", []) if item.get("priority") is True]
    priority_types = {
        str(type_name)
        for query in priority_queries
        for type_name in query.get("exactTypeNames", [])
        if str(type_name).strip()
    }
    require(TARGET_CARD_TYPES.issubset(priority_types), "target card types are not protected from symbol truncation")
    scanner_text = METADATA_SCANNER.read_text(encoding="utf-8")
    require("PriorityExactTypeScore" in scanner_text, "priority type scoring was removed")
    require("IsExactTypeOrNested" in scanner_text, "exact target type matching was removed")

    main_file = MAIN.read_text(encoding="utf-8")
    require("PatchAll" not in main_file, "unconditional Harmony PatchAll returned to startup")
    require("GameIntegrationBootstrap.Start" in main_file, "startup no longer uses the integration gate")
    require("PendingGameIntegrationInstaller" in main_file, "startup is not fail-closed")

    print(
        "OK: repository contract is disabled and structurally restricted to a remotely attested "
        "rolling public-beta build. Exact target card types are protected from bounded-report truncation."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

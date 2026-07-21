#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "SasukeIronclad/data/game_integration_contract.json"
BETA_POLICY = ROOT / "SasukeIronclad/data/latest_beta_policy.json"
MAIN = ROOT / "SasukeIroncladCode/MainFile.cs"
BOOTSTRAP = ROOT / "SasukeIroncladCode/Runtime/GameIntegrationBootstrap.cs"
COLLECTOR = ROOT / "SasukeIroncladCode/Runtime/RuntimeBuildFingerprintCollector.cs"
GATE = ROOT / "SasukeIroncladCode/Runtime/GameIntegrationGate.cs"
MODELS = ROOT / "SasukeIroncladCode/Visuals/GameIntegrationConfiguration.cs"
BETA_GUARD = ROOT / "tools/latest_beta_guard.py"
PROFILE_GENERATOR = ROOT / "tools/create_integration_profile.py"
REVIEW_BUILDER = ROOT / "tools/build_audit_review.py"
REVIEW_COMPILER = ROOT / "tools/compile_reviewed_profile.py"

REQUIRED_VISUALS = {
    "card_visual_request",
    "original_impact",
    "state_removed",
    "form_removed",
    "combat_ended",
    "character_state",
}
REQUIRED_TITLES = {"card_art", "hand", "deck_list", "reward", "compendium", "tooltip"}


def read(path: Path) -> str:
    if not path.is_file():
        raise AssertionError(f"required file is missing: {path.relative_to(ROOT)}")
    return path.read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    contract = json.loads(read(CONTRACT))
    beta_policy = json.loads(read(BETA_POLICY))

    require(contract.get("schema_version") == 1, "unsupported integration contract schema")
    require(contract.get("gameplay_changes") is False, "integration contract enables gameplay changes")
    require(contract.get("status") == "pending_local_audit", "repository contract must remain disabled")
    require(contract.get("profiles") == [], "repository must not ship an unaudited game profile")
    require(set(contract.get("required_visual_events", [])) == REQUIRED_VISUALS, "visual event contract changed")
    require(set(contract.get("required_title_surfaces", [])) == REQUIRED_TITLES, "title surface contract changed")

    policy = contract.get("policy", {})
    required_true = {
        "exact_build_fingerprint_required",
        "unverified_bindings_disabled",
        "fallback_to_original_on_mismatch",
        "multiplayer_local_visuals_only",
        "latest_beta_only",
        "remote_beta_attestation_required",
        "stale_profiles_disabled",
    }
    missing_guards = sorted(key for key in required_true if policy.get(key) is not True)
    require(not missing_guards, "integration safety flags are missing: " + ", ".join(missing_guards))
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

    sources = {
        "main": read(MAIN),
        "bootstrap": read(BOOTSTRAP),
        "collector": read(COLLECTOR),
        "gate": read(GATE),
        "models": read(MODELS),
        "guard": read(BETA_GUARD),
        "profile_generator": read(PROFILE_GENERATOR),
        "review_builder": read(REVIEW_BUILDER),
        "review_compiler": read(REVIEW_COMPILER),
    }
    require("PatchAll" not in "\n".join(sources.values()), "unconditional Harmony PatchAll returned")
    require("HarmonyPatch" not in sources["main"] + sources["bootstrap"] + sources["collector"],
            "startup contains an unaudited Harmony target")
    require("GameIntegrationBootstrap.Start" in sources["main"], "startup does not use the integration gate")
    require("PendingGameIntegrationInstaller" in sources["main"], "startup is not fail-closed")

    require("public-beta" in sources["collector"], "runtime collector cannot identify public-beta")
    require("latest_beta_guard" in BETA_GUARD.stem, "latest beta guard entry point is missing")
    require("--latest-beta-attestation" in sources["profile_generator"],
            "profile templates are not tied to beta attestation")
    require("--latest-beta-attestation" in sources["review_builder"],
            "review workbooks are not tied to beta attestation")
    require("beta_attestation" in sources["review_compiler"],
            "compiled profiles do not retain beta attestation")
    require("GameBetaAttestationSpec" in sources["models"], "runtime configuration lacks beta attestation model")
    require("IsFreshLatestBetaProfile" in sources["gate"], "runtime gate does not reject stale beta profiles")

    print(
        "OK: repository integration is disabled by default and restricted to a fresh, "
        "remotely attested rolling public-beta build."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

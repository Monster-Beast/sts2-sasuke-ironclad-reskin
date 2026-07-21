#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "SasukeIronclad/data/game_integration_contract.json"
BETA_POLICY = ROOT / "SasukeIronclad/data/latest_beta_policy.json"
GATE = ROOT / "SasukeIroncladCode/Runtime/GameIntegrationGate.cs"
BOOTSTRAP = ROOT / "SasukeIroncladCode/Runtime/GameIntegrationBootstrap.cs"
COLLECTOR = ROOT / "SasukeIroncladCode/Runtime/RuntimeBuildFingerprintCollector.cs"
MAIN = ROOT / "SasukeIroncladCode/MainFile.cs"
MODELS = ROOT / "SasukeIroncladCode/Visuals/GameIntegrationConfiguration.cs"
PROFILE_GENERATOR = ROOT / "tools/create_integration_profile.py"
REVIEW_COMPILER = ROOT / "tools/compile_reviewed_profile.py"
BETA_GUARD = ROOT / "tools/latest_beta_guard.py"
GATE_TEST = ROOT / "tools/integration_gate_test/Program.cs"
ADDITIONAL_GATE_TEST = ROOT / "tools/integration_gate_test/AdditionalSafetyTests.cs"

REQUIRED_VISUALS = {
    "card_visual_request",
    "original_impact",
    "state_removed",
    "form_removed",
    "combat_ended",
    "character_state",
}
REQUIRED_TITLES = {"card_art", "hand", "deck_list", "reward", "compendium", "tooltip"}


def main() -> int:
    raw_contract = CONTRACT.read_text(encoding="utf-8")
    data = json.loads(raw_contract)
    beta_policy = json.loads(BETA_POLICY.read_text(encoding="utf-8"))
    assert data["schema_version"] == 1
    assert data["gameplay_changes"] is False
    assert data["status"] == "pending_local_audit"
    assert data["profiles"] == []
    assert set(data["required_visual_events"]) == REQUIRED_VISUALS
    assert set(data["required_title_surfaces"]) == REQUIRED_TITLES

    policy = data["policy"]
    for key in [
        "exact_build_fingerprint_required",
        "unverified_bindings_disabled",
        "fallback_to_original_on_mismatch",
        "multiplayer_local_visuals_only",
        "latest_beta_only",
        "remote_beta_attestation_required",
        "stale_profiles_disabled",
    ]:
        assert policy[key] is True, key
    assert policy["required_branch"] == "public-beta"
    assert 1 <= int(policy["max_beta_attestation_age_hours"]) <= 168

    assert beta_policy["required_branch"] == "public-beta"
    assert beta_policy["tracking_mode"] == "rolling_latest"
    assert beta_policy["remote_build_source"] == "steamcmd_app_info_print"
    assert beta_policy["profile_must_match_attested_build"] is True
    assert beta_policy["stale_profiles_disabled"] is True
    assert beta_policy["max_attestation_age_hours"] == policy["max_beta_attestation_age_hours"]

    gate = GATE.read_text(encoding="utf-8")
    bootstrap = BOOTSTRAP.read_text(encoding="utf-8")
    collector = COLLECTOR.read_text(encoding="utf-8")
    main_file = MAIN.read_text(encoding="utf-8")
    models = MODELS.read_text(encoding="utf-8")
    generator = PROFILE_GENERATOR.read_text(encoding="utf-8")
    review_compiler = REVIEW_COMPILER.read_text(encoding="utf-8")
    beta_guard = BETA_GUARD.read_text(encoding="utf-8")
    gate_test = GATE_TEST.read_text(encoding="utf-8")
    additional_gate_test = ADDITIONAL_GATE_TEST.read_text(encoding="utf-8")

    for field in [
        "SteamBuildId", "Sts2Sha256", "ModuleMvid", "BaseLibVersion",
        "RequiredBranch", "LatestBetaOnly", "BetaAttestation",
    ]:
        assert field in gate + models
    for contract in [
        "contract.Status != \"verified\"",
        "profile.Status != \"verified\"",
        "No exact audited build profile matched",
        "Multiple integration profiles matched",
        "Duplicate build fingerprint",
        "IsFreshLatestBetaProfile",
        "beta attestation is stale",
        "RollingBetaBranch",
        "IsVerified",
        "IsMethodDefinitionToken",
        "0x06000000u",
        "original_title",
        "original_visual",
        "MultiplayerLocalVisualsOnly",
        "ExactBuildFingerprintRequired",
    ]:
        assert contract in gate

    assert "JsonPropertyName(\"status\")" in models
    assert "JsonPropertyName(\"beta_attestation\")" in models
    assert "HarmonyPatch" not in gate + bootstrap + collector
    assert "PatchAll" not in gate + bootstrap + collector + main_file
    assert '"profiles": []' in raw_contract
    assert "GameIntegrationBootstrap.Start" in main_file
    assert "PendingGameIntegrationInstaller" in main_file

    for contract in [
        "SHA256.HashData",
        "ReadModuleMvid",
        "ReadSteamMetadata",
        "betakey",
        "workshop",
        "BaseLib",
        "STS2_BRANCH",
        "public-beta",
    ]:
        assert contract in collector

    for contract in [
        '"status": "pending_review"',
        "two equivalent audit runs are required",
        "manual_symbol_review_required",
        "INTEGRATION_PROFILE_TEMPLATE_OK",
    ]:
        assert contract in generator

    for contract in [
        "METHOD_TOKEN_RE",
        "non-method metadata target",
        "non-MethodDef metadata token",
        "latest public-beta",
        "beta_attestation",
        '"status": "pending_review"',
    ]:
        assert contract in review_compiler

    for contract in [
        "DEFAULT_BRANCH = \"public-beta\"",
        "LATEST_BETA_OK",
        "LATEST_BETA_STALE",
        "steamcmd_app_info_print",
        "installed_build_id",
        "remote_build_id",
    ]:
        assert contract in beta_guard

    for contract in [
        "pending global contract unexpectedly enabled bindings",
        "pending profile unexpectedly enabled bindings",
        "mismatched assembly hash unexpectedly enabled bindings",
        "stable branch unexpectedly enabled",
        "stale beta attestation unexpectedly enabled",
        "pending title surface unexpectedly enabled title bindings",
        "duplicate fingerprint was accepted",
        "bootstrap=true",
        "collector=true",
        "fail_closed=true",
        "INTEGRATION_GATE_OK",
    ]:
        assert contract in gate_test

    for contract in [
        "A non-MethodDef title token was not rejected",
        "Workshop BaseLib",
        "public-beta",
        "Exact audited MethodDef did not resolve",
        "mismatched method signature was accepted",
        "mismatched module MVID was accepted",
    ]:
        assert contract in additional_gate_test

    print(
        "OK: integration is limited to a fresh, remotely attested public-beta build; "
        "verified MethodDef bindings and installer behavior remain fail-closed."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

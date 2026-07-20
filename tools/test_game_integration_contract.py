#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "SasukeIronclad/data/game_integration_contract.json"
GATE = ROOT / "SasukeIroncladCode/Runtime/GameIntegrationGate.cs"
MODELS = ROOT / "SasukeIroncladCode/Visuals/GameIntegrationConfiguration.cs"
PROFILE_GENERATOR = ROOT / "tools/create_integration_profile.py"
GATE_TEST = ROOT / "tools/integration_gate_test/Program.cs"

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
    ]:
        assert policy[key] is True, key

    gate = GATE.read_text(encoding="utf-8")
    models = MODELS.read_text(encoding="utf-8")
    generator = PROFILE_GENERATOR.read_text(encoding="utf-8")
    gate_test = GATE_TEST.read_text(encoding="utf-8")

    for field in ["SteamBuildId", "Sts2Sha256", "ModuleMvid", "BaseLibVersion"]:
        assert field in gate and field in models
    for contract in [
        "contract.Status != \"verified\"",
        "profile.Status != \"verified\"",
        "No exact audited build profile matched",
        "Multiple integration profiles matched",
        "Duplicate build fingerprint",
        "IsVerified",
        "original_title",
        "MultiplayerLocalVisualsOnly",
        "ExactBuildFingerprintRequired",
    ]:
        assert contract in gate

    assert "JsonPropertyName(\"status\")" in models
    assert "HarmonyPatch" not in gate
    assert "PatchAll" not in gate
    assert '"profiles": []' in raw_contract

    for contract in [
        '"status": "pending_review"',
        "two equivalent audit runs are required",
        "manual_symbol_review_required",
        "INTEGRATION_PROFILE_TEMPLATE_OK",
    ]:
        assert contract in generator

    for contract in [
        "pending global contract unexpectedly enabled bindings",
        "pending profile unexpectedly enabled bindings",
        "mismatched assembly hash unexpectedly enabled bindings",
        "pending title surface unexpectedly enabled title bindings",
        "duplicate fingerprint was accepted",
        "INTEGRATION_GATE_OK",
    ]:
        assert contract in gate_test

    print(
        "OK: exact-build integration requires verified contract, profile and bindings; "
        "profile generation remains pending-review and all mismatches fall back."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

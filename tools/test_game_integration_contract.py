#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "SasukeIronclad/data/game_integration_contract.json"
GATE = ROOT / "SasukeIroncladCode/Runtime/GameIntegrationGate.cs"
MODELS = ROOT / "SasukeIroncladCode/Visuals/GameIntegrationConfiguration.cs"

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
    data = json.loads(CONTRACT.read_text(encoding="utf-8"))
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
    for field in ["SteamBuildId", "Sts2Sha256", "ModuleMvid", "BaseLibVersion"]:
        assert field in gate and field in models
    for contract in [
        "No exact audited build profile matched",
        "IsVerified",
        "original_title",
        "MultiplayerLocalVisualsOnly",
        "ExactBuildFingerprintRequired",
    ]:
        assert contract in gate

    assert "HarmonyPatch" not in gate
    assert "PatchAll" not in gate
    assert "profiles\": []" in CONTRACT.read_text(encoding="utf-8")

    print(
        "OK: integration remains disabled until an exact audited build profile "
        "and all required visual/title bindings are verified."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REVIEW_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-runtime-binding-review.json"
MANIFEST_PATH = ROOT / "SasukeIronclad/data/runtime_observation_targets.json"
CONTRACT_PATH = ROOT / "SasukeIronclad/data/game_integration_contract.json"
PROFILE_PATH = ROOT / "SasukeIronclad/data/integration_profiles/public-beta-24251656-ee45848ff631.pending-review.json"
SCOPE_PATH = ROOT / "SasukeIronclad/data/current_beta_card_scope.json"
SCRIPT_PATH = ROOT / "tools/runtime-canary.ps1"
MAIN_PATH = ROOT / "SasukeIroncladCode/MainFile.cs"
LOCAL_FILES_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryLocalFiles.cs"
SESSION_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanarySession.cs"

EXPECTED_APPROVED = {
    "card_visual_request", "original_impact", "state_removed", "combat_ended",
    "card_art", "hand", "deck_list", "reward", "compendium", "tooltip",
}
EXPECTED_BLOCKED = {"form_removed", "character_state"}
EXPECTED_LOG_HASHES = {
    "338d85a75625876ffdaa8c29fc3452aacc2c48b35fce6e1d0153e66869b4a5b6",
    "bd22300d335cb6292051f0bb6d54e9e77837d5926a7b94c049bd001eb35f9392",
}


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    review = load(REVIEW_PATH)
    manifest = load(MANIFEST_PATH)
    contract = load(CONTRACT_PATH)
    profile = load(PROFILE_PATH)["profile"]
    scope = load(SCOPE_PATH)

    require(review["schema_version"] == 1 and review["status"] == "partial_manual_review", "manual review status changed")
    require(review["profile_id"] == manifest["profile_id"] == profile["id"], "review identity mismatch")
    require(review["runtime"]["steam_build_id"] == manifest["fingerprint"]["steam_build_id"] == scope["steam_build_id"], "buildid mismatch")
    require(review["runtime"]["sts2_sha256"] == manifest["fingerprint"]["sts2_sha256"], "assembly digest mismatch")
    require(review["runtime"]["module_mvid"] == manifest["fingerprint"]["module_mvid"], "MVID mismatch")
    require(review["runtime"]["baselib_version"] == manifest["fingerprint"]["baselib_version"], "BaseLib mismatch")
    require({item["sha256"] for item in review["source"]["logs"]} == EXPECTED_LOG_HASHES, "runtime log digest set changed")
    require(review["quality"]["event_counts"] == [4279, 6109], "reviewed event counts changed")
    require(review["quality"]["balanced_call_pairs"] is True and review["quality"]["errors"] == [], "runtime evidence is invalid")

    decisions = {item["binding_id"]: item for item in review["decisions"]}
    approved = {key for key, item in decisions.items() if item["status"] == "approved_for_canary"}
    blocked = {key for key, item in decisions.items() if item["status"] == "needs_targeted_observation"}
    require(approved == EXPECTED_APPROVED, "approved canary binding set changed")
    require(blocked == EXPECTED_BLOCKED, "blocked canary binding set changed")
    require(all(decisions[key]["selected_target_id"] == "" for key in blocked), "blocked binding was selected")
    require(review["readiness"] == {
        "title_canary_candidate_count": 6,
        "visual_canary_candidate_count": 4,
        "blocked_binding_count": 2,
        "production_profile_ready": False,
    }, "canary readiness changed")

    require(contract["status"] == "pending_local_audit" and contract["profiles"] == [], "production contract was enabled")
    require(profile["status"] == "pending_review", "pending profile was promoted")
    require(all(not item["metadata_token"] for item in profile["visual_bindings"] + profile["title_bindings"]), "pending profile contains selected methods")
    require(scope["gameplay_changes"] is False and len(scope["active_cards"]) == 10, "active card scope changed")

    script_bytes = SCRIPT_PATH.read_bytes()
    require(all(byte < 128 for byte in script_bytes), "runtime-canary.ps1 must remain ASCII-only")
    script_text = script_bytes.decode("ascii")
    main_text = MAIN_PATH.read_text(encoding="utf-8")
    local_text = LOCAL_FILES_PATH.read_text(encoding="utf-8")
    session_text = SESSION_PATH.read_text(encoding="utf-8")
    require("local_visual_only" in script_text, "canary marker mode changed")
    require("RuntimeCanaryLocalFiles.LoadOptIn" in main_text and "RuntimeCanaryLocalFiles.WriteStatus" in main_text, "canary startup wiring is missing")
    require("SasukeIronclad.canary.json" in local_text and "runtime-canary-status.json" in local_text, "canary local filenames changed")
    require("Runtime observation marker is present" in local_text, "simultaneous observation is not rejected")
    require("Demon Form" in session_text and "targeted run proves the exact form-removal event" in session_text, "Demon Form is not fail-closed")
    require("ReferenceEquals(sourceCardModel, _activeCardModel)" in session_text, "impact forwarding is not scoped to the active local card")

    print("RUNTIME_CANARY_CONTRACT_OK sessions=2 events=10388 approved=10 blocked=2 explicit_opt_in=true production=false")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

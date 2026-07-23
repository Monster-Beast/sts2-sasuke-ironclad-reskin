#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "SasukeIronclad/data/runtime_observation_targets.json"
SCOPE_PATH = ROOT / "SasukeIronclad/data/current_beta_card_scope.json"
PROFILE_PATH = ROOT / "SasukeIronclad/data/integration_profiles/public-beta-24251656-ee45848ff631.pending-review.json"
CONTRACT_PATH = ROOT / "SasukeIronclad/data/game_integration_contract.json"
BINDING_REVIEW_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-runtime-binding-review.json"
CONTROL_SCRIPT_PATH = ROOT / "tools/runtime-observation.ps1"
MAIN_FILE_PATH = ROOT / "SasukeIroncladCode/MainFile.cs"
BOOTSTRAP_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeObservationBootstrap.cs"
GATE_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeObservationGate.cs"
METHOD_TOKEN_RE = re.compile(r"^0x06[0-9A-Fa-f]{6}$")
EXPECTED_CANARY_BINDINGS = {
    "card_visual_request": "card-play.local-queue",
    "original_impact": "impact.damage-history",
    "state_removed": "power.container-removed",
    "combat_ended": "combat.room-ended",
    "card_art": "title.card-label-update",
    "hand": "title.hand-card-update",
    "deck_list": "title.deck-grid-set",
    "reward": "title.reward-refresh",
    "compendium": "title.compendium-grid-display",
    "tooltip": "title.card-hover",
}
EXPECTED_TARGETED_BINDINGS = {"form_removed", "character_state"}
EXPECTED_RUNTIME_LOG_SHA256 = {
    "c7d6f7dbe679ec57c70de9a36eb3f009565992ef4f7a253dfd882c01e4e6e24f",
    "97f411fa0761bbd9dc644227477c1c3ac19ea354eabb0547ea6471ec33ffbf18",
}


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    manifest = load(MANIFEST_PATH)
    scope = load(SCOPE_PATH)
    profile = load(PROFILE_PATH)["profile"]
    contract = load(CONTRACT_PATH)
    binding_review = load(BINDING_REVIEW_PATH)

    require(manifest.get("schema_version") == 1, "unsupported observation manifest schema")
    require(manifest.get("status") == "observation_only", "observation manifest status changed")
    require(manifest.get("gameplay_changes") is False, "observation manifest permits gameplay changes")
    require(manifest.get("profile_id") == profile.get("id"), "observation manifest/profile ID mismatch")
    require(manifest.get("branch") == "public-beta", "observation manifest is not public-beta only")

    manifest_fingerprint = manifest["fingerprint"]
    profile_fingerprint = profile["fingerprint"]
    scope_fingerprint = scope["fingerprint"]
    require(manifest_fingerprint["steam_build_id"] == scope["steam_build_id"], "scope buildid mismatch")
    for key in ("sts2_sha256", "module_mvid", "baselib_version"):
        require(
            manifest_fingerprint[key] == profile_fingerprint[key] == scope_fingerprint[key],
            f"observation fingerprint mismatch: {key}",
        )
    require(
        manifest_fingerprint["baselib_manifest_sha256"] == scope_fingerprint["baselib_manifest_sha256"],
        "BaseLib manifest fingerprint mismatch",
    )

    manifest_attestation = manifest["beta_attestation"]
    profile_attestation = profile["beta_attestation"]
    require(manifest_attestation["status"] == profile_attestation["status"] == "verified", "beta proof is not verified")
    require(manifest_attestation["checked_at_utc"] == profile_attestation["checked_at_utc"], "beta proof timestamp mismatch")
    require(manifest_attestation["source"] == profile_attestation["source"], "beta proof source mismatch")
    require(
        manifest_attestation["steamcmd_output_sha256"] == profile_attestation["steamcmd_output_sha256"],
        "beta proof digest mismatch",
    )
    require(1 <= int(manifest_attestation["max_age_hours"]) <= 168, "beta proof age window is unsafe")

    active_scope_types = {item["model_type"] for item in scope["active_cards"]}
    absent_scope_types = {item["expected_model_type"] for item in scope["design_only_absent_cards"]}
    manifest_types = set(manifest["active_card_model_types"])
    require(manifest_types == active_scope_types, "observation cards differ from audited Beta scope")
    require(not manifest_types.intersection(absent_scope_types), "absent Beta cards entered the observation scope")
    require(len(manifest_types) == 10, "current observation scope must contain ten card types")

    required_bindings = set(contract["required_visual_events"]) | set(contract["required_title_surfaces"])
    manifest_bindings = set(manifest["required_binding_ids"])
    require(manifest_bindings == required_bindings, "observation bindings differ from production review surfaces")
    require(len(manifest_bindings) == 12, "observation binding scope must contain twelve surfaces")

    policy = manifest["policy"]
    require(policy.get("default_enabled") is False, "observation is not disabled by default")
    for key in ("read_only_only", "exact_fingerprint_required", "fail_closed_on_target_mismatch", "unpatch_on_reset"):
        require(policy.get(key) is True, f"observation safety policy missing {key}")
    require(policy.get("capture_argument_values") is False, "observation permits arbitrary argument values")
    require(policy.get("capture_absolute_paths") is False, "observation permits absolute paths")
    require(policy.get("opt_in_file_name") == "SasukeIronclad.observe.json", "unexpected opt-in marker name")
    require(policy.get("output_directory_name") == "observation-output", "unexpected output directory")
    require(100 <= int(policy["minimum_max_events"]) <= int(policy["default_max_events"]), "event floor/default invalid")
    require(int(policy["default_max_events"]) <= int(policy["maximum_max_events"]) <= 50000, "event ceiling invalid")
    require(0 <= int(policy["maximum_stack_frames"]) <= 16, "stack frame limit is unsafe")

    target_ids: set[str] = set()
    target_tokens: set[str] = set()
    covered_bindings: set[str] = set()
    manifest_targets: dict[str, dict] = {}
    for target in manifest["targets"]:
        target_id = str(target.get("id", ""))
        token = str(target.get("metadata_token", ""))
        require(target_id and target_id not in target_ids, f"duplicate observation target: {target_id}")
        require(METHOD_TOKEN_RE.fullmatch(token) is not None, f"target is not a MethodDef: {target_id}")
        require(token not in target_tokens, f"duplicate observation token: {token}")
        require(target.get("required") is True, f"target is not required: {target_id}")
        binding_ids = set(target.get("binding_ids", []))
        require(binding_ids and binding_ids.issubset(required_bindings), f"target has invalid binding IDs: {target_id}")
        target_ids.add(target_id)
        target_tokens.add(token)
        covered_bindings.update(binding_ids)
        manifest_targets[target_id] = target
    require(len(target_ids) >= 20, "observation target matrix is not broad enough")
    require(covered_bindings == required_bindings, "observation targets do not cover all review surfaces")

    require(binding_review.get("schema_version") == 1, "unsupported binding review schema")
    require(binding_review.get("status") == "partial_manual_review", "binding review must remain partial")
    require(binding_review.get("profile_id") == profile.get("id"), "binding review/profile mismatch")
    for key in ("steam_build_id", "sts2_sha256", "module_mvid", "baselib_version"):
        require(binding_review["runtime"][key] == manifest_fingerprint[key], f"binding review fingerprint mismatch: {key}")
    require(
        binding_review["source"]["runtime_review_sha256"]
        == "2fa4d6c86be2510fdf186dde9d6d799b757601d85c163d379658ef2b0f828c0a",
        "uploaded runtime review digest changed",
    )
    require(
        {item["sha256"] for item in binding_review["source"]["logs"]} == EXPECTED_RUNTIME_LOG_SHA256,
        "runtime log digest set changed",
    )
    require(binding_review["quality"]["event_counts"] == [8977, 9319], "reviewed event counts changed")
    require(binding_review["quality"]["balanced_call_pairs"] is True, "runtime calls are not balanced")
    require(binding_review["quality"]["errors"] == [], "runtime review contains errors")
    require(binding_review["policy"]["production_contract_remains_disabled"] is True, "review enabled production")
    require(binding_review["policy"]["runtime_bindings_remain_disabled"] is True, "review enabled bindings")

    decisions = {item["binding_id"]: item for item in binding_review["decisions"]}
    require(set(decisions) == required_bindings, "binding review does not cover twelve surfaces")
    for binding_id, target_id in EXPECTED_CANARY_BINDINGS.items():
        decision = decisions[binding_id]
        target = manifest_targets[target_id]
        require(decision["status"] == "approved_for_canary", f"{binding_id} is not canary-only")
        require(decision["selected_target_id"] == target_id, f"{binding_id} target drifted")
        require(binding_id in target["binding_ids"], f"{target_id} does not cover {binding_id}")
        for key in ("declaring_type", "method_signature", "metadata_token"):
            require(decision[key] == target[key], f"{binding_id} {key} drifted")
        require(decision["observed_in_all_sessions"] is True, f"{binding_id} lacks two-run evidence")
        require(
            len(decision["session_evidence"]) == 2
            and all(item["enter_count"] == item["return_count"] > 0 for item in decision["session_evidence"]),
            f"{binding_id} runtime evidence is invalid",
        )
    for binding_id in EXPECTED_TARGETED_BINDINGS:
        decision = decisions[binding_id]
        require(decision["status"] == "needs_targeted_observation", f"{binding_id} must remain blocked")
        require(not decision["selected_target_id"] and not decision["metadata_token"], f"{binding_id} was selected")

    require(binding_review["readiness"]["title_canary_candidate_count"] == 6, "title canary count changed")
    require(binding_review["readiness"]["visual_canary_candidate_count"] == 4, "visual canary count changed")
    require(binding_review["readiness"]["blocked_binding_count"] == 2, "blocked binding count changed")
    require(binding_review["readiness"]["production_profile_ready"] is False, "review claims production readiness")
    require(contract["status"] == "pending_local_audit" and contract["profiles"] == [], "production contract was enabled")
    require(profile["status"] == "pending_review", "pending profile was promoted")
    require(
        all(not item["metadata_token"] and item["status"] == "pending_review" for item in profile["visual_bindings"]),
        "pending visual profile was populated",
    )
    require(
        all(not item["metadata_token"] and item["status"] == "pending_review" for item in profile["title_bindings"]),
        "pending title profile was populated",
    )

    script_bytes = CONTROL_SCRIPT_PATH.read_bytes()
    require(all(byte < 128 for byte in script_bytes), "PowerShell 5.1 control script must remain ASCII-only")
    script_text = script_bytes.decode("ascii")
    main_text = MAIN_FILE_PATH.read_text(encoding="utf-8")
    bootstrap_text = BOOTSTRAP_PATH.read_text(encoding="utf-8")
    gate_text = GATE_PATH.read_text(encoding="utf-8")
    require("runtime-observation-status.json" in script_text, "control script does not expose startup status")
    require("Previous startup status was cleared" in script_text, "enable action can leave stale startup status")
    require("RuntimeObservationLocalFiles.WriteStatus" in main_text, "Mod initializer does not persist observation status")
    require("mod_initializer_reached" in bootstrap_text, "startup status lacks initializer evidence")
    require("Path.GetFileName(status.OutputPath)" in bootstrap_text, "startup status may expose an absolute output path")
    require("GeneratedRegex" not in gate_text, "source-generated regexes are unsafe in the game runtime")
    require("RegexOptions.Compiled" not in gate_text, "runtime observation regexes must not require dynamic compilation")

    print(
        f"RUNTIME_OBSERVATION_CONTRACT_OK targets={len(target_ids)} bindings={len(required_bindings)} "
        "cards=10 default_enabled=false read_only=true startup_status=true generated_regex=false "
        "manual_review=true approved_canary=10 blocked=2 production=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

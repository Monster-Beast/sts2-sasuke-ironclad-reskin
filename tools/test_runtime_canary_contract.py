#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REVIEW_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-runtime-binding-review.json"
CALIBRATION_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-anchor-calibration.json"
REPLACEMENT_REVIEW_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-replacement-canary-review.json"
STABILITY_REVIEW_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-multi-combat-stability-review.json"
RESOLUTION_UI_REVIEW_PATH = ROOT / "SasukeIronclad/data/reviews/public-beta-24251656-resolution-ui-review.json"
ANIMATION_MANIFEST_PATH = ROOT / "SasukeIronclad/data/card_animation_manifest.json"
MANIFEST_PATH = ROOT / "SasukeIronclad/data/runtime_observation_targets.json"
CONTRACT_PATH = ROOT / "SasukeIronclad/data/game_integration_contract.json"
PROFILE_PATH = ROOT / "SasukeIronclad/data/integration_profiles/public-beta-24251656-ee45848ff631.pending-review.json"
SCOPE_PATH = ROOT / "SasukeIronclad/data/current_beta_card_scope.json"
SCRIPT_PATH = ROOT / "tools/runtime-canary.ps1"
CHECKPOINT_SCRIPT_PATH = ROOT / "tools/runtime-canary-checkpoint.ps1"
JOURNAL_ANALYZER_PATH = ROOT / "tools/analyze_runtime_canary_journal.py"
JOURNAL_TEST_PATH = ROOT / "tools/test_runtime_canary_journal.py"
FAILURE_ANALYZER_PATH = ROOT / "tools/analyze_runtime_canary_failure.py"
FAILURE_TEST_PATH = ROOT / "tools/test_runtime_canary_failure.py"
STARTUP_FAILURE_ANALYZER_PATH = ROOT / "tools/analyze_runtime_canary_startup_failure.py"
STARTUP_FAILURE_TEST_PATH = ROOT / "tools/test_runtime_canary_startup_failure.py"
MAIN_PATH = ROOT / "SasukeIroncladCode/MainFile.cs"
LOCAL_FILES_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryLocalFiles.cs"
SESSION_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanarySession.cs"
JOURNAL_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryEventJournal.cs"
FAILURE_CONTROLLER_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryFailureInjection.cs"
STARTUP_FAILURE_CONTROLLER_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryStartupFailureInjection.cs"
FAILURE_DIAGNOSTICS_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryFailureDiagnostics.cs"
ANCHOR_RESOLVER_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimePlayerVisualAnchorResolver.cs"
SCENE_HOST_PATH = ROOT / "SasukeIroncladCode/Adapters/GodotVisualSceneHost.cs"
FAILURE_SOURCE_PATH = ROOT / "SasukeIroncladCode/Adapters/IRuntimeCanaryFailureSource.cs"
SELECTOR_PATH = ROOT / "SasukeIroncladCode/Runtime/CardAnimationSelector.cs"
GATE_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryGate.cs"
BOOTSTRAP_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryBootstrap.cs"
TARGET_RESOLVER_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeCanaryTargetResolver.cs"
REPLACEMENT_CONTROLLER_PATH = ROOT / "SasukeIroncladCode/Runtime/RuntimeOriginalVisualReplacementController.cs"
PLAYBACK_PATH = ROOT / "SasukeIroncladCode/Runtime/CardVisualPlaybackService.cs"
STATEFUL_DIRECTOR_PATH = ROOT / "SasukeIronclad/scripts/runtime/stateful_animation_director.gd"
RUNTIME_CONTRACT_PROJECT_PATH = ROOT / "tools/runtime_contract/SasukeIronclad.RuntimeContract.csproj"

EXPECTED_APPROVED = {
    "card_visual_request", "original_impact", "state_removed", "combat_ended",
    "card_art", "hand", "deck_list", "reward", "compendium", "tooltip",
}
EXPECTED_BLOCKED = {"form_removed", "character_state"}
EXPECTED_LOG_HASHES = {
    "c7d6f7dbe679ec57c70de9a36eb3f009565992ef4f7a253dfd882c01e4e6e24f",
    "97f411fa0761bbd9dc644227477c1c3ac19ea354eabb0547ea6471ec33ffbf18",
}
EXPECTED_STABILITY_ARCHIVE_HASH = "8b74fc4e9bf0abb6567e68e672a186909f6fe7c7ac5cd51eff47af1960d17026"
EXPECTED_RESOLUTION_UI_ARCHIVE_HASH = "060c87dba25f004dcc5b618f953ac61a7d2ed5cd1d8ebfeef78e9dce9ad5cf05"
REPLACEMENT_ACK = "public-beta-24251656-local-ironclad-replacement"
FAILURE_ACK = "public-beta-24251656-local-visual-failure-injection"
STARTUP_FAILURE_ACK = "public-beta-24251656-local-startup-method-signature-mismatch"


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> int:
    review = load(REVIEW_PATH)
    calibration = load(CALIBRATION_PATH)
    replacement_review = load(REPLACEMENT_REVIEW_PATH)
    stability_review = load(STABILITY_REVIEW_PATH)
    resolution_ui_review = load(RESOLUTION_UI_REVIEW_PATH)
    animation_manifest = load(ANIMATION_MANIFEST_PATH)
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
    require(review["quality"]["event_counts"] == [8977, 9319], "reviewed event counts changed")
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

    require(calibration["schema_version"] == 1 and calibration["status"] == "user_approved_single_setup", "anchor calibration approval changed")
    require(calibration["profile_id"] == review["profile_id"], "calibration profile mismatch")
    resolved_anchor = calibration["resolved_anchor"]
    require(resolved_anchor["strategy"] == "callback_local_player_reference", "reviewed anchor strategy changed")
    require(resolved_anchor["anchor_type"] == "MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals", "reviewed anchor type changed")
    require(resolved_anchor["anchor_name"] == "Ironclad", "reviewed anchor name changed")
    require(resolved_anchor["local_player_reference_count"] == 1, "reviewed local-player reference count changed")
    require((resolved_anchor["observed_global_x"], resolved_anchor["observed_global_y"]) == (480, 740), "reviewed anchor coordinates changed")
    observations = calibration["additional_observations"]
    require(set(observations["candidate_counts_seen"]) == {2, 3}, "replacement runs did not preserve observed candidate-count variation")
    require(observations["candidate_count_is_identity_invariant"] is False, "candidate count was promoted into an identity invariant")
    require(observations["stable_anchor_type"] == resolved_anchor["anchor_type"] and observations["stable_anchor_name"] == resolved_anchor["anchor_name"], "additional anchor identity changed")
    require(observations["stable_local_player_reference_count"] == 1, "additional local-player reference evidence changed")
    require(observations["replacement_activation_confirmed"] is True, "replacement activation evidence is missing")
    require(observations["unsupported_card_restoration_confirmed"] is True, "unsupported-card restoration evidence is missing")
    require(calibration["approved_calibration"]["anchor_scale"] == 1.2, "approved anchor scale changed")
    require(calibration["approved_calibration"]["anchor_offset_x"] == 0.0, "approved anchor X offset changed")
    require(calibration["approved_calibration"]["anchor_offset_y"] == -150.0, "approved anchor Y offset changed")
    require(calibration["safety"]["production_profile_promoted"] is False, "calibration promoted production bindings")

    require(replacement_review["schema_version"] == 1, "replacement review schema changed")
    require(replacement_review["status"] == "replacement_canary_and_multi_combat_stability_passed", "replacement review status changed")
    require(replacement_review["profile_id"] == review["profile_id"], "replacement review profile mismatch")
    activation = replacement_review["activation"]
    require(activation["passed"] is True and activation["active"] is True and activation["ever_hidden"] is True, "replacement activation was not recorded as passed")
    require(activation["last_transition"] == "original_visual_hidden_after_verified_overlay_playback", "replacement activation transition changed")
    require(activation["target_type"] == resolved_anchor["anchor_type"] and activation["target_name"] == resolved_anchor["anchor_name"], "replacement activation target changed")
    require(activation["original_visible_before_hide"] is True and activation["anchor_bound"] is True and activation["overlay_visible"] is True, "replacement activation safety evidence is incomplete")
    restoration = replacement_review["unsupported_card_restoration"]
    require(restoration["passed"] is True and restoration["active"] is False and restoration["ever_hidden"] is True, "unsupported-card restoration was not recorded as passed")
    require(restoration["restore_count"] == 1, "unsupported-card restoration count changed")
    require(restoration["last_transition"] == "original_visual_restored:card_not_in_reviewed_replacement_scope", "unsupported-card restoration transition changed")
    followup = replacement_review["observed_followup"]
    require(followup["reason"].endswith("original_impact_timeout"), "reviewed impact timeout evidence is missing")
    require(followup["original_visual_restored"] is True, "impact timeout did not restore the original visual")
    require(followup["local_retest_required"] is False, "completed impact-sync retest was left open")
    retest = followup["local_retest"]
    require(retest["archive_sha256"] == EXPECTED_STABILITY_ARCHIVE_HASH, "impact-sync retest archive changed")
    require(retest["same_process_multi_combat"] is True and retest["passed"] is True, "impact-sync retest did not pass")
    require(retest["original_impact_timeout_occurrences"] == 0 and retest["playback_fallback_occurrences"] == 0, "impact-sync regression reappeared")
    require(replacement_review["conclusions"]["impact_sync_regression_closed"] is True, "impact-sync regression was not closed")
    require(replacement_review["conclusions"]["same_process_multi_combat_stability_passed"] is True, "multi-combat replacement stability was not recorded")
    require(replacement_review["conclusions"]["production_profile_ready"] is False, "replacement evidence promoted production")

    require(stability_review["schema_version"] == 1, "stability review schema changed")
    require(stability_review["status"] == "multi_combat_stability_passed_with_capture_caveats", "stability review status changed")
    require(stability_review["profile_id"] == review["profile_id"], "stability review profile mismatch")
    require(stability_review["source"]["archive_sha256"] == EXPECTED_STABILITY_ARCHIVE_HASH, "stability archive digest changed")
    require(stability_review["source"]["archive_size_bytes"] == 40469, "stability archive size changed")
    require(stability_review["run"]["single_game_process"] is True and stability_review["run"]["checkpoint_count"] == 9, "stability run shape changed")
    checkpoints = stability_review["checkpoint_evidence"]
    require(len(checkpoints) == 9, "stability checkpoint inventory changed")
    require([item["restore_count"] for item in checkpoints] == [0, 1, 2, 3, 3, 3, 4, 4, 4], "replacement restore sequence changed")
    require({item.get("candidate_count") for item in checkpoints if item.get("candidate_count") is not None} == {2, 3}, "stability anchor candidate range changed")
    require(stability_review["archive_search"] == {
        "original_impact_timeout_occurrences": 0,
        "playback_fallback_occurrences": 0,
        "exception_occurrences": 0,
        "failed_occurrences": 0,
        "anchor_invalidated_occurrences": 0,
    }, "stability archive contains a reviewed failure marker")
    stability_conclusions = stability_review["conclusions"]
    for key in [
        "replacement_reactivated_after_combat_cleanup",
        "unsupported_card_restoration_passed",
        "subsequent_combat_reactivation_passed",
        "combat_end_cleanup_passed",
        "impact_sync_regression_closed",
        "same_process_multi_combat_stability_passed",
    ]:
        require(stability_conclusions[key] is True, f"stability conclusion is not passed: {key}")
    require(stability_conclusions["production_profile_ready"] is False, "stability review promoted production")
    require(len(stability_review["capture_caveats"]) >= 4, "checkpoint capture caveats were discarded")

    require(resolution_ui_review["schema_version"] == 1 and resolution_ui_review["status"] == "passed", "resolution/UI review is not passed")
    require(resolution_ui_review["profile_id"] == review["profile_id"], "resolution/UI review profile mismatch")
    resolution_attempts = resolution_ui_review["attempts"]
    require(len(resolution_attempts) == 3, "resolution/UI attempt history changed")
    require(resolution_attempts[0]["menu_cleanup_passed"] is False, "initial 4K menu cleanup regression was discarded")
    require(resolution_attempts[1]["passed"] is True and resolution_attempts[1]["window_width"] == 3840 and resolution_attempts[1]["window_height"] == 2160, "4K fullscreen retest evidence changed")
    latest_resolution_attempt = resolution_attempts[2]
    require(latest_resolution_attempt["passed"] is True and latest_resolution_attempt["display_mode"] == "windowed", "1080P windowed attempt is not passed")
    require((latest_resolution_attempt["window_width"], latest_resolution_attempt["window_height"]) == (1920, 1080), "1080P window dimensions changed")
    require([item["card_id"] for item in latest_resolution_attempt["cards_played"]] == ["Strike", "Defend"], "resolution/UI card sequence changed")
    require(latest_resolution_attempt["menu_cleanup_passed"] is True and latest_resolution_attempt["scene_exit_watchdog_log_count"] == 1, "1080P menu cleanup evidence changed")
    require(latest_resolution_attempt["archive"]["sha256"] == EXPECTED_RESOLUTION_UI_ARCHIVE_HASH, "1080P evidence archive digest changed")
    resolution_conclusions = resolution_ui_review["conclusions"]
    for key in [
        "4k_fullscreen_alignment_passed",
        "4k_fullscreen_title_display_passed",
        "4k_fullscreen_menu_cleanup_passed",
        "1080p_windowed_alignment_passed",
        "1080p_windowed_title_display_passed",
        "1080p_windowed_menu_cleanup_passed",
        "scene_exit_watchdog_verified",
        "resolution_ui_matrix_passed",
    ]:
        require(resolution_conclusions[key] is True, f"resolution/UI conclusion is not passed: {key}")
    require(resolution_conclusions["production_profile_ready"] is False, "resolution/UI evidence promoted production")
    require(resolution_ui_review["safety"]["affects_gameplay"] is False, "resolution/UI review changed gameplay scope")

    animations = animation_manifest["animations"]
    require(sum(1 for item in animations if item["is_damage_card"]) == 8, "damage-card animation inventory changed")
    require(sum(1 for item in animations if not item["is_damage_card"]) == 5, "non-damage animation inventory changed")
    require(all(
        item["hit_sync"] == ("original_hit_events" if item["is_damage_card"] else "timeline_local_events")
        for item in animations
    ), "damage and non-damage impact synchronization policies diverged")

    script_bytes = SCRIPT_PATH.read_bytes()
    checkpoint_bytes = CHECKPOINT_SCRIPT_PATH.read_bytes()
    require(all(byte < 128 for byte in script_bytes), "runtime-canary.ps1 must remain ASCII-only")
    require(all(byte < 128 for byte in checkpoint_bytes), "runtime-canary-checkpoint.ps1 must remain ASCII-only")
    script_text = script_bytes.decode("ascii")
    checkpoint_text = checkpoint_bytes.decode("ascii")
    main_text = MAIN_PATH.read_text(encoding="utf-8")
    local_text = LOCAL_FILES_PATH.read_text(encoding="utf-8")
    session_text = SESSION_PATH.read_text(encoding="utf-8")
    journal_text = JOURNAL_PATH.read_text(encoding="utf-8")
    failure_controller_text = FAILURE_CONTROLLER_PATH.read_text(encoding="utf-8")
    startup_failure_controller_text = STARTUP_FAILURE_CONTROLLER_PATH.read_text(encoding="utf-8")
    failure_diagnostics_text = FAILURE_DIAGNOSTICS_PATH.read_text(encoding="utf-8")
    resolver_text = ANCHOR_RESOLVER_PATH.read_text(encoding="utf-8")
    host_text = SCENE_HOST_PATH.read_text(encoding="utf-8")
    failure_source_text = FAILURE_SOURCE_PATH.read_text(encoding="utf-8")
    selector_text = SELECTOR_PATH.read_text(encoding="utf-8")
    gate_text = GATE_PATH.read_text(encoding="utf-8")
    bootstrap_text = BOOTSTRAP_PATH.read_text(encoding="utf-8")
    target_resolver_text = TARGET_RESOLVER_PATH.read_text(encoding="utf-8")
    replacement_text = REPLACEMENT_CONTROLLER_PATH.read_text(encoding="utf-8")
    playback_text = PLAYBACK_PATH.read_text(encoding="utf-8")
    stateful_director_text = STATEFUL_DIRECTOR_PATH.read_text(encoding="utf-8")
    runtime_contract_text = RUNTIME_CONTRACT_PROJECT_PATH.read_text(encoding="utf-8")

    require("local_visual_only" in script_text, "canary marker mode changed")
    require("session_label" in script_text and "SessionLabel" in script_text, "canary session label is missing")
    require("Combined" in script_text and '"combined"' in script_text, "explicit combined canary control is missing")
    require("canary-output" in script_text, "canary event journal discovery is missing")
    require("anchor_to_local_player" in script_text and "anchor_scale" in script_text, "local-player anchor marker fields are missing")
    require("[double]$AnchorScale = 1.2" in script_text and "[double]$AnchorOffsetY = -150.0" in script_text, "approved calibration is not the local default")
    require("ReplaceOriginal" in script_text and "hide_original_visual" in script_text, "replacement canary control is missing")
    require(REPLACEMENT_ACK in script_text, "replacement acknowledgement changed")
    require(FAILURE_ACK in script_text and FAILURE_ACK in gate_text, "failure injection acknowledgement changed")
    for scenario in ["missing_timeline", "forced_playback_failure", "anchor_invalidation"]:
        require(scenario in script_text and scenario in failure_controller_text, f"failure scenario is missing: {scenario}")
    require("FailureScenario" in script_text and "failure_injection_once" in script_text, "one-shot failure controls are missing")
    require("Failure injection requires anchored animation replacement" in gate_text, "failure injection can bypass replacement recovery")
    require("target must be a reviewed active card other than Demon Form" in gate_text, "failure target scope is not fail-closed")
    require("process_count" in checkpoint_text and '"[]"' in checkpoint_text, "checkpoint tool does not record a deterministic empty process list")
    require("event_file" in checkpoint_text and "checkpoint.json" in checkpoint_text, "checkpoint tool does not capture journal identity")
    require("runtime-canary-failure-status.json" in checkpoint_text, "checkpoint does not capture failure status")
    require("$markerSource" in checkpoint_text and "$MarkerFileName" in checkpoint_text, "checkpoint does not preserve the exact startup opt-in marker")
    require(JOURNAL_ANALYZER_PATH.exists() and JOURNAL_TEST_PATH.exists(), "journal analyzer or regression test is missing")
    require(FAILURE_ANALYZER_PATH.exists() and FAILURE_TEST_PATH.exists(), "failure analyzer or regression test is missing")
    require(STARTUP_FAILURE_ANALYZER_PATH.exists() and STARTUP_FAILURE_TEST_PATH.exists(), "startup failure analyzer or regression test is missing")
    require("RuntimeCanaryLocalFiles.LoadOptIn" in main_text and "RuntimeCanaryLocalFiles.WriteStatus" in main_text, "canary startup wiring is missing")
    require("events={CanaryStatus.EventFileName" in main_text, "Mod startup log does not report the journal file")
    require("SasukeIronclad.canary.json" in local_text and "runtime-canary-status.json" in local_text, "canary local filenames changed")
    require("EventFile = status.EventFileName" in local_text, "startup status does not publish the event file")
    require("runtime-canary-anchor-status.json" in local_text, "anchor diagnostics filename is missing")
    require("runtime-canary-replacement-status.json" in local_text, "replacement diagnostics filename is missing")
    require("runtime-canary-failure-status.json" in local_text, "failure diagnostics filename is missing")
    require("Runtime observation marker is present" in local_text, "simultaneous observation is not rejected")
    require("Demon Form" in session_text and "targeted run proves the exact form-removal event" in session_text, "Demon Form is not fail-closed")
    require("ReferenceEquals(sourceCardModel, _activeCardModel)" in session_text, "impact forwarding is not scoped to the active local card")
    require("RuntimePlayerVisualAnchorResolver.Resolve" in session_text, "animation canary does not resolve a local-player anchor")
    require("TryActivateReplacement" in session_text and "FailReplacementForCombat" in session_text, "replacement lifecycle is not wired")
    require("FallbackActivated += OnPlaybackFallback" in session_text, "playback fallback does not restore the original visual")
    require("AnchorInvalidated += OnAnchorInvalidated" in session_text, "anchor loss does not restore the original visual")
    require(
        'ReleaseCombatResourcesCore("combat_ended", "combat_resources_released")'
        in session_text,
        "combat end does not restore the original visual",
    )
    require("card_not_in_reviewed_replacement_scope" in session_text, "unreviewed cards do not restore original presentation")
    require("demon_form_replacement_remains_blocked" in session_text, "Demon Form does not restore original presentation")
    for event_name in [
        "session_start", "title_applied", "playback_started", "replacement_hidden",
        "original_impact_forwarded", "combat_ended", "session_stop",
    ]:
        require(f'"{event_name}"' in session_text, f"canary journal event is missing: {event_name}")
    require("RuntimeCanaryEventJournal.TryCreate" in session_text, "canary session does not create a journal")
    require("MaxEvents = 20_000" in journal_text and "DefaultIgnoreCondition" in journal_text, "journal is not bounded or compact")
    require("absolute paths" in journal_text and "game objects" in journal_text, "journal privacy boundary is undocumented")
    require("TryTrigger" in failure_controller_text and "TriggerCount: _triggered ? 1 : 0" in failure_controller_text, "failure controller is not one-shot")
    require("ConfirmRecovery" in failure_controller_text, "failure controller does not require recovery confirmation")
    require("WeakReference<GodotVisualSceneHost>" in failure_diagnostics_text, "failure diagnostics owns the Godot host strongly")
    require("TryTriggerMissingTimeline" in failure_diagnostics_text and "TryTakePostHideFailure" in failure_diagnostics_text, "failure diagnostics do not separate pre-hide and post-hide faults")
    require("ReferenceEquals(local, associated)" in resolver_text, "anchor resolution is not tied to the local Player object")
    require("Multiple similarly ranked local-player visual nodes" in resolver_text, "ambiguous anchors do not fail closed")
    require("MaxSceneNodes" in resolver_text and "MaxReferenceObjects" in resolver_text, "anchor traversal is not bounded")
    require("Visible = false" in host_text and "BindToAnchor" in host_text, "visual host does not stay hidden before anchoring")
    require("AnchorInvalidated" in host_text and "GetGlobalTransformWithCanvas" in host_text, "visual host does not report anchor loss")
    require("bind_runtime_anchor" in host_text and "RuntimeAnchorExitedSinceBind" in host_text, "visual host does not bridge the Godot-side scene-exit watchdog")
    require("tree_exiting.connect" in stateful_director_text and "sasuke_runtime_anchor_exited" in stateful_director_text, "Godot-side anchor exit watchdog is missing")
    require("runtime_root.visible = false" in stateful_director_text and "host.visible = false" in stateful_director_text, "scene-exit watchdog does not hide the runtime overlay")
    stale_exit_index = session_text.index("RuntimeAnchorExitedSinceBind")
    combat_start_index = session_text.index("EnsureCombatStarted(cardId)")
    require(stale_exit_index < combat_start_index, "stale scene-exit state is not cleared before the next local card starts combat")
    require("RuntimeCanaryFailureDiagnostics.TryTriggerMissingTimeline" in host_text, "missing timeline fault is not injected in memory")
    require("RuntimeCanaryFailureDiagnostics.TryTakePostHideFailure" in host_text, "post-hide failure is not deferred until replacement activates")
    require("File.Delete" not in host_text and "File.Move" not in host_text, "failure injection modifies files from the Godot host")
    require("ConsumeCanPlayFailureReason" in failure_source_text and "IRuntimeCanaryFailureSource" in playback_text, "missing-resource reason does not reach normal fallback")
    require('["external_impact_sync"] = selection.RequiresOriginalImpactSync' in host_text, "scene host does not use the selected impact-sync policy")
    require('["external_impact_sync"] = true' not in host_text, "scene host still forces external impact sync for every card")
    require("RequiresOriginalImpactSync = spec.IsDamageCard && string.Equals" in selector_text, "non-damage timelines can still wait for original damage impacts")
    require(REPLACEMENT_ACK in gate_text and "original visibility value must be captured" in gate_text.lower(), "replacement gate policy is incomplete")
    require(STARTUP_FAILURE_ACK in script_text and STARTUP_FAILURE_ACK in gate_text, "startup failure acknowledgement changed")
    require("local_startup_failure_only" in script_text and "local_startup_failure_only" in gate_text, "startup failure mode is not distinct from the visual canary")
    require("method_signature_mismatch" in script_text and "method_signature_mismatch" in startup_failure_controller_text, "startup signature mismatch scenario is missing")
    require("Startup failure injection cannot be combined with presentation failure injection" in gate_text, "startup and presentation failures are not mutually exclusive")
    require("Startup failure injection requires animations-only resolution" in gate_text, "startup failure mode is not restricted to animations-only resolution")
    require("File.Write" not in startup_failure_controller_text and "File.Move" not in startup_failure_controller_text, "startup failure injection may modify files")
    baseline_index = target_resolver_text.index("string actualSignature = AuditedMethodBindingResolver.FormatMethodSignature(method);")
    injection_index = target_resolver_text.index("TryInjectMethodSignatureMismatch")
    resolve_index = bootstrap_text.index("RuntimeCanaryTargetResolver.Resolve(")
    startup_return_index = bootstrap_text.index("if (startupFailureSnapshot.Requested)")
    session_index = bootstrap_text.index("session = new RuntimeCanarySession")
    install_index = bootstrap_text.index("patcher.Install(session, resolution.Targets)")
    require(baseline_index < injection_index, "startup mismatch is injected before the real reflected signature is established")
    require(resolve_index < startup_return_index < session_index < install_index, "startup failure can reach session creation or patch installation")
    require("PatchInstallAttempted = status.PatchInstallAttempted" in local_text, "startup status does not expose patch-install attempts")
    require("StartupFailureInjectionBaselineMatchConfirmed" in local_text, "startup status does not preserve baseline-match evidence")
    require("IsValidSessionLabel" in gate_text, "manually edited canary session labels are not validated")
    require("RequiredTargetType = \"MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals\"" in replacement_text, "replacement target type is not exact")
    require("RequiredTargetName = \"Ironclad\"" in replacement_text, "replacement target name is not exact")
    require("target.Visible = false" in replacement_text and "target.Visible = _originalVisibleBeforeHide.Value" in replacement_text, "replacement visibility is not captured and restored")
    require("already hidden by the game or another Mod" in replacement_text, "replacement does not reject an externally hidden target")
    require("NotifyOriginalVisualHidden" in replacement_text and "NotifyReplacementRestored" in replacement_text, "failure diagnostics do not observe hide and restore transitions")
    require("FallbackActivated?.Invoke" in playback_text, "playback fallback notification is missing")
    require("SasukeIroncladCode/MainFile.cs" in runtime_contract_text, "runtime contract does not compile the Mod initializer")
    require("Sts2ModdingStubs.cs" in runtime_contract_text, "runtime contract does not compile the STS2 Mod stubs")

    print(
        "RUNTIME_CANARY_CONTRACT_OK sessions=2 events=18296 approved=10 blocked=2 "
        "explicit_opt_in=true anchor=true candidate_counts=2,3 calibration=1.2,0,-150 "
        "replacement=true restoration=true unreviewed_fallback=true damage_only_impact_sync=true "
        "impact_retest_passed=true multi_combat_stability=true capture_caveats=true "
        "combined_journal=true deterministic_checkpoint=true failure_injection=true "
        "failure_scenarios=3 startup_fail_closed=true scene_exit_watchdog=true "
        "in_memory_only=true production=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

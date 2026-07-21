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
CONTROL_SCRIPT_PATH = ROOT / "tools/runtime-observation.ps1"
METHOD_TOKEN_RE = re.compile(r"^0x06[0-9A-Fa-f]{6}$")


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
    require(len(target_ids) >= 20, "observation target matrix is not broad enough")
    require(covered_bindings == required_bindings, "observation targets do not cover all review surfaces")

    script_bytes = CONTROL_SCRIPT_PATH.read_bytes()
    require(all(byte < 128 for byte in script_bytes), "PowerShell 5.1 control script must remain ASCII-only")

    print(
        f"RUNTIME_OBSERVATION_CONTRACT_OK targets={len(target_ids)} bindings={len(required_bindings)} "
        "cards=10 default_enabled=false read_only=true"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

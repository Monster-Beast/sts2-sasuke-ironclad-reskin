#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

REQUIRED_EVIDENCE = {
    "observed_run_1",
    "observed_run_2",
    "fallback_verified",
    "local_visual_only_verified",
}
METHOD_TOKEN_RE = re.compile(r"^0x06[0-9a-fA-F]{6}$")


def load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def source_digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def selected_candidate(binding: dict[str, Any]) -> dict[str, Any]:
    review = binding.get("review", {})
    if review.get("status") != "approved":
        raise SystemExit(f"binding {binding.get('id')} is not approved")
    reviewer = str(review.get("reviewer", "")).strip()
    if not reviewer:
        raise SystemExit(f"binding {binding.get('id')} is missing a reviewer")
    evidence = {str(item).strip() for item in review.get("evidence", []) if str(item).strip()}
    missing = sorted(REQUIRED_EVIDENCE - evidence)
    if missing:
        raise SystemExit(f"binding {binding.get('id')} is missing evidence: {', '.join(missing)}")
    selected_id = str(review.get("selected_candidate_id", "")).strip()
    candidates = {candidate["candidate_id"]: candidate for candidate in binding.get("candidates", [])}
    if not selected_id or selected_id not in candidates:
        raise SystemExit(f"binding {binding.get('id')} does not select a listed candidate")
    candidate = candidates[selected_id]
    if str(candidate.get("kind", "")).strip().lower() != "method":
        raise SystemExit(f"binding {binding.get('id')} selected a non-method metadata target")
    metadata_token = str(candidate.get("metadata_token", "")).strip()
    if not METHOD_TOKEN_RE.fullmatch(metadata_token):
        raise SystemExit(f"binding {binding.get('id')} selected a non-MethodDef metadata token")
    return candidate


def build_profile(
    review: dict[str, Any],
    source_path: Path,
    profile_id: str | None,
) -> dict[str, Any]:
    if review.get("schema_version") != 1:
        raise SystemExit("unsupported binding review schema")
    if review.get("policy", {}).get("auto_selection_forbidden") is not True:
        raise SystemExit("review did not preserve the no-auto-selection policy")
    fingerprint = review.get("fingerprint", {})
    required_fingerprint = [
        "branch",
        "steam_build_id",
        "sts2_sha256",
        "module_mvid",
        "baselib_version",
    ]
    missing = [key for key in required_fingerprint if not str(fingerprint.get(key, "")).strip()]
    if missing:
        raise SystemExit("review fingerprint is incomplete: " + ", ".join(missing))

    visual_bindings: list[dict[str, Any]] = []
    title_bindings: list[dict[str, Any]] = []
    evidence_by_binding: dict[str, Any] = {}
    for binding in review.get("bindings", []):
        binding_id = str(binding.get("id", "")).strip()
        candidate = selected_candidate(binding)
        review_data = binding["review"]
        entry = {
            "declaring_type": candidate["declaring_type"],
            "method_signature": candidate["signature"],
            "metadata_token": candidate["metadata_token"],
            "status": "pending_review",
            "fallback": "original_visual" if binding.get("kind") == "visual" else "original_title",
        }
        if binding.get("kind") == "visual":
            visual_bindings.append({"id": binding_id, **entry})
        elif binding.get("kind") == "title":
            title_bindings.append({"surface_id": binding_id, **entry})
        else:
            raise SystemExit(f"binding {binding_id} has unsupported kind {binding.get('kind')}")
        evidence_by_binding[binding_id] = {
            "reviewer": review_data["reviewer"],
            "evidence": sorted({str(item) for item in review_data.get("evidence", [])}),
            "notes": str(review_data.get("notes", "")),
            "candidate_id": candidate["candidate_id"],
        }

    derived_id = profile_id or (
        f"{fingerprint['branch']}-{fingerprint['steam_build_id']}-"
        f"{str(fingerprint['sts2_sha256'])[:12]}"
    ).lower()
    return {
        "schema_version": 1,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": "pending_review",
        "source_review_sha256": source_digest(source_path),
        "profile": {
            "id": derived_id,
            "status": "pending_review",
            "branch": fingerprint["branch"],
            "fingerprint": {
                "steam_build_id": fingerprint["steam_build_id"],
                "sts2_sha256": fingerprint["sts2_sha256"],
                "module_mvid": fingerprint["module_mvid"],
                "baselib_version": fingerprint["baselib_version"],
            },
            "visual_bindings": visual_bindings,
            "title_bindings": title_bindings,
        },
        "review_evidence": evidence_by_binding,
        "promotion_requirements": [
            "real_game_build_succeeds",
            "visual_fallback_regression_passes",
            "title_fallback_regression_passes",
            "single_player_regression_passes",
            "multiplayer_local_only_regression_passes",
            "manual_status_change_to_verified",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Compile a manually approved review into a still-disabled pending profile."
    )
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--profile-id")
    args = parser.parse_args()
    review = load(args.review)
    result = build_profile(review, args.review, args.profile_id)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        f"REVIEWED_PROFILE_OK profile={result['profile']['id']} "
        f"visuals={len(result['profile']['visual_bindings'])} "
        f"titles={len(result['profile']['title_bindings'])} status=pending_review"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

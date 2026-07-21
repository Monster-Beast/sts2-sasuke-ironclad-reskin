#!/usr/bin/env python3
from __future__ import annotations

import copy
import importlib.util
import json
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


review_module = load_module("build_audit_review", ROOT / "tools/build_audit_review.py")
compile_module = load_module("compile_reviewed_profile", ROOT / "tools/compile_reviewed_profile.py")


def symbol(kind: str, name: str, declaring: str, token: str, categories: list[str], constant: str = "") -> dict:
    return {
        "kind": kind,
        "name": name,
        "declaringType": declaring,
        "signature": f"void {declaring}.{name}()" if kind == "method" else f"string {declaring}.{name}",
        "metadataToken": token,
        "visibility": "Public",
        "score": 50,
        "categories": categories,
        "constantValue": constant,
    }


def approve(review: dict) -> None:
    for binding in review["bindings"]:
        binding["review"] = {
            "status": "approved",
            "selected_candidate_id": binding["candidates"][0]["candidate_id"],
            "reviewer": "fixture-reviewer",
            "evidence": sorted(compile_module.REQUIRED_EVIDENCE),
            "notes": "fixture",
        }


def main() -> int:
    visual_ids = [
        "card_visual_request", "original_impact", "state_removed",
        "form_removed", "combat_ended", "character_state"
    ]
    title_ids = ["card_art", "hand", "deck_list", "reward", "compendium", "tooltip"]
    names = [
        ("CardVisualRequest", ["cards", "visuals"]),
        ("OriginalImpact", ["visuals"]),
        ("StateRemoved", ["lifecycle"]),
        ("FormRemoved", ["lifecycle", "visuals"]),
        ("CombatEnded", ["lifecycle"]),
        ("CharacterStateChanged", ["visuals", "lifecycle"]),
        ("RefreshCardTitle", ["titles", "cards"]),
        ("RefreshHandTitle", ["titles", "cards"]),
        ("RefreshDeckListTitle", ["titles", "cards"]),
        ("RefreshRewardTitle", ["titles", "cards"]),
        ("RefreshCompendiumTitle", ["titles", "cards"]),
        ("RefreshTooltipTitle", ["titles", "cards"]),
    ]
    symbols = [
        symbol("method", name, "Fixture.GameVisuals", f"0x{0x06000001 + index:08X}", categories)
        for index, (name, categories) in enumerate(names)
    ]
    symbols.extend(
        [
            symbol("field", "StrikeCardId", "Fixture.IroncladCards", "0x04000001", ["cards"], "STRIKE"),
            symbol("property", "CardTitle", "Fixture.CardView", "0x17000001", ["titles", "cards"]),
            symbol("event", "OriginalImpact", "Fixture.GameEvents", "0x14000001", ["visuals"]),
        ]
    )

    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        report_path = root / "audit-report.json"
        comparison_path = root / "audit-comparison.json"
        contract_path = root / "contract.json"
        attestation_path = root / "latest-beta-attestation.json"
        review_path = root / "binding-review.json"
        report = {
            "schemaVersion": 1,
            "session": "run-1",
            "branch": "public-beta",
            "game": {
                "steamBuildId": "123456",
                "sts2AssemblySha256": "a" * 64,
                "moduleVersionId": "11111111-2222-3333-4444-555555555555",
                "baseLibVersion": "v3.1.8",
            },
            "symbols": symbols,
            "assets": [],
        }
        comparison = {
            "schemaVersion": 1,
            "equivalent": True,
            "firstSession": "run-1",
            "secondSession": "run-2",
        }
        contract = {
            "required_visual_events": visual_ids,
            "required_title_surfaces": title_ids,
        }
        attestation = {
            "schema_version": 1,
            "app_id": "2868840",
            "required_branch": "public-beta",
            "installed_branch": "public-beta",
            "installed_build_id": "123456",
            "remote_build_id": "123456",
            "is_latest": True,
            "status": "verified",
            "checked_at_utc": "2026-07-21T00:00:00+00:00",
            "source": "steamcmd_app_info_print",
            "steamcmd_output_sha256": "b" * 64,
        }
        report_path.write_text(json.dumps(report), encoding="utf-8")
        comparison_path.write_text(json.dumps(comparison), encoding="utf-8")
        contract_path.write_text(json.dumps(contract), encoding="utf-8")
        attestation_path.write_text(json.dumps(attestation), encoding="utf-8")

        review = review_module.build_review(
            report_path,
            comparison_path,
            contract_path,
            ROOT / "tools/game_audit/binding-review-rules.json",
            attestation_path,
        )
        assert len(review["bindings"]) == 12
        assert len(review["card_id_candidates"]) == 2
        assert any(item["constant_value"] == "STRIKE" for item in review["card_id_candidates"])
        assert all(binding["candidates"] for binding in review["bindings"])
        assert all(candidate["kind"] == "method" for binding in review["bindings"] for candidate in binding["candidates"])
        assert all(binding["review"]["selected_candidate_id"] == "" for binding in review["bindings"])
        assert review["policy"]["auto_selection_forbidden"] is True
        assert review["policy"]["latest_public_beta_required"] is True
        assert review["latest_beta"]["remote_build_id"] == "123456"
        assert review["fingerprint"]["branch"] == "public-beta"

        approve(review)
        review_path.write_text(json.dumps(review), encoding="utf-8")
        compiled = compile_module.build_profile(review, review_path, "public-beta-fixture")
        assert compiled["status"] == "pending_review"
        assert compiled["profile"]["status"] == "pending_review"
        assert len(compiled["profile"]["visual_bindings"]) == 6
        assert len(compiled["profile"]["title_bindings"]) == 6
        assert all(item["status"] == "pending_review" for item in compiled["profile"]["visual_bindings"])
        assert all(item["status"] == "pending_review" for item in compiled["profile"]["title_bindings"])
        assert all(item["metadata_token"].startswith("0x06") for item in compiled["profile"]["visual_bindings"])
        assert all(item["metadata_token"].startswith("0x06") for item in compiled["profile"]["title_bindings"])

        missing_evidence = copy.deepcopy(review)
        missing_evidence["bindings"][0]["review"]["evidence"] = []
        try:
            compile_module.build_profile(missing_evidence, review_path, "invalid")
        except SystemExit as exc:
            assert "missing evidence" in str(exc)
        else:
            raise AssertionError("missing evidence was accepted")

        non_method = copy.deepcopy(review)
        malicious = symbol("property", "CardTitle", "Fixture.CardView", "0x17000001", ["titles", "cards"])
        malicious_candidate = {
            "candidate_id": "malicious-property",
            "review_score": 999,
            "kind": malicious["kind"],
            "name": malicious["name"],
            "declaring_type": malicious["declaringType"],
            "signature": malicious["signature"],
            "metadata_token": malicious["metadataToken"],
            "visibility": malicious["visibility"],
            "constant_value": "",
            "audit_score": malicious["score"],
            "categories": malicious["categories"],
        }
        non_method["bindings"][0]["candidates"].append(malicious_candidate)
        non_method["bindings"][0]["review"]["selected_candidate_id"] = "malicious-property"
        try:
            compile_module.build_profile(non_method, review_path, "invalid")
        except SystemExit as exc:
            assert "non-method" in str(exc)
        else:
            raise AssertionError("non-method metadata target was accepted")

        stale_attestation = dict(attestation)
        stale_attestation["remote_build_id"] = "123457"
        attestation_path.write_text(json.dumps(stale_attestation), encoding="utf-8")
        try:
            review_module.build_review(
                report_path,
                comparison_path,
                contract_path,
                ROOT / "tools/game_audit/binding-review-rules.json",
                attestation_path,
            )
        except SystemExit as exc:
            assert "does not match" in str(exc)
        else:
            raise AssertionError("stale beta build was accepted")

    print("AUDIT_REVIEW_PIPELINE_OK bindings=12 card_ids=2 methoddef_only=true latest_beta=true status=pending_review")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

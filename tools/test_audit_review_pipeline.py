#!/usr/bin/env python3
from __future__ import annotations

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
    symbols.append(symbol("field", "StrikeCardId", "Fixture.IroncladCards", "0x04000001", ["cards"], "STRIKE"))

    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        report_path = root / "audit-report.json"
        comparison_path = root / "audit-comparison.json"
        contract_path = root / "contract.json"
        review_path = root / "binding-review.json"
        report = {
            "schemaVersion": 1,
            "session": "run-1",
            "branch": "stable",
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
        report_path.write_text(json.dumps(report), encoding="utf-8")
        comparison_path.write_text(json.dumps(comparison), encoding="utf-8")
        contract_path.write_text(json.dumps(contract), encoding="utf-8")

        review = review_module.build_review(
            report_path,
            comparison_path,
            contract_path,
            ROOT / "tools/game_audit/binding-review-rules.json",
        )
        assert len(review["bindings"]) == 12
        assert len(review["card_id_candidates"]) == 1
        assert review["card_id_candidates"][0]["constant_value"] == "STRIKE"
        assert all(binding["candidates"] for binding in review["bindings"])
        assert all(binding["review"]["selected_candidate_id"] == "" for binding in review["bindings"])
        assert review["policy"]["auto_selection_forbidden"] is True

        for binding in review["bindings"]:
            binding["review"] = {
                "status": "approved",
                "selected_candidate_id": binding["candidates"][0]["candidate_id"],
                "reviewer": "fixture-reviewer",
                "evidence": sorted(compile_module.REQUIRED_EVIDENCE),
                "notes": "fixture",
            }
        review_path.write_text(json.dumps(review), encoding="utf-8")
        compiled = compile_module.build_profile(review, review_path, "stable-fixture")
        assert compiled["status"] == "pending_review"
        assert compiled["profile"]["status"] == "pending_review"
        assert len(compiled["profile"]["visual_bindings"]) == 6
        assert len(compiled["profile"]["title_bindings"]) == 6
        assert all(item["status"] == "pending_review" for item in compiled["profile"]["visual_bindings"])
        assert all(item["status"] == "pending_review" for item in compiled["profile"]["title_bindings"])

        review["bindings"][0]["review"]["evidence"] = []
        try:
            compile_module.build_profile(review, review_path, "invalid")
        except SystemExit as exc:
            assert "missing evidence" in str(exc)
        else:
            raise AssertionError("missing evidence was accepted")

    print("AUDIT_REVIEW_PIPELINE_OK bindings=12 card_ids=1 status=pending_review")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

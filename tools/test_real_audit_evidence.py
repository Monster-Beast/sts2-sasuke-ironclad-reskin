#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "tools/validate_real_audit_evidence.py"

ACTIVE_CARDS = {
    "Strike": "StrikeIronclad",
    "Defend": "DefendIronclad",
    "Bash": "Bash",
    "Anger": "Anger",
    "Thunderclap": "Thunderclap",
    "Flame Barrier": "FlameBarrier",
    "Whirlwind": "Whirlwind",
    "Burning Pact": "BurningPact",
    "Demon Form": "DemonForm",
    "Fiend Fire": "FiendFire",
}
DESIGN_ONLY = {
    "Cleave": "Cleave",
    "Heavy Blade": "HeavyBlade",
    "Limit Break": "LimitBreak",
}


def load_module():
    spec = importlib.util.spec_from_file_location("validate_real_audit_evidence", MODULE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def write(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def report(session: str, *, baselib: bool, complete_cards: bool) -> dict:
    selected = ACTIVE_CARDS.values() if complete_cards else ["StrikeIronclad", "DefendIronclad"]
    symbols = [
        {
            "kind": "type",
            "name": f"MegaCrit.Sts2.Core.Models.Cards.{name}",
            "declaringType": f"MegaCrit.Sts2.Core.Models.Cards.{name}",
            "signature": f"MegaCrit.Sts2.Core.Models.Cards.{name} : MegaCrit.Sts2.Core.Models.CardModel",
            "metadataToken": f"0x0200{index:04X}",
            "visibility": "Public",
            "constantValue": None,
            "score": 100000,
            "categories": ["target_cards", "cards"],
        }
        for index, name in enumerate(selected, start=1)
    ]
    return {
        "schemaVersion": 1,
        "session": session,
        "branch": "public-beta",
        "game": {
            "steamBuildId": "24251656",
            "sts2AssemblySha256": "a" * 64,
            "moduleVersionId": "a49d3537-5a42-4dcd-9877-663e394f2b44",
            "baseLibVersion": "3.3.0" if baselib else None,
            "baseLibManifestSha256": "b" * 64 if baselib else None,
        },
        "symbols": symbols,
        "assets": [],
    }


def scope(*, baselib: bool = True) -> dict:
    return {
        "schema_version": 1,
        "status": "pending_review",
        "gameplay_changes": False,
        "branch": "public-beta",
        "steam_build_id": "24251656",
        "fingerprint": {
            "sts2_sha256": "a" * 64,
            "module_mvid": "a49d3537-5a42-4dcd-9877-663e394f2b44",
            "baselib_version": "3.3.0" if baselib else "",
            "baselib_manifest_sha256": "b" * 64 if baselib else "",
        },
        "active_cards": [
            {
                "card_id": card_id,
                "model_type": f"MegaCrit.Sts2.Core.Models.Cards.{name}",
            }
            for card_id, name in ACTIVE_CARDS.items()
        ],
        "design_only_absent_cards": [
            {
                "card_id": card_id,
                "expected_model_type": f"MegaCrit.Sts2.Core.Models.Cards.{name}",
                "reason": "self-test",
            }
            for card_id, name in DESIGN_ONLY.items()
        ],
    }


def review(report_path: Path, comparison_path: Path, attestation_path: Path) -> dict:
    binding_ids = [
        "card_visual_request",
        "original_impact",
        "state_removed",
        "form_removed",
        "combat_ended",
        "character_state",
        "card_art",
        "hand",
        "deck_list",
        "reward",
        "compendium",
        "tooltip",
    ]
    return {
        "schema_version": 1,
        "source": {
            "report_sha256": sha(report_path),
            "comparison_sha256": sha(comparison_path),
            "latest_beta_attestation_sha256": sha(attestation_path),
        },
        "bindings": [
            {
                "id": binding_id,
                "candidates": [{"kind": "method", "metadata_token": "0x06000001"}],
                "review": {"status": "unreviewed", "selected_candidate_id": ""},
            }
            for binding_id in binding_ids
        ],
        "card_id_candidates": [],
    }


def main() -> int:
    module = load_module()
    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        run1 = root / "run1.json"
        run2 = root / "run2.json"
        comparison = root / "comparison.json"
        attestation = root / "attestation.json"
        review_path = root / "review.json"
        scope_path = root / "scope.json"

        write(run1, report("run-1", baselib=False, complete_cards=False))
        write(run2, report("run-2", baselib=False, complete_cards=False))
        write(
            comparison,
            {
                "schemaVersion": 1,
                "equivalent": True,
                "sameAssembly": True,
                "sameSteamBuild": True,
                "sameBaseLibVersion": True,
                "sameSymbols": True,
                "sameAssets": True,
            },
        )
        write(
            attestation,
            {
                "schema_version": 1,
                "status": "verified",
                "is_latest": True,
                "installed_build_id": "24251656",
                "remote_build_id": "24251656",
                "checked_at_utc": "2026-07-21T07:00:09+00:00",
            },
        )
        write(review_path, review(run1, comparison, attestation))
        write(scope_path, scope(baselib=False))

        blocked = module.analyze(run1, run2, comparison, attestation, review_path, scope_path)
        blocker_ids = {item["id"] for item in blocked["blockers"]}
        assert blocked["status"] == "blocked"
        assert "baselib_missing" in blocker_ids
        assert "target_card_coverage_incomplete" in blocker_ids
        assert blocked["target_card_coverage"]["found_count"] == 2

        write(run1, report("run-1", baselib=True, complete_cards=True))
        write(run2, report("run-2", baselib=True, complete_cards=True))
        write(review_path, review(run1, comparison, attestation))
        write(scope_path, scope(baselib=True))
        pending = module.analyze(run1, run2, comparison, attestation, review_path, scope_path)
        assert pending["status"] == "pending_review"
        assert pending["can_create_pending_review_profile"] is True
        assert pending["can_create_runtime_profile"] is False
        assert pending["target_card_coverage"]["found_count"] == 10
        assert pending["target_card_coverage"]["required_count"] == 10
        assert len(pending["target_card_coverage"]["design_only_absent_cards"]) == 3

    print("REAL_AUDIT_EVIDENCE_OK blocked=true pending_review=true active_cards=10 design_only=3")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

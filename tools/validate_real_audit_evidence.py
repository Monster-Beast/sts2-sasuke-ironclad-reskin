#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
METHOD_TOKEN_RE = re.compile(r"^0x06[0-9a-fA-F]{6}$")

TARGET_CARDS: dict[str, tuple[str, ...]] = {
    "strike": ("StrikeIronclad",),
    "defend": ("DefendIronclad",),
    "bash": ("Bash", "BashIronclad"),
    "anger": ("Anger", "AngerIronclad"),
    "cleave": ("Cleave", "CleaveIronclad"),
    "thunderclap": ("Thunderclap", "ThunderClap"),
    "heavy_blade": ("HeavyBlade", "HeavyBladeIronclad"),
    "flame_barrier": ("FlameBarrier", "FlameBarrierIronclad"),
    "whirlwind": ("Whirlwind", "WhirlwindIronclad"),
    "burning_pact": ("BurningPact", "BurningPactIronclad"),
    "demon_form": ("DemonForm", "DemonFormIronclad"),
    "limit_break": ("LimitBreak", "LimitBreakIronclad"),
    "fiend_fire": ("FiendFire", "FiendFireIronclad"),
}

REQUIRED_BINDINGS = {
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
}


def load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized(value: object) -> str:
    return str(value or "").strip()


def target_card_coverage(report: dict[str, Any]) -> dict[str, Any]:
    searchable_types: set[str] = set()
    for symbol in report.get("symbols", []):
        kind = normalized(symbol.get("kind")).lower()
        declaring = normalized(symbol.get("declaringType"))
        name = normalized(symbol.get("name"))
        if "MegaCrit.Sts2.Core.Models.Cards." not in declaring:
            continue
        if kind == "type":
            searchable_types.add(declaring)
        else:
            searchable_types.add(declaring.split("+", 1)[0])
        if name.startswith("MegaCrit.Sts2.Core.Models.Cards."):
            searchable_types.add(name.split("+", 1)[0])

    found: dict[str, list[str]] = {}
    missing: list[str] = []
    for card_id, aliases in TARGET_CARDS.items():
        matches = sorted(
            type_name
            for type_name in searchable_types
            if any(type_name.rsplit(".", 1)[-1].casefold() == alias.casefold() for alias in aliases)
        )
        if matches:
            found[card_id] = matches
        else:
            missing.append(card_id)

    return {
        "required_count": len(TARGET_CARDS),
        "found_count": len(found),
        "complete": not missing,
        "found": found,
        "missing": missing,
    }


def validate_hash_linkage(
    report_path: Path,
    comparison_path: Path,
    attestation_path: Path,
    review: dict[str, Any],
) -> list[dict[str, str]]:
    blockers: list[dict[str, str]] = []
    source = review.get("source", {})
    expected = {
        "report_sha256": digest(report_path),
        "comparison_sha256": digest(comparison_path),
        "latest_beta_attestation_sha256": digest(attestation_path),
    }
    for key, actual in expected.items():
        recorded = normalized(source.get(key)).lower()
        if recorded != actual:
            blockers.append(
                {
                    "id": f"{key}_mismatch",
                    "detail": f"review {key} does not match the supplied file",
                }
            )
    return blockers


def analyze(
    report_path: Path,
    second_report_path: Path,
    comparison_path: Path,
    attestation_path: Path,
    review_path: Path,
) -> dict[str, Any]:
    report = load(report_path)
    second = load(second_report_path)
    comparison = load(comparison_path)
    attestation = load(attestation_path)
    review = load(review_path)

    blockers: list[dict[str, str]] = []
    warnings: list[dict[str, str]] = []

    if report.get("schemaVersion") != 1 or second.get("schemaVersion") != 1:
        blockers.append({"id": "report_schema", "detail": "unsupported audit report schema"})
    if comparison.get("schemaVersion") != 1 or comparison.get("equivalent") is not True:
        blockers.append({"id": "comparison_not_equivalent", "detail": "two equivalent audit runs are required"})
    if review.get("schema_version") != 1:
        blockers.append({"id": "review_schema", "detail": "unsupported binding review schema"})

    blockers.extend(validate_hash_linkage(report_path, comparison_path, attestation_path, review))

    if attestation.get("schema_version") != 1 or attestation.get("status") != "verified":
        blockers.append({"id": "beta_attestation", "detail": "latest public-beta attestation is not verified"})
    if attestation.get("is_latest") is not True:
        blockers.append({"id": "beta_not_latest", "detail": "installed build is not the remotely attested latest public-beta"})

    first_game = report.get("game", {})
    second_game = second.get("game", {})
    fingerprint_fields = {
        "branch": (normalized(report.get("branch")), normalized(second.get("branch"))),
        "steam_build_id": (normalized(first_game.get("steamBuildId")), normalized(second_game.get("steamBuildId"))),
        "sts2_sha256": (
            normalized(first_game.get("sts2AssemblySha256")).lower(),
            normalized(second_game.get("sts2AssemblySha256")).lower(),
        ),
        "module_mvid": (
            normalized(first_game.get("moduleVersionId")).lower(),
            normalized(second_game.get("moduleVersionId")).lower(),
        ),
        "baselib_version": (
            normalized(first_game.get("baseLibVersion")),
            normalized(second_game.get("baseLibVersion")),
        ),
        "baselib_manifest_sha256": (
            normalized(first_game.get("baseLibManifestSha256")).lower(),
            normalized(second_game.get("baseLibManifestSha256")).lower(),
        ),
    }
    for key, (first_value, second_value) in fingerprint_fields.items():
        if first_value != second_value:
            blockers.append({"id": f"{key}_drift", "detail": f"{key} differs between run-1 and run-2"})

    branch = fingerprint_fields["branch"][0]
    build_id = fingerprint_fields["steam_build_id"][0]
    assembly_hash = fingerprint_fields["sts2_sha256"][0]
    mvid = fingerprint_fields["module_mvid"][0]
    baselib_version = fingerprint_fields["baselib_version"][0]
    baselib_hash = fingerprint_fields["baselib_manifest_sha256"][0]

    if branch != "public-beta":
        blockers.append({"id": "wrong_branch", "detail": "audit branch is not public-beta"})
    if not build_id.isdigit():
        blockers.append({"id": "invalid_build_id", "detail": "Steam buildid is missing or invalid"})
    if not SHA256_RE.fullmatch(assembly_hash):
        blockers.append({"id": "invalid_sts2_hash", "detail": "sts2.dll SHA-256 is missing or invalid"})
    if not mvid:
        blockers.append({"id": "missing_mvid", "detail": "module MVID is missing"})
    if not baselib_version:
        blockers.append(
            {
                "id": "baselib_missing",
                "detail": "BaseLib version was not detected; the runtime profile requires the exact loaded BaseLib version",
            }
        )
    if not SHA256_RE.fullmatch(baselib_hash):
        blockers.append(
            {
                "id": "baselib_manifest_hash_missing",
                "detail": "BaseLib manifest SHA-256 was not detected",
            }
        )

    coverage = target_card_coverage(report)
    if not coverage["complete"]:
        blockers.append(
            {
                "id": "target_card_coverage_incomplete",
                "detail": (
                    f"only {coverage['found_count']}/{coverage['required_count']} target card model types "
                    "were present in the bounded symbol report"
                ),
            }
        )

    bindings = review.get("bindings", [])
    binding_ids = {normalized(item.get("id")) for item in bindings}
    missing_bindings = sorted(REQUIRED_BINDINGS - binding_ids)
    if missing_bindings:
        blockers.append(
            {
                "id": "binding_sections_missing",
                "detail": "missing binding review sections: " + ", ".join(missing_bindings),
            }
        )

    invalid_candidates = 0
    selected_bindings: list[str] = []
    unreviewed_bindings: list[str] = []
    for binding in bindings:
        binding_id = normalized(binding.get("id"))
        review_state = binding.get("review", {})
        if normalized(review_state.get("selected_candidate_id")):
            selected_bindings.append(binding_id)
        if normalized(review_state.get("status")).lower() != "verified":
            unreviewed_bindings.append(binding_id)
        for candidate in binding.get("candidates", []):
            if normalized(candidate.get("kind")).lower() != "method":
                invalid_candidates += 1
                continue
            if not METHOD_TOKEN_RE.fullmatch(normalized(candidate.get("metadata_token"))):
                invalid_candidates += 1

    if invalid_candidates:
        blockers.append(
            {
                "id": "invalid_binding_candidates",
                "detail": f"{invalid_candidates} binding candidates are not MethodDef entries",
            }
        )

    if unreviewed_bindings:
        warnings.append(
            {
                "id": "runtime_review_pending",
                "detail": (
                    f"{len(unreviewed_bindings)} binding sections remain unverified; runtime observation, "
                    "fallback verification, and local-visual-only verification are still required"
                ),
            }
        )
    if not selected_bindings:
        warnings.append(
            {
                "id": "no_bindings_selected",
                "detail": "no candidate binding has been selected, which is the expected fail-closed state",
            }
        )

    card_candidates = review.get("card_id_candidates", [])
    reviewed_card_candidates = [
        item
        for item in card_candidates
        if normalized(item.get("review", {}).get("status")).lower() == "verified"
    ]
    if len(reviewed_card_candidates) < len(TARGET_CARDS):
        warnings.append(
            {
                "id": "card_id_review_pending",
                "detail": f"{len(reviewed_card_candidates)}/{len(TARGET_CARDS)} target card IDs have verified review evidence",
            }
        )

    status = "blocked" if blockers else "pending_review"
    return {
        "schema_version": 1,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": status,
        "can_create_runtime_profile": not blockers and not unreviewed_bindings,
        "can_create_pending_review_profile": not blockers,
        "fingerprint": {
            "branch": branch,
            "steam_build_id": build_id,
            "sts2_sha256": assembly_hash,
            "module_mvid": mvid,
            "baselib_version": baselib_version,
            "baselib_manifest_sha256": baselib_hash,
        },
        "latest_beta": {
            "installed_build_id": normalized(attestation.get("installed_build_id")),
            "remote_build_id": normalized(attestation.get("remote_build_id")),
            "is_latest": attestation.get("is_latest") is True,
            "checked_at_utc": normalized(attestation.get("checked_at_utc")),
        },
        "two_run_comparison": {
            "equivalent": comparison.get("equivalent") is True,
            "same_assembly": comparison.get("sameAssembly") is True,
            "same_steam_build": comparison.get("sameSteamBuild") is True,
            "same_baselib_version": comparison.get("sameBaseLibVersion") is True,
            "same_symbols": comparison.get("sameSymbols") is True,
            "same_assets": comparison.get("sameAssets") is True,
        },
        "target_card_coverage": coverage,
        "binding_review": {
            "required_count": len(REQUIRED_BINDINGS),
            "present_count": len(binding_ids.intersection(REQUIRED_BINDINGS)),
            "selected": sorted(selected_bindings),
            "unreviewed": sorted(unreviewed_bindings),
            "invalid_candidate_count": invalid_candidates,
        },
        "blockers": blockers,
        "warnings": warnings,
        "source_sha256": {
            "report": digest(report_path),
            "second_report": digest(second_report_path),
            "comparison": digest(comparison_path),
            "attestation": digest(attestation_path),
            "review": digest(review_path),
        },
    }


def markdown(result: dict[str, Any]) -> str:
    lines = [
        "# Real audit evidence readiness",
        "",
        f"- Status: `{result['status']}`",
        f"- Pending-review profile allowed: `{result['can_create_pending_review_profile']}`",
        f"- Runtime profile allowed: `{result['can_create_runtime_profile']}`",
        f"- Branch/build: `{result['fingerprint']['branch']} / {result['fingerprint']['steam_build_id']}`",
        f"- Target card coverage: `{result['target_card_coverage']['found_count']}/{result['target_card_coverage']['required_count']}`",
        f"- BaseLib: `{result['fingerprint']['baselib_version'] or 'missing'}`",
        "",
    ]
    if result["blockers"]:
        lines.extend(["## Blockers", ""])
        lines.extend(f"- `{item['id']}`: {item['detail']}" for item in result["blockers"])
        lines.append("")
    if result["warnings"]:
        lines.extend(["## Pending review", ""])
        lines.extend(f"- `{item['id']}`: {item['detail']}" for item in result["warnings"])
        lines.append("")
    missing = result["target_card_coverage"]["missing"]
    if missing:
        lines.extend(["## Missing target card coverage", ""])
        lines.extend(f"- `{card_id}`" for card_id in missing)
        lines.append("")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate whether a real two-run audit is ready for an exact-build profile.")
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--second-report", type=Path, required=True)
    parser.add_argument("--comparison", type=Path, required=True)
    parser.add_argument("--latest-beta-attestation", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--strict", action="store_true")
    args = parser.parse_args()

    result = analyze(
        args.report,
        args.second_report,
        args.comparison,
        args.latest_beta_attestation,
        args.review,
    )
    args.output.mkdir(parents=True, exist_ok=True)
    json_path = args.output / "audit-readiness.json"
    markdown_path = args.output / "audit-readiness.md"
    json_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    markdown_path.write_text(markdown(result), encoding="utf-8")

    marker = "AUDIT_READINESS_OK" if result["status"] != "blocked" else "AUDIT_READINESS_BLOCKED"
    print(
        f"{marker} status={result['status']} "
        f"cards={result['target_card_coverage']['found_count']}/{result['target_card_coverage']['required_count']} "
        f"baselib={result['fingerprint']['baselib_version'] or 'missing'} output={json_path}"
    )
    if args.strict and result["status"] == "blocked":
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

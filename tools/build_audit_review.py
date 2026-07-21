#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CONTRACT = ROOT / "SasukeIronclad/data/game_integration_contract.json"
DEFAULT_RULES = ROOT / "tools/game_audit/binding-review-rules.json"
TOKEN_RE = re.compile(r"^0x[0-9a-fA-F]{8}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
REQUIRED_BETA_BRANCH = "public-beta"


def load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized(value: object) -> str:
    return str(value or "").strip()


def candidate_id(symbol: dict[str, Any]) -> str:
    raw = "|".join(
        [
            normalized(symbol.get("kind")),
            normalized(symbol.get("metadataToken")),
            normalized(symbol.get("declaringType")),
            normalized(symbol.get("signature")),
        ]
    )
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()[:20]


def validate_sources(report: dict[str, Any], comparison: dict[str, Any]) -> None:
    if report.get("schemaVersion") != 1:
        raise SystemExit("unsupported audit report schema")
    if comparison.get("schemaVersion") != 1 or comparison.get("equivalent") is not True:
        raise SystemExit("binding review requires two equivalent audit runs")
    session = normalized(report.get("session"))
    sessions = {
        normalized(comparison.get("firstSession")),
        normalized(comparison.get("secondSession")),
    }
    if not session or session not in sessions:
        raise SystemExit("selected report was not part of the supplied comparison")


def validate_latest_beta(
    report: dict[str, Any],
    attestation: dict[str, Any],
) -> dict[str, Any]:
    if attestation.get("schema_version") != 1:
        raise SystemExit("unsupported latest beta attestation schema")
    if attestation.get("status") != "verified" or attestation.get("is_latest") is not True:
        raise SystemExit("latest beta attestation is not verified")

    required_branch = normalized(attestation.get("required_branch")).lower()
    installed_branch = normalized(attestation.get("installed_branch")).lower()
    installed_build = normalized(attestation.get("installed_build_id"))
    remote_build = normalized(attestation.get("remote_build_id"))
    report_branch = normalized(report.get("branch")).lower()
    report_build = normalized(report.get("game", {}).get("steamBuildId"))
    checked_at = normalized(attestation.get("checked_at_utc"))
    output_sha = normalized(attestation.get("steamcmd_output_sha256")).lower()

    if required_branch != REQUIRED_BETA_BRANCH or installed_branch != REQUIRED_BETA_BRANCH:
        raise SystemExit("audit review requires an installed public-beta branch")
    if report_branch != REQUIRED_BETA_BRANCH:
        raise SystemExit("audit report branch is not public-beta")
    if not installed_build.isdigit() or installed_build != remote_build or report_build != remote_build:
        raise SystemExit("audit report buildid does not match the remotely attested latest public-beta build")
    if not SHA256_RE.fullmatch(output_sha):
        raise SystemExit("latest beta attestation lacks a valid SteamCMD output digest")
    try:
        parsed_checked_at = datetime.fromisoformat(checked_at.replace("Z", "+00:00"))
    except ValueError as exc:
        raise SystemExit("latest beta attestation timestamp is invalid") from exc
    if parsed_checked_at.tzinfo is None:
        raise SystemExit("latest beta attestation timestamp must include a timezone")

    return {
        "required_branch": required_branch,
        "installed_build_id": installed_build,
        "remote_build_id": remote_build,
        "checked_at_utc": parsed_checked_at.astimezone(timezone.utc).isoformat(),
        "source": normalized(attestation.get("source")),
        "steamcmd_output_sha256": output_sha,
        "is_latest": True,
    }


def score_candidate(symbol: dict[str, Any], rule: dict[str, Any]) -> int | None:
    kind = normalized(symbol.get("kind")).lower()
    allowed_kinds = {normalized(item).lower() for item in rule.get("symbol_kinds", [])}
    if allowed_kinds and kind not in allowed_kinds:
        return None
    token = normalized(symbol.get("metadataToken"))
    if not TOKEN_RE.fullmatch(token):
        return None

    name = normalized(symbol.get("name")).lower()
    declaring = normalized(symbol.get("declaringType")).lower()
    signature = normalized(symbol.get("signature")).lower()
    constant_value = normalized(symbol.get("constantValue")).lower()
    categories = {normalized(item).lower() for item in symbol.get("categories", [])}

    score = int(symbol.get("score", 0))
    expected_categories = {normalized(item).lower() for item in rule.get("categories", [])}
    score += 18 * len(categories.intersection(expected_categories))
    for term in rule.get("name_terms", []):
        term = normalized(term).lower()
        if not term:
            continue
        if term in name:
            score += 18
        if term in declaring:
            score += 9
        if term in signature:
            score += 5
        if constant_value and term in constant_value:
            score += 7
    for term in rule.get("exclude_terms", []):
        term = normalized(term).lower()
        if term and (term in name or term in declaring or term in signature):
            score -= 24
    return score


def build_binding_entry(
    binding_id: str,
    binding_kind: str,
    rule: dict[str, Any],
    symbols: list[dict[str, Any]],
) -> dict[str, Any]:
    ranked: list[tuple[int, dict[str, Any]]] = []
    for symbol in symbols:
        score = score_candidate(symbol, rule)
        if score is not None:
            ranked.append((score, symbol))
    ranked.sort(
        key=lambda item: (
            -item[0],
            normalized(item[1].get("declaringType")),
            normalized(item[1].get("name")),
            normalized(item[1].get("metadataToken")),
        )
    )
    limit = max(1, min(int(rule.get("max_candidates", 20)), 50))
    candidates: list[dict[str, Any]] = []
    for review_score, symbol in ranked[:limit]:
        candidates.append(
            {
                "candidate_id": candidate_id(symbol),
                "review_score": review_score,
                "kind": normalized(symbol.get("kind")),
                "name": normalized(symbol.get("name")),
                "declaring_type": normalized(symbol.get("declaringType")),
                "signature": normalized(symbol.get("signature")),
                "metadata_token": normalized(symbol.get("metadataToken")),
                "visibility": normalized(symbol.get("visibility")),
                "constant_value": normalized(symbol.get("constantValue")),
                "audit_score": int(symbol.get("score", 0)),
                "categories": sorted(
                    {normalized(item) for item in symbol.get("categories", []) if normalized(item)}
                ),
            }
        )

    return {
        "id": binding_id,
        "kind": binding_kind,
        "purpose": normalized(rule.get("purpose")),
        "required_evidence": list(rule.get("required_evidence", [])),
        "candidates": candidates,
        "review": {
            "status": "unreviewed",
            "selected_candidate_id": "",
            "reviewer": "",
            "evidence": [],
            "notes": "",
        },
    }


def build_card_inventory(report: dict[str, Any]) -> list[dict[str, Any]]:
    results: list[dict[str, Any]] = []
    seen: set[tuple[str, str, str]] = set()
    for symbol in report.get("symbols", []):
        kind = normalized(symbol.get("kind")).lower()
        categories = {normalized(item).lower() for item in symbol.get("categories", [])}
        name = normalized(symbol.get("name"))
        declaring = normalized(symbol.get("declaringType"))
        value = normalized(symbol.get("constantValue"))
        searchable = f"{name} {declaring} {normalized(symbol.get('signature'))}".lower()
        card_related = "cards" in categories or "card" in searchable or "ironclad" in searchable
        if not card_related or kind not in {"field", "property", "type"}:
            continue
        if kind == "field" and not value and "id" not in name.lower():
            continue
        key = (kind, normalized(symbol.get("metadataToken")), value)
        if key in seen:
            continue
        seen.add(key)
        results.append(
            {
                "candidate_id": candidate_id(symbol),
                "kind": kind,
                "name": name,
                "declaring_type": declaring,
                "signature": normalized(symbol.get("signature")),
                "metadata_token": normalized(symbol.get("metadataToken")),
                "constant_value": value,
                "categories": sorted(categories),
                "review": {
                    "status": "unreviewed",
                    "canonical_card_id": "",
                    "card_kind": "unknown",
                    "reviewer": "",
                    "notes": "",
                },
            }
        )
    results.sort(
        key=lambda item: (
            0 if item["constant_value"] else 1,
            item["declaring_type"],
            item["name"],
            item["metadata_token"],
        )
    )
    return results


def build_review(
    report_path: Path,
    comparison_path: Path,
    contract_path: Path,
    rules_path: Path,
    latest_beta_attestation_path: Path,
) -> dict[str, Any]:
    report = load(report_path)
    comparison = load(comparison_path)
    contract = load(contract_path)
    rules = load(rules_path)
    attestation = load(latest_beta_attestation_path)
    validate_sources(report, comparison)
    latest_beta = validate_latest_beta(report, attestation)

    rules_by_id = {normalized(item.get("id")): item for item in rules.get("bindings", [])}
    required: list[tuple[str, str]] = [
        *((normalized(item), "visual") for item in contract.get("required_visual_events", [])),
        *((normalized(item), "title") for item in contract.get("required_title_surfaces", [])),
    ]
    missing_rules = [binding_id for binding_id, _ in required if binding_id not in rules_by_id]
    if missing_rules:
        raise SystemExit("binding review rules are missing: " + ", ".join(missing_rules))

    symbols = list(report.get("symbols", []))
    game = report.get("game", {})
    bindings = [
        build_binding_entry(binding_id, binding_kind, rules_by_id[binding_id], symbols)
        for binding_id, binding_kind in required
    ]
    return {
        "schema_version": 1,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "status": "unreviewed",
        "source": {
            "report_session": normalized(report.get("session")),
            "comparison_sessions": sorted(
                {
                    normalized(comparison.get("firstSession")),
                    normalized(comparison.get("secondSession")),
                }
            ),
            "report_sha256": digest(report_path),
            "comparison_sha256": digest(comparison_path),
            "latest_beta_attestation_sha256": digest(latest_beta_attestation_path),
            "two_runs_equivalent": True,
        },
        "fingerprint": {
            "branch": normalized(report.get("branch")),
            "steam_build_id": normalized(game.get("steamBuildId")),
            "sts2_sha256": normalized(game.get("sts2AssemblySha256")).lower(),
            "module_mvid": normalized(game.get("moduleVersionId")).lower(),
            "baselib_version": normalized(game.get("baseLibVersion")),
        },
        "latest_beta": latest_beta,
        "policy": {
            "auto_selection_forbidden": True,
            "selected_symbols_must_come_from_report": True,
            "output_bindings_remain_pending_review": True,
            "runtime_observation_required": True,
            "latest_public_beta_required": True,
        },
        "bindings": bindings,
        "card_id_candidates": build_card_inventory(report),
    }


def markdown(review: dict[str, Any]) -> str:
    lines = [
        "# 游戏接口候选审阅表",
        "",
        "> 所有候选仅来自两次一致的最新 public-beta 本地元数据审计；本文件不会自动选择或启用接口。",
        "",
        f"- 状态：`{review['status']}`",
        f"- 审计 Session：`{review['source']['report_session']}`",
        f"- 分支：`{review['fingerprint']['branch']}`",
        f"- Steam buildid：`{review['fingerprint']['steam_build_id']}`",
        f"- 远端 public-beta buildid：`{review['latest_beta']['remote_build_id']}`",
        f"- Beta 校验时间：`{review['latest_beta']['checked_at_utc']}`",
        f"- sts2 SHA-256：`{review['fingerprint']['sts2_sha256']}`",
        "",
    ]
    for binding in review["bindings"]:
        lines.extend(
            [
                f"## {binding['kind']}：`{binding['id']}`",
                "",
                binding["purpose"] or "待人工确认用途。",
                "",
                "| 排名 | 分数 | 类型 | Token | 声明类型 | 签名 |",
                "|---:|---:|---|---|---|---|",
            ]
        )
        for index, candidate in enumerate(binding["candidates"], 1):
            signature = candidate["signature"].replace("|", "\\|").replace("`", "'")
            lines.append(
                f"| {index} | {candidate['review_score']} | {candidate['kind']} | "
                f"`{candidate['metadata_token']}` | `{candidate['declaring_type']}` | `{signature}` |"
            )
        if not binding["candidates"]:
            lines.append("| - | - | - | - | - | 未找到候选，需扩展审计关键词 |")
        lines.extend(
            [
                "",
                "人工结论：",
                "",
                "- [ ] 两次启动均观察到该入口",
                "- [ ] 已确认只读取视觉所需数据",
                "- [ ] 已验证失败时原游戏表现保留",
                "- [ ] 已检查多人仅创建本地视觉节点",
                "",
            ]
        )

    lines.extend(
        [
            "## 卡牌 ID 候选",
            "",
            "| 类型 | Token | 声明类型 | 名称 | 元数据常量 |",
            "|---|---|---|---|---|",
        ]
    )
    for item in review["card_id_candidates"][:300]:
        value = item["constant_value"].replace("|", "\\|").replace("`", "'")
        lines.append(
            f"| {item['kind']} | `{item['metadata_token']}` | `{item['declaring_type']}` | "
            f"`{item['name']}` | `{value}` |"
        )
    if not review["card_id_candidates"]:
        lines.append("| - | - | - | - | 未发现候选 |")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Build a no-auto-selection review workbook from equivalent latest-beta audit runs."
    )
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--comparison", type=Path, required=True)
    parser.add_argument("--latest-beta-attestation", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--contract", type=Path, default=DEFAULT_CONTRACT)
    parser.add_argument("--rules", type=Path, default=DEFAULT_RULES)
    args = parser.parse_args()

    review = build_review(
        args.report,
        args.comparison,
        args.contract,
        args.rules,
        args.latest_beta_attestation,
    )
    args.output.mkdir(parents=True, exist_ok=True)
    json_path = args.output / "binding-review.json"
    md_path = args.output / "binding-review.md"
    json_path.write_text(json.dumps(review, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    md_path.write_text(markdown(review), encoding="utf-8")
    print(
        f"AUDIT_REVIEW_OK bindings={len(review['bindings'])} "
        f"card_candidates={len(review['card_id_candidates'])} "
        f"beta_build={review['latest_beta']['remote_build_id']} output={args.output}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

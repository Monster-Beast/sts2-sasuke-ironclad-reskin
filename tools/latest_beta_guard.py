#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

APP_ID = "2868840"
DEFAULT_BRANCH = "public-beta"
SUPPORTED_BRANCHES = {
    "stable": "public",
    "public-beta": "public-beta",
}
PAIR_RE = re.compile(r'"(?P<key>[^"\\]*(?:\\.[^"\\]*)*)"\s+"(?P<value>[^"\\]*(?:\\.[^"\\]*)*)"')


class LatestBetaError(RuntimeError):
    """Backward-compatible error name for release-channel attestation failures."""


def _unescape(value: str) -> str:
    return value.replace(r'\"', '"').replace(r'\\', '\\')


def _acf_pairs(text: str) -> list[tuple[str, str]]:
    return [(_unescape(match.group("key")), _unescape(match.group("value"))) for match in PAIR_RE.finditer(text)]


def _first_value(text: str, key: str) -> str:
    for current_key, value in _acf_pairs(text):
        if current_key.casefold() == key.casefold():
            return value.strip()
    return ""


def normalize_branch(value: str | None) -> str:
    candidate = (value or "").strip().lower()
    if candidate in {"", "none", "public", "default", "stable"}:
        return "stable"
    if candidate in {"beta", "public_beta", "public beta"}:
        return DEFAULT_BRANCH
    return candidate


def remote_branch_name(local_branch: str) -> str:
    normalized = normalize_branch(local_branch)
    try:
        return SUPPORTED_BRANCHES[normalized]
    except KeyError as exc:
        raise LatestBetaError(
            f"unsupported release channel {normalized!r}; expected one of: {', '.join(sorted(SUPPORTED_BRANCHES))}"
        ) from exc


def find_appmanifest(game_path: Path) -> Path:
    current = game_path.resolve()
    if current.is_file():
        current = current.parent
    for _ in range(8):
        candidate = current / f"appmanifest_{APP_ID}.acf"
        if candidate.is_file():
            return candidate
        if current.parent == current:
            break
        current = current.parent
    raise LatestBetaError(f"appmanifest_{APP_ID}.acf was not found above the game directory")


def read_installed_metadata(game_path: Path) -> tuple[str, str]:
    manifest = find_appmanifest(game_path)
    text = manifest.read_text(encoding="utf-8", errors="replace")
    build_id = _first_value(text, "buildid")
    beta_key = _first_value(text, "BetaKey")
    branch = normalize_branch(beta_key)
    if not build_id.isdigit():
        raise LatestBetaError("installed Steam buildid is missing or invalid")
    return branch, build_id


def _balanced_block(text: str, start: int, branch: str) -> str:
    depth = 0
    in_string = False
    escaped = False
    for index in range(start, len(text)):
        char = text[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[start : index + 1]
    raise LatestBetaError(f"SteamCMD branch block for {branch!r} is incomplete")


def _find_branch_blocks(text: str, branch: str) -> list[str]:
    branch_pattern = re.compile(rf'"{re.escape(branch)}"\s*\{{', re.IGNORECASE)
    matches = list(branch_pattern.finditer(text))
    if not matches:
        raise LatestBetaError(f"SteamCMD output does not contain branch {branch!r}")

    blocks: list[str] = []
    incomplete = False
    for match in matches:
        try:
            blocks.append(_balanced_block(text, match.end() - 1, branch))
        except LatestBetaError:
            incomplete = True

    if blocks:
        return blocks
    if incomplete:
        raise LatestBetaError(f"SteamCMD branch block for {branch!r} is incomplete")
    raise LatestBetaError(f"SteamCMD output does not contain a complete branch block for {branch!r}")


def read_remote_build_id(steamcmd_output: Path, branch: str = DEFAULT_BRANCH) -> str:
    text = steamcmd_output.read_text(encoding="utf-8", errors="replace")
    for block in _find_branch_blocks(text, branch):
        build_id = _first_value(block, "buildid")
        if build_id.isdigit():
            return build_id
    raise LatestBetaError(f"remote buildid for {branch!r} is missing or invalid")


def create_attestation(
    game_path: Path,
    steamcmd_output: Path,
    required_branch: str = DEFAULT_BRANCH,
    checked_at: datetime | None = None,
) -> dict[str, Any]:
    required = normalize_branch(required_branch)
    remote_branch = remote_branch_name(required)
    installed_branch, installed_build_id = read_installed_metadata(game_path)
    remote_build_id = read_remote_build_id(steamcmd_output, remote_branch)
    is_latest = installed_branch == required and installed_build_id == remote_build_id
    timestamp = checked_at or datetime.now(timezone.utc)
    output_digest = hashlib.sha256(steamcmd_output.read_bytes()).hexdigest()

    return {
        "schema_version": 1,
        "app_id": APP_ID,
        "required_branch": required,
        "remote_branch": remote_branch,
        "installed_branch": installed_branch,
        "installed_build_id": installed_build_id,
        "remote_build_id": remote_build_id,
        "is_latest": is_latest,
        "status": "verified" if is_latest else "rejected",
        "checked_at_utc": timestamp.astimezone(timezone.utc).isoformat(),
        "source": "steamcmd_app_info_print",
        "steamcmd_output_sha256": output_digest,
    }


def write_attestation(attestation: dict[str, Any], output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(attestation, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def verify(args: argparse.Namespace) -> int:
    try:
        attestation = create_attestation(
            Path(args.game_path),
            Path(args.steamcmd_output),
            args.required_branch,
        )
        write_attestation(attestation, Path(args.output))
    except LatestBetaError as exc:
        print(f"RELEASE_CHANNEL_GUARD_ERROR {exc}", file=sys.stderr)
        return 2

    if not attestation["is_latest"]:
        print(
            "RELEASE_CHANNEL_STALE "
            f"installed_branch={attestation['installed_branch']} "
            f"required_branch={attestation['required_branch']} "
            f"installed_build={attestation['installed_build_id']} "
            f"remote_build={attestation['remote_build_id']}",
            file=sys.stderr,
        )
        return 3

    marker = "LATEST_BETA_OK" if attestation["required_branch"] == DEFAULT_BRANCH else "RELEASE_CHANNEL_OK"
    print(
        f"{marker} "
        f"branch={attestation['required_branch']} "
        f"remote_branch={attestation['remote_branch']} "
        f"buildid={attestation['remote_build_id']} "
        f"output={args.output}"
    )
    return 0


def parse_remote(args: argparse.Namespace) -> int:
    try:
        requested = normalize_branch(args.branch)
        build_id = read_remote_build_id(Path(args.steamcmd_output), remote_branch_name(requested))
    except LatestBetaError as exc:
        print(f"RELEASE_CHANNEL_GUARD_ERROR {exc}", file=sys.stderr)
        return 2
    print(build_id)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Verify that a local Slay the Spire 2 installation matches the current stable or public-beta Steam build."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    verify_parser = subparsers.add_parser("verify")
    verify_parser.add_argument("--game-path", required=True)
    verify_parser.add_argument("--steamcmd-output", required=True)
    verify_parser.add_argument("--output", required=True)
    verify_parser.add_argument("--required-branch", default=DEFAULT_BRANCH, choices=sorted(SUPPORTED_BRANCHES))
    verify_parser.set_defaults(handler=verify)

    parse_parser = subparsers.add_parser("parse-remote")
    parse_parser.add_argument("--steamcmd-output", required=True)
    parse_parser.add_argument("--branch", default=DEFAULT_BRANCH, choices=sorted(SUPPORTED_BRANCHES))
    parse_parser.set_defaults(handler=parse_remote)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    return int(args.handler(args))


if __name__ == "__main__":
    raise SystemExit(main())

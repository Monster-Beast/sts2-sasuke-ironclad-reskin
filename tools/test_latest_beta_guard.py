#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import tempfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "tools/latest_beta_guard.py"


def load_module():
    spec = importlib.util.spec_from_file_location("latest_beta_guard", MODULE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def steam_output(public_build: str, beta_build: str) -> str:
    return f'''AppID : 2868840, change number : 999999
"2868840"
{{
  "depots"
  {{
    "branches"
    {{
      "public"
      {{
        "buildid" "{public_build}"
      }}
      "public-beta"
      {{
        "buildid" "{beta_build}"
        "timeupdated" "1780000000"
      }}
    }}
  }}
}}
'''


def appmanifest(build_id: str, beta_key: str) -> str:
    return f'''"AppState"
{{
  "appid" "2868840"
  "buildid" "{build_id}"
  "UserConfig"
  {{
    "BetaKey" "{beta_key}"
  }}
}}
'''


def expect_error(module, callback, contains: str) -> None:
    try:
        callback()
    except module.LatestBetaError as exc:
        assert contains in str(exc), str(exc)
    else:
        raise AssertionError(f"expected LatestBetaError containing {contains!r}")


def main() -> int:
    module = load_module()
    policy = json.loads((ROOT / "SasukeIronclad/data/latest_beta_policy.json").read_text(encoding="utf-8"))
    assert policy["required_branch"] == "public-beta"
    assert policy["tracking_mode"] == "rolling_latest"
    assert policy["profile_must_match_attested_build"] is True
    assert policy["stale_profiles_disabled"] is True
    assert 1 <= int(policy["max_attestation_age_hours"]) <= 168

    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        steamapps = root / "steamapps"
        game = steamapps / "common/Slay the Spire 2"
        game.mkdir(parents=True)
        manifest = steamapps / "appmanifest_2868840.acf"
        remote = root / "steamcmd.txt"

        manifest.write_text(appmanifest("222222", "public-beta"), encoding="utf-8")
        remote.write_text(steam_output("111111", "222222"), encoding="utf-8")
        attestation = module.create_attestation(
            game,
            remote,
            "public-beta",
            datetime(2026, 7, 21, 0, 0, tzinfo=timezone.utc),
        )
        assert attestation["is_latest"] is True
        assert attestation["status"] == "verified"
        assert attestation["installed_branch"] == "public-beta"
        assert attestation["installed_build_id"] == "222222"
        assert attestation["remote_build_id"] == "222222"
        assert str(root) not in json.dumps(attestation)

        manifest.write_text(appmanifest("222221", "public-beta"), encoding="utf-8")
        stale = module.create_attestation(game, remote, "public-beta")
        assert stale["is_latest"] is False
        assert stale["status"] == "rejected"

        manifest.write_text(appmanifest("222222", ""), encoding="utf-8")
        stable = module.create_attestation(game, remote, "public-beta")
        assert stable["installed_branch"] == "stable"
        assert stable["is_latest"] is False

        expect_error(
            module,
            lambda: module.create_attestation(game, remote, "stable"),
            "only supports",
        )
        remote.write_text(steam_output("111111", "not-a-build"), encoding="utf-8")
        expect_error(module, lambda: module.read_remote_build_id(remote), "missing or invalid")

    print("LATEST_BETA_GUARD_OK branch=public-beta latest=true stale_rejected=true stable_rejected=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

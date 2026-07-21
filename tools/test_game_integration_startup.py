#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8")


def main() -> int:
    main_file = read("SasukeIroncladCode/MainFile.cs")
    bootstrap = read("SasukeIroncladCode/Runtime/GameIntegrationBootstrap.cs")
    collector = read("SasukeIroncladCode/Runtime/RuntimeBuildFingerprintCollector.cs")
    installer = read("SasukeIroncladCode/Adapters/PendingGameIntegrationInstaller.cs")

    assert "HarmonyLib" not in main_file
    assert "PatchAll" not in main_file
    assert "GameIntegrationBootstrap.Start" in main_file
    assert "CurrentProcessRuntimeBuildFingerprintProvider" in main_file
    assert "PendingGameIntegrationInstaller" in main_file

    for contract in [
        "SafeReset(installer)",
        "if (!decision.AnyEnabled",
        "installer.Install(profile, decision)",
        "Integration installation failed closed",
        "DisabledDecision",
    ]:
        assert contract in bootstrap, contract

    for contract in [
        "SHA256.HashData",
        "ReadModuleMvid",
        "ReadSteamBuildId",
        "ReadBaseLibVersion",
        "STS2_BRANCH",
        "The STS2 branch is unknown",
    ]:
        assert contract in collector, contract

    assert "throw new InvalidOperationException" in installer
    assert "No audited game adapter is registered" in installer
    assert "HarmonyPatch" not in bootstrap + collector + installer

    print("GAME_INTEGRATION_STARTUP_OK patchall=false exact_fingerprint=true installer=fail_closed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

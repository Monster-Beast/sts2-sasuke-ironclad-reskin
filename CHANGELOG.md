# Changelog

## [0.1.0-alpha.0] - 2026-07-20

### Added

- 当前 ModTemplate 基线工程；
- 数据驱动视觉配置加载器；
- 13 张卡牌概念和 13 个动作 Profile；
- 角色、美术、动画、技术和版权文档；
- GitHub Actions 与本地校验工具。

## Unreleased

### Card-specific presentation runtime

- Added 13 independent card timelines selected only by original `card_id`.
- Added authoritative impact gating, multi-hit loops, fast and low-flash modifiers, persistent states, Demon Form and character-state presentation.
- Added Chinese and English display-only Sasuke card titles without changing rules or internal identity.
- Added Godot 4.5.1 headless parser, stress, cleanup, Form and character-state execution tests.

### Local game audit and exact-build integration

- Added a metadata-only .NET 9 audit CLI with two-run comparison and path redaction.
- Added managed type, method, field, property, event and safe metadata-constant discovery.
- Added automatic card-ID candidates and a no-auto-selection binding review workbook.
- Added a reviewed-profile compiler that can emit only `pending_review` bindings.
- Removed unconditional startup `Harmony.PatchAll()`.
- Added exact branch, Steam buildid, assembly SHA-256, MVID and BaseLib runtime fingerprint collection.
- Added Steam BetaKey and Workshop BaseLib discovery for runtime fingerprints.
- Added a fail-closed integration bootstrap and default installer that refuses unaudited hooks.
- Restricted verified bindings to MethodDef metadata tokens.
- Added a second MethodDef resolution pass checking token, module MVID, declaring type and method signature.
- Added Bash and PowerShell audit-helper syntax checks plus automatic review generation after equivalent scans.

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

### Exact-build presentation canaries

- Added two-run read-only runtime observation and a manual binding review for the reviewed public Beta.
- Added explicit title-only, animation-only and local-Ironclad replacement canaries with fail-safe restoration.
- Added local-player anchor calibration, same-process multi-combat stability evidence and damage-only original-impact synchronization.
- Added an explicit combined title-and-animation canary with a bounded privacy-safe JSONL event journal.
- Added deterministic checkpoint capture that always records an empty process list and copies the active journal by startup-session identity.
- Added process-query, game-process and journal-tail metadata to checkpoint evidence.
- Added a journal analyzer that separates expected unreviewed-card fallback from hard adapter, anchor or playback failure.
- Added checkpoint-backed session closure when the runtime cannot append `session_stop` during process shutdown.
- Recorded a real combined run with nine title applications, three completed animations, two original-impact forwards, successful replacement/restoration and no hard failures.
- Kept `reward`, `compendium` and `tooltip` as a focused combined-surface follow-up instead of repeating the full combat matrix.
- Kept the production integration contract disabled and retained `form_removed`, `character_state` and Demon Form replacement as blocked work.

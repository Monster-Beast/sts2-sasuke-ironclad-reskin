# Public-beta runtime binding review

## Scope

This review is bound to the exact installed runtime below:

- branch: `public-beta`
- Steam build: `24251656`
- `sts2.dll` SHA-256: `ee45848ff6319dfc7af2538d3a52d05d82bef35ee4c5fd0400dc9efe8f9054aa`
- module MVID: `a49d3537-5a42-4dcd-9877-663e394f2b44`
- BaseLib: `v3.3.7`

The source evidence consists of two independent real-game sessions:

- run 1: 4,279 events, SHA-256 `338d85a75625876ffdaa8c29fc3452aacc2c48b35fce6e1d0153e66869b4a5b6`
- run 2: 6,109 events, SHA-256 `bd22300d335cb6292051f0bb6d54e9e77837d5926a7b94c049bd001eb35f9392`

Both sessions have balanced enter/return pairs, zero open calls and no analysis errors. Neither session contains a final `session_stop` record, so that warning remains visible and is not treated as proof of a clean shutdown.

## Manual decisions

The following methods are approved only as exact-build canary candidates:

| Binding | Selected target | Reason |
| --- | --- | --- |
| `card_visual_request` | `card-play.local-queue` | Earliest local callback carrying the concrete `CardModel`. |
| `original_impact` | `impact.damage-history` | Authoritative damage-history record carrying `DamageResult` and the source card when present. |
| `state_removed` | `power.container-removed` | Directly carries the removed `PowerModel`. |
| `combat_ended` | `combat.room-ended` | Earliest semantic combat endpoint, before UI/save cleanup. |
| `card_art` | `title.card-label-update` | Direct `NCard` title-label refresh with a concrete model. |
| `hand` | `title.hand-card-update` | Hand-holder-specific refresh. |
| `deck_list` | `title.deck-grid-set` | Observed in both sessions with deck/card-grid context. |
| `reward` | `title.reward-refresh` | Reward-screen-specific population callback. |
| `compendium` | `title.compendium-grid-display` | Card-library-grid-specific display callback. |
| `tooltip` | `title.card-hover` | Narrow hover/inspection callback with a concrete holder model. |

These selections do not activate production hooks. They only define the next canary implementation candidates.

## Bindings still blocked

### `form_removed`

The generic power-removal path was observed repeatedly, but neither session applied and removed Demon Form. The canary must not infer a form-removal binding from unrelated powers. A targeted run must observe the concrete Demon Form power type entering and leaving the player.

### `character_state`

The existing candidates represent:

- combat-state object reset at combat setup;
- broad player phase changes;
- numeric state-value changes dominated by energy.

They do not prove hit, death or victory semantics. Direct local-player hit/death/victory targets must be added or reviewed before this binding can be selected.

## Remaining safety gates

Before a profile can be promoted from `pending_review`:

1. Build an exact-build canary adapter that uses only `approved_for_canary` decisions.
2. Require an explicit local opt-in marker; default startup must remain unchanged.
3. Demonstrate fallback to the original title/visual when an asset, method target or adapter action fails.
4. Verify multiplayer behavior changes only local presentation nodes and never network/gameplay state.
5. Collect targeted Demon Form and character hit/death/victory evidence.
6. Repeat the exact-build checks after every public-beta update.

The production `game_integration_contract.json` remains `pending_local_audit` with no profiles, and the checked-in exact-build profile remains `pending_review`.

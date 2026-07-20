#!/usr/bin/env bash
set -euo pipefail

GAME_PATH="${1:-}"
ASSET_ROOT="${2:-}"
BRANCH="${BRANCH:-stable}"
OUTPUT_ROOT="${OUTPUT_ROOT:-local-audit}"
MEGADOT_VERSION="${MEGADOT_VERSION:-4.5.1}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/game_audit/SasukeIronclad.GameAudit.csproj"

if [[ -z "$GAME_PATH" ]]; then
  for candidate in \
    "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2" \
    "$HOME/.steam/steam/steamapps/common/Slay the Spire 2" \
    "$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"; do
    if [[ -d "$candidate" ]]; then
      GAME_PATH="$candidate"
      break
    fi
  done
fi

if [[ -z "$GAME_PATH" || ! -d "$GAME_PATH" ]]; then
  echo "未找到《杀戮尖塔 2》安装目录。用法：tools/run-game-audit.sh '<game-path>' ['asset-root']" >&2
  exit 2
fi

run_scan() {
  local session="$1"
  local output="$2"
  local args=(
    run --project "$PROJECT" --configuration Release --
    scan
    --game-path "$GAME_PATH"
    --output "$output"
    --branch "$BRANCH"
    --session "$session"
    --megadot-version "$MEGADOT_VERSION"
  )
  if [[ -n "$ASSET_ROOT" ]]; then
    [[ -d "$ASSET_ROOT" ]] || { echo "asset root 不存在：$ASSET_ROOT" >&2; exit 2; }
    args+=(--asset-root "$ASSET_ROOT")
  fi
  dotnet "${args[@]}"
}

run_scan run-1 "$OUTPUT_ROOT/run-1"
printf '第一次审计已完成。请完整启动并退出一次游戏，然后按 Enter 继续第二次审计。\n'
read -r _
run_scan run-2 "$OUTPUT_ROOT/run-2"

dotnet run --project "$PROJECT" --configuration Release -- compare \
  --first "$OUTPUT_ROOT/run-1/audit-report.json" \
  --second "$OUTPUT_ROOT/run-2/audit-report.json" \
  --output "$OUTPUT_ROOT/comparison"

echo "两次独立审计一致：$OUTPUT_ROOT/comparison/audit-comparison.md"
echo "不要提交 local-audit 目录；只把确认后的元数据写入审计文档。"

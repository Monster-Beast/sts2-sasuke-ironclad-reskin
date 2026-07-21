#!/usr/bin/env bash
set -euo pipefail

GAME_PATH=""
ASSET_ROOT=""
BRANCH="${BRANCH:-stable}"
OUTPUT_ROOT="${OUTPUT_ROOT:-local-audit}"
MEGADOT_VERSION="${MEGADOT_VERSION:-4.5.1}"
SINGLE_RUN=false
SKIP_REVIEW=false
TOOLS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$TOOLS_DIR/game_audit/SasukeIronclad.GameAudit.csproj"

usage() {
  cat <<'EOF'
用法：
  tools/run-game-audit.sh --game-path '<path>' [选项]

选项：
  --asset-root <path>       可选的恢复资源目录
  --branch <name>           stable、beta 或实际 Steam BetaKey；默认 stable
  --output-root <path>      默认 local-audit
  --megadot-version <text>  默认 4.5.1
  --single-run              只执行第一次扫描
  --skip-review             不自动生成 binding-review
  -h, --help                显示帮助

兼容旧用法：
  tools/run-game-audit.sh '<game-path>' ['asset-root']
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --game-path) GAME_PATH="${2:?--game-path 缺少值}"; shift 2 ;;
    --asset-root) ASSET_ROOT="${2:?--asset-root 缺少值}"; shift 2 ;;
    --branch) BRANCH="${2:?--branch 缺少值}"; shift 2 ;;
    --output-root) OUTPUT_ROOT="${2:?--output-root 缺少值}"; shift 2 ;;
    --megadot-version) MEGADOT_VERSION="${2:?--megadot-version 缺少值}"; shift 2 ;;
    --single-run) SINGLE_RUN=true; shift ;;
    --skip-review) SKIP_REVIEW=true; shift ;;
    -h|--help) usage; exit 0 ;;
    --*) echo "未知参数：$1" >&2; usage >&2; exit 2 ;;
    *)
      if [[ -z "$GAME_PATH" ]]; then
        GAME_PATH="$1"
      elif [[ -z "$ASSET_ROOT" ]]; then
        ASSET_ROOT="$1"
      else
        echo "多余的位置参数：$1" >&2
        usage >&2
        exit 2
      fi
      shift
      ;;
  esac
done

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
  echo "未找到《杀戮尖塔 2》安装目录，请通过 --game-path 指定。" >&2
  exit 2
fi
if [[ -n "$ASSET_ROOT" && ! -d "$ASSET_ROOT" ]]; then
  echo "asset root 不存在：$ASSET_ROOT" >&2
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
    args+=(--asset-root "$ASSET_ROOT")
  fi
  dotnet "${args[@]}"
}

run_scan run-1 "$OUTPUT_ROOT/run-1"
echo "第一次审计已完成：$OUTPUT_ROOT/run-1"
if [[ "$SINGLE_RUN" == true ]]; then
  exit 0
fi

printf '请完整启动并退出一次游戏，并确认 Mod 与分支未变化，然后按 Enter 继续第二次审计。\n'
read -r _
run_scan run-2 "$OUTPUT_ROOT/run-2"

dotnet run --project "$PROJECT" --configuration Release -- compare \
  --first "$OUTPUT_ROOT/run-1/audit-report.json" \
  --second "$OUTPUT_ROOT/run-2/audit-report.json" \
  --output "$OUTPUT_ROOT/comparison"

echo "两次独立审计一致：$OUTPUT_ROOT/comparison/audit-comparison.md"

if [[ "$SKIP_REVIEW" != true ]]; then
  PYTHON=""
  if command -v python3 >/dev/null 2>&1; then
    PYTHON="python3"
  elif command -v python >/dev/null 2>&1; then
    PYTHON="python"
  fi
  if [[ -n "$PYTHON" ]]; then
    "$PYTHON" "$TOOLS_DIR/build_audit_review.py" \
      --report "$OUTPUT_ROOT/run-1/audit-report.json" \
      --comparison "$OUTPUT_ROOT/comparison/audit-comparison.json" \
      --output "$OUTPUT_ROOT/review"
    echo "候选审阅表已生成：$OUTPUT_ROOT/review/binding-review.md"
  else
    echo "未检测到 Python；跳过候选审阅表。可稍后手动运行 tools/build_audit_review.py。" >&2
  fi
fi

echo "不要提交 $OUTPUT_ROOT；审阅结果在真实游戏验证完成前必须保持 pending_review。"

#!/usr/bin/env bash
set -euo pipefail

GAME_PATH=""
ASSET_ROOT=""
BRANCH="${BRANCH:-public-beta}"
OUTPUT_ROOT="${OUTPUT_ROOT:-local-audit}"
MEGADOT_VERSION="${MEGADOT_VERSION:-4.5.1}"
STEAMCMD_PATH=""
STEAMCMD_OUTPUT=""
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
  --branch public-beta      仅允许 public-beta；默认 public-beta
  --steamcmd <path>         SteamCMD 可执行文件；默认从 PATH 查找
  --steamcmd-output <path>  使用预先保存的 app_info_print 输出
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
    --steamcmd) STEAMCMD_PATH="${2:?--steamcmd 缺少值}"; shift 2 ;;
    --steamcmd-output) STEAMCMD_OUTPUT="${2:?--steamcmd-output 缺少值}"; shift 2 ;;
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

if [[ "$BRANCH" != "public-beta" ]]; then
  echo "本项目只适配 Steam 最新 public-beta，不再接受 stable 或其他分支。" >&2
  exit 2
fi

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

PYTHON=""
if command -v python3 >/dev/null 2>&1; then
  PYTHON="python3"
elif command -v python >/dev/null 2>&1; then
  PYTHON="python"
else
  echo "最新 Beta 校验需要 Python 3，但当前系统未找到 python3 或 python。" >&2
  exit 2
fi

resolve_steamcmd() {
  if [[ -n "$STEAMCMD_PATH" ]]; then
    [[ -x "$STEAMCMD_PATH" ]] || { echo "SteamCMD 不可执行：$STEAMCMD_PATH" >&2; exit 2; }
    printf '%s\n' "$STEAMCMD_PATH"
    return
  fi
  if command -v steamcmd >/dev/null 2>&1; then
    command -v steamcmd
    return
  fi
  for candidate in \
    "$HOME/steamcmd/steamcmd.sh" \
    "$HOME/.steam/steamcmd/steamcmd.sh" \
    "/usr/games/steamcmd" \
    "/usr/local/bin/steamcmd"; do
    if [[ -x "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return
    fi
  done
  echo "未找到 SteamCMD。请安装 Valve SteamCMD，或使用 --steamcmd / --steamcmd-output。" >&2
  exit 2
}

LATEST_BETA_ATTESTATION="$OUTPUT_ROOT/latest-beta/attestation.json"
verify_latest_beta() {
  mkdir -p "$OUTPUT_ROOT/latest-beta"
  local captured="$OUTPUT_ROOT/latest-beta/steamcmd-app-info.txt"
  if [[ -n "$STEAMCMD_OUTPUT" ]]; then
    [[ -f "$STEAMCMD_OUTPUT" ]] || { echo "SteamCMD 输出文件不存在：$STEAMCMD_OUTPUT" >&2; exit 2; }
    cp "$STEAMCMD_OUTPUT" "$captured"
  else
    local steamcmd
    steamcmd="$(resolve_steamcmd)"
    set +e
    "$steamcmd" +login anonymous +app_info_update 1 +app_info_print 2868840 +quit 2>&1 | tee "$captured"
    local status=${PIPESTATUS[0]}
    set -e
    if [[ $status -ne 0 ]]; then
      echo "SteamCMD 查询 public-beta 失败，退出码：$status" >&2
      exit "$status"
    fi
  fi

  "$PYTHON" "$TOOLS_DIR/latest_beta_guard.py" verify \
    --game-path "$GAME_PATH" \
    --steamcmd-output "$captured" \
    --output "$LATEST_BETA_ATTESTATION" \
    --required-branch public-beta
  echo "已确认当前安装为最新 public-beta：$LATEST_BETA_ATTESTATION"
}

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

verify_latest_beta
run_scan run-1 "$OUTPUT_ROOT/run-1"
echo "第一次审计已完成：$OUTPUT_ROOT/run-1"
if [[ "$SINGLE_RUN" == true ]]; then
  exit 0
fi

printf '请完整启动并退出一次 public-beta 游戏，并确认 Steam 未更新且 Mod 环境未变化，然后按 Enter 继续。\n'
read -r _
verify_latest_beta
run_scan run-2 "$OUTPUT_ROOT/run-2"

dotnet run --project "$PROJECT" --configuration Release -- compare \
  --first "$OUTPUT_ROOT/run-1/audit-report.json" \
  --second "$OUTPUT_ROOT/run-2/audit-report.json" \
  --output "$OUTPUT_ROOT/comparison"

echo "两次独立审计一致：$OUTPUT_ROOT/comparison/audit-comparison.md"

if [[ "$SKIP_REVIEW" != true ]]; then
  "$PYTHON" "$TOOLS_DIR/build_audit_review.py" \
    --report "$OUTPUT_ROOT/run-1/audit-report.json" \
    --comparison "$OUTPUT_ROOT/comparison/audit-comparison.json" \
    --latest-beta-attestation "$LATEST_BETA_ATTESTATION" \
    --output "$OUTPUT_ROOT/review"
  echo "候选审阅表已生成：$OUTPUT_ROOT/review/binding-review.md"
fi

echo "不要提交 $OUTPUT_ROOT；审阅结果在真实游戏验证完成前必须保持 pending_review。"

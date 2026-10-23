#!/usr/bin/env bash
# Unity Phase 1 QA: macOS 빌드 → 자동 플레이 봇으로 실행 → 스크린샷 수집 → 저장 상태 요약
# 사용: tools/unity-qa.sh [에디터 버전]   (기본 6000.0.85f1)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VER="${1:-6000.0.85f1}"
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
OUT="${QA_OUT:-$ROOT/unity/Builds/qa}"
APP="$ROOT/unity/Builds/Mac/MarsBottle.app"
mkdir -p "$OUT" "$ROOT/unity/Logs"

if [ "${SKIP_BUILD:-0}" != "1" ]; then
  echo "== 빌드 ($VER)"
  "$UNITY" -batchmode -nographics -projectPath "$ROOT/unity" -executeMethod BuildScript.BuildMac -quit -logFile "$ROOT/unity/Logs/build.log" || {
    echo "빌드 실패. 로그:"; grep -nE "error CS|Error|Exception|License" "$ROOT/unity/Logs/build.log" | head -40; exit 1; }
  grep -E "Mars build:" "$ROOT/unity/Logs/build.log" || true
fi

echo "== 실행 (자동 플레이, 스크린샷 → $OUT)"
rm -f "$OUT"/shot_*.png
"$APP/Contents/MacOS/MarsBottle" -wipe -autoplay -shot "$OUT" -screen-width 540 -screen-height 1170 -screen-fullscreen 0 -logFile "$OUT/player.log" || true
echo "== 결과"
grep -E "screenshot|QA done|Exception|error" "$OUT/player.log" | cut -c1-400 || true
ls -la "$OUT"/shot_*.png 2>/dev/null || echo "스크린샷 없음"

#!/usr/bin/env bash
# Unity Phase 1 QA: macOS 빌드 → 자동 플레이 봇으로 실행 → 스크린샷 수집 → 저장 상태 요약
# 사용: tools/unity-qa.sh [에디터 버전]   (기본 6000.0.85f1)
#   SKIP_BUILD=1  빌드 생략 / QA_OUT=<dir> 출력 폴더 / QA_ARGS="-rich -shots 2,30,60,120" 플레이어 추가 인자
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
BIN="$(ls "$APP/Contents/MacOS/"* | head -1)"   # 실행 파일명 = 제품명(화성 물병 가게)
rm -f "$OUT/player.log"
# 기본은 숨김 백그라운드 실행: open -g(앱을 활성화하지 않음)·-j(숨긴 채 실행: 화면에 창이 안 뜸)·-n(새 인스턴스, 병렬 QA끼리 안 섞임)·-W(끝날 때까지 대기).
# 스크린샷은 게임 렌더 버퍼에서 찍으므로 창이 숨어 있어도 된다. 소리는 게임이 -shot 인자를 보고 끈다(Bootstrap). QA_FOREGROUND=1이면 예전처럼 앞에서 실행.
if [ "${QA_FOREGROUND:-0}" = "1" ]; then
  # shellcheck disable=SC2086
  "$BIN" -wipe -autoplay -shot "$OUT" ${QA_ARGS:-} -screen-width 540 -screen-height 1170 -screen-fullscreen 0 -logFile "$OUT/player.log" || true
else
  # shellcheck disable=SC2086
  open -g -j -n -W "$APP" --args -wipe -autoplay -shot "$OUT" ${QA_ARGS:-} -screen-width 540 -screen-height 1170 -screen-fullscreen 0 -logFile "$OUT/player.log" || true
fi
echo "== 결과"
grep -E "screenshot|QA done|Exception|error" "$OUT/player.log" | cut -c1-400 || true
ls -la "$OUT"/shot_*.png 2>/dev/null || echo "스크린샷 없음"

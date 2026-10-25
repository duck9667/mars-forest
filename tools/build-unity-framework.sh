#!/usr/bin/env bash
# Unity as a Library용 UnityFramework.framework 만들기 (호스트 앱: 꿈마을 등)
#   1) Unity 배치 빌드(-buildTarget iOS)로 Xcode 프로젝트 생성 → unity/Builds/iOS
#      (Editor/IOSUaaLPostProcess.cs가 Data 폴더를 UnityFramework 타깃 리소스에 추가)
#   2) xcodebuild로 UnityFramework 스킴만 실기기(iphoneos) Release 빌드, 서명 없이
#   3) 결과를 unity/Builds/UnityFramework/UnityFramework.framework 로 복사 (+ 빌드한 커밋 기록)
# 사용: tools/build-unity-framework.sh [에디터 버전]     SKIP_UNITY=1 이면 1)을 건너뛴다
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VER="${1:-$(sed -n 's/^m_EditorVersion: //p' "$ROOT/unity/ProjectSettings/ProjectVersion.txt")}"
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
XC="$ROOT/unity/Builds/iOS"
OUT="$ROOT/unity/Builds/UnityFramework"
mkdir -p "$ROOT/unity/Logs"

if [ "${SKIP_UNITY:-0}" != "1" ]; then
  echo "== Unity iOS 빌드 ($VER)"
  "$UNITY" -batchmode -nographics -buildTarget iOS -projectPath "$ROOT/unity" -executeMethod BuildScript.BuildIOS -quit -logFile "$ROOT/unity/Logs/build-ios.log" || {
    echo "Unity 빌드 실패:"; grep -nE "error CS|Error|Exception" "$ROOT/unity/Logs/build-ios.log" | head -30; exit 1; }
  grep -E "Mars build:|UaaL:" "$ROOT/unity/Logs/build-ios.log" || true
fi

# Data가 UnityFramework 리소스에 들어갔는지 확인 (후처리가 안 돌면 호스트에서 검은 화면)
python3 - "$XC/Unity-iPhone.xcodeproj/project.pbxproj" <<'EOF'
import re, sys
s = open(sys.argv[1]).read()
t = re.search(r'/\* UnityFramework \*/ = \{\s*isa = PBXNativeTarget;.*?buildPhases = \((.*?)\);', s, re.S)
res = [g for g in re.findall(r'(\w{24}) /\* Resources \*/', t.group(1))]
ok = any(re.search(res_id + r' /\* Resources \*/ = \{\s*isa = PBXResourcesBuildPhase;.*?files = \((.*?)\);', s, re.S) and
         'Data in Resources' in re.search(res_id + r' /\* Resources \*/ = \{\s*isa = PBXResourcesBuildPhase;.*?files = \((.*?)\);', s, re.S).group(1) for res_id in res)
print("Data → UnityFramework 리소스:", "OK" if ok else "없음")
sys.exit(0 if ok else 1)
EOF

echo "== xcodebuild UnityFramework (iphoneos Release)"
xcodebuild -project "$XC/Unity-iPhone.xcodeproj" -scheme UnityFramework -configuration Release -sdk iphoneos \
  -derivedDataPath "$XC/DerivedDataFW" CODE_SIGNING_ALLOWED=NO build > "$ROOT/unity/Logs/xcodebuild-fw.log" 2>&1 || {
  echo "xcodebuild 실패:"; grep -E "error:|BUILD FAILED" "$ROOT/unity/Logs/xcodebuild-fw.log" | head -20; exit 1; }
FW="$XC/DerivedDataFW/Build/Products/Release-iphoneos/UnityFramework.framework"
[ -d "$FW/Data" ] || { echo "프레임워크 안에 Data가 없음: $FW"; exit 1; }
rm -rf "$OUT"; mkdir -p "$OUT"
cp -R "$FW" "$OUT/"
git -C "$ROOT" rev-parse --short HEAD > "$OUT/UnityFramework.version"
echo "== 완료: $OUT/UnityFramework.framework ($(du -sh "$OUT/UnityFramework.framework" | cut -f1), mars-forest@$(cat "$OUT/UnityFramework.version"))"

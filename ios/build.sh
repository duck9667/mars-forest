#!/bin/zsh
# 사용:
#   ./build.sh            시뮬레이터용 빌드 → 켜져 있는 시뮬레이터에 설치·실행
#   ./build.sh xcode      Xcode 프로젝트만 생성해서 열기 (실기기 서명·설치는 여기서)
# 요구: Xcode, brew install xcodegen
set -e
cd "$(dirname "$0")"

# 1) three.js 확보 (한 번만 내려받아 ../vendor/에 캐시) → 웹 번들 동기화, importmap을 번들 경로로 치환
THREE_URL=https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js
mkdir -p ../vendor
[[ -s ../vendor/three.module.js ]] || curl -fsSL "$THREE_URL" -o ../vendor/three.module.js
rm -rf App/Web && mkdir -p App/Web/vendor
sed "s#$THREE_URL#./vendor/three.module.js#" ../app/index.html > App/Web/index.html
cp ../vendor/three.module.js App/Web/vendor/three.module.js

# 2) 앱 아이콘: SVG → 1024px PNG (macOS 내장 qlmanage) + 프로젝트 생성
ICON=App/Assets.xcassets/AppIcon.appiconset/icon-1024.png
if [[ ! -s $ICON ]]; then
  TMP=$(mktemp -d); qlmanage -t -s 1024 -o "$TMP" App/icon.svg >/dev/null 2>&1 && cp "$TMP/icon.svg.png" "$ICON"
fi
xcodegen generate >/dev/null

if [[ "$1" == "xcode" ]]; then open MarsForest.xcodeproj; exit 0; fi

# 3) 시뮬레이터 빌드
mkdir -p build
xcodebuild -project MarsForest.xcodeproj -scheme MarsForest -configuration Debug \
  -destination 'generic/platform=iOS Simulator' -derivedDataPath build/dd \
  CODE_SIGNING_ALLOWED=NO -quiet build
APP=build/dd/Build/Products/Debug-iphonesimulator/화성\ 물병\ 가게.app
echo "✓ $APP"

# 4) 켜져 있는 시뮬레이터에 설치·실행 (없으면 건너뜀)
if xcrun simctl list devices booted | grep -q Booted; then
  xcrun simctl install booted "$APP"
  xcrun simctl launch booted com.jakekwak.marsforest >/dev/null
  echo "✓ 시뮬레이터에서 실행 중"
else
  echo "· 시뮬레이터를 켜고 다시 실행하면 자동 설치돼요 (open -a Simulator)"
fi

#!/bin/zsh
# 사용:
#   ./build.sh            시뮬레이터용 빌드 → 켜져 있는 시뮬레이터에 설치·실행
#   ./build.sh xcode      Xcode 프로젝트만 생성해서 열기 (실기기 서명·설치는 여기서)
# 요구: Xcode, brew install xcodegen
set -e
cd "$(dirname "$0")"

# 1) 웹 번들 동기화: 앱용 게임(app/index.html) + three.js (importmap 경로를 번들 기준으로 치환)
rm -rf App/Web && mkdir -p App/Web/vendor
sed 's#\.\./vendor/three\.module\.js#./vendor/three.module.js#' ../app/index.html > App/Web/index.html
cp ../vendor/three.module.js App/Web/vendor/three.module.js

# 2) 프로젝트 생성
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

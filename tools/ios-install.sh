#!/usr/bin/env bash
# 화성 Unity 판 단독 앱을 실기기에 설치. Xcode 계정이 로그인돼 있지 않아도, 받아 둔 프로파일(무료 서명 7일)과 인증서로 직접 서명한다.
#   1) Unity iOS 빌드(-buildTarget iOS) → unity/Builds/iOS          (SKIP_UNITY=1이면 건너뜀)
#   2) xcodebuild 서명 없이(generic iOS, Release)
#   3) 번들 ID에 맞는 프로파일을 찾아 embedded.mobileprovision + codesign(프레임워크 → 앱, 프로파일 entitlements)
#   4) devicectl로 설치
# 사용: tools/ios-install.sh [기기 UDID]   BUNDLE_ID 기본 com.jakekwak.marsforest
# 프로파일이 만료됐으면 Xcode 계정으로 한 번 자동 서명 빌드를 해서 새 프로파일을 받아야 한다(Xcode → 설정 → Accounts).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUNDLE_ID="${BUNDLE_ID:-com.jakekwak.marsforest}"
VER="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/unity/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
UDID="${1:-$(xcrun devicectl list devices 2>/dev/null | awk '/physical/ {for(i=1;i<=NF;i++) if ($i ~ /^[0-9A-F]{8}-[0-9A-F]{16}$/) print $i}' | head -1)}"
[ -n "$UDID" ] || { echo "실기기를 찾지 못했어요"; exit 1; }

# 번들 ID에 맞는 가장 늦게 만료되는 프로파일
PROF=""; LATEST=""
for f in "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"/*.mobileprovision; do
  [ -f "$f" ] || continue
  id=$(security cms -D -i "$f" 2>/dev/null | plutil -extract Entitlements.application-identifier raw -o - - 2>/dev/null || true)
  exp=$(security cms -D -i "$f" 2>/dev/null | plutil -extract ExpirationDate raw -o - - 2>/dev/null || true)
  if [[ "$id" == *".$BUNDLE_ID" ]] && [[ "$exp" > "$LATEST" ]]; then PROF="$f"; LATEST="$exp"; fi
done
[ -n "$PROF" ] || { echo "$BUNDLE_ID 프로파일이 없어요(Xcode 로그인 후 자동 서명 빌드 한 번 필요)"; exit 1; }
[[ "$LATEST" > "$(date -u +%Y-%m-%dT%H:%M:%SZ)" ]] || { echo "프로파일 만료: $LATEST"; exit 1; }
IDENT="$(security find-identity -v -p codesigning | grep -v REVOKED | grep 'Apple Development' | head -1 | awk '{print $2}')"
echo "프로파일 $(basename "$PROF") (만료 $LATEST), 인증서 $IDENT, 기기 $UDID"

if [ "${SKIP_UNITY:-0}" != "1" ]; then
  echo "== Unity iOS 빌드"
  IOS_BUNDLE_ID="$BUNDLE_ID" "$UNITY" -batchmode -nographics -buildTarget iOS -projectPath "$ROOT/unity" -executeMethod BuildScript.BuildIOS -quit -logFile "$ROOT/unity/Logs/build-ios.log"
  git -C "$ROOT" checkout -q -- unity/ProjectSettings/ProjectSettings.asset 2>/dev/null || true   # 빌드가 서명 설정을 다시 쓰는 경우 되돌림
fi
XC="$ROOT/unity/Builds/iOS"
sed -i '' "s/com\.duck9667\.marsbottle/$BUNDLE_ID/g" "$XC/Unity-iPhone.xcodeproj/project.pbxproj"
echo "== xcodebuild(서명 없이)"
xcodebuild -project "$XC/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release -destination 'generic/platform=iOS' \
  -derivedDataPath "$XC/DerivedData" CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" build > "$ROOT/unity/Logs/xcodebuild-ios.log" 2>&1 || {
  grep -E "error:|BUILD FAILED" "$ROOT/unity/Logs/xcodebuild-ios.log" | head -10; exit 1; }
APP="$XC/DerivedData/Build/Products/Release-iphoneos/ProductName.app"
echo "== 서명"
cp "$PROF" "$APP/embedded.mobileprovision"
ENT="$(mktemp)"; security cms -D -i "$PROF" > "$ENT.plist"; /usr/libexec/PlistBuddy -x -c "Print :Entitlements" "$ENT.plist" > "$ENT"
for fw in "$APP"/Frameworks/*; do [ -e "$fw" ] && codesign -f -s "$IDENT" --timestamp=none "$fw"; done
codesign -f -s "$IDENT" --entitlements "$ENT" --timestamp=none "$APP"
codesign --verify --deep --strict "$APP"
echo "== 설치"
xcrun devicectl device install app --device "$UDID" "$APP" | grep -E "installed|bundleID" || true

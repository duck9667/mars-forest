# 화성 물병 가게 — Unity 포팅

웹 앱(`../app/index.html`)을 [`../docs/unity-port-spec.md`](../docs/unity-port-spec.md) 명세대로 C#으로 옮긴다. 수치·루프는 명세(=웹 코드)가 정본이다.

- Phase 1 (10/24): §10.2 최소 플레이어블 — 코어 루프·조이스틱·발판·시트·손님·삐걱·튜토리얼·힌트·연출
- Phase 2 (10/24): ice2 구역(잠금 아치·해제 연출)·뽀삐 러너·얼음 드릴·간판, 목표 12개(상자 보상)·콤보·VIP·러시·낮밤·가로등, Kenney 모델(자체 GLB 파서·팔레트 교체), 명세 대조 리뷰 15건 반영
- 비주얼 패스 (10/24): A 외곽선·ACES·비네트·블룸(PostFX), B 지면·구역 텍스처·먼지(GroundPainter·DustFX), C 치비 캐릭터(CharacterKit), D UI(UIKit·Jua 폰트·아이콘)
- Phase 3 (10/24): 먼지 토끼 우리(토끼 3·팁 적립·수거), 휴게 돔(유리 돔·분수·화단·에어록·좌석 6, 손님 착석·팁), 장식 6종+시트 탭(업그레이드·장식·목표)+이주 카드, 프레스티지 "새 기지로 이주", 테라포밍(이끼 타일·나무·땅색), 부재 중 수익 모달, 명세 대조 리뷰 11건 반영
- 꿈마을 통합(10/24): Unity as a Library로 꿈마을 화성 탭에 임베드(`tools/build-unity-framework.sh`)

## 구조

씬 파일에 의존하지 않는다. `Assets/Scenes/Main.unity`는 빈 씬이고, `Bootstrap`이 `RuntimeInitializeOnLoadMethod`로 모든 시스템을 런타임에 조립한다(원본 단일 파일 구조와 같은 감각). 건물·바위·착륙선은 Kenney GLB, 캐릭터·울타리·카운터는 프리미티브 조립, 지면은 코드로 그린 텍스처.

| 스크립트 | 역할 (명세 §) |
|---|---|
| `GameConfig` · `WorldMap` | 상수·발판 정의·ORDER·좌표(논리 400×1040 → `W2U`, k=0.05) (§0~4) |
| `SaveData` · `GameState` | 저장(JSON, `persistentDataPath/save.json`)·파생값·이벤트 (§8) |
| `Player` · `FloatingJoystick` | 이동(서브스텝 충돌)·걷기 애니·등 뒤 블록 탑(`Items.CarryBlock`, 스프링 흔들림, 가득일 때만 MAX)·§1.1 상호작용 순서 (§2). 두 번째 손가락이 닿으면 조이스틱 취소(핀치 우선) |
| `Stations` | 얼음 더미 8 · 해빙기(얼음을 녹여 물로) · 병입기 · 카운터(선반) · 계산대(지폐) (§1) |
| `PadManager` | 발판 노출 규칙·정지 납입·완료·시트 일시불 (§3) |
| `ZoneManager` | 구역 바닥·울타리(둘레 순서 부품), 잠긴 구역 윤곽·자물쇠 아치, 해제 연출 (§4.3). 열린 구역 울타리는 `WorldMap.FenceBlocked`로 실제로 막힘(게이트 틈만 통과, ice·ice2 E변 게이트 추가) |
| `CustomerSystem` · `CashierBot` | 손님 줄·결제·인내·VIP·콤보·러시, 삐걱 자동 결제 (§1.3·§5) |
| `RunnerSystem` · `DrillStation` · `AdsSign` | 뽀삐 상태기계(적재→하역), 드릴 2초마다 얼음 2장, 간판 입간판 (§5.1) |
| `QuestManager` | 목표 12개 순차, 업그레이드 시트 목표 탭에서만 표시(상단 칩 없음), 달성 즉시 보상·저장, qskip (§6.1) |
| `SettleStations` · `MachineKit` | 생존 기지 생산 체인(레골리스 더미·전기분해기·토양 정화기·비료 배합기·온실, docs/settlement-design.md), 기계 공통 문법(받침판→투입부→움직이는 가공부→산출 받침대, MachineAnim) |
| `Frontier` | 2장 개척지(docs/chapter2-design.md D): 거주 구역 돔 주택 8채(정착민 3명 중 1명 입주 → S.pop, 상한 32, 창 불·도착 간격 −최대 20%·하루 세금), 태양광 단지(패널 낱장 기울기·배터리 막대, 낮·배터리 동안 기계 속도 ×1.2 → `GameState.MachineSpeedMul`). 검증 인자 `-pop N`·`-batt f` |
| `SettlementManager` · `MapScreen` | 정착 목표 10단계 두 장: 1장 생존(물 확보→숨 쉴 공기→흙 살리기→첫 수확→생존 완료), 2장 마을(전력 확보→첫 이웃→기지 정비→자급 마을→화성 마을 선포, 엔딩 무전)·보상, 지도 화면(구역 상태·시설·플레이어·지금 장 5단계 진행 막대·지난 장 요약), HUD "1장 n/5"→"2장 n/5" 알약. 검증 인자 `-stage N`(0~10)·`-power 초`·`-mall` |
| `PenSystem` · `DomeSystem` | 먼지 토끼 우리(토끼·소품·팁 적립·수거), 휴게 돔(조립·좌석 예약·경로·팁·수거) (§5.1·§5.2) |
| `DecoManager` · `ShopDecor` | 장식 6종 구매·매장 시각물(차양·네온·선인장·줄전구·깃발·체크 바닥) (§3.4) |
| `PrestigeManager` · `TerraformManager` · `OfflineEarnings` | 이주(초기화·보너스·로켓 연출·Bootstrap.Restart), 테라포밍(terra·45 슬롯·점등), 부재 수익 모달 (§5.4·§6.3·§6.4) |
| `StationDress` · `WorldDress` · `ModelLib` | Kenney GLB 모델 배치·팔레트 교체, 자체 GLB 파서 (§9) |
| `PostFX` · `HullOutline` · `Resources/Shaders` | 포스트 외곽선(깊이 2차 미분)·ACES·비네트·블룸·그림자 품질, VIP용 hull 외곽선 헬퍼 (§7.2) |
| `GroundPainter` · `DustFX` | 코드로 그린 지면 텍스처(잔무늬 최소·넓은 명암·기지 밖 비네트)·구역 바닥·잠긴 구역 점선, 가장자리 먼지 36개·약한 돌풍·회오리 1개 (§7.4). 맵 바깥은 `WorldDress`의 지층 메사 16덩어리 |
| `CharacterKit` | 원본 bPerson/bTraveler/bRobot 치비 조립(플레이어·손님·삐걱·뽀삐+손수레) |
| `UIKit` · `Resources/Fonts/Jua-Regular.ttf` | 9-slice 베벨 패널·아이콘 34종·말풍선·버튼·핍, Jua(OFL) 글꼴 |
| `HUD` · `UpgradeSheet` · `WorldLabels` | 벨·N일째·톱니(소리/진동 토글)·콤보/러시 배지·업그레이드 버튼·시트, 월드→스크린 라벨(말풍선은 맨 앞 손님만) |
| `TutorialManager` · `HintSystem` | 말풍선 3단계·손 포인터, 바닥 화살표·타깃 링 (§6.2) |
| `StoryManager` | 서사(docs/chapter2-design.md A): 관제사 하나의 새 게임 인트로 3컷(탭으로 넘김·건너뛰기, 끝나야 튜토리얼 시작)과 사건 무전 카드(HUD 아래 작은 카드, 3~4초 뒤 접힘·탭하면 닫힘, 본 id는 `S.story`, 시트·지도·배너 중엔 대기). 대본은 파일 안 `INTRO`·`LINES` 표 |
| `MarsEvents` | 화성 이벤트(튜토리얼 끝·판매 20 이후, 하루 0~1번, 같은 이벤트 연속 금지): 먼지 폭풍 40~60초(화면 살짝 탁하게·지면 먼지 띠, 태양광 정지 `Frontier`, 이동 −15% `GameState.Speed`), 보급선(착륙장 캡슐, 닿으면 벨+물통 등짐 또는 레골리스 → 정화기, 60초), 얼음 운석(북쪽 구역 빈 땅, 닿으면 얼음 16장 → 해빙기 또는 벨, 90초). 위치는 힌트 화살표만, 무전은 `StoryManager.Push(id, delay, repeat)`. 저장은 다음 판정 `S.evNext`만. 검증 인자 `-event storm\|supply\|meteor [초]` |
| `ArcFlyer` · `JuiceFX` · `CameraRig` · `AudioManager` | 아크 비행·스쿼시·팝·링·동전·흔들림, 직교 추적 카메라(핀치·휠 확대 1~2.2배, 저장), 합성 효과음, `Haptics`(iOS `MarsHaptics.mm`, 설정에서 끔) (§7) |
| `DevTools` | `-wipe` 저장 초기화, `-autoplay` 힌트 추종 봇(격자 경로·5초마다 diag 로그·좌석 팁 수거), `-shot <dir>` 스크린샷 후 종료(소리 꺼짐), `-shots 2,10,…`, `-rich`/`-bells N`, `-pads id=lv,…`, `-sold N`, `-tut N`, `-shelf N`, `-start x,y`, `-save 이름`(저장 파일 분리), F5 초기화, F6 +500벨. 기능별: `-pentip N`·`-pent S`(우리), `-decos all`·`-decobuy id,…`·`-clock N`(장식), `-sheettab upgrade|deco|quest`(시트), `-offline 초`(부재 수익), `-prestigeAt 초`(이주), `-zoom f`(카메라 확대), `-settings s`(설정 창 열기), `-carry N`(등짐 N개 채우기), `-walk dx,dy`(봇 대신 한 방향으로 밀기, 0,0이면 제자리), `-shelfo2 N`·`-shelffood N`(카운터 칸 채우기), `-map [초]`·`-stage N`(0~10)·`-sup 물,산소,식량[,비료[,정착민]]`(정착 목표), `-fx mask=0`(외곽선 마스크 끄기), `-story id`(그 무전 바로 띄우기)·`-intro`(인트로 강제). 페이싱: `-timescale N`(최대 8배 빨리 감기, 한 프레임 게임 dt 1/30초 고정이라 로직은 60fps 때와 같음)·`-quitstage N`(정착 단계 N 달성 뒤 종료), 로그 `pace pad`·`pace stage`(게임 초·벨·수입 출처)·`pace min`(분마다 벨·수입)(docs/pacing-report.md). `-shots`는 소수 시점도 받는다(1/100초 이름). 검증 인자는 실행당 1회(Restart 뒤 유지) |
| `Editor/ProjectSetup` · `Editor/BuildScript` | 재질 에셋(셰이더 포함용)·빈 씬·플레이어 설정, 배치 빌드 |

## 빌드·QA

```bash
# macOS 창 빌드 (Unity 6000.0.x, 라이선스 활성화 필요)
"/Applications/Unity/Hub/Editor/6000.0.85f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -projectPath unity -executeMethod BuildScript.BuildMac -quit -logFile unity/Logs/build.log

# 자동 플레이 + 스크린샷(2·10·25·45·70·100초). 게임 창은 숨긴 채(open -g -j) 무음으로 돈다. QA_FOREGROUND=1이면 앞에서
./tools/unity-qa.sh
# 자금 2000으로 150초 (구역 해제·뽀삐·드릴까지 확인)
QA_ARGS="-rich -shots 2,8,20,40,60,90,120,150" ./tools/unity-qa.sh
```

iOS:
```bash
# 단독 앱(Xcode 프로젝트 unity/Builds/iOS) — BuildScript.BuildIOS
# 꿈마을 화성 탭용 UnityFramework.framework(Unity as a Library) — Data를 프레임워크에 넣는 후처리(Editor/IOSUaaLPostProcess.cs) 포함
tools/build-unity-framework.sh     # → unity/Builds/UnityFramework/UnityFramework.framework (꿈마을은 tools/sync_mars_unity.sh로 받아감)
# 단독 앱 실기기 설치(Xcode 계정 로그인 없이도, 받아 둔 프로파일·인증서로 직접 서명)
tools/ios-install.sh               # SKIP_UNITY=1이면 Unity 빌드 건너뜀
```

빌드가 `Connectivity with IL Post Processor runner cannot be established yet`에서 멈추면: ILPP 러너가 ~/Documents 감시(FSEventStreamCreate)에서 걸려 /tmp 소켓을 못 만든 것(10/25 다듬기 3차에서 반복 재현). 프로젝트를 ~/Documents 밖으로 rsync해 빌드한 뒤 `Builds/Mac`만 되돌려 복사하면 된다.

페이싱 목표(원본 README): 첫 판매 0:30, 첫 발판 0:50, 첫 구역 확장 2:30 전후. 봇은 사람보다 효율적이라 실플레이는 더 느리다.

## 원본과 다르게 둔 것

- 외곽선은 메시별 hull 대신 카메라 포스트(깊이 2차 미분) 한 번: 맞닿은 같은 높이 물체 사이엔 선이 없음. VIP 금색은 `HullOutline` 헬퍼(아직 미호출)
- 조명 강도 태양 1.15·반구광 0.55는 원본 물리 조명(1.9/π·1.05/π)×ACES 노출의 등가값이라 되돌리지 않음. PostFX exposure 0.85
- 효과음은 런타임 합성(원본 WebAudio 신스를 흉내), 햅틱은 `Handheld.Vibrate`
- 모델은 glTFast 대신 자체 GLB 파서(`ModelLib`), 팔레트는 머티리얼 baseColorFactor 교체
- 테라포밍 땅색: 원본은 흰 재질에 b3683e를 곱하지만 우리 지면은 이미 주황 텍스처라 연초록(d2ffb0)을 terra×0.45로 은은하게 곱함. TERRA_SLOTS는 원본 후보 39개를 우리 레이아웃에 맞춰 옮기고 보충해 45개
- 프레스티지 하늘색 이동은 카메라 배경색만(구도상 거의 안 보임), qc 통계(earned·penGot 등)는 없음

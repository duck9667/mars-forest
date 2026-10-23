# 화성 물병 가게 — Unity 포팅

웹 앱(`../app/index.html`)을 [`../docs/unity-port-spec.md`](../docs/unity-port-spec.md) 명세대로 C#으로 옮긴다. 수치·루프는 명세(=웹 코드)가 정본이다.

- Phase 1 (10/24): §10.2 최소 플레이어블 — 코어 루프·조이스틱·발판·시트·손님·삐걱·튜토리얼·힌트·연출
- Phase 2 (10/24~): ice2 구역(잠금 아치·해제 연출)·뽀삐 러너·얼음 드릴·간판, 목표 12개(상자 보상)·콤보·VIP·러시, Kenney 모델(glTFast·팔레트 교체)
- Phase 3 (예정): 먼지 토끼 우리·휴게 돔(좌석·팁)·장식·낮밤·테라포밍·프레스티지·부재 수익·외곽선·iOS 빌드

## 구조

씬 파일에 의존하지 않는다. `Assets/Scenes/Main.unity`는 빈 씬이고, `Bootstrap`이 `RuntimeInitializeOnLoadMethod`로 모든 시스템을 런타임에 조립한다(원본 단일 파일 구조와 같은 감각). 모델은 아직 박스 플레이스홀더.

| 스크립트 | 역할 (명세 §) |
|---|---|
| `GameConfig` · `WorldMap` | 상수·발판 정의·ORDER·좌표(논리 400×1040 → `W2U`, k=0.05) (§0~4) |
| `SaveData` · `GameState` | 저장(JSON, `persistentDataPath/save.json`)·파생값·이벤트 (§8) |
| `Player` · `FloatingJoystick` | 이동·충돌·걷기 애니·등짐 스택 흔들림·§1.1 상호작용 순서 (§2) |
| `Stations` | 얼음 더미 8 · 제빙기 · 병입기 · 카운터(선반) · 계산대(지폐) (§1) |
| `PadManager` | 발판 노출 규칙·정지 납입·완료·시트 일시불 (§3) |
| `ZoneManager` | 구역 바닥·울타리(둘레 순서 부품), 잠긴 구역 윤곽·자물쇠 아치, 해제 연출 (§4.3) |
| `CustomerSystem` · `CashierBot` | 손님 줄·결제·인내·VIP·콤보·러시, 삐걱 자동 결제 (§1.3·§5) |
| `RunnerSystem` · `DrillStation` · `AdsSign` | 뽀삐 상태기계(적재→하역), 드릴 2초마다 얼음 2장, 간판 입간판 (§5.1) |
| `QuestManager` | 목표 12개 순차, HUD 칩, 상자 팝·보상 (§6.1) |
| `StationDress` · `WorldDress` · `ModelLib` | Kenney GLB 모델 배치·팔레트 교체 (§9) |
| `HUD` · `UpgradeSheet` · `WorldLabels` | 벨·업그레이드 버튼·시트, 월드→스크린 라벨 |
| `TutorialManager` · `HintSystem` | 말풍선 3단계·손 포인터, 바닥 화살표·타깃 링 (§6.2) |
| `ArcFlyer` · `JuiceFX` · `CameraRig` · `AudioManager` | 아크 비행·스쿼시·팝·링·동전·흔들림, 직교 추적 카메라, 합성 효과음 (§7) |
| `DevTools` | `-wipe` 저장 초기화, `-autoplay` 힌트 추종 봇(5초마다 diag 로그), `-shot <dir>` 스크린샷 후 종료, `-shots 2,10,…` 시점 목록, `-rich` 시작 벨 2000, F5 초기화, F6 +500벨 |
| `Editor/ProjectSetup` · `Editor/BuildScript` | 재질 에셋(셰이더 포함용)·빈 씬·플레이어 설정, 배치 빌드 |

## 빌드·QA

```bash
# macOS 창 빌드 (Unity 6000.0.x, 라이선스 활성화 필요)
"/Applications/Unity/Hub/Editor/6000.0.85f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -projectPath unity -executeMethod BuildScript.BuildMac -quit -logFile unity/Logs/build.log

# 자동 플레이 + 스크린샷(2·10·25·45·70·100초)
./tools/unity-qa.sh
# 자금 2000으로 150초 (구역 해제·뽀삐·드릴까지 확인)
QA_ARGS="-rich -shots 2,8,20,40,60,90,120,150" ./tools/unity-qa.sh
```

페이싱 목표(원본 README): 첫 판매 0:30, 첫 발판 0:50, 첫 구역 확장 2:30 전후. 봇은 사람보다 효율적이라 실플레이는 더 느리다.

## 원본과 다르게 둔 것

- 바닥은 흙색 단색(원본은 흰 재질 + 캔버스 텍스처), 외곽선·비네트·낮밤은 아직 없음
- 조명 강도: 톤매핑이 없어 태양 1.15·반구광 0.55배 (원본 1.9·1.05)
- 효과음은 런타임 합성(원본 WebAudio 신스를 흉내), 햅틱은 `Handheld.Vibrate`
- 잠긴 구역 윤곽은 점선 대신 어두운 반투명 평면

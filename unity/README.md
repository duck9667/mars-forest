# 화성 물병 가게 — Unity 포팅 (Phase 1)

웹 앱(`../app/index.html`)을 [`../docs/unity-port-spec.md`](../docs/unity-port-spec.md) 명세대로 C#으로 옮긴다. 수치·루프는 명세(=웹 코드)가 정본이고, 이 폴더는 §10.2 **Phase 1 최소 플레이어블** 범위다.

## 구조

씬 파일에 의존하지 않는다. `Assets/Scenes/Main.unity`는 빈 씬이고, `Bootstrap`이 `RuntimeInitializeOnLoadMethod`로 모든 시스템을 런타임에 조립한다(원본 단일 파일 구조와 같은 감각). 모델은 아직 박스 플레이스홀더.

| 스크립트 | 역할 (명세 §) |
|---|---|
| `GameConfig` · `WorldMap` | 상수·발판 정의·ORDER·좌표(논리 400×1040 → `W2U`, k=0.05) (§0~4) |
| `SaveData` · `GameState` | 저장(JSON, `persistentDataPath/save.json`)·파생값·이벤트 (§8) |
| `Player` · `FloatingJoystick` | 이동·충돌·걷기 애니·등짐 스택 흔들림·§1.1 상호작용 순서 (§2) |
| `Stations` | 얼음 더미 8 · 제빙기 · 병입기 · 카운터(선반) · 계산대(지폐) (§1) |
| `PadManager` | 발판 노출 규칙·정지 납입·완료·시트 일시불 (§3) |
| `CustomerSystem` · `CashierBot` | 손님 줄·결제·인내, 삐걱 자동 결제 (§5) |
| `HUD` · `UpgradeSheet` · `WorldLabels` | 벨·업그레이드 버튼·시트, 월드→스크린 라벨 |
| `TutorialManager` · `HintSystem` | 말풍선 3단계·손 포인터, 바닥 화살표·타깃 링 (§6.2) |
| `ArcFlyer` · `JuiceFX` · `CameraRig` · `AudioManager` | 아크 비행·스쿼시·팝·링·동전·흔들림, 직교 추적 카메라, 합성 효과음 (§7) |
| `DevTools` | `-wipe` 저장 초기화, `-autoplay` 힌트 추종 봇, `-shot <dir>` 스크린샷 시퀀스 후 종료, F5 초기화, F6 +500벨 |
| `Editor/ProjectSetup` · `Editor/BuildScript` | 재질 에셋(셰이더 포함용)·빈 씬·플레이어 설정, 배치 빌드 |

## 빌드·QA

```bash
# macOS 창 빌드 (Unity 6000.0.x, 라이선스 활성화 필요)
"/Applications/Unity/Hub/Editor/6000.0.85f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -projectPath unity -executeMethod BuildScript.BuildMac -quit -logFile unity/Logs/build.log

# 자동 플레이 + 스크린샷(2·10·25·45·70·100초)
./tools/unity-qa.sh
```

Phase 1 페이싱 목표(원본 README): 첫 판매 0:30, 첫 발판 0:50.

## Phase 1에서 원본과 다르게 둔 것

- `melter`·`shelf` 해금 조건: 원본 `ice2≥1` → `bottler≥1` (ice2 구역은 Phase 2)
- 바닥은 흙색 단색(원본은 흰 재질 + 캔버스 텍스처), 외곽선·비네트·낮밤 없음
- 효과음은 런타임 합성(원본 WebAudio 신스를 흉내), 햅틱은 `Handheld.Vibrate`

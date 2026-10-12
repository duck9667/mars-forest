# 화성의 숲

작은 붉은 행성을 초록으로 물들이는 느긋한 콜로니 게임. 피자레디식 허브: 광장 한 화면에서 얼음을 캐고, 제빙기·병입기로 물병을 만들어 카운터에 올리면 여행자 손님이 사 간다. 번 벨로 발판을 밟아 업그레이드·고용·구역 해제. 지는 조건은 없다.

- 단일 파일 `index.html` (바닐라 JS + three.js, `vendor/`에 동봉 → 오프라인 동작)
- 진행은 localStorage에 자동 저장

## 조작

| | 데스크톱 | 폰(터치) |
|---|---|---|
| 이동 | 방향키·WASD 또는 땅 클릭 | 화면 아무 데나 드래그(플로팅 조이스틱) 또는 땅 탭 |
| 행동 | Space 또는 대상 클릭 | 대상 탭 또는 행동 버튼 · 얼음·돌은 옆을 지나면 자동 채집 |
| 카메라 | 드래그로 둘러보기, 휠로 확대·축소 | 두 손가락 핀치 |
| 탈것 | E | 탈것 탭(타기) · 행동 버튼(내리기) |

폰에서는 HUD가 세로 화면용으로 줄고, 목표 패널은 한 줄로 접힌다(제목 탭으로 펼침). 소리·초기화는 ⚙ 설정.

## iOS 앱

`ios/`에 WKWebView 래퍼. 게임 파일을 그대로 번들에 넣고 `mars://app/` 커스텀 스킴으로 서빙한다(ES 모듈·localStorage 안정). 세로 고정, 상태바 숨김, 세이프 에어리어는 게임 CSS가 처리.

```bash
cd ios
./build.sh          # 시뮬레이터 빌드 → 켜져 있는 시뮬레이터에 설치·실행
./build.sh xcode    # Xcode 프로젝트 생성·열기 (실기기 서명·설치)
```

요구: Xcode, `brew install xcodegen`. 구조는 hanjul·menubar-pets와 같다(`project.yml` + `build.sh`). `App/Web/`은 build.sh가 매번 `../index.html`과 `../vendor/`에서 복사하므로 손대지 않는다.

```
ios/project.yml              xcodegen (타겟 1개, iOS 16+, 세로 전용)
ios/App/AppDelegate.swift    WKWebView + 커스텀 스킴 핸들러
ios/App/Info.plist
ios/App/Assets.xcassets      앱 아이콘
ios/build.sh
```

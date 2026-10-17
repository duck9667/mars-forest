# tools/qa — 화성 물병 가게 자동 검증

헤드리스 크로미움(iPhone 390×844 에뮬레이션)으로 `app/index.html`을 열어 기능을 확인하고 스크린샷을 찍는다. 저장소 루트를 `localhost:8765`로 서빙하는 서버가 없으면 스크립트가 직접 띄운다.

```bash
cd tools/qa && npm install && npx playwright install chromium   # 한 번만
npm run check    # checks.mjs 17항목 + checks2.mjs 13항목 → PASS/FAIL 출력 (각 1~3분)
npm run bot      # 자동 플레이 150초: 판매·발판 구매 루프, 손님 이탈 0·콘솔 에러 0 이어야 함
npm run shots    # 시작·중반·휴게 돔 스크린샷 → out/*.png
node shot.mjs out/x.png setups/top.js 2500   # 임의 상태: setups/*.js 는 페이지 안에서 실행되는 한 조각 JS
```

- `checks.mjs`: 옛 저장 마이그레이션, 게이트 해제 팝, 시트 구매, 부재 중 수익, 저장 왕복, 두 번 탭 리셋, 콘솔 에러.
- `checks2.mjs`: 목표 달성·상자, 콤보, VIP 결제, 장식, 낮밤, 러시, 새 필드 저장 왕복, 시트 탭, 이주.
- `bot.mjs <out.png> [초] [steps]`: `dbg.steps`로 게임 시간을 빨리 돌리며 매 스텝 `dbg.hook`으로 조향한다. 15초마다 상태 한 줄.
- 소프트웨어 WebGL 이라 느리다(1~2 fps). 타이밍 검사는 게임 시간(`dbg.steps`) 기준이라 괜찮다.
- 검사에 쓰는 디버그 API: `window.S`(저장 상태), `dbg.save/restoreBots/renderer/steps/hook`, `QUESTS`, `custs`, `bots`. `app/index.html` 끝부분의 `window.*` 노출을 지우면 검사가 깨진다.
- 결과물 `out/`은 git에 넣지 않는다.

# 화성 물병 가게 — Unity(C#) 포팅 명세

원본: `mars-forest/app/index.html` (three.js 0.160, 단일 파일, 세로 전용). 모든 수치는 원본 코드의 상수·식을 그대로 옮겼다(변수명은 `코드체`). README와 코드가 다르면 **코드가 정본**(차이는 §11).

---

## 0. 좌표계·단위 변환

| 항목 | 원본 | Unity 권장 |
|---|---|---|
| 논리 월드 | `W=400`, `H=1040` (x 0..400, y 0..1040, **y↑ = 화면 아래**) | 데이터는 논리 좌표 그대로 보관, 변환 함수 하나로 배치 |
| 3D 변환 | `W2T(x,y,h) = (x-200, h, y-520)` (three 오른손) | `Vector3((x-200)*k, h*k, -(y-520)*k)`, `k=0.05` 권장(논리 20 = 1m). z 부호 반전 = 왼손 좌표 보정 |
| 속도·반경·높이 | 논리 px/초 | 전부 `×k` |
| 시간 | 초 (`dt` 최대 0.05로 클램프) | `Time.deltaTime` 동일 클램프 |
| 고정 난수 | `seed=11`, Park–Miller `rnd()` (바위·자갈·타일 배치) | 같은 LCG를 C#으로 옮겨 배치 재현 |

이하 거리·속도·좌표는 모두 **논리 단위**.

---

## 1. 코어 루프

```
얼음밭 더미 ─(r40, 0.09s/장)→ 등짐(ice) ─(제빙기 r40, 0.06s/장)→ meltQ
meltQ ─(meltTime마다 1장)→ water += 10
water ≥10 & bottles<24 ─(bottleTime)→ water −= 10, bottles++ (트레이)
트레이 ─(병입기 r40, 0.13s/장)→ 등짐(bottle) ─(카운터 r48 & 카운터 북쪽, 0.11s/장)→ shelf
줄 맨 앞 손님 & shelf≥want & (삐걱 or 플레이어가 계산대 r70) ─0.35s→ 결제 → S.cash (계산대 지폐 더미)
지폐 더미 ─(계산대 r54)→ 벨(S.bells) → 발판/시트에서 소비
```
얼음 1장 = 물 10 = 병 1개(병 1개 = 손님 want 1).

### 1.1 근접 판정
- 스테이션: `nearStn(k,r)` = `|px-sx| < w/2+r && |py-sy| < h/2+r` (AABB 확장).
- 얼음 더미·좌석·토끼 팁·발판: 원형 `hypot < r`.
- `interact()`는 매 프레임 실행, `actT` 쿨다운 동안 대기. 아래 표 **순서대로** 첫 번째로 성립한 행동 1개만 수행.

| 순서 | 행동 | 조건 | 반경 | 쿨다운 `actT` |
|---|---|---|---|---|
| 1 | 얼음 줍기 | `carry+pend < carryMax()`, 등짐이 비었거나 ice, 열린 더미(`S.spots[i]<=0 && spotOpen(i)`) | 40 (원) | 0.09 |
| 2 | 제빙기에 넣기 | 등짐 맨 위가 ice | `nearStn('melter',40)` | 0.06 (1장씩 pop) |
| 3 | 병 받기 | `S.bottles>0`, 용량 여유, 등짐 비었거나 bottle | `nearStn('bottler',40)` | 0.13 |
| 4 | 선반 올리기 | 맨 위 bottle, `S.shelf<shelfCap()`, **`p.y < counter.y`**(카운터 뒤쪽) | `nearStn('counter',48)` | 0.11 |
| 5 | 지폐 수거 | `S.cash>0`, 수거 연출 중 아님(`cashPile==0`) | `nearStn('register',54)` | 0.25 |
| 6 | 토끼 벨 수거 | `S.penTip>0` | `PEN_TIP` 40 (원) | 0.2 |
| (별도) | 좌석 팁 수거 | `seat.tip>0` | 좌석 34 (원) | 없음 (`tickCustomers`) |
| (별도) | 발판 납입 | **정지 상태**(`walking==false`), 발판 30 이내 | 30 | 프레임 단위 |

- 종류 혼합 금지: ice와 bottle은 동시에 들 수 없음. 날아오는 중인 아이템 `pend`/`pendK`도 같은 종류여야 함.
- 제빙기 투입은 **용량 체크 없음**(`MELT_CAP=40`은 드릴에만 적용). 큐 시각화는 최대 10장(2열).

### 1.2 용량·생산 상수

| 항목 | 식 / 값 | Lv별 값 |
|---|---|---|
| 등짐 `carryMax()` | `CARRY[min(6,hand)] + 3*min(2,prestige)` / `CARRY=[6,9,12,16,20,26,32]` | 6,9,12,16,20,26,32 (+프레스티지 최대 +6 → 38) |
| 스택 메시 상한 | `STACK.ice.max=40`, `bottle.max=40`, `bill.max=24`, `coin.max=8` | |
| 선반 `shelfCap()` | `8 + shelf*7` (레이아웃 12열×3단, 간격 x15·z12) | 8,15,22,29,36 |
| 병입기 트레이 | `BOTTLE_CAP=24` (6열×4단, 간격 10) | 고정 |
| 제빙 큐 | 무제한(드릴만 `MELT_CAP=40`) | |
| 제빙 시간 `meltTime()` | `1.6/(1+melter*0.35)` 초/장 | 1.600, 1.185, 0.941, 0.780, 0.667, 0.582, 0.516 |
| 병입 시간 `bottleTime()` | `1.6/(1+bottler*0.35)` 초/병 | 동일 |
| 병 가격 `bottlePrice()` | `round((12 + price*6) * 1.5^prestige)` | 12,18,24,30,36,42 (×1.5ⁿ) |
| 얼음 더미 리스폰 | 주운 직후 `S.spots[i] = 6 + rand*3` 초 | |
| 얼음 더미 수 | `ICE_SPOTS` 12개, `BASE_SPOTS=8` (ice2 해제 시 +4: 인덱스 8~11) | |
| 지폐 더미 장수 | `min(24, ceil(cash/12))` (3열×8단, `billLayout`) | |
| 수거 연출 | 더미를 윗장부터 40ms 간격으로 플레이어에게 날림(0.22s) | |

### 1.3 결제·수익

| 항목 | 값 |
|---|---|
| 결제 조건 | 줄 0번(`qi==0`), 상태 `queue`, `S.shelf >= want`, `cashier 로봇 존재 || nearStn('register',70)` |
| 결제 시간 | `c.pay` 누적 0.35s (말풍선 금색 링 `--p` 진행) |
| 기본 수익 | `bottlePrice()*want` |
| VIP | `×3` + 팁 `bottlePrice()*2` |
| 콤보 | 직전 판매 후 2초 이내면 `combo++`, 아니면 1. `combo>=3`이면 `earn*(1+0.1*min(combo,5))` (최대 +50%) |
| 지급처 | `S.cash`(계산대 더미) → 플레이어가 수거 시 `addBells` |
| 큰 판매 연출 | VIP or `want>=3` or `combo>=3`: 색종이 18(VIP 36), 흔들림 2(VIP 4) |
| 큰 수거 | `n >= price*8`: 동전 팝 12개 + 흔들림 3. 플로팅 텍스트 big은 `n>=price*4` |

---

## 2. 플레이어

| 항목 | 값 |
|---|---|
| 시작 위치 | `p={x:200,y:450,dir:[0,1]}` (매장 안) |
| 이동 속도 | `(165 + 18*spd) * |입력|` 논리/초 → 165,183,201,219,237,255 |
| 입력 | 조이스틱 벡터는 **아날로그**(크기 0~1), 키보드 WASD/화살표는 정규화 |
| 충돌 | 축 분리 슬라이드: x, y 각각 `blocked()` 검사 후 이동 |
| `blocked(x,y)` | 경계 `x<14 || x>386 || y<30 || y>1024`; 각 `STN` AABB `+10(x)/+8(y)` (drill은 설치 후만); 돔 열림 시 좌석 r16, 분수 `(DOME.x, DOME.y+30)` r17, 화단 2곳 `(DOME.x±58, DOME.y+22)` ±20, 에어록 `(DOME.x-55, DOME.y-DOME.r-4)` 18×11; 드럼통 영역 `x>338 && 358<y<404`; 착륙선 r34. **울타리는 충돌 없음**(넘어다님) |
| 발걸음 먼지 | 걷는 중 0.14s마다 `puff` (풀 14개, 0.5s 수명) |
| 회전 | `turnTo` 보간 `dt*14`, yaw = `atan2(dir.x, dir.y)` |
| 걷기 애니 | 몸 높이 `|sin(t/90)|*2.5`, 팔다리 스윙 `sin(t/90)`(팔 ×0.7), 앞기울기 `rot.x→0.14` (`dt*8`), 롤 `sin(t/90)*0.05`; 정지 시 호흡 `sin(t/600)*0.8` |

### 2.1 플로팅 조이스틱
| 항목 | 값 |
|---|---|
| 생성 | 캔버스 아무 곳 `pointerdown` 위치에 베이스 표시 (멀티터치 중 첫 포인터만) |
| 베이스/노브 | 지름 120px / 50px |
| 최대 반경 | 45px (넘으면 클램프) |
| 데드존 | 6px 미만이면 `null` |
| 출력 | `[dx/45, dy/45]` (화면 아래 = +y = 월드 +y) |
| 종료 | `pointerup/cancel` 시 숨김. 시트 열 때 강제 해제 |

### 2.2 등짐 스택(피자레디식)
| 항목 | 값 |
|---|---|
| 부착점 | 캐릭터 로컬 `(0,30,-17)`, `rot.x=-0.1` (등 뒤) |
| 아이템 메시 | 납작 판 `slabGeo` 14×2.4×10 (옆면 림 텍스처 + 윗면 재질 2그룹) |
| 아이템 높이 `ITEM_H` | ice 2.8, bottle 3.0, bill 3.2, coin 1.8 |
| 쌓기 | i번째 = `y += ITEM_H[k]` (한 기둥) |
| 흔들림 `stackSway` | 로컬 속도 `lv`(전방·측방) ×0.06 → 목표 `clamp(-v*0.9, ±9)`, `lerp(dt*9)`; i번째(`kk=min(i,18)`) 위치 `+sw*kk*0.11`, 회전 `x=-swz*0.007*kk`, `z=swx*0.007*kk` |
| 줍기 연출 | 아크 비행 0.3s → 도착 시 push, 스쿼시 0.9, 음 높이 `520+380*(n/max)`, 3장마다 진동 6ms, 가득 차는 순간 딩 1회 |
| 개수 칩 | 탑 꼭대기 `max(72, 34+n*ITEM_H+14)` 높이에 아이콘+개수, 가득이면 빨간 테두리 |

---

## 3. 업그레이드(발판) — `PADS` 15개

`cost(l)` = 현재 레벨 l에서 다음 레벨 비용. `menu:true` = **시트 전용(월드 발판 없음)**. `zone` = 구역 발판(게이트 아치에 가격표).

| id | 이름 | 위치(x,y) | 비용식 | Lv별 비용 | max | 해금 `req` | 효과 | 종류 |
|---|---|---|---|---|---|---|---|---|
| hand | 손 크기 | (60,452) | `40*1.75^l` | 40/70/123/214/375/657 | 6 | 항상 | `CARRY` 인덱스 | 시트 |
| spd | 이동 속도 | (200,452) | `60*1.8^l` | 60/108/194/350/630 | 5 | 항상 | +18/Lv | 시트 |
| price | 프리미엄 라벨 | (134,598) | `200*1.9^l` | 200/380/722/1372/2606 | 5 | sold≥10 | 병값 +6/Lv | 시트 |
| cashier | 삐걱 고용 | (360,598) | 150 | 150 | 1 | sold≥3 | 계산대 로봇(자동 결제), 러시·부재수익 활성 | 월드 |
| bottler | 병입기 속도 | (290,372) | `80*1.7^l` | 80/136/231/393/668/1136 | 6 | sold≥3 | `bottleTime` | 월드 |
| ice2 | 얼음밭 확장 | (126,220) | 120 | 120 | 1 | bottler≥1 | 구역 ice2, 더미 +4 | 구역 |
| melter | 제빙기 속도 | (290,192) | `70*1.7^l` | 70/119/202/344/585/994 | 6 | ice2≥1 | `meltTime` | 월드 |
| shelf | 선반 칸 | (134,452) | `120*1.8^l` | 120/216/389/700 | 4 | ice2≥1 | +7칸/Lv | 월드 |
| runner | 뽀삐 고용 | (282,598) | 350 | 350 | 1 | sold≥20 | 운반 로봇 1 | 월드 |
| ads | 간판 | (50,598) | `150*1.8^l` | 150/270/486/875/1575 | 5 | runner≥1 | 손님 간격↓, 묶음↑, Lv2+ 줄 +2 | 월드 |
| drill | 얼음 드릴 | (62,166) | 600 | 600 | 1 | runner≥1 & ice2≥1 | 2초마다 얼음 2장 → 제빙기 | 월드 |
| pen | 먼지 토끼 우리 | (330,720) | 400 | 400 | 1 | drill≥1 | 구역 pen, 토끼 3, 22초마다 벨 | 구역 |
| dome | 휴게 돔 | (106,720) | 700 | 700 | 1 | pen≥1 | 구역 dome, 좌석 6, 팁 | 구역 |
| runspd | 뽀삐 속도 | (282,662) | `250*1.8^l` | 250/450/810/1458 | 4 | runner≥1 | +25/Lv | 월드 |
| runner2 | 뽀삐 한 명 더 | (360,662) | 1200 | 1200 | 1 | drill≥1 | 운반 로봇 +1 | 월드 |

`PAD_MAX` = 레벨 총합 48. 비용은 모두 `Math.round`.

### 3.1 월드 발판 노출 규칙 `visiblePads()`
```
ORDER = [cashier, bottler, ice2, melter, shelf, runner, ads, bottler, melter, ads, drill, pen, shelf, dome,
         runspd, bottler, melter, runner2, ads, shelf, runspd, bottler, melter, runspd, ads, bottler, melter, runspd, shelf]
```
1. `ORDER`를 앞에서부터 순회. id가 n번째 등장이면 "Lv n까지가 그 차례". `padLv(id) < 등장횟수` && `< max` && `req` 충족 && `!menu`인 것을 최대 **2개** 수집.
2. 2개 미만이면 남은 월드 발판 중 **현재 비용이 싼 순**으로 보충.
3. 부분 납입(`padProg>0`)된 발판은 추가로 항상 노출.
4. 새로 노출된 발판: pop(1.4→1, 0.5s) + 링. 구역 발판이면 게이트 아치·점선 윤곽도 이때 처음 등장.
5. `visSet`은 0.3초마다 + 구매 직후 갱신, 발판 납입·렌더·힌트·시트가 모두 이것만 참조.

### 3.2 발판 납입 `tickPads`
- 정지 상태 & 발판 30 이내 & `visSet` 포함일 때만.
- 프레임당 `take = min(bells, cost-prog, max(3, cost*dt*0.6))` → 최소 약 1.67초에 완납.
- 0.05초마다 플레이어→발판 동전 비행 + `tick` 음(높이 `480+700*prog/cost`).
- 진행 표시: 발판 안쪽 남색 원판 스케일 `sqrt(prog/cost)`. 살 수 있으면 금색 링 펄스(`1+sin(t/250)*0.04`).
- 완료 `completePad`: Lv+1, 링 2개, 플래시(구역 0.5/그 외 0.3), 흔들림(구역 7/3), 진동, 플로팅 `아이콘 Lv.n`, `onPad`, 테라 갱신, 저장.

### 3.3 시트(하단 업그레이드 버튼)
- 탭: 업그레이드 / 장식(sold≥60부터) / 목표(목표 칩 탭으로만 진입).
- 업그레이드 탭 구성: ① 이주 카드(sold≥60 or prestige>0) ② 스탯 3줄(hand·spd·price, 레벨 핍) ③ 시설 카드(구역·메뉴 제외 9개; 정렬 = 노출 중 금테 → 구매 가능 → 잠김 → MAX). 잠김 조건은 `rqN` 아이콘+숫자.
- 시트 구매 `buyPad`: 남은 금액 일시불. 시트 스탯은 시트를 닫고 HUD→플레이어 동전 6개, 시설은 버튼→HUD 동전.
- 업그레이드 버튼: 메뉴 스탯을 살 수 있으면 점 표시, 튜토리얼 후 첫 시트 구매 전까지 바운스.

### 3.4 장식 `DECOS` (1회 구매, 프레스티지 후 유지)
| id | 가격 | 효과 |
|---|---|---|
| awning | 150 | 차양 파란 줄무늬 |
| neon | 250 | 카운터 위 네온 간판(밤 발광) |
| plant | 200 | 매장 네 모서리 선인장 4개 |
| lights | 300 | 차양 아래 줄전구 11개 |
| flag | 120 | 깃발 3개 보라색 |
| tile | 350 | 매장 바닥 체크무늬 |

---

## 4. 구역·레이아웃

### 4.1 스테이션 `STN` (중심 x,y / 폭 w×h)
| 키 | x,y | w×h |
|---|---|---|
| melter 제빙기 | 322,124 | 96×64 |
| bottler 병입기 | 322,304 | 96×64 |
| counter 카운터 | 178,504 | 220×36 |
| register 계산대 | 316,504 | 40×40 (지폐 자리 `REG_SPOT` = +36,+4) |
| drill 얼음 드릴 | 62,104 | 80×60 (설치 전 숨김·충돌 없음) |

### 4.2 기타 좌표
| 이름 | 값 |
|---|---|
| `ICE_SPOTS` | (60,240)(116,268)(178,232)(84,312)(152,318)(44,366)(190,350)(112,380) ← 기본 8 / (150,86)(204,118)(148,158)(206,172) ← ice2 |
| 손님 줄 | `QUEUE_X=216`, `QUEUE_Y0=584`, `QUEUE_GAP=46`, 입구 `ENTRY=(206,1000)` (착륙장 원 r58) |
| `DOME` | (108,838) r84, 좌석 `SEATS` (74|142, 786|838|890) 6개 |
| `PEN` | x 276~378, y 740~934, 팁 자리 `PEN_TIP=(327,766)` |
| `LANDER` 착륙선 | (84,988) |
| 러너 적재/하역점 | 병입기 `(x-58, y+8)` / 카운터 `(x-30, y-40)` |
| 삐걱 자리 | 계산대 `(x+2, y-34)` |

### 4.3 구역 `ZONES` (사각 x0,y0–x1,y1, 울타리 변 `sides`, 출입구 `gates{s,at,w}`)
| 키 | 사각 | 바닥색 | 울타리 | 게이트 | 잠금 |
|---|---|---|---|---|---|
| ice 얼음밭 | 16,196–236,404 | #e6d8c6 | NESW | N@126 w70, S@126 w70 | 열림 |
| ice2 얼음밭 확장 | 16,46–236,196 | #e6d8c6 | NEW | (ice의 N게이트 공유) | pad `ice2` 120 |
| melt 제빙 구역 | 256,46–380,224 | #d9b298 | NESW | W@150 w70 | 열림 |
| bottle 병입 구역 | 256,224–380,404 | #d9b298 | ESW | W@330 w70, S@322 w70 | 열림 |
| shop 매장 | 16,420–380,560 | #efcfa9 | EW | — | 열림 |
| qL / qR 줄 난간 | x=186 / 246, y 570–676 | — | W(레일) | — | 열림 |
| dome 휴게 돔 | 22,740–196,934 | #d3dbbd | NESW | N@106 w60 | pad `dome` 700 |
| pen 토끼 우리 | 276,740–378,934 | #dcb085 | NESW | N@330 w60 | pad `pen` 400 |

- 해금 순서(강제): ice2(bottler≥1) → … → drill → pen → dome. 비용 120 → 400 → 700.
- 울타리: 기둥 간격 `POST_GAP=40`, 기둥 5×24×5, 레일 2단(y8, y17), 게이트 양쪽 굵은 기둥 7×30×7. 인스턴싱.
- 잠긴 구역: 바닥·울타리 숨김, 차례가 오면 점선 윤곽(어두운 평면) + 자물쇠 아치(폭 = 게이트 w+10, 높이 38) 표시, 가격표 라벨은 아치 위.
- 해제 연출 `unlockZone`: 바닥 pop(0.001→1, 0.7s, 되튐 0.35), 울타리 부품이 땅 밑 −22에서 둘레 순서로 솟음(각 0.5s, 순서×0.03s 지연, easeOutBack), 네 모서리 먼지, 링 2개(흰 r60·금 r30), 가로등 켜짐, 테라 타일, 흔들림 5, 팡파르, 색종이 32.
- 모서리 가로등: 열린 구역 모서리마다(중복 제거), 밤에 전구·바닥빛(opacity `night*0.33`).

---

## 5. 로봇·손님·부재 수익

### 5.1 로봇
| 로봇 | 고용 | 동작 |
|---|---|---|
| 삐걱(cashier) | 150 | 계산대 옆 고정, 존재만으로 자동 결제. 회색 몸체 `0x9aa7b2`. 러시·부재 수익 조건 |
| 뽀삐(runner) ×최대 2 | 350 / 추가 1200 | 상태기계 `toBottler → load → toCounter → unload`. 속도 `95+25*runspd` (95,120,145,170,195). 적재 0.18s/병, 최대 8병, `carry>0 && bottles==0 && t>1.5`면 출발. 하역 0.15s/병, 선반 가득이면 대기. 손수레 스택(`cartLayout`) |
| 얼음 드릴 | 600 | `drillT` 2.0s마다 얼음 2장(0.15s 차, 0.9s 아크, lift 140)을 `meltQ<40`일 때 제빙기로 |
| 먼지 토끼 ×3 | pen 400 | 우리 안 랜덤 이동 속도 58, 대기 1~4s, 깡충 `|sin(hop)|*9`. `penT` 22s마다 `penTip < price*8`이면 `penTip += round(price*1.2)` |

### 5.2 손님
| 항목 | 값 |
|---|---|
| 첫 스폰 | `spawnT=3` |
| 간격 `baseGap()` | `max(2.0, 6.5 - ads*0.9)` → 6.5,5.6,4.7,3.8,2.9,2.0 s |
| 실제 간격 | `spawnGap()*(0.7~1.3)`, `spawnGap = baseGap / (rush?3:1)`. 튜토리얼(`tut<3`) 중엔 고정 12s |
| 줄 상한(대기 중 인원) | 평상시 `6 (+2 if ads>=2)`, 러시 중 `floor((1000-584)/46)+1 = 10` |
| 원하는 병 수 `want` | 튜토리얼 1 / 일반 `1 + (rand<0.3+0.06·ads) + (rand<0.1+0.05·ads)` / VIP 2~3 |
| VIP | `!tut && sold>=40 && custN%10==0` (10번째 손님마다). 금색 몸, 왕관, 금 외곽선 |
| 이동 속도 | 입장·줄 110, 좌석 이동 110, 퇴장 120 |
| 인내 | 줄 0번이고 `tut>=3`일 때만 `wait` 누적. >25s 느낌표·빨강, >40s 화나서 떠남(`S.lost++`, streak 0) |
| 외형 | `CUST_COLS` 7색 × 액세서리 `acc` 0~5(모자·배낭·안테나·목도리 등) |
| 결제 후 | 산 병이 손님 머리 위(y56, VIP 66)로 날아가 쌓임(공유 인스턴스 `HOLD_MAX=48`), 점프 0.5s(높이 22) + 하트 |
| 휴게 돔 | 돔 열림 & 빈 좌석 → 경유점 `seatPath`로 착석, 5~8s 앉았다 팁 `round(price*0.5)`을 좌석에 남기고 `leavePath`로 퇴장 |
| 저장 | 손님은 저장 안 함 |

### 5.3 러시
- 삐걱 고용 후 `rushT` 누적 240s → `rush=20`s. 간격 ÷3, 줄 상한 10, 상단 카운트다운 배지, 경보음, 흔들림 4.

### 5.4 부재 중 수익 `offlineEarn()`
```
el = now - savedAt (초);  el < 120 또는 삐걱 없음 → 없음
secs = min(el, 3*3600)
rate = min(1/bottleTime(), 1.55/baseGap()) * bottlePrice() * (runner ? 0.5 : 0.25) * (drill ? 1 : 0.7)
n = round(secs*rate);  n < 10 → 없음
```
- 시작 버튼 탭 시 + 앱 복귀(visibilitychange) 시 계산. 모달(보물 상자 + `+n · ⏱ m분`), 확인 시 동전 14개 HUD로.
- 인트로 화면이 떠 있는 동안은 `savedAt` 갱신 안 함.

---

## 6. 목표·튜토리얼·프레스티지·테라포밍

### 6.1 목표 `QUESTS` (순차 12개, `qv=2`)
| # | 내용 | 목표 g | 판정 | 보상 r(벨) |
|---|---|---|---|---|
| 1 | 물병 1개 팔기 | 1 | sold | 30 |
| 2 | 물병 5개 팔기 | 5 | sold | 50 |
| 3 | 손 크기 올리기 | 1 | hand | 40 |
| 4 | 삐걱 고용 | 1 | cashier | 80 |
| 5 | 얼음밭 확장 | 1 | ice2 | 60 |
| 6 | 물병 30개 | 30 | sold | 100 |
| 7 | 뽀삐 고용 | 1 | runner | 150 |
| 8 | 간판 Lv.2 | 2 | ads | 150 |
| 9 | 얼음 드릴 | 1 | drill | 250 |
| 10 | 토끼 우리 | 1 | pen | 200 |
| 11 | 휴게 돔 | 1 | dome | 300 |
| 12 | 물병 300개 | 300 | sold | 500 |

- 0.3초마다 `questTick`. 걷는 중·아이템 비행 중·발판 완료 0.6s 이내면 최대 3s 보류.
- 완료: 비차단 상자 팝 1.5s(`chestT=1.6` 동안 다음 목표 보류), 0.4s 후 뚜껑 열림 + 동전 14개 HUD, 플래시 0.3, 흔들림 4. 전부 끝나면 칩에 왕관.
- 상단 목표 칩: 아이콘, `v/g`, 진행 바, 상자 + 보상.

### 6.2 튜토리얼 `S.tut` (0→3)
| 단계 | 머리 위 말풍선 `TUT` | 다음 단계 조건 |
|---|---|---|
| 0 | 얼음을 주워요 | 등짐 가득 or 열린 더미 없음 |
| 1 | 제빙기에 넣어요 | 등짐의 얼음을 모두 제빙기에 넣음 |
| 2 | 카운터에 올려요 | 병을 선반에 처음 올림 |
| 3 | (완료) | — |
- 튜토리얼 중: 손님 12s 간격·want 1·VIP 없음, 인내 미적용. 시작 시 손 포인터 "드래그" 애니(첫 이동 시 사라짐).
- 튜토리얼 후: 처음 드러나는 가장 가까운 발판 위에 손 포인터 탭 애니 3.6s(`handShown=1`).
- 이후 안내는 문장 없이 **바닥 화살표**(타깃이 70 초과로 멀 때, 플레이어 앞 30, 높이 30) + **타깃 링**(r26 펄스) + 머리 위 아이콘 쌍 말풍선(입력 없음 2s 또는 타깃 120 초과일 때).
- 힌트 우선순위 `hint()`: 얼음 가득/붓는 중/밭 비었음→제빙기 · 병 받는 중→병입기 · 병 들고 선반 여유→카운터 · 병 들고 선반 가득→(삐걱 있으면 카운터, 없으면 계산대) · 현금 있음(삐걱 없거나 cash>price*4)→계산대 · 트레이 병 있고 뽀삐 없음→병입기 · 결제 대기 손님 & 삐걱 없음→계산대 · 토끼 팁≥price*4→우리 · 다음 발판(sold≥1 & bells≥남은금액의 50%)→발판 · (드릴 없고 큐 비었으면) 가까운 얼음 더미 · 그 외 병입기.

### 6.3 프레스티지 "새 기지로 이주"
| 항목 | 값 |
|---|---|
| 조건 `canPrestige()` | ice2·pen·dome 모두 해금 && `sold>=300` && `terra()>=0.6` |
| UI | 시트 최상단 🚀 카드(sold≥60부터 노출), 2회 탭 확인(3s 내) |
| 초기화 | pads, padProg, bells, shelf, bottles, water, cash, meltQ/T, botT, penTip, spots, 플레이어 위치·등짐, 손님·좌석 |
| 유지 | sold, quest, deco, clock/day, custN, qc 통계 |
| 영구 보너스 | `prestige++` → 병값 `×1.5^prestige`, 등짐 +3(최대 2회), `terraKeep += 8`(초록 타일), 하늘색이 `0x6fa6d8` 쪽으로 `min(0.5, 0.1*prestige)` |

### 6.4 테라포밍
- `terra() = clamp(0.5*(ice2+pen+dome)/3 + 0.5*(Σ발판 레벨)/48, 0, 1)`.
- 켜진 타일 수 `n = min(슬롯수, round(terra*24) + terraKeep)`. 슬롯 `TERRA_SLOTS` 45개(바위와 `r+14` 이내 겹치면 제외), 3번째마다 키 큰 나무.
- 구역 태그 슬롯(ice2/pen/dome 각 3개)은 그 구역이 열리면 우선 점등.
- 점등 연출: 0.12s 간격, 0.5s easeOutBack(0.001→`TS=28`), 초록 링.
- 땅 색: `white.lerp(0xb3683e, terra*0.45)`.

---

## 7. 카메라·조명·연출

### 7.1 카메라
| 항목 | 값 |
|---|---|
| 투영 | **직교**. 가로 반폭 `HALF=210` 고정 → `orthographicSize = 210*k * (Screen.height/Screen.width)` |
| 방향 | 타깃에서 오프셋 `normalize(0.07, 1.43, 1) * 1800` → 고도 ≈55°, 아주 약한 사선(yaw ≈4°). Unity에선 z 반전: `(0.07, 1.43, -1)` |
| near/far | −3000 / 6000 (Unity는 near 0.3, 거리 1800k에 맞게 far 조정) |
| 추적 | `cam.x=200` 고정(가로 스크롤 없음). `lead = lerp(lead, clamp(vel.y*0.22, ±50), dt*3)`, 목표 y = `p.y + lead + viewH*0.04`, `clamp(viewH*0.5-80, H-viewH*0.5+50)`, `cam.y = lerp(cam.y, 목표, dt*5)` |
| 흔들림 | `shakeA` 최대값 유지, 감쇠 `×exp(-7·dt)`, 타깃 x,z에 `±shakeA` 랜덤 |

### 7.2 렌더·조명
| 항목 | 값 |
|---|---|
| 톤매핑 | ACES Filmic, exposure 1.1, sRGB |
| 배경 | `0xc9552b` (낮밤 보간) |
| 반구광 | 하늘 `0xfff1e2`, 땅 `0xa8482a`, 1.05 |
| 태양(그림자) | `0xfff0dc`, 1.9, 방향 `normalize(-0.75, 0.8, 0.55)` (낮은 해 → 긴 그림자), 그림자맵 2048×1280 PCF Soft, bias −0.0005, normalBias 1.2 |
| 보조광 | `0x9fc6ff` 0.25, 위치 (600,500,−400), 그림자 없음 |
| 재질 | `MeshStandard roughness 0.95, metalness 0, flatShading` (툰 로우폴리) → URP Lit + Flat 또는 Simple Lit |
| 외곽선 | 역면(BackSide) 외곽선 메시, 색 `0x1b1a2e`(VIP 금 `0xd49000`), 두께 1.4~2.2 → URP Inverted Hull 셰이더 or Renderer Feature |
| 접지 그림자 | 반투명 원판 `0x3a1206` α0.3, 점프 높이에 따라 `max(0.5, 1-h/60)` 축소 |
| 비네트 | DOM `#vig` → Post-processing Vignette |

### 7.3 낮밤 `DAYK` (`DAY_LEN=300`s, 위상 = `clock%300/300`)
| 위상 | 태양색 | 강도 | 하늘 | 땅 | 반구 | 배경 | night |
|---|---|---|---|---|---|---|---|
| 0.00 정오 | fff0dc | 1.9 | fff1e2 | a8482a | 1.05 | c9552b | 0 |
| 0.32 | ffe6c8 | 1.85 | ffe9d4 | a8482a | 1.05 | c9552b | 0 |
| 0.46 노을 | ffb47c | 1.6 | ffc8a4 | 8a3a24 | 1.05 | b84a2a | 0.3 |
| 0.58 밤 | 8fa0e8 | 1.05 | 7080c8 | 3a2a50 | 1.2 | 4e2436 | 1 |
| 0.78 밤 | 8fa0e8 | 1.05 | 7080c8 | 3a2a50 | 1.2 | 4e2436 | 1 |
| 0.90 새벽 | ffc4a0 | 1.6 | ffd4bc | 8a4034 | 1.05 | b84a2a | 0.3 |
| 1.00 | (=0.00) | | | | | | |
- `night` 값으로 발광 재질(`emissiveIntensity = b + k*night`: 창문·가로등·네온·줄전구·착륙선 창) 켬. 날이 바뀌면 `☀️ n` 토스트.

### 7.4 손맛(juice) 목록
| 효과 | 사양 |
|---|---|
| 아이템 아크 비행 `fly` | ease-out quad, 높이 `+sin(πk)*lift`(기본 45), y축 회전 8rad/s, 기본 0.3~0.35s |
| 스쿼시 `sq` | 0.32s, `s=sin(πk)*0.22*a` → (1+s, 1−s, 1+s) |
| 팝 `pop` | from→1 easeOutBack(c=1.70158, 바닥 0.35), 기본 0.55s |
| 링 `ringFx` | 토러스, 0.6s, 스케일 1+2.2k, 페이드 |
| 동전 팝 `coinPop` | 동전 n개 튀김, 중력 380, 바닥 반발 −0.4, 0.9s |
| HUD 동전 `coinsToHud` | DOM 동전: 0.3s 튀어오름(중력 1200) → 0.36s 아크로 벨 HUD 이동, 35ms 시차, 도착마다 숫자 증가·펀치·음 높이 상승(`1100*1.06^i`) |
| 색종이 | 판매(18/36), 구역(32), 장식(20) |
| 화면 플래시 | 0.3~0.8 불투명 → 0.45s 페이드 |
| 흔들림 강도 | 발판 3, 구역 발판 7, 구역 해제 5, 러시 4, VIP 4, 큰 판매 2, 큰 수거 3, 목표 4, 이주 8 |
| HUD 숫자 펀치 | 값이 바뀔 때 CSS punch |
| 콤보 배지 | `×n` (3~5) + 상승음 |
| 플로팅 텍스트 | 1.1s, 높이 40+50t, gold/big 변형 |
| 진동 | `navigator.vibrate` 6~60ms 패턴 → Unity `Handheld.Vibrate` 또는 iOS 햅틱 플러그인 |
| 살아있는 파츠 | 제빙 코어 발광(`0.9+sin(t/110)*0.5`), 김 0.28s 간격, 병입 컨베이어 텍스처 스크롤(1.4/s)·병 이동, 깃발·차양 흔들림, 계산대 화면 발광, 안테나 회전(`t/1800`), 드릴 회전·상하, 착륙선 비컨, 태양광 반짝임(`sin^24`) |
| 환경 | 먼지 입자 150(돌풍 14~24s 주기, 2.6s, ×6), 먼지 회오리 2개(구슬 14개), 바람 루프(로우패스 260Hz, LFO 0.09Hz) |
| 사운드(신스) | pick, put, coin, sell, tick, pad, up, ding, no, hop, click, whoosh, puff, fanfare, unlock, combo, chest, vip, rush, jump → Unity에선 짧은 오디오 클립으로 대체(피치 파라미터 유지) |

---

## 8. 저장 데이터

- 키: `localStorage['mars-bottle-v1']` (JSON). 3초마다 + 백그라운드 전환 + pagehide 시 저장.
- Unity: `PlayerPrefs`(문자열 JSON) 또는 `Application.persistentDataPath/save.json`. 같은 필드명 유지 → 웹 저장 이전 가능.

| 필드 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| bells | float | 0 | 화폐(벨). 발판 납입은 소수 단위 |
| sold | int | 0 | 누적 판매 병 수 (프레스티지 후 유지) |
| custs | int | 0 | 결제 손님 수 |
| pads | {id:int} | {} | 발판 레벨 |
| padProg | {id:float} | {} | 부분 납입액 |
| p | {x,y,dir[2],carry[string]} | {200,450,[0,1],[]} | 플레이어 위치·방향·등짐 |
| water | int | 0 | 물 (10 단위) |
| bottles | int | 0 | 트레이 병 |
| shelf | int | 0 | 선반 병 |
| cash | int | 0 | 계산대 미수거 금액 |
| meltQ, meltT, botT | int/float | 0 | 제빙 큐·타이머 |
| spots | float[12] | 0 | 더미별 리스폰 남은 초 |
| savedAt | ms epoch | now | 부재 수익 기준 |
| muted | bool | false | |
| spawnT | float | 3 | 다음 손님까지 |
| penTip, penT | int/float | 0 | 토끼 벨·타이머 |
| drillT | float | (없음) | 드릴 타이머 |
| clock, day | float/int | 0/1 | 게임 시계·일차 |
| quest | int | 0 | 현재 목표 인덱스 |
| qc | {streak,comboMax,dayRev,dayBest,penGot,tipGot,vip,rush,earned} | 0 | 통계 |
| deco | {awning,neon,plant,lights,flag,tile:0/1} | 0 | 장식 |
| rushT | float | 0 | 러시 누적 |
| prestige | int | 0 | 이주 횟수 |
| custN | int | 0 | 스폰 카운터(VIP 판정) |
| lost | int | 0 | 화나서 떠난 손님 |
| tut | 0~3 | 0 | 튜토리얼 단계 |
| handShown | 0/1 | 0 | 발판 손 포인터 표시 여부 |
| qv | int | 2 | 목표 목록 버전 |
| terraKeep | int | 0 | 프레스티지로 남긴 타일 |
| qskip / qinit | 일시 | — | 옛 저장 마이그레이션용 |

`migrate()`: 누락 필드 채움, `sold>=1`이면 tut=3·handShown=1, drill 있으면 ice2=1 보정, 메뉴 스탯·요구조건 미충족·비용 초과 부분 납입금 환불. 로드 위치가 `blocked`면 시작 위치로.

---

## 9. 에셋 (Kenney CC0, `assets/models/*.glb`)

원본은 `tools/bake-models.mjs`의 `LIST`가 GLB를 정점색으로 구워 base64로 넣고 `SK(포인트색, 어두운색)`으로 팔레트 4색(흰 edf0f5·밝은회 d6dbe4·어두운회 8f949e·주황 ffd07c)을 게임 `COL`로 교체한다. Unity에선 GLB를 그대로 import(glTFast)하고 `Textures/colormap.png` 팔레트를 교체한 머티리얼 변형을 만들면 된다.

| LIST 이름 | 파일 | 용도 (`put` 스케일 sx,sy,sz) |
|---|---|---|
| coin | coin.glb | 날아가는 동전·동전 팝 (발광 금 재질) |
| trees / treesTall | grass-trees.glb / grass-trees-tall.glb | 테라포밍 초록 타일(TS=28) + 돔 화단(스케일 34) |
| fountain | pavement-fountain.glb | 돔 중앙 분수(30) |
| barrel | barrel.glb (z+90° 사전 회전) | 병입 구역 드럼통 3개(24, 주황·파랑·회색) |
| rockA~D | formation-rock / large-rock / stone / large-stone | 가장자리 바위 ~40개 + 큰 바위 6개 (화성색 교체, 인스턴싱) |
| hangar | hangar_smallA.glb | 제빙기 본체 (36,44,26) |
| hangarRound | hangar_roundA.glb | 병입기 본체 (20,26,16) |
| tankBig | machine_barrelLarge.glb | 제빙기 물탱크 (40,52,40, 파랑) |
| gen | machine_generator.glb | 병입기 펌프(30), 드릴 발전기(34) |
| genBig | machine_generatorLarge.glb | **구워지지만 코드에서 미사용** |
| pipe | pipe_straight.glb | 제빙기 파이프 (14,14,22) |
| chimney | chimney_detailed.glb | 제빙기 굴뚝 (16,24,16) |
| rFins/rFuel/rSides/rTop | rocket_finsA/fuelA/sidesA/topA | 착륙선 4단 (각 23) |
| tower | structure_detailed.glb | 드릴 데릭 2단 (36,44,36)+(30,40,30 45°) |
| barrels | barrels.glb | 드릴 옆 드럼통 묶음(34, 파랑) |
| pad | platform_large.glb | 돔 바닥 발판 (r84×1.04, 높이 18, 모래색) |
| airlock | corridor_end.glb | 돔 에어록 (30,34,30) |
| dish | satelliteDish.glb | 제빙 구역 위성 안테나(46, 회전) |
| rover | rover.glb | 착륙장 옆 로버(64) |

절차적(프리미티브) 메시로 만드는 것: 캐릭터(우주복 플레이어 `bPerson`, 손님 `bTraveler`, 로봇 `bRobot`, 토끼 `bHopper`), 카운터·차양·계산대, 울타리·게이트 아치, 발판 링, 얼음 판·물병 팩 판·지폐, 선인장, 태양광 패널, 곡괭이·썰매·팔레트, 돔 유리 반구, 진행 바, 화살표. 지면·구역 바닥은 캔버스 텍스처(균열·크레이터·바퀴 자국·착륙장 원). → Unity에선 ProBuilder/Blender로 미리 모델링 권장.

UI: Jua 글꼴, 베벨 패널·3px 테두리, 아이콘 스프라이트(bell, gear, ice, bottle, cash, drop, shelf, lock, crown, chest, heart, zz, bang, arrow, flag, up 등) + 이모지 아이콘(🧤👟🏷️🤖🧴🧊🔥🗄️🛒📣⛏️🐰🏕️⚡🚀🌱).

---

## 10. Unity 아키텍처 권장

### 10.1 구성
| 레이어 | 스크립트 | 책임 |
|---|---|---|
| 데이터 | `GameConfig` (ScriptableObject) | §1~6 상수 전부(CARRY, 반경, 쿨다운, 가격식 계수, 손님 확률, DAYK) |
| | `PadDef` (SO ×15), `ZoneDef` (SO ×8), `QuestDef` (SO ×12), `DecoDef` (SO ×6) | 발판 비용식(`base*growth^l`, 고정가), max, req(sold / pad 의존), 위치, menu/zone 플래그 |
| | `PadOrder` (SO) | `ORDER` 배열 |
| 상태 | `SaveData` ([Serializable], §8 필드) + `SaveSystem` | JSON 저장/로드/마이그레이션, 3초 자동저장, `OnApplicationPause` 저장 |
| | `GameState` (싱글턴) | `SaveData` 래핑 + 파생값(`CarryMax`, `ShelfCap`, `BottlePrice`, `MeltTime`, `BottleTime`, `BaseGap`, `RunnerSpeed`, `Terra`) + 이벤트(`OnBellsChanged`, `OnPadLevel`) |
| 좌표 | `WorldMap` (static) | `W2U(x,y,h)` 변환, STN·ICE_SPOTS·QUEUE·DOME 좌표 |
| 플레이어 | `FloatingJoystick` (UI) | 120/50px, 반경 45, 데드존 6, 아날로그 벡터 |
| | `PlayerMover` | 속도, 축분리 충돌(`Blocker` AABB/원 목록 or CharacterController + 구역 충돌체), 걷기 애니 |
| | `CarryStack` | 종류·리스트, 인스턴스 배치, 흔들림, 개수 칩 |
| | `PlayerInteractor` | §1.1 우선순위 표 + `actT` 쿨다운 + pend 처리 |
| 스테이션 | `IceSpot` ×12, `Melter`, `Bottler`, `Counter`(Shelf), `Register`(CashPile), `IceDrill` | 각자 타이머·용량·시각화(진행 바, 스택) |
| 업그레이드 | `PadManager` (`visiblePads`, `tickPads`, 구매), `UpgradePad` (월드 발판 뷰) | |
| | `ZoneManager`, `ZoneView` (바닥·울타리 솟기·아치·점선) | |
| AI | `CustomerSpawner`, `Customer` (상태기계 walkin/queue/toSeat/sit/leave), `QueueManager` | 경유점 경로 `seatPath/leavePath` |
| | `CashierBot`, `RunnerBot` (상태기계), `DustBunny`, `RushController` | |
| 메타 | `QuestManager`, `TutorialManager`, `HintSystem`(화살표·링·아이콘 말풍선), `PrestigeManager`, `TerraformManager`, `OfflineEarnings`, `DecoManager` | |
| 연출 | `ArcFlyer`(풀링), `JuiceFX`(squash/pop/ring/coinPop), `CameraRig`(ortho 추적·흔들림), `DayNightCycle`, `AudioManager`, `Haptics` | |
| UI | `HUD`(벨·톱니·목표 칩·러시·콤보), `UpgradeSheet`(탭 3), `WorldLabelPool`(월드→스크린 라벨: 자원 카운터·가격표·손님 말풍선·플로팅 텍스트), `Modals`(인트로·부재·설정·상자 팝) | |

- 시뮬레이션은 논리 좌표(Vector2, float)로 돌리고 뷰만 `W2U`로 배치하면 원본 수치를 그대로 쓸 수 있고 QA 대조가 쉽다.
- 대량 동일 메시(울타리·바위·타일·스택·손님 병)는 `Graphics.RenderMeshInstanced` 또는 GPU 인스턴싱 머티리얼.
- 원본 성능 기준: 드로우콜 시작 ~135, 후반 ~265.

### 10.2 Phase 1 — 최소 플레이어블 범위
| 포함 | 내용 |
|---|---|
| 맵 | 얼음밭(ice) + 제빙·병입 구역 + 매장, 기본 얼음 더미 8개, 평면 바닥·간단 울타리(충돌 없음) |
| 카메라 | 직교 55° 추적 + 흔들림 |
| 플레이어 | 플로팅 조이스틱, 이동·충돌, 등짐 스택(흔들림 포함) |
| 루프 | 줍기 → 제빙기 → 병입기 → 선반 → 손님 결제(플레이어가 계산대 r70) → 지폐 더미 → 수거 |
| 손님 | 스폰 간격 `baseGap`, 줄(최대 6), want 1~3, 인내 40s (VIP·러시·좌석 제외) |
| 업그레이드 | 월드 발판 4종(cashier, bottler, melter, shelf) + 시트 3스탯(hand, spd, price), `ORDER` 노출 규칙·정지 납입 |
| 로봇 | 삐걱(자동 결제) |
| 저장 | `SaveData` 핵심 필드(bells, sold, pads, padProg, p, water, bottles, shelf, cash, meltQ, spots, tut) |
| 연출 | 아크 비행, 스쿼시, 팝, HUD 동전, 효과음 5종(pick/put/coin/sell/ding) |
| 튜토리얼 | 말풍선 3단계 + 바닥 화살표 |

| 제외(Phase 2+) | 구역 해제(ice2/pen/dome)·뽀삐·드릴·토끼·돔 좌석, VIP·콤보·러시, 목표·상자, 장식, 낮밤, 테라포밍, 프레스티지, 부재 수익, Kenney 모델 교체(Phase 1은 박스 플레이스홀더) |

Phase 1 페이싱 검증 목표(원본 README): 첫 판매 0:30, 첫 발판 0:50, 첫 구역 확장 2:30 전후.

---

## 11. README ↔ 코드 차이 (코드 기준으로 포팅)

| 항목 | README | 코드 |
|---|---|---|
| 부재 수익 상한 | 2시간 | `3*3600` = 3시간, 삐걱만 있으면 발생(뽀삐 없으면 ×0.25) |
| VIP | 판매 20개 뒤 12번째마다 | `sold>=40 && custN%10==0` |
| 발판 수 | 14종 | `PADS` 15개(시트 3 + 월드 9 + 구역 3) |
| 목표 | 30단계(v2) | 12개(v5, `qv=2`) |
| genBig 모델 | 사용 목록에 있음 | 구워지지만 미사용 |

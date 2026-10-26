# 게임에 구워 넣는 저폴리 모델

`node tools/bake-models.mjs`가 이 폴더의 GLB를 읽어 `app/index.html`의 `/*@MODELS*/ … /*@/MODELS*/` 한 줄로 굽는다. 텍스처(`Textures/colormap.png`)는 굽는 단계에서 정점색으로 샘플링하므로 게임은 텍스처를 쓰지 않는다.

| 파일 | 게임에서 | 출처 | 라이선스 |
|---|---|---|---|
| `formation-rock.glb` `formation-large-rock.glb` `formation-stone.glb` `formation-large-stone.glb` | 가장자리 바위·지평선 바위산 (화성 색으로 교체) | Kenney, pmndrs market 배포본(Draco 해제) | CC0 1.0 |
| `barrel.glb` | 병입 구역 드럼통 3개 | Kenney, pmndrs market 배포본 | CC0 1.0 |
| `coin.glb` | 날아가는 동전·동전 튀김 | Kenney Starter Kit 3D Platformer | CC0 1.0 (키트 코드 MIT) |
| `grass-trees.glb` `grass-trees-tall.glb` `pavement-fountain.glb` | 휴게 돔 온실 화단·분수 | Kenney Starter Kit City Builder | CC0 1.0 (키트 코드 MIT) |
| `hangar_smallA` `hangar_roundA` `machine_barrelLarge` `machine_generator` `machine_generatorLarge` `pipe_straight` `chimney_detailed` `rocket_finsA` `rocket_fuelA` `rocket_sidesA` `rocket_topA` `structure_detailed` `barrels` `platform_large` `corridor_end` `satelliteDish` `rover` (.glb) | 제빙기·병입기 본체, 물탱크·펌프·굴뚝, 착륙선(로켓 4단), 얼음 드릴 데릭, 휴게 돔 바닥·에어록, 위성 안테나, 로버 | Kenney Space Kit 2.0 (`Models/GLTF format/`의 GLB) | CC0 1.0 |
| `Textures/colormap.png` | 위 Kenney 키트 모델의 색 팔레트 | Kenney | CC0 1.0 |

Kenney(https://kenney.nl)의 에셋은 CC0라 표기 의무가 없지만 고마우니 적어 둔다. 새 모델을 더할 때는 CC0 또는 그에 준하는 라이선스인지 확인하고 이 표에 한 줄 적는다.

## Space Kit

Kenney Space Kit(https://kenney.nl/assets/space-kit) zip은 `Models/GLB format/`이 없고 `Models/GLTF format/` 안에 `.glb`가 들어 있다. 게임 쪽 색은 `app/index.html`의 `SK(포인트색, 어두운색)` 색교체맵으로 팔레트 4색(흰·밝은 회색·어두운 회색·주황)을 게임 `COL`에 맞추고, `put(그룹, 이름, sx, sy, sz, x, y, z, 회전, 색교체맵)`으로 비균등 스케일을 걸어 배치한다.

## KayKit (Kay Lousberg, CC0)

KayKit 팩(Space Base Bits 1.0·Restaurant Bits 1.0, https://kaylousberg.itch.io · GitHub KayKit-Game-Assets)은 `.gltf + .bin + 팔레트 아틀라스 png`로 온다. 원본은 레포에 넣지 않고, 쓸 모델만 `tools/kaykit_bake.py`로 아틀라스 색을 정점 색으로 구워 `unity/Assets/Resources/Models/kk_<이름>.bytes`(자체 바이너리, Unity 좌표)에 둔다. Unity는 `KK.Load` → `ToyMesh.Kk`로 다른 조각과 한 메시에 합쳐 정점 색 셰이더(MarsVColor)로 그린다.

```
python3 -I tools/kaykit_bake.py sheet <팩>/Assets/gltf 미리보기.png [이름 ...]   # 고를 때 보는 접촉 인화
python3 -I tools/kaykit_bake.py bake  <팩>/Assets/gltf unity/Assets/Resources/Models 이름 [이름 ...]
```

| 파일(kk_) | 게임에서 | 팩 |
|---|---|---|
| `basemodule_B` | 해빙기 화로(문이 서쪽 얼음밭을 봄, 지붕 위 얼음 호퍼) | Space Base Bits |
| `basemodule_C` | 병입기 충전실(문 앞 컨베이어) | Space Base Bits |
| `roofmodule_base` | 기계·드릴 팔각 받침(눌러 편 판) | Space Base Bits |
| `containers_C` `solarpanel` | 해빙기·병입기 개량·고급 부품(파란 물통 묶음·태양판) | Space Base Bits |
| `drill_structure` `cargo_B_stacked` | 얼음 드릴 | Space Base Bits |
| `landingpad_large` `lander_A` | 착륙장·착륙선 | Space Base Bits |
| `cargo_A_stacked` `cargo_B_stacked` `rocks_A` `rock_B` | 소품(화물 더미 둘·돌 둘) | Space Base Bits |
| `kitchencounter_straight_A` `kitchencounter_straight_B` | 카운터 6칸·계산대 몸통 | Restaurant Bits |
| `crate` | 병입기 병 상자(금색 윗테를 덧댐) | Restaurant Bits |
| `basemodule_A` `basemodule_D` | 전기분해기·비료 배합기 / 토양 정화기 몸통 | Space Base Bits |
| `basemodule_C` `basemodule_E` `roofmodule_cargo_A` `roofmodule_solarpanels` `structure_tall` | 본부 Lv1~5(지휘 모듈·유리 돔 모듈·화물 지붕·태양 돛·통신탑·관제탑 철골) | Space Base Bits |
| `basemodule_E` `solarpanel` `structure_low` `cargo_B` | 개척지 거주 돔·태양광 판·배터리 받침·변환기 | Space Base Bits |
| `tunnel_straight_A` `containers_A` | 온실 → 물탱크 통로·휴게 돔 에어록 / 배합기 비료 통 | Space Base Bits |
| `basemodule_B` | 토끼집(작게) | Space Base Bits |
| `rocks_B` `rock_A` `terrain_tall_curved` `spacetruck` | 가장자리 바위·앞 메사 지형 조각·로버 | Space Base Bits |
| `chair_A` `table_round_A_small` `bowl` | 휴게 돔 좌석·탁자·분수 수반, 우리 물그릇 | Restaurant Bits |
| `crate_potatoes` `crate_lettuce` `crate_carrots` `food_ingredient_lettuce` | 식량 물건(감자 상자)·우리 상추·당근 상자·온실 작물 | Restaurant Bits |

CC0라 표기 의무는 없지만 Kay Lousberg(www.kaylousberg.com)에게 고마움을 적어 둔다.

## Unity 빌드에 남긴 Kenney GLB(r7)

KayKit 통일(r7)로 Unity `Resources/Models`에는 로켓 4단(`rocket_finsA` `rocket_fuelA` `rocket_sidesA` `rocket_topA`: 이주 연출 PrestigeManager·`-kk 0` 착륙선)만 남겼다. 나머지 Kenney GLB 와 `Textures/colormap.png` 는 빌드에서 뺐고, `-kk 0` 에서 그 모델을 찾으면 `ModelLib.Mdl` 이 빈 자리를 둔다. 웹판(`app/`)은 이 폴더 원본을 그대로 쓴다.

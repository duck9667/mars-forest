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

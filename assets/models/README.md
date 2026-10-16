# 게임에 구워 넣는 저폴리 모델

`node tools/bake-models.mjs`가 이 폴더의 GLB를 읽어 `app/index.html`의 `/*@MODELS*/ … /*@/MODELS*/` 한 줄로 굽는다. 텍스처(`Textures/colormap.png`)는 굽는 단계에서 정점색으로 샘플링하므로 게임은 텍스처를 쓰지 않는다.

| 파일 | 게임에서 | 출처 | 라이선스 |
|---|---|---|---|
| `formation-rock.glb` `formation-large-rock.glb` `formation-stone.glb` `formation-large-stone.glb` | 가장자리 바위·지평선 바위산 (화성 색으로 교체) | Kenney, pmndrs market 배포본(Draco 해제) | CC0 1.0 |
| `barrel.glb` | 병입 구역 드럼통 3개 | Kenney, pmndrs market 배포본 | CC0 1.0 |
| `coin.glb` | 날아가는 동전·동전 튀김 | Kenney Starter Kit 3D Platformer | CC0 1.0 (키트 코드 MIT) |
| `grass-trees.glb` `grass-trees-tall.glb` `pavement-fountain.glb` | 휴게 돔 온실 화단·분수 | Kenney Starter Kit City Builder | CC0 1.0 (키트 코드 MIT) |
| `Textures/colormap.png` | 위 Kenney 키트 모델의 색 팔레트 | Kenney | CC0 1.0 |

Kenney(https://kenney.nl)의 에셋은 CC0라 표기 의무가 없지만 고마우니 적어 둔다. 새 모델을 더할 때는 CC0 또는 그에 준하는 라이선스인지 확인하고 이 표에 한 줄 적는다.

## 더 좋은 건물을 쓰고 싶을 때

Kenney **Space Kit**(https://kenney.nl/assets/space-kit, CC0, 150여 개: 돔·로켓·로버·기계·파이프·크레이터)은 컨테이너에서 내려받을 수 없어 아직 안 들어갔다. 맥에서 zip을 받아 `Models/GLB format/`의 원하는 파일을 이 폴더에 넣고 `tools/bake-models.mjs`의 `LIST`에 추가하면 같은 방식으로 들어간다.

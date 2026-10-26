using UnityEngine;
using static KayKitDress;

// KayKit 통일(r7): 맵 가장자리 소품. 포인트 바위 = KayKit 돌무더기(rocks_B·rock_A, 붉은 갈색으로), 앞(남) 낮은 메사 둘 = KayKit 지형 조각(terrain_tall_curved)
//  묶음(지층 색으로), 로버 = KayKit 우주 트럭(spacetruck), 북쪽 물 배관 끝 저장 탱크 = KayKit 톤 둥근 탱크. 예전 모형은 -kk 0.
public static partial class WorldDress
{
    static System.Func<Color, Color> KkRockColor => KK.Hue(0, 40, ROCK_M, 0.75f);

    // 포인트 바위: sc = 무더기 지름(논리)
    static Mesh KkRockMesh(int v, float sc) => ToyMesh.Cached("kkRock" + v + "_" + Mathf.RoundToInt(sc), m =>
    {
        m.At(Vector3.zero, new Vector3(0, v * 70, 0)).Kk("rocks_B", sc / 1.9f, KkRockColor);
        m.At(new Vector3(sc * 0.55f, 0, -sc * 0.3f), new Vector3(0, 40 + v * 50, 0)).Kk("rock_A", sc * 0.6f, KkRockColor);
        return m;
    });

    // 지형 조각 색: KayKit 주황 윗면·갈색 옆면 → 메사 지층(밝을수록 뚜껑 색)
    static Color KkMesaColor(Color c)
    {
        Color.RGBToHSV(c, out _, out _, out float v);
        float t = Mathf.Clamp01((v - 0.45f) / 0.42f);
        return t > 0.8f ? MESA_TOP : Color.Lerp(STRATA[0], STRATA[3], t / 0.8f);
    }
    // 낮은 메사 자리(타원 rx·ry, 높이 h)에 지형 조각 다섯 개: 가운데 셋은 높게, 앞 둘은 낮게. 둥근 모서리가 바깥을 보게 돌린다
    static void KkMesa(Transform parent, Vector4 me, float h, int seed)
    {
        float rx = me.z, ry = me.w;
        var mesh = ToyMesh.Cached("kkMesa" + seed, m =>
        {
            var blocks = new[] { new Vector4(-0.5f, -0.1f, 1f, 225), new Vector4(0f, -0.3f, 1.15f, 0), new Vector4(0.5f, -0.1f, 0.95f, 135),
                                 new Vector4(-0.22f, 0.35f, 0.7f, 270), new Vector4(0.3f, 0.38f, 0.75f, 90) };
            foreach (var b in blocks)
            {
                float bh = h * b.z, w = Mathf.Min(rx, ry) * 0.62f;
                m.At(new Vector3(b.x * rx, bh / 2f, -b.y * ry), new Vector3(0, b.w + seed * 17, 0)).Kk("terrain_tall_curved", new Vector3(w / 2f, bh / 2f, w / 2f), KkMesaColor);
            }
            return m;
        });
        var go = ToyMesh.Put(parent, "mesa", mesh, ToyMesh.Mat(0.05f), WorldMap.W2U(me.x, me.y, 0f) - parent.position);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    // 로버: 본부 터 남서쪽 모서리 밖(놀이 경계 밖)
    static void KkRover(Transform worldRoot)
    {
        var go = ToyMesh.Put(worldRoot, "rover", ToyMesh.Cached("kkRover", m => m.Kk("spacetruck", 36f)), ToyMesh.Mat(0.25f), WorldMap.W2U(274, 1052, 0f) - worldRoot.position);
        go.transform.localRotation = Quaternion.Euler(0, 2.3f * Mathf.Rad2Deg, 0);
    }

    static Mesh KkTankMesh() => ToyMesh.Cached("kkPipeTank", m =>
    {
        m.At(0, 0, 0).Cyl(12.6f, 12.2f, 2.4f, 22, KD, KG);
        m.At(0, 2.4f, 0).Cyl(11f, 11f, 20f, 22, KW_D, KW, true, KW);
        m.At(0, 22.4f, 0).Dome(11f, 11f, 6f, KW, Color.Lerp(KW, Color.white, 0.5f), 22, 4);
        m.At(0, 12f, 0).Band(11.35f, 4.4f, 22, GameConfig.Hex("4fb0f0"));
        return m;
    });
}

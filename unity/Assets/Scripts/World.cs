using UnityEngine;

// §4·§7.2 환경: 바닥·구역 바닥·울타리(인스턴싱 대신 Phase 1은 개별 메시)·바위·조명. Phase 1은 낮밤 없음(정오 고정).
public class World : MonoBehaviour
{
    void Start()
    {
        float k = GameConfig.K;
        // 바닥(여유 폭 포함). 원본은 흰 재질 + 캔버스 텍스처. 텍스처 없는 Phase 1은 화성 흙색 플레이스홀더.
        Prim.Box(transform, "ground", WorldMap.W2U(200, 520, -1), new Vector3(GameConfig.W + 600, 2, GameConfig.H + 600), GameConfig.Hex("cf7a48"));
        // 구역 바닥
        foreach (var z in WorldMap.ZONES)
        {
            if (!z.open) continue;
            float cx = (z.x0 + z.x1) / 2, cy = (z.y0 + z.y1) / 2;
            Prim.Box(transform, "floor_" + z.key, WorldMap.W2U(cx, cy, 0.5f), new Vector3(z.x1 - z.x0, 1, z.y1 - z.y0), z.floor);
            Fence(z);
        }
        // 줄 난간 qL/qR: x=186/246, y 570~676 레일
        Rail(186, 570, 676); Rail(246, 570, 676);
        // 착륙장 원 r58 (입구)
        Prim.Cyl(transform, "pad", WorldMap.W2U(WorldMap.ENTRY, 0.3f), 116, 0.6f, GameConfig.Hex("9a5a3c"));
        // 바위: 고정 시드
        var rnd = new System.Random(11);
        for (int i = 0; i < 26; i++)
        {
            float x = (float)rnd.NextDouble() * 520 - 60, y = (float)rnd.NextDouble() * 1100 - 30;
            if (x > 0 && x < 400 && y > 30 && y < 1024) { i--; continue; }
            float s = 10 + (float)rnd.NextDouble() * 22;
            var r = Prim.Sphere(transform, "rock", WorldMap.W2U(x, y, s * 0.3f), s, GameConfig.Hex("b3683e"));
            r.transform.localScale = new Vector3(s, s * 0.7f, s * 0.85f) * k;
        }
        Lights();
    }

    void Fence(WorldMap.Zone z)
    {
        var post = GameConfig.Hex("8a5a3c"); var rail = GameConfig.Hex("a8724a");
        foreach (char side in z.sides)
        {
            bool horiz = side == 'N' || side == 'S';
            float fixedV = side == 'N' ? z.y0 : side == 'S' ? z.y1 : side == 'W' ? z.x0 : z.x1;
            float a = horiz ? z.x0 : z.y0, b = horiz ? z.x1 : z.y1;
            // 게이트 틈
            float gAt = -1, gW = 0;
            foreach (var g in WorldMap.GATES) if (g.zone == z.key && g.side == side) { gAt = g.at; gW = g.w; }
            for (float v = a; v <= b + 0.1f; v += WorldMap.POST_GAP)
            {
                if (gAt >= 0 && Mathf.Abs(v - gAt) < gW / 2) continue;
                float x = horiz ? v : fixedV, y = horiz ? fixedV : v;
                Prim.Box(transform, "post", WorldMap.W2U(x, y, 12), new Vector3(5, 24, 5), post);
            }
            // 레일 2단(y8, y17): 게이트 틈을 제외한 구간별로
            void RailSeg(float s0, float s1)
            {
                if (s1 - s0 < 2) return;
                float mid = (s0 + s1) / 2, len = s1 - s0;
                float x = horiz ? mid : fixedV, y = horiz ? fixedV : mid;
                var size = horiz ? new Vector3(len, 3, 2.5f) : new Vector3(2.5f, 3, len);
                Prim.Box(transform, "rail", WorldMap.W2U(x, y, 8), size, rail);
                Prim.Box(transform, "rail", WorldMap.W2U(x, y, 17), size, rail);
            }
            if (gAt >= 0)
            {
                RailSeg(a, gAt - gW / 2); RailSeg(gAt + gW / 2, b);
                float x0 = horiz ? gAt - gW / 2 : fixedV, y0 = horiz ? fixedV : gAt - gW / 2;
                float x1 = horiz ? gAt + gW / 2 : fixedV, y1 = horiz ? fixedV : gAt + gW / 2;
                Prim.Box(transform, "gpost", WorldMap.W2U(x0, y0, 15), new Vector3(7, 30, 7), post);
                Prim.Box(transform, "gpost", WorldMap.W2U(x1, y1, 15), new Vector3(7, 30, 7), post);
            }
            else RailSeg(a, b);
        }
    }

    void Rail(float x, float y0, float y1)
    {
        var c = GameConfig.Hex("8a8fa8");
        for (float y = y0; y <= y1; y += 53) Prim.Box(transform, "qpost", WorldMap.W2U(x, y, 10), new Vector3(4, 20, 4), c);
        Prim.Box(transform, "qrail", WorldMap.W2U(x, (y0 + y1) / 2, 18), new Vector3(2.5f, 2.5f, y1 - y0), c);
    }

    void Lights()
    {
        var sun = new GameObject("sun").AddComponent<Light>();
        // 원본 1.9는 ACES 톤매핑 전제. 톤매핑 없는 Phase 1은 과노출을 피하려고 낮춘다.
        sun.type = LightType.Directional; sun.color = GameConfig.Hex("fff0dc"); sun.intensity = 1.15f;
        sun.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.75f, 0.8f, -0.55f).normalized, Vector3.up);
        sun.shadows = LightShadows.Soft; sun.shadowBias = 0.02f; sun.shadowNormalBias = 0.6f;
        var fill = new GameObject("fill").AddComponent<Light>();
        fill.type = LightType.Directional; fill.color = GameConfig.Hex("9fc6ff"); fill.intensity = 0.2f; fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.LookRotation(-new Vector3(600, 500, 400).normalized, Vector3.up);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = GameConfig.Hex("fff1e2") * 0.55f;
        RenderSettings.ambientEquatorColor = Color.Lerp(GameConfig.Hex("fff1e2"), GameConfig.Hex("a8482a"), 0.5f) * 0.55f;
        RenderSettings.ambientGroundColor = GameConfig.Hex("a8482a") * 0.55f;
        QualitySettings.shadowDistance = 200f; // 직교 카메라 거리 90 + 월드 반경
        QualitySettings.shadowResolution = ShadowResolution.High;
    }
}

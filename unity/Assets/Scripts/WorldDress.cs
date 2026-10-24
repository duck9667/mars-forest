using System.Collections.Generic;
using UnityEngine;

// §9 환경: 맵 바깥을 감싸는 메사(평평한 붉은 절벽) 테두리 + 포인트 바위 몇 개, 착륙선 4단, 로버, 위성 안테나, 드럼통.
// jake 피드백("어설픈 산맥이 아쉽다")으로 원본의 가장자리 바위 40여 개 흩뿌림을 "협곡 안 기지" 테두리로 바꿨다.
// 원본 index.html 546~550행(착륙선), 555행(드럼통), 562행(안테나), 573행(로버). World.Start가 호출한다.
public static class WorldDress
{
    const float LANDER_X = 84f, LANDER_Y = 988f; // 원본 253행 LANDER

    // 원본 237행 배치용 고정 난수 (seed 11 Park–Miller). 원본은 이 난수를 바위 전에 다른 장식에도 쓰므로 자리 자체는 원본과 1:1이 아니다
    class Rnd
    {
        long seed = 11;
        public float Next() { seed = seed * 16807 % 2147483647; return (float)((seed - 1) / 2147483646.0); }
    }

    // rockRoot: 프리미티브 바위들의 부모. 이 아래를 비우고 모델 바위로 다시 채운다.
    public static void Dress(Transform worldRoot, Transform rockRoot)
    {
        for (int i = rockRoot.childCount - 1; i >= 0; i--) { var c = rockRoot.GetChild(i); c.SetParent(null, false); Object.Destroy(c.gameObject); }
        Rocks(rockRoot);
        Lander(worldRoot);
        // 착륙장 옆 로버 (원본 573행: mdl('rover',64,SK(orange,dark)), rotation.y=-2.3, at(344,990))
        ModelLib.Put(worldRoot, "rover", 64, 64, 64, 344 - 200, 0, 990 - 520, -2.3f, ModelLib.SK(ModelLib.COL.orange, ModelLib.COL.dark));
        Dish(worldRoot);
        Barrels(worldRoot);
        NorthPipe(worldRoot);
    }

    // 메사: (중심 x, 중심 y, 반지름 rx, ry, 높이). 보이는 범위는 대략 x −10~410, y Y_MIN−100(−490)~1090(CameraRig 직교 반폭 210).
    // 안쪽 가장자리는 놀이 경계(x 14~386, y Y_MIN+6~1024) 밖에 머물게, 아래쪽은 착륙장·입구(x 140~270) 앞을 협곡 입구로 비운다.
    // 북쪽 확장(생존 기지): 위 4개를 새 북쪽 경계 밖(y≈−500)으로 올리고, 늘어난 좌우에 2개씩 덧붙였다(배열 끝: 기존 메사 모양 난수 순서 유지)
    static readonly Vector4[] MESAS =
    {
        // 위(북): 옆면 지층 띠가 정면으로 보이는 쪽이라 가장 높게
        new Vector4(-40, -500, 110, 90), new Vector4(150, -498, 110, 88), new Vector4(330, -504, 120, 92), new Vector4(470, -490, 100, 80),
        // 왼쪽
        new Vector4(-64, 150, 68, 130), new Vector4(-62, 400, 66, 140), new Vector4(-66, 650, 70, 130), new Vector4(-64, 880, 68, 120),
        // 오른쪽
        new Vector4(464, 170, 68, 140), new Vector4(462, 430, 66, 130), new Vector4(466, 680, 70, 140), new Vector4(464, 920, 68, 120),
        // 아래(남): 앞쪽 물체를 가리지 않게 낮게
        new Vector4(10, 1124, 100, 72), new Vector4(390, 1128, 110, 76), new Vector4(-60, 1080, 80, 90), new Vector4(470, 1090, 80, 90),
        // 북쪽 확장 좌우(생존 기지 옆)
        new Vector4(-66, -330, 70, 140), new Vector4(-62, -90, 66, 130), new Vector4(466, -340, 70, 140), new Vector4(462, -96, 68, 130),
    };
    static readonly float[] MESA_H = { 72, 80, 76, 70, 58, 64, 54, 60, 60, 56, 62, 58, 26, 28, 40, 40, 62, 56, 60, 58 };

    // rockRoot: 테두리 부모(이름은 예전 그대로). 메사 + 포인트 바위 6개(북쪽 길 위에 있던 것은 북쪽 메사 발치·채굴장 옆으로)
    static void Rocks(Transform rockRoot)
    {
        var rnd = new Rnd();
        for (int i = 0; i < MESAS.Length; i++) Mesa(rockRoot, MESAS[i], MESA_H[i], rnd);
        // 포인트 바위: 메사 발치·경계 안쪽 빈자리에 몇 개만 (x, y, 크기, 회전)
        // 소품 밀도 절반(2026-10-25): 11개 → 6개
        var spots = new[] { new Vector4(4, 188, 10, .4f), new Vector4(398, 724, 10, 3.5f), new Vector4(134, 1050, 9, 1.7f),
                            new Vector4(88, -400, 9, 2.8f), new Vector4(4, -44, 10, 1.3f), new Vector4(397, -128, 9, .6f) };
        string[] names = { "rockA", "rockB", "rockC", "rockD" };
        var reAB = RockRe(0x8a3418, 0xf0a878); var reCD = RockRe(0x6e2a12, 0xd0683a);
        for (int i = 0; i < spots.Length; i++)
        {
            var sp = spots[i]; float sc = sp.z * 1.5f; int k = i % 4;
            ModelLib.Put(rockRoot, names[k], sc, sc, sc, sp.x - 200, 0, sp.y - 520, sp.w, k < 2 ? reAB : reCD);
        }
    }

    // 메사 한 덩어리: 들쭉날쭉한 10각 바닥을 지층 띠 4단으로 쌓아 올리고(단마다 살짝 들여 넣어 턱), 위는 평평한 밝은 뚜껑.
    // 한 메시에 서브메시(띠 4 + 뚜껑)로 그려 드로우콜을 줄이고, 면마다 꼭짓점을 따로 둬 저폴리 플랫 셰이딩
    // 지층 색: 예전(97401f·b9562f·a64a27·c4663b, 뚜껑 d4825a)에서 채도 25%·명도 3% 낮춰 땅과 같은 주황 계열로 뒤로 물러나게(2026-10-25)
    static readonly Color[] STRATA = { GameConfig.Hex("92533b"), GameConfig.Hex("b36b4f"), GameConfig.Hex("a15e45"), GameConfig.Hex("be7a5a") };
    static readonly Color MESA_TOP = GameConfig.Hex("ce9275");
    static void Mesa(Transform parent, Vector4 m, float h, Rnd rnd)
    {
        const int N = 10; float k = GameConfig.K;
        var foot = new Vector2[N];
        float a0 = rnd.Next() * Mathf.PI * 2;
        for (int i = 0; i < N; i++)
        {
            float a = a0 + (i + (rnd.Next() - 0.5f) * 0.5f) / N * Mathf.PI * 2, r = 0.84f + rnd.Next() * 0.2f;
            foot[i] = new Vector2(Mathf.Cos(a) * m.z * r, Mathf.Sin(a) * m.w * r);
        }
        float[] hs = { 0f, 0.34f, 0.58f, 0.8f, 1f };            // 띠 경계 높이 비율
        float[] insetB = { 1f, 0.97f, 0.95f, 0.92f };           // 띠 아래 둘레 배율(턱)
        float[] insetT = { 0.985f, 0.96f, 0.935f, 0.9f };       // 띠 위 둘레 배율(살짝 기울기)
        var verts = new List<Vector3>(); var subs = new List<List<int>>();
        Vector3 V(Vector2 p, float s, float y) => new Vector3(p.x * s * k, y * k, -p.y * s * k);   // 논리 y↓ → Unity z
        for (int b = 0; b < 4; b++)
        {
            var tri = new List<int>(); float y0 = hs[b] * h, y1 = hs[b + 1] * h;
            for (int i = 0; i < N; i++)
            {
                var p = foot[i]; var q = foot[(i + 1) % N]; int v = verts.Count;
                verts.Add(V(p, insetB[b], y0)); verts.Add(V(q, insetB[b], y0)); verts.Add(V(q, insetT[b], y1)); verts.Add(V(p, insetT[b], y1));
                tri.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
            }
            // 턱: 이 띠의 위 둘레(insetT[b]) → 다음 띠의 아래 둘레(insetB[b+1]) 사이 수평 고리
            if (b < 3)
                for (int i = 0; i < N; i++)
                {
                    var p = foot[i]; var q = foot[(i + 1) % N]; int v = verts.Count;
                    verts.Add(V(p, insetT[b], y1)); verts.Add(V(q, insetT[b], y1)); verts.Add(V(q, insetB[b + 1], y1)); verts.Add(V(p, insetB[b + 1], y1));
                    tri.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
                }
            subs.Add(tri);
        }
        {   // 뚜껑(부채꼴)
            var tri = new List<int>(); int c = verts.Count; verts.Add(new Vector3(0, h * k, 0));
            for (int i = 0; i < N; i++) verts.Add(V(foot[i], insetT[3], h));
            for (int i = 0; i < N; i++) tri.AddRange(new[] { c, c + 1 + (i + 1) % N, c + 1 + i });
            subs.Add(tri);
        }
        var mesh = new Mesh { name = "mesa" }; mesh.SetVertices(verts); mesh.subMeshCount = subs.Count;
        for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
        // 감김 방향 보정: 바깥을 향하도록(뚜껑은 위). 법선이 아래/안쪽이면 뒤집는다
        mesh.RecalculateNormals();
        if (Vector3.Dot(mesh.normals[verts.Count - 1], Vector3.up) < 0f)
        {
            for (int i = 0; i < subs.Count; i++) { var t = subs[i]; for (int j = 0; j < t.Count; j += 3) { int x = t[j + 1]; t[j + 1] = t[j + 2]; t[j + 2] = x; } mesh.SetTriangles(t, i); }
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        var go = new GameObject("mesa"); go.transform.SetParent(parent, false);
        go.transform.position = WorldMap.W2U(m.x, m.y, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { Mats.Solid(STRATA[0]), Mats.Solid(STRATA[1]), Mats.Solid(STRATA[2]), Mats.Solid(STRATA[3]), Mats.Solid(MESA_TOP) };
        mr.receiveShadows = true;
    }
    static Dictionary<int, int> RockRe(int a, int b) => new Dictionary<int, int> { { 0xdeb590, a }, { 0xede2cd, b }, { 0xc9d8e2, a } };

    // 북쪽 물 배관: 해빙 구역 동쪽 밖(x 388)을 따라 토양 공장 모서리까지 낮게 기는 관 + 받침, 끝에 저장 탱크 하나.
    // 온실이 물을 "파이프로" 먼저 가져간다는 설정을 배경으로만 보여 준다(충돌 없음, 놀이 경계 밖 가장자리)
    static void NorthPipe(Transform worldRoot)
    {
        var g = new GameObject("northPipe").transform; g.SetParent(worldRoot, false);
        const float X = 388f, Y0 = 124f, Y1 = -372f, R = 5f, H = 4f;   // Y0: 해빙기 높이에서 울타리 밑으로 빠져나온다
        var steel = ModelLib.C(ModelLib.COL.steel); var dark = ModelLib.C(ModelLib.COL.steelD);
        var pipe = Prim.Cyl(g, "pipe", WorldMap.W2U(X, (Y0 + Y1) / 2f, H), R, Y0 - Y1 + R, steel);
        pipe.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var stub = Prim.Cyl(g, "stub", WorldMap.W2U(X - 7f, Y0, H), R, 14f, steel);   // 울타리 쪽으로 꺾이는 짧은 관
        stub.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        for (float y = Y0 - 12f; y > Y1 + 8f; y -= 54f)
        {
            Prim.Box(g, "saddle", WorldMap.W2U(X, y, 2.5f), new Vector3(9, 5, 4), dark);
            Prim.Box(g, "band", WorldMap.W2U(X, y, H), new Vector3(R + 1.6f, R + 1.6f, 2), dark);
        }
        ModelLib.Put(g, "tankBig", 26, 26, 26, X - 200, 0, -400 - 520, 0.5f, ModelLib.SK(ModelLib.COL.blue));
    }

    // 착륙선: Kenney 로켓 파츠 4단 (날개·연료·몸통·코), S=23, SK(orange, dark) (원본 546~550행)
    static void Lander(Transform worldRoot)
    {
        var g = new GameObject("lander").transform; g.SetParent(worldRoot, false);
        g.localPosition = WorldMap.W2U(LANDER_X, LANDER_Y, 0f);
        var re = ModelLib.SK(ModelLib.COL.orange, ModelLib.COL.dark); const float S = 23f;
        ModelLib.Put(g, "rFins", S, S, S, 0, 0, 0, Mathf.PI / 4, re);
        ModelLib.Put(g, "rFuel", S, S, S, 0, .7f * S, 0, 0f, re);
        ModelLib.Put(g, "rSides", S, S * .9f, S, 0, 1.2f * S, 0, 0f, re);
        ModelLib.Put(g, "rTop", S, S, S, 0, 2.1f * S, 0, 0f, re);
        var bk = Prim.Sphere(g, "beacon", new Vector3(0, 2.9f * S + 3, 0) * GameConfig.K, 5, ModelLib.C(0xff5a3c)); Prim.NoShadow(bk);
    }

    // 멜터 옆 위성 안테나 (268,70): dish 46, SK(orange), 접시(안테나 전체)가 천천히 돈다 + 끝의 빨간 점 (원본 562행)
    static void Dish(Transform worldRoot)
    {
        var d = new GameObject("dish").transform; d.SetParent(worldRoot, false);
        d.localPosition = WorldMap.W2U(268, 70, 0f);
        var spin = new GameObject("spin").transform; spin.SetParent(d, false); spin.gameObject.AddComponent<DishSpin>();
        ModelLib.Put(spin, "dish", 46, 46, 46, 0, 0, 0, 0f, ModelLib.SK(ModelLib.COL.orange));
        var tip = Prim.Sphere(spin, "tip", new Vector3(0, 30, -4) * GameConfig.K, 3, ModelLib.C(0xff5a3c)); Prim.NoShadow(tip);
    }

    // 드럼통 2개 (병입 구역 구석, 원본 555행): barrel 24, 주황·파랑·회색, rotation.y = i×2.1. 통 색 d1a790→c, 테 edf4fa→steelD, 띠 ebc6aa→dark
    static void Barrels(Transform worldRoot)
    {
        var at = new (float x, float y, int c)[] { (352, 390, ModelLib.COL.orange), (370, 380, ModelLib.COL.steel) };   // 소품 절반: 3 → 2
        for (int i = 0; i < at.Length; i++)
        {
            var re = new Dictionary<int, int> { { 0xd1a790, at[i].c }, { 0xedf4fa, ModelLib.COL.steelD }, { 0xebc6aa, ModelLib.COL.dark } };
            ModelLib.Put(worldRoot, "barrel", 24, 24, 24, at[i].x - 200, 0, at[i].y - 520, i * 2.1f, re);
        }
    }
}

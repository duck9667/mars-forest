using System.Collections.Generic;
using UnityEngine;

// §9 Kenney 모델로 환경을 입힌다: 가장자리 바위·큰 바위, 착륙선 4단, 로버, 위성 안테나, 드럼통.
// 원본 index.html 516~522행(바위), 546~550행(착륙선), 555행(드럼통), 562행(안테나), 573행(로버). World.Start가 프리미티브 바위를 만든 뒤 호출한다.
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
    }

    // 바위: 가장자리 40개(원본 517~518행 분포, 논리 0..400×30..1024 안쪽은 비움) + 좌우 띠의 큰 바위 6개(519행).
    // 모델 4종 × 화성 색(521행 rockRe): A·B는 (8a3418, f0a878), C·D는 (6e2a12, d0683a). 크기 r×1.5, y회전 ry
    static void Rocks(Transform rockRoot)
    {
        var rnd = new Rnd();
        var spots = new List<Vector4>(); // x, y, r, ry
        var entry = WorldMap.ENTRY;
        for (int tries = 0; spots.Count < 40 && tries < 400; tries++)
        {
            float side = rnd.Next(); float x, y;
            if (side < 0.3f) { x = -30 + rnd.Next() * 30; y = -60 + rnd.Next() * 1140; }
            else if (side < 0.55f) { x = 386 + rnd.Next() * 22; y = 600 + rnd.Next() * 480; }
            else if (side < 0.85f) { x = rnd.Next() < 0.5f ? 24 + rnd.Next() * 146 : 262 + rnd.Next() * 114; y = 590 + rnd.Next() * 130; }
            else { x = rnd.Next() * 400; y = 1040 + rnd.Next() * 60; }
            if (Vector2.Distance(new Vector2(x, y), entry) < 80 || Vector2.Distance(new Vector2(x, y), new Vector2(LANDER_X, LANDER_Y)) < 50) continue;
            if (x > 0 && x < 400 && y > 30 && y < 1024) continue; // 놀이 영역 안쪽은 비워 둔다
            spots.Add(new Vector4(x, y, 6 + rnd.Next() * 10, rnd.Next() * 6));
        }
        spots.Add(new Vector4(-26, 90, 15, .4f)); spots.Add(new Vector4(-24, 250, 18, 2.2f)); spots.Add(new Vector4(-28, 430, 14, 1.1f));
        spots.Add(new Vector4(408, 720, 16, 3.5f)); spots.Add(new Vector4(406, 860, 19, 2.8f)); spots.Add(new Vector4(410, 990, 15, .9f));
        string[] names = { "rockA", "rockB", "rockC", "rockD" };
        var reAB = RockRe(0x8a3418, 0xf0a878); var reCD = RockRe(0x6e2a12, 0xd0683a);
        for (int i = 0; i < spots.Count; i++)
        {
            var s = spots[i]; float sc = s.z * 1.5f; int k = i % 4;
            ModelLib.Put(rockRoot, names[k], sc, sc, sc, s.x - 200, 0, s.y - 520, s.w, k < 2 ? reAB : reCD);
        }
    }
    static Dictionary<int, int> RockRe(int a, int b) => new Dictionary<int, int> { { 0xdeb590, a }, { 0xede2cd, b }, { 0xc9d8e2, a } };

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

    // 드럼통 3개 (병입 구역 구석, 원본 555행): barrel 24, 주황·파랑·회색, rotation.y = i×2.1. 통 색 d1a790→c, 테 edf4fa→steelD, 띠 ebc6aa→dark
    static void Barrels(Transform worldRoot)
    {
        var at = new (float x, float y, int c)[] { (350, 392, ModelLib.COL.orange), (370, 384, ModelLib.COL.blue), (358, 368, ModelLib.COL.steel) };
        for (int i = 0; i < at.Length; i++)
        {
            var re = new Dictionary<int, int> { { 0xd1a790, at[i].c }, { 0xedf4fa, ModelLib.COL.steelD }, { 0xebc6aa, ModelLib.COL.dark } };
            ModelLib.Put(worldRoot, "barrel", 24, 24, 24, at[i].x - 200, 0, at[i].y - 520, i * 2.1f, re);
        }
    }
}

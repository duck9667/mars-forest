using System.Collections.Generic;
using UnityEngine;

// 3장 우주항 꾸밈(docs/chapter3-design.md "3장 우주항", art-bible 덜어내기): 시설 사이 빈 땅에 "착륙장 기지"가 읽히게 KayKit 소품만 조금.
//  · 착륙장 유도등: 착륙장(팔각) 꼭짓점 바깥 콘크리트 판 위 낮은 등 8개. 화물선이 내려오거나 뜰 때 그 착륙장 것만 서서히 밝아진다.
//  · 연료 배관: 북서 초록 저장 탱크 → 착륙장 뒤(북쪽)를 지나는 낮은 관 → 착륙장마다 짧은 갈래 + 밸브(착륙장을 지으면 함께 선다).
//  · 화물 마당(남서): 창고(cargodepot_C) + 상자 더미 + 트레일러 단 작은 트럭. 발사대 남쪽은 비워 둔다(불꽃 자리).
//  · 조명탑 둘(북동·서쪽 입구, 기둥 위 등판이 착륙장 쪽을 봄, 밤에 등만 빛남) · 심우주 안테나 접시(남동, 끝 빨간 등이 천천히 숨 쉼) · 지역 가장자리 바위(메사 발치).
// 글자·말풍선 없음, 금색 없음(금색은 상호작용만), 소품은 배경 레이어(외곽선 없음, PostFX.ENV_LAYER).
// 걷는 길·발판·시설 발자국·힌트 지점은 비우고, 큰 소품만 우주항이 열렸을 때 Regions.SetSolid 발자국을 둔다.
// SpacePort가 부른다: Build(조립) · PadDress(착륙장마다) · OnPad(열림 솟아오름) · Tick(불빛).
public static class PortDress
{
    static Color Hex(string h) => GameConfig.Hex(h);
    static readonly Color KW = KayKitDress.KW, KW_D = KayKitDress.KW_D, KG = KayKitDress.KG, KD = KayKitDress.KD, KD_D = KayKitDress.KD_D, KO = KayKitDress.KO;
    static readonly Color SLAB = Hex("d9c8b2"), SLAB_T = Hex("e6d8c4");
    static readonly Color FUEL_G = Hex("34b37e");   // 연료통 초록(배관 밸브 손잡이)
    static readonly Color GUIDE = Hex("9fd6ff"), FLOOD = Hex("fff1c2"), BEACON = Hex("ff5a3c");

    // ---- 배치(논리) ----
    public const float PIPE_Y = 222f, PIPE_H = 5f;                                   // 착륙장 뒤 연료 관(지역 북쪽 띠)
    static readonly Vector2 TANKS = new Vector2(668, 258);                           // 연료 저장 탱크(북서)
    static readonly Vector2 YARD = new Vector2(716, 624);                            // 화물 마당(남서, 연료 공장 남쪽)
    static readonly Vector2 DISH = new Vector2(1040, 626);                           // 안테나 접시(남동, 관제탑 남쪽)
    static readonly Vector2[] FLOODS = { new Vector2(1062, 240), new Vector2(656, 470) };   // 조명탑(북동·서쪽 입구 남쪽)
    static readonly float[] FLOOD_RY = { 45f, -45f };                                // 등판이 보는 쪽(북동 → 남서 착륙장, 입구 → 남동 광장)
    static readonly Vector4[] RIM =   // 지역 가장자리 바위(메사 발치): x, y, 회전, 모양(0 큰 무더기 · 1 작은 돌)
    {
        new Vector4(800, 204, 20, 1), new Vector4(940, 202, -40, 0), new Vector4(1078, 318, 70, 1),
        new Vector4(650, 664, 10, 0), new Vector4(904, 664, 130, 1), new Vector4(1078, 552, 200, 1),
    };
    static readonly Color ROCK = Hex("b06a4a");   // 메사 옆면보다 한 톤 짙은 흙빛(빨강으로 튀지 않게)
    static Mesh Rock(int v) => ToyMesh.Cached("pdRock" + v, m =>
    {
        var re = KK.Hue(0, 40, ROCK, 0.7f);
        if (v == 0) { m.Kk("rocks_B", 20, re); m.AtM(22, 6, 0, 50).Kk("rock_A", 24, re); }
        else { m.Kk("rock_A", 30, re); m.AtM(-13, 4, 0, -30).Kk("rock_A", 18, re); }
        return m;
    });

    static Transform root, rim;
    static readonly List<Transform> parts = new List<Transform>();
    static Material[] guideMat; static Material floodMat, beaconMat;
    static float[] guideB;
    static readonly List<Light> floodLights = new List<Light>();

    static Material Emissive(Color c)
    {
        var emBase = Resources.Load<Material>("Mats/Emissive");
        var m = emBase != null ? new Material(emBase) : Mats.Solid(Color.white);
        m.color = c; if (emBase != null) m.SetColor("_EmissionColor", Color.black);
        return m;
    }
    static void Env(Transform t) { foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = PostFX.ENV_LAYER; }
    static Transform Group(Transform parent, string name, Vector2 at, float ry = 0f)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.position = WorldMap.W2U(at); g.rotation = Quaternion.Euler(0, ry, 0);
        return g;
    }
    static GameObject Put(Transform g, string key, System.Func<ToyMesh, ToyMesh> build, float gloss = 0.12f)
        => ToyMesh.Put(g, key, ToyMesh.Cached(key, build), ToyMesh.Mat(gloss), Vector3.zero);
    static GameObject PutLit(Transform g, string key, System.Func<ToyMesh, ToyMesh> build, Material mat)
    {
        var go = ToyMesh.Put(g, key, ToyMesh.Cached(key, build), mat, Vector3.zero); Prim.NoShadow(go); return go;
    }

    // ───────────────────────── 조립 ─────────────────────────
    public static void Build(Transform parent, System.Func<bool> open)
    {
        parts.Clear(); floodLights.Clear();
        guideMat = new Material[SpacePort.PADS.Length]; guideB = new float[SpacePort.PADS.Length];
        for (int i = 0; i < guideMat.Length; i++) guideMat[i] = Emissive(GUIDE);
        floodMat = Emissive(FLOOD); beaconMat = Emissive(BEACON);

        root = new GameObject("portDress").transform; root.SetParent(parent, false);
        Tanks(); Pipeline(); Yard(); Dish();
        for (int i = 0; i < FLOODS.Length; i++) Flood(i);
        Env(root);
        root.gameObject.SetActive(open());

        // 바위는 지형이라 지역이 열리기 전에도 보인다(잠긴 지역 테두리)
        rim = new GameObject("portRim").transform; rim.SetParent(parent, false);
        foreach (var b in RIM)
        {
            var go = RegionDress.Put(rim, "boulder", Rock((int)b.w), 0.06f);
            go.transform.position = WorldMap.W2U(b.x, b.y); go.transform.rotation = Quaternion.Euler(0, b.z, 0);
        }
        Env(rim);

        // 발자국: 우주항이 열렸을 때만(걷는 길·발판·시설 접근 자리와 겹치지 않게)
        Regions.SetSolid("dressTanks", new Rect(TANKS.x - 16, TANKS.y - 18, 32, 36), open);
        Regions.SetSolid("dressDepot", new Rect(YARD.x - 62, YARD.y - 24, 54, 48), open);
        Regions.SetSolid("dressCrates", new Rect(YARD.x - 8, YARD.y - 18, 46, 40), open);
        Regions.SetSolid("dressTruck", new Rect(YARD.x + 58, YARD.y - 4, 44, 22), open);
        Regions.SetSolid("dressDish", new Rect(DISH.x - 18, DISH.y - 16, 36, 32), open, true);
        for (int i = 0; i < FLOODS.Length; i++) Regions.SetSolid("dressFlood" + i, new Rect(FLOODS[i].x - 8, FLOODS[i].y - 8, 16, 16), open, true);
    }

    // 연료 저장 탱크(북서): 콘크리트 받침 + 초록 탱크 넷(containers_A, 키를 키움) + 북쪽으로 나가는 관
    static void Tanks()
    {
        var g = Group(root, "tanks", TANKS); parts.Add(g);
        Put(g, "pdTanks", m =>
        {
            m.AtM(0, 0, 1.5f).RBox(new Vector3(34, 3f, 38), 4f, SLAB, SLAB_T);
            m.AtM(0, 2, 3f).Kk("containers_A", new Vector3(58, 115, 58));
            m.PipeM(-6, -12, PIPE_H, -6, PIPE_Y - TANKS.y, PIPE_H, 2.2f, KW_D);
            return m;
        });
    }

    // 착륙장 뒤 연료 관: 탱크 → 동쪽 끝(지역 북쪽 띠, 낮게). 받침은 띄엄띄엄, 끝은 마개
    static void Pipeline()
    {
        float x0 = TANKS.x - 6f, x1 = 1050f, cx = (x0 + x1) / 2f;
        var g = Group(root, "pipeline", new Vector2(cx, PIPE_Y)); parts.Add(g);
        Put(g, "pdPipe", m =>
        {
            m.PipeM(x0 - cx, 0, PIPE_H, x1 - cx, 0, PIPE_H, 2.2f, KW_D);
            for (float x = x0 + 30f; x < x1 - 10f; x += 52f)
            {
                m.AtM(x - cx, 0, 1.6f).RBox(new Vector3(4.4f, 3.2f, 7f), 1f, KD, KG);
                m.AtM(x - cx, 0, PIPE_H).Band(2.9f, 1.6f, 10, KD);   // 관 고정 띠
            }
            m.AtM(x1 - cx, 0, PIPE_H - 3.6f).Cyl(3.6f, 3.6f, 7.2f, 12, KD, KG);   // 끝 마개
            return m;
        });
    }

    // 화물 마당(남서): 콘크리트 판 + 창고(cargodepot_C) + 상자 더미(한쪽으로 쏠리게) + 짐 실은 작은 트럭(서쪽 창고를 봄).
    //  KayKit 상자 빨강은 기계 강조색(주황)으로 바꿔 화면에서 튀지 않게
    static readonly System.Func<Color, Color> CRATE = KK.Hue2(330, 360, KO, 0, 18, KO, 0.9f, 0.9f);
    static void Yard()
    {
        var g = Group(root, "yard", YARD); parts.Add(g);
        Put(g, "pdYard", m =>
        {
            m.AtM(-6, 0, 1.2f).RBox(new Vector3(112, 2.4f, 54), 5f, SLAB, SLAB_T);
            m.AtM(-36, 0, 2.4f, 0).Kk("cargodepot_C", new Vector3(26, 24, 22), CRATE);
            m.AtM(4, -6, 2.4f, 6).Kk("cargo_A_stacked", 21, CRATE);
            m.AtM(26, -8, 2.4f, -8).Kk("cargo_B_stacked", 18, CRATE);
            m.AtM(8, 14, 2.4f, 20).Kk("cargo_A", 17, CRATE);
            m.AtM(26, 14, 2.4f, -12).Kk("cargo_B", 15, CRATE);
            return m;
        });
        var truck = Group(root, "truck", YARD + new Vector2(80, 7), 90f); parts.Add(truck);
        Put(truck, "pdTruck", m =>
        {
            m.Kk("spacetruck", 40);
            m.At(0, 8f, 7f, 4).Kk("cargo_A_packed", 17, CRATE);
            return m;
        }, 0.18f);
    }

    // 심우주 안테나: 받침 + 기둥 + 남쪽 하늘로 기운 큰 접시(안쪽 짙게) + 급전 막대 끝 빨간 등
    static void Dish()
    {
        var g = Group(root, "dish", DISH, 62f); parts.Add(g);
        Put(g, "pdDish", m =>
        {
            m.AtM(0, 0, 1.5f).RBox(new Vector3(34, 3f, 30), 4f, SLAB, SLAB_T);
            m.AtM(0, 0, 3f).Cyl(7f, 5.4f, 5f, 14, KD, KG, true, KD);
            m.AtM(0, 0, 8f).Cyl(3.2f, 3f, 14f, 12, KW_D, KW);
            m.AtM(0, 0, 21f).RBox(new Vector3(12f, 6f, 7f), 1.6f, KD, KG);
            var tilt = Quaternion.Euler(-55f, 0, 0);
            m.At(new Vector3(0, 25f, 0), tilt).Cyl(5f, 22f, 8f, 24, KW_D, Hex("aab1b1"), true, KW, true);
            m.At(new Vector3(0, 25f, 0) + tilt * new Vector3(0, 8f, 0), tilt).Ring(20f, 22.6f, 1.4f, 24, KW_D, KW);
            m.At(new Vector3(0, 25f, 0) + tilt * new Vector3(0, 8.2f, 0), tilt).Cyl(4f, 3.4f, 1.2f, 12, KD, KG);
            var tip = new Vector3(0, 25f, 0) + tilt * new Vector3(0, 24f, 0);
            foreach (float a in new[] { 0f, 120f, 240f })
            {   // 급전부를 받치는 다리 셋
                var foot = new Vector3(0, 25f, 0) + tilt * (Quaternion.Euler(0, a, 0) * new Vector3(17f, 8.6f, 0));
                m.Pipe(foot, tip, 0.6f, KD, false, 6);
            }
            m.At(tip, tilt).Cyl(1.8f, 1.4f, 2.4f, 10, KD, KG);
            return m;
        }, 0.2f);
        var tipL = new Vector3(0, 25f, 0) + Quaternion.Euler(-55f, 0, 0) * new Vector3(0, 26.6f, 0);
        var b = PutLit(g, "pdBeacon", m => m.Ball(1.9f, Color.white), beaconMat);
        b.transform.localPosition = tipL * GameConfig.K;
    }

    // 조명탑: 받침 + 가는 기둥 + 꼭대기 등판(2×3 등, 아래로 기울어 착륙장·광장을 봄). KayKit lights는 날개 셋이라 풍차처럼 읽혀서 직접 만든다.
    //  등판은 남쪽(앞)이 정면이고 그룹 회전(FLOOD_RY)으로 비출 쪽을 돌린다. 등만 발광 재질, 밤엔 바닥빛 하나
    const float FLOOD_H = 84f;
    static readonly Quaternion FLOOD_TILT = Quaternion.Euler(30f, 0, 0);   // 윗변이 뒤로 → 앞면이 아래를 봄
    static Vector3 FloodPanel(float x, float y, float z) => new Vector3(0, FLOOD_H, 1.5f) + FLOOD_TILT * new Vector3(x, y, z);
    static void Flood(int i)
    {
        var g = Group(root, "flood", FLOODS[i], FLOOD_RY[i]); parts.Add(g);
        Put(g, "pdFlood", m =>
        {
            m.AtM(0, 0, 1.4f).RBox(new Vector3(16, 2.8f, 16), 3f, SLAB, SLAB_T);
            m.AtM(0, 0, 2.8f).Cyl(4.4f, 3.4f, 5f, 12, KD, KG, true, KD);
            m.AtM(0, 0, 7.8f).Cyl(2f, 1.5f, FLOOD_H - 12f, 10, KW_D, KW, true, KW);
            m.AtM(0, 0, 30f).Band(2.2f, 2.4f, 10, KO);
            m.AtM(0, 0, FLOOD_H - 6f).RBox(new Vector3(5f, 4f, 5f), 1f, KD, KG);
            m.At(FloodPanel(0, 0, 0), FLOOD_TILT).RBox(new Vector3(28f, 16f, 3.4f), 1.4f, KD, KG);
            return m;
        });
        PutLit(g, "pdFloodLamps", m =>
        {
            for (int r = 0; r < 2; r++) for (int c = 0; c < 3; c++)
                m.At(FloodPanel((c - 1) * 8.4f, (r - 0.5f) * 7f, -1.7f), FLOOD_TILT * Quaternion.Euler(-90f, 0, 0)).Cyl(3.3f, 2.7f, 1.2f, 12, Color.white, Color.white);   // 둥근 등(태양판처럼 안 보이게)
            return m;
        }, floodMat);
        var lo = new GameObject("floodLight"); lo.transform.SetParent(g, false); lo.transform.localPosition = new Vector3(0, 40f, -40f) * GameConfig.K;
        var l = lo.AddComponent<Light>(); l.type = LightType.Point; l.color = Hex("ffd9a0"); l.range = 150f * GameConfig.K; l.intensity = 0; l.shadows = LightShadows.None; l.enabled = false;
        floodLights.Add(l);
    }

    // 착륙장마다(padRoot 자식이라 착륙장과 함께 서고 숨는다): 꼭짓점 바깥 유도등 8개 + 북쪽 연료 관 갈래·밸브
    public const float GUIDE_R = 49f;
    public static void PadDress(Transform padRoot, int i)
    {
        var g = new GameObject("padDress").transform; g.SetParent(padRoot, false);
        float dyPipe = PIPE_Y - SpacePort.PADS[i].y;   // 관까지(북쪽이 음수)
        Put(g, "pdPadBase", m =>
        {
            for (int k = 0; k < 8; k++)
            {
                float a = (22.5f + 45f * k) * Mathf.Deg2Rad;
                m.AtM(Mathf.Cos(a) * GUIDE_R, Mathf.Sin(a) * GUIDE_R, 0).Cyl(3.6f, 3.2f, 1.4f, 10, KD, KG);
            }
            // 연료 관 갈래(착륙장 북쪽 판 끝 → 관) + 밸브 상자(초록 손잡이)
            m.PipeM(0, dyPipe, PIPE_H, 0, -50f, PIPE_H, 1.8f, KW_D);
            m.AtM(0, -52f, 3.2f).RBox(new Vector3(9f, 6.4f, 6f), 1.4f, KD, KG);
            m.AtM(0, -52f, 6.4f).Cyl(0.6f, 0.6f, 2.2f, 6, KD, KD);
            m.AtM(0, -52f, 8.6f).Ring(1.4f, 2.6f, 0.9f, 10, FUEL_G, FUEL_G);
            m.AtM(0, dyPipe, PIPE_H).Ball(3.2f, KD, KG);   // 관 이음
            return m;
        });
        PutLit(g, "pdPadLens", m =>
        {
            for (int k = 0; k < 8; k++)
            {
                float a = (22.5f + 45f * k) * Mathf.Deg2Rad;
                m.AtM(Mathf.Cos(a) * GUIDE_R, Mathf.Sin(a) * GUIDE_R, 1.4f).Cyl(2.6f, 1.8f, 2f, 10, Color.white, Color.white);
            }
            return m;
        }, guideMat[i]);
        Env(g);
    }

    // 우주항이 열리면 소품이 하나씩 솟아오른다
    public static void OnPad(string id)
    {
        if (id != "port" || root == null || root.gameObject.activeSelf) return;
        root.gameObject.SetActive(true);
        float d = 0.5f;
        foreach (var p in parts) { JuiceFX.I.Rise(p, d, 18f, 0.6f); d += 0.12f; }
    }

    // ───────────────────────── 불빛 ─────────────────────────
    // state(i): 착륙장 i 화물선 상태(1 하강·4 이륙이면 유도등이 밝아짐, -1 = 착륙장 없음)
    public static void Tick(float dt, System.Func<int, int> state)
    {
        if (root == null || guideMat == null) return;
        float night = DayNight.I != null ? DayNight.I.night : 0f;
        for (int i = 0; i < guideMat.Length; i++)
        {
            int s = state(i); if (s < 0) continue;
            float want = s == 1 || s == 4 ? 1f : 0f;
            guideB[i] = Mathf.MoveTowards(guideB[i], want, dt * 1.5f);
            guideMat[i].SetColor("_EmissionColor", GUIDE * (0.15f + 1.3f * night + 0.9f * guideB[i]));
        }
        if (!root.gameObject.activeSelf) return;
        floodMat.SetColor("_EmissionColor", FLOOD * (night * 1.4f));
        bool on = night > 0.05f;
        foreach (var l in floodLights) { l.enabled = on; l.intensity = night * 1.3f; }
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
        beaconMat.SetColor("_EmissionColor", BEACON * ((0.15f + 0.85f * night) * (0.2f + 0.8f * pulse)));
    }
}

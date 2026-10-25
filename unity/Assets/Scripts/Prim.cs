using System.Collections.Generic;
using UnityEngine;

// 재질·프리미티브 헬퍼. Phase 1은 Kenney 모델 대신 박스 플레이스홀더(§10.2).
// 셰이더는 Resources/Mats/*.mat 에셋을 통해 빌드에 포함시킨다(Editor/ProjectSetup이 만듦).
public static class Mats
{
    static Material _base, _trans;
    static readonly Dictionary<Color, Material> solidCache = new Dictionary<Color, Material>();

    static Material Base
    {
        get
        {
            if (_base == null)
            {
                _base = Resources.Load<Material>("Mats/Base");
                if (_base == null) { _base = new Material(Shader.Find("Standard")); _base.SetFloat("_Glossiness", 0.05f); }
            }
            return _base;
        }
    }
    static Material TransBase
    {
        get
        {
            if (_trans == null)
            {
                _trans = Resources.Load<Material>("Mats/Trans");
                if (_trans == null) _trans = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
            }
            return _trans;
        }
    }

    public static Material Solid(Color c)
    {
        if (solidCache.TryGetValue(c, out var m)) return m;
        m = new Material(Base) { color = c };
        solidCache[c] = m;
        return m;
    }
    // 투명 재질은 개별 인스턴스(알파를 애니메이션하므로 캐시 안 함). 쓰는 쪽이 파괴할 때 Destroy(mat) 할 것
    public static Material Trans(Color c) => new Material(TransBase) { color = c };
    // 알파가 고정인 투명 재질(접지 그림자 등)은 색별로 공유
    static readonly Dictionary<Color, Material> transCache = new Dictionary<Color, Material>();
    public static Material TransShared(Color c)
    {
        if (transCache.TryGetValue(c, out var m)) return m;
        m = new Material(TransBase) { color = c }; transCache[c] = m; return m;
    }
    // 맑은 유리·얼음 겉면(Shaders/MarsGlass): 가운데는 비치고 가장자리 테 + 해 쪽 반짝임. 셰이더가 없으면 예전 투명 재질
    static Shader _glass; static bool glassTried;
    public static Material Glass(Color center, Color edge, float pow = 2.5f, float spec = 0.8f, float gloss = 60f)
    {
        if (!glassTried) { glassTried = true; _glass = Resources.Load<Shader>("Shaders/MarsGlass"); if (_glass != null && !_glass.isSupported) _glass = null; }
        if (_glass == null) return new Material(TransBase) { color = center };
        var m = new Material(_glass);
        m.SetColor("_Color", center); m.SetColor("_EdgeColor", edge); m.SetFloat("_Pow", pow); m.SetFloat("_Spec", spec); m.SetFloat("_Gloss", gloss);
        return m;
    }
    // 매끈한 불투명 재질(얼음 몸통처럼 해 반짝임이 맺히는 것): 색·매끈함별 공유
    static readonly Dictionary<(Color, float), Material> glossCache = new Dictionary<(Color, float), Material>();
    public static Material Gloss(Color c, float smooth)
    {
        if (glossCache.TryGetValue((c, smooth), out var m)) return m;
        m = new Material(Base) { color = c }; m.SetFloat("_Glossiness", smooth); glossCache[(c, smooth)] = m; return m;
    }
}

public static class Prim
{
    static GameObject Make(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Mats.Solid(c);
        return go;
    }
    // 크기는 논리 단위(×K 적용), 위치는 Unity 단위
    public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 sizeL, Color c) => Make(PrimitiveType.Cube, parent, name, pos, sizeL * GameConfig.K, c);
    public static GameObject Cyl(Transform parent, string name, Vector3 pos, float dL, float hL, Color c) => Make(PrimitiveType.Cylinder, parent, name, pos, new Vector3(dL, hL / 2, dL) * GameConfig.K, c);
    public static GameObject Sphere(Transform parent, string name, Vector3 pos, float dL, Color c) => Make(PrimitiveType.Sphere, parent, name, pos, Vector3.one * dL * GameConfig.K, c);
    public static GameObject Capsule(Transform parent, string name, Vector3 pos, float dL, float hL, Color c) => Make(PrimitiveType.Capsule, parent, name, pos, new Vector3(dL, hL / 2, dL) * GameConfig.K, c);

    public static void NoShadow(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
    }

    // 접지 그림자: 반투명 원판 (§7.2)
    public static GameObject BlobShadow(Transform parent, float dL)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "blob";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0.01f, 0);
        go.transform.localScale = new Vector3(dL, 0.2f, dL) * GameConfig.K;
        var c = GameConfig.Hex("3a1206"); c.a = 0.3f;
        go.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(c);
        NoShadow(go);
        return go;
    }

    // 평평한 고리 메시(링 FX·타깃 링용)
    static Mesh _ring;
    public static Mesh RingMesh()
    {
        if (_ring != null) return _ring;
        int n = 32; float r0 = 0.8f, r1 = 1f;
        var v = new Vector3[n * 2]; var tri = new int[n * 6]; var nor = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2 / n; float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            v[i * 2] = new Vector3(ca * r0, 0, sa * r0); v[i * 2 + 1] = new Vector3(ca * r1, 0, sa * r1);
            nor[i * 2] = nor[i * 2 + 1] = Vector3.up;
            int j = (i + 1) % n;
            tri[i * 6] = i * 2; tri[i * 6 + 1] = j * 2 + 1; tri[i * 6 + 2] = i * 2 + 1;
            tri[i * 6 + 3] = i * 2; tri[i * 6 + 4] = j * 2; tri[i * 6 + 5] = j * 2 + 1;
        }
        _ring = new Mesh { vertices = v, triangles = tri, normals = nor };
        return _ring;
    }
    public static GameObject Ring(Transform parent, Vector3 pos, float rL, Color c)
    {
        var go = new GameObject("ring");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * rL * GameConfig.K;
        go.AddComponent<MeshFilter>().sharedMesh = RingMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mats.Trans(c);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return go;
    }
}

// 아이템 시각(§2.2): 종류별 미니 모형(ToyMesh 한 메시 + 정점 색 재질 하나, 같은 종류끼리 인스턴싱).
//  물병 = 몸통·라벨·어깨·뚜껑 / 산소통 = 흰 실린더·파란 띠·밸브 / 식량 = 감자 상자 / 흙·비료 = 자루 / 레골리스 = 붉은 돌 / 얼음 = 깎은 얼음 덩어리
//  병입기 받침대·카운터 선반·정착민이 받아 드는 것·날아가는 것(ArcFlyer)이 모두 Slab 하나를 쓴다. 지폐·동전은 예전 판 그대로.
//  피벗은 모형 높이 ITEM_H 의 가운데(SyncStack 이 h/2 + 층×h 에 둔다). GAP 은 더미 간격 권장값(모형 발자국 + 틈)
public static class Items
{
    public static readonly Dictionary<string, float> ITEM_H = new Dictionary<string, float> { { "ice", 5.2f }, { "bottle", 8.0f }, { "bill", 3.2f }, { "coin", 1.8f }, { "rego", 5.0f }, { "soil", 6.0f }, { "fert", 6.0f }, { "o2", 8.4f }, { "food", 5.6f } };
    static readonly Dictionary<string, Vector2> GAP = new Dictionary<string, Vector2> {
        { "ice", new Vector2(10.5f, 8.5f) }, { "bottle", new Vector2(7.6f, 7.6f) }, { "o2", new Vector2(7.4f, 7.4f) }, { "food", new Vector2(11.5f, 9.2f) },
        { "soil", new Vector2(10.5f, 8.5f) }, { "fert", new Vector2(10.5f, 8.5f) }, { "rego", new Vector2(11.5f, 9.5f) }, { "bill", new Vector2(16f, 12f) }, { "coin", new Vector2(10f, 10f) } };
    public static Vector2 Gap(string kind) => GAP.TryGetValue(kind, out var g) ? g : new Vector2(15, 11);
    public static Color ColorOf(string kind)
    {
        switch (kind)
        {
            case "ice": return GameConfig.ICE;
            case "bottle": return GameConfig.BOTTLE;
            case "bill": return GameConfig.BILL;
            case "rego": return GameConfig.REGO;
            case "soil": return GameConfig.SOIL;
            case "fert": return GameConfig.FERT;
            case "o2": return GameConfig.O2;
            case "food": return GameConfig.FOOD;
            default: return GameConfig.GOLD;
        }
    }
    public static GameObject Slab(Transform parent, string kind)
    {
        if (kind == "coin") return Prim.Cyl(parent, kind, Vector3.zero, 9, 1.8f, GameConfig.GOLD);
        if (kind == "bill" || !ITEM_H.ContainsKey(kind)) return Prim.Box(parent, kind, Vector3.zero, new Vector3(14, 2.4f, 10), ColorOf(kind));
        return ToyMesh.Put(parent, kind, ItemMesh(kind), MatOf(kind), Vector3.zero);
    }
    // 정착민이 받아 든 물건 자리(손 위 2열로 쌓기, hold 로컬 단위): 키 큰 병을 한 줄로 쌓으면 머리 위로 탑이 솟았다
    public static Vector3 HoldOffset(string kind, int idx)
    {
        float k = GameConfig.K, h = ITEM_H.TryGetValue(kind, out var hh) ? hh : 3f;
        float gx = Mathf.Min(Gap(kind).x, 10f);
        return new Vector3(((idx % 2) - 0.5f) * gx * k, (idx / 2) * h * k, 0);
    }

    // ---- 색 ----
    // 얼음(목표 그림: 반투명 하늘색 각진 덩어리 + 흰 모서리 + 안쪽이 진한 코어). 불투명 정점 색 그라데이션이라 정렬 깜빡임이 없고,
    // 햇빛을 받아도 블룸 문턱(2.4)에 안 닿게 가장 밝은 모서리도 흰색 1.0 이하
    //  (햇빛·ACES 가 밝은 하늘색을 회백색으로 바래게 해서 몸통은 한 톤 진하게, 흰빛은 깎은 모서리에만)
    //  알파 = 비침 양(Shaders/MarsIce, r6): 몸통은 비치고 깎은 위 모서리는 조금만·반사 줄은 안 비쳐 흰빛이 남는다
    public static readonly Color ICE_FACE = GameConfig.Hex("74c8f0"), ICE_CORE = GameConfig.Hex("3f9fdc"), ICE_EDGE = WithA(GameConfig.Hex("e2f6ff"), 0.3f),
        ICE_TOP = GameConfig.Hex("98d8f6"), ICE_TOP_CORE = GameConfig.Hex("5db7e8"),
        ICE_GLINT = WithA(GameConfig.Hex("f2fbff"), 0f);   // 윗면 사선 반사 줄(ToyMesh.Glint)
    static Color WithA(Color c, float a) { c.a = a; return c; }
    static readonly Color BOT_D = GameConfig.Hex("4fb0e6"), BOT = GameConfig.Hex("6fc3f0"), BOT_L = GameConfig.Hex("9ad7f6"), LABEL = GameConfig.Hex("f4f1ea"), BOT_CAP = GameConfig.Hex("2f6fb3");
    static readonly Color O2_D = GameConfig.Hex("d5e2ee"), O2_BAND = GameConfig.Hex("4fb0f0"), O2_VALVE = GameConfig.Hex("7d8894");
    static readonly Color CRATE = GameConfig.Hex("e08a45"), CRATE_D = GameConfig.Hex("b9682f"), CRATE_L = GameConfig.Hex("f0a865"), CRATE_IN = GameConfig.Hex("8a4a22");
    static readonly Color FOOD_D = GameConfig.Hex("d39a2c"), FOOD_L = GameConfig.Hex("f8c95e"), FOOD_IN = GameConfig.Hex("9a6420");
    static readonly Color POTATO_T = GameConfig.Hex("dcae6c"), POTATO = GameConfig.Hex("c99a5b"), POTATO_D = GameConfig.Hex("a3753f");
    // 레골리스 돌: 윗면 밝은 붉은 갈색 → 옆 붉은 갈색 두 톤 → 아래 짙은 적갈(햇빛·ACES 에 주황으로 바래지 않게 한 톤 진하게)
    public static readonly Color REGO_T = GameConfig.Hex("c45f3a"), REGO_M = GameConfig.Hex("9c3f26"), REGO_A = GameConfig.Hex("86351f"), REGO_D = GameConfig.Hex("5a2216");
    static readonly Color SOIL_L = GameConfig.Hex("86613f"), SOIL_D = GameConfig.Hex("553a27"), SOIL_TIE = GameConfig.Hex("d8b88a");
    static readonly Color FERT_L = GameConfig.Hex("a8c45e"), FERT_D = GameConfig.Hex("6f8a34"), FERT_TIE = GameConfig.Hex("f6e6d2");

    public static Material MatOf(string kind) => kind == "ice" ? IceMat : kind == "bottle" || kind == "o2" ? ToyMesh.Mat(0.45f) : ToyMesh.Mat(0.1f);
    // 얼음 재질(r6): 뒤가 비치는 불투명 얼음(Shaders/MarsIce: 비침·속 깊이·가장자리 밝힘). 셰이더가 없으면 예전 정점 색 재질
    static Material _ice; static bool iceTried;
    public static Material IceMat
    {
        get
        {
            if (_ice != null) return _ice;
            if (!iceTried) { iceTried = true; var sh = Resources.Load<Shader>("Shaders/MarsIce"); if (sh != null && sh.isSupported) _ice = new Material(sh) { enableInstancing = true }; }
            return _ice != null ? _ice : ToyMesh.Mat(0.55f, 0.22f, 0.16f);
        }
    }

    // ---- 미니 모형(피벗 = 높이 가운데) ----
    public static Mesh ItemMesh(string kind) => ToyMesh.Cached("item_" + kind, m =>
    {
        float h = ITEM_H[kind], y0 = -h / 2f;
        switch (kind)
        {
            case "bottle":   // 물병: 진한 아랫몸 → 크림 라벨 → 밝은 윗몸 → 어깨 → 남색 뚜껑
                m.At(0, y0, 0).Cyl(3.1f, 3.1f, 2.2f, 12, BOT_D, BOT_D, true, BOT);
                m.At(0, y0 + 2.2f, 0).Band(3.2f, 1.8f, 12, LABEL);
                m.At(0, y0 + 4.0f, 0).Cyl(3.1f, 3.1f, 1.3f, 12, BOT, BOT, true, BOT_L);
                m.At(0, y0 + 5.3f, 0).Cyl(3.1f, 1.5f, 1.2f, 12, BOT_L, BOT_L);
                m.At(0, y0 + 6.5f, 0).Cyl(1.75f, 1.75f, 1.5f, 10, BOT_CAP, BOT_CAP);
                return m;
            case "o2":       // 산소통: 흰 실린더 + 파란 띠 + 둥근 어깨 + 회색 밸브
                m.At(0, y0, 0).Cyl(3f, 3f, 5.4f, 12, O2_D, GameConfig.O2, true, GameConfig.O2);
                m.At(0, y0 + 1.2f, 0).Band(3.12f, 1.4f, 12, O2_BAND);
                m.At(0, y0 + 5.4f, 0).Cyl(3f, 2f, 1.1f, 12, GameConfig.O2, GameConfig.O2);
                m.At(0, y0 + 6.5f, 0).Cyl(2f, 0.9f, 0.6f, 12, GameConfig.O2, GameConfig.O2);
                m.At(0, y0 + 7.1f, 0).Cyl(0.8f, 0.8f, 0.6f, 8, O2_VALVE, O2_VALVE);
                m.At(0, y0 + 7.9f, 0).Box(new Vector3(2.6f, 0.9f, 0.9f), O2_VALVE);
                return m;
            case "food":     // 감자 상자: 깎은 노란 상자(안은 진하게) + 감자 4알
                m.At(0, y0 + 1.7f, 0).Bevel(new Vector3(10, 3.4f, 8), 0.6f, FOOD_D, GameConfig.FOOD, FOOD_L, FOOD_IN, FOOD_IN);
                for (int i = 0; i < 4; i++)
                    m.At((i % 2 - 0.5f) * 4.6f, y0 + 4.0f, (i / 2 - 0.5f) * 3.6f).Blob(new Vector3(2.3f, 1.5f, 1.9f), 0, 31 + i, 0.15f, POTATO_T, POTATO, POTATO_D);
                return m;
            case "soil":
            case "fert":     // 자루: 아주 둥글게 깎은 몸통 + 묶은 목 + 매듭
            {
                bool s = kind == "soil";
                Color body = s ? GameConfig.SOIL : GameConfig.FERT, lt = s ? SOIL_L : FERT_L, dk = s ? SOIL_D : FERT_D, tie = s ? SOIL_TIE : FERT_TIE;
                m.At(0, y0 + 2.3f, 0).Bevel(new Vector3(9, 4.6f, 7), 1.7f, body, dk, lt, lt, body);
                m.At(0, y0 + 4.5f, 0).Cyl(1.9f, 1.2f, 0.8f, 8, body, body);
                m.At(0, y0 + 5.1f, 0).Cyl(1.3f, 1.5f, 0.9f, 8, tie, tie);
                return m;
            }
            case "rego":     // 레골리스: 붉은 저폴리 돌 + 작은 조각
                m.At(0, y0 + 1.4f, 0).Blob(new Vector3(5f, 2.7f, 4f), 1, 7, 0.2f, REGO_T, REGO_M, REGO_D, REGO_A, 0.5f);
                m.At(3.4f, y0 + 1.0f, -2f).Blob(new Vector3(2.2f, 1.6f, 2f), 0, 9, 0.2f, REGO_T, REGO_A, REGO_D);
                return m;
            case "ice":      // 얼음: 깎은 하늘색 덩어리(가운데 진하고 모서리 흰빛)
                m.At(0, -0.2f, 0).Bevel(new Vector3(9, 4.6f, 7), 0.9f, ICE_FACE, ICE_CORE, ICE_EDGE, ICE_TOP, ICE_TOP_CORE).Glint(new Vector3(9, 4.6f, 7), ICE_GLINT);
                return m;
        }
        return m.Box(new Vector3(10, h, 8), ColorOf(kind));
    });

    // 등짐 탑 전용 블록(피자레디처럼 등 뒤에 높이 쌓는 두툼한 덩어리). 한 층 CARRY_H 논리, 피벗은 블록 중심.
    // 층마다 위쪽 모서리를 깎아 밝게 해 쌓인 층이 줄무늬처럼 또렷하게 읽힌다. 블록 하나 = 메시 하나(예전 병 상자는 프리미티브 14개였다)
    public const float CARRY_H = 5.0f;
    public static GameObject CarryBlock(Transform parent, string kind)
    {
        var go = ToyMesh.Put(parent, kind, CarryMesh(kind), MatOf(kind), Vector3.zero);
        return go;
    }
    static Mesh CarryMesh(string kind) => ToyMesh.Cached("carry_" + kind, m =>
    {
        float h = CARRY_H, y0 = -h / 2f;
        switch (kind)
        {
            case "ice":
                m.At(0, -0.25f, 0).Bevel(new Vector3(17, h - 0.5f, 12.5f), 1.2f, ICE_FACE, ICE_CORE, ICE_EDGE, ICE_TOP, ICE_TOP_CORE).Glint(new Vector3(17, h - 0.5f, 12.5f), ICE_GLINT);
                return m;
            case "bottle":   // 나무 상자 + 병 6개(뚜껑이 위로)
                m.At(0, y0 + 1.6f, 0).Bevel(new Vector3(17, 3.2f, 12.5f), 0.6f, CRATE, CRATE_D, CRATE_L, CRATE_IN, CRATE_IN);
                for (int r = 0; r < 2; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        float x = (c - 1) * 5f, z = (r - 0.5f) * 5.6f;
                        m.At(x, y0 + 2.4f, z).Cyl(1.9f, 1.9f, 1.4f, 10, BOT, BOT_L);
                        m.At(x, y0 + 3.8f, z).Cyl(1.9f, 1.0f, 0.5f, 10, BOT_L, BOT_L);
                        m.At(x, y0 + 4.3f, z).Cyl(1.15f, 1.15f, 0.6f, 8, BOT_CAP, BOT_CAP);
                    }
                return m;
            case "rego":     // 붉은 돌덩이 셋
                m.At(0, -0.6f, 0).Blob(new Vector3(8.4f, 2.6f, 6f), 1, 3, 0.18f, REGO_T, REGO_M, REGO_D, REGO_A, 0.6f);
                m.At(4f, 0.9f, -1f).Blob(new Vector3(4f, 1.5f, 3.4f), 0, 5, 0.2f, REGO_T, REGO_A, REGO_D);
                m.At(-4.5f, 0.8f, 2f).Blob(new Vector3(3.6f, 1.4f, 3f), 0, 11, 0.2f, REGO_T, REGO_M, REGO_D);
                return m;
            case "soil":
            case "fert":
            {
                bool s = kind == "soil";
                Color body = s ? GameConfig.SOIL : GameConfig.FERT, lt = s ? SOIL_L : FERT_L, dk = s ? SOIL_D : FERT_D, tie = s ? SOIL_TIE : FERT_TIE;
                m.At(0, -0.4f, 0).Bevel(new Vector3(16.5f, h - 1f, 12.5f), 1.9f, body, dk, lt, lt, body);
                m.At(5.5f, h / 2 - 1.3f, 0).Cyl(1.9f, 1.6f, 1.4f, 8, tie, tie);
                return m;
            }
            case "o2":       // 산소통 2개를 눕혀 묶고 파란 끈, 끝에 밸브
                for (int s = -1; s <= 1; s += 2)
                {
                    m.At(new Vector3(7.6f, -0.2f, s * 3.2f), new Vector3(0, 0, 90)).Cyl(3f, 3f, 15.2f, 12, GameConfig.O2, O2_D, true, O2_D);
                    m.At(new Vector3(7.6f, -0.2f, s * 3.2f), new Vector3(0, 0, -90)).Cyl(1f, 1f, 1.4f, 8, O2_VALVE, O2_VALVE);
                }
                m.At(0, -0.2f, 0).Box(new Vector3(3.2f, h - 0.2f, 13.6f), O2_BAND);
                return m;
            case "food":     // 노란 나무 상자 + 감자 6알
                m.At(0, y0 + 1.6f, 0).Bevel(new Vector3(17, 3.2f, 12.5f), 0.6f, FOOD_D, GameConfig.FOOD, FOOD_L, FOOD_IN, FOOD_IN);
                for (int r = 0; r < 2; r++)
                    for (int c = 0; c < 3; c++)
                        m.At((c - 1) * 5f, y0 + 3.4f, (r - 0.5f) * 5.4f).Blob(new Vector3(2.4f, 1.5f, 2.1f), 0, 40 + r * 3 + c, 0.15f, POTATO_T, (r + c) % 2 == 0 ? POTATO : POTATO_D, POTATO_D);
                return m;
        }
        return m.Bevel(new Vector3(17, h - 0.4f, 12.5f), 0.8f, ColorOf(kind), ColorOf(kind), Color.Lerp(ColorOf(kind), Color.white, 0.3f), ColorOf(kind), ColorOf(kind));
    });

    // 컨테이너 안 스택을 count에 맞춰 재구성(간단한 재빌드; 수십 개 수준이라 충분)
    public static void SyncStack(Transform anchor, string kind, int count, int cols, int rows, float gapX, float gapZ, int max)
    {
        count = Mathf.Min(count, max);
        // Destroy는 프레임 끝에 실행되므로 먼저 떼어내야 childCount가 줄어든다
        for (int i = anchor.childCount - 1; i >= count; i--) { var c = anchor.GetChild(i); c.SetParent(null, false); Object.Destroy(c.gameObject); }
        for (int i = anchor.childCount; i < count; i++) Slab(anchor, kind);
        float h = ITEM_H[kind] * GameConfig.K;
        for (int i = 0; i < anchor.childCount; i++)
        {
            int layer = i / (cols * rows); int r = (i / cols) % rows; int c = i % cols;
            float ox = (c - (cols - 1) / 2f) * gapX * GameConfig.K;
            float oz = (r - (rows - 1) / 2f) * gapZ * GameConfig.K;
            anchor.GetChild(i).localPosition = new Vector3(ox, h / 2 + layer * h, oz);
        }
    }
}

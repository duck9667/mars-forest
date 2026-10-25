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

// 아이템(등짐 판) 시각: 납작 판 14×2.4×10 (§2.2)
public static class Items
{
    public static readonly Dictionary<string, float> ITEM_H = new Dictionary<string, float> { { "ice", 2.8f }, { "bottle", 3.0f }, { "bill", 3.2f }, { "coin", 1.8f }, { "rego", 3.0f }, { "soil", 3.0f }, { "fert", 3.4f }, { "o2", 3.6f }, { "food", 3.2f } };
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
        return Prim.Box(parent, kind, Vector3.zero, new Vector3(14, 2.4f, 10), ColorOf(kind));
    }
    // 등짐 탑 전용 블록(피자레디처럼 등 뒤에 높이 쌓는 두툼한 덩어리). 한 층 CARRY_H 논리, 피벗은 블록 중심.
    // 얼음: 하늘색 얼음 블록 + 밝은 윗면 + 서리 띠 / 병: 나무 상자 + 병 6개(뚜껑이 위로 보이게)
    // 생존 기지: 레골리스 붉은 돌덩이 / 흙 갈색 자루 / 비료 초록 자루 / 산소통 흰 실린더 2개+파란 띠 / 식량 감자 상자
    public const float CARRY_H = 5.0f;
    static readonly Color ICE_BODY = GameConfig.Hex("8fd4f5"), ICE_TOP = GameConfig.Hex("f2fbff");
    static readonly Color CRATE = GameConfig.Hex("e08a45"), CRATE_LIP = GameConfig.Hex("a8582a"), CAP = GameConfig.Hex("ffffff");
    public static GameObject CarryBlock(Transform parent, string kind)
    {
        var root = new GameObject(kind); root.transform.SetParent(parent, false);
        var t = root.transform; float k = GameConfig.K, h = CARRY_H;
        // 층마다 윗면 테두리(립)를 몸통보다 넓게 둬서 쌓인 층이 줄무늬처럼 또렷하게 읽히게 한다(같은 폭이면 외곽선이 안 생김)
        if (kind == "bottle")
        {
            float ch = 3.2f;   // 상자 높이, 나머지는 병목·뚜껑
            Prim.Box(t, "crate", new Vector3(0, (-h / 2 + ch / 2) * k, 0), new Vector3(17, ch, 12.5f), CRATE);
            Prim.Box(t, "lip", new Vector3(0, (-h / 2 + ch - 0.4f) * k, 0), new Vector3(18, 0.8f, 13.5f), CRATE_LIP);
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                {
                    float x = (c - 1) * 5f, z = (r - 0.5f) * 5.6f;
                    Prim.Cyl(t, "b", new Vector3(x * k, (-h / 2 + ch + 0.5f) * k, z * k), 3.8f, 1.4f, GameConfig.BOTTLE);
                    Prim.Cyl(t, "cap", new Vector3(x * k, (h / 2 - 0.3f) * k, z * k), 2.4f, 0.6f, CAP);
                }
        }
        else if (kind == "ice")
        {
            Prim.Box(t, "ice", new Vector3(0, -0.5f * k, 0), new Vector3(17, h - 1f, 12.5f), ICE_BODY);
            Prim.Box(t, "top", new Vector3(0, (h / 2 - 0.5f) * k, 0), new Vector3(18, 1f, 13.5f), ICE_TOP);
        }
        else if (kind == "rego")
        {
            // 레골리스: 붉은 돌덩이(각지게 비튼 덩어리 셋)
            Prim.Box(t, "rock", new Vector3(0, -0.4f * k, 0), new Vector3(16, h - 1.2f, 12), GameConfig.REGO).transform.localRotation = Quaternion.Euler(0, 8, 3);
            Prim.Box(t, "chunk", new Vector3(4f * k, (h / 2 - 1f) * k, -1f * k), new Vector3(8, 2.6f, 7), REGO_D).transform.localRotation = Quaternion.Euler(6, 35, 0);
            Prim.Box(t, "chunk", new Vector3(-4.5f * k, (h / 2 - 1.2f) * k, 2f * k), new Vector3(7, 2.2f, 6), REGO_L).transform.localRotation = Quaternion.Euler(-5, -20, 4);
        }
        else if (kind == "soil" || kind == "fert") Sack(t, kind == "soil" ? GameConfig.SOIL : GameConfig.FERT, kind == "soil" ? SOIL_TIE : FERT_TIE, kind == "fert");
        else if (kind == "o2")
        {
            // 산소통: 흰 실린더 2개를 눕혀 묶고 파란 띠, 끝에 밸브
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Cyl(t, "tank", new Vector3(0, -0.2f * k, s * 3.2f * k), 6.2f, 16, GameConfig.O2).transform.localRotation = Quaternion.Euler(0, 0, 90);
                Prim.Cyl(t, "valve", new Vector3(8.6f * k, -0.2f * k, s * 3.2f * k), 2.4f, 1.6f, O2_VALVE).transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            Prim.Box(t, "band", new Vector3(0, -0.2f * k, 0), new Vector3(3.2f, h - 0.2f, 13.4f), O2_BAND);
        }
        else if (kind == "food")
        {
            // 식량: 노란 나무 상자 + 감자 6알
            float ch = 3.2f;
            Prim.Box(t, "crate", new Vector3(0, (-h / 2 + ch / 2) * k, 0), new Vector3(17, ch, 12.5f), GameConfig.FOOD);
            Prim.Box(t, "lip", new Vector3(0, (-h / 2 + ch - 0.4f) * k, 0), new Vector3(18, 0.8f, 13.5f), FOOD_LIP);
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                    Prim.Sphere(t, "potato", new Vector3((c - 1) * 5f * k, (-h / 2 + ch + 0.6f) * k, (r - 0.5f) * 5.4f * k), 4.6f, (r + c) % 2 == 0 ? POTATO : POTATO_D);
        }
        else Prim.Box(t, kind, Vector3.zero, new Vector3(17, h - 0.4f, 12.5f), ColorOf(kind));
        return root;
    }
    static readonly Color REGO_D = GameConfig.Hex("8f3d20"), REGO_L = GameConfig.Hex("d4703f"), SOIL_TIE = GameConfig.Hex("c9a77c"), FERT_TIE = GameConfig.Hex("f6e6d2");
    static readonly Color O2_VALVE = GameConfig.Hex("8c98a4"), O2_BAND = GameConfig.Hex("4fb0f0"), FOOD_LIP = GameConfig.Hex("b9802a"), POTATO = GameConfig.Hex("c99a5b"), POTATO_D = GameConfig.Hex("a87a44");
    // 자루: 불룩한 몸통(가운데가 살짝 더 넓게) + 묶은 매듭, 비료는 흰 라벨 띠
    static void Sack(Transform t, Color body, Color tie, bool label)
    {
        float k = GameConfig.K, h = CARRY_H;
        Prim.Box(t, "sack", new Vector3(0, -0.3f * k, 0), new Vector3(16, h - 1f, 12), body);
        Prim.Box(t, "belly", new Vector3(0, -0.6f * k, 0), new Vector3(17.5f, h - 2.6f, 13), body);
        Prim.Box(t, "tie", new Vector3(5.5f * k, (h / 2 - 0.5f) * k, 0), new Vector3(4, 1.6f, 4), tie);
        if (label) Prim.Box(t, "label", new Vector3(0, -0.4f * k, 0), new Vector3(6, 2.4f, 13.4f), tie);
    }

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

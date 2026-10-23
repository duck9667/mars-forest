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
    // 투명 재질은 개별 인스턴스(알파를 애니메이션하므로 캐시 안 함)
    public static Material Trans(Color c) => new Material(TransBase) { color = c };
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
        go.GetComponent<Renderer>().sharedMaterial = Mats.Trans(c);
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
    public static readonly Dictionary<string, float> ITEM_H = new Dictionary<string, float> { { "ice", 2.8f }, { "bottle", 3.0f }, { "bill", 3.2f }, { "coin", 1.8f } };
    public static Color ColorOf(string kind)
    {
        switch (kind)
        {
            case "ice": return GameConfig.ICE;
            case "bottle": return GameConfig.BOTTLE;
            case "bill": return GameConfig.BILL;
            default: return GameConfig.GOLD;
        }
    }
    public static GameObject Slab(Transform parent, string kind)
    {
        var go = Prim.Box(parent, kind, Vector3.zero, new Vector3(14, 2.4f, 10), ColorOf(kind));
        if (kind == "coin") { Object.Destroy(go); go = Prim.Cyl(parent, kind, Vector3.zero, 9, 1.8f, GameConfig.GOLD); }
        return go;
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

using System.Collections.Generic;
using UnityEngine;

// 역면(hull) 외곽선 헬퍼 (원본 index.html 433행 hull()). 기본 외곽선은 PostFX 의 깊이 엣지가 모든 물체에 일괄로 그리므로,
// 이 헬퍼는 색을 따로 줘야 하는 물체에만 쓴다 — 예: VIP 손님의 금색 0xd49000 (Customers 쪽에서 HullOutline.Hull(view, HullOutline.GOLD, 2f) 호출).
// 플랫 셰이딩 메시(프리미티브·ModelLib)는 정점 노멀이 면마다 갈라져 있어 그대로 밀어내면 모서리가 벌어진다 → 같은 위치의 정점 노멀을
// 평균한 사본 메시를 만들어(메시별 캐시) 밀어낸다.
public static class HullOutline
{
    public static readonly Color NAVY = GameConfig.OUTLINE;
    public static readonly Color GOLD = GameConfig.Hex("d49000");

    static Shader _sh;
    static readonly Dictionary<Mesh, Mesh> smooth = new Dictionary<Mesh, Mesh>();
    static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();

    static Material Mat(Color c)
    {
        if (mats.TryGetValue(c, out var m)) return m;
        if (_sh == null) _sh = Resources.Load<Shader>("Shaders/MarsOutlineHull") ?? Shader.Find("Mars/OutlineHull");
        m = new Material(_sh) { color = c, name = "hull" };
        mats[c] = m;
        return m;
    }

    // root 아래 모든 불투명 MeshRenderer 에 hull 자식을 단다. thickL: 두께(논리 단위, 원본 1.4~2.2)
    public static void Hull(Transform root, Color color, float thickL = 1.8f)
    {
        var mat = Mat(color);
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.name == "hull" || mr.sharedMaterial == null) continue;
            if (mr.sharedMaterial.renderQueue >= 3000) continue;   // 투명(접지 그림자·링)은 원본처럼 제외
            var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            if (mr.transform.Find("hull") != null) continue;
            var go = new GameObject("hull");
            go.transform.SetParent(mr.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Smooth(mf.sharedMesh);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var pb = new MaterialPropertyBlock(); pb.SetFloat("_Thick", thickL * GameConfig.K); r.SetPropertyBlock(pb);
        }
    }

    public static void RemoveHull(Transform root)
    {
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true)) if (mr.name == "hull") Object.Destroy(mr.gameObject);
    }

    // 같은 위치(1e-4 반올림)의 정점 노멀을 면적 가중 평균한 사본. 서브메시는 하나로 합친다(hull 은 단색이라 상관없음)
    static Mesh Smooth(Mesh src)
    {
        if (smooth.TryGetValue(src, out var m)) return m;
        var v = src.vertices; var tri = src.triangles;
        var acc = new Dictionary<Vector3Int, Vector3>(); var key = new Vector3Int[v.Length];
        for (int i = 0; i < v.Length; i++) key[i] = new Vector3Int(Mathf.RoundToInt(v[i].x * 1e4f), Mathf.RoundToInt(v[i].y * 1e4f), Mathf.RoundToInt(v[i].z * 1e4f));
        for (int t = 0; t < tri.Length; t += 3)
        {
            int a = tri[t], b = tri[t + 1], c = tri[t + 2];
            var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);   // 길이 = 면적×2 (가중치)
            foreach (int i in new[] { a, b, c }) { acc.TryGetValue(key[i], out var s); acc[key[i]] = s + fn; }
        }
        var n = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++) { var s = acc[key[i]]; n[i] = s.sqrMagnitude > 1e-12f ? s.normalized : Vector3.up; }
        m = new Mesh { name = src.name + "_hull", indexFormat = src.indexFormat };
        m.vertices = v; m.triangles = tri; m.normals = n;
        m.RecalculateBounds();
        smooth[src] = m;
        return m;
    }
}

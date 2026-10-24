using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §3.4 매장 장식 시각물. 원본 bCounter(607~619행: 차양·기둥·깃발·줄전구·네온) + plantG(713행) + applyDeco(716행)의 Unity 판.
// - 항상 있는 것: 카운터 앞 줄무늬 차양(주황·크림, 가리비 늘어뜨림, 기둥 2개) + 매장 깃발 3개(주황)
// - 장식: awning 줄무늬 파랑 / flag 깃발 보라 / neon 차양 위 네온 간판(밤 발광·깜빡임) / plant 네 모서리 선인장 / lights 차양 아래 줄전구 11개(밤 발광·물결 점멸·흔들림) / tile 매장 바닥 체크무늬
// 배치 원칙: 카메라 고도 55°라 높이 h는 화면에서 h·0.70만큼 북쪽으로 올라가 보인다(논리 y − 0.70h = 투영 y).
// 카운터 뒤(북쪽: 플레이어·선반 병, 투영 y ≈ 445~490)를 가리지 않도록 차양은 카운터 앞(손님 쪽, y 534~556)에 h 55~62로 낮게 내밀어
// 투영 y 490~523에 놓이게 하고, 네온은 차양 앞단에 뒤로 35° 눕혀 높이 21로 세운다(투영 y 490~515).
public class ShopDecor : MonoBehaviour
{
    GameState G => GameState.I;
    const float K = GameConfig.K;
    static Vector3 P(float x, float y, float h) => WorldMap.W2U(x, y, h);
    static Vector3 L(float x, float h, float south) => new Vector3(x * K, h * K, -south * K);   // 그룹 로컬(논리): x 동쪽, h 위, south 남쪽(+)
    static Color Hex(string h) => GameConfig.Hex(h);

    // 원본 COL·stripes: 기본 차양 f6ede0/f07a3a, 파란 차양 ffffff/4fb0f0, 깃발 주황 → 보라(COL.purple)
    static readonly Color AWN_A0 = Hex("f6ede0"), AWN_B0 = Hex("f07a3a"), AWN_A1 = Hex("ffffff"), AWN_B1 = Hex("4fb0f0");
    static readonly Color FLAG0 = Hex("f07a3a"), FLAG1 = Hex("8a5cd6");
    static readonly Color POLE = Hex("6e3f26"), DARK = Hex("2b2420"), GOLDC = Hex("ffc83d"), STEEL = Hex("8c98a4");
    static readonly Color GREEN = Hex("5cc45e"), GREEN_D = Hex("2f7a33"), PINK = Hex("ff8ad8");
    static readonly Color NEON_EP = Hex("ff2fa8"), NEON_ET = Hex("16e0c4"), NEON_EG = Hex("ffb020");

    // 차양: 카운터(178,504) 앞. 뒤 모서리 y 534·h 62 → 앞 모서리 y 556·h 55 (앞으로 17° 숙임), 폭 236, 줄무늬 21개
    const float CX = 178f, AWN_Y = 534f, AWN_H = 62f, AWN_D = 23f, AWN_W = 236f, TILT = 17f; const int NS = 21;
    const float POLE_Y = 548f; static readonly float[] POLE_X = { 72f, 284f };
    const float NEON_X = 136f;                                                                // 줄 맨 앞 손님(x 216) 말풍선을 피해 서쪽으로
    static readonly Vector2 FLAG3 = new Vector2(34f, 500f);                                   // 매장 서쪽 단독 깃대
    static readonly Vector2[] PLANTS = { new Vector2(30, 438), new Vector2(368, 440), new Vector2(28, 532), new Vector2(368, 540) };
    // 매장(16,420–380,560) 바닥 타일: 바닥 텍스처 격자(26)에 맞추고 점선 테두리는 비워 둔다(안쪽 10)
    const float TS = 26f, TX0 = 26f, TX1 = 370f, TY0 = 430f, TY1 = 550f, TILE_H = 0.16f;

    Transform root, awn, neon, lights; bool built;
    Renderer awnBase;
    class Strip { public Transform t; public Renderer r; public bool dark; public float x, kick; public Vector3 basePos; }
    readonly List<Strip> stripes = new List<Strip>(), flaps = new List<Strip>();
    class Flag { public Transform pivot, seg2; public Renderer[] cloth; public float ph, spin; }
    readonly List<Flag> flags = new List<Flag>();
    Material neonPink, neonTeal, neonGold; float flickNext = 3f, flickT = -1f, neonOn = 1f;
    readonly List<Transform> plants = new List<Transform>();
    class Bulb { public Transform t; public Material m; public Color c, e; public float boost; }
    readonly List<Bulb> bulbs = new List<Bulb>();
    GameObject pool; Material poolMat;
    Transform tiles;
    float awnKick;

    // ───────────────────────── 조립 ─────────────────────────
    public void Build()
    {
        if (built) return;
        root = new GameObject("shopDecor").transform; root.SetParent(transform, false);
        BuildAwning();
        BuildFlags();
        BuildNeon();
        BuildPlants();
        BuildLights();
        built = true;
    }

    void BuildAwning()
    {
        awn = new GameObject("awning").transform; awn.SetParent(root, false);
        awn.position = P(CX, AWN_Y, AWN_H); awn.localRotation = Quaternion.Euler(-TILT, 0, 0);
        awnBase = Prim.Box(awn, "canopy", L(0, 0, AWN_D / 2), new Vector3(AWN_W, 2.5f, AWN_D), AWN_A0).GetComponent<Renderer>();
        float sw = AWN_W / NS;
        for (int i = 0; i < NS; i++)
        {
            float x = -AWN_W / 2 + sw * (i + 0.5f); bool dark = i % 2 == 0;
            if (dark)
            {
                var s = Prim.Box(awn, "stripe", L(x, 0, AWN_D / 2), new Vector3(sw, 2.9f, AWN_D + 0.5f), AWN_B0);
                stripes.Add(new Strip { t = s.transform, r = s.GetComponent<Renderer>(), dark = true, x = x, basePos = s.transform.localPosition });
            }
            // 가리비 늘어뜨림(원본 strip 232×7 대신): 줄무늬마다 둥근 끝 천 조각, 앞 모서리에서 수직으로 매달려 물결친다
            var fp = new GameObject("flap").transform; fp.SetParent(awn, false);
            fp.localPosition = L(x, 0, AWN_D + 0.15f); fp.localRotation = Quaternion.Euler(TILT, 0, 0);
            fp.gameObject.AddComponent<MeshFilter>().sharedMesh = FlapMesh(sw);
            var mr = fp.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mats.Solid(dark ? AWN_B0 : AWN_A0);
            flaps.Add(new Strip { t = fp, r = mr, dark = dark, x = x });
        }
        // 기둥 2개(원본 woodD) + 받침 + 위로 깃대·금색 꼭지
        foreach (float x in POLE_X)
        {
            Prim.Cyl(root, "foot", P(x, POLE_Y, 1.5f), 9, 3, STEEL);
            Prim.Cyl(root, "pole", P(x, POLE_Y, 28.5f), 4, 57, POLE);
            Prim.Cyl(root, "flagpole", P(x, POLE_Y, 73.5f), 2, 34, DARK);
            Prim.Sphere(root, "tip", P(x, POLE_Y, 91f), 4.5f, GOLDC);
        }
    }

    // 늘어뜨림 조각: 상자(폭 w, 높이 5) + 아래 둥근 원판(지름 0.96w, 두께 1 < 상자 1.2라 앞면이 겹치지 않음)을 메시 하나로
    static Mesh _flap; static float _flapW;
    static Mesh FlapMesh(float w)
    {
        if (_flap != null && Mathf.Approximately(_flapW, w)) return _flap;
        var ci = new CombineInstance[2];
        ci[0].mesh = PrimMesh(PrimitiveType.Cube); ci[0].transform = Matrix4x4.TRS(new Vector3(0, -2.5f, 0) * K, Quaternion.identity, new Vector3(w, 5f, 1.2f) * K);
        ci[1].mesh = PrimMesh(PrimitiveType.Cylinder); ci[1].transform = Matrix4x4.TRS(new Vector3(0, -5f, 0) * K, Quaternion.Euler(90, 0, 0), new Vector3(w * 0.96f, 0.5f, w * 0.96f) * K);
        _flap = new Mesh { name = "flap" }; _flap.CombineMeshes(ci, true, true); _flapW = w;
        return _flap;
    }
    static readonly Dictionary<PrimitiveType, Mesh> primMesh = new Dictionary<PrimitiveType, Mesh>();
    static Mesh PrimMesh(PrimitiveType t)
    {
        if (primMesh.TryGetValue(t, out var m) && m != null) return m;
        var go = GameObject.CreatePrimitive(t); m = go.GetComponent<MeshFilter>().sharedMesh; Destroy(go);
        primMesh[t] = m; return m;
    }

    // 깃발 3개: 차양 기둥 위 2 + 매장 서쪽 단독 깃대 1. 천은 두 마디(바깥 마디가 더 펄럭)
    void BuildFlags()
    {
        foreach (float x in POLE_X) flags.Add(MakeFlag(P(x, POLE_Y, 88f), 8f, 10f));
        Prim.Cyl(root, "foot", P(FLAG3.x, FLAG3.y, 2f), 11, 4, STEEL);
        Prim.Cyl(root, "flagpole", P(FLAG3.x, FLAG3.y, 39f), 3, 74, DARK);
        Prim.Sphere(root, "tip", P(FLAG3.x, FLAG3.y, 77f), 5, GOLDC);
        flags.Add(MakeFlag(P(FLAG3.x, FLAG3.y, 74f), 9.5f, 12f));
    }
    Flag MakeFlag(Vector3 pos, float w, float h)
    {
        var pv = new GameObject("flag").transform; pv.SetParent(root, false); pv.position = pos;
        var a = Prim.Box(pv, "cloth", L(w / 2, -h / 2, 0), new Vector3(w, h, 0.8f), FLAG0);
        var s2 = new GameObject("seg2").transform; s2.SetParent(pv, false); s2.localPosition = L(w, 0, 0);
        var b = Prim.Box(s2, "cloth2", L(w * 0.45f, -h * 0.47f, 0), new Vector3(w * 0.9f, h * 0.88f, 0.8f), FLAG0);
        return new Flag { pivot = pv, seg2 = s2, cloth = new[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>() }, ph = flags.Count * 1.9f };
    }

    // 네온 간판(원본: 남색 판 + 분홍 고리 + 청록 막대, 밤 발광). 차양 앞단 위에 뒤로 35° 눕혀 세운다: 분홍 거품 고리 · 청록 물병 · 금색 반짝이
    void BuildNeon()
    {
        neon = new GameObject("neon").transform; neon.SetParent(root, false);
        float y = 553f; float h = AWN_H - (y - AWN_Y) * Mathf.Tan(TILT * Mathf.Deg2Rad) + 1.0f;
        neon.position = P(NEON_X, y, h); neon.localRotation = Quaternion.Euler(35, 0, 0);
        Prim.Box(neon, "foot", new Vector3(-26 * K, -1.5f * K, 0), new Vector3(4, 4, 3), DARK);
        Prim.Box(neon, "foot", new Vector3(26 * K, -1.5f * K, 0), new Vector3(4, 4, 3), DARK);
        Prim.Box(neon, "frame", new Vector3(0, 10.5f * K, 1.0f * K), new Vector3(80, 25, 1.5f), Hex("15213a"));
        Prim.Box(neon, "board", new Vector3(0, 10.5f * K, 0), new Vector3(76, 21, 3), Hex("22304a"));
        neonPink = EmMat(PINK); neonTeal = EmMat(Hex("5ed6c8")); neonGold = EmMat(Hex("ffd27a"));
        // (발광색은 Update에서 NEON_E* × 세기: 알베도보다 채도를 높여 블룸이 하얗게 타지 않고 색으로 번지게)
        float z = -2.1f * K;
        // 분홍 고리(거품)
        var ring = new GameObject("ring"); ring.transform.SetParent(neon, false);
        ring.transform.localPosition = new Vector3(-22 * K, 10.5f * K, z); ring.transform.localRotation = Quaternion.Euler(-90, 0, 0); ring.transform.localScale = Vector3.one * 7.2f * K;
        ring.AddComponent<MeshFilter>().sharedMesh = TubeRing(); var rr = ring.AddComponent<MeshRenderer>(); rr.sharedMaterial = neonPink; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 청록 물병
        Glyph("body", 0, 8.6f, 8, 9.6f, neonTeal);
        Glyph("neck", 0, 14.6f, 3.4f, 2.8f, neonTeal);
        Glyph("cap", 0, 16.8f, 5.2f, 1.6f, neonTeal);
        // 금색 반짝이(+ 와 ×)
        Glyph("s1", 22, 10.5f, 9, 1.6f, neonGold); Glyph("s2", 22, 10.5f, 1.6f, 9, neonGold);
        Glyph("s3", 22, 10.5f, 5, 1.2f, neonGold, 45); Glyph("s4", 22, 10.5f, 5, 1.2f, neonGold, -45);
        neon.gameObject.SetActive(false);
    }
    void Glyph(string n, float x, float y, float w, float hh, Material m, float rz = 0)
    {
        var g = Prim.Box(neon, n, new Vector3(x * K, y * K, -2.1f * K), new Vector3(w, hh, 1.2f), Color.white);
        g.transform.localRotation = Quaternion.Euler(0, 0, rz);
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    static Material EmMat(Color c)
    {
        var b = Resources.Load<Material>("Mats/Emissive");
        var m = b != null ? new Material(b) : new Material(Mats.Solid(c));
        m.color = c; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.3f);
        return m;
    }
    // 굵은 고리(안쪽 반지름 0.62, 바깥 1) — 위(+y)를 보는 납작 메시
    static Mesh _tube;
    static Mesh TubeRing()
    {
        if (_tube != null) return _tube;
        int n = 28; float r0 = 0.62f, r1 = 1f;
        var v = new Vector3[n * 2]; var nor = new Vector3[n * 2]; var tri = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2 / n; float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            v[i * 2] = new Vector3(ca * r0, 0, sa * r0); v[i * 2 + 1] = new Vector3(ca * r1, 0, sa * r1); nor[i * 2] = nor[i * 2 + 1] = Vector3.up;
            int j = (i + 1) % n;
            tri[i * 6] = i * 2; tri[i * 6 + 1] = j * 2 + 1; tri[i * 6 + 2] = i * 2 + 1;
            tri[i * 6 + 3] = i * 2; tri[i * 6 + 4] = j * 2; tri[i * 6 + 5] = j * 2 + 1;
        }
        _tube = new Mesh { name = "tube", vertices = v, normals = nor, triangles = tri };
        return _tube;
    }

    // 선인장 4개(원본 bCactus ×1.25): 테라코타 화분·흙·줄기·팔 2·꼭대기 분홍 꽃. 몸통(body)은 숨쉬듯 늘었다 줄었다
    void BuildPlants()
    {
        foreach (var p in PLANTS)
        {
            var g = new GameObject("cactus").transform; g.SetParent(root, false); g.position = P(p.x, p.y, 0);
            float s = 1.25f;
            Prim.BlobShadow(g, 20);
            Prim.Cyl(g, "pot", L(0, 4.5f * s, 0), 13 * s, 9 * s, Hex("c4542a"));
            Prim.Cyl(g, "rim", L(0, 9f * s, 0), 14.5f * s, 2f * s, Hex("d4612f"));
            Prim.Cyl(g, "soil", L(0, 9.6f * s, 0), 12f * s, 1f, Hex("6e2a12"));
            var body = new GameObject("body").transform; body.SetParent(g, false); body.localPosition = L(0, 9.6f * s, 0);
            Prim.Cyl(body, "stem", L(0, 8 * s, 0), 6.4f * s, 16 * s, GREEN);
            Prim.Sphere(body, "top", L(0, 16 * s, 0), 6.4f * s, GREEN);
            var a = Prim.Cyl(body, "armR", L(4.6f * s, 10.5f * s, 0), 3.4f * s, 8 * s, GREEN); a.transform.localRotation = Quaternion.Euler(0, 0, -20);
            Prim.Sphere(body, "armRTip", L(6f * s, 14.6f * s, 0), 3.5f * s, GREEN);
            var b = Prim.Cyl(body, "armL", L(-4f * s, 7.5f * s, 0.6f), 3f * s, 6.5f * s, GREEN_D); b.transform.localRotation = Quaternion.Euler(0, 0, 25);
            Prim.Sphere(body, "armLTip", L(-5.3f * s, 10.4f * s, 0.6f), 3.1f * s, GREEN_D);
            Prim.Sphere(body, "flower", L(0, 19.4f * s, 0), 3.2f * s, PINK);
            g.localRotation = Quaternion.Euler(0, (p.x < 200 ? 20 : -20) + p.y * 0.7f, 0);
            g.gameObject.SetActive(false);
            plants.Add(g);
        }
    }

    // 줄전구 11개(원본: 차양 앞단 아래, 노랑·분홍 번갈아, 밤 발광 b .5 + k 1.2). 기둥(72·284)과 가운데(178)에 걸린 두 경간이 처진다
    void BuildLights()
    {
        lights = new GameObject("lights").transform; lights.SetParent(root, false);
        lights.position = P(CX, 558.5f, 49f);
        var pts = new Vector2[11];
        for (int i = 0; i < 11; i++)
        {
            float dx = -104f + 20.8f * i; float t = (i % 5) / 5f; float sag = (i == 0 || i == 5 || i == 10) ? 0 : 7f * Mathf.Sin(Mathf.PI * t);
            pts[i] = new Vector2(dx, -sag);
        }
        // 전선(얇은 막대) — 양 끝은 기둥 쪽으로 조금 더
        var ext = new List<Vector2> { new Vector2(-110, 2f) }; ext.AddRange(pts); ext.Add(new Vector2(110, 2f));
        for (int i = 0; i + 1 < ext.Count; i++)
        {
            Vector2 a = ext[i], b = ext[i + 1], m = (a + b) / 2, d = b - a;
            var w = Prim.Box(lights, "wire", L(m.x, m.y, 0), new Vector3(d.magnitude + 0.6f, 0.8f, 0.8f), Hex("1b1a2e"));
            w.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg); Prim.NoShadow(w);
        }
        for (int i = 0; i < 11; i++)
        {
            var bt = new GameObject("bulb").transform; bt.SetParent(lights, false); bt.localPosition = L(pts[i].x, pts[i].y, 0);
            var sock = Prim.Box(bt, "socket", L(0, -1.2f, 0), new Vector3(2.6f, 2.4f, 2.6f), Hex("3b3a52")); Prim.NoShadow(sock);
            bool warm = i % 2 == 0;
            Color c = warm ? Hex("ffe0a0") : Hex("ffb0b0"), e = warm ? Hex("ffa424") : Hex("ff4f6e");
            var sp = Prim.Sphere(bt, "glass", L(0, -4.9f, 0), 7f, c); Prim.NoShadow(sp);
            var m = EmMat(c); sp.GetComponent<Renderer>().sharedMaterial = m;
            bulbs.Add(new Bulb { t = bt, m = m, c = c, e = e });
        }
        // 밤 바닥빛(원본 가로등처럼 바닥에 은은한 원판): 줄전구 아래 타원 빛
        pool = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(pool.GetComponent<Collider>()); pool.name = "glow";
        pool.transform.SetParent(root, false); pool.transform.position = P(CX, 566f, 0.35f); pool.transform.rotation = Quaternion.Euler(90, 0, 0);
        pool.transform.localScale = new Vector3(270 * K, 64 * K, 1);
        var sh = Shader.Find("Sprites/Default");
        poolMat = sh != null ? new Material(sh) : Mats.Trans(Color.white);
        poolMat.mainTexture = GlowTex(); poolMat.color = new Color(1f, 0.82f, 0.48f, 0f); poolMat.renderQueue = 2990;
        var pr = pool.GetComponent<Renderer>(); pr.sharedMaterial = poolMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        lights.gameObject.SetActive(false); pool.SetActive(false);
    }
    static Texture2D _glow;
    static Texture2D GlowTex()
    {
        if (_glow != null) return _glow;
        const int n = 64; _glow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);
            px[y * n + x] = new Color(1, 1, 1, a);
        }
        _glow.SetPixels(px); _glow.Apply(false, true);
        return _glow;
    }

    // 체크무늬 바닥: 바닥 격자(26) 한 칸 걸러 진한 칸. 등장 연출 땐 칸마다 따로 만들고, 끝나면 색별로 메시 하나로 합친다
    static readonly Color TILE_A = Hex("d9a66e"), TILE_B = Hex("cf9a62");
    List<Transform> BuildTiles()
    {
        if (tiles != null) Destroy(tiles.gameObject);
        tiles = new GameObject("tiles").transform; tiles.SetParent(root, false);
        var cells = new List<Transform>();
        for (int gy = 0; gy < 6; gy++)
            for (int gx = 0; gx < 14; gx++)
            {
                if ((gx + gy) % 2 == 0) continue;
                float x0 = Mathf.Max(TX0, 16 + gx * TS), x1 = Mathf.Min(TX1, 16 + (gx + 1) * TS);
                float y0 = Mathf.Max(TY0, 420 + gy * TS), y1 = Mathf.Min(TY1, 420 + (gy + 1) * TS);
                if (x1 - x0 < 1 || y1 - y0 < 1) continue;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "cell";
                q.transform.SetParent(tiles, false); q.transform.position = P((x0 + x1) / 2, (y0 + y1) / 2, TILE_H); q.transform.rotation = Quaternion.Euler(90, 0, 0);
                q.transform.localScale = new Vector3((x1 - x0) * K, (y1 - y0) * K, 1);
                var r = q.GetComponent<Renderer>(); r.sharedMaterial = Mats.Solid(((gx * 7 + gy * 3) % 3 == 0) ? TILE_B : TILE_A);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = true;
                cells.Add(q.transform);
            }
        return cells;
    }
    void CombineTiles()
    {
        if (tiles == null) return;
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var w2l = tiles.worldToLocalMatrix;
        foreach (Transform c in tiles)
        {
            var mf = c.GetComponent<MeshFilter>(); var mr = c.GetComponent<MeshRenderer>(); if (mf == null || mr == null) continue;
            if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = w2l * c.localToWorldMatrix });
        }
        for (int i = tiles.childCount - 1; i >= 0; i--) { var c = tiles.GetChild(i); c.SetParent(null, false); Destroy(c.gameObject); }
        foreach (var kv in groups)
        {
            var go = new GameObject("tilesMesh"); go.transform.SetParent(tiles, false);
            var mesh = new Mesh { name = "shopTiles" }; mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = kv.Key;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = true;
        }
    }

    // ───────────────────────── 상태 반영 ─────────────────────────
    bool Has(string id) => DecoManager.I != null && DecoManager.I.Has(id);

    /// 저장 상태 그대로(연출 없이) 반영 — 시작·재시작·이주 후
    public void ApplyAll()
    {
        if (!built) Build();
        PaintAwning(Has("awning"));
        PaintFlags(Has("flag"));
        neon.gameObject.SetActive(Has("neon"));
        foreach (var p in plants) p.gameObject.SetActive(Has("plant"));
        lights.gameObject.SetActive(Has("lights")); pool.SetActive(Has("lights"));
        if (Has("tile")) { if (tiles == null) { BuildTiles(); CombineTiles(); } }
        else if (tiles != null) { Destroy(tiles.gameObject); tiles = null; }
    }
    void PaintAwning(bool blue)
    {
        awnBase.sharedMaterial = Mats.Solid(blue ? AWN_A1 : AWN_A0);
        foreach (var s in stripes) s.r.sharedMaterial = Mats.Solid(blue ? AWN_B1 : AWN_B0);
        foreach (var f in flaps) f.r.sharedMaterial = Mats.Solid(f.dark ? (blue ? AWN_B1 : AWN_B0) : (blue ? AWN_A1 : AWN_A0));
    }
    void PaintFlags(bool purple) { foreach (var f in flags) PaintFlag(f, purple); }
    void PaintFlag(Flag f, bool purple) { foreach (var r in f.cloth) r.sharedMaterial = Mats.Solid(purple ? FLAG1 : FLAG0); }

    /// 장식 연출의 중심(색종이·동전 목표·플로팅 글자)
    public Vector3 Focus(string id)
    {
        switch (id)
        {
            case "awning": return P(CX, 546, 66);
            case "neon": return P(NEON_X, 548, 72);
            case "plant": return P(CX, 500, 36);
            case "lights": return P(CX, 558, 46);
            case "flag": return P(CX, 540, 80);
            case "tile": return P(CX, 470, 4);
        }
        return P(CX, 504, 50);
    }

    /// 구매 직후 등장 연출. delay 동안 기다렸다가(시트가 내려가는 시간) 팝 + 색종이 20 + 플래시 0.3 + 장식별 연출
    public void Reveal(string id, float delay) { if (!built) Build(); StartCoroutine(CoReveal(id, delay)); }
    IEnumerator CoReveal(string id, float delay)
    {
        // 결과(보유)는 이미 저장됐다. 연출 전에 상태가 먼저 보이지 않게 이번 장식만 아직 숨겨 둔다
        switch (id)
        {
            case "neon": neon.gameObject.SetActive(false); break;
            case "plant": foreach (var p in plants) p.gameObject.SetActive(false); break;
            case "lights": lights.gameObject.SetActive(false); pool.SetActive(false); break;
        }
        if (delay > 0) yield return new WaitForSeconds(delay);
        var d = DecoManager.Def(id);
        var focus = Focus(id);
        JuiceFX.I.Confetti(focus, 20);
        JuiceFX.I.Ring(new Vector3(focus.x, 0.05f, focus.z), GameConfig.GOLD, 40);
        if (d != null) WorldLabels.I.Float(focus + Vector3.up * 0.6f, d.name + "!", true, false);
        switch (id)
        {
            case "awning": yield return CoAwning(); break;
            case "flag": yield return CoFlags(); break;
            case "neon": yield return CoNeon(); break;
            case "plant": yield return CoPlants(); break;
            case "lights": yield return CoLights(); break;
            case "tile": yield return CoTiles(); break;
        }
    }

    // 차양: 왼쪽→오른쪽으로 줄무늬가 하나씩 튀어 오르며 파랗게 바뀌고, 늘어뜨림이 물결로 차인다
    IEnumerator CoAwning()
    {
        awnBase.sharedMaterial = Mats.Solid(AWN_A1);
        var all = new List<Strip>(stripes); all.AddRange(flaps);
        foreach (var s in all) StartCoroutine(CoStripe(s, (s.x + AWN_W / 2) / AWN_W * 0.5f));
        for (int i = 0; i < 7; i++) { AudioManager.I.Play("pop", 1f + i * 0.12f, 0.45f); yield return new WaitForSeconds(0.075f); }
        awnKick = 4f; JuiceFX.I.Squash(awn, 0.7f);
        AudioManager.I.Play("ding", 1.1f, 0.5f);
    }
    IEnumerator CoStripe(Strip s, float wait)
    {
        yield return new WaitForSeconds(wait);
        bool isFlap = flaps.Contains(s);
        s.r.sharedMaterial = Mats.Solid(s.dark ? AWN_B1 : AWN_A1);
        if (isFlap) { s.kick = 30f; yield break; }
        float el = 0;
        while (el < 0.28f) { el += Time.deltaTime; float k = Mathf.Clamp01(el / 0.28f); s.t.localPosition = s.basePos + Vector3.up * Mathf.Sin(Mathf.PI * k) * 2.2f * K; yield return null; }
        s.t.localPosition = s.basePos;
    }

    // 깃발: 하나씩 한 바퀴 돌며 보라로
    IEnumerator CoFlags()
    {
        for (int i = 0; i < flags.Count; i++) { StartCoroutine(CoFlagSpin(flags[i])); AudioManager.I.Play("whoosh", 1.1f + i * 0.15f, 0.6f); yield return new WaitForSeconds(0.15f); }
    }
    IEnumerator CoFlagSpin(Flag f)
    {
        float el = 0; bool painted = false;
        while (el < 0.55f)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / 0.55f); float e = 1 - (1 - k) * (1 - k) * (1 - k);
            f.spin = e * 360f;
            if (!painted && k > 0.4f) { painted = true; PaintFlag(f, true); }
            yield return null;
        }
        f.spin = 0; PaintFlag(f, true);
    }

    // 네온: 바닥에서 솟아(팝) 나오고, 지지직 두세 번 깜빡이다 켜진다
    IEnumerator CoNeon()
    {
        neon.gameObject.SetActive(true); neonOn = 0f;
        JuiceFX.I.Pop(neon, 0.001f, 0.5f, 2.2f);
        yield return new WaitForSeconds(0.35f);
        float[] seq = { 1, 0.05f, 0, 0.07f, 1, 0.05f, 0.2f, 0.1f, 1 };
        foreach (var v in seq)
        {
            if (v >= 0.5f) { neonOn = 1f; AudioManager.I.Play("tick", 0.6f, 0.6f); }
            else if (v > 0.01f) { yield return new WaitForSeconds(v); continue; }
            else neonOn = 0f;
            yield return new WaitForSeconds(0.06f);
        }
        neonOn = 1f; AudioManager.I.Play("ding", 1.3f, 0.5f);
    }

    // 선인장: 네 모서리에서 하나씩 쏙 솟는다(되튐 큰 팝 + 흙먼지 + 음 높이 상승)
    IEnumerator CoPlants()
    {
        for (int i = 0; i < plants.Count; i++)
        {
            var p = plants[i]; p.gameObject.SetActive(true);
            JuiceFX.I.Pop(p, 0.001f, 0.5f, 2.6f);
            JuiceFX.I.Dust(p.position + Vector3.up * 0.2f, 5);
            JuiceFX.I.Confetti(p.position + Vector3.up * 1.2f, 4);
            AudioManager.I.Play("pop", 0.9f + i * 0.18f, 0.7f);
            yield return new WaitForSeconds(0.16f);
        }
    }

    // 줄전구: 왼쪽부터 전구가 하나씩 톡톡 달리며 켜지고, 마지막에 다 같이 반짝
    IEnumerator CoLights()
    {
        lights.gameObject.SetActive(true); pool.SetActive(true);
        foreach (var b in bulbs) b.t.gameObject.SetActive(false);
        for (int i = 0; i < bulbs.Count; i++)
        {
            var b = bulbs[i]; b.t.gameObject.SetActive(true); b.boost = 2.2f;
            JuiceFX.I.Pop(b.t, 0.001f, 0.32f, 3f);
            AudioManager.I.PlayHz("tick", 560f * Mathf.Pow(1.07f, i), 480f, 0.6f);
            yield return new WaitForSeconds(0.06f);
        }
        yield return new WaitForSeconds(0.1f);
        foreach (var b in bulbs) b.boost = 2.6f;
        AudioManager.I.Play("ding", 1.2f, 0.6f);
    }

    // 바닥 타일: 카운터에서 바깥으로 칸이 물결처럼 튀어 오른다. 끝나면 메시 합치기
    IEnumerator CoTiles()
    {
        var cells = BuildTiles();
        var c0 = new Vector2(CX, 490);
        cells.Sort((a, b) => Vector2.Distance(WorldMap.U2W(a.position), c0).CompareTo(Vector2.Distance(WorldMap.U2W(b.position), c0)));
        foreach (var c in cells) c.gameObject.SetActive(false);
        float maxD = 0; foreach (var c in cells) maxD = Mathf.Max(maxD, Vector2.Distance(WorldMap.U2W(c.position), c0));
        float start = Time.time; int idx = 0, ticks = 0;
        while (idx < cells.Count)
        {
            float el = Time.time - start;
            while (idx < cells.Count && Vector2.Distance(WorldMap.U2W(cells[idx].position), c0) / maxD * 0.8f <= el)
            {
                var c = cells[idx++]; c.gameObject.SetActive(true); JuiceFX.I.Pop(c, 0.001f, 0.35f, 2.4f);
                if (ticks++ % 4 == 0) AudioManager.I.PlayHz("tick", 520f + idx * 14f, 480f, 0.45f);
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.45f);
        CombineTiles();
        AudioManager.I.Play("ding", 1.15f, 0.5f);
    }

    // ───────────────────────── 살아있는 움직임 ─────────────────────────
    void Update()
    {
        if (!built) return;
        float t = Time.time, dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        float night = DayNight.I != null ? DayNight.I.night : 0f;
        // 차양 흔들림(원본 awn.rotation.x = sin(t/900)·0.035) + 구매 때 차임
        awnKick = Mathf.MoveTowards(awnKick, 0, dt * 10f);
        awn.localRotation = Quaternion.Euler(-TILT + Mathf.Sin(t * 1.11f) * 1.2f + Mathf.Sin(t * 22f) * awnKick, 0, 0);
        // 늘어뜨림: 바람 물결(왼→오), 차이면 크게 흔들리다 잦아든다
        foreach (var f in flaps)
        {
            f.kick = Mathf.MoveTowards(f.kick, 0, dt * 60f);
            float a = Mathf.Sin(t * 2.6f - f.x * 0.06f) * 5f + Mathf.Sin(t * 18f + f.x) * f.kick;
            f.t.localRotation = Quaternion.Euler(TILT + a, 0, 0);
        }
        // 깃발 펄럭임(원본 rotation.y = sin(t/280+1)·0.3) — 바깥 마디가 늦게 더 크게
        foreach (var f in flags)
        {
            f.pivot.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 3.57f + f.ph) * 17f + f.spin, 0);
            f.seg2.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 3.57f + f.ph - 1.3f) * 28f, 0);
        }
        // 네온: 낮엔 은은, 밤엔 밝게(발광 b .25 + k 1.5 → 블룸), 가끔 분홍 고리가 지지직
        if (neon.gameObject.activeInHierarchy)
        {
            flickNext -= dt;
            if (flickNext <= 0) { flickT = 0; flickNext = Random.Range(4f, 9f); }
            float pinkMul = 1f;
            if (flickT >= 0)
            {
                flickT += dt;
                pinkMul = flickT < 0.05f ? 0.1f : flickT < 0.11f ? 1f : flickT < 0.15f ? 0.15f : flickT < 0.3f ? 0.8f : 1f;
                if (flickT > 0.3f) flickT = -1f;
            }
            float hum = 1 + Mathf.Sin(t * 9f) * 0.04f;
            float b = (0.35f + 1.05f * night) * neonOn * hum;
            neonPink.SetColor("_EmissionColor", NEON_EP * b * pinkMul);
            neonTeal.SetColor("_EmissionColor", NEON_ET * b * 0.9f);
            neonGold.SetColor("_EmissionColor", NEON_EG * b * (0.8f + 0.2f * Mathf.Sin(t * 5f)));
            // 밤엔 알베도를 어둡게 눌러 발광색만 남긴다(낮엔 원래 색)
            float dk = 1 - 0.55f * night;
            neonPink.color = PINK * dk; neonTeal.color = Hex("5ed6c8") * dk; neonGold.color = Hex("ffd27a") * dk;
        }
        // 선인장 숨쉬기
        for (int i = 0; i < plants.Count; i++)
        {
            if (!plants[i].gameObject.activeSelf) continue;
            var body = plants[i].Find("body"); float s = Mathf.Sin(t * 1.7f + i * 1.3f) * 0.035f;
            body.localScale = new Vector3(1 - s * 0.5f, 1 + s, 1 - s * 0.5f);
        }
        // 줄전구: 바람에 살랑, 밤엔 빛 물결이 왼→오로 흐른다. 구매 직후 boost로 번쩍
        if (lights.gameObject.activeInHierarchy)
        {
            lights.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.4f) * 4f, 0, Mathf.Sin(t * 0.9f) * 0.6f);
            for (int i = 0; i < bulbs.Count; i++)
            {
                var b = bulbs[i]; b.boost = Mathf.MoveTowards(b.boost, 0, dt * 3f);
                float chase = Mathf.Max(0, Mathf.Sin(t * 3.2f - i * 0.75f)); chase *= chase;
                float k = 0.4f + night * (0.75f + 0.65f * chase) + b.boost;
                b.m.SetColor("_EmissionColor", b.e * k);
                b.m.color = b.c * (1 - 0.5f * night);
                b.t.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.3f + i * 0.8f) * 6f, 0, 0);
            }
            var pc = poolMat.color; pc.a = night * 0.4f * (0.92f + 0.08f * Mathf.Sin(t * 2f)); poolMat.color = pc;
            pool.SetActive(night > 0.02f);
        }
    }

    void OnDestroy()
    {
        foreach (var m in new[] { neonPink, neonTeal, neonGold, poolMat }) if (m != null) Destroy(m);
        foreach (var b in bulbs) if (b.m != null) Destroy(b.m);
    }
}

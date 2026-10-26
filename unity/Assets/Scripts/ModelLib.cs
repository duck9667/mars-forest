using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// §9 Kenney GLB 모델 파이프라인. 웹 원본(app/index.html 414~423행 mdlGeo·mdl·put, tools/bake-models.mjs)과 같은 의미로 동작한다.
//  - GLB(JSON 청크 + BIN 버퍼)를 직접 읽는다(glTFast 미사용): POSITION·TEXCOORD_0·indices, 노드 변환(TRS/matrix) 적용
//  - 베이커와 똑같이 통모델은 바닥 중심을 원점으로 옮긴다 → put 의 (x,y,z)는 모델 바닥 중심의 위치
//  - 색: 머티리얼 baseColorFactor(선형→sRGB hex) 또는 colormap.png 픽셀을 re 맵(hex→hex)으로 바꾼다(SK 팔레트).
//    Space Kit·바위·드럼통은 전부 factor 색이라 색별 Standard 재질(Mats.Solid)의 서브메시로, 텍스처 모델(coin·나무·분수)은 교체본 텍스처 사본으로
//  - 정점은 비인덱스로 펼쳐 면 법선(플랫 셰이딩). 좌표는 three.js(오른손)→Unity(왼손) z 반전: WorldMap.W2U 와 같은 거울
//  - 모델 에셋: Resources/Models/<파일>.glb.bytes (TextAsset), Resources/Models/Textures/colormap.png (읽기 가능)
public static class ModelLib
{
    // 원본 COL 중 모델 팔레트에 쓰는 색 (index.html 398행)
    public static class COL
    {
        public const int white = 0xf8f6f2, steel = 0xc9d1d8, steelD = 0x8c98a4, orange = 0xf07a3a, cream = 0xf6e6d2,
            blue = 0x4fb0f0, dark = 0x2b2420, navy = 0x22304a, gold = 0xffc83d;
    }

    // 모델 이름 → 파일 (bake-models.mjs LIST). q: 사전 회전(x,y,z,w) — 베이커와 같은 순서(노드 변환 → 사전 회전 → 바닥 중심)
    class Def { public string file; public Quaternion q; public bool hasQ; }
    static readonly Dictionary<string, Def> DEFS = new Dictionary<string, Def>
    {
        { "coin", new Def { file = "coin.glb" } },
        { "trees", new Def { file = "grass-trees.glb" } }, { "treesTall", new Def { file = "grass-trees-tall.glb" } }, { "fountain", new Def { file = "pavement-fountain.glb" } },
        { "barrel", new Def { file = "barrel.glb", hasQ = true, q = new Quaternion(0, 0, 0.7071068f, 0.7071068f) } }, // 누워 있는 통을 세운다 (z축 +90°)
        { "rockA", new Def { file = "formation-rock.glb" } }, { "rockB", new Def { file = "formation-large-rock.glb" } },
        { "rockC", new Def { file = "formation-stone.glb" } }, { "rockD", new Def { file = "formation-large-stone.glb" } },
        { "hangar", new Def { file = "hangar_smallA.glb" } }, { "hangarRound", new Def { file = "hangar_roundA.glb" } },
        { "tankBig", new Def { file = "machine_barrelLarge.glb" } }, { "gen", new Def { file = "machine_generator.glb" } }, { "genBig", new Def { file = "machine_generatorLarge.glb" } },
        { "pipe", new Def { file = "pipe_straight.glb" } }, { "chimney", new Def { file = "chimney_detailed.glb" } },
        { "rFins", new Def { file = "rocket_finsA.glb" } }, { "rFuel", new Def { file = "rocket_fuelA.glb" } }, { "rSides", new Def { file = "rocket_sidesA.glb" } }, { "rTop", new Def { file = "rocket_topA.glb" } },
        { "tower", new Def { file = "structure_detailed.glb" } }, { "barrels", new Def { file = "barrels.glb" } },
        { "pad", new Def { file = "platform_large.glb" } }, { "airlock", new Def { file = "corridor_end.glb" } },
        { "dish", new Def { file = "satelliteDish.glb" } }, { "rover", new Def { file = "rover.glb" } },
    };

    // SK(acc, dk): Space Kit 팔레트(흰·밝은 회색·어두운 회색·주황·순백) → 게임 색 (index.html 422행)
    public static Dictionary<int, int> SK(int acc = COL.orange, int dk = COL.steelD) => new Dictionary<int, int>
    {
        { 0xedf0f5, COL.white }, { 0xd6dbe4, COL.steel }, { 0x8f949e, dk }, { 0xffd07c, acc }, { 0xffffff, COL.cream },
    };

    public static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f);

    // 원본 put(g,name,sx,sy,sz,x,y,z,ry,re): GLB 원래 단위 × (sx,sy,sz) 논리 스케일, 그룹 g 기준 three.js 오프셋 (x,y,z), y축 회전 ry(rad).
    // Unity: 스케일 ×K, 위치 (x,y,−z)·K, 회전은 z 거울이라 −ry.
    public static GameObject Put(Transform g, string name, float sx, float sy, float sz, float x, float y, float z, float ry = 0f, Dictionary<int, int> re = null)
    {
        var go = Mdl(name, 1f, re);
        var t = go.transform;
        t.SetParent(g, false);
        t.localScale = new Vector3(sx, sy, sz) * GameConfig.K;
        t.localPosition = new Vector3(x, y, -z) * GameConfig.K;
        t.localRotation = Quaternion.Euler(0f, -ry * Mathf.Rad2Deg, 0f);
        return go;
    }

    // 원본 mdl(name, sc, re): 균등 스케일 sc(논리 단위 → ×K). 그림자 캐스트·리시브, 콜라이더 없음.
    public static GameObject Mdl(string name, float sc = 1f, Dictionary<int, int> re = null)
    {
        // KayKit 통일(r7)로 KayKit 모드에서 안 쓰는 Kenney GLB 는 빌드에서 뺐다. -kk 0(예전 모형 비교)에서 그 모델을 찾으면 빈 오브젝트만 둔다
        if (DEFS.TryGetValue(name, out var d0) && Resources.Load<TextAsset>("Models/" + d0.file) == null)
        {
            if (missing.Add(name)) Debug.Log("ModelLib: " + d0.file + " 는 빌드에서 뺐다(KayKit 통일) → 빈 자리");
            var e = new GameObject(name); e.transform.localScale = Vector3.one * sc * GameConfig.K; return e;
        }
        var b = Build(name, re);
        var go = new GameObject(name);
        go.transform.localScale = Vector3.one * sc * GameConfig.K;
        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = b.mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = b.mats;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; mr.receiveShadows = true;
        return go;
    }

    /* ---------- 메시·재질 캐시 (이름 + 색교체맵) ---------- */
    class Built { public Mesh mesh; public Material[] mats; }
    static readonly Dictionary<string, Built> cache = new Dictionary<string, Built>();
    static readonly Dictionary<string, Glb> glbCache = new Dictionary<string, Glb>();
    static readonly Dictionary<string, Material> texMatCache = new Dictionary<string, Material>();
    static readonly HashSet<string> missing = new HashSet<string>();

    static string ReKey(Dictionary<int, int> re)
    {
        if (re == null || re.Count == 0) return "";
        var keys = new List<int>(re.Keys); keys.Sort();
        var sb = new StringBuilder();
        foreach (var k in keys) sb.Append(k.ToString("x6")).Append('>').Append(re[k].ToString("x6")).Append(',');
        return sb.ToString();
    }

    static Built Build(string name, Dictionary<int, int> re)
    {
        string key = name + "|" + ReKey(re);
        if (cache.TryGetValue(key, out var hit)) return hit;
        if (!DEFS.TryGetValue(name, out var def)) throw new ArgumentException("ModelLib: 모르는 모델 " + name);
        var g = LoadGlb(def.file);

        // 재질 그룹별 삼각형(비인덱스): 그룹 키 "c:hex"(단색) 또는 "t:uri"(텍스처)
        var groups = new Dictionary<string, List<Vector3>>();
        var groupUv = new Dictionary<string, List<Vector2>>();
        var order = new List<string>();
        var meshes = L(g.json, "meshes"); var mats = L(g.json, "materials");
        void OnMesh(int mi, Matrix4x4 world)
        {
            foreach (var po in L(O(meshes[mi]), "primitives"))
            {
                var pr = O(po);
                if (I(pr, "mode", 4) != 4) continue;
                var attr = O(pr["attributes"]);
                var pos = Accessor(g, I(attr, "POSITION"), out int pn);
                float[] uv = null; int un = 2;
                if (attr.ContainsKey("TEXCOORD_0")) uv = Accessor(g, I(attr, "TEXCOORD_0"), out un);
                int vcount = pos.Length / pn;
                int[] idx;
                if (pr.ContainsKey("indices")) { var f = Accessor(g, I(pr, "indices"), out _); idx = new int[f.Length]; for (int i = 0; i < f.Length; i++) idx[i] = (int)f[i]; }
                else { idx = new int[vcount]; for (int i = 0; i < vcount; i++) idx[i] = i; }
                // 재질 → 그룹 키
                string gk = "c:ffffff"; bool textured = false;
                if (pr.ContainsKey("material"))
                {
                    var m = O(mats[I(pr, "material")]);
                    var pbr = m.ContainsKey("pbrMetallicRoughness") ? O(m["pbrMetallicRoughness"]) : new Dictionary<string, object>();
                    string uri = pbr.ContainsKey("baseColorTexture") ? TexUri(g, O(pbr["baseColorTexture"])) : null;
                    if (uri != null && uv != null) { gk = "t:" + uri; textured = true; }
                    else
                    {
                        int hex = 0xffffff;
                        if (pbr.ContainsKey("baseColorFactor")) { var f = L(pbr["baseColorFactor"]); hex = (Lin2Srgb(D(f[0])) << 16) | (Lin2Srgb(D(f[1])) << 8) | Lin2Srgb(D(f[2])); }
                        if (re != null && re.TryGetValue(hex, out int nh)) hex = nh;
                        gk = "c:" + hex.ToString("x6");
                    }
                }
                if (!groups.TryGetValue(gk, out var vl)) { vl = new List<Vector3>(); groups[gk] = vl; groupUv[gk] = new List<Vector2>(); order.Add(gk); }
                var ul = groupUv[gk];
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int vi = idx[t + c];
                        vl.Add(world.MultiplyPoint3x4(new Vector3(pos[vi * pn], pos[vi * pn + 1], pos[vi * pn + 2])));
                        ul.Add(textured ? new Vector2(uv[vi * un], 1f - uv[vi * un + 1]) : Vector2.zero); // glTF v는 위가 0 → Unity는 아래가 0
                    }
                }
            }
        }
        WalkScene(g, OnMesh);

        // 사전 회전 → 바닥 중심 원점 → z 거울 (오른손→왼손)
        int total = 0; foreach (var k in order) total += groups[k].Count;
        var verts = new Vector3[total]; var uvs = new Vector2[total]; int p = 0;
        foreach (var k in order) { groups[k].CopyTo(verts, p); groupUv[k].CopyTo(uvs, p); p += groups[k].Count; }
        if (def.hasQ) for (int i = 0; i < total; i++) verts[i] = def.q * verts[i];
        var mn = new Vector3(1e9f, 1e9f, 1e9f); var mx = -mn;
        for (int i = 0; i < total; i++) { mn = Vector3.Min(mn, verts[i]); mx = Vector3.Max(mx, verts[i]); }
        var off = new Vector3((mn.x + mx.x) / 2, mn.y, (mn.z + mx.z) / 2);
        for (int i = 0; i < total; i++) { var v = verts[i] - off; v.z = -v.z; verts[i] = v; }

        var mesh = new Mesh { name = key };
        if (total > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts; mesh.uv = uvs; mesh.subMeshCount = order.Count;
        var materials = new Material[order.Count];
        p = 0;
        for (int s = 0; s < order.Count; s++)
        {
            int n = groups[order[s]].Count; var tri = new int[n];
            for (int i = 0; i < n; i++) tri[i] = p + i; // 거울이면 보이는 감김과 앞면 규약이 함께 뒤집혀 순서는 그대로
            mesh.SetTriangles(tri, s, false); p += n;
            string gk = order[s];
            materials[s] = gk[0] == 'c' ? Mats.Solid(C(int.Parse(gk.Substring(2), NumberStyles.HexNumber))) : TexMat(gk.Substring(2), re);
        }
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var built = new Built { mesh = mesh, mats = materials };
        cache[key] = built;
        return built;
    }

    // 선형 baseColorFactor → sRGB 8bit (bake-models.mjs lin2srgb 와 동일, 더블 정밀도)
    static int Lin2Srgb(double v) => (int)Math.Round(255 * (v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055));

    /* ---------- 텍스처 모델: colormap 사본에 팔레트 교체 ---------- */
    static Material TexMat(string uri, Dictionary<int, int> re)
    {
        string key = uri + "|" + ReKey(re);
        if (texMatCache.TryGetValue(key, out var m)) return m;
        string path = "Models/" + uri; int dot = path.LastIndexOf('.'); if (dot > path.LastIndexOf('/')) path = path.Substring(0, dot);
        var src = Resources.Load<Texture2D>(path);
        Texture2D tex = src;
        if (src == null) Debug.LogWarning("ModelLib: 텍스처 없음 " + path);
        else if (re != null && re.Count > 0)
        {
            try
            {
                var px = src.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    int hex = (px[i].r << 16) | (px[i].g << 8) | px[i].b;
                    if (re.TryGetValue(hex, out int nh)) px[i] = new Color32((byte)(nh >> 16), (byte)(nh >> 8), (byte)nh, px[i].a);
                }
                tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false) { name = key, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                tex.SetPixels32(px); tex.Apply(false, true);
            }
            catch (Exception e) { Debug.LogWarning("ModelLib: 텍스처 읽기 실패(isReadable?) " + path + " — " + e.Message); tex = src; }
        }
        m = new Material(Mats.Solid(Color.white)) { name = "tex:" + key, mainTexture = tex };
        texMatCache[key] = m;
        return m;
    }

    /* ---------- GLB 읽기 ---------- */
    class Glb { public Dictionary<string, object> json; public byte[] b; public int bin; }

    static Glb LoadGlb(string file)
    {
        if (glbCache.TryGetValue(file, out var g)) return g;
        var ta = Resources.Load<TextAsset>("Models/" + file);
        if (ta == null) throw new Exception("ModelLib: Resources/Models/" + file + ".bytes 없음");
        var b = ta.bytes;
        if (BitConverter.ToUInt32(b, 0) != 0x46546C67) throw new Exception("ModelLib: GLB 아님 " + file);
        int len = (int)BitConverter.ToUInt32(b, 8);
        g = new Glb { b = b, bin = -1 };
        for (int p = 12; p + 8 <= len;)
        {
            int cl = (int)BitConverter.ToUInt32(b, p); uint ct = BitConverter.ToUInt32(b, p + 4);
            if (ct == 0x4E4F534A) g.json = (Dictionary<string, object>)Json.Parse(Encoding.UTF8.GetString(b, p + 8, cl));
            else if (ct == 0x004E4942) g.bin = p + 8;
            p += 8 + cl;
        }
        if (g.json == null || g.bin < 0) throw new Exception("ModelLib: GLB 청크 누락 " + file);
        glbCache[file] = g;
        return g;
    }

    static string TexUri(Glb g, Dictionary<string, object> bct)
    {
        var tex = O(L(g.json, "textures")[I(bct, "index")]);
        var img = O(L(g.json, "images")[I(tex, "source")]);
        if (!img.ContainsKey("uri")) { Debug.LogWarning("ModelLib: 내장 텍스처(bufferView)는 미지원"); return null; }
        return Uri.UnescapeDataString(S(img, "uri"));
    }

    // 접근자 → float 배열(count×n). 정수형은 값 그대로(normalized면 0..1)
    static float[] Accessor(Glb g, int ai, out int n)
    {
        var a = O(L(g.json, "accessors")[ai]);
        if (a.ContainsKey("sparse")) throw new Exception("ModelLib: sparse 접근자 미지원");
        string type = S(a, "type");
        n = type == "SCALAR" ? 1 : type == "VEC2" ? 2 : type == "VEC3" ? 3 : type == "VEC4" ? 4 : type == "MAT4" ? 16 : 1;
        int count = I(a, "count"), ct = I(a, "componentType"); bool norm = a.ContainsKey("normalized") && (bool)a["normalized"];
        int bytes = (ct == 5126 || ct == 5125) ? 4 : (ct == 5123 || ct == 5122) ? 2 : 1;
        var bv = O(L(g.json, "bufferViews")[I(a, "bufferView")]);
        int off = g.bin + I(bv, "byteOffset", 0) + I(a, "byteOffset", 0);
        int stride = I(bv, "byteStride", 0); if (stride == 0) stride = n * bytes;
        var o = new float[count * n]; var b = g.b;
        for (int k = 0; k < count; k++)
        {
            int bas = off + k * stride;
            for (int c = 0; c < n; c++)
            {
                int at = bas + c * bytes; float v;
                switch (ct)
                {
                    case 5126: v = BitConverter.ToSingle(b, at); break;
                    case 5125: v = BitConverter.ToUInt32(b, at); break;
                    case 5123: v = BitConverter.ToUInt16(b, at); if (norm) v /= 65535f; break;
                    case 5122: v = BitConverter.ToInt16(b, at); if (norm) v = Mathf.Max(v / 32767f, -1f); break;
                    case 5121: v = b[at]; if (norm) v /= 255f; break;
                    default: v = (sbyte)b[at]; if (norm) v = Mathf.Max(v / 127f, -1f); break;
                }
                o[k * n + c] = v;
            }
        }
        return o;
    }

    static void WalkScene(Glb g, Action<int, Matrix4x4> onMesh)
    {
        var nodes = L(g.json, "nodes");
        var roots = new List<int>();
        if (g.json.ContainsKey("scenes"))
        {
            var sc = O(L(g.json, "scenes")[I(g.json, "scene", 0)]);
            foreach (var r in L(sc, "nodes")) roots.Add((int)D(r));
        }
        else
        {
            var child = new HashSet<int>();
            foreach (var no in nodes) if (O(no).ContainsKey("children")) foreach (var c in L(O(no), "children")) child.Add((int)D(c));
            for (int i = 0; i < nodes.Count; i++) if (!child.Contains(i)) roots.Add(i);
        }
        foreach (var r in roots) Walk(nodes, r, Matrix4x4.identity, onMesh);
    }

    static void Walk(List<object> nodes, int ni, Matrix4x4 parent, Action<int, Matrix4x4> onMesh)
    {
        var n = O(nodes[ni]);
        Matrix4x4 local;
        if (n.ContainsKey("matrix")) { local = Matrix4x4.identity; var m = L(n["matrix"]); for (int i = 0; i < 16; i++) local[i] = (float)D(m[i]); } // 둘 다 열 우선
        else
        {
            var t = n.ContainsKey("translation") ? V3(L(n["translation"])) : Vector3.zero;
            var r = Quaternion.identity; if (n.ContainsKey("rotation")) { var q = L(n["rotation"]); r = new Quaternion((float)D(q[0]), (float)D(q[1]), (float)D(q[2]), (float)D(q[3])); }
            var s = n.ContainsKey("scale") ? V3(L(n["scale"])) : Vector3.one;
            local = Matrix4x4.TRS(t, r, s);
        }
        var world = parent * local;
        if (n.ContainsKey("mesh")) onMesh(I(n, "mesh"), world);
        if (n.ContainsKey("children")) foreach (var c in L(n["children"])) Walk(nodes, (int)D(c), world, onMesh);
    }

    /* ---------- JSON 헬퍼 ---------- */
    static Dictionary<string, object> O(object o) => (Dictionary<string, object>)o;
    static List<object> L(object o) => (List<object>)o;
    static List<object> L(Dictionary<string, object> d, string k) => (List<object>)d[k];
    static double D(object o) => (double)o;
    static int I(Dictionary<string, object> d, string k) => (int)(double)d[k];
    static int I(Dictionary<string, object> d, string k, int def) => d.ContainsKey(k) ? (int)(double)d[k] : def;
    static string S(Dictionary<string, object> d, string k) => (string)d[k];
    static Vector3 V3(List<object> l) => new Vector3((float)D(l[0]), (float)D(l[1]), (float)D(l[2]));

    // 최소 JSON 파서: object→Dictionary, array→List, number→double, string, bool, null
    static class Json
    {
        public static object Parse(string s) { int i = 0; return Val(s, ref i); }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i); char c = s[i];
            if (c == '{')
            {
                i++; var d = new Dictionary<string, object>(); Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++; // ':'
                    d[k] = Val(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d; // '}'
                }
            }
            if (c == '[')
            {
                i++; var l = new List<object>(); Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Val(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l; // ']'
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }
        static string Str(string s, ref int i)
        {
            i++; var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++; char e = s[i];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            i++; return sb.ToString();
        }
    }
}

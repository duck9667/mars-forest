using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §4.3 구역: 바닥·울타리(기둥 40 간격, 레일 2단, 게이트 굵은 기둥) + 잠긴 구역의 점선 윤곽·자물쇠 아치 + 해제 연출 unlockZone
public class ZoneManager : MonoBehaviour
{
    public static ZoneManager I;
    GameState G => GameState.I;

    class ZView
    {
        public WorldMap.Zone z; public Transform root, floor, fence, outline, arch; public List<Transform> parts = new List<Transform>();
        public List<Vector3> finalPos = new List<Vector3>(); public bool open, archShown;
    }
    readonly Dictionary<string, ZView> views = new Dictionary<string, ZView>();
    static readonly Color POST = GameConfig.Hex("8a5a3c"), RAIL = GameConfig.Hex("a8724a");

    void Awake() { I = this; }

    // §4.3 모서리 가로등: 열린 구역 모서리마다(중복 제거). 밤에 전구 발광 + 바닥빛(point light)
    class Lamp { public Transform root; public Renderer bulb; public Light light; public Material mat, pool; }
    readonly List<Lamp> lamps = new List<Lamp>();
    readonly HashSet<Vector2> lampSpots = new HashSet<Vector2>();

    void Start()
    {
        foreach (var z in WorldMap.ZONES) Build(z);
        G.OnPadLevel += OnPad;
        if (PadManager.I != null) PadManager.I.OnChanged += RefreshArches;
        RefreshArches();
        foreach (var v in views.Values) if (v.open) AddLamps(v.z);
    }

    void AddLamps(WorldMap.Zone z)
    {
        foreach (var c in new[] { new Vector2(z.x0, z.y0), new Vector2(z.x1, z.y0), new Vector2(z.x0, z.y1), new Vector2(z.x1, z.y1) })
        {
            if (lampSpots.Contains(c)) continue; lampSpots.Add(c);
            var root = new GameObject("lamp").transform; root.SetParent(transform, false); root.position = WorldMap.W2U(c);
            Prim.Box(root, "post", new Vector3(0, 22 * GameConfig.K, 0), new Vector3(3, 44, 3), GameConfig.Hex("3b3a52"));
            var bulb = Prim.Sphere(root, "bulb", new Vector3(0, 46 * GameConfig.K, 0), 7, GameConfig.Hex("fff1c2"));
            var r = bulb.GetComponent<Renderer>(); Prim.NoShadow(bulb);
            // 발광 변형이 빌드에 포함되도록 Resources/Mats/Emissive.mat(ProjectSetup이 _EMISSION 켜서 생성)을 복제
            var emBase = Resources.Load<Material>("Mats/Emissive");
            var em = emBase != null ? new Material(emBase) : new Material(r.sharedMaterial);
            em.color = GameConfig.Hex("fff1c2"); r.sharedMaterial = em;
            var lt = new GameObject("light").AddComponent<Light>(); lt.transform.SetParent(root, false); lt.transform.localPosition = new Vector3(0, 46 * GameConfig.K, 0);
            lt.type = LightType.Point; lt.color = GameConfig.Hex("ffd27a"); lt.range = 140 * GameConfig.K; lt.intensity = 0; lt.shadows = LightShadows.None; lt.enabled = false;
            // 바닥빛 원판(원본: opacity night*0.33). 포인트 라이트는 픽셀 라이트 수 제한으로 대부분 안 그려지므로 원판이 주역
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(pool.GetComponent<Collider>()); pool.name = "pool";
            pool.transform.SetParent(root, false); pool.transform.localPosition = new Vector3(0, 0.04f, 0); pool.transform.localScale = new Vector3(60, 0.2f, 60) * GameConfig.K;
            var pc = GameConfig.Hex("ffd27a"); pc.a = 0f; var pm = Mats.Trans(pc); pool.GetComponent<Renderer>().sharedMaterial = pm; Prim.NoShadow(pool);
            lamps.Add(new Lamp { root = root, bulb = r, light = lt, mat = em, pool = pm });
        }
    }

    void Update()
    {
        float night = DayNight.I != null ? DayNight.I.night : 0f;
        if (lamps.Count == 0) return;
        Color em = GameConfig.Hex("ffd27a") * (night * 1.6f);
        var pc = GameConfig.Hex("ffd27a"); pc.a = night * 0.33f;
        bool on = night > 0.05f;
        foreach (var l in lamps)
        {
            l.light.enabled = on; l.light.intensity = night * 1.6f;
            l.mat.SetColor("_EmissionColor", on ? em : Color.black);
            l.pool.color = pc;
        }
    }

    public bool IsOpen(string key) => views.TryGetValue(key, out var v) && v.open;

    void Build(WorldMap.Zone z)
    {
        var v = new ZView { z = z };
        v.root = new GameObject("zone_" + z.key).transform; v.root.SetParent(transform, false);
        float cx = (z.x0 + z.x1) / 2, cy = (z.y0 + z.y1) / 2;
        v.floor = Prim.Box(v.root, "floor", WorldMap.W2U(cx, cy, 0.5f), new Vector3(z.x1 - z.x0, 1, z.y1 - z.y0), z.floor).transform;
        v.fence = new GameObject("fence").transform; v.fence.SetParent(v.root, false);
        BuildFence(v);
        v.open = z.open || (z.pad != null && G.PadLv(z.pad) >= 1);
        if (!v.open)
        {
            v.floor.gameObject.SetActive(false); v.fence.gameObject.SetActive(false);
            // 점선 윤곽 대신 어두운 반투명 평면 + 자물쇠 아치(차례가 오면 표시)
            var c = new Color(0.1f, 0.05f, 0.02f, 0.18f);
            var og = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(og.GetComponent<Collider>());
            og.name = "outline"; og.transform.SetParent(v.root, false);
            og.transform.position = WorldMap.W2U(cx, cy, 0.3f); og.transform.localScale = new Vector3(z.x1 - z.x0, 0.4f, z.y1 - z.y0) * GameConfig.K;
            og.GetComponent<Renderer>().sharedMaterial = Mats.Trans(c); Prim.NoShadow(og);
            v.outline = og.transform;
            v.arch = BuildArch(v);
            v.outline.gameObject.SetActive(false); v.arch.gameObject.SetActive(false);
        }
        views[z.key] = v;
    }

    // 울타리 부품을 둘레 순서(N 서→동, E 북→남, S 동→서, W 남→북)로 만들어 해제 연출에서 차례로 솟게 한다
    void BuildFence(ZView v)
    {
        var z = v.z;
        foreach (char side in "NESW")
        {
            if (z.sides.IndexOf(side) < 0) continue;
            bool horiz = side == 'N' || side == 'S';
            float fixedV = side == 'N' ? z.y0 : side == 'S' ? z.y1 : side == 'W' ? z.x0 : z.x1;
            float a = horiz ? z.x0 : z.y0, b = horiz ? z.x1 : z.y1;
            bool reverse = side == 'S' || side == 'W';
            float gAt = -1, gW = 0;
            foreach (var g in WorldMap.GATES) if (g.zone == z.key && g.side == side) { gAt = g.at; gW = g.w; }
            // 기둥
            var posts = new List<float>();
            for (float t = a; t <= b + 0.1f; t += WorldMap.POST_GAP) { if (gAt >= 0 && Mathf.Abs(t - gAt) < gW / 2) continue; posts.Add(t); }
            if (reverse) posts.Reverse();
            foreach (float t in posts)
            {
                float x = horiz ? t : fixedV, y = horiz ? fixedV : t;
                Part(v, Prim.Box(v.fence, "post", WorldMap.W2U(x, y, 12), new Vector3(5, 24, 5), POST));
            }
            // 레일 2단, 게이트 틈 제외
            void RailSeg(float s0, float s1)
            {
                if (s1 - s0 < 2) return;
                float mid = (s0 + s1) / 2, len = s1 - s0;
                float x = horiz ? mid : fixedV, y = horiz ? fixedV : mid;
                var size = horiz ? new Vector3(len, 3, 2.5f) : new Vector3(2.5f, 3, len);
                Part(v, Prim.Box(v.fence, "rail", WorldMap.W2U(x, y, 8), size, RAIL));
                Part(v, Prim.Box(v.fence, "rail", WorldMap.W2U(x, y, 17), size, RAIL));
            }
            if (gAt >= 0)
            {
                RailSeg(a, gAt - gW / 2); RailSeg(gAt + gW / 2, b);
                float x0 = horiz ? gAt - gW / 2 : fixedV, y0 = horiz ? fixedV : gAt - gW / 2;
                float x1 = horiz ? gAt + gW / 2 : fixedV, y1 = horiz ? fixedV : gAt + gW / 2;
                Part(v, Prim.Box(v.fence, "gpost", WorldMap.W2U(x0, y0, 15), new Vector3(7, 30, 7), POST));
                Part(v, Prim.Box(v.fence, "gpost", WorldMap.W2U(x1, y1, 15), new Vector3(7, 30, 7), POST));
            }
            else RailSeg(a, b);
        }
    }
    void Part(ZView v, GameObject go) { v.parts.Add(go.transform); v.finalPos.Add(go.transform.position); }

    // 자물쇠 아치: 게이트 폭+10, 높이 38. ice2는 ice의 N게이트를 공유하므로 그 자리에.
    Transform BuildArch(ZView v)
    {
        var z = v.z; float gx = (z.x0 + z.x1) / 2, gy = z.y1, gw = 70; char side = 'S';
        foreach (var g in WorldMap.GATES) if (g.zone == z.key) { gx = g.side == 'N' || g.side == 'S' ? g.at : (g.side == 'W' ? z.x0 : z.x1); gy = g.side == 'N' ? z.y0 : g.side == 'S' ? z.y1 : g.at; gw = g.w; side = g.side; }
        if (z.key == "ice2") { gx = 126; gy = z.y1; gw = 70; side = 'S'; }
        // 피벗은 게이트 중심(팝이 제자리에서), 자식은 로컬 오프셋
        var arch = new GameObject("arch").transform; arch.SetParent(v.root, false); arch.position = WorldMap.W2U(gx, gy, 0);
        float w = gw + 10, h = 38, k = GameConfig.K; bool horiz = side == 'N' || side == 'S';
        var col = GameConfig.NAVY;
        Vector3 across = horiz ? new Vector3(1, 0, 0) : new Vector3(0, 0, -1);   // 게이트 변 방향(논리 x 또는 y)
        Prim.Box(arch, "l", -across * w / 2 * k + Vector3.up * h / 2 * k, new Vector3(5, h, 5), col);
        Prim.Box(arch, "r", across * w / 2 * k + Vector3.up * h / 2 * k, new Vector3(5, h, 5), col);
        Prim.Box(arch, "top", Vector3.up * h * k, horiz ? new Vector3(w + 5, 5, 5) : new Vector3(5, 5, w + 5), col);
        Prim.Box(arch, "lock", Vector3.up * (h - 9) * k, new Vector3(10, 12, 4), GameConfig.GOLD);
        Prim.Box(arch, "shackle", Vector3.up * (h - 9 + 8) * k, new Vector3(7, 6, 3), GameConfig.GOLD);
        return arch;
    }

    // 차례가 온 구역(발판이 노출 중)만 윤곽·아치 표시
    void RefreshArches()
    {
        foreach (var v in views.Values)
        {
            if (v.open || v.arch == null) continue;
            bool show = PadManager.I != null && PadManager.I.visSet.Contains(v.z.pad);
            if (show && !v.archShown)
            {
                v.outline.gameObject.SetActive(true); v.arch.gameObject.SetActive(true);
                JuiceFX.I.Pop(v.arch, 0.001f, 0.5f);
            }
            else if (!show && v.archShown) { v.outline.gameObject.SetActive(false); v.arch.gameObject.SetActive(false); }
            v.archShown = show;
        }
    }

    void OnPad(string id, int lv)
    {
        foreach (var v in views.Values) if (!v.open && v.z.pad == id && lv >= 1) StartCoroutine(Unlock(v));
    }

    // §4.3 해제 연출: 바닥 pop 0.7s, 울타리 부품이 −22에서 둘레 순서로 솟음(각 0.5s, 순서×0.03s 지연), 모서리 먼지, 링 2개, 흔들림 5, 팡파르, 색종이 32
    IEnumerator Unlock(ZView v)
    {
        v.open = true;
        if (v.outline) v.outline.gameObject.SetActive(false);
        if (v.arch) v.arch.gameObject.SetActive(false);
        var z = v.z; float cx = (z.x0 + z.x1) / 2, cy = (z.y0 + z.y1) / 2; Vector3 center = WorldMap.W2U(cx, cy, 0);
        v.floor.gameObject.SetActive(true); JuiceFX.I.Pop(v.floor, 0.001f, 0.7f, 0.35f);   // 되튐 0.35 (§4.3)
        v.fence.gameObject.SetActive(true);
        for (int i = 0; i < v.parts.Count; i++) v.parts[i].position = v.finalPos[i] + Vector3.down * 22 * GameConfig.K;
        JuiceFX.I.Ring(center, Color.white, 60); JuiceFX.I.Ring(center, GameConfig.GOLD, 30);
        JuiceFX.I.Shake(5); JuiceFX.I.Flash(0.5f); JuiceFX.I.Vibrate(40);
        AudioManager.I.Play("up", 1f); AudioManager.I.Play("pad", 0.8f);
        JuiceFX.I.Confetti(center + Vector3.up * 1.5f, 32);
        foreach (var corner in new[] { new Vector2(z.x0, z.y0), new Vector2(z.x1, z.y0), new Vector2(z.x0, z.y1), new Vector2(z.x1, z.y1) }) JuiceFX.I.Dust(WorldMap.W2U(corner, 2), 5);
        Stations.I?.RefreshZoneGated();
        AddLamps(z);
        float el = 0; int n = v.parts.Count; float total = 0.5f + n * 0.03f;
        while (el < total)
        {
            el += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                float k = Mathf.Clamp01((el - i * 0.03f) / 0.5f);
                float e = k <= 0 ? 0 : JuiceFX.EaseOutBack(k);
                v.parts[i].position = v.finalPos[i] + Vector3.down * 22 * GameConfig.K * (1 - e);
            }
            yield return null;
        }
        for (int i = 0; i < n; i++) v.parts[i].position = v.finalPos[i];
    }
}

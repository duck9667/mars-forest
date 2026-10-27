using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §4.3 구역: 바닥·울타리(낮은 연석 + 게이트 작은 기둥) + 잠긴 구역의 점선 윤곽·자물쇠 아치 + 해제 연출 unlockZone
// 북쪽 생존 기지 구역(y1 ≤ NORTH_Y)은 잠겨 있는 동안 늘 "개척 예정지"(옅은 측량 점선 + 모서리 ㄱ자)로 보이고, 차례가 오면 윤곽·아치로 바뀐다
public class ZoneManager : MonoBehaviour
{
    public static ZoneManager I;
    GameState G => GameState.I;

    class ZView
    {
        public WorldMap.Zone z; public Transform root, floor, fence, outline, arch, survey, surveyLine; public List<Transform> parts = new List<Transform>();
        public List<Vector3> finalPos = new List<Vector3>(); public bool open, archShown;
    }
    readonly Dictionary<string, ZView> views = new Dictionary<string, ZView>();
    static readonly Color GPOST = GameConfig.Hex("7f5f48"), CURB = GameConfig.Hex("e2bf98");   // 게이트 기둥·연석(목표 그림의 밝은 둥근 연석)

    void Awake() { I = this; WorldMap.ClearZones(); }   // 이주(Restart) 뒤 새 씬은 열림 상태를 처음부터

    // §4.3 가로등: 상점 네 모서리 + 열린 구역마다 하나(AddLamps). 밤에 전구 발광 + 바닥빛(point light)
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
        StartCoroutine(UnstickPlayer());
    }

    // 울타리 충돌 이전 저장본은 플레이어가 울타리 선 위에 저장돼 있을 수 있다 → 시작 자리로
    IEnumerator UnstickPlayer()
    {
        yield return null;
        if (Player.I != null && WorldMap.Blocked(Player.I.pos.x, Player.I.pos.y)) Player.I.pos = WorldMap.PLAYER_START;
    }

    // 가로등(단순화): 상점은 네 모서리, 그 밖의 구역은 북서 모서리 하나만. 이미 있는 등과 30 안이면 건너뛴다
    void AddLamps(WorldMap.Zone z)
    {
        z = GroundPainter.Vis(z);   // 넓힌 길 가장자리(보이는 바닥 모서리)에 선다
        var corners = z.key == "shop"
            ? new[] { new Vector2(z.x0, z.y0), new Vector2(z.x1, z.y0), new Vector2(z.x0, z.y1), new Vector2(z.x1, z.y1) }
            : new[] { new Vector2(z.x0, z.y0) };
        foreach (var c in corners) AddLamp(c);
    }
    /// 가로등 하나(이미 있는 등과 30 안이면 건너뜀). 3·4장 길·지역(RegionWorld)도 쓴다
    public void AddLamp(Vector2 c)
    {
        {
            bool near = false; foreach (var q in lampSpots) if ((q - c).sqrMagnitude < 30 * 30) { near = true; break; }
            if (near) return; lampSpots.Add(c);
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
        // 바닥: 코드로 그린 텍스처 판(색 면 + 타일 명암 + 점선 테두리, GroundPainter.PaintFloor). 접지 그림자(0.01m)보다 낮게 0.1
        v.floor = GroundPainter.FloorQuad(v.root, z, 0.1f).transform;
        v.fence = new GameObject("fence").transform; v.fence.SetParent(v.root, false);
        if (WorldMap.FENCED.Contains(z.key)) { BuildFence(v); Corners(v); }   // 울타리는 우리만
        v.open = z.open || (z.pad != null && G.PadLv(z.pad) >= 1);
        WorldMap.SetZoneOpen(z.key, v.open);
        if (!v.open)
        {
            v.floor.gameObject.SetActive(false); v.fence.gameObject.SetActive(false);
            // 원본처럼 점선 윤곽(살짝 어두운 면 + 크림색 점선 투명 판) + 자물쇠 아치(차례가 오면 표시)
            v.outline = GroundPainter.DashQuad(v.root, z, 0.12f).transform;
            v.arch = BuildArch(v);
            v.outline.gameObject.SetActive(false); v.arch.gameObject.SetActive(false);
            if (z.y1 <= WorldMap.NORTH_Y) BuildSurvey(v);
        }
        views[z.key] = v;
    }

    // 울타리(단순화, jake 동의 2026-10-25): 기둥+난간 대신 낮은 연석(높이 5·두께 4) + 게이트 양옆 작은 기둥만.
    // 충돌은 WorldMap.FenceBlocked가 그대로 막으므로 모양만 바뀐다. 연석은 40 길이 조각으로 나눠 둘레 순서
    // (N 서→동, E 북→남, S 동→서, W 남→북)로 만들어 해제 연출에서 차례로 솟게 한다
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
            var segs = new List<Vector2>();   // (시작, 끝) 연석 조각
            void Curb(float s0, float s1)
            {
                if (s1 - s0 < 2) return;
                int n = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / WorldMap.POST_GAP));
                for (int i = 0; i < n; i++) segs.Add(new Vector2(s0 + (s1 - s0) * i / n, s0 + (s1 - s0) * (i + 1) / n));
            }
            if (gAt >= 0) { Curb(a - 2, gAt - gW / 2); Curb(gAt + gW / 2, b + 2); } else Curb(a - 2, b + 2);
            if (reverse) segs.Reverse();
            foreach (var sg in segs)
            {
                float mid = (sg.x + sg.y) / 2, len = sg.y - sg.x;
                float x = horiz ? mid : fixedV, y = horiz ? fixedV : mid;
                Part(v, Prim.Box(v.fence, "curb", WorldMap.W2U(x, y, 3f), horiz ? new Vector3(len, 6, 7) : new Vector3(7, 6, len), CURB));
            }
            if (gAt >= 0)
            {
                float x0 = horiz ? gAt - gW / 2 : fixedV, y0 = horiz ? fixedV : gAt - gW / 2;
                float x1 = horiz ? gAt + gW / 2 : fixedV, y1 = horiz ? fixedV : gAt + gW / 2;
                Part(v, Prim.Box(v.fence, "gpost", WorldMap.W2U(x0, y0, 6), new Vector3(6, 12, 6), GPOST));
                Part(v, Prim.Box(v.fence, "gpost", WorldMap.W2U(x1, y1, 6), new Vector3(6, 12, 6), GPOST));
            }
        }
    }
    // 둥근 모서리: 변이 닿는 구역 꼭짓점마다 연석 굵기의 원기둥(목표 그림의 둥근 연석 모서리)
    void Corners(ZView v)
    {
        var z = v.z;
        foreach (var (cx, cy, s1, s2) in new[] { (z.x0, z.y0, 'N', 'W'), (z.x1, z.y0, 'N', 'E'), (z.x1, z.y1, 'S', 'E'), (z.x0, z.y1, 'S', 'W') })
            if (z.sides.IndexOf(s1) >= 0 || z.sides.IndexOf(s2) >= 0)
                Part(v, Prim.Cyl(v.fence, "corner", WorldMap.W2U(cx, cy, 3f), 9, 6, CURB));
    }
    void Part(ZView v, GameObject go) { v.parts.Add(go.transform); v.finalPos.Add(go.transform.position); }

    // 개척 예정지: 옅은 측량 점선 + 모서리 ㄱ자 표시를 바닥에 그린 판 하나(세운 말뚝은 잔물건처럼 보여 뺐다). 해제되면 숨긴다
    void BuildSurvey(ZView v)
    {
        v.survey = new GameObject("survey").transform; v.survey.SetParent(v.root, false);
        v.surveyLine = GroundPainter.DashQuad(v.survey, v.z, 0.12f, true).transform;
    }

    // 자물쇠 아치: 게이트 폭+10, 높이 38. 발판에서 가장 가까운 게이트에(채굴장·산소동·온실·토양 공장은 모두 S변, 돔·우리는 N변).
    // ice2는 자기 게이트(E·N) 대신 ice의 N게이트를 공유하므로 그 자리에.
    Transform BuildArch(ZView v)
    {
        var z = GroundPainter.Vis(v.z); float gx = (z.x0 + z.x1) / 2, gy = z.y1, gw = 70; char side = 'S';   // 아치는 보이는 바닥 변(넓힌 길 가장자리)에 선다
        var pad = z.pad != null ? Pads.Get(z.pad) : null; float best = float.MaxValue;
        foreach (var g in WorldMap.GATES)
        {
            if (g.zone != z.key) continue;
            float x = g.side == 'N' || g.side == 'S' ? g.at : (g.side == 'W' ? z.x0 : z.x1);
            float y = g.side == 'N' ? z.y0 : g.side == 'S' ? z.y1 : g.at;
            float d = pad != null ? (pad.pos - new Vector2(x, y)).sqrMagnitude : 0f;   // 발판 정보가 없으면 예전처럼 마지막 게이트
            if (d <= best) { best = d; gx = x; gy = y; gw = g.w; side = g.side; }
        }
        if (z.key == "ice2") { gx = 126; gy = v.z.y1; gw = 70; side = 'S'; }
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
                if (v.surveyLine) v.surveyLine.gameObject.SetActive(false);   // 개척 예정지 점선 → 윤곽
                JuiceFX.I.Pop(v.arch, 0.001f, 0.5f);
            }
            else if (!show && v.archShown)
            {
                v.outline.gameObject.SetActive(false); v.arch.gameObject.SetActive(false);
                if (v.surveyLine) v.surveyLine.gameObject.SetActive(true);
            }
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
        WorldMap.SetZoneOpen(v.z.key, true);
        // 해제 순간 플레이어가 새 울타리 선 위에 있으면 안쪽으로 밀어 낸다
        if (Player.I != null && WorldMap.Blocked(Player.I.pos.x, Player.I.pos.y))
            Player.I.pos = Vector2.MoveTowards(Player.I.pos, new Vector2((v.z.x0 + v.z.x1) / 2, (v.z.y0 + v.z.y1) / 2), 12f);
        if (v.outline) v.outline.gameObject.SetActive(false);
        if (v.arch) v.arch.gameObject.SetActive(false);
        if (v.survey) v.survey.gameObject.SetActive(false);
        var z = v.z; float cx = (z.x0 + z.x1) / 2, cy = (z.y0 + z.y1) / 2; Vector3 center = WorldMap.W2U(cx, cy, 0);
        v.floor.gameObject.SetActive(true); JuiceFX.I.Pop(v.floor, 0.001f, 0.7f, 0.35f);   // 되튐 0.35 (§4.3)
        v.fence.gameObject.SetActive(true);
        for (int i = 0; i < v.parts.Count; i++) v.parts[i].position = v.finalPos[i] + Vector3.down * 22 * GameConfig.K;
        JuiceFX.I.Ring(center, Color.white, 60); JuiceFX.I.Ring(center, GameConfig.GOLD, 30);
        JuiceFX.I.Shake(5); JuiceFX.I.Vibrate(40);
        AudioManager.I.Play("up", 1f); AudioManager.I.Play("pad", 0.8f);
        JuiceFX.I.Confetti(center + Vector3.up * 1.5f, 32);
        var vz = GroundPainter.Vis(z);   // 먼지는 보이는 바닥 모서리에서
        foreach (var corner in new[] { new Vector2(vz.x0, vz.y0), new Vector2(vz.x1, vz.y0), new Vector2(vz.x0, vz.y1), new Vector2(vz.x1, vz.y1) }) JuiceFX.I.Dust(WorldMap.W2U(corner, 2), 5);
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

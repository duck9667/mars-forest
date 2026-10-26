using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 이어지는 세계의 땅(docs/chapter3-design.md): 동쪽 지면 한 장, 지역마다 길(크림 타일)·관문(기둥·차단봉)·지역 바닥·측량 점선, 동쪽 메사 테두리.
//  · 잠긴 길·지역: 측량 점선만(기지 북쪽 개척 예정지와 같은 그림). 지역 발판이 노출되면 점선이 윤곽(어두운 면 + 크림 점선)으로 바뀐다.
//  · 길 발판을 사면 차단봉이 올라가고 길이 깔린다, 지역 발판을 사면 바닥이 팝(구역 해제와 같은 연출: 링·색종이·흔들림·먼지).
//  · 기지 동쪽 메사 벽(462,430)을 북·남 둘로 갈라 길이 지나는 고개를 내고, 동쪽 지역 둘레는 기지 메사를 복제해 두른다(WorldDress는 건드리지 않는다).
//  · 옛 저장의 이주 준비금(fund)은 한 번 벨로 돌려준다(S.fundBack).
public class RegionWorld : MonoBehaviour
{
    public static RegionWorld I;
    GameState G => GameState.I;
    SaveData S => G.S;

    class RView
    {
        public Regions.Def r;
        public Transform road, roadSurvey, gate, boom, floor, survey, outline;
        public bool roadOpen, zoneOpen, outlineShown;
        public float boomA;   // 0 닫힘 ~ 1 열림
    }
    readonly List<RView> views = new List<RView>();

    void Awake() { I = this; }

    void Start()
    {
        Regions.Sync(S);
        GroundPainter.EastQuad(transform);
        foreach (var r in Regions.ALL) Build(r);
        G.OnPadLevel += OnPad;
        if (PadManager.I != null) PadManager.I.OnChanged += RefreshOutlines;
        StartCoroutine(CoMesas());
        FundBack();
        RefreshOutlines();
    }
    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPad; if (PadManager.I != null) PadManager.I.OnChanged -= RefreshOutlines; }

    // 옛 저장: 이주 준비금으로 낸 벨을 우주항 건설 기금으로 돌려준다(한 번)
    void FundBack()
    {
        if (S.fundBack != 0) return;
        S.fundBack = 1;
        int lv = G.PadLv("fund"); if (lv <= 0) return;
        var d = Pads.Get("fund"); float sum = 0; for (int l = 0; l < lv; l++) sum += d.Cost(l);
        G.AddBells(sum, "fund");
        foreach (var e in S.pads) if (e.id == "fund") { e.lv = 0; e.prog = 0; }
        Debug.Log("region: 이주 준비금 Lv" + lv + " → 우주항 건설 기금 +" + sum.ToString("0"));
    }

    // ───────────────────────── 조립 ─────────────────────────
    void Build(Regions.Def r)
    {
        var v = new RView { r = r };
        var root = new GameObject("region_" + r.key).transform; root.SetParent(transform, false);
        // 길: 크림 타일(상점 북쪽 띠와 같은 높이·줄눈). 1번 도로는 띠 끝(x 380)을 덮도록 서쪽으로 6 더
        float rx0 = r.roadX0 - (r.roadX0 <= Regions.EAST_X ? 12f : 4f), rx1 = r.x0 + 4f;
        v.road = GroundPainter.TexQuad(root, "road", WorldMap.W2U((rx0 + rx1) / 2f, (Regions.DRAW_Y0 + Regions.DRAW_Y1) / 2f, 0.08f), rx1 - rx0, Regions.DRAW_Y1 - Regions.DRAW_Y0, PaintRoad(rx0, rx1), false).transform;
        var rs = new WorldMap.Zone { key = r.key + "Road", x0 = r.roadX0 + Regions.STUB, y0 = Regions.ROAD_Y0 - 2f, x1 = r.x0, y1 = Regions.ROAD_Y1 + 2f };
        v.roadSurvey = GroundPainter.DashQuad(root, rs, 0.12f, true).transform;
        // 관문: 기둥 둘 + 차단봉(북쪽 기둥 축)
        v.gate = new GameObject("gate").transform; v.gate.SetParent(root, false); v.gate.position = WorldMap.W2U(r.gateX, Regions.ROAD_Y);
        RegionDress.Put(v.gate, "posts", RegionDress.Gate());
        var pivot = new GameObject("boom").transform; pivot.SetParent(v.gate, false);
        pivot.localPosition = new Vector3(5.4f * GameConfig.K, (RegionDress.GATE_POST_H - 9f) * GameConfig.K, RegionDress.GATE_HALF * GameConfig.K);   // 북쪽 기둥 동쪽 면
        RegionDress.Put(pivot, "bar", RegionDress.Boom());
        v.boom = pivot;
        foreach (float sgn in new[] { -1f, 1f })
        {   // 기둥 꼭대기 빨간 등
            var lamp = Prim.Sphere(v.gate, "lamp", new Vector3(0, (RegionDress.GATE_POST_H + 1.5f) * GameConfig.K, -sgn * RegionDress.GATE_HALF * GameConfig.K), 4.4f, GameConfig.Hex("ff5a3c")); Prim.NoShadow(lamp);
        }
        // 지역 바닥·측량 점선·윤곽
        var z = new WorldMap.Zone { key = r.key, x0 = r.x0, y0 = r.y0, x1 = r.x1, y1 = r.y1 };
        v.floor = GroundPainter.TexQuad(root, "floor", WorldMap.W2U(r.Center, 0.1f), r.W, r.H, PaintFloor(r), false).transform;
        v.survey = GroundPainter.DashQuad(root, z, 0.12f, true).transform;
        v.outline = GroundPainter.DashQuad(root, z, 0.12f, false).transform;
        v.roadOpen = Regions.RoadOpen(r); v.zoneOpen = Regions.ZoneOpen(r);
        v.boomA = v.roadOpen ? 1f : 0f; SetBoom(v);
        v.road.gameObject.SetActive(v.roadOpen); v.roadSurvey.gameObject.SetActive(!v.roadOpen);
        v.floor.gameObject.SetActive(v.zoneOpen); v.survey.gameObject.SetActive(!v.zoneOpen); v.outline.gameObject.SetActive(false);
        if (v.roadOpen) RoadLamps(r);
        if (v.zoneOpen) ZoneLamps(r);
        views.Add(v);
    }

    static void SetBoom(RView v) { if (v.boom) v.boom.localRotation = Quaternion.Euler(82f * Mathf.SmoothStep(0, 1, v.boomA), 0, 0); }   // 남쪽 끝이 위로

    void RoadLamps(Regions.Def r)
    {
        if (ZoneManager.I == null) return;
        ZoneManager.I.AddLamp(new Vector2(r.gateX + 34f, Regions.DRAW_Y0 - 8f));
        ZoneManager.I.AddLamp(new Vector2(r.x0 - 26f, Regions.DRAW_Y1 + 8f));
    }
    void ZoneLamps(Regions.Def r)
    {
        if (ZoneManager.I == null) return;
        ZoneManager.I.AddLamp(new Vector2(r.x0 + 10f, r.y0 + 10f));
        ZoneManager.I.AddLamp(new Vector2(r.x1 - 10f, r.y1 - 10f));
    }

    // 차례가 온 지역(지역 발판이 노출 중)은 측량 점선 → 윤곽
    void RefreshOutlines()
    {
        foreach (var v in views)
        {
            if (v.zoneOpen) continue;
            bool show = PadManager.I != null && PadManager.I.visSet.Contains(v.r.zonePad);
            if (show == v.outlineShown) continue;
            v.outlineShown = show;
            v.outline.gameObject.SetActive(show); v.survey.gameObject.SetActive(!show);
            if (show) JuiceFX.I?.Ring(WorldMap.W2U(new Vector2(v.r.x0 + 40f, Regions.ROAD_Y)), GameConfig.GOLD, 40);
        }
    }

    void Update()
    {
        if (G == null) return;
        Regions.stage = S.stage;
        // 테라포밍 땅색(기지 지면 재질)을 동쪽 지면에도
        if (GroundPainter.EastMaterial != null && GroundPainter.GroundMaterial != null) GroundPainter.EastMaterial.color = GroundPainter.GroundMaterial.color;
        foreach (var v in views)
        {
            float target = v.roadOpen ? 1f : 0f;
            if (!Mathf.Approximately(v.boomA, target)) { v.boomA = Mathf.MoveTowards(v.boomA, target, Time.deltaTime / 0.9f); SetBoom(v); }
        }
    }

    // ───────────────────────── 열림 ─────────────────────────
    void OnPad(string id, int lv)
    {
        if (lv < 1) return;
        foreach (var v in views)
        {
            if (id == v.r.roadPad && !v.roadOpen) { Regions.SetOpen(id, true); v.roadOpen = true; StartCoroutine(CoOpenRoad(v)); }
            if (id == v.r.zonePad && !v.zoneOpen) { Regions.SetOpen(id, true); v.zoneOpen = true; StartCoroutine(CoOpenZone(v)); }
        }
    }

    IEnumerator CoOpenRoad(RView v)
    {
        var r = v.r;
        v.roadSurvey.gameObject.SetActive(false);
        v.road.gameObject.SetActive(true);
        JuiceFX.I.Pop(v.road, 0.001f, 0.6f, 0.3f);
        var gp = v.gate.position;
        JuiceFX.I.Ring(gp, Color.white, 50); JuiceFX.I.Ring(gp, GameConfig.GOLD, 26);
        JuiceFX.I.Shake(4); JuiceFX.I.Vibrate(30);
        AudioManager.I.Play("up", 1f); AudioManager.I.Play("whoosh", 0.8f, 0.6f);
        // 길 따라 먼지가 동쪽으로 번진다
        for (float x = r.gateX; x <= r.x0; x += 46f) { JuiceFX.I.Dust(WorldMap.W2U(x, Regions.ROAD_Y, 2f), 3); yield return new WaitForSeconds(0.05f); }
        RoadLamps(r);
    }

    IEnumerator CoOpenZone(RView v)
    {
        var r = v.r;
        v.survey.gameObject.SetActive(false); v.outline.gameObject.SetActive(false);
        v.floor.gameObject.SetActive(true);
        JuiceFX.I.Pop(v.floor, 0.001f, 0.7f, 0.35f);
        var c = WorldMap.W2U(r.Center);
        JuiceFX.I.Ring(c, Color.white, 80); JuiceFX.I.Ring(c, GameConfig.GOLD, 40);
        JuiceFX.I.Shake(5); JuiceFX.I.Vibrate(40);
        AudioManager.I.Play("up", 1f); AudioManager.I.Play("pad", 0.8f);
        JuiceFX.I.Confetti(c + Vector3.up * 1.5f, 32);
        foreach (var k in new[] { new Vector2(r.x0, r.y0), new Vector2(r.x1, r.y0), new Vector2(r.x0, r.y1), new Vector2(r.x1, r.y1) }) JuiceFX.I.Dust(WorldMap.W2U(k, 2), 5);
        ZoneLamps(r);
        yield break;
    }

    // ───────────────────────── 바닥 그림 ─────────────────────────
    static readonly Color GROUND = GameConfig.Hex("c97754"), SEAM = GameConfig.Hex("8a5a3a");
    static Color C(int r, int g, int b) => GroundPainter.C(r, g, b);

    // 길: 땅색 위 크림 타일(알파 .96, 기지 띠와 같은 칠), 세계 좌표 20 격자 줄눈, 위·아래 가장자리 선. 서·동 끝은 다른 바닥이 덮으므로 선 없음
    static Texture2D PaintRoad(float x0, float x1)
    {
        float y0 = Regions.DRAW_Y0, y1 = Regions.DRAW_Y1;
        var p = new GroundPainter(x0, y0, x1 - x0, y1 - y0, 2f, 41, GROUND);
        Tiles(p, x0 - 8f, y0, x1 + 8f, y1);
        return p.ToTexture(false);
    }
    // 크림 타일 띠 하나(세계 좌표). 가로 띠의 끝(x)은 바깥으로 넘겨 그려 선이 안 남게
    static void Tiles(GroundPainter p, float x0, float y0, float x1, float y1)
    {
        p.Rect(x0, y0, x1 - x0, y1 - y0, GroundPainter.PATH_TILE, 0.96f);
        for (float gx = Mathf.Ceil((x0 + 1f) / 20f) * 20f; gx < x1 - 1f; gx += 20f) p.Line(gx, y0 + 1f, gx, y1 - 1f, 1f, C(120, 70, 40), 0.10f);
        for (float gy = Mathf.Ceil((y0 + 1f) / 20f) * 20f; gy < y1 - 1f; gy += 20f) p.Line(x0 + 1f, gy, x1 - 1f, gy, 1f, C(120, 70, 40), 0.10f);
        p.Line(x0, y0 + 0.75f, x1, y0 + 0.75f, 3f, C(150, 90, 50), 0.22f);
        p.Line(x0, y1 - 0.75f, x1, y1 - 0.75f, 3f, C(150, 90, 50), 0.22f);
    }
    // 세로 갈래(광장 길에서 시설·착륙장으로): 세로 띠 + 좌우 가장자리 선
    static void Spur(GroundPainter p, float cx, float ya, float yb, float w = 30f)
    {
        float x0 = cx - w / 2f, x1 = cx + w / 2f, y0 = Mathf.Min(ya, yb), y1 = Mathf.Max(ya, yb);
        p.Rect(x0, y0, w, y1 - y0, GroundPainter.PATH_TILE, 0.96f);
        for (float gx = Mathf.Ceil((x0 + 1f) / 20f) * 20f; gx < x1 - 1f; gx += 20f) p.Line(gx, y0 + 1f, gx, y1 - 1f, 1f, C(120, 70, 40), 0.10f);
        for (float gy = Mathf.Ceil((y0 + 1f) / 20f) * 20f; gy < y1 - 1f; gy += 20f) p.Line(x0 + 1f, gy, x1 - 1f, gy, 1f, C(120, 70, 40), 0.10f);
        p.Line(x0 + 0.75f, y0, x0 + 0.75f, y1, 3f, C(150, 90, 50), 0.22f);
        p.Line(x1 - 0.75f, y0, x1 - 0.75f, y1, 3f, C(150, 90, 50), 0.22f);
    }
    // 콘크리트 받침 판(착륙장·시설 터): 연회색 판 + 판 줄눈 + 둥근 가장자리 선
    static void Apron(GroundPainter p, float cx, float cy, float w, float h)
    {
        float x0 = cx - w / 2f, y0 = cy - h / 2f;
        p.RoundRect(x0, y0, w, h, 8f, GroundPainter.SLAB_FLOOR, 1f);
        for (float x = x0 + 24f; x < x0 + w - 4f; x += 24f) p.Line(x, y0 + 3f, x, y0 + h - 3f, 1.1f, C(85, 75, 65), 0.16f);
        for (float y = y0 + 24f; y < y0 + h - 4f; y += 24f) p.Line(x0 + 3f, y, x0 + w - 3f, y, 1.1f, C(85, 75, 65), 0.16f);
        p.Polyline(GroundPainter.RoundRectPath(x0 + 1f, y0 + 1f, w - 2f, h - 2f, 8f), 2.2f, C(110, 70, 45), 0.28f, true);
    }

    static Texture2D PaintFloor(Regions.Def r)
    {
        var p = new GroundPainter(r.x0, r.y0, r.W, r.H, 2f, 53 + r.key.Length, r.floor);
        p.Grain(0.022f);
        if (r.key == "canyon")
        {
            // 서리 낀 땅: 광맥 쪽(북동)으로 갈수록 옅은 하늘빛 큰 얼룩(잔무늬 없이 넓게)
            p.Ellipse(1520f, 300f, 170f, 120f, 0.3f, C(225, 236, 245), 0.10f);
            p.Ellipse(1490f, 330f, 110f, 80f, -0.2f, C(235, 244, 250), 0.08f);
        }
        p.EdgeShade(12f, 0.12f, C(90, 35, 15));
        // 광장 길(1번·2번 도로를 잇는 동서 띠)과 갈래
        if (r.key == "port")
        {
            foreach (var pad in SpacePort.PADS) { Apron(p, pad.x, pad.y, 104f, 104f); Spur(p, pad.x, pad.y + 50f, Regions.DRAW_Y0 + 2f); }
            Apron(p, SpacePort.FUEL.x, SpacePort.FUEL.y, 122f, 88f);
            Apron(p, SpacePort.LAUNCH.x, SpacePort.LAUNCH.y, 110f, 104f);
            Apron(p, SpacePort.TOWER.x, SpacePort.TOWER.y, 76f, 76f);
            Spur(p, SpacePort.FUEL.x, Regions.DRAW_Y1 - 2f, SpacePort.FUEL.y - 42f);
            Spur(p, SpacePort.LAUNCH.x - 6f, Regions.DRAW_Y1 - 2f, SpacePort.LAUNCH.y - 50f);
            Spur(p, SpacePort.TOWER.x, Regions.DRAW_Y1 - 2f, SpacePort.TOWER.y - 36f);
            Tiles(p, r.x0 - 10f, Regions.DRAW_Y0, r.x1 + 10f, Regions.DRAW_Y1);
        }
        else
        {
            Apron(p, IceCanyon.REFINERY.x, IceCanyon.REFINERY.y, 122f, 92f);
            Spur(p, IceCanyon.REFINERY.x, Regions.DRAW_Y1 - 2f, IceCanyon.REFINERY.y - 44f);
            Tiles(p, r.x0 - 10f, Regions.DRAW_Y0, 1420f, Regions.DRAW_Y1);
            p.RoundRect(1400f, Regions.DRAW_Y0, 40f, Regions.DRAW_Y1 - Regions.DRAW_Y0, 18f, GroundPainter.PATH_TILE, 0.96f);   // 길 끝은 둥글게(로버 마당으로 이어짐)
        }
        return p.ToTexture(false);
    }

    // ───────────────────────── 메사 ─────────────────────────
    // 기지 동쪽 벽(462,430)을 길 고개 북·남 둘로, 동쪽 지역 둘레는 기지 메사를 복제해 두른다(크기·방향만 바꿔 같은 지층 그림)
    // (x, y, 반지름 x, 반지름 y) 논리. 남쪽 벽은 앞쪽이라 낮게(EAST_MESA_H 높이 배율)
    static readonly Vector4[] EAST_MESAS =
    {
        // 1번 도로 고개 동쪽(길 북·남)
        new Vector4(584, 262, 44, 82), new Vector4(588, 566, 44, 72),
        // 북쪽 벽(지역 위): 지역 띠가 화면보다 낮아 늘 보이므로 낮고 납작하게
        new Vector4(700, 128, 92, 62), new Vector4(856, 122, 96, 60), new Vector4(1010, 128, 92, 62),
        new Vector4(1160, 268, 56, 96), new Vector4(1310, 108, 96, 60), new Vector4(1466, 102, 96, 60), new Vector4(1620, 108, 92, 62),
        // 남쪽 벽(지역 아래, 앞쪽이라 더 낮게)
        new Vector4(700, 734, 92, 62), new Vector4(856, 740, 96, 62), new Vector4(1010, 734, 92, 62),
        new Vector4(1160, 566, 56, 96), new Vector4(1310, 754, 96, 62), new Vector4(1466, 760, 96, 62), new Vector4(1620, 754, 92, 62),
        // 동쪽 끝
        new Vector4(1764, 300, 70, 140), new Vector4(1764, 580, 70, 140),
        // 얼음 협곡 입구 절벽(길 북·남, IceCanyon.GATE_CLIFFS 충돌과 같은 자리)·안쪽 턱
        new Vector4(1274, 326, 36, 36), new Vector4(1274, 508, 34, 34), new Vector4(1390, 214, 40, 26), new Vector4(1640, 640, 44, 30),
    };
    static readonly float[] EAST_MESA_H = { 0.7f, 0.45f, 0.6f, 0.62f, 0.58f, 0.7f, 0.62f, 0.66f, 0.6f, 0.32f, 0.3f, 0.34f, 0.45f, 0.32f, 0.3f, 0.34f, 0.8f, 0.7f, 0.62f, 0.42f, 0.4f, 0.3f };

    IEnumerator CoMesas()
    {
        yield return null;   // World.Start(메사 생성)가 먼저
        var world = GameObject.Find("World"); if (world == null) yield break;
        var rocks = world.transform.Find("rocks"); if (rocks == null) yield break;
        var mesas = new List<Transform>();
        foreach (Transform t in rocks) if (t.name == "mesa") mesas.Add(t);
        if (mesas.Count == 0) yield break;
        var holder = new GameObject("eastMesas").transform; holder.SetParent(transform, false);
        // 고개: (462,430) 메사를 북쪽(y 290, 세로 0.6배)으로 옮기고, 남쪽은 복제(y 520)
        Transform pass = null; float best = float.MaxValue;
        foreach (var t in mesas) { float d = (WorldMap.U2W(t.position) - new Vector2(462, 430)).sqrMagnitude; if (d < best) { best = d; pass = t; } }
        if (pass != null && best < 40 * 40)
        {
            var south = Instantiate(pass.gameObject, holder).transform; south.name = "mesaPassS";
            south.position = WorldMap.W2U(470, 535); south.localScale = new Vector3(1f, 0.85f, 0.55f); south.rotation = Quaternion.Euler(0, 180, 0);
            pass.position = WorldMap.W2U(466, 290); pass.localScale = new Vector3(1f, 1f, 0.6f);
        }
        for (int i = 0; i < EAST_MESAS.Length; i++)
        {
            var m = EAST_MESAS[i]; var src = mesas[(i * 5 + 3) % mesas.Count];
            var mf = src.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var sz = mf.sharedMesh.bounds.size / GameConfig.K;   // 원본 메사 크기(논리, 회전 없음)
            var c = Instantiate(src.gameObject, holder).transform; c.name = "mesaEast";
            c.position = WorldMap.W2U(m.x, m.y);
            c.rotation = Quaternion.Euler(0, i % 2 == 0 ? 0f : 180f, 0);   // 옆면 지층 띠가 앞(남)을 보게 0·180만
            c.localScale = new Vector3(m.z * 2f / Mathf.Max(1f, sz.x), EAST_MESA_H[i], m.w * 2f / Mathf.Max(1f, sz.z));
        }
    }
}

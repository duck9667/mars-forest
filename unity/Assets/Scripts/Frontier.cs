using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 2장 개척지(docs/chapter2-design.md "D 개척지"): 개척 길(y −410~−380) 너머 두 구역.
//  · 거주 구역(hab, x 16~236): 작은 둥근 돔 주택 8채가 크림 타일 길 두 줄을 따라 선다. 결제를 마친 정착민 3명 중 1명이 가운데 통로를 걸어 올라와
//    집에 들어가 산다(S.pop, 상한 = 집 수×4). 사람이 찰수록 창에 하나씩 불이 켜지고(밤엔 문 앞 바닥빛까지), 인구는 정착민 도착 간격을 조금 줄이고(GapMul)
//    하루가 바뀔 때 인구×작은 세금 벨을 낸다.
//  · 태양광 단지(solar, x 256~380): 패널 줄 3 + 짧은 줄 3이 낮엔 해 쪽으로 기울고 밤엔 눕는다. 배터리 탑 막대 5칸이 충전량.
//    전력: 햇빛만큼 + 밤엔 배터리가 남은 동안 모든 기계 속도 +20%(PowerMul → GameState.MachineSpeedMul).
// 바닥 길(크림 타일)은 GroundPainter.PaintFloor가 Walks()를 받아 구역 바닥에 그린다. 구역이 잠겨 있으면 모형은 숨김(해제 순간 차례로 솟음).
// 검증 인자: -pop N(인구 미리 채우기), -batt f(배터리 0~1)
public class Frontier : MonoBehaviour
{
    public static Frontier I;
    GameState G => GameState.I;
    SaveData S => G.S;

    // ================= 배치(논리 좌표) =================
    public const int PER_HOUSE = 4;
    public const float HOUSE_R = 19f;
    const float WALK_A_Y = -515f, WALK_B_Y = -437f, SPINE_X = 126f, AISLE_X = 246f;
    // 뒷줄 4채(문이 길 A 쪽) · 앞줄 4채(문이 길 B 쪽), 가운데(x 114~138)는 길 A ↔ 남쪽 게이트를 잇는 척추 길
    public static readonly Vector2[] HOUSES =
    {
        new Vector2(48, -556), new Vector2(92, -556), new Vector2(160, -556), new Vector2(204, -556),
        new Vector2(48, -474), new Vector2(92, -474), new Vector2(160, -474), new Vector2(204, -474),
    };
    public int Cap => HOUSES.Length * PER_HOUSE;

    // 구역 바닥에 그릴 크림 타일 길(논리 사각형 x, y, w, h). 거주: 길 A(동쪽 게이트 y −515)·척추(남쪽 게이트 x 126)·길 B(앞줄 문 앞)
    // 태양광: 서쪽 게이트 길·남쪽 게이트 길(배터리·짧은 패널 사이)
    static readonly Rect[] HAB_WALKS = { new Rect(24, -527, 212, 24), new Rect(114, -503, 24, 93), new Rect(28, -446, 196, 18) };
    static readonly Rect[] SOLAR_WALKS = { new Rect(256, -527, 116, 24), new Rect(306, -503, 24, 93) };
    public static Rect[] Walks(string zone) => zone == "hab" ? HAB_WALKS : zone == "solar" ? SOLAR_WALKS : null;

    // 패널 줄: (중심 x, 중심 y, 길이). 북쪽 긴 줄 3 + 남동쪽 짧은 줄 3
    static readonly Vector3[] ROWS =
    {
        new Vector3(318, -601, 104), new Vector3(318, -575, 104), new Vector3(318, -549, 104),
        new Vector3(354, -484, 38), new Vector3(354, -458, 38), new Vector3(354, -432, 38),
    };
    static readonly Vector2 BATTERY = new Vector2(278, -470), INVERTER = new Vector2(286, -432);

    // ================= 상태 =================
    public bool HabOpen => G != null && G.PadLv("hab") >= 1;
    public bool SolarOpen => G != null && G.PadLv("solar") >= 1;
    public float power = 1f;                 // 기계 속도 배율(1 ~ 1.2)
    public float batt = 0.5f;                // 배터리 0~1(저장 안 함: 켤 때 반쯤)
    public static float PowerMul => I != null ? I.power : 1f;
    // 인구가 늘면 정착민 도착 간격이 조금 줄어든다(최대 −20%)
    public float GapMul => 1f - Mathf.Min(0.2f, (G != null ? S.pop : 0) * 0.0065f);
    public const float MOVE_CHANCE = 1f / 3f, CHARGE = 1f / 60f, DRAIN = 1f / 50f, BOOST = 0.2f;

    class Mover { public Customer c; public int house; }
    readonly List<Mover> movers = new List<Mover>();
    int lastDay = -1;
    static bool argsDone;
    bool qaLog;

    void Awake() { I = this; }

    void Start()
    {
        qaLog = DevArgs.Has("-shot");
        Build();
        if (!argsDone)
        {
            argsDone = true;
            if (int.TryParse(DevArgs.Get("-pop"), out var pv)) S.pop = Mathf.Clamp(pv, 0, Cap);
            var bv = DevArgs.Get("-batt");
            if (bv != null && float.TryParse(bv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bf)) batt = Mathf.Clamp01(bf);
        }
        S.pop = Mathf.Clamp(S.pop, 0, Cap);
        habRoot.gameObject.SetActive(HabOpen); solarRoot.gameObject.SetActive(SolarOpen);
        habShown = HabOpen; solarShown = SolarOpen;
        G.OnPadLevel += OnPad;
    }
    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPad; foreach (var m in owned) if (m != null) Destroy(m); }

    void OnPad(string id, int lv)
    {
        if (id == "hab" && lv >= 1 && !habShown) { habShown = true; StartCoroutine(Reveal(habRoot, houseViews)); }
        if (id == "solar" && lv >= 1 && !solarShown) { solarShown = true; StartCoroutine(Reveal(solarRoot, solarParts)); }
    }

    // 구역 해제 연출(ZoneManager 바닥 팝) 뒤에 모형이 하나씩 솟는다
    IEnumerator Reveal(Transform root, List<Transform> parts)
    {
        foreach (var p in parts) p.gameObject.SetActive(false);
        root.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.45f);
        foreach (var p in parts)
        {
            p.gameObject.SetActive(true);
            JuiceFX.I.Pop(p, 0.001f, 0.5f);
            JuiceFX.I.Dust(p.position + Vector3.up * 0.2f, 3);
            AudioManager.I.Play("pop", 1.1f + Random.value * 0.3f, 0.35f);
            yield return new WaitForSeconds(0.09f);
        }
    }

    // ================= 정착민 입주 (Customers.AfterPurchase가 부른다) =================
    // 결제·점프를 마친 정착민 3명 중 1명: 빈 자리가 있으면 경유점을 채워 Leave 상태로 보내고 true. 도착(Gone)하면 Update가 입주 처리
    public bool TryMoveIn(Customer c)
    {
        if (!HabOpen || S.pop + movers.Count >= Cap || Random.value > MOVE_CHANCE) return false;
        int h = Mathf.Min(HOUSES.Length - 1, (S.pop + movers.Count) / PER_HOUSE);
        c.wp = PathTo(h, c.pos); c.st = Customer.St.Leave;
        movers.Add(new Mover { c = c, house = h });
        return true;
    }

    // 가게 동쪽(계산대 옆)으로 돌아 북쪽 띠 → 가운데 통로(x 246)를 따라 올라와 거주 구역 동쪽 게이트(y −515) → 길 → 집 문 → 안
    static List<Vector2> PathTo(int h, Vector2 from)
    {
        var p = HOUSES[h]; bool back = h < 4;
        var l = new List<Vector2>
        {
            new Vector2(from.x, 548), new Vector2(354, 548), new Vector2(354, 412), new Vector2(AISLE_X, 412),
            new Vector2(AISLE_X, WALK_A_Y),
        };
        if (back) { l.Add(new Vector2(p.x, WALK_A_Y)); l.Add(new Vector2(p.x, p.y + 6)); }
        else { l.Add(new Vector2(SPINE_X, WALK_A_Y)); l.Add(new Vector2(SPINE_X, WALK_B_Y)); l.Add(new Vector2(p.x, WALK_B_Y)); l.Add(new Vector2(p.x, p.y + 6)); }
        return l;
    }

    void Arrive(int h)
    {
        S.pop = Mathf.Min(Cap, S.pop + 1);
        var v = houseViews[h];
        JuiceFX.I.Squash(v, 0.6f);
        JuiceFX.I.Dust(WorldMap.W2U(HOUSES[h].x, HOUSES[h].y + HOUSE_R + 6, 2f), 3);
        AudioManager.I.Play("pop", 1.5f, 0.4f);
        if (qaLog) Debug.Log("frontier: 입주 house=" + h + " pop=" + S.pop + "/" + Cap);
    }

    // ================= 세금: 하루가 바뀌면 인구 × 작은 세금 =================
    public int TaxEach => Mathf.Max(1, Mathf.FloorToInt(G.BottlePrice * 0.25f + 0.5f));
    void Tax()
    {
        int n = S.pop * TaxEach; if (n <= 0) return;
        G.AddBells(n);   // 즉시 가산, 동전은 연출
        var cam = Camera.main; Vector3 from = WorldMap.W2U(SPINE_X, -490, 2);
        JuiceFX.I.CoinPop(from, 5);
        if (cam != null && HUD.I != null)
        {
            Vector2 sp = cam.WorldToScreenPoint(from);
            sp.x = Mathf.Clamp(sp.x, Screen.width * 0.1f, Screen.width * 0.9f); sp.y = Mathf.Clamp(sp.y, Screen.height * 0.15f, Screen.height * 0.85f);   // 화면 밖이면 가장자리에서
            HUD.I.Coins(sp, () => HUD.I.BellsScreen(), Mathf.Clamp(S.pop / 3, 3, 8));
        }
        StartCoroutine(CoTaxLabel(n));
        if (qaLog) Debug.Log("frontier: 세금 day=" + S.day + " pop=" + S.pop + " +" + n + " bells=" + S.bells.ToString("0"));
    }
    IEnumerator CoTaxLabel(int n)
    {
        yield return new WaitForSeconds(0.9f);   // "n일째" 글자와 겹치지 않게 조금 뒤에
        if (Player.I != null) WorldLabels.I.Float(Player.I.view.position + Vector3.up * 2.2f, "세금 +" + n, true, false);
        AudioManager.I.Play("coin", 1.2f, 0.6f);
    }

    // ================= 매 프레임 =================
    float animT;
    void Update()
    {
        if (G == null) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX); animT += dt;
        float night = DayNight.I != null ? DayNight.I.night : 0f, sun = 1f - night;
        // 전력: 햇빛만큼 쓰고 남는 만큼 충전, 어두워지면 배터리로 메운다
        if (SolarOpen)
        {
            bool backup = batt > 0.001f;
            float f = sun + (1f - sun) * (backup ? 1f : 0f);
            batt = Mathf.Clamp01(batt + (sun * CHARGE - (backup ? (1f - sun) * DRAIN : 0f)) * dt);
            power = 1f + BOOST * f;
        }
        else power = 1f;
        // 입주: 경유점을 다 걸어 집 안으로 들어가면(Gone) 인구 +1
        for (int i = movers.Count - 1; i >= 0; i--)
            if (movers[i].c.st == Customer.St.Gone) { Arrive(movers[i].house); movers.RemoveAt(i); }
        // 하루가 바뀌면 세금
        if (S.day != lastDay)
        {
            if (lastDay > 0 && S.day > lastDay && HabOpen) Tax();
            lastDay = S.day;
        }
        Animate(dt, night);
    }

    // ================= 모형 =================
    static Vector3 L(float dx, float dy, float h) => new Vector3(dx, h, -dy) * GameConfig.K;   // 중심 기준 논리 오프셋(dy+ = 남쪽) → 로컬
    static readonly Color SHELL = MachineKit.SHELL, SHELL_D = MachineKit.SHELL_D, PAD = MachineKit.PAD, STEEL_D = MachineKit.STEEL_D;
    static readonly Color WIN_OFF = GameConfig.Hex("55657e"), WIN_ON = GameConfig.Hex("ffd27a"), PANEL = GameConfig.Hex("2f5fa8"), PANEL_HI = GameConfig.Hex("6f9fe0");
    static readonly Color BAR_ON = GameConfig.Hex("7fd36a"), BAR_OFF = GameConfig.Hex("3a3f4a");
    // 집마다 띠·문 색(흰 외피 + 부드러운 강조색 하나)
    static readonly Color[] ACCENT = { GameConfig.Hex("6fb6f0"), GameConfig.Hex("f08a6a"), GameConfig.Hex("5ec8bf"), GameConfig.Hex("a99be8") };

    Transform habRoot, solarRoot; bool habShown, solarShown;
    readonly List<Transform> houseViews = new List<Transform>(), solarParts = new List<Transform>();
    readonly List<Renderer> windows = new List<Renderer>();      // 집 순서대로 창 1개씩
    readonly List<Renderer> porch = new List<Renderer>();        // 집마다 문 위 등
    readonly List<Renderer> pools = new List<Renderer>();        // 집마다 문 앞 바닥빛
    readonly List<Transform> tilts = new List<Transform>();      // 패널 기울기 축
    readonly List<Renderer> bars = new List<Renderer>();         // 배터리 막대 5칸(아래부터)
    Renderer battLamp;
    Material winOn, winOff, poolMat, barOn, barOff;
    readonly List<Material> owned = new List<Material>();
    int lastLit = -1, lastBars = -1; float tilt; bool tiltInit;

    Material Emissive(Color c, Color em)
    {
        var src = Resources.Load<Material>("Mats/Emissive");
        var m = src != null ? new Material(src) : new Material(Mats.Solid(c));
        m.color = c; m.SetColor("_EmissionColor", em); owned.Add(m); return m;
    }

    void Build()
    {
        winOn = Emissive(WIN_ON, WIN_ON * 0.3f); winOff = Mats.Solid(WIN_OFF);
        barOn = Emissive(BAR_ON, BAR_ON * 0.5f); barOff = Mats.Solid(BAR_OFF);
        var pc = GameConfig.Hex("ffd27a"); pc.a = 0f; poolMat = Mats.Trans(pc); owned.Add(poolMat);
        habRoot = new GameObject("hab").transform; habRoot.SetParent(transform, false);
        solarRoot = new GameObject("solar").transform; solarRoot.SetParent(transform, false);
        for (int i = 0; i < HOUSES.Length; i++) BuildHouse(i);
        BuildPlaza();
        foreach (var r in ROWS) BuildRow(r);
        BuildBattery(); BuildInverter();
    }

    // 둥근 돔 주택(목표 그림 docs/art/target_frontier_v1.png): 얇은 받침 → 낮고 넓은 흰 돔(이글루처럼) + 아래 강조색 띠 →
    // 남쪽으로 짧게 튀어나온 현관과 강조색 문, 동쪽 앞 둥근 창 1개(입주하면 불), 꼭대기 작은 환기구, 문 위 작은 등
    void BuildHouse(int i)
    {
        var p = HOUSES[i]; var acc = ACCENT[i % ACCENT.Length]; const float R = HOUSE_R, DH = 14.5f;   // DH: 돔 높이(반지름보다 낮게: 이글루처럼 넓적하게)
        var v = new GameObject("house" + i).transform; v.SetParent(habRoot, false); v.position = WorldMap.W2U(p);
        houseViews.Add(v);
        Prim.Cyl(v, "plinth", L(0, 0, 1.5f), R * 2 + 4, 3, PAD);
        var dome = Prim.Sphere(v, "dome", L(0, 0, 3), R * 2, SHELL);
        dome.transform.localScale = new Vector3(R * 2, DH * 2, R * 2) * GameConfig.K;
        Prim.Cyl(v, "band", L(0, 0, 5.5f), R * 2 + 1.0f, 4f, acc);
        // 현관: 가로 원통(축 남북) + 앞 마개 + 문
        var tun = Prim.Cyl(v, "porch", L(0, R - 3, 6), 17, 8, SHELL); tun.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim.Cyl(v, "porchRim", L(0, R + 1.0f, 6), 17.6f, 1.4f, SHELL_D).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim.Cyl(v, "door", L(0, R + 1.8f, 5.6f), 11.5f, 0.8f, acc).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim.Box(v, "doorSill", L(0, R + 2.2f, 0.6f), new Vector3(12, 1.2f, 1.5f), acc * 0.8f + Color.black * 0.2f);
        var lamp = Prim.Sphere(v, "porchLamp", L(0, R + 0.5f, 14.4f), 3.2f, SHELL_D); Prim.NoShadow(lamp); porch.Add(lamp.GetComponent<Renderer>());
        // 둥근 창 1개: 돔(타원체) 표면 위, 남쪽에서 동쪽으로 50°·높이 30° (창 둘 + 문이면 얼굴처럼 보여 하나만)
        foreach (float az in new[] { 50f })
        {
            float a = az * Mathf.Deg2Rad, e = 32f * Mathf.Deg2Rad;
            float dx = Mathf.Sin(a) * Mathf.Cos(e) * R, dy = Mathf.Cos(a) * Mathf.Cos(e) * R, dh = Mathf.Sin(e) * DH;
            var n = new Vector3(dx / (R * R), dh / (DH * DH), -dy / (R * R)).normalized;   // 타원체 법선(로컬, z = 북)
            var rot = Quaternion.FromToRotation(Vector3.up, n);
            var frame = Prim.Cyl(v, "winFrame", L(dx, dy, 3 + dh) + n * 0.02f, 12.5f, 1.4f, SHELL_D); frame.transform.localRotation = rot;
            var w = Prim.Cyl(v, "win", L(dx, dy, 3 + dh) + n * 0.06f, 9.4f, 1.2f, WIN_OFF); w.transform.localRotation = rot; Prim.NoShadow(w);
            windows.Add(w.GetComponent<Renderer>());
        }
        Prim.Cyl(v, "vent", L(0, -2, 3 + DH - 0.4f), 6, 3, SHELL_D);
        Prim.Cyl(v, "ventCap", L(0, -2, 3 + DH + 1.4f), 7.4f, 1.2f, acc);
        // 문 앞 바닥빛(밤에 입주한 집만)
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(pool.GetComponent<Collider>()); pool.name = "pool";
        pool.transform.SetParent(v, false); pool.transform.localPosition = L(0, R + 7, 0.25f); pool.transform.localScale = new Vector3(34, 0.2f, 22) * GameConfig.K;
        var pr = pool.GetComponent<Renderer>(); pr.sharedMaterial = poolMat; Prim.NoShadow(pool); pools.Add(pr);
    }

    // 뒷줄 가운데(척추 길 끝): 둥근 화단 하나(마을 광장 느낌)
    void BuildPlaza()
    {
        var g = new GameObject("plaza").transform; g.SetParent(habRoot, false); g.position = WorldMap.W2U(SPINE_X, -560);
        houseViews.Add(g);   // 해제 연출 차례에 함께(입주 대상 아님: HOUSES 수만큼만 인덱스로 쓴다)
        Prim.Cyl(g, "rim", L(0, 0, 2.5f), 26, 5, MachineKit.PAD_TOP);
        Prim.Cyl(g, "soil", L(0, 0, 5.1f), 22, 0.4f, GameConfig.SOIL);
        Prim.Cyl(g, "trunk", L(0, 0, 9), 3, 10, GameConfig.Hex("8a5a3a"));
        Prim.Sphere(g, "crown", L(0, 0, 17), 17, GameConfig.Hex("6fb35a"));
        Prim.Sphere(g, "crown2", L(-3, 2, 22), 11, GameConfig.Hex("8cc96a"));
    }

    // 패널 줄(목표 그림): 줄마다 네모난 패널 모듈 낱장(폭 30 안팎, 틈 5)이 짧은 기둥 하나씩 위에 올라 각자 기운다.
    // 모듈 = 흰 테 + 파란 판 + 옅은 칸 줄(가로 1·세로 2) + 반사 한 줄
    void BuildRow(Vector3 r)
    {
        float len = r.z; const float D = 21f, H = 8f, GAP = 5f;
        var g = new GameObject("row").transform; g.SetParent(solarRoot, false); g.position = WorldMap.W2U(r.x, r.y);
        solarParts.Add(g);
        int n = Mathf.Max(1, Mathf.RoundToInt((len + GAP) / 35f)); float mw = (len - GAP * (n - 1)) / n;
        for (int j = 0; j < n; j++)
        {
            float x = -len / 2 + mw / 2 + j * (mw + GAP);
            Prim.Cyl(g, "foot", L(x, 0, 0.8f), 8, 1.6f, MachineKit.PAD_TOP);
            Prim.Cyl(g, "post", L(x, 0, H / 2), 3.6f, H, SHELL_D);
            var t = new GameObject("tilt").transform; t.SetParent(g, false); t.localPosition = L(x, 0, H + 1.2f);
            tilts.Add(t);
            Prim.Box(t, "frame", L(0, 0, 0), new Vector3(mw, 2.2f, D), SHELL);
            Prim.Box(t, "cells", L(0, 0, 1.2f), new Vector3(mw - 3.4f, 0.6f, D - 3.4f), PANEL);
            Prim.Box(t, "hLine", L(0, 0, 1.55f), new Vector3(mw - 3.4f, 0.2f, 0.7f), PANEL_HI);
            for (int c = 1; c < 3; c++) Prim.Box(t, "vLine", L(-mw / 2 + 1.7f + (mw - 3.4f) * c / 3f, 0, 1.55f), new Vector3(0.7f, 0.2f, D - 3.4f), PANEL_HI);
            Prim.Box(t, "glint", L(-mw / 2 + 7, -D / 2 + 4.5f, 1.6f), new Vector3(7, 0.2f, 2.4f), GameConfig.Hex("a9c8f2"));   // 햇빛 반사
        }
    }

    // 배터리 탑: 받침 → 통통한 흰 원통 + 둥근 뚜껑, 남쪽 면에 어두운 창과 초록 막대 5칸(충전량), 꼭대기 작은 등
    void BuildBattery()
    {
        var g = new GameObject("battery").transform; g.SetParent(solarRoot, false); g.position = WorldMap.W2U(BATTERY);
        solarParts.Add(g);
        Prim.Cyl(g, "plinth", L(0, 0, 1.5f), 36, 3, PAD);
        Prim.Cyl(g, "body", L(0, 0, 22), 28, 38, SHELL);
        var cap = Prim.Sphere(g, "cap", L(0, 0, 41), 28, SHELL); cap.transform.localScale = new Vector3(28, 14, 28) * GameConfig.K;
        Prim.Cyl(g, "band", L(0, 0, 6), 29.2f, 5, BAR_ON * 0.6f + SHELL_D * 0.4f);
        Prim.Cyl(g, "bandTop", L(0, 0, 38), 29.2f, 2.4f, SHELL_D);
        Prim.Box(g, "gauge", L(0, 12.6f, 22), new Vector3(10, 30, 3), STEEL_D);
        for (int j = 0; j < 5; j++)
        {
            var b = Prim.Box(g, "bar", L(0, 14.2f, 11 + j * 5.6f), new Vector3(7, 4, 1), BAR_OFF); Prim.NoShadow(b);
            bars.Add(b.GetComponent<Renderer>());
        }
        var lamp = Prim.Sphere(g, "lamp", L(0, 0, 49), 5, BAR_OFF); Prim.NoShadow(lamp); battLamp = lamp.GetComponent<Renderer>();
    }

    // 변환기: 작은 둥근 상자 + 환풍기 원판(전력이 흐르면 돈다)
    Transform fan;
    void BuildInverter()
    {
        var g = new GameObject("inverter").transform; g.SetParent(solarRoot, false); g.position = WorldMap.W2U(INVERTER);
        solarParts.Add(g);
        Prim.Box(g, "base", L(0, 0, 1.2f), new Vector3(24, 2.4f, 16), PAD);
        Prim.Box(g, "body", L(0, 0, 9), new Vector3(20, 14, 12), SHELL);
        Prim.Box(g, "lid", L(0, 0, 16.6f), new Vector3(21, 1.6f, 13), SHELL_D);
        Prim.Cyl(g, "fanRim", L(-4, 6.2f, 9), 9, 0.8f, SHELL_D).transform.localRotation = Quaternion.Euler(90, 0, 0);
        fan = new GameObject("fan").transform; fan.SetParent(g, false); fan.localPosition = L(-4, 6.8f, 9);
        Prim.Box(fan, "a", Vector3.zero, new Vector3(7, 1.2f, 0.6f), STEEL_D);
        Prim.Box(fan, "b", Vector3.zero, new Vector3(1.2f, 7, 0.6f), STEEL_D);
        Prim.Box(g, "led", L(5, 6.2f, 11), new Vector3(3, 3, 0.6f), BAR_ON);
    }

    void Animate(float dt, float night)
    {
        // 창: 첫 입주자가 들어온 집부터 하나씩(집 순서대로 4명씩 찬다). 낮엔 은은하게, 밤엔 환하게
        int lit = Mathf.Min(windows.Count, (S.pop + PER_HOUSE - 1) / PER_HOUSE);
        if (lit != lastLit)
        {
            for (int i = 0; i < windows.Count; i++) windows[i].sharedMaterial = i < lit ? winOn : winOff;
            for (int h = 0; h < porch.Count; h++) porch[h].sharedMaterial = h < lit ? winOn : Mats.Solid(SHELL_D);
            lastLit = lit;
        }
        winOn.SetColor("_EmissionColor", WIN_ON * Mathf.Lerp(0.3f, 1.7f, night));
        var pc = poolMat.color; pc.a = night * 0.3f; poolMat.color = pc;
        for (int h = 0; h < pools.Count; h++) { bool on = h < lit && night > 0.05f; if (pools[h].enabled != on) pools[h].enabled = on; }
        // 패널: 낮(해가 높을수록 눕고 아침·저녁엔 세움, 남쪽 = 해 쪽), 밤엔 평평하게 눕힘
        float p = (S.clock % DayNight.DAY_LEN) / DayNight.DAY_LEN;              // 0.0~0.46 낮, 0.9~1 새벽
        float dayK = Mathf.Clamp01(1f - night * 1.4f);
        float noon = Mathf.Abs(Mathf.Repeat(p + 0.05f, 1f) - 0.25f) / 0.3f;     // 한낮(p≈0.2) 0 → 아침·저녁 1
        float target = dayK * Mathf.Lerp(16f, 34f, Mathf.Clamp01(noon));
        tilt = tiltInit ? Mathf.MoveTowards(tilt, target, dt * 6f) : target; tiltInit = true;   // 처음엔 바로 그 각도로
        foreach (var t in tilts) t.localRotation = Quaternion.Euler(-tilt, 0, 0);
        // 배터리 막대
        int nb = Mathf.CeilToInt(batt * 5f - 0.001f);
        if (nb != lastBars)
        {
            for (int i = 0; i < bars.Count; i++) bars[i].sharedMaterial = i < nb ? barOn : barOff;
            lastBars = nb;
        }
        bool flowing = SolarOpen && power > 1.001f;
        battLamp.sharedMaterial = flowing && Mathf.Repeat(animT, 1.2f) < 0.6f ? barOn : barOff;
        if (fan != null && flowing) fan.Rotate(0, 0, 300f * dt, Space.Self);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 3장 우주항(docs/chapter3-design.md): 착륙장 A·B·C · 화물선 주문 · 연료 공장 · 발사대(수출) · 관제탑.
//  · 화물선: 착륙장마다 상태기계(S.ships[i]) 0 빈 착륙장(t = 다음 도착까지) → 1 하강(역추진 불꽃·먼지 고리) → 2 내려앉아 주문 대기
//    → 3 정착민이 내려 길로 걸어감 → 4 이륙 → 0. 주문은 물·산소·식량 묶음([아이콘] 숫자 칩), 다 채우면 벨(병값 × 개수 × ORDER_MUL × 관제탑 보너스).
//    간격 SHIP_GAP × 관제탑 배율(Lv0 1 · Lv1 0.8 · Lv2 0.65 · Lv3 0.5). 주문은 기다려 주고 실패는 없다.
//  · 연료 공장: 얼음 → 연료통(지붕 호퍼·동쪽 받침대, 큐 FUEL_Q_CAP·출력 FUEL_CAP). 얼음은 길 바로 위 기지 얼음밭에서 손으로 줍는다
//    (처음엔 물통을 받았지만 3장 측정에서 물이 모자라 산소·주문이 멈춰서 얼음으로 바꿨다. 4장 정제소와 같은 얼음 → 연료 문법) 처리 시간 FUEL_T ÷(1+0.3×fuelspd) ÷ 기계 속도 배율.
//  · 발사대: 연료 ROCKET_CAP을 실으면 카운트다운(눈금 깜빡) → 발사(불꽃·연기·흔들림) → 벨(수출) → ROCKET_BUILD초 뒤 새 로켓이 솟아오름.
//  · 관제탑 Lv1~3: 화물선 간격·주문 보상(+10%/Lv), 레벨마다 부품이 솟아오름(Lv2 레이더·옆 탱크, Lv3 옆 모듈·금 테).
// 등짐 상한은 GameState.WantsMore가 Need(kind)를 더해 본다(주문 몫·연료 공장 큐·발사대 자리). 힌트(봇 포함)는 Hint가 정한다.
// 검증 인자: -shipnow(열린 착륙장에 바로 화물선) · -rocketfuel N(로켓에 연료 N) · -fuelfill 큐,출력
public class SpacePort : MonoBehaviour
{
    public static SpacePort I;
    GameState G => GameState.I;
    SaveData S => G.S;

    // ---- 배치(논리) ----
    public static readonly Vector2[] PADS = { new Vector2(740, 300), new Vector2(865, 300), new Vector2(990, 300) };
    static readonly string[] PAD_IDS = { "port", "pad2", "pad3" };   // 착륙장 A는 우주항을 열면 함께
    public static readonly Vector2 FUEL = new Vector2(735, 525), LAUNCH = new Vector2(880, 565), TOWER = new Vector2(1020, 545);
    static readonly Rect FUEL_R = new Rect(FUEL.x - 52, FUEL.y - 31, 122, 62), LAUNCH_R = new Rect(LAUNCH.x - 36, LAUNCH.y - 36, 98, 70), TOWER_R = new Rect(TOWER.x - 30, TOWER.y - 30, 60, 60);
    const float SHIP_R = 74f, FAC_R = 34f;

    // ---- 수치 ----
    public const float SHIP_GAP = 70f, FIRST_SHIP = 5f, DESC_T = 4.2f, UNLOAD_T = 2.6f, ASC_T = 3.6f, SHIP_H0 = 340f;
    public const float ORDER_MUL = 3.0f;
    public const int FUEL_CAP = 40, FUEL_Q_CAP = 60; public const float FUEL_T = 1.0f;
    public const int ROCKET_CAP = 30; public const float ROCKET_BUILD = 22f, ROCKET_MUL = 4.5f;
    static readonly float[] TOWER_GAP = { 1f, 0.8f, 0.65f, 0.5f };

    public bool PortOpen => G != null && G.PadLv("port") >= 1;
    public bool PadBuilt(int i) => G != null && G.PadLv(PAD_IDS[i]) >= 1;
    public bool FuelBuilt => G != null && G.PadLv("fuel") >= 1;
    public bool LaunchBuilt => G != null && G.PadLv("launch") >= 1;
    public int TowerLv => G == null ? 0 : G.PadLv("tower");
    public bool RocketReady => LaunchBuilt && S.rocketT <= 0f && !launching;
    public int RocketRoom => RocketReady ? Mathf.Max(0, ROCKET_CAP - S.rocketFuel - rocketPend) : 0;
    public float FuelTime => FUEL_T / (1f + 0.3f * G.PadLv("fuelspd")) / G.MachineSpeedMul;

    // ---- 화물선 보기 ----
    class ShipView
    {
        public Transform padRoot, ship, flame, shadow; public Light glow;
        public WorldLabels.Label[] chips = new WorldLabels.Label[3];
        public int[] pend = new int[3];
        public bool built;
    }
    readonly ShipView[] sv = { new ShipView(), new ShipView(), new ShipView() };
    static readonly string[] KINDS = { "bottle", "o2", "food" };

    // 시설 보기
    Transform fuelRoot, fuelHop, fuelTray, launchRoot, rocket, rocketFlame, towerRoot, towerLv2, towerLv3, radar, beacon;
    Renderer[] gauge = new Renderer[5]; Material gaugeOn, gaugeOff, gaugeBlink; Light rocketLight;
    WorldLabels.Label rocketChip;
    int fuelPend, rocketPend; float fuelVisT; int lastHop = -1, lastTray = -1;
    bool launching; float beaconT;
    readonly List<Transform> walkers = new List<Transform>();

    void Awake()
    {
        I = this;
        Regions.SetSolid("pad0", new Rect(PADS[0].x - 40, PADS[0].y - 40, 80, 80), () => PadBuilt(0), true);
        Regions.SetSolid("pad1", new Rect(PADS[1].x - 40, PADS[1].y - 40, 80, 80), () => PadBuilt(1), true);
        Regions.SetSolid("pad2", new Rect(PADS[2].x - 40, PADS[2].y - 40, 80, 80), () => PadBuilt(2), true);
        Regions.SetSolid("fuel", new Rect(FUEL.x - 50, FUEL.y - 30, 118, 60), () => FuelBuilt);
        Regions.SetSolid("launch", new Rect(LAUNCH.x - 34, LAUNCH.y - 34, 94, 66), () => LaunchBuilt);
        Regions.SetSolid("tower", new Rect(TOWER.x - 28, TOWER.y - 28, 56, 56), () => TowerLv >= 1);
    }

    void Start()
    {
        while (S.ships.Count < PADS.Length) S.ships.Add(new ShipSave { state = 0, t = FIRST_SHIP });
        // 저장 복원: 하강 중이면 내려앉은 것으로, 정착민·이륙 중이면 빈 착륙장으로
        foreach (var s in S.ships) { if (s.state == 1) s.state = 2; else if (s.state >= 3) { s.state = 0; s.t = 4f; } }
        var root = new GameObject("spacePort").transform; root.SetParent(transform, false);
        PortDress.Build(root, () => PortOpen);   // 꾸밈(유도등·연료 관·화물 마당·조명탑·안테나·가장자리 바위). 착륙장보다 먼저(유도등 재질)
        for (int i = 0; i < PADS.Length; i++) BuildPad(root, i);
        BuildFuel(root); BuildLaunch(root); BuildTower(root);
        G.OnPadLevel += OnPad;
        // 검증 인자(앱 실행당 1회)
        if (!devApplied)
        {
            devApplied = true;
            if (DevArgs.Has("-shipnow")) for (int i = 0; i < S.ships.Count; i++) if (S.ships[i].state == 0) S.ships[i].t = 0.5f + i * 1.2f;
            if (int.TryParse(DevArgs.Get("-rocketfuel"), out var rf)) { S.rocketFuel = Mathf.Clamp(rf, 0, ROCKET_CAP); S.rocketT = 0; }
            var ff = DevArgs.Get("-fuelfill");
            if (!string.IsNullOrEmpty(ff)) { var a = ff.Split(','); if (a.Length > 0 && int.TryParse(a[0], out var q)) S.fuelQ = q; if (a.Length > 1 && int.TryParse(a[1], out var o)) S.fuelOut = o; }
        }
    }
    static bool devApplied;
    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPad; }

    // ───────────────────────── 조립 ─────────────────────────
    void BuildPad(Transform root, int i)
    {
        var v = sv[i];
        v.padRoot = new GameObject("landingPad" + i).transform; v.padRoot.SetParent(root, false); v.padRoot.position = WorldMap.W2U(PADS[i]);
        RegionDress.Put(v.padRoot, "pad", RegionDress.LandingPad(), 0.2f);
        PortDress.PadDress(v.padRoot, i);
        v.ship = new GameObject("ship").transform; v.ship.SetParent(v.padRoot, false);
        RegionDress.Put(v.ship, "body", RegionDress.Ship(), 0.25f);
        var bk = Prim.Sphere(v.ship, "beacon", new Vector3(0, 46f, 0) * GameConfig.K, 4, GameConfig.Hex("ff5a3c")); Prim.NoShadow(bk);
        v.flame = new GameObject("flame").transform; v.flame.SetParent(v.ship, false); v.flame.localPosition = new Vector3(0, RegionDress.SHIP_ENGINE_H * GameConfig.K, 0);
        var fl = RegionDress.Put(v.flame, "cone", RegionDress.ShipFlames(), 0f, 1f); Prim.NoShadow(fl);
        v.glow = v.flame.gameObject.AddComponent<Light>(); v.glow.type = LightType.Point; v.glow.color = GameConfig.Hex("ffb35a"); v.glow.range = 120 * GameConfig.K; v.glow.intensity = 0; v.glow.shadows = LightShadows.None;
        v.shadow = Prim.BlobShadow(v.padRoot, 56).transform; v.shadow.localPosition = new Vector3(0, (RegionDress.PAD_TOP + 0.3f) * GameConfig.K, 0);
        for (int k = 0; k < 3; k++)
        {
            int kk = k; var vv = v;
            v.chips[k] = WorldLabels.I.Add(() => vv.ship.position + Vector3.up * 2.9f, "", 17, GameConfig.NAVY, new Vector2((k - 1) * 54f, 0), false);
            v.chips[k].SetIcon(KINDS[kk]);
            v.chips[k].Show(false);
        }
        v.built = PadBuilt(i);
        v.padRoot.gameObject.SetActive(v.built);
        SyncShip(i, true);
    }

    void BuildFuel(Transform root)
    {
        fuelRoot = new GameObject("fuelPlant").transform; fuelRoot.SetParent(root, false); fuelRoot.position = WorldMap.W2U(FUEL);
        RegionDress.Put(fuelRoot, "plant", RegionDress.FuelPlant(), 0.15f);
        float k = GameConfig.K;
        fuelHop = new GameObject("hop").transform; fuelHop.SetParent(fuelRoot, false); fuelHop.localPosition = new Vector3(RegionDress.FUEL_HOP.x, RegionDress.FUEL_TOP + 1.2f, -RegionDress.FUEL_HOP.y) * k;
        fuelTray = new GameObject("tray").transform; fuelTray.SetParent(fuelRoot, false); fuelTray.localPosition = new Vector3(RegionDress.FUEL_TRAY.x, 4.2f, -RegionDress.FUEL_TRAY.y) * k;
        fuelRoot.gameObject.SetActive(FuelBuilt);
    }

    void BuildLaunch(Transform root)
    {
        launchRoot = new GameObject("launchPad").transform; launchRoot.SetParent(root, false); launchRoot.position = WorldMap.W2U(LAUNCH);
        RegionDress.Put(launchRoot, "pad", RegionDress.LaunchPad(), 0.18f);
        rocket = new GameObject("rocket").transform; rocket.SetParent(launchRoot, false); rocket.localPosition = new Vector3(0, RegionDress.LAUNCH_TOP * GameConfig.K, 0);
        RegionDress.Put(rocket, "body", RegionDress.Rocket(), 0.3f);
        rocketFlame = new GameObject("flame").transform; rocketFlame.SetParent(rocket, false);
        var fl = RegionDress.Put(rocketFlame, "cone", RegionDress.Flame(), 0f, 1f); Prim.NoShadow(fl);
        rocketFlame.localScale = new Vector3(1.1f, 1.6f, 1.1f); rocketFlame.gameObject.SetActive(false);
        rocketLight = rocketFlame.gameObject.AddComponent<Light>(); rocketLight.type = LightType.Point; rocketLight.color = GameConfig.Hex("ffb35a"); rocketLight.range = 160 * GameConfig.K; rocketLight.intensity = 0; rocketLight.shadows = LightShadows.None;
        // 연료 눈금 5칸(발사탑 앞면)
        var emBase = Resources.Load<Material>("Mats/Emissive");
        gaugeOn = emBase != null ? new Material(emBase) : Mats.Solid(Color.white); gaugeOn.color = GameConfig.Hex("7df0a8"); if (emBase != null) gaugeOn.SetColor("_EmissionColor", GameConfig.Hex("3fc77a") * 1.2f);
        gaugeBlink = emBase != null ? new Material(emBase) : Mats.Solid(Color.white); gaugeBlink.color = GameConfig.Hex("ffe08a"); if (emBase != null) gaugeBlink.SetColor("_EmissionColor", GameConfig.Hex("f2c14e") * 1.3f);
        gaugeOff = Mats.Solid(GameConfig.Hex("2f4a3e"));
        for (int i = 0; i < 5; i++)
        {
            var g = ToyMesh.Put(launchRoot, "gauge" + i, RegionDress.GaugeCell(), gaugeOff, RegionDress.GaugeCellLocal(i));
            gauge[i] = g.GetComponent<Renderer>(); Prim.NoShadow(g);
        }
        rocketChip = WorldLabels.I.Add(() => launchRoot.position + Vector3.up * 5.3f, "", 20, GameConfig.NAVY, Vector2.zero, false);
        rocketChip.SetIcon("fuel"); rocketChip.Show(false);
        launchRoot.gameObject.SetActive(LaunchBuilt);
        rocket.gameObject.SetActive(S.rocketT <= 0f);
    }

    void BuildTower(Transform root)
    {
        towerRoot = new GameObject("tower").transform; towerRoot.SetParent(root, false); towerRoot.position = WorldMap.W2U(TOWER);
        RegionDress.Put(towerRoot, "base", RegionDress.TowerBase(), 0.18f);
        towerLv2 = new GameObject("lv2").transform; towerLv2.SetParent(towerRoot, false); RegionDress.Put(towerLv2, "m", RegionDress.TowerLv2(), 0.18f);
        towerLv3 = new GameObject("lv3").transform; towerLv3.SetParent(towerRoot, false); RegionDress.Put(towerLv3, "m", RegionDress.TowerLv3(), 0.18f);
        radar = new GameObject("radar").transform; radar.SetParent(towerRoot, false); radar.localPosition = new Vector3(0, RegionDress.TOWER_TOP * GameConfig.K, 0);
        RegionDress.Put(radar, "dish", RegionDress.Radar(), 0.3f);
        beacon = Prim.Sphere(towerRoot, "beacon", new Vector3(14, RegionDress.TOWER_TOP - 6f, 0) * GameConfig.K, 4.5f, GameConfig.Hex("ff5a3c")).transform; Prim.NoShadow(beacon.gameObject);
        SyncTower(false);
    }

    void SyncTower(bool rise)
    {
        int lv = TowerLv;
        bool b = lv >= 1, l2 = lv >= 2, l3 = lv >= 3;
        if (rise && b && !towerRoot.gameObject.activeSelf) { towerRoot.gameObject.SetActive(true); JuiceFX.I.Rise(towerRoot, 0f, 40f, 0.8f); }
        towerRoot.gameObject.SetActive(b);
        if (rise && l2 && !towerLv2.gameObject.activeSelf) { towerLv2.gameObject.SetActive(true); JuiceFX.I.Rise(towerLv2, 0.1f, 20f); JuiceFX.I.Rise(radar, 0.25f, 10f); }
        if (rise && l3 && !towerLv3.gameObject.activeSelf) { towerLv3.gameObject.SetActive(true); JuiceFX.I.Rise(towerLv3, 0.1f, 20f); }
        towerLv2.gameObject.SetActive(l2); radar.gameObject.SetActive(l2); towerLv3.gameObject.SetActive(l3);
    }

    void OnPad(string id, int lv)
    {
        if (lv < 1) return;
        PortDress.OnPad(id);
        for (int i = 0; i < PADS.Length; i++)
            if (id == PAD_IDS[i] && !sv[i].built)
            {
                sv[i].built = true; sv[i].padRoot.gameObject.SetActive(true);
                JuiceFX.I.Rise(sv[i].padRoot, 0.35f, 14f, 0.7f);
                S.ships[i].state = 0; S.ships[i].t = FIRST_SHIP + i; SyncShip(i, true);
                var pl = Player.I; if (pl != null && (pl.pos - PADS[i]).sqrMagnitude < 44f * 44f) pl.pos = ShipPt(i);   // 착륙장 발밑이면 길 쪽으로
            }
        if (id == "fuel") { fuelRoot.gameObject.SetActive(true); JuiceFX.I.Rise(fuelRoot, 0.1f, 30f, 0.8f); Unstick(FUEL_R); }
        if (id == "launch") { launchRoot.gameObject.SetActive(true); JuiceFX.I.Rise(launchRoot, 0.1f, 30f, 0.8f); S.rocketT = 0; rocket.gameObject.SetActive(true); Unstick(LAUNCH_R); }
        if (id == "tower") { SyncTower(true); Unstick(TOWER_R); }
        if (id == "fuel" || id == "launch" || id == "tower" || System.Array.IndexOf(PAD_IDS, id) >= 0) WorldMap.fenceVer++;   // 봇 격자: 시설 발자국이 바뀔 때만
    }
    // 시설이 플레이어 발밑에 섰으면 길 쪽(북쪽)으로 비켜 세운다
    static void Unstick(Rect r)
    {
        var pl = Player.I; if (pl == null) return;
        if (WorldMap.Blocked(pl.pos.x, pl.pos.y)) pl.pos = new Vector2(Mathf.Clamp(pl.pos.x, r.xMin + 10, r.xMax - 10), Regions.ROAD_Y);
    }

    // ───────────────────────── 매 프레임 ─────────────────────────
    void Update()
    {
        if (G == null) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        for (int i = 0; i < PADS.Length; i++) TickShip(i, dt);
        TickFuel(dt);
        TickRocket(dt);
        TickTower(dt);
        TickWalkers(dt);
        PortDress.Tick(dt, i => sv[i].built ? S.ships[i].state : -1);
    }

    // ---- 화물선 ----
    void TickShip(int i, float dt)
    {
        var v = sv[i]; if (!v.built) return;
        var s = S.ships[i];
        switch (s.state)
        {
            case 0:
                s.t -= dt * (MarsEvents.I != null && MarsEvents.MoveMul < 1f ? 0.5f : 1f);   // 먼지 폭풍 중엔 늦게
                if (s.t <= 0f) { NewOrder(i); s.state = 1; s.t = 0f; v.ship.gameObject.SetActive(true); AudioManager.I?.Play("whoosh", 0.6f, 0.5f); }
                break;
            case 1:
                s.t += dt;
                if (s.t >= DESC_T)
                {
                    s.state = 2; s.t = 0f;
                    var at = WorldMap.W2U(PADS[i], RegionDress.PAD_TOP);
                    JuiceFX.I.Dust(at, 10); JuiceFX.I.Ring(at, Color.white, 46);
                    if (NearCam(PADS[i])) { JuiceFX.I.Shake(2); AudioManager.I?.Play("pop", 0.6f, 0.6f); }
                }
                break;
            case 2:
                if (OrderDone(s)) { CompleteOrder(i); s.state = 3; s.t = 0f; }
                break;
            case 3:
                s.t += dt; if (s.t >= UNLOAD_T) { s.state = 4; s.t = 0f; AudioManager.I?.Play("whoosh", 0.7f, 0.5f); }
                break;
            case 4:
                s.t += dt; if (s.t >= ASC_T) { s.state = 0; s.t = SHIP_GAP * TOWER_GAP[Mathf.Clamp(TowerLv, 0, 3)] * Random.Range(0.85f, 1.15f); }
                break;
        }
        SyncShip(i, false);
    }

    static bool NearCam(Vector2 p) => CameraRig.I != null && Mathf.Abs(CameraRig.I.camX - p.x) < 260f && Mathf.Abs(CameraRig.I.camY - p.y) < 500f;

    // 우주선 높이·불꽃·그림자·주문 칩
    void SyncShip(int i, bool snap)
    {
        var v = sv[i]; if (!v.built) return;
        var s = S.ships[i]; float k = GameConfig.K;
        float h = 0f, flame = 0f;
        switch (s.state)
        {
            case 0: v.ship.gameObject.SetActive(false); break;
            case 1: { float e = Mathf.Clamp01(s.t / DESC_T); float u = 1f - e; h = SHIP_H0 * u * u * u; flame = 0.35f + 0.65f * Mathf.Clamp01(1.2f - e * 0.9f); if (e > 0.96f) flame *= (1f - e) / 0.04f; break; }
            case 2: case 3: h = 0f; break;
            case 4: { float e = Mathf.Clamp01(s.t / ASC_T); h = SHIP_H0 * 1.3f * e * e * e; flame = e < 0.08f ? e / 0.08f : 1f; break; }
        }
        if (s.state != 0) v.ship.gameObject.SetActive(true);
        v.ship.localPosition = new Vector3(0, (RegionDress.PAD_TOP + h) * k, 0);
        bool fon = flame > 0.01f;
        if (v.flame.gameObject.activeSelf != fon) v.flame.gameObject.SetActive(fon);
        if (fon)
        {
            float f = 1f + Mathf.Sin(Time.time * 47f + i) * 0.12f + Mathf.Sin(Time.time * 31f) * 0.08f;
            v.flame.localScale = new Vector3(0.85f + 0.15f * flame, (0.45f + 0.75f * flame) * f, 0.85f + 0.15f * flame);
            float night = DayNight.I != null ? DayNight.I.night : 0f;
            v.glow.intensity = flame * (0.6f + 1.4f * night);
        }
        // 그림자: 높을수록 작고 옅게
        float sc = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(h / SHIP_H0));
        v.shadow.gameObject.SetActive(s.state != 0);
        v.shadow.localScale = new Vector3(56 * sc, 0.2f, 56 * sc) * k;
        // 주문 칩: 내려앉아 주문 대기 중에만, 남은 종류만
        int nShow = 0; for (int c = 0; c < 3; c++) if (s.state == 2 && Need(s, c) - Got(s, c) > 0) nShow++;
        for (int c = 0, j = 0; c < 3; c++)
        {
            int need = Need(s, c) - Got(s, c);
            bool show = s.state == 2 && need > 0;
            v.chips[c].Show(show);
            if (show) { v.chips[c].Set(need.ToString()); v.chips[c].offset = new Vector2((j - (nShow - 1) / 2f) * 54f, 0); j++; }
        }
        // 착륙 표시등: 대기 중엔 천천히 깜빡
    }

    static int Need(ShipSave s, int c) => c == 0 ? s.needW : c == 1 ? s.needO : s.needF;
    static int Got(ShipSave s, int c) => c == 0 ? s.gotW : c == 1 ? s.gotO : s.gotF;
    static void AddGot(ShipSave s, int c) { if (c == 0) s.gotW++; else if (c == 1) s.gotO++; else s.gotF++; }
    static bool OrderDone(ShipSave s) => s.gotW >= s.needW && s.gotO >= s.needO && s.gotF >= s.needF;

    void NewOrder(int i)
    {
        var s = S.ships[i];
        s.seed = S.orders * 31 + i * 7 + S.day;
        var rnd = new System.Random(s.seed);
        int t = Mathf.Min(8, S.orders / 2);
        s.needW = 6 + 2 * t + rnd.Next(0, 4);
        s.needO = G.KindOpen("o2") ? 3 + t + rnd.Next(0, 3) : 0;
        s.needF = G.KindOpen("food") ? 2 + t / 2 + rnd.Next(0, 3) : 0;
        int kind = rnd.Next(5);   // 가끔 한 종류를 빼서 주문 모양을 바꾼다(물은 늘 있다)
        if (kind == 0) s.needO = 0; else if (kind == 1) s.needF = 0;
        s.gotW = s.gotO = s.gotF = 0;
        float v = s.needW * G.PriceOf("bottle") + s.needO * G.PriceOf("o2") + s.needF * G.PriceOf("food");
        s.reward = Mathf.Max(50, Mathf.RoundToInt(v * ORDER_MUL * (1f + 0.1f * TowerLv) / 50f) * 50);
        Debug.Log("port: 화물선 " + i + " 주문 물" + s.needW + " 산소" + s.needO + " 식량" + s.needF + " → " + s.reward);
    }

    void CompleteOrder(int i)
    {
        var s = S.ships[i]; var at = sv[i].ship.position + Vector3.up * 1.6f;
        S.orders++;
        G.AddBells(s.reward, "order");
        JuiceFX.I.CoinPop(at, 12); JuiceFX.I.Ring(WorldMap.W2U(PADS[i], RegionDress.PAD_TOP), GameConfig.GOLD, 40);
        if (NearCam(PADS[i])) { WorldLabels.I.Float(at + Vector3.up * 0.6f, "+" + s.reward.ToString("N0"), true, true); JuiceFX.I.Shake(3); }
        AudioManager.I?.Play("up", 1.15f); AudioManager.I?.Play("coin", 1f, 0.6f); JuiceFX.I.Vibrate(25);
        int n = 3 + (s.seed % 3);
        S.arrivals += n;
        for (int k = 0; k < n; k++) StartCoroutine(CoWalker(PADS[i], k, n));
        MissionBoard.I?.Count("order", 1);
        Debug.Log("port: 주문 완료 " + i + " +" + s.reward + " (누계 " + S.orders + ", 정착민 +" + n + " → " + S.arrivals + ")");
        G.Save();
    }

    // ---- 정착민 무리: 우주선에서 내려 길까지 → 서쪽으로 걸어가다 관문 앞에서 사라진다 ----
    static readonly Color[] PAL = { GameConfig.Hex("e48a55"), GameConfig.Hex("e6b552"), GameConfig.Hex("82b0cf"), GameConfig.Hex("a4b384") };
    IEnumerator CoWalker(Vector2 pad, int k, int n)
    {
        yield return new WaitForSeconds(0.25f * k);
        var view = new GameObject("arrival").transform; view.SetParent(transform, false);
        var body = new GameObject("body").transform; body.SetParent(view, false);
        CharacterKit.BuildCustomer(body, PAL[(k + n) % PAL.Length], (k * 3 + n) % 6, false, out _);
        Prim.BlobShadow(view, 20);
        walkers.Add(view);
        var rig = body.GetComponent<CharRig>();
        var pts = new List<Vector2> { pad + new Vector2((k - n / 2f) * 9f, 34f), new Vector2(pad.x + (k - n / 2f) * 9f, Regions.ROAD_Y + (k % 2 == 0 ? -8f : 8f)), new Vector2(Regions.ALL[0].gateX + 30f, Regions.ROAD_Y + (k % 2 == 0 ? -8f : 8f)) };
        Vector2 p = pad + new Vector2(0, 20f); view.position = WorldMap.W2U(p);
        JuiceFX.I.Pop(view, 0.2f, 0.3f);
        float speed = 72f + k * 4f;
        foreach (var q in pts)
        {
            while ((q - p).sqrMagnitude > 4f)
            {
                float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
                var d = (q - p).normalized; p = Vector2.MoveTowards(p, q, speed * dt);
                view.position = WorldMap.W2U(p);
                view.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, -d.y), Vector3.up);
                float s = Mathf.Sin(Time.time * 11f + k);
                body.localPosition = new Vector3(0, Mathf.Abs(s) * 2.5f * GameConfig.K, 0);
                CharacterKit.Swing(rig, s, 40f, dt);
                yield return null;
            }
        }
        // 관문 앞에서 작아지며 사라짐(기지로 들어감)
        float el = 0; var sc0 = view.localScale;
        while (el < 0.35f) { el += Time.deltaTime; view.localScale = sc0 * (1f - el / 0.35f); yield return null; }
        walkers.Remove(view); Destroy(view.gameObject);
    }
    void TickWalkers(float dt) { }

    // ---- 연료 공장 ----
    void TickFuel(float dt)
    {
        if (!FuelBuilt) return;
        if (S.fuelQ > 0 && S.fuelOut < FUEL_CAP)
        {
            S.fuelT += dt;
            if (S.fuelT >= FuelTime) { S.fuelT = 0; S.fuelQ--; S.fuelOut++; S.fuelMade++; MissionBoard.I?.Count("fuel", 1); }
        }
        fuelVisT += dt; if (fuelVisT < 0.2f) return; fuelVisT = 0;
        int hop = Mathf.Min(S.fuelQ, 12), tray = Mathf.Min(S.fuelOut, 24);
        if (hop != lastHop) { lastHop = hop; Items.SyncStack(fuelHop, "ice", hop, 3, 2, 10.5f, 8.5f, 12); }
        if (tray != lastTray) { lastTray = tray; Items.SyncStack(fuelTray, "fuel", tray, 3, 2, 7.8f, 7.2f, 24); }
    }

    // ---- 발사대 ----
    void TickRocket(float dt)
    {
        if (!LaunchBuilt) { rocketChip.Show(false); return; }
        if (S.rocketT > 0f)
        {
            S.rocketT -= dt;
            if (S.rocketT <= 0f) { S.rocketT = 0; S.rocketFuel = 0; rocket.localPosition = new Vector3(0, RegionDress.LAUNCH_TOP * GameConfig.K, 0); rocket.gameObject.SetActive(true); JuiceFX.I.Rise(rocket, 0f, 40f, 0.9f); }
        }
        if (RocketReady && S.rocketFuel >= ROCKET_CAP && rocketPend == 0) StartCoroutine(CoLaunch());
        // 눈금: 실은 연료 5칸
        int lit = S.rocketT > 0f ? 0 : Mathf.CeilToInt(S.rocketFuel / (float)ROCKET_CAP * 5f - 0.001f);
        bool blink = launching && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f;
        for (int i = 0; i < 5; i++) gauge[i].sharedMaterial = launching ? (blink ? gaugeBlink : gaugeOff) : i < lit ? gaugeOn : gaugeOff;
        int room = ROCKET_CAP - S.rocketFuel;
        bool chip = RocketReady && room > 0 && S.rocketFuel + rocketPend > 0 || RocketReady && room > 0 && Player.I != null && Vector2.Distance(Player.I.pos, LAUNCH) < 160f;
        rocketChip.Show(chip);
        if (chip) rocketChip.Set(room.ToString());
    }

    IEnumerator CoLaunch()
    {
        launching = true;
        AudioManager.I?.Play("tick", 1.2f, 0.6f);
        // 카운트다운: 눈금 깜빡 + 발치 김
        float el = 0f;
        while (el < 2.4f) { el += Time.deltaTime; if (Mathf.Repeat(el, 0.6f) < Time.deltaTime) { AudioManager.I?.Play("tick", 1.5f, 0.5f); JuiceFX.I.Dust(rocket.position + Vector3.up * 0.2f, 2); } yield return null; }
        // 점화 → 상승
        rocketFlame.gameObject.SetActive(true);
        var basePos = rocket.position;
        bool seen = NearCam(LAUNCH);
        JuiceFX.I.Dust(basePos, 14); JuiceFX.I.Ring(basePos, Color.white, 60); JuiceFX.I.Ring(basePos, GameConfig.Hex("ffc94a"), 34);
        if (seen) { JuiceFX.I.Shake(4); JuiceFX.I.Vibrate(40); }
        AudioManager.I?.Play("whoosh", 0.55f, 0.9f); AudioManager.I?.Play("up", 0.9f, 0.6f);
        float h = 0f, v = 0f, puffT = 0f; el = 0f;
        while (el < 4.2f)
        {
            float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX); el += dt;
            v += (el < 0.6f ? 90f : 330f) * dt; h += v * dt;
            rocket.localPosition = new Vector3(0, (RegionDress.LAUNCH_TOP + h) * GameConfig.K, 0);
            float f = 1f + Mathf.Sin(Time.time * 53f) * 0.15f;
            rocketFlame.localScale = new Vector3(1.1f, 1.6f * f + Mathf.Min(1.2f, v / 300f), 1.1f);
            float night = DayNight.I != null ? DayNight.I.night : 0f;
            rocketLight.intensity = 1.2f + 2.2f * night;
            puffT += dt; if (puffT > 0.07f && h < 260f) { puffT = 0; JuiceFX.I.Dust(rocket.position + Vector3.up * 0.1f, 1); }
            yield return null;
        }
        rocketFlame.gameObject.SetActive(false); rocketLight.intensity = 0;
        rocket.gameObject.SetActive(false);
        // 수출 보상
        int reward = Mathf.RoundToInt(ROCKET_CAP * G.PriceOf("bottle") * ROCKET_MUL / 50f) * 50;
        S.launches++; S.rocketFuel = 0; S.rocketT = ROCKET_BUILD;
        G.AddBells(reward, "export");
        if (seen) { WorldLabels.I.Float(basePos + Vector3.up * 2.4f, "+" + reward.ToString("N0"), true, true); JuiceFX.I.CoinPop(basePos + Vector3.up * 1f, 14); }
        AudioManager.I?.Play("coin", 1f, 0.6f);
        MissionBoard.I?.Count("launch", 1);
        Debug.Log("port: 수출 발사 +" + reward + " (누계 " + S.launches + ")");
        G.Save();
        launching = false;
    }

    // ---- 관제탑: 레이더 회전·표시등 깜빡 ----
    void TickTower(float dt)
    {
        if (TowerLv < 1) return;
        if (TowerLv >= 2) radar.Rotate(0, 40f * dt, 0, Space.Self);
        beaconT += dt; bool on = Mathf.Repeat(beaconT, 1.6f) < 0.25f;
        if (beacon.gameObject.activeSelf != on) beacon.gameObject.SetActive(on);
    }

    // ───────────────────────── 플레이어 ─────────────────────────
    static bool NearRect(Rect r, Vector2 p, float rad)
    {
        float dx = p.x < r.xMin ? r.xMin - p.x : p.x > r.xMax ? p.x - r.xMax : 0f;
        float dy = p.y < r.yMin ? r.yMin - p.y : p.y > r.yMax ? p.y - r.yMax : 0f;
        return dx * dx + dy * dy < rad * rad;
    }

    public void Interact(Player pl)
    {
        if (!PortOpen) return;
        var p = pl.pos;
        // 1. 내려앉은 화물선에 주문 물건 싣기
        for (int i = 0; i < PADS.Length; i++)
        {
            if (!sv[i].built) continue;
            var s = S.ships[i]; if (s.state != 2) continue;
            if ((p - PADS[i]).sqrMagnitude > SHIP_R * SHIP_R) continue;
            for (int c = 0; c < 3; c++)
            {
                string kind = KINDS[c];
                if (Got(s, c) + sv[i].pend[c] >= Need(s, c) || !G.Holds(kind)) continue;
                int ii = i, cc = c; sv[i].pend[c]++;
                var from = pl.TakeOut(kind);
                ArcFlyer.I.Fly(pl.FlyBlock(kind), from, () => sv[ii].ship.position + Vector3.up * 1.4f, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { sv[ii].pend[cc]--; AddGot(S.ships[ii], cc); });
                AudioManager.I.Play("put", 1.1f + Random.Range(-0.05f, 0.05f));
                pl.actT = 0.07f; return;
            }
        }
        // 2. 연료 공장: 물통 넣기 → 연료통 받기
        if (FuelBuilt && NearRect(FUEL_R, p, FAC_R))
        {
            if (G.Holds("ice") && S.fuelQ + fuelPend < FUEL_Q_CAP)
            {
                fuelPend++; var from = pl.TakeOut("ice");
                ArcFlyer.I.Fly(pl.FlyBlock("ice"), from, () => fuelHop.position + Vector3.up * 0.4f, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { fuelPend--; S.fuelQ++; });
                AudioManager.I.Play("put", 1f + Random.Range(-0.05f, 0.05f));
                pl.actT = 0.07f; return;
            }
            if (S.fuelOut > 0 && G.WantsMore("fuel"))
            {
                S.fuelOut--; G.PendAdd("fuel");
                var from = fuelTray.position + Vector3.up * 0.3f;
                ArcFlyer.I.Fly(pl.FlyBlock("fuel"), from, () => pl.InsertWorld("fuel"), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => pl.Arrive("fuel"));
                pl.actT = 0.09f; return;
            }
        }
        // 3. 로켓에 연료 싣기
        if (RocketReady && G.Holds("fuel") && S.rocketFuel + rocketPend < ROCKET_CAP && NearRect(LAUNCH_R, p, FAC_R + 6f))
        {
            rocketPend++; var from = pl.TakeOut("fuel");
            ArcFlyer.I.Fly(pl.FlyBlock("fuel"), from, () => rocket.position + Vector3.up * 2.4f, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { rocketPend--; S.rocketFuel = Mathf.Min(ROCKET_CAP, S.rocketFuel + 1); });
            AudioManager.I.Play("put", 1.2f + Random.Range(-0.05f, 0.05f));
            pl.actT = 0.07f; return;
        }
    }

    // 내려앉은 화물선이 아직 그 종류를 원하나(든 것·날아가는 것 빼고)
    bool ShipWants(string kind)
    {
        int c = System.Array.IndexOf(KINDS, kind); if (c < 0) return false;
        for (int i = 0; i < PADS.Length; i++) { var s = S.ships[i]; if (sv[i].built && s.state == 2 && Need(s, c) - Got(s, c) - sv[i].pend[c] > 0) return true; }
        return false;
    }
    int ShipRemain(string kind)
    {
        int c = System.Array.IndexOf(KINDS, kind); if (c < 0) return 0; int n = 0;
        for (int i = 0; i < PADS.Length; i++) { var s = S.ships[i]; if (sv[i].built && s.state == 2) n += Mathf.Max(0, Need(s, c) - Got(s, c) - sv[i].pend[c]); }
        return n;
    }

    /// 등짐 상한에 더할 몫(GameState.WantsMore): 주문 남은 양, 얼음은 연료 공장 큐 빈자리, 연료통은 로켓 자리 + 한 대 분량
    public static int Need(string kind)
    {
        var sp = I; if (sp == null || GameState.I == null || !sp.PortOpen) return 0;
        var S = GameState.I.S; int n = sp.ShipRemain(kind);
        if (kind == "ice" && sp.FuelBuilt) n += Mathf.Max(0, FUEL_Q_CAP - S.fuelQ - sp.fuelPend);
        if (kind == "fuel" && sp.LaunchBuilt) n += sp.RocketRoom + ROCKET_CAP;
        return n;
    }

    // ───────────────────────── 힌트(봇 포함) ─────────────────────────
    static Vector2 StnPt(string k) { var s = WorldMap.STN[k]; return new Vector2(s.x, s.y + s.h / 2 + 18); }
    public static Vector2 ShipPt(int i) => new Vector2(PADS[i].x, PADS[i].y + 62f);
    public static readonly Vector2 FUEL_PT = new Vector2(FUEL.x + 34f, FUEL.y - 52f), ROCKET_PT = new Vector2(LAUNCH.x - 14f, LAUNCH.y - 56f);

    // 기지 얼음밭에서 지금 주울 수 있는 가장 가까운 얼음 더미(없으면 -1)
    static int NearestIce(Vector2 p)
    {
        var st = Stations.I; if (st == null) return -1;
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < st.SpotCount; i++) if (st.SpotOpen(i)) { float d = (p - WorldMap.ICE_SPOTS[i]).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        return best;
    }

    /// 3·4장 일 안내. 있으면 true(at 목표 점, key 힌트 키, deliver 지금 든 것을 넣으러 가는 중)
    public static bool Hint(Vector2 pos, out Vector2 at, out string key, out bool deliver)
    {
        at = Vector2.zero; key = ""; deliver = false;
        var sp = I; var G = GameState.I; if (sp == null || G == null || !sp.PortOpen) return false;
        var S = G.S; var ic = IceCanyon.I;
        bool full = G.Carry + G.pend >= G.CarryMax;
        // 물이 바닥나 화물선 물·산소를 못 채우면 얼음은 해빙기로(기지 힌트가 안내), 아니면 연료 공장으로
        bool thirsty = (sp.ShipRemain("bottle") > 0 || sp.ShipRemain("o2") > 0 && S.elecOut == 0) && S.bottles < 3 && S.water < 30 && S.meltQ < 12;
        // ---- 넣기 ----
        if (G.Holds("fuel") && sp.RocketRoom > 0) { at = ROCKET_PT; key = "rocket"; deliver = true; return true; }
        int bestShip = -1; float bd = float.MaxValue;
        for (int i = 0; i < PADS.Length; i++)
        {
            var s = S.ships[i]; if (!sp.sv[i].built || s.state != 2) continue;
            bool want = false;
            for (int c = 0; c < 3; c++) if (Need(s, c) - Got(s, c) - sp.sv[i].pend[c] > 0 && G.Holds(KINDS[c])) want = true;
            if (!want) continue;
            float d = (pos - PADS[i]).sqrMagnitude; if (d < bd) { bd = d; bestShip = i; }
        }
        if (bestShip >= 0) { at = ShipPt(bestShip); key = "ship:" + bestShip; deliver = true; return true; }
        if (ic != null && ic.Open && G.Holds("ice") && ic.RefRoom > 0 && (pos.x > IceCanyon.ZONE_X0 - 60f || G.Held("ice") >= 20)) { at = IceCanyon.REF_PT; key = "refine"; deliver = true; return true; }
        if (sp.FuelBuilt && !thirsty && G.Holds("ice") && S.fuelQ + sp.fuelPend < FUEL_Q_CAP && pos.x < IceCanyon.ZONE_X0 - 120f && (G.Held("ice") >= 20 || S.fuelQ < 6 || NearRect(FUEL_R, pos, FAC_R + 4f) || NearestIce(pos) < 0))
        { at = FUEL_PT; key = "fuel"; deliver = true; return true; }
        if (full) return false;
        // ---- 받기 ----
        int room = sp.RocketRoom - G.HeldAll("fuel");
        if (room > 0)
        {
            int need = Mathf.Min(room, 8);
            bool fromPlant = sp.FuelBuilt && S.fuelOut >= need, fromRef = ic != null && ic.Open && S.refOut >= need;
            if (fromPlant || fromRef)
            {
                float dp = fromPlant ? (pos - FUEL_PT).sqrMagnitude : float.MaxValue, dr = fromRef ? (pos - IceCanyon.REF_PT).sqrMagnitude : float.MaxValue;
                if (dp <= dr) { at = FUEL_PT; key = "fuelTake"; } else { at = IceCanyon.REF_PT; key = "refTake"; }
                return true;
            }
        }
        // 발사대가 있는데 연료(공장 큐·출력·든 것)가 한 대 분량도 안 되면 주문보다 얼음 줍기가 먼저(수출이 주문에 밀려 멈추지 않게)
        //  주문과 번갈아: 발사 수가 주문 수의 1/3을 넘으면 주문 쪽을 먼저(측정: 연료만 하면 주문이 멈췄다)
        if (sp.LaunchBuilt && !thirsty && sp.FuelBuilt && S.launches <= S.orders / 3 && S.fuelQ + sp.fuelPend + S.fuelOut + G.HeldAll("ice") + G.HeldAll("fuel") < ROCKET_CAP && G.WantsMore("ice") && (ic == null || !ic.Open || S.refOut + S.refQ < ROCKET_CAP))
        {
            int b = NearestIce(pos);
            if (b >= 0) { at = WorldMap.ICE_SPOTS[b]; key = "ice:" + b; return true; }
        }
        // 얼음 협곡: 로버가 오기 전엔 정제소가 비어 가면 광맥 얼음 줍기가 주문보다 먼저(협곡 목표가 주문에 밀리지 않게)
        if (!thirsty && ic != null && ic.Open && ic.Rovers == 0 && ic.RefRoom > 10 && S.refOut < 30 && S.refQ + G.HeldAll("ice") < 30)
        {
            int v = ic.NearestVein(pos);
            if (v >= 0) { at = IceCanyon.VEIN_SPOTS[v]; key = "vein:" + v; return true; }
        }
        // 화물선 주문 재료 가지러: 재고가 있는 종류 중 남은 게 많은 것
        var ss = SettleStations.I;
        string pick = null; int most = 0;
        foreach (var k in KINDS)
        {
            int rem = sp.ShipRemain(k) - G.HeldAll(k); if (rem <= 0) continue;
            bool stock = k == "bottle" ? S.bottles >= 1 : k == "o2" ? ss != null && ss.Open(0) && S.elecOut >= 1 : ss != null && ss.Open(3) && S.grnOut >= 1;
            if (stock && rem > most) { most = rem; pick = k; }
        }
        if (pick != null)
        {
            if (pick == "bottle") { at = StnPt("bottler"); key = "bottler"; }
            else if (pick == "o2") { at = SettleStations.Approach(0); key = "elec"; }
            else { at = SettleStations.Approach(3); key = "green"; }
            return true;
        }
        // 물이 바닥나 화물선이 기다리면 기지 얼음밭에서 얼음을 주워 해빙기로(드릴만으론 정착민 몫도 빠듯하다)
        if (thirsty && G.WantsMore("ice") && G.HeldAll("ice") < 24)
        {
            int b = NearestIce(pos);
            if (b >= 0) { at = WorldMap.ICE_SPOTS[b]; key = "ice:" + b; return true; }
        }
        // 산소·식량이 모자라 화물선이 기다리면 그 체인은 기지 힌트(북쪽 일)에 맡긴다
        bool starving = (sp.ShipRemain("o2") > 0 && S.elecOut == 0) || (sp.ShipRemain("food") > 0 && S.grnOut == 0);
        // 연료 공장이 비어 가면 기지 얼음밭에서 얼음 줍기(든 얼음이 모자랄 때만)
        if (!starving && sp.FuelBuilt && S.fuelQ + sp.fuelPend + G.HeldAll("ice") < 30 && S.fuelOut < 30 && G.WantsMore("ice"))
        {
            int b = NearestIce(pos);
            if (b >= 0) { at = WorldMap.ICE_SPOTS[b]; key = "ice:" + b; return true; }
        }
        // 얼음 협곡: 정제소가 비어 가고 로버가 모자라면 광맥 얼음 줍기
        if (!starving && ic != null && ic.Open && ic.RefRoom > 10 && S.refOut < 30 && (ic.Rovers == 0 || S.refQ < 6))
        {
            int v = ic.NearestVein(pos);
            if (v >= 0) { at = IceCanyon.VEIN_SPOTS[v]; key = "vein:" + v; return true; }
        }
        return false;
    }
}

using System.Collections.Generic;
using UnityEngine;

// 4장 얼음 협곡(docs/chapter3-design.md): 거대한 얼음 광맥 · 정제소(얼음 → 연료통) · 채굴 로버(광맥 → 정제소 자동 운반) · KayKit 계단 절벽.
//  · 광맥 더미와 협곡 벽은 잠긴 동안에도 보인다(우주항 동쪽 끝에서 보이는 다음 지역 실루엣). 정제소·얼음 조각·로버는 협곡을 연 뒤.
//  · 광맥 둘레 얼음 조각(VEIN_SPOTS 8곳, 리스폰 S.vspots)을 주우면 S.veinPicked, 정제소 호퍼에 넣으면 S.refQ → 연료통 S.refOut(정제 누계 S.refMade).
//  · 로버(채굴 로버 Lv = 대수): 광맥 앞(LOAD)에서 ROVER_LOAD개 싣고 정제소(UNLOAD)로 와서 호퍼에 붓는다(S.roverHauled). 자리가 없으면 기다림.
public class IceCanyon : MonoBehaviour
{
    public static IceCanyon I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public const float ZONE_X0 = 1240f;
    public static readonly Vector2 VEIN = new Vector2(1530, 286), REFINERY = new Vector2(1340, 548);
    public static readonly Vector2[] VEIN_SPOTS =
    {
        new Vector2(1440, 318), new Vector2(1468, 348), new Vector2(1502, 364), new Vector2(1542, 368),
        new Vector2(1580, 360), new Vector2(1614, 340), new Vector2(1636, 304), new Vector2(1430, 280),
    };
    static readonly Rect REF_R = new Rect(REFINERY.x - 52, REFINERY.y - 36, 124, 72);
    public static readonly Vector2 REF_PT = new Vector2(REFINERY.x + 36f, REFINERY.y - 58f);
    static readonly Vector2 LOAD = new Vector2(1528, 360), UNLOAD = new Vector2(REFINERY.x + 6f, REFINERY.y - 58f), PARK = new Vector2(1470, 470);
    // 협곡 입구 절벽(길 북·남, 충돌)
    static readonly Vector2[] GATE_CLIFFS = { new Vector2(1272, 334), new Vector2(1272, 500) };

    public const int REF_Q_CAP = 60, REF_OUT_CAP = 40, ROVER_LOAD = 10;
    public const float REF_T = 0.7f, ROVER_SPEED = 62f, ROVER_ACT = 1.4f, VEIN_RESPAWN = 3.6f;
    const float FAC_R = 34f;

    public bool Open => G != null && G.PadLv("canyon") >= 1;
    public int Rovers => G == null ? 0 : G.PadLv("rover");
    public int RefRoom => Mathf.Max(0, REF_Q_CAP - S.refQ - refPend);
    public float RefTime => REF_T / (1f + 0.3f * G.PadLv("refspd")) / G.MachineSpeedMul;

    Transform root, openRoot, refHop, refTray; readonly Transform[] chunks = new Transform[8];
    int refPend, lastHop = -1, lastTray = -1; float visT;

    class Rover { public Transform truck, trailer, load; public Vector2 pos, dir = Vector2.up; public int state, ice, lastLoad = -1; public float t; public Vector2 from, to; }
    readonly List<Rover> rovers = new List<Rover>();

    void Awake()
    {
        I = this;
        Regions.SetSolid("vein", new Rect(VEIN.x - 74, VEIN.y - 44, 148, 74), null);
        Regions.SetSolid("refinery", new Rect(REFINERY.x - 50, REFINERY.y - 34, 120, 66), () => Open);
        for (int i = 0; i < GATE_CLIFFS.Length; i++) Regions.SetSolid("cliff" + i, new Rect(GATE_CLIFFS[i].x - 30, GATE_CLIFFS[i].y - 30, 60, 60), null);
    }

    void Start()
    {
        if (S.vspots == null || S.vspots.Length < VEIN_SPOTS.Length) S.vspots = new float[VEIN_SPOTS.Length];
        root = new GameObject("iceCanyon").transform; root.SetParent(transform, false);
        // 광맥 더미(얼음 재질은 물건 얼음과 같은 MarsIce)·발치 돌: 잠긴 동안에도 보임
        var vein = ToyMesh.Put(root, "vein", RegionDress.IceVein(), Items.IceMat, Vector3.zero); vein.transform.position = WorldMap.W2U(VEIN);
        var vr = RegionDress.Put(root, "veinRocks", RegionDress.VeinRocks(), 0.08f); vr.transform.position = WorldMap.W2U(VEIN);
        Cliffs();
        // 열린 뒤: 정제소·얼음 조각
        openRoot = new GameObject("open").transform; openRoot.SetParent(root, false);
        var refi = new GameObject("refinery").transform; refi.SetParent(openRoot, false); refi.position = WorldMap.W2U(REFINERY);
        RegionDress.Put(refi, "body", RegionDress.Refinery(), 0.15f);
        float k = GameConfig.K;
        refHop = new GameObject("hop").transform; refHop.SetParent(refi, false); refHop.localPosition = new Vector3(RegionDress.REF_HOP.x, RegionDress.REF_TOP + 1.2f, -RegionDress.REF_HOP.y) * k;
        refTray = new GameObject("tray").transform; refTray.SetParent(refi, false); refTray.localPosition = new Vector3(RegionDress.REF_TRAY.x, 4.2f, -RegionDress.REF_TRAY.y) * k;
        for (int i = 0; i < VEIN_SPOTS.Length; i++)
        {
            var c = ToyMesh.Put(openRoot, "chunk" + i, RegionDress.IceChunk(), Items.IceMat, Vector3.zero);
            c.transform.position = WorldMap.W2U(VEIN_SPOTS[i]); c.transform.rotation = Quaternion.Euler(0, i * 47f, 0);
            chunks[i] = c.transform;
        }
        openRoot.gameObject.SetActive(Open);
        for (int i = 0; i < Rovers; i++) AddRover(false);
        G.OnPadLevel += OnPad;
    }
    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPad; }

    // 협곡 벽은 메사(RegionWorld)가 맡고, 여기선 벽 발치에 굴러 내린 바위(KayKit rocks_B·rock_A, 메사 돌 색)만 띄엄띄엄.
    //  KayKit 지형 블록(terrain_tall·slope)은 시험해 보니 상자처럼 보여 뺐다(docs/chapter3-design.md)
    void Cliffs()
    {
        var g = new GameObject("boulders").transform; g.SetParent(root, false);
        foreach (var b in BOULDERS)
        {
            var go = RegionDress.Put(g, "boulder", RegionDress.Boulder((int)b.w), 0.06f);
            go.transform.position = WorldMap.W2U(b.x, b.y); go.transform.rotation = Quaternion.Euler(0, b.z, 0);
        }
    }
    static readonly Vector4[] BOULDERS =   // x, y, 회전, 모양(0 큰 무더기 · 1 작은 돌)
    {
        new Vector4(1300, 196, 30, 0), new Vector4(1440, 190, -40, 1), new Vector4(1660, 214, 70, 0),
        new Vector4(1262, 662, 10, 1), new Vector4(1500, 668, 120, 0), new Vector4(1300, 360, 200, 1),
    };

    void OnPad(string id, int lv)
    {
        if (id == "canyon" && lv >= 1 && !openRoot.gameObject.activeSelf)
        {
            openRoot.gameObject.SetActive(true);
            foreach (Transform c in openRoot) JuiceFX.I.Rise(c, 0.2f + Random.value * 0.4f, 20f, 0.7f);
            WorldMap.fenceVer++;
        }
        if (id == "rover") { while (rovers.Count < lv) AddRover(true); }
    }

    void AddRover(bool fx)
    {
        var r = new Rover();
        r.truck = new GameObject("rover").transform; r.truck.SetParent(root, false);
        RegionDress.Put(r.truck, "truck", RegionDress.Rover(), 0.22f);
        r.trailer = new GameObject("trailer").transform; r.trailer.SetParent(root, false);
        RegionDress.Put(r.trailer, "body", RegionDress.Trailer(), 0.18f);
        r.load = new GameObject("load").transform; r.load.SetParent(r.trailer, false); r.load.localPosition = new Vector3(0, RegionDress.TRAILER_TOP * GameConfig.K, 0);
        r.pos = PARK + new Vector2(rovers.Count * 26f, 0); r.state = 0; r.from = r.pos; r.to = LOAD; r.t = rovers.Count * 1.5f;
        rovers.Add(r);
        Place(r);
        if (fx) { JuiceFX.I.Rise(r.truck, 0.1f, 20f); JuiceFX.I.Ring(WorldMap.W2U(r.pos), GameConfig.GOLD, 30); }
    }

    // ───────────────────────── 매 프레임 ─────────────────────────
    void Update()
    {
        if (G == null || !Open) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 광맥 얼음 조각 리스폰
        for (int i = 0; i < VEIN_SPOTS.Length; i++)
        {
            if (S.vspots[i] > 0f) S.vspots[i] -= dt;
            bool on = S.vspots[i] <= 0f;
            if (chunks[i].gameObject.activeSelf != on) { chunks[i].gameObject.SetActive(on); if (on) JuiceFX.I.Pop(chunks[i], 0.2f, 0.35f); }
        }
        // 정제소
        if (S.refQ > 0 && S.refOut < REF_OUT_CAP)
        {
            S.refT += dt;
            if (S.refT >= RefTime) { S.refT = 0; S.refQ--; S.refOut++; S.refMade++; MissionBoard.I?.Count("fuel", 1); }
        }
        foreach (var r in rovers) TickRover(r, dt);
        visT += dt; if (visT < 0.2f) return; visT = 0;
        int hop = Mathf.Min(S.refQ, 12), tray = Mathf.Min(S.refOut, 24);
        if (hop != lastHop) { lastHop = hop; Items.SyncStack(refHop, "ice", hop, 3, 2, 10.5f, 8.5f, 12); }
        if (tray != lastTray) { lastTray = tray; Items.SyncStack(refTray, "fuel", tray, 3, 2, 7.8f, 7.2f, 24); }
    }

    // 로버: 0 광맥으로 → 1 싣기 → 2 정제소로 → 3 붓기(자리 날 때까지) → 0
    void TickRover(Rover r, float dt)
    {
        switch (r.state)
        {
            case 0:
            case 2:
                {
                    if (r.t > 0f) { r.t -= dt; break; }
                    var target = r.state == 0 ? LOAD : UNLOAD;
                    var d = target - r.pos; float L = d.magnitude;
                    if (L < 2f) { r.pos = target; r.state++; r.t = ROVER_ACT; break; }
                    var dir = d / L;
                    r.dir = Vector2.Lerp(r.dir, dir, dt * 4f).normalized;
                    r.pos += r.dir * Mathf.Min(L, ROVER_SPEED * dt);
                    break;
                }
            case 1:
                r.t -= dt;
                if (r.t <= 0f) { r.ice = ROVER_LOAD; r.state = 2; JuiceFX.I.Dust(WorldMap.W2U(r.pos, 2f), 2); }
                break;
            case 3:
                if (r.ice > 0)
                {
                    r.t -= dt;
                    if (r.t <= 0f && S.refQ + refPend < REF_Q_CAP)
                    {
                        r.t = 0.12f; r.ice--; refPend++;
                        var from = r.load.position + Vector3.up * 0.2f;
                        ArcFlyer.I.Fly(Items.Slab(ArcFlyer.I.transform, "ice"), from, () => refHop.position + Vector3.up * 0.3f, 0.35f, 30f, () => { refPend--; S.refQ++; S.roverHauled++; });
                    }
                }
                else { r.state = 0; r.t = 0.4f; }
                break;
        }
        if (r.ice != r.lastLoad) { r.lastLoad = r.ice; Items.SyncStack(r.load, "ice", Mathf.Min(r.ice, 6), 2, 3, 9f, 9f, 6); }
        Place(r);
    }

    void Place(Rover r)
    {
        r.truck.position = WorldMap.W2U(r.pos);
        r.truck.rotation = Quaternion.LookRotation(new Vector3(r.dir.x, 0, -r.dir.y), Vector3.up);
        var tp = r.pos - r.dir * (RegionDress.TRAILER_LEN * 0.95f);
        r.trailer.position = WorldMap.W2U(tp); r.trailer.rotation = r.truck.rotation;
    }

    // ───────────────────────── 플레이어 ─────────────────────────
    static bool NearRect(Rect r, Vector2 p, float rad)
    {
        float dx = p.x < r.xMin ? r.xMin - p.x : p.x > r.xMax ? p.x - r.xMax : 0f;
        float dy = p.y < r.yMin ? r.yMin - p.y : p.y > r.yMax ? p.y - r.yMax : 0f;
        return dx * dx + dy * dy < rad * rad;
    }
    public int NearestVein(Vector2 p)
    {
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < VEIN_SPOTS.Length; i++) if (S.vspots[i] <= 0f) { float d = (p - VEIN_SPOTS[i]).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        return best;
    }

    public void Interact(Player pl)
    {
        if (!Open) return;
        var p = pl.pos;
        // 1. 광맥 얼음 줍기: 정제소 자리만큼만(든 얼음 포함)
        if (G.CanCarry("ice") && G.HeldAll("ice") + S.refQ + refPend < REF_Q_CAP + 20)
        {
            int best = -1; float bd = GameConfig.R_ICE;
            for (int i = 0; i < VEIN_SPOTS.Length; i++) { if (S.vspots[i] > 0f) continue; float d = Vector2.Distance(p, VEIN_SPOTS[i]); if (d < bd) { bd = d; best = i; } }
            if (best >= 0)
            {
                S.vspots[best] = VEIN_RESPAWN + Random.value * 1.2f; chunks[best].gameObject.SetActive(false);
                G.PendAdd("ice");
                ArcFlyer.I.Fly(pl.FlyBlock("ice"), WorldMap.W2U(VEIN_SPOTS[best], 5f), () => pl.InsertWorld("ice"), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { pl.Arrive("ice"); S.veinPicked++; });
                pl.actT = GameConfig.CD_ICE; return;
            }
        }
        if (!NearRect(REF_R, p, FAC_R)) return;
        // 2. 정제소 호퍼에 얼음
        if (G.Holds("ice") && S.refQ + refPend < REF_Q_CAP)
        {
            refPend++; var from = pl.TakeOut("ice");
            ArcFlyer.I.Fly(pl.FlyBlock("ice"), from, () => refHop.position + Vector3.up * 0.3f, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { refPend--; S.refQ++; });
            AudioManager.I.Play("put", 1f + Random.Range(-0.05f, 0.05f));
            pl.actT = GameConfig.CD_MELT; return;
        }
        // 3. 연료통 받기(발사대가 받을 만큼)
        if (S.refOut > 0 && G.WantsMore("fuel"))
        {
            S.refOut--; G.PendAdd("fuel");
            ArcFlyer.I.Fly(pl.FlyBlock("fuel"), refTray.position + Vector3.up * 0.3f, () => pl.InsertWorld("fuel"), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => pl.Arrive("fuel"));
            pl.actT = 0.09f; return;
        }
    }
}

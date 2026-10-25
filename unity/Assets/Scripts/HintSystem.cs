using UnityEngine;

// §6.2 힌트: 바닥 화살표(타깃 70 초과로 멀 때, 플레이어 앞 30) + 타깃 링(r26 펄스). 우선순위는 hint() Phase 1 부분집합 + 생존 기지 생산 체인.
public class HintSystem : MonoBehaviour
{
    public static HintSystem I;
    GameState G => GameState.I;
    SaveData S => G.S;
    Transform arrow, ring; public Vector2 target; public string targetKey = "";
    public bool hasTarget = true;   // 할 일이 없으면 false(화살표·링 숨김, 원본 "힌트 없음")
    float t;

    void Awake() { I = this; }

    void Start()
    {
        arrow = new GameObject("arrow").transform; arrow.SetParent(transform, false);
        var mf = arrow.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = ArrowMesh();
        var mr = arrow.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mats.Solid(GameConfig.GOLD);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        arrow.localScale = Vector3.one * 22 * GameConfig.K;
        ring = Prim.Ring(transform, Vector3.zero, 26, GameConfig.GOLD).transform;
    }

    static Mesh ArrowMesh()
    {
        // 평면 화살표: 꼬리 사각 + 머리 삼각, +z 방향
        var v = new Vector3[] { new Vector3(-0.25f, 0, -1), new Vector3(0.25f, 0, -1), new Vector3(0.25f, 0, 0.2f), new Vector3(-0.25f, 0, 0.2f), new Vector3(-0.7f, 0, 0.2f), new Vector3(0.7f, 0, 0.2f), new Vector3(0, 0, 1) };
        var tri = new int[] { 0, 2, 1, 0, 3, 2, 4, 6, 5 };
        var n = new Vector3[v.Length]; for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
        return new Mesh { vertices = v, triangles = tri, normals = n };
    }

    // 등짐에 실을 수 있는 종류(넣을 곳 찾기 순서)
    static readonly string[] KINDS = { "ice", "bottle", "rego", "soil", "fert", "o2", "food" };
    public bool delivering;   // 지금 든 것을 넣으러 가는 중(봇: 이때는 발판 쇼핑·좌석 팁으로 끊지 않는다)

    // 힌트 우선순위 (§6.2) Phase 1 부분집합. 등짐은 섞어 싣는다(10/25): 든 것 중 지금 넣을 곳이 있는 종류(목적지가 가장 가까운 것)부터 안내하고,
    // 다 넣을 곳이 없으면 빈손일 때처럼 줍기·받기 일을 안내한다(등짐이 가득일 때만 기다림)
    Vector2 Pick()
    {
        var st = WorldMap.STN; int price = G.BottlePrice;
        delivering = false;
        bool anyOpen = false; for (int i = 0; i < Stations.I.SpotCount; i++) if (Stations.I.SpotOpen(i)) anyOpen = true;
        // 타깃 점은 상호작용 반경 안쪽(스테이션 남쪽 가장자리 + 18)
        Vector2 P(string k) { var s = st[k]; targetKey = k; return new Vector2(s.x, s.y + s.h / 2 + 18); }
        Vector2 Cnt() { targetKey = "counter"; return CntPt(); }
        Vector2 Ice()
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Stations.I.SpotCount; i++) if (Stations.I.SpotOpen(i)) { float d = Vector2.Distance(Player.I.pos, WorldMap.ICE_SPOTS[i]); if (d < bd) { bd = d; best = i; } }
            targetKey = "ice:" + best; return WorldMap.ICE_SPOTS[best];
        }
        bool full = G.Carry + G.pend >= G.CarryMax;   // 등짐 가득(더 못 줍는다)
        // 해빙기 큐 + 든 얼음이 상한이면 얼음은 더 못 줍는다(Player 1번) → 든 것만 넣고 다른 일
        anyOpen &= G.IceWantedHeld;
        // 해빙기 옆이면 든 얼음을 다 넣을 때까지 머문다(몇 개 넣자마자 "덜 찼다"며 얼음밭으로 돌아가는 왕복 방지)
        if (G.Holds("ice") && WorldMap.NearStn(Player.I.pos, "melter", GameConfig.R_MELT)) return P("melter");
        // 화성 이벤트(보급 캡슐·얼음 운석)가 바닥에 있으면 시간 제한이 있으니 먼저 그쪽(화살표·링만)
        if (MarsEvents.I != null && MarsEvents.I.PickupTarget(out var ev)) { targetKey = "event"; return ev; }
        // 정착 단계에 필요한 구역·본부 발판을 바로 살 수 있으면 그게 먼저(들고 있어도 발판 위에 서면 납입된다). 나르기 일에 밀려 다음 단계가 안 열리는 걸 막는다
        if (S.sold >= 1 && PadManager.I.visOrder.Count > 0)
        {
            string kid = PadManager.I.visOrder[0]; var kd = Pads.Get(kid);
            if ((kd.zone || kid == "hq") && S.bells >= G.PadRemain(kid)) { targetKey = "pad:" + kid; return kd.pos; }
        }
        var ss = SettleStations.I;
        Vector2 F(int i) { targetKey = SettleStations.KEYS[i]; return SettleStations.Approach(i); }
        Vector2 Rego() { int b = ss.NearestRego(Player.I.pos, float.MaxValue); targetKey = "rego:" + b; return WorldMap.REGO_SPOTS[b]; }
        // 든(또는 날아오는) 것 중 넣을 곳이 있는 종류: 목적지가 가장 가까운 것
        string hk = ""; float hd = float.MaxValue;
        foreach (var k in KINDS)
        {
            if (G.HeldAll(k) == 0 || !DestOf(k, ss, out var at)) continue;
            float d = Vector2.Distance(Player.I.pos, at);
            if (d < hd) { hd = d; hk = k; }
        }
        int hn = hk == "" ? 0 : G.HeldAll(hk);
        delivering = hk != "";
        // 출력 더미 옆에서 받는 중이면(근처일 때만: 멀어진 뒤 새로 1개 생겼다고 되돌아오지 않게) 가득 차거나 빌 때까지 그 자리
        //  그 종류를 더 받을 수 없으면(종류별 상한, GameState.WantsMore) 기다리지 않는다: 비료 40을 든 채 배합기 옆에서 영영 서 있던 막힘(10/25 시설 속도 발판 뒤 페이싱 점검)
        bool Taking(int i, int outN) => !full && outN > 0 && ss.Near(i, Player.I.pos) && G.WantsMore(SettleStations.OUT_K[i]);
        // 얼음은 ICE_BATCH개 모이거나 밭이 비면 해빙기로(등짐 100개를 다 채울 때까지 얼음밭에 붙잡아 두지 않는다), 아니면 계속 줍기
        if (hk == "ice") return (full || hn >= Mathf.Min(G.CarryMax, GameConfig.ICE_BATCH) || !anyOpen) ? P("melter") : Ice();
        if (ss != null)
        {
            if (hk == "rego") return (full || !ss.AnyRego || S.purQ + ss.qPend[1] + hn >= GameConfig.SETTLE_Q_CAP) ? F(1) : Rego();   // 정화기 큐를 채울 만큼 들었으면 넣기
            if (hk == "soil") return F(2);
            if (hk == "fert") return Taking(2, S.mixOut + S.mixQ) ? F(2) : F(3);   // 배합 중인 것까지 기다렸다 한 번에
            // 산소통·식량은 카운터 칸에 들어갈 만큼만 받는다(정착민 수요가 정해져 있어 더 들면 등짐만 찬다)
            if (hk == "o2") return Taking(0, S.elecOut) && hn < Room("o2") ? F(0) : Cnt();
            if (hk == "food") return Taking(3, S.grnOut) && hn < Room("food") ? F(3) : Cnt();
            // 물통은 뽀삐가 선반을 채우거나 선반이 가득이면 전기분해기로
            if (hk == "bottle" && ElecTakesBottles(ss) && (G.PadLv("runner") >= 1 || S.shelf + G.shelfPend >= G.ShelfCap)) return F(0);
        }
        if (hk == "bottle") return S.shelf + G.shelfPend < G.ShelfCap ? Cnt() : F(0);
        // 여기부터는 든 것 중 지금 넣을 곳이 없다(빈손 포함). 등짐이 가득이면 계산대 돈을 걷거나 첫 종류 자리에서 기다린다
        if (full)
        {
            if (S.cash > 0) return P("register");
            string k0 = G.CarryTop != "" ? S.p.carry[0] : G.pendK;
            if (k0 == "ice") return P("melter");
            if (ss != null && k0 == "rego") return F(1);
            if (ss != null && k0 == "soil") return F(2);
            if (ss != null && k0 == "fert") return F(3);
            return Cnt();
        }
        // 정화기·배합기 틈에 있으면 넣어 둔 묶음이 흙 → 비료가 될 때까지 머문다(레골리스 한 번 나르면 비료까지 이어지게)
        if (ss != null && (ss.Near(1, Player.I.pos) || ss.Near(2, Player.I.pos))
            && ((S.purQ + S.purOut > 0 && ss.CanPut(2)) || (S.mixQ + S.mixOut > 0 && ss.CanPut(3)))) return S.purQ + S.purOut > 0 ? F(1) : F(2);
        if (S.cash > 0 && (!G.HasCashier || S.cash > price * 4)) return P("register");
        if (S.bottles > 0 && S.shelf + G.shelfPend < G.ShelfCap && G.PadLv("runner") == 0) return P("bottler");   // 뽀삐가 있으면 운반은 뽀삐 몫
        if (CustomerSystem.I.AnyWaitingToPay() && !G.HasCashier) return P("register");
        // 토끼 팁 ≥ price*4 → 우리 팁 자리 (§6.2)
        if (S.penTip >= price * 4 && G.PadLv("pen") >= 1) { targetKey = "pen"; return WorldMap.PEN_TIP; }
        // 다음 발판: sold≥1 & bells≥남은 금액의 50%
        if (S.sold >= 1 && PadManager.I.visOrder.Count > 0)
        {
            string id = PadManager.I.visOrder[0];   // ORDER상 차례인 발판
            if (S.bells >= G.PadRemain(id) * 0.5f) { targetKey = "pad:" + id; return Pads.Get(id).pos; }
        }
        // (드릴 없고 큐 비었으면) 가까운 얼음 더미 · 그 외 병입기 (§6.2). 드릴이 있으면 얼음은 드릴 몫이라 일이 없으면 타깃 없음
        bool drill = G.PadLv("drill") >= 1;
        // 생존 기지(넣을 것이 없을 때): 완성품(식량·산소통) 받기 → 비료 → 흙 → 레골리스 → 전기분해기용 물통. 산소·식량 중 카운터+기계 재고가 적은 쪽을 먼저
        int north = -1;   // 0~3 시설, 4 레골리스, 5 병입기(물통)
        if (ss != null)
        {
            // 완성품(식량)을 먼저 정착민에게: 온실 출력이 차면 비료를 더 넣어도 멈춰 있으니 받아 가는 게 먼저. 카운터 칸에 자리가 있을 때만
            // 원료 넣기는 다음 시설이 밀려 있지 않을 때만(큐 여유 8칸 이상·출력 가득 전): 멈춘 시설에 계속 붓느라 체인 끝이 비는 걸 막는다. 한 번에 크게 날라 왕복을 줄인다
            const int FEED = GameConfig.SETTLE_Q_CAP - 8, OUTC = GameConfig.SETTLE_OUT_CAP;
            bool foodReady = S.grnOut >= 4 && Room("food") >= 4, o2Ready = S.elecOut >= 4 && Room("o2") >= 4;
            bool o2Feed = S.bottles > 0 && ElecWantsBottles(ss);
            // 산소 공급 누계가 식량보다 적으면 산소 쪽 일을 먼저(정착 목표는 산소·식량 누계를 같이 요구): 나르기가 한 체인에만 쏠리지 않게.
            // 오늘 미션에 산소·식량 공급이 남아 있으면 그쪽을, 레골리스·비료가 남아 있으면 흙 체인(레골리스 → 흙 → 비료)을 먼저(9단계 "하루 미션 누적 완료"가 늘어지지 않게)
            bool mO2 = MissionWants("o2"), mFood = MissionWants("food"), mChain = MissionWants("rego") || MissionWants("fert");
            bool o2First = ss.Open(0) && (mO2 || (!mFood && (!G.KindOpen("food") || S.supO < S.supF)));
            if (mChain && Batch(S.mixOut, S.mixQ) && ss.CanPut(3) && S.grnOut < OUTC && S.grnQ + ss.qPend[3] < FEED) north = 2;
            else if (mChain && Batch(S.purOut, S.purQ) && ss.CanPut(2) && S.mixOut < OUTC && S.mixQ + ss.qPend[2] < FEED) north = 1;
            else if (mChain && ss.AnyRego && S.purQ + ss.qPend[1] < FEED && S.purOut < OUTC) north = 4;
            else if (o2Ready && (o2First || !foodReady)) north = 0;
            else if (foodReady) north = 3;
            else if (o2First && o2Feed) north = 5;
            else if (Batch(S.mixOut, S.mixQ) && ss.CanPut(3) && S.grnOut < OUTC && S.grnQ + ss.qPend[3] < FEED) north = 2;
            else if (Batch(S.purOut, S.purQ) && ss.CanPut(2) && S.mixOut < OUTC && S.mixQ + ss.qPend[2] < FEED) north = 1;
            else if (ss.AnyRego && S.purQ + ss.qPend[1] < FEED && S.purOut < OUTC) north = 4;
            else if (o2Feed) north = 5;
        }
        Vector2 North() => north == 4 ? Rego() : north == 5 ? P("bottler") : F(north);
        // 기지 재고(선반+병)가 선반 칸만큼 있으면 북쪽 일을 얼음 줍기보다 먼저, 모자라면 얼음 먼저
        if (north >= 0 && S.shelf + S.bottles >= G.ShelfCap) return North();
        if (!drill && S.meltQ == 0 && anyOpen) return Ice();
        if (north >= 0) return North();
        // 뽀삐가 있으면 병 나르기는 뽀삐 몫: 전기분해기가 물통을 원할 때만 병입기로(빈 손으로 병입기 앞에 서면 병을 집어 들어 손이 묶인다)
        bool runner = G.PadLv("runner") >= 1;
        if (S.bottles > 0 && G.BottleHasPlace && (!runner || ss == null || ElecWantsBottles(ss))) return P("bottler");
        if (!drill && anyOpen) return Ice();
        if (drill && S.meltQ == 0 && S.water < 10) { hasTarget = false; return P("bottler"); }
        if (runner) { hasTarget = false; return P("register"); }   // 할 일 없음: 계산대 옆에서 기다림(링·화살표 숨김)
        return P("bottler");
    }

    static Vector2 CntPt() { var c = WorldMap.STN["counter"]; return new Vector2(c.x, c.y - c.h / 2 - 14); }
    static Vector2 StnPt(string k) { var s = WorldMap.STN[k]; return new Vector2(s.x, s.y + s.h / 2 + 18); }
    // 그 종류를 지금 넣을 곳이 있나(목적지 점과 함께). 얼음은 해빙기가 늘 받는다
    bool DestOf(string k, SettleStations ss, out Vector2 at)
    {
        at = Vector2.zero;
        switch (k)
        {
            case "ice": at = StnPt("melter"); return true;
            case "bottle":
                if (S.shelf + G.shelfPend < G.ShelfCap) { at = CntPt(); return true; }
                if (ss != null && ElecTakesBottles(ss)) { at = SettleStations.Approach(0); return true; }
                return false;
            case "o2": case "food": at = CntPt(); return Room(k) > 0;
            case "rego": if (ss == null || !ss.CanPut(1)) return false; at = SettleStations.Approach(1); return true;
            case "soil": if (ss == null || !ss.CanPut(2)) return false; at = SettleStations.Approach(2); return true;
            case "fert": if (ss == null || !ss.CanPut(3)) return false; at = SettleStations.Approach(3); return true;
        }
        return false;
    }
    // 전기분해기가 물통을 받을 수 있나(열려 있고 출력·큐에 자리)
    bool ElecTakesBottles(SettleStations ss) => ss.Open(0) && S.elecOut < GameConfig.SETTLE_OUT_CAP && S.elecQ + ss.qPend[0] < GameConfig.SETTLE_Q_CAP;
    // 중간재(흙·비료)를 받으러 갈 만한 묶음인가: 8개 이상 쌓였거나 큐가 다 끝났을 때(몇 개 때문에 북쪽 끝까지 왕복하지 않게)
    static bool Batch(int outN, int qN) => outN >= 8 || (outN > 0 && qN == 0);
    // 오늘 미션 중 아직 안 끝난 그 종류가 있나(시트 목표 탭의 하루 미션)
    static bool MissionWants(string id)
    {
        var mb = MissionBoard.I; if (mb == null || !mb.Active) return false;
        foreach (var m in mb.Today) if (m.id == id && !m.done) return true;
        return false;
    }
    int Room(string k) => G.ShelfCap - G.ShelfOf(k) - Stations.I.ShelfPendOf(k);

    // 전기분해기 큐가 바닥나 가고(6 미만) 출력 자리가 있으면 물통을 날라 줄 때
    bool ElecWantsBottles(SettleStations ss) => ss.Open(0) && S.elecQ + ss.qPend[0] < 6 && S.elecOut < GameConfig.SETTLE_OUT_CAP;

    void Update()
    {
        var pl = Player.I; if (pl == null || Stations.I == null) return;
        t += Time.deltaTime;
        hasTarget = true;
        target = Pick();
        float d = Vector2.Distance(pl.pos, target);
        bool far = hasTarget && d > 70f;
        ring.gameObject.SetActive(hasTarget);
        arrow.gameObject.SetActive(far);
        if (far)
        {
            Vector2 dir = (target - pl.pos).normalized;
            Vector2 ap = pl.pos + dir * 30f;
            arrow.position = WorldMap.W2U(ap, 30f) + Vector3.up * Mathf.Sin(t * 5f) * 0.1f;
            arrow.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, -dir.y), Vector3.up);
        }
        ring.position = WorldMap.W2U(target, 0.5f);
        ring.localScale = Vector3.one * 26 * GameConfig.K * (1 + Mathf.Sin(t * 6f) * 0.12f);
    }
}

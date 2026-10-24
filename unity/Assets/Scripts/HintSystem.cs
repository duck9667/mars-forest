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

    // 힌트 우선순위 (§6.2) Phase 1 부분집합
    Vector2 Pick()
    {
        var st = WorldMap.STN; int price = G.BottlePrice; string top = G.CarryTop;
        bool anyOpen = false; for (int i = 0; i < Stations.I.SpotCount; i++) if (Stations.I.SpotOpen(i)) anyOpen = true;
        // 타깃 점은 상호작용 반경 안쪽(스테이션 남쪽 가장자리 + 18)
        Vector2 P(string k) { var s = st[k]; targetKey = k; return new Vector2(s.x, s.y + s.h / 2 + 18); }
        Vector2 Cnt() { targetKey = "counter"; return new Vector2(st["counter"].x, st["counter"].y - st["counter"].h / 2 - 14); }
        Vector2 Ice()
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Stations.I.SpotCount; i++) if (Stations.I.SpotOpen(i)) { float d = Vector2.Distance(Player.I.pos, WorldMap.ICE_SPOTS[i]); if (d < bd) { bd = d; best = i; } }
            targetKey = "ice:" + best; return WorldMap.ICE_SPOTS[best];
        }
        bool iceFull = G.Carry + G.pend >= G.CarryMax;
        // 해빙기 옆이면 다 넣을 때까지 머문다(몇 개 넣자마자 "덜 찼다"며 얼음밭으로 돌아가는 왕복 방지)
        if (top == "ice" && WorldMap.NearStn(Player.I.pos, "melter", GameConfig.R_MELT)) return P("melter");
        if (top == "ice" || G.pendK == "ice") return (iceFull || !anyOpen) ? P("melter") : Ice();   // 얼음 가득/밭 비었음→해빙기, 아니면 계속 줍기
        // 생존 기지: 들고 있는(또는 날아오는) 종류별 목적지. 출력을 받는 중이면 가득 차거나 더미가 빌 때까지 그 자리
        var ss = SettleStations.I;
        Vector2 F(int i) { targetKey = SettleStations.KEYS[i]; return SettleStations.Approach(i); }
        Vector2 Rego() { int b = ss.NearestRego(Player.I.pos, float.MaxValue); targetKey = "rego:" + b; return WorldMap.REGO_SPOTS[b]; }
        string hk = top != "" ? top : G.pend > 0 ? G.pendK : "";
        // 출력 더미 옆에서 받는 중이면(근처일 때만: 멀어진 뒤 새로 1개 생겼다고 되돌아오지 않게) 가득 차거나 빌 때까지 그 자리
        bool Taking(int i, int outN) => !iceFull && outN > 0 && ss.Near(i, Player.I.pos);
        if (ss != null)
        {
            if (hk == "rego") return (iceFull || !ss.AnyRego || S.purQ + ss.qPend[1] >= GameConfig.SETTLE_Q_CAP / 2) ? F(1) : Rego();   // 정화기에 이미 많으면 더 줍지 말고 넣기
            if (hk == "soil") return F(2);
            if (hk == "fert") return Taking(2, S.mixOut + S.mixQ) ? F(2) : F(3);   // 배합 중인 것까지 기다렸다 한 번에
            if (hk == "o2") return Taking(0, S.elecOut) ? F(0) : Cnt();
            if (hk == "food") return Taking(3, S.grnOut) ? F(3) : Cnt();
            // 빈 손으로 정화기·배합기 틈에 있으면 넣어 둔 묶음이 흙 → 비료가 될 때까지 머문다(레골리스 한 번 나르면 비료까지 이어지게)
            if (hk == "" && (ss.Near(1, Player.I.pos) || ss.Near(2, Player.I.pos))
                && ((S.purQ + S.purOut > 0 && ss.CanPut(2)) || (S.mixQ + S.mixOut > 0 && ss.CanPut(3)))) return S.purQ + S.purOut > 0 ? F(1) : F(2);
            // 물통은 뽀삐가 선반을 채우거나 선반이 가득이면 전기분해기로
            if (top == "bottle" && ss.Open(0) && S.elecOut < GameConfig.SETTLE_OUT_CAP && S.elecQ + ss.qPend[0] < GameConfig.SETTLE_Q_CAP && (G.PadLv("runner") >= 1 || S.shelf + G.shelfPend >= G.ShelfCap))
                return F(0);
        }
        if (top == "bottle" && S.shelf + G.shelfPend < G.ShelfCap) return Cnt();
        if (top == "bottle") return G.HasCashier ? Cnt() : P("register");
        if (S.cash > 0 && (!G.HasCashier || S.cash > price * 4)) return P("register");
        if (S.bottles > 0 && G.Carry == 0 && S.shelf + G.shelfPend < G.ShelfCap && G.PadLv("runner") == 0) return P("bottler");   // 뽀삐가 있으면 운반은 뽀삐 몫
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
        // 생존 기지(빈 손): 비료(온실에 자리 있을 때) → 흙(바로 옆 배합기로) → 레골리스 → 식량·산소통 6개 이상 → 전기분해기용 물통
        int north = -1;   // 0~3 시설, 4 레골리스, 5 병입기(물통)
        if (ss != null && G.Carry == 0 && G.pend == 0)
        {
            if (S.mixOut > 0 && ss.CanPut(3)) north = 2;
            else if (S.purOut > 0 && ss.CanPut(2)) north = 1;
            else if (ss.AnyRego && S.purQ + ss.qPend[1] < GameConfig.SETTLE_Q_CAP / 2) north = 4;
            else if (S.grnOut >= 6) north = 3;
            else if (S.elecOut >= 6) north = 0;
            else if (S.bottles > 0 && ElecWantsBottles(ss)) north = 5;
        }
        Vector2 North() => north == 4 ? Rego() : north == 5 ? P("bottler") : F(north);
        // 기지 재고(선반+병)가 선반 칸만큼 있으면 북쪽 일을 얼음 줍기보다 먼저, 모자라면 얼음 먼저
        if (north >= 0 && S.shelf + S.bottles >= G.ShelfCap) return North();
        if (!drill && S.meltQ == 0 && anyOpen && G.Carry == 0) return Ice();
        if (north >= 0) return North();
        if (S.bottles > 0) return P("bottler");
        if (!drill && anyOpen && G.Carry < G.CarryMax) return Ice();
        if (drill && S.meltQ == 0 && S.water < 10) { hasTarget = false; return P("bottler"); }
        return P("bottler");
    }

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

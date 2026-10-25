using UnityEngine;

// §1 코어 루프의 고정 스테이션: 얼음 더미 8 · 해빙기 · 병입기 · 카운터(선반) · 계산대(지폐 더미)
public class Stations : MonoBehaviour
{
    public static Stations I;
    GameState G => GameState.I;

    public Transform meltAnchor, trayAnchor, shelfAnchor, cashAnchor;
    // 레벨 외형(LevelLook): 해빙기·병입기·카운터(선반 칸)·계산대(프리미엄 라벨). StationDress 가 만든다
    public TierLook melterLook, bottlerLook, counterLook, registerLook;
    // 병입기 고급 단계의 두 번째 병 상자(trayTwin 이면 trayAnchor·trayAnchor2 에 3×2씩 번갈아 채운다)
    public Transform trayAnchor2; bool trayTwin;
    // 카운터 종류별 칸: 물(shelfAnchor)·산소·식량. 산소·식량이 열리기 전엔 물이 카운터 전체(12×3)를 쓴다
    public Transform shelfO2Anchor, shelfFoodAnchor;
    Transform[] shelfMarks = new Transform[3];   // 칸마다 상판 위 색 받침판(글자 라벨은 두지 않는다). 앞면은 늘 있는 차양에 가려 위에 둔다
    bool shelfSplit;
    // 산소·식량 칸으로 날아가는 중인 수(물은 G.shelfPend). 칸마다 ShelfCap을 넘지 않게 함께 센다
    public int shelfPendO2, shelfPendFood;
    public Transform melterView, bottlerView, counterView, registerView, drillView;
    public const int ALL_SPOTS = 12;
    Transform[] spotViews = new Transform[ALL_SPOTS];
    Transform[] spotRoots = new Transform[ALL_SPOTS];
    bool[] spotShown = new bool[ALL_SPOTS];
    GameObject meltCore;
    // 수치 라벨(얼음·물·병·선반·+n)은 두지 않는다: 쌓인 모형(얼음·병·지폐 더미)으로 충분 (jake 피드백 10/24)
    int lastMeltQ = -1, lastBottles = -1, lastShelf = -1, lastShelfO2 = -1, lastShelfFood = -1, lastCash = -1;
    float coreT;

    // 카운터 칸 받침판(물 하늘색·산소 흰빛·식량 밀색): 진한 원색 판은 상판을 덮은 색 블록처럼 보여 옅은 톤으로, 둘레에 상판이 테처럼 남게
    static readonly Color[] SHELF_MARK = { GameConfig.Hex("7fc3e8"), GameConfig.Hex("e3e9ee"), GameConfig.Hex("eed29b") };
    // 얼음 더미(fb2): 등짐·호퍼 얼음과 같은 언어(Items.ICE_*) — 모서리를 깎은 하늘색 덩어리, 가운데가 진하고 깎은 위 모서리가 흰빛.
    // 큰 덩어리 하나 + 앞 작은 덩어리 + 조각 하나를 메시 하나로(예전 몸통·윗판·빛줄·유리 껍질 5조각은 회색 상자처럼 보였고 껍질은 투명 정렬이 흔들렸다)
    static Mesh IcePileMesh(int v) => ToyMesh.Cached("icePile" + v, m =>
    {
        Color F = Items.ICE_FACE, C = Items.ICE_CORE, E = Items.ICE_EDGE, T = Items.ICE_TOP, TC = Items.ICE_TOP_CORE;
        // 큰 덩어리(살짝 기움) + 앞 오른쪽에 기대 선 중간 덩어리 + 큰 덩어리 위 뒤쪽 조각: 한쪽으로 쏠리게(양옆 대칭이면 소파처럼 보였다)
        m.At(new Vector3(-1.5f, 5.4f, 0.5f), new Vector3(0, 0, v == 1 ? 3 : -2)).Bevel(new Vector3(19, 10.6f, 14), 1.8f, F, C, E, T, TC).Glint(new Vector3(19, 10.6f, 14), Items.ICE_GLINT);   // 윗면 반사 줄(투명감)
        m.At(new Vector3(9.5f, 4.3f, -6f), new Vector3(-6, 30 + v * 9, -12)).Bevel(new Vector3(10, 8.4f, 8.5f), 1.4f, F, C, E, T, TC);
        m.At(new Vector3(-4.5f, 12.4f, 3f), new Vector3(6, -24 - v * 11, 7)).Bevel(new Vector3(7.5f, 4.4f, 6.5f), 1f, F, C, E, T, TC);
        return m;
    });
    void Awake() { I = this; }

    void Start()
    {
        Build();
        G.OnPadLevel += OnPad;
    }
    void OnDestroy() { if (GameState.I != null) GameState.I.OnPadLevel -= OnPad; }
    void OnPad(string id, int lv)
    {
        if (id == "o2" || id == "farm") RefreshShelfLayout();
        // 레벨이 올라 외형 단계가 바뀌면 바뀐 부품이 솟아오른다(같은 단계면 아무 일 없음)
        int t = LevelLook.Tier(lv);
        if (id == "melter") melterLook?.Apply(t, true);
        else if (id == "bottler") bottlerLook?.Apply(t, true);
        else if (id == "shelf") counterLook?.Apply(t, true);
        else if (id == "price") registerLook?.Apply(t, true);
    }

    // 병 상자 하나(3×4) ↔ 둘(3×2씩) 바꾸기: 다음 Update 에서 병을 다시 놓는다
    public void SetTrayTwin(bool twin)
    {
        if (twin == trayTwin && lastBottles >= 0) return;
        trayTwin = twin; lastBottles = -1;
        if (!twin && trayAnchor2 != null) Items.SyncStack(trayAnchor2, "bottle", 0, 1, 1, 0, 0, 0);
    }
    // 고급 두 상자: 한 층 6개씩 A → B → A → B 순서(병 번호 i 의 상자·층)
    static int TwinA(int n) => (n / 12) * 6 + Mathf.Min(n % 12, 6);

    // 칸 표지 외형(단계): 기본 각진 판 / 개량 둥근 판 / 고급 둥근 판 + 금 테. 레벨 업 순간엔 보이는 표지가 솟아오른다
    public void DressShelfMarks(int t, bool anim)
    {
        var c = WorldMap.STN["counter"];
        for (int i = 0; i < 3; i++)
        {
            var mk = shelfMarks[i]; if (mk == null) continue;
            mk.localScale = Vector3.one;
            mk.GetComponent<MeshFilter>().sharedMesh = StationDress.ShelfMarkMesh(t, SHELF_MARK[i], c.w / 3f - 8, c.h - 10);
            mk.GetComponent<MeshRenderer>().sharedMaterial = LevelLook.Mat(t);
            if (anim && mk.gameObject.activeInHierarchy) JuiceFX.I.Rise(mk, 0.15f + 0.1f * i, 6f);
        }
    }

    void Build()
    {
        var root = transform;
        // 얼음 더미: 납작한 얼음판 3장(바닥 서리 원판은 jake 10/25 요청으로 없앰). 8~11은 ice2 구역(열리기 전엔 숨김)
        for (int i = 0; i < ALL_SPOTS; i++)
        {
            var sp = WorldMap.ICE_SPOTS[i];
            var g = new GameObject("spot" + i); g.transform.SetParent(root, false); g.transform.position = WorldMap.W2U(sp);
            spotRoots[i] = g.transform;
            var pile = new GameObject("pile"); pile.transform.SetParent(g.transform, false);
            // 얼음 덩어리(목표 그림처럼 하늘색 큰 덩어리 하나 + 밝은 윗면 + 옆 작은 조각): 예전 흰 판 3장은 소파처럼 보였다
            pile.transform.localRotation = Quaternion.Euler(0, (i * 37) % 24 - 12, 0);
            ToyMesh.Put(pile.transform, "ice", IcePileMesh(i % 3), Items.IceMat, Vector3.zero);
            spotViews[i] = pile.transform; spotShown[i] = true;
        }
        // 해빙기
        var m = WorldMap.STN["melter"];
        melterView = Machine("melter", m, GameConfig.Hex("7f98b8"), 50).transform;
        meltCore = Prim.Sphere(melterView, "core", Local(melterView, m.x + 24, m.y - 4, 56), 16, GameConfig.Hex("9fe0ff"));
        Prim.NoShadow(meltCore);
        meltAnchor = new GameObject("meltQ").transform; meltAnchor.SetParent(melterView, false); meltAnchor.position = WorldMap.W2U(m.x - 22, m.y + 2, 52);
        // 병입기
        var b = WorldMap.STN["bottler"];
        bottlerView = Machine("bottler", b, GameConfig.Hex("8fb0a0"), 50).transform;
        trayAnchor = new GameObject("tray").transform; trayAnchor.SetParent(bottlerView, false); trayAnchor.position = WorldMap.W2U(b.x, b.y, 52);
        // 카운터(선반)
        var c = WorldMap.STN["counter"];
        counterView = Machine("counter", c, GameConfig.Hex("d9b38a"), 40).transform;   // 밝은 나무 몸통 + 크림 상판(목표 그림)
        shelfAnchor = new GameObject("shelf").transform; shelfAnchor.SetParent(counterView, false);
        shelfO2Anchor = new GameObject("shelfO2").transform; shelfO2Anchor.SetParent(counterView, false);
        shelfFoodAnchor = new GameObject("shelfFood").transform; shelfFoodAnchor.SetParent(counterView, false);
        for (int i = 0; i < 3; i++)
        {
            // 칸 받침판: 더미보다 조금 넓은 얇은 판(물 파랑·산소 흰빛·식량 노랑을 한 톤 어둡게), 칸 사이는 상판이 보이게 띄운다
            string kind = GameState.SUPPLY_KINDS[i];
            var mk = Prim.Box(counterView, "mark_" + kind, Local(counterView, SectionX(i), c.y, SHELF_H + MARK_T / 2), new Vector3(c.w / 3f - 8, MARK_T, c.h - 10), SHELF_MARK[i]);
            Prim.NoShadow(mk);
            shelfMarks[i] = mk.transform;
        }
        RefreshShelfLayout(true);
        // 계산대(지폐)
        var r = WorldMap.STN["register"];
        registerView = Machine("register", r, GameConfig.Hex("5b6b8c"), 30).transform;
        Prim.Box(registerView, "screen", Local(registerView, r.x, r.y + 6, 34), new Vector3(24, 14, 3), GameConfig.Hex("c9f0ff"));
        cashAnchor = new GameObject("cash").transform; cashAnchor.SetParent(registerView, false); cashAnchor.position = WorldMap.W2U(r.x + WorldMap.REG_SPOT.x, r.y + WorldMap.REG_SPOT.y, 0);
        // 얼음 드릴 (설치 전 숨김)
        var d = WorldMap.STN["drill"];
        drillView = Machine("drill", d, GameConfig.Hex("a67c52"), 70).transform;
        RefreshZoneGated();
        // Kenney 모델 입히기 (StationDress가 플레이스홀더 "body"/"top"을 숨기고 모델을 붙인다)
        StationDress.Dress(this);
    }

    // ice2 구역·드릴 설치 상태를 시각에 반영 (ZoneManager·PadManager가 호출)
    public void RefreshZoneGated()
    {
        bool ice2 = G.PadLv("ice2") >= 1;
        for (int i = GameConfig.BASE_SPOTS; i < ALL_SPOTS; i++) spotRoots[i].gameObject.SetActive(ice2);
        WorldMap.drillInstalled = G.PadLv("drill") >= 1;
        WorldMap.domeOpen = G.PadLv("dome") >= 1;
        drillView.gameObject.SetActive(WorldMap.drillInstalled);
    }

    // 카운터 3구역 중심 x(논리): 물 왼쪽·산소 가운데·식량 오른쪽
    const float SHELF_H = 42f, MARK_T = 0.7f;   // 상판 윗면 높이·받침판 두께
    static float SectionX(int i) { var c = WorldMap.STN["counter"]; return c.x + (i - 1) * c.w / 3f; }
    Transform AnchorOf(string kind) => kind == "o2" ? shelfO2Anchor : kind == "food" ? shelfFoodAnchor : shelfAnchor;
    // 칸 배치(모형 크기에 맞춘 간격): 나뉘기 전 물 18×3(한 층 54, 상판 가득 한 층) / 나뉜 뒤 칸마다 병·산소통 6×3, 감자 상자 5×3
    int ColsOf(string kind) => !shelfSplit ? 18 : kind == "food" ? 5 : 6;
    float GapOf(string kind) => !shelfSplit ? 9.6f : kind == "food" ? 11.5f : 9.6f;
    int ShelfLayer(string kind) => ColsOf(kind) * 3;

    // 산소·식량 중 하나라도 열리면 카운터를 3구역으로 나눈다. 칸 위치·표지를 다시 맞추고 다음 Update에서 더미를 재배치
    public void RefreshShelfLayout(bool force = false)
    {
        bool split = G.KindOpen("o2") || G.KindOpen("food");
        if (split == shelfSplit && !force) return;
        shelfSplit = split;
        var c = WorldMap.STN["counter"];
        float h = split ? SHELF_H + MARK_T : SHELF_H;   // 나뉘면 받침판 위에 쌓는다
        shelfAnchor.position = WorldMap.W2U(split ? SectionX(0) : c.x, c.y, h);
        shelfO2Anchor.position = WorldMap.W2U(SectionX(1), c.y, h);
        shelfFoodAnchor.position = WorldMap.W2U(SectionX(2), c.y, h);
        for (int i = 0; i < 3; i++) shelfMarks[i].gameObject.SetActive(split && G.KindOpen(GameState.SUPPLY_KINDS[i]));
        shelfO2Anchor.gameObject.SetActive(split); shelfFoodAnchor.gameObject.SetActive(split);
        lastShelf = lastShelfO2 = lastShelfFood = -1;
        if (split && !force) JuiceFX.I.Squash(counterView, 0.5f);
    }

    // 그룹 피벗은 스테이션 중심(JuiceFX 스쿼시·팝이 제자리에서 일어나도록). 자식은 로컬 오프셋 = W2U(목표) - W2U(중심)
    GameObject Machine(string name, WorldMap.Stn s, Color col, float hL)
    {
        var g = new GameObject(name); g.transform.SetParent(transform, false);
        g.transform.position = WorldMap.W2U(s.x, s.y, 0);
        Prim.Box(g.transform, "body", new Vector3(0, hL / 2 * GameConfig.K, 0), new Vector3(s.w, hL, s.h), col);
        // 윗판은 몸체보다 밝은 톤으로 살짝 안쪽에(몸체 색이 테두리로 보이게)
        Prim.Box(g.transform, "top", new Vector3(0, (hL + 1f) * GameConfig.K, 0), new Vector3(s.w - 8, 2, s.h - 8), Color.Lerp(col, Color.white, 0.3f));
        return g;
    }
    static Vector3 Local(Transform view, float x, float y, float h) => WorldMap.W2U(x, y, h) - view.position;

    public Vector3 SpotWorld(int i) => WorldMap.W2U(WorldMap.ICE_SPOTS[i], 6);
    public int SpotCount => G.PadLv("ice2") >= 1 ? ALL_SPOTS : GameConfig.BASE_SPOTS;
    public bool SpotOpen(int i) => i < SpotCount && G.S.spots[i] <= 0;

    void Update()
    {
        var S = G.S; float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 얼음 리스폰
        for (int i = 0; i < SpotCount; i++)
        {
            if (S.spots[i] > 0) S.spots[i] -= dt;
            bool open = S.spots[i] <= 0;
            if (open != spotShown[i])
            {
                spotShown[i] = open; spotViews[i].gameObject.SetActive(open);
                if (open) JuiceFX.I.Pop(spotViews[i], 0.001f, 0.45f);
            }
        }
        // 제빙 (§1): meltQ → water+10
        if (S.meltQ > 0)
        {
            S.meltT += dt;
            if (S.meltT >= G.MeltTime) { S.meltT = 0; S.meltQ--; S.water += 10; JuiceFX.I.Squash(melterView, 0.12f); }
        }
        else S.meltT = 0;
        // 병입: water≥10 & bottles<24 → bottle. 온실 큐가 있으면 온실 몫 물(10)을 남겨 둔다(온실 우선)
        if (S.water >= 10 + (SettleStations.I != null ? SettleStations.I.WaterReserve : 0) && S.bottles < GameConfig.BOTTLE_CAP)
        {
            S.botT += dt;
            if (S.botT >= G.BottleTime) { S.botT = 0; S.water -= 10; S.bottles++; JuiceFX.I.Squash(bottlerView, 0.12f); }
        }
        else S.botT = 0;
        // 제빙 코어 발광 흉내(크기 맥동)
        coreT += dt; meltCore.transform.localScale = Vector3.one * 16 * GameConfig.K * (S.meltQ > 0 ? 1 + Mathf.Sin(coreT * 9f) * 0.08f : 1f);

        // 시각 동기화
        if (S.meltQ != lastMeltQ) { lastMeltQ = S.meltQ; Items.SyncStack(meltAnchor, "ice", S.meltQ, MELT_COLS, MELT_ROWS, Items.Gap("ice").x, Items.Gap("ice").y, GameConfig.MELT_VIS_MAX); }
        if (S.bottles != lastBottles)
        {
            lastBottles = S.bottles;
            if (!trayTwin) Items.SyncStack(trayAnchor, "bottle", S.bottles, TRAY_COLS, TRAY_ROWS, 9.2f, 9.6f, GameConfig.BOTTLE_CAP);
            else
            {
                int n = Mathf.Min(S.bottles, GameConfig.BOTTLE_CAP), na = TwinA(n);
                Items.SyncStack(trayAnchor, "bottle", na, TRAY_COLS, 2, 9.2f, 9.6f, GameConfig.BOTTLE_CAP);
                Items.SyncStack(trayAnchor2, "bottle", n - na, TRAY_COLS, 2, 9.2f, 9.6f, GameConfig.BOTTLE_CAP);
            }
        }
        // 표지는 열린 종류만(식량은 산소보다 늦게 열린다)
        if (shelfSplit && shelfMarks[2].gameObject.activeSelf != G.KindOpen("food")) shelfMarks[2].gameObject.SetActive(G.KindOpen("food"));
        if (S.shelf != lastShelf) { lastShelf = S.shelf; Items.SyncStack(shelfAnchor, "bottle", S.shelf, ColsOf("bottle"), 3, GapOf("bottle"), 8.6f, 36); }
        if (shelfSplit && S.shelfO2 != lastShelfO2) { lastShelfO2 = S.shelfO2; Items.SyncStack(shelfO2Anchor, "o2", S.shelfO2, ColsOf("o2"), 3, GapOf("o2"), 8.6f, 36); }
        if (shelfSplit && S.shelfFood != lastShelfFood) { lastShelfFood = S.shelfFood; Items.SyncStack(shelfFoodAnchor, "food", S.shelfFood, ColsOf("food"), 3, GapOf("food"), 9.2f, 36); }
        if (S.cash != lastCash) { lastCash = S.cash; Items.SyncStack(cashAnchor, "bill", Bills(S.cash), 3, 1, 16, 0, GameConfig.BILL_MAX); }
    }

    public static int Bills(int cash) => Mathf.Min(GameConfig.BILL_MAX, Mathf.CeilToInt(cash / 12f));
    // 병입기 병 상자(동쪽 32×42)에 물병 3×4를 한 층으로 세운다(StationDress.Bottler) · 해빙기 넓은 호퍼에 얼음 4×3 한 층
    public const int TRAY_COLS = 3, TRAY_ROWS = 4, MELT_COLS = 4, MELT_ROWS = 3;
    public Vector3 TrayTop()
    {
        int i = Mathf.Max(0, G.S.bottles - 1);
        if (!trayTwin) return trayAnchor.position + Vector3.up * (i / (TRAY_COLS * TRAY_ROWS) + 1) * Items.ITEM_H["bottle"] * GameConfig.K;
        // 두 상자: 맨 위 병(i)이 든 상자와 층
        var a = i % 12 < 6 ? trayAnchor : trayAnchor2;
        return a.position + Vector3.up * (i / 12 + 1) * Items.ITEM_H["bottle"] * GameConfig.K;
    }
    public Vector3 ShelfTop() => ShelfTop("bottle");
    public Vector3 ShelfTop(string kind) => AnchorOf(kind).position + Vector3.up * (G.ShelfOf(kind) / ShelfLayer(kind) + 1) * Items.ITEM_H[kind] * GameConfig.K;
    // 그 칸으로 날아가는 중인 수
    public int ShelfPendOf(string kind) => kind == "o2" ? shelfPendO2 : kind == "food" ? shelfPendFood : G.shelfPend;
    public void AddShelfPend(string kind, int n) { if (kind == "o2") shelfPendO2 += n; else if (kind == "food") shelfPendFood += n; else G.shelfPend += n; }
    public Vector3 CashTop() => cashAnchor.position + Vector3.up * (Bills(G.S.cash) / 3 + 1) * Items.ITEM_H["bill"] * GameConfig.K;
    public Vector3 MeltTop() => meltAnchor.position + Vector3.up * (Mathf.Min(G.S.meltQ, GameConfig.MELT_VIS_MAX) / (MELT_COLS * MELT_ROWS) + 1) * Items.ITEM_H["ice"] * GameConfig.K;
}

using UnityEngine;

// §1 코어 루프의 고정 스테이션: 얼음 더미 8 · 해빙기 · 병입기 · 카운터(선반) · 계산대(지폐 더미)
public class Stations : MonoBehaviour
{
    public static Stations I;
    GameState G => GameState.I;

    public Transform meltAnchor, trayAnchor, shelfAnchor, cashAnchor;
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
    // 얼음 덩어리(다듬기 3차): 옅은 얼음색 매끈한 몸통 + 흰 윗면 + 앞 윗모서리 흰 빛줄 + 반투명 겉껍질 한 겹(Mats.Glass, 외곽선 마스크에 안 잡힘)
    static readonly Color ICE_SIDE = GameConfig.Hex("8ccaec"), ICE_TOP = GameConfig.Hex("cdebfa"), ICE_EDGE = GameConfig.Hex("ffffff");
    static Material iceShell;
    static Material IceShell => iceShell != null ? iceShell : (iceShell = Mats.Glass(new Color(0.85f, 0.95f, 1f, 0.07f), new Color(0.88f, 0.97f, 1f, 0.42f), 1.8f, 0f));
    void Awake() { I = this; }

    void Start()
    {
        Build();
        G.OnPadLevel += OnPad;
    }
    void OnDestroy() { if (GameState.I != null) GameState.I.OnPadLevel -= OnPad; }
    void OnPad(string id, int lv) { if (id == "o2" || id == "farm") RefreshShelfLayout(); }

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
            float k = GameConfig.K;
            var body = Prim.Box(pile.transform, "a", new Vector3(0, 5f * k, 0), new Vector3(20, 10, 14), ICE_SIDE);
            body.GetComponent<Renderer>().sharedMaterial = Mats.Gloss(ICE_SIDE, 0.8f);
            Prim.Box(pile.transform, "b", new Vector3(0, 10.2f * k, 0.6f * k), new Vector3(18, 0.8f, 12), ICE_TOP);
            Prim.NoShadow(Prim.Box(pile.transform, "edge", new Vector3(0, 10.1f * k, -6.6f * k), new Vector3(18.5f, 1.1f, 1.1f), ICE_EDGE));   // 앞 윗모서리 빛줄(카메라 쪽)
            var shell = Prim.Box(pile.transform, "shell", new Vector3(0, 5.6f * k, 0), new Vector3(22.5f, 11.4f, 16.5f), Color.white);
            shell.GetComponent<Renderer>().sharedMaterial = IceShell; Prim.NoShadow(shell);
            var small = Prim.Box(pile.transform, "c", new Vector3(13f * k, 3f * k, 6f * k), new Vector3(8, 6, 7), ICE_SIDE);
            small.transform.localRotation = Quaternion.Euler(0, 25, 0); small.GetComponent<Renderer>().sharedMaterial = Mats.Gloss(ICE_SIDE, 0.8f);
            Prim.NoShadow(Prim.Box(small.transform, "cTop", new Vector3(0, 0.52f, 0), new Vector3(0.9f, 0.08f, 0.88f) / GameConfig.K, ICE_TOP));   // 부모 스케일 기준(윗면 흰 판)
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
    // 칸 배치: 나뉘기 전 물 12×3(한 층 36) / 나뉜 뒤 칸마다 4×3(한 층 12)
    int ShelfCols => shelfSplit ? 4 : 12;
    int ShelfLayer => ShelfCols * 3;

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
        if (S.meltQ != lastMeltQ) { lastMeltQ = S.meltQ; Items.SyncStack(meltAnchor, "ice", S.meltQ, 2, 1, 16, 0, GameConfig.MELT_VIS_MAX); }
        if (S.bottles != lastBottles) { lastBottles = S.bottles; Items.SyncStack(trayAnchor, "bottle", S.bottles, TRAY_COLS, TRAY_ROWS, 15, 11, GameConfig.BOTTLE_CAP); }
        // 표지는 열린 종류만(식량은 산소보다 늦게 열린다)
        if (shelfSplit && shelfMarks[2].gameObject.activeSelf != G.KindOpen("food")) shelfMarks[2].gameObject.SetActive(G.KindOpen("food"));
        if (S.shelf != lastShelf) { lastShelf = S.shelf; Items.SyncStack(shelfAnchor, "bottle", S.shelf, ShelfCols, 3, 15, 12, 36); }
        if (shelfSplit && S.shelfO2 != lastShelfO2) { lastShelfO2 = S.shelfO2; Items.SyncStack(shelfO2Anchor, "o2", S.shelfO2, 4, 3, 15, 12, 36); }
        if (shelfSplit && S.shelfFood != lastShelfFood) { lastShelfFood = S.shelfFood; Items.SyncStack(shelfFoodAnchor, "food", S.shelfFood, 4, 3, 15, 12, 36); }
        if (S.cash != lastCash) { lastCash = S.cash; Items.SyncStack(cashAnchor, "bill", Bills(S.cash), 3, 1, 16, 0, GameConfig.BILL_MAX); }
    }

    public static int Bills(int cash) => Mathf.Min(GameConfig.BILL_MAX, Mathf.CeilToInt(cash / 12f));
    // 병입기 산출 받침대(동쪽 32×42)에 물통 2×3을 한 층으로 쌓는다(StationDress.Bottler)
    public const int TRAY_COLS = 2, TRAY_ROWS = 3;
    public Vector3 TrayTop() => trayAnchor.position + Vector3.up * (Mathf.Max(0, G.S.bottles - 1) / (TRAY_COLS * TRAY_ROWS) + 1) * Items.ITEM_H["bottle"] * GameConfig.K;
    public Vector3 ShelfTop() => ShelfTop("bottle");
    public Vector3 ShelfTop(string kind) => AnchorOf(kind).position + Vector3.up * (G.ShelfOf(kind) / ShelfLayer + 1) * Items.ITEM_H[kind] * GameConfig.K;
    // 그 칸으로 날아가는 중인 수
    public int ShelfPendOf(string kind) => kind == "o2" ? shelfPendO2 : kind == "food" ? shelfPendFood : G.shelfPend;
    public void AddShelfPend(string kind, int n) { if (kind == "o2") shelfPendO2 += n; else if (kind == "food") shelfPendFood += n; else G.shelfPend += n; }
    public Vector3 CashTop() => cashAnchor.position + Vector3.up * (Bills(G.S.cash) / 3 + 1) * Items.ITEM_H["bill"] * GameConfig.K;
    public Vector3 MeltTop() => meltAnchor.position + Vector3.up * (Mathf.Min(G.S.meltQ, GameConfig.MELT_VIS_MAX) / 2 + 1) * Items.ITEM_H["ice"] * GameConfig.K;
}

using UnityEngine;

// §1 코어 루프의 고정 스테이션: 얼음 더미 8 · 해빙기 · 병입기 · 카운터(선반) · 계산대(지폐 더미)
public class Stations : MonoBehaviour
{
    public static Stations I;
    GameState G => GameState.I;

    public Transform meltAnchor, trayAnchor, shelfAnchor, cashAnchor;
    public Transform melterView, bottlerView, counterView, registerView, drillView;
    public const int ALL_SPOTS = 12;
    Transform[] spotViews = new Transform[ALL_SPOTS];
    Transform[] spotRoots = new Transform[ALL_SPOTS];
    bool[] spotShown = new bool[ALL_SPOTS];
    GameObject meltCore;
    // 수치 라벨(얼음·물·병·선반·+n)은 두지 않는다: 쌓인 모형(얼음·병·지폐 더미)으로 충분 (jake 피드백 10/24)
    int lastMeltQ = -1, lastBottles = -1, lastShelf = -1, lastCash = -1;
    float coreT;

    void Awake() { I = this; }

    void Start()
    {
        Build();
    }

    void Build()
    {
        var root = transform;
        // 얼음 더미: 납작한 얼음판 3장 + 바닥 서리 원판. 8~11은 ice2 구역(열리기 전엔 숨김)
        for (int i = 0; i < ALL_SPOTS; i++)
        {
            var sp = WorldMap.ICE_SPOTS[i];
            var g = new GameObject("spot" + i); g.transform.SetParent(root, false); g.transform.position = WorldMap.W2U(sp);
            spotRoots[i] = g.transform;
            Prim.Cyl(g.transform, "frost", new Vector3(0, 0.02f, 0), 46, 0.6f, GameConfig.Hex("eaf6ff"));
            var pile = new GameObject("pile"); pile.transform.SetParent(g.transform, false);
            Prim.Box(pile.transform, "a", new Vector3(0, 1.8f * GameConfig.K, 0), new Vector3(26, 3.6f, 18), GameConfig.Hex("a9dcf5"));
            Prim.Box(pile.transform, "b", new Vector3(2f * GameConfig.K, 5.4f * GameConfig.K, -1.5f * GameConfig.K), new Vector3(22, 3.6f, 16), GameConfig.ICE);
            Prim.Box(pile.transform, "c", new Vector3(-1.5f * GameConfig.K, 9f * GameConfig.K, 1.5f * GameConfig.K), new Vector3(18, 3.6f, 13), GameConfig.Hex("e4f6ff"));
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
        counterView = Machine("counter", c, GameConfig.Hex("d89a62"), 40).transform;
        shelfAnchor = new GameObject("shelf").transform; shelfAnchor.SetParent(counterView, false); shelfAnchor.position = WorldMap.W2U(c.x, c.y, 42);
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
            if (S.meltT >= G.MeltTime) { S.meltT = 0; S.meltQ--; S.water += 10; JuiceFX.I.Squash(melterView, 0.5f); }
        }
        else S.meltT = 0;
        // 병입: water≥10 & bottles<24 → bottle
        if (S.water >= 10 && S.bottles < GameConfig.BOTTLE_CAP)
        {
            S.botT += dt;
            if (S.botT >= G.BottleTime) { S.botT = 0; S.water -= 10; S.bottles++; JuiceFX.I.Squash(bottlerView, 0.4f); }
        }
        else S.botT = 0;
        // 제빙 코어 발광 흉내(크기 맥동)
        coreT += dt; meltCore.transform.localScale = Vector3.one * 16 * GameConfig.K * (S.meltQ > 0 ? 1 + Mathf.Sin(coreT * 9f) * 0.08f : 1f);

        // 시각 동기화
        if (S.meltQ != lastMeltQ) { lastMeltQ = S.meltQ; Items.SyncStack(meltAnchor, "ice", S.meltQ, 2, 1, 16, 0, GameConfig.MELT_VIS_MAX); }
        if (S.bottles != lastBottles) { lastBottles = S.bottles; Items.SyncStack(trayAnchor, "bottle", S.bottles, 6, 4, 10, 10, GameConfig.BOTTLE_CAP); }
        if (S.shelf != lastShelf) { lastShelf = S.shelf; Items.SyncStack(shelfAnchor, "bottle", S.shelf, 12, 3, 15, 12, 36); }
        if (S.cash != lastCash) { lastCash = S.cash; Items.SyncStack(cashAnchor, "bill", Bills(S.cash), 3, 1, 16, 0, GameConfig.BILL_MAX); }
    }

    public static int Bills(int cash) => Mathf.Min(GameConfig.BILL_MAX, Mathf.CeilToInt(cash / 12f));
    public Vector3 TrayTop() => trayAnchor.position + Vector3.up * (Mathf.Max(0, G.S.bottles - 1) / 24 + 1) * Items.ITEM_H["bottle"] * GameConfig.K;
    public Vector3 ShelfTop() => shelfAnchor.position + Vector3.up * (G.S.shelf / 36 + 1) * Items.ITEM_H["bottle"] * GameConfig.K;
    public Vector3 CashTop() => cashAnchor.position + Vector3.up * (Bills(G.S.cash) / 3 + 1) * Items.ITEM_H["bill"] * GameConfig.K;
    public Vector3 MeltTop() => meltAnchor.position + Vector3.up * (Mathf.Min(G.S.meltQ, GameConfig.MELT_VIS_MAX) / 2 + 1) * Items.ITEM_H["ice"] * GameConfig.K;
}

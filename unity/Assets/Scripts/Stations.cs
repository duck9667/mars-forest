using UnityEngine;

// §1 코어 루프의 고정 스테이션: 얼음 더미 8 · 제빙기 · 병입기 · 카운터(선반) · 계산대(지폐 더미)
public class Stations : MonoBehaviour
{
    public static Stations I;
    GameState G => GameState.I;

    public Transform meltAnchor, trayAnchor, shelfAnchor, cashAnchor;
    public Transform melterView, bottlerView, counterView, registerView;
    Transform[] spotViews = new Transform[GameConfig.BASE_SPOTS];
    bool[] spotShown = new bool[GameConfig.BASE_SPOTS];
    GameObject meltCore;
    WorldLabels.Label meltLbl, waterLbl, trayLbl, shelfLbl, cashLbl;
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
        // 얼음 더미: 납작한 얼음판 3장 + 바닥 서리 원판
        for (int i = 0; i < GameConfig.BASE_SPOTS; i++)
        {
            var sp = WorldMap.ICE_SPOTS[i];
            var g = new GameObject("spot" + i); g.transform.SetParent(root, false); g.transform.position = WorldMap.W2U(sp);
            Prim.Cyl(g.transform, "frost", new Vector3(0, 0.02f, 0), 46, 0.6f, GameConfig.Hex("eaf6ff"));
            var pile = new GameObject("pile"); pile.transform.SetParent(g.transform, false);
            Prim.Box(pile.transform, "a", new Vector3(0, 1.4f * GameConfig.K, 0), new Vector3(18, 2.8f, 13), GameConfig.ICE);
            Prim.Box(pile.transform, "b", new Vector3(1.5f * GameConfig.K, 4.2f * GameConfig.K, -1f * GameConfig.K), new Vector3(16, 2.8f, 12), GameConfig.ICE);
            Prim.Box(pile.transform, "c", new Vector3(-1f * GameConfig.K, 7f * GameConfig.K, 1f * GameConfig.K), new Vector3(14, 2.8f, 11), GameConfig.Hex("e4f6ff"));
            spotViews[i] = pile.transform; spotShown[i] = true;
        }
        // 제빙기
        var m = WorldMap.STN["melter"];
        melterView = Machine("melter", m, GameConfig.Hex("7f98b8"), 50).transform;
        meltCore = Prim.Sphere(melterView, "core", WorldMap.W2U(m.x + 24, m.y - 4, 36), 16, GameConfig.Hex("9fe0ff"));
        Prim.NoShadow(meltCore);
        meltAnchor = new GameObject("meltQ").transform; meltAnchor.SetParent(melterView, false); meltAnchor.position = WorldMap.W2U(m.x - 22, m.y + 2, 50);
        meltLbl = WorldLabels.I.Add(() => WorldMap.W2U(m.x - 22, m.y - 10, 70), "", 22, Color.white, Vector2.zero);
        waterLbl = WorldLabels.I.Add(() => WorldMap.W2U(m.x + 24, m.y - 10, 70), "", 22, GameConfig.Hex("bfe9ff"), Vector2.zero);
        // 병입기
        var b = WorldMap.STN["bottler"];
        bottlerView = Machine("bottler", b, GameConfig.Hex("8fb0a0"), 50).transform;
        trayAnchor = new GameObject("tray").transform; trayAnchor.SetParent(bottlerView, false); trayAnchor.position = WorldMap.W2U(b.x, b.y, 50);
        trayLbl = WorldLabels.I.Add(() => WorldMap.W2U(b.x, b.y - 14, 72), "", 22, Color.white, Vector2.zero);
        // 카운터(선반)
        var c = WorldMap.STN["counter"];
        counterView = Machine("counter", c, GameConfig.Hex("d89a62"), 40).transform;
        shelfAnchor = new GameObject("shelf").transform; shelfAnchor.SetParent(counterView, false); shelfAnchor.position = WorldMap.W2U(c.x, c.y, 40);
        shelfLbl = WorldLabels.I.Add(() => WorldMap.W2U(c.x - 80, c.y - 10, 62), "", 22, Color.white, Vector2.zero);
        // 계산대(지폐)
        var r = WorldMap.STN["register"];
        registerView = Machine("register", r, GameConfig.Hex("5b6b8c"), 30).transform;
        Prim.Box(registerView, "screen", WorldMap.W2U(r.x, r.y + 6, 34), new Vector3(24, 14, 3), GameConfig.Hex("c9f0ff"));
        cashAnchor = new GameObject("cash").transform; cashAnchor.SetParent(registerView, false); cashAnchor.position = WorldMap.W2U(r.x + WorldMap.REG_SPOT.x, r.y + WorldMap.REG_SPOT.y, 0);
        cashLbl = WorldLabels.I.Add(() => cashAnchor.position + Vector3.up * 1.2f, "", 24, GameConfig.GOLD, Vector2.zero);
    }

    GameObject Machine(string name, WorldMap.Stn s, Color col, float hL)
    {
        var g = new GameObject(name); g.transform.SetParent(transform, false);
        Prim.Box(g.transform, "body", WorldMap.W2U(s.x, s.y, hL / 2), new Vector3(s.w, hL, s.h), col);
        Prim.Box(g.transform, "trim", WorldMap.W2U(s.x, s.y, hL + 1.5f), new Vector3(s.w + 4, 3, s.h + 4), GameConfig.Hex("3a3f5a"));
        return g;
    }

    public Vector3 SpotWorld(int i) => WorldMap.W2U(WorldMap.ICE_SPOTS[i], 6);
    public bool SpotOpen(int i) => i < GameConfig.BASE_SPOTS && G.S.spots[i] <= 0;

    void Update()
    {
        var S = G.S; float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 얼음 리스폰
        for (int i = 0; i < GameConfig.BASE_SPOTS; i++)
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
        meltLbl.Set(S.meltQ > 0 ? "얼음 " + S.meltQ : "");
        waterLbl.Set(S.water > 0 ? "물 " + S.water : "");
        trayLbl.Set("병 " + S.bottles + "/" + GameConfig.BOTTLE_CAP);
        shelfLbl.Set("선반 " + S.shelf + "/" + G.ShelfCap);
        cashLbl.Set(S.cash > 0 ? "+" + S.cash : "");
    }

    public static int Bills(int cash) => Mathf.Min(GameConfig.BILL_MAX, Mathf.CeilToInt(cash / 12f));
    public Vector3 TrayTop() => trayAnchor.position + Vector3.up * (Mathf.Max(0, G.S.bottles - 1) / 24 + 1) * Items.ITEM_H["bottle"] * GameConfig.K;
    public Vector3 ShelfTop() => shelfAnchor.position + Vector3.up * (G.S.shelf / 36 + 1) * Items.ITEM_H["bottle"] * GameConfig.K;
    public Vector3 CashTop() => cashAnchor.position + Vector3.up * (Bills(G.S.cash) / 3 + 1) * Items.ITEM_H["bill"] * GameConfig.K;
    public Vector3 MeltTop() => meltAnchor.position + Vector3.up * (Mathf.Min(G.S.meltQ, GameConfig.MELT_VIS_MAX) / 2 + 1) * Items.ITEM_H["ice"] * GameConfig.K;
}

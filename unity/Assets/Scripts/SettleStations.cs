using System.Collections.Generic;
using UnityEngine;

// 생존 기지 생산 체인(docs/settlement-design.md): 레골리스 더미 8 · 전기분해기(물통→산소통) · 토양 정화기(레골리스→흙) · 비료 배합기(흙→비료) · 온실(비료+물 10→식량)
// 시설마다 입력 큐(…Q, 상한 40)·출력 더미(…Out, 상한 24)·타이머(…T). 구역이 열리기 전엔 모형·더미 모두 숨김(구역 = 발판 id)
public class SettleStations : MonoBehaviour
{
    public static SettleStations I;
    GameState G => GameState.I;
    SaveData S => G.S;

    // 시설 표: 0 전기분해기 · 1 토양 정화기 · 2 비료 배합기 · 3 온실
    public static readonly string[] KEYS = { "elec", "purif", "mixer", "green" };
    public static readonly string[] IN_K = { "bottle", "rego", "soil", "fert" };
    public static readonly string[] OUT_K = { "o2", "soil", "fert", "food" };
    static readonly float[] TIMES = { GameConfig.ELEC_T, GameConfig.PURIF_T, GameConfig.MIXER_T, GameConfig.GREEN_T };
    public const float R_REGO = 40f, CD_REGO = 0.09f, R_FAC = 40f, CD_PUT = 0.06f, CD_TAKE = 0.13f;
    public const int GREEN_WATER = 10;
    const int IN_VIS = 18;   // 입력 큐 시각 상한(3열×2줄×3층)

    public readonly Transform[] views = new Transform[4];
    readonly Transform[] inA = new Transform[4], outA = new Transform[4];
    readonly int[] lastIn = { -1, -1, -1, -1 }, lastOut = { -1, -1, -1, -1 };
    public readonly int[] qPend = new int[4];   // 넣는 중(비행 중) 원료 수
    readonly bool[] facShown = new bool[4];
    readonly Transform[] rRoots = new Transform[GameConfig.REGO_SPOTS], rViews = new Transform[GameConfig.REGO_SPOTS];
    readonly bool[] rShown = new bool[GameConfig.REGO_SPOTS];
    bool mineShown;
    // 움직이는 부품: 전해조 거품 · 세척 드럼 · 배합 날개 · 작물
    readonly List<Transform> bubbles = new List<Transform>(), crops = new List<Transform>();
    Transform drum, paddle;
    float animT, drumA, paddleA;

    void Awake() { I = this; }

    void Start()
    {
        Build();
        Refresh(false);
        G.OnPadLevel += OnPad;
    }
    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPad; }
    void OnPad(string id, int lv) { if (id == "o2" || id == "mine" || id == "soil" || id == "farm") Refresh(true); }

    // ---- 상태 접근 (저장 필드는 시설별 이름이라 번호로 묶는다) ----
    public ref int Q(int i) { switch (i) { case 0: return ref S.elecQ; case 1: return ref S.purQ; case 2: return ref S.mixQ; default: return ref S.grnQ; } }
    public ref int Out(int i) { switch (i) { case 0: return ref S.elecOut; case 1: return ref S.purOut; case 2: return ref S.mixOut; default: return ref S.grnOut; } }
    ref float T(int i) { switch (i) { case 0: return ref S.elecT; case 1: return ref S.purT; case 2: return ref S.mixT; default: return ref S.grnT; } }
    static string ZonePad(string zone) { foreach (var z in WorldMap.ZONES) if (z.key == zone) return z.pad; return zone; }
    public bool Open(int i) => G.PadLv(ZonePad(WorldMap.STN_ZONE[KEYS[i]])) >= 1;
    public bool MineOpen => G.PadLv(ZonePad("mine")) >= 1;
    public static int Index(string key) => System.Array.IndexOf(KEYS, key);

    // 시설이 지금 못 돈다(출력 가득·온실 물 부족): 이때는 큐 상한을 넘겨서라도 받아 준다(등짐이 안 빠져 꼼짝 못 하는 일 방지)
    bool Stalled(int i) => Out(i) >= GameConfig.SETTLE_OUT_CAP || (i == 3 && S.water < GREEN_WATER);
    public bool CanPut(int i) => Open(i) && (Q(i) + qPend[i] < GameConfig.SETTLE_Q_CAP || Stalled(i));
    public void Put(int i) { qPend[i] = Mathf.Max(0, qPend[i] - 1); Q(i)++; }
    public bool Near(int i, Vector2 p) => Open(i) && WorldMap.NearStn(p, KEYS[i], R_FAC);
    // 갈 곳이 있는 것만 집는다: 비료는 온실이 열려야(그 전엔 배합기에 쌓이기만 = 비료 생산 누계), 레골리스는 정화기가 있어야
    public bool CanTake(int i) => Out(i) > 0 && (i != 2 || Open(3));
    // 온실 큐가 있으면 병입기보다 물을 먼저 가져간다: 병입기는 이만큼을 남겨 둔다 (Stations 병입 조건)
    public int WaterReserve => Open(3) && S.grnQ > 0 && S.grnOut < GameConfig.SETTLE_OUT_CAP ? GREEN_WATER : 0;

    // 힌트·봇 타깃 점(상호작용 반경 안). 정화기·배합기는 둘 사이 틈(정화기 남쪽·배합기 북쪽), 나머지는 남쪽 가장자리 + 18
    public static Vector2 Approach(int i)
    {
        var s = WorldMap.STN[KEYS[i]];
        if (i == 1) return new Vector2(s.x - 12, s.y + s.h / 2 + 16);
        if (i == 2) return new Vector2(s.x + 8, s.y - s.h / 2 - 16);
        return new Vector2(s.x, s.y + s.h / 2 + 18);
    }

    // ---- 레골리스 더미 ----
    public Vector3 RegoWorld(int i) => WorldMap.W2U(WorldMap.REGO_SPOTS[i], 6);
    public bool RegoOpen(int i) => MineOpen && Open(1) && S.rspots[i] <= 0;
    public bool AnyRego { get { for (int i = 0; i < GameConfig.REGO_SPOTS; i++) if (RegoOpen(i)) return true; return false; } }
    public int NearestRego(Vector2 p, float r)
    {
        int best = -1; float bd = r;
        for (int i = 0; i < GameConfig.REGO_SPOTS; i++)
        {
            if (!RegoOpen(i)) continue;
            float d = Vector2.Distance(p, WorldMap.REGO_SPOTS[i]);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    // 비행 도착·출발점: 입력 큐 다음 자리 / 출력 더미 맨 위
    public Vector3 InTop(int i) => inA[i].position + Vector3.up * (Mathf.Min(Q(i) + qPend[i], IN_VIS) / 6 + 0.5f) * Items.ITEM_H[IN_K[i]] * GameConfig.K;
    public Vector3 OutTop(int i) => outA[i].position + Vector3.up * (Mathf.Max(0, Mathf.Min(Out(i), GameConfig.SETTLE_OUT_CAP) - 1) / 6 + 1) * Items.ITEM_H[OUT_K[i]] * GameConfig.K;

    void Refresh(bool pop)
    {
        bool mine = MineOpen;
        if (mine != mineShown)
        {
            mineShown = mine;
            for (int i = 0; i < rRoots.Length; i++) { rRoots[i].gameObject.SetActive(mine); if (mine && pop) JuiceFX.I.Pop(rRoots[i], 0.001f, 0.5f); }
        }
        for (int i = 0; i < 4; i++)
        {
            bool open = Open(i);
            if (open == facShown[i]) continue;
            facShown[i] = open; views[i].gameObject.SetActive(open);
            if (open && pop) { JuiceFX.I.Pop(views[i], 0.001f, 0.6f); JuiceFX.I.Dust(views[i].position + Vector3.up * 0.2f, 6); }
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 레골리스 리스폰 (얼음 더미와 같은 GameConfig.SpotRespawn, 줍기는 Player)
        for (int i = 0; i < GameConfig.REGO_SPOTS; i++)
        {
            if (S.rspots[i] > 0) S.rspots[i] -= dt;
            bool open = S.rspots[i] <= 0;
            if (open != rShown[i])
            {
                rShown[i] = open; rViews[i].gameObject.SetActive(open);
                if (open && mineShown) JuiceFX.I.Pop(rViews[i], 0.001f, 0.45f);
            }
        }
        // 생산: 큐 1개 → 출력 1개. 온실은 물 10을 함께 쓴다. 배합기는 비료 생산 누계(S.fertMade)를 올린다
        for (int i = 0; i < 4; i++)
        {
            ref float t = ref T(i);
            if (!Open(i)) { t = 0; continue; }
            bool can = Q(i) > 0 && Out(i) < GameConfig.SETTLE_OUT_CAP && (i != 3 || S.water >= GREEN_WATER);
            if (!can) { t = 0; continue; }
            t += dt * G.MachineSpeedMul;   // 공용 기계 속도 배율(본부 Lv4·태양광 전력)
            if (t >= TIMES[i])
            {
                t = 0; Q(i)--; Out(i)++;
                if (i == 3) S.water -= GREEN_WATER;
                if (i == 2) S.fertMade++;
                JuiceFX.I.Squash(views[i], 0.4f);
            }
        }
        Animate(dt);
        // 시각 동기화
        for (int i = 0; i < 4; i++)
        {
            int q = Q(i), o = Out(i);
            // 간격은 모형 크기(Items.Gap)에 맞춘다: 쟁반 44×24·48×28 안에 3×2
            if (q != lastIn[i]) { lastIn[i] = q; var gi = Items.Gap(IN_K[i]); Items.SyncStack(inA[i], IN_K[i], q, 3, 2, gi.x + 1f, gi.y + 0.5f, IN_VIS); }
            if (o != lastOut[i]) { lastOut[i] = o; var go = Items.Gap(OUT_K[i]); Items.SyncStack(outA[i], OUT_K[i], o, 3, 2, go.x + 1.5f, go.y + 1f, GameConfig.SETTLE_OUT_CAP); }
        }
    }

    bool Working(int i) => facShown[i] && T(i) > 0;

    void Animate(float dt)
    {
        animT += dt; float k = GameConfig.K;
        // 전해조: 일할 때만 전극 둘레에서 거품이 올라와 수면에서 꺼진다
        bool e = Working(0);
        for (int i = 0; i < bubbles.Count; i++)
        {
            var b = bubbles[i]; b.gameObject.SetActive(e);
            if (!e) continue;
            float p = Mathf.Repeat(animT * 0.9f + i / (float)bubbles.Count, 1f);
            var lp = b.localPosition; lp.y = (10f + p * 9f) * k; b.localPosition = lp;
            b.localScale = Vector3.one * (2f + p * 1.6f) * (p > 0.85f ? (1f - p) / 0.15f : 1f) * k;
        }
        // 세척 드럼 회전·배합 날개 회전
        if (Working(1)) drumA += 140f * dt;
        if (drum) drum.localRotation = Quaternion.Euler(0, 0, drumA);   // 남북 축 드럼: 앞면 날개가 돈다
        if (Working(2)) paddleA += 260f * dt;
        if (paddle) paddle.localRotation = Quaternion.Euler(0, paddleA, 0);
        // 작물: 큐가 있으면 진행도만큼 자라고 살랑, 없으면 어린 싹
        float grow = S.grnQ > 0 || S.grnT > 0 ? 0.65f + 0.35f * Mathf.Clamp01(S.grnT / GameConfig.GREEN_T) : 0.6f;
        for (int i = 0; i < crops.Count; i++)
        {
            crops[i].localScale = Vector3.one * grow;
            crops[i].localRotation = Quaternion.Euler(Mathf.Sin(animT * 2.2f + i) * 5f, 0, Mathf.Cos(animT * 1.7f + i * 0.7f) * 5f);
        }
    }

    // ================= 모형 =================
    static Vector3 L(float dx, float dy, float h) => new Vector3(dx, h, -dy) * GameConfig.K;   // 시설 중심 기준 논리 오프셋(dy+ = 남쪽) → 로컬
    static readonly Color STEEL = ModelLib.C(ModelLib.COL.steel), STEEL_D = ModelLib.C(ModelLib.COL.steelD), WHITE = ModelLib.C(ModelLib.COL.white),
        DARK = ModelLib.C(ModelLib.COL.dark), WATER = GameConfig.Hex("6fc3f0"), PIPE = GameConfig.Hex("4fb0f0");
    const float PLATE = 4f, TRAY = 1.8f;

    void Build()
    {
        // 레골리스 더미(fb2 "돌 디자인이 엉성하다"): 둥글고 덩어리감 있는 저폴리 붉은 돌무더기(큰 돌 + 중간 돌 + 작은 돌, 3톤) 메시 하나.
        //  얼음 더미와 같은 규칙: 주우면 사라졌다 GameConfig.SpotRespawn() 뒤 다시 솟음. 목표 그림 docs/art/target_north_v1.png
        for (int i = 0; i < GameConfig.REGO_SPOTS; i++)
        {
            var g = new GameObject("rego" + i).transform; g.SetParent(transform, false); g.position = WorldMap.W2U(WorldMap.REGO_SPOTS[i]);
            rRoots[i] = g;
            var pile = new GameObject("pile").transform; pile.SetParent(g, false);
            pile.localRotation = Quaternion.Euler(0, i * 47f, 0);
            ToyMesh.Put(pile, "rego", RegoPileMesh(i % 3), ToyMesh.Mat(0.08f), Vector3.zero);
            rViews[i] = pile; rShown[i] = true;
            g.gameObject.SetActive(false);
        }
        BuildElec(); BuildPurif(); BuildMixer(); BuildGreen();
        // 목표 그림(docs/art/target_north_v1.png)처럼 기계가 구역을 채우게: 키만 1.25배(충돌 발자국 그대로), 온실은 전체 1.2배
        for (int i = 0; i < 3; i++) Grow(i, new Vector3(1f, 1.25f, 1f));
        Grow(3, new Vector3(1.2f, 1.2f, 1.2f));
        for (int i = 0; i < 4; i++) views[i].gameObject.SetActive(false);
    }

    static Mesh RegoPileMesh(int v) => ToyMesh.Cached("regoPile" + v, m =>
    {
        Color T = Items.REGO_T, M = Items.REGO_M, A = Items.REGO_A, D = Items.REGO_D;
        m.At(new Vector3(0, 3.4f, 0), new Vector3(0, v * 50, 0)).Blob(new Vector3(11f, 6.4f, 9f), 1, 81 + v, 0.24f, T, M, D, A, 0.5f);
        m.At(new Vector3(10.5f, 2.6f, -5.5f), new Vector3(0, 20 + v * 30, 8)).Blob(new Vector3(5.6f, 4.2f, 5f), 0, 91 + v, 0.22f, T, A, D, M, 0.45f);
        m.At(new Vector3(-10f, 1.6f, -6.5f), new Vector3(0, -30, 0)).Blob(new Vector3(3.8f, 2.8f, 3.4f), 0, 97 + v, 0.22f, T, M, D);
        m.At(new Vector3(-2f, 8.4f, 1.5f), new Vector3(10, 40 + v * 20, 0)).Blob(new Vector3(4.6f, 3f, 4f), 0, 103 + v, 0.2f, T, A, D);
        return m;
    });

    void Grow(int i, Vector3 s)
    {
        views[i].localScale = s;
        var inv = new Vector3(1f / s.x, 1f / s.y, 1f / s.z); inA[i].localScale = inv; outA[i].localScale = inv;
    }

    // 시설 뷰: 피벗은 시설 중심(JuiceFX 스쿼시·팝이 제자리), 바닥판 steelD + 입력·출력 앵커
    Transform View(int i, Vector2 inAt, Vector2 outAt)
    {
        var s = WorldMap.STN[KEYS[i]];
        var v = new GameObject(KEYS[i]).transform; v.SetParent(transform, false); v.position = WorldMap.W2U(s.x, s.y, 0);
        float gs = i == 3 ? 1.2f : 1f;   // 온실은 뷰를 1.2배 키우므로(Grow) 판은 설계 크기로
        // 받침(다듬기 3차): 둥근 모서리 콘크리트 판 + 한 단 밝은 윗판(목표 그림 docs/art/target_north_v1.png). 예전 직각 판·어두운 입력 쟁반은 네모 블록으로 읽혔다
        MachineKit.RoundSlab(v, "plate", 0, 0, 0, s.w / gs, s.h / gs, PLATE, 12, MachineKit.PAD);
        MachineKit.RoundSlab(v, "plateTop", 0, 0, PLATE, s.w / gs - 6, s.h / gs - 6, 0.6f, 9, MachineKit.PAD_TOP);
        inA[i] = new GameObject("in").transform; inA[i].SetParent(v, false); inA[i].localPosition = L(inAt.x, inAt.y, PLATE + TRAY);
        outA[i] = new GameObject("out").transform; outA[i].SetParent(v, false); outA[i].localPosition = L(outAt.x, outAt.y, PLATE + TRAY);
        // 받침 쟁반(둥근 판, 두께 TRAY): 입력은 옅은 회색, 출력은 금색 테 + 크림 안쪽(안쪽을 살짝 높여 테가 턱으로 보이게)
        MachineKit.RoundSlab(v, "inTray", inAt.x, inAt.y, PLATE, 44, 24, TRAY, 7, GameConfig.Hex("c4bcb0"));
        MachineKit.RoundSlab(v, "outTray", outAt.x, outAt.y, PLATE, 48, 28, TRAY - 0.3f, 9, GameConfig.GOLD);
        MachineKit.RoundSlab(v, "outTrayIn", outAt.x, outAt.y, PLATE, 42, 22, TRAY, 7, GameConfig.Hex("fff4d6"));
        views[i] = v;
        return v;
    }

    // 전기분해기: 뒤에 발전기, 가운데 물 담긴 전해조 + 전극 두 개(＋빨강·－검정)와 거품, 양옆 기체 탱크 2개(산소 파랑 띠·수소 주황 띠)
    void BuildElec()
    {
        var v = View(0, new Vector2(-24, 20), new Vector2(24, 20));
        ModelLib.Put(v, "gen", 22, 22, 22, 0, PLATE, -22, 0f, ModelLib.SK(ModelLib.COL.orange));
        Prim.Box(v, "tub", L(0, -4, PLATE + 6), new Vector3(36, 12, 20), GameConfig.Hex("dfe8ef"));
        Prim.Box(v, "water", L(0, -4, PLATE + 12.2f), new Vector3(32, 1, 16), WATER);
        foreach (var (x, cap) in new[] { (-8f, GameConfig.Hex("e04848")), (8f, DARK) })
        {
            Prim.Cyl(v, "rod", L(x, -4, PLATE + 14), 3, 14, GameConfig.Hex("8c98a4"));
            Prim.Box(v, "cap", L(x, -4, PLATE + 22), new Vector3(5, 3, 5), cap);
            Prim.Box(v, "wire", L(x, -12, PLATE + 23), new Vector3(1.4f, 1.4f, 16), cap);
        }
        for (int j = 0; j < 6; j++)
        {
            var b = Prim.Sphere(v, "bub", L((j % 2 == 0 ? -8 : 8) + (j % 3 - 1) * 2.5f, -4 + (j % 3 - 1) * 3, PLATE + 12), 2.5f, Color.white);
            Prim.NoShadow(b); bubbles.Add(b.transform);
        }
        // 탱크: 흰 원통 + 색 띠 + 둥근 뚜껑, 전해조에서 파이프
        foreach (var (x, band) in new[] { (-38f, GameConfig.Hex("4fb0f0")), (38f, ModelLib.C(ModelLib.COL.orange)) })
        {
            Prim.Cyl(v, "tank", L(x, -10, PLATE + 16), 17, 32, WHITE);
            Prim.Sphere(v, "dome", L(x, -10, PLATE + 32), 17, WHITE);
            Prim.Cyl(v, "band", L(x, -10, PLATE + 20), 18, 5, band);
            Prim.Box(v, "pipe", L(x * 0.66f, -6, PLATE + 9), new Vector3(16, 2.5f, 2.5f), PIPE);
        }
    }

    // 토양 정화기(다듬기 3차, 목표 그림 docs/art/target_north_v1.png): 왼쪽 둥근 깔때기 호퍼(붉은 레골리스) → 둥근 받침 몸통 위 세척 드럼.
    // 드럼은 카메라 쪽(남)으로 동그란 앞면이 보이게 남북 축으로 눕히고, 앞면 둥근 창 + 날개 3개가 돌아 일하는 게 보인다. 위로 물 관 아치 하나
    void BuildPurif()
    {
        var v = View(1, new Vector2(-26, 16), new Vector2(26, 16));
        // 호퍼: 다리 둘(원기둥) + 위가 넓은 원통 층(배합기 깔때기와 같은 문법) + 윗테 파랑 + 레골리스
        foreach (float lx in new[] { -38f, -24f }) Prim.Cyl(v, "leg", L(lx, -10, PLATE + 11), 3f, 22, STEEL_D);
        float[] hd = { 24f, 20f, 15f }; float[] hh = { 31f, 27f, 23.5f };
        for (int f = 0; f < hd.Length; f++) Prim.Cyl(v, "hopper", L(-31, -10, PLATE + hh[f]), hd[f], 4.4f, MachineKit.SHELL);
        Prim.Cyl(v, "rim", L(-31, -10, PLATE + 33.4f), 26, 1.4f, PIPE);
        Prim.Cyl(v, "rego", L(-31, -10, PLATE + 33.8f), 21, 1f, GameConfig.REGO);
        Prim.Box(v, "chute", L(-18, -10, PLATE + 22), new Vector3(10, 3, 7), MachineKit.SHELL_D).transform.localRotation = Quaternion.Euler(0, 0, -25);
        // 몸통: 둥근 모서리 받침 + 드럼 요람(양옆 둥근 기둥)
        MachineKit.RoundSlab(v, "body", 10, -10, PLATE, 44, 30, 10, 9, MachineKit.SHELL_D);
        foreach (float sx in new[] { -10f, 30f }) Prim.Cyl(v, "cradle", L(sx, -10, PLATE + 16), 7, 12, MachineKit.SHELL);
        // 드럼(남북 축): 몸통 위에 누워 앞면이 카메라를 본다
        drum = new GameObject("drum").transform; drum.SetParent(v, false); drum.localPosition = L(10, -10, PLATE + 26);
        Prim.Cyl(drum, "shell", Vector3.zero, 32, 26, MachineKit.SHELL).transform.localRotation = Quaternion.Euler(90, 0, 0);
        foreach (float by in new[] { -11f, 11f }) Prim.Cyl(drum, "hoop", new Vector3(0, 0, -by * GameConfig.K), 34, 3, PIPE).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim.Cyl(drum, "window", new Vector3(0, 0, -13.2f * GameConfig.K), 20, 1, GameConfig.Hex("7a4430")).transform.localRotation = Quaternion.Euler(90, 0, 0);   // 둥근 창 너머 붉은 흙
        MachineKit.Tube(drum, "ring", new Vector3(0, 0, -14.4f * GameConfig.K), 23, 1.6f, MachineKit.SHELL_D).transform.localRotation = Quaternion.Euler(90, 0, 0);   // 창틀(속 빈 고리)
        // 창 안에서 구르는 흙덩이 3개(드럼과 같이 돌아 일하는 게 보인다). 예전 날개 3개는 앞면 로고처럼 읽혔다
        for (int j = 0; j < 3; j++)
        {
            float a = j * 120f * Mathf.Deg2Rad;
            var ch = Prim.Box(drum, "clump", new Vector3(Mathf.Cos(a) * 4.5f, Mathf.Sin(a) * 4.5f, -13.6f) * GameConfig.K, new Vector3(4.5f, 4.5f, 1.2f), j == 1 ? GameConfig.Hex("8f3d20") : GameConfig.REGO);
            ch.transform.localRotation = Quaternion.Euler(0, 0, j * 37f);
        }
        // 물 관: 뒤쪽에서 솟아 드럼 위로 건너온다(아치 하나만)
        MachineKit.PipeArch(v, 38, 10, -16, PLATE, PLATE + 41, PLATE + 50, 3f, PIPE);
    }

    // 비료 배합기: 다리 넷 위 계단식 깔때기 호퍼(흙이 찬 윗면) → 아래 배합 통의 돌아가는 날개, 왼쪽 퇴비 더미·드럼통
    void BuildMixer()
    {
        var v = View(2, new Vector2(-26, 16), new Vector2(26, 16));
        foreach (var (lx, ly) in new[] { (2f, -20f), (26f, -20f), (2f, 0f), (26f, 0f) }) Prim.Cyl(v, "leg", L(lx, ly, PLATE + 16), 3f, 32, STEEL_D);
        // 원형 깔때기(목표 그림): 위가 넓은 원통 층을 쌓아 원뿔처럼, 윗테는 파랑
        float[] fd = { 34f, 30f, 25f, 19f, 13f }; float[] fh = { 44f, 39f, 34f, 29f, 24.5f };
        for (int f = 0; f < fd.Length; f++) Prim.Cyl(v, "funnel", L(14, -10, PLATE + fh[f]), fd[f], 5.2f, MachineKit.SHELL);
        Prim.Cyl(v, "rim", L(14, -10, PLATE + 46.8f), 36, 1.6f, PIPE);
        Prim.Cyl(v, "fill", L(14, -10, PLATE + 47.2f), 30, 1.2f, GameConfig.SOIL);
        var heap = Prim.Sphere(v, "heap", L(14, -10, PLATE + 47.5f), 22, GameConfig.Hex("7a5638"));   // 호퍼 위로 솟은 흙 더미
        heap.transform.localScale = new Vector3(24, 9, 18) * GameConfig.K;
        Prim.Cyl(v, "tub", L(14, -10, PLATE + 5), 26, 10, MachineKit.SHELL_D);
        Prim.Cyl(v, "tubIn", L(14, -10, PLATE + 10.2f), 20, 0.6f, GameConfig.FERT);
        paddle = new GameObject("paddle").transform; paddle.SetParent(v, false); paddle.localPosition = L(14, -10, PLATE + 12);
        Prim.Box(paddle, "a", Vector3.zero, new Vector3(20, 1.6f, 3), DARK);
        Prim.Box(paddle, "b", Vector3.zero, new Vector3(3, 1.6f, 20), DARK);
        Prim.Cyl(paddle, "shaft", new Vector3(0, 5 * GameConfig.K, 0), 2, 10, DARK);
        // 퇴비 더미(흙 + 퇴비) · 비료 드럼통
        var pile = Prim.Sphere(v, "compost", L(-32, -12, PLATE), 24, GameConfig.Hex("5a3d22"));
        pile.transform.localScale = new Vector3(24, 10, 18) * GameConfig.K;
        Prim.Sphere(v, "leaf", L(-35, -13, PLATE + 4.5f), 5, GameConfig.FERT);
        Prim.Sphere(v, "leaf", L(-28, -9, PLATE + 3.5f), 4, GameConfig.Hex("6f8f3a"));
        ModelLib.Put(v, "barrel", 14, 14, 14, -40, PLATE, -22, 0.5f, ModelLib.SK(0x8fae4a, ModelLib.COL.steelD));
    }

    // 온실: 남북으로 놓인 유리 터널(목표 그림 docs/art/target_north_v1.png). 카메라가 남쪽에서 보므로 아치 살이 반원으로 또렷하게 읽힌다
    // (예전 동서 터널은 살이 위에서 세로줄로 보여 격자처럼 보였다). 안에 흙 두둑 3줄과 작물 12포기, 동쪽 물탱크에서 파이프
    void BuildGreen()
    {
        var v = View(3, new Vector2(-38, 28), new Vector2(38, 28));
        const float R = 36f, LEN = 60f, CX = -20f, CY = -14f;   // 터널 앞 끝(y 16)은 투입 쟁반 바로 뒤
        foreach (float bx in new[] { CX - 16, CX, CX + 16 }) Prim.Box(v, "bed", L(bx, CY, PLATE + 2), new Vector3(10, 4, LEN - 8), GameConfig.SOIL);
        for (int r = 0; r < 3; r++)
            for (int j = 0; j < 4; j++)
            {
                var c = new GameObject("crop").transform; c.SetParent(v, false); c.localPosition = L(CX - 16 + r * 16, CY - 22 + j * 14.5f, PLATE + 4);
                Prim.Cyl(c, "stem", new Vector3(0, 3 * GameConfig.K, 0), 1.8f, 6, GameConfig.Hex("4f8a3a"));
                Prim.Sphere(c, "leaf", new Vector3(0, 8 * GameConfig.K, 0), 9, GameConfig.Hex("4f9a3a"));
                Prim.Sphere(c, "top", new Vector3(1.5f * GameConfig.K, 11 * GameConfig.K, -1f * GameConfig.K), 5.5f, GameConfig.Hex("7cc35a"));
                crops.Add(c);
            }
        // 유리(투명 반원통) — 깊이를 안 써서 외곽선이 생기지 않고 안의 작물이 비친다. 메시는 축 x라 세로로 돌린다
        var film = new GameObject("film"); film.transform.SetParent(v, false); film.transform.localPosition = L(CX, CY, PLATE);
        film.transform.localScale = Vector3.one * GameConfig.K; film.transform.localRotation = Quaternion.Euler(0, 90, 0);
        film.AddComponent<MeshFilter>().sharedMesh = TunnelMesh(R, LEN, 20);
        var mr = film.AddComponent<MeshRenderer>(); var fc = GameConfig.Hex("eef8ff"); fc.a = 0.3f; mr.sharedMaterial = Mats.TransShared(fc);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        // 아치 살 3개(앞·가운데·뒤): 반원을 10토막 상자로
        const int SEG = 10; float segL = Mathf.PI * R / SEG * 1.08f;
        foreach (float ay in new[] { CY + LEN / 2, CY, CY - LEN / 2 })
            for (int j = 0; j < SEG; j++)
            {
                float a = Mathf.PI * (j + 0.5f) / SEG;
                var rib = Prim.Box(v, "rib", L(CX - Mathf.Cos(a) * R, ay, PLATE + Mathf.Sin(a) * R), new Vector3(segL, 2f, 2f), WHITE);
                rib.transform.localRotation = Quaternion.Euler(0, 0, 90f - a * Mathf.Rad2Deg);
            }
        foreach (float sx in new[] { CX - R, CX + R }) Prim.Box(v, "sill", L(sx, CY, PLATE + 1.5f), new Vector3(3, 3, LEN + 3), WHITE);
        // 앞쪽 아치 문(흰 문틀 + 남색 문)
        float fy = CY + LEN / 2 + 1;
        Prim.Box(v, "door", L(CX, fy, PLATE + 11), new Vector3(14, 22, 1.2f), GameConfig.Hex("9cc4da"));   // 유리문(남색 문은 검은 네모로 튀었다)
        Prim.Box(v, "doorL", L(CX - 8, fy + 0.3f, PLATE + 11), new Vector3(2, 22, 1.6f), WHITE);
        Prim.Box(v, "doorR", L(CX + 8, fy + 0.3f, PLATE + 11), new Vector3(2, 22, 1.6f), WHITE);
        Prim.Box(v, "doorT", L(CX, fy + 0.3f, PLATE + 23), new Vector3(18, 2.4f, 1.6f), WHITE);
        // 물탱크(동쪽)·파이프 (물은 S.water에서 끌어 온다)
        Prim.Cyl(v, "wtank", L(40, -26, PLATE + 14), 20, 28, WHITE);
        Prim.Cyl(v, "wband", L(40, -26, PLATE + 18), 21, 5, PIPE);
        Prim.Sphere(v, "wdome", L(40, -26, PLATE + 28), 20, WHITE);
        Prim.Cyl(v, "wcap", L(40, -26, PLATE + 37.5f), 8, 2, PIPE);
        Prim.Box(v, "wpipe", L(CX + R + 6, -26, PLATE + 5), new Vector3(16, 3, 3), PIPE);
    }

    // 축 x, 바닥 y=0인 반원통의 바깥 면(논리 단위; 뷰가 K로 줄인다)
    static Mesh TunnelMesh(float r, float len, int seg)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.PI * i / seg, y = Mathf.Sin(a), z = Mathf.Cos(a);
            v.Add(new Vector3(-len / 2, y * r, z * r)); v.Add(new Vector3(len / 2, y * r, z * r));
            n.Add(new Vector3(0, y, z)); n.Add(new Vector3(0, y, z));
        }
        for (int i = 0; i < seg; i++)
        {
            int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
            tri.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
        }
        // 양 끝 반원 마개(부채꼴): 옆에서 아치 실루엣이 보이게
        for (int e = -1; e <= 1; e += 2)
        {
            int c0 = v.Count; v.Add(new Vector3(e * len / 2, 0, 0)); n.Add(new Vector3(e, 0, 0));
            for (int i = 0; i <= seg; i++) { float a = Mathf.PI * i / seg; v.Add(new Vector3(e * len / 2, Mathf.Sin(a) * r, Mathf.Cos(a) * r)); n.Add(new Vector3(e, 0, 0)); }
            for (int i = 0; i < seg; i++) { if (e > 0) tri.AddRange(new[] { c0, c0 + i + 2, c0 + i + 1 }); else tri.AddRange(new[] { c0, c0 + i + 1, c0 + i + 2 }); }
        }
        var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return m;
    }
}

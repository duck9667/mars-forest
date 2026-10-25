using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §5.1 먼지 토끼 우리(pen, Phase 3): 우리 해제 후 토끼 3마리가 우리 안을 깡충거리고, penT 22초마다 penTip이 쌓인다.
// 플레이어가 PEN_TIP(327,766) r40에서 수거(§1.1 6번째 행동, Player.Interact → Collect).
// 원본 app/index.html: bHopper(699), bPenProps(637), onPad·spawnHoppers(836~838), tickMachines 팁 누적(846), tickHoppers(881), 수거(810), 팁 더미(1047)
// 원본과 다르게 둔 것(연출만, 수치는 그대로): 해제 직후 토끼가 게이트에서 하나씩 팝, 팁이 쌓일 때 가까운 토끼가 동전을 던져 올림,
// 대기 중 두리번·코 씰룩·가끔 빙키, 건초·물그릇·당근에 들러 오물거림, 플레이어가 다가오면 비켜 깡충, 수거하면 다 같이 깡충.
public class PenSystem : MonoBehaviour
{
    public static PenSystem I;
    void Awake() { I = this; }
    /// 플레이어 등짐 칩·벨 비행의 기준 위치(우리 팁 자리)
    public Vector3 TipWorld => WorldMap.W2U(WorldMap.PEN_TIP, 6f);

    // §1.1 6행·§5.1 수치
    public const float R_TIP = 40f, CD_TIP = 0.2f;   // 수거 반경(원)·쿨다운
    public const float TIP_PERIOD = 22f;              // penT 주기
    public const float HOP_SPEED = 58f;               // 토끼 이동 속도(논리/초)
    public const int HOPPER_N = 3;
    const float K = GameConfig.K;
    // 우리 안 이동 범위 (PEN_X0+12..X1-12, PEN_Y0+24..Y1-12)
    const float RX0 = WorldMap.PEN_X0 + 12, RX1 = WorldMap.PEN_X1 - 12, RY0 = WorldMap.PEN_Y0 + 24, RY1 = WorldMap.PEN_Y1 - 12;

    GameState G => GameState.I;
    SaveData S => G.S;
    public bool Open => G != null && G.PadLv("pen") >= 1;

    // 원본 COL
    static readonly Color TAN = GameConfig.Hex("d9a46c"), TAN_D = GameConfig.Hex("b0804e"), WHITE = GameConfig.Hex("f8f6f2"), DARK = GameConfig.Hex("2b2420");
    static readonly Color EAR_IN = GameConfig.Hex("f2b8a0"), NOSE = GameConfig.Hex("ffb3a0");
    static readonly Color STEEL = GameConfig.Hex("c9d1d8"), STEEL_D = GameConfig.Hex("8c98a4"), ORANGE = GameConfig.Hex("f07a3a"), BLUE = GameConfig.Hex("4fb0f0"), NAVY = GameConfig.Hex("22304a");
    static readonly Color GREEN = GameConfig.Hex("5cc45e"), GREEN_D = GameConfig.Hex("2f7a33"), WOOD_D = GameConfig.Hex("6e3f26");
    static readonly Color HAY = GameConfig.Hex("e3b955"), HAY_L = GameConfig.Hex("f0cf72"), HAY_D = GameConfig.Hex("9a6a2a"), STRAW = GameConfig.Hex("f6dd92");

    // 소품 자리(논리). 게이트(N@330 w60)~팁 자리 동선은 비워 둔다
    static readonly Vector2 P_HAY = new Vector2(357, 822), P_CARROT = new Vector2(291, 816), P_BOWL = new Vector2(296, 864),
                            P_HUTCH = new Vector2(302, 904);
    // 토끼가 피해 다니는 원(중심·반경): 0 팁 자리, 1 건초, 2 당근, 3 물그릇, 4 집
    static readonly Vector2[] OB_C = { WorldMap.PEN_TIP, P_HAY, P_CARROT, P_BOWL, P_HUTCH };
    static readonly float[] OB_R = { 17f, 15f, 9f, 10f, 18f };   // 멈춰 설 자리 판정. 팁 자리는 몸길이(~33)를 감안해 넉넉히(머리가 동전 위를 덮지 않게)
    static readonly float[] OB_PR = { 11f, 15f, 9f, 10f, 18f };  // 지나가는 길 판정. 팁 접시는 스쳐 지나가도 되게 접시 크기만
    // 들르는 자리(관심 지점) → 바라볼 방향(논리)·연결된 장애물. 토끼 머리(중심 앞 ~19)가 소품에 닿는 거리
    static readonly Vector2[] SPOT = { new Vector2(331, 826), new Vector2(312, 864), new Vector2(305, 818) };
    static readonly Vector2[] SPOT_FACE = { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(-1, 0) };
    static readonly int[] SPOT_OB = { 1, 3, 2 };

    class Hopper
    {
        public Transform view, lift, root, body, earL, earR, nose, shadow;
        public Vector3 shadowBase, noseBase;
        public Vector2 pos, tgt, dir = new Vector2(0, 1);
        public float wait, hop, ph, yaw, pitch, earX;
        public bool moving, ready, nibble;
        public float jumpT = -1f, jumpDur, jumpAmp; public bool jumpTwist;   // 추가 점프(팁 배달·축하·빙키·놀람)
        public float lookT, binkT, fleeCD;
        public int stuck;   // 목표 뽑기 연속 실패 수(3번째부터 길 검사 생략)
        public int interest = -1;
    }
    readonly List<Hopper> hoppers = new List<Hopper>();

    bool built;
    Transform propsRoot, pile;
    readonly List<Transform> props = new List<Transform>();
    readonly List<Transform> coins = new List<Transform>();
    int shown;            // 더미가 보여 주는 금액(배달 동전이 도착할 때 S.penTip을 따라잡는다)
    int pendingFly;       // 날아가는 중인 배달 동전 수
    bool collecting;
    Material beaconMat, lampMat;

    void Start()
    {
        G.OnPadLevel += OnPad;
        // QA 전용: -pentip N 팁 금액, -pent S 타이머 시작값(22초를 기다리지 않고 누적 확인)
        if (int.TryParse(DevArgs.Get("-pentip"), out var pt)) S.penTip = Mathf.Max(0, pt);
        if (float.TryParse(DevArgs.Get("-pent"), out var ptt)) S.penT = Mathf.Clamp(ptt, 0, TIP_PERIOD);
        if (Open) Build(false);
    }

    void OnDestroy()
    {
        if (G != null) G.OnPadLevel -= OnPad;
        if (beaconMat) Destroy(beaconMat); if (lampMat) Destroy(lampMat);
        if (I == this) I = null;
    }

    void OnPad(string id, int lv) { if (id == "pen" && lv >= 1) Build(true); }

    // ───────── 조립 ─────────
    void Build(bool fx)
    {
        if (built) return; built = true;
        propsRoot = new GameObject("penProps").transform; propsRoot.SetParent(transform, false);
        BuildTipPlate(); BuildHay(); BuildCarrots(); BuildBowl(); BuildHutch();
        // 팁 금액 라벨은 두지 않는다: 동전 더미로 보인다 (jake 피드백 10/24)
        shown = S.penTip; SyncPile(false);
        if (fx) StartCoroutine(CoUnlock());
        else
        {
            for (int i = 0; i < HOPPER_N; i++)
            {
                var h = MakeHopper(RandomFree(null));
                h.ready = true; h.wait = 0.5f + Random.Range(0f, 2f); h.dir = Rot(new Vector2(0, 1), Random.Range(-60f, 60f)); h.yaw = Yaw(h.dir);
            }
        }
    }

    // 해제: 소품이 차례로 솟고(0.15s~), 바닥이 깔린 뒤 토끼가 게이트 안쪽에서 하나씩 팝으로 등장해 각자 자리로 깡충
    IEnumerator CoUnlock()
    {
        foreach (var p in props) p.gameObject.SetActive(false);
        pile.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.15f);
        pile.gameObject.SetActive(true); JuiceFX.I.Pop(pile, 0.001f, 0.45f);
        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i]; p.gameObject.SetActive(true); JuiceFX.I.Pop(p, 0.001f, 0.5f);
            JuiceFX.I.Dust(p.position + Vector3.up * 0.1f, 2);
            AudioManager.I.Play("pop", 1.1f + i * 0.08f, 0.35f);
            yield return new WaitForSeconds(0.07f);
        }
        yield return new WaitForSeconds(0.25f);
        Vector2[] door = { new Vector2(306, 752), new Vector2(350, 752), new Vector2(328, 744) };   // 게이트(300~360) 안쪽, 팁 자리 비켜서
        for (int i = 0; i < HOPPER_N; i++)
        {
            var h = MakeHopper(door[i % door.Length]);
            h.dir = new Vector2(0, 1); h.yaw = Yaw(h.dir); h.view.rotation = Quaternion.Euler(0, h.yaw, 0);   // 화면 쪽(남)을 보며 등장
            JuiceFX.I.Pop(h.view, 0.001f, 0.45f);
            JuiceFX.I.Dust(h.view.position + Vector3.up * 0.1f, 3);
            JuiceFX.I.Ring(h.view.position, GameConfig.GOLD, 14);
            AudioManager.I.Play("pop", 1.25f + i * 0.15f, 0.55f);
            StartCoroutine(CoReady(h, 0.45f, 0.1f + i * 0.12f));
            yield return new WaitForSeconds(0.3f);
        }
    }
    IEnumerator CoReady(Hopper h, float after, float wait)
    {
        yield return new WaitForSeconds(after);
        h.ready = true; h.wait = wait; Jump(h, 10f, 0.3f);
    }

    // 원본 bHopper: 몸(타원)·머리·귀 2(속 분홍)·눈·코·발·꼬리, 그룹 스케일 1.25. 귀는 밑동 피벗(스케일 1) 아래 형제로 둬 스케일이 곱해지지 않게
    Hopper MakeHopper(Vector2 at)
    {
        var h = new Hopper { pos = at, tgt = at, ph = Random.Range(0f, 7f), lookT = Random.Range(0.8f, 2f), binkT = Random.Range(5f, 12f) };
        h.view = new GameObject("hopper").transform; h.view.SetParent(transform, false); h.view.position = WorldMap.W2U(at);
        h.shadow = Prim.BlobShadow(h.view, 24).transform; h.shadowBase = h.shadow.localScale;
        h.lift = Group(h.view, "lift", 0, 0, 0);
        h.root = Group(h.lift, "root", 0, 0, 0); h.root.localScale = Vector3.one * 1.25f;
        h.body = Ell(h.root, "body", 0, 10, 0, 18, 16.2f, 21.6f, TAN).transform;
        Sph(h.root, "head", 0, 17, 8, 14, TAN);
        h.earL = Ear(h.root, -3); h.earR = Ear(h.root, 3);
        Sph(h.root, "eyeL", -3, 18, 14, 2.6f, DARK); Sph(h.root, "eyeR", 3, 18, 14, 2.6f, DARK);
        h.nose = Sph(h.root, "nose", 0, 15.5f, 15, 2.6f, NOSE).transform; h.noseBase = h.nose.localScale;
        Ell(h.root, "footL", -5, 2.5f, 4, 7, 3.5f, 11.2f, TAN_D); Ell(h.root, "footR", 5, 2.5f, 4, 7, 3.5f, 11.2f, TAN_D);
        Sph(h.root, "tail", 0, 10, -11, 6, WHITE);
        hoppers.Add(h);
        return h;
    }
    Transform Ear(Transform p, float x)
    {
        var piv = Group(p, x < 0 ? "earL" : "earR", x, 21.5f, 6);
        Ell(piv, "m", 0, 4.6f, 0, 2.8f, 10.6f, 2.3f, TAN);
        Prim.NoShadow(Ell(piv, "in", 0, 4.6f, 0.95f, 1.3f, 4.4f, 0.75f, EAR_IN));
        return piv;
    }

    // 팁 자리: 낮은 강철 접시 + 주황 테(빈 상태에서도 "여기에 쌓인다"가 읽히게). 동전은 접시 위 3더미
    void BuildTipPlate()
    {
        pile = new GameObject("tipPile").transform; pile.SetParent(transform, false); pile.position = WorldMap.W2U(WorldMap.PEN_TIP);
        var rim = Cyl(pile, "rim", 0, 0.8f, 0, 27, 1.6f, ORANGE); rim.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var plate = Cyl(pile, "plate", 0, 1.0f, 0, 23, 2f, STEEL); plate.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // 건초 더미(오른쪽 위): 끈 묶은 건초 뭉치 2단 + 위에 흩어진 지푸라기, 옆에 떨어진 한 줌
    void BuildHay()
    {
        var g = Prop("hay", P_HAY);
        var a = new GameObject("baleA").transform; a.SetParent(g, false); a.localRotation = Quaternion.Euler(0, 10, 0);
        Bale(a, 0, 0, 22, 10, 14);
        var b = new GameObject("baleB").transform; b.SetParent(g, false); b.localPosition = P(1, 10, 0.5f); b.localRotation = Quaternion.Euler(0, -18, 0);
        Bale(b, 0, 0, 15, 8, 11);
        float[,] st = { { -3, 18.4f, -1, 0, 30, 84 }, { 2, 18.4f, 2, 0, -50, 86 }, { 5, 18.3f, -2, 0, 75, 88 }, { -9, 10.4f, 4, 0, 10, 80 } };
        for (int i = 0; i < st.GetLength(0); i++)
        {
            var s = Box(g, "straw", st[i, 0], st[i, 1], st[i, 2], 0.9f, 9f, 0.9f, STRAW);
            s.transform.localRotation = Quaternion.Euler(st[i, 5], st[i, 4], 0);
        }
        EllS(g, "tuft", -12, 1f, 7, 9, 2.4f, 6, HAY_L);
    }
    void Bale(Transform p, float x, float y, float w, float h, float d)
    {
        Box(p, "bale", x, y + h / 2, 0, w, h, d, HAY);
        Box(p, "top", x, y + h + 0.01f, 0, w - 2f, 0.4f, d - 2f, HAY_L);
        foreach (float sx in new[] { -w * 0.27f, w * 0.27f }) Box(p, "string", x + sx, y + h / 2, 0, 1.1f, h + 0.4f, d + 0.4f, HAY_D);
    }

    // 당근 밭(왼쪽 위): 흙 자리 + 반쯤 묻힌 당근 3, 잎 2장씩
    void BuildCarrots()
    {
        var g = Prop("carrots", P_CARROT);
        EllS(g, "soil", 0, 0.3f, 0, 20, 2.2f, 16, TAN_D);
        float[,] c = { { -4, 3 }, { 4, 1 }, { 0, -5 } };
        for (int i = 0; i < c.GetLength(0); i++)
        {
            float x = c[i, 0], z = c[i, 1];
            Cap(g, "carrot", x, 2f, z, 4f, 8f, ORANGE);
            var l1 = Box(g, "leaf", x - 0.9f, 7.5f, z, 1.2f, 5.5f, 1.8f, GREEN); l1.transform.localRotation = Quaternion.Euler(0, 0, 22);
            var l2 = Box(g, "leaf", x + 0.9f, 7.5f, z, 1.2f, 5.5f, 1.8f, GREEN_D); l2.transform.localRotation = Quaternion.Euler(0, 0, -22);
        }
    }

    // 물그릇(왼쪽 아래): 강철 그릇 + 그 위로 살짝 올라온 물(테두리에 안 가리게)
    void BuildBowl()
    {
        var g = Prop("bowl", P_BOWL);
        Cyl(g, "bowl", 0, 2.2f, 0, 17, 4.4f, STEEL);
        Cyl(g, "foot", 0, 0.4f, 0, 18.5f, 0.8f, STEEL_D);
        Prim.NoShadow(Cyl(g, "water", 0, 4.75f, 0, 13.5f, 0.9f, BLUE));
        Prim.NoShadow(EllS(g, "glint", -2.5f, 5.25f, -2f, 4f, 0.3f, 2f, GameConfig.Hex("bfe4ff")));
    }

    // 작은 집(왼쪽 아래): 박공지붕 토끼집. 화성 기지풍으로 흰 벽·주황 지붕·태양광 판·깜빡이 안테나, 남쪽(화면 앞) 둥근 문과 문등
    void BuildHutch()
    {
        var g = Prop("hutch", P_HUTCH);
        Box(g, "base", 0, 0.8f, 0, 27, 1.6f, 21, STEEL_D);
        Box(g, "walls", 0, 7.5f, 0, 24, 12, 18, WHITE);
        var gable = Box(g, "gable", 0, 13.5f, 0, 15.5f, 15.5f, 17.6f, WHITE); gable.transform.localRotation = Quaternion.Euler(0, 0, 45);
        var rl = Box(g, "roofL", -7.2f, 18.4f, 0, 20.5f, 1.8f, 21.5f, ORANGE); rl.transform.localRotation = Quaternion.Euler(0, 0, 45);
        var rr = Box(g, "roofR", 7.2f, 18.4f, 0, 20.5f, 1.8f, 21.5f, ORANGE); rr.transform.localRotation = Quaternion.Euler(0, 0, -45);
        Box(g, "ridge", 0, 25.6f, 0, 2.4f, 2.4f, 22f, GameConfig.Hex("c4542a")).transform.localRotation = Quaternion.Euler(0, 0, 45);
        var panel = Box(g, "solar", -7.8f, 19.8f, -1, 9.5f, 0.7f, 10, GameConfig.Hex("2a4f8f")); panel.transform.localRotation = Quaternion.Euler(0, 0, 45);
        // 문(남쪽 벽 z+9): 사각 + 둥근 윗부분
        Box(g, "door", 0, 5.6f, 9.25f, 8, 8, 0.8f, NAVY);
        var arch = Cyl(g, "doorTop", 0, 9.6f, 9.25f, 8, 0.8f, NAVY); arch.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box(g, "step", 0, 1.9f, 11f, 11, 0.8f, 3.2f, STEEL_D);
        var lamp = SphS(g, "lamp", 0, 16.5f, 9.4f, 3f, GameConfig.Hex("ffe9a0")); Prim.NoShadow(lamp);
        lampMat = Emis(GameConfig.Hex("ffe9a0")); lamp.GetComponent<Renderer>().sharedMaterial = lampMat;
        Cyl(g, "ant", 6, 26f, -6, 1.2f, 10, DARK);
        var bc = SphS(g, "beacon", 6, 31.5f, -6, 3.4f, GameConfig.Hex("ff5a4a")); Prim.NoShadow(bc);
        beaconMat = Emis(GameConfig.Hex("ff5a4a")); bc.GetComponent<Renderer>().sharedMaterial = beaconMat;
    }

    // ───────── 매 프레임 ─────────
    void Update()
    {
        if (!built || G == null) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX), t = Time.time;
        TickTip(dt);
        for (int i = 0; i < hoppers.Count; i++) TickHopper(hoppers[i], dt, t);
        if (pendingFly == 0 && !collecting && shown != S.penTip) { shown = S.penTip; SyncPile(false); }
        if (beaconMat) beaconMat.SetColor("_EmissionColor", GameConfig.Hex("ff5a4a") * (Mathf.Repeat(t, 1.4f) < 0.18f ? 2.2f : 0.15f));
        if (lampMat) { float night = DayNight.I != null ? DayNight.I.night : 0f; lampMat.SetColor("_EmissionColor", GameConfig.Hex("ffd27a") * (0.25f + night * 1.8f)); }
    }

    // §5.1: penT 22s마다 penTip < price*8이면 penTip += round(price*1.2). 금액은 즉시 저장 상태에 반영하고 동전 배달은 연출만
    void TickTip(float dt)
    {
        S.penT += dt;
        if (S.penT < TIP_PERIOD) return;
        S.penT = 0;
        int price = G.BottlePrice;
        if (S.penTip >= price * 8) return;
        int add = Mathf.FloorToInt(price * 1.2f + 0.5f);
        S.penTip += add;
        StartCoroutine(CoDeliver(add));
    }

    // 팁 자리에 가장 가까운 토끼가 그쪽을 보고 깡충 → 꼭대기에서 동전을 던져 올린다 → 더미가 자라고 칩이 튄다
    IEnumerator CoDeliver(int add)
    {
        Hopper best = null; float bd = float.MaxValue;
        foreach (var h in hoppers) { if (!h.ready) continue; float d = (h.pos - WorldMap.PEN_TIP).sqrMagnitude; if (d < bd) { bd = d; best = h; } }
        pendingFly++;
        if (best != null)
        {
            var dv = WorldMap.PEN_TIP - best.pos; if (dv.sqrMagnitude > 1) best.dir = dv.normalized;
            Jump(best, 13f, 0.36f);
            AudioManager.I.Play("pick", 0.62f, 0.6f);   // 원본 sfx('hop') 대응: 낮고 짧은 톡
            yield return new WaitForSeconds(0.16f);
            Vector3 from = best.root.position + Vector3.up * 22 * K;
            ArcFlyer.I.FlyItem("coin", from, PileTopWorld, () => OnCoinLanded(add), 0.45f, 40f);
        }
        else OnCoinLanded(add);
    }
    void OnCoinLanded(int add)
    {
        pendingFly = Mathf.Max(0, pendingFly - 1);
        if (collecting || S.penTip <= 0) return;   // 날아오는 사이 이미 수거했으면 착지 연출 생략
        shown = S.penTip; SyncPile(true);
        JuiceFX.I.Squash(pile, 0.8f);
        JuiceFX.I.Ring(pile.position, GameConfig.GOLD, 12);
        AudioManager.I.PlayHz("coin", 1250, 1100, 0.45f);
        WorldLabels.I.Float(WorldMap.W2U(WorldMap.PEN_TIP.x, WorldMap.PEN_TIP.y - 4, 18), "+" + add, true);
    }

    // ───────── §1.1 6번째 행동: 토끼 벨 수거 ─────────
    /// 팁 더미를 플레이어에게 빨아들인다. 벨은 즉시 가산(저장 안전)하고 연출만 뒤따른다. 수거액 반환
    public int Collect()
    {
        int n = S.penTip; if (n <= 0) return 0;
        S.penTip = 0; shown = 0;
        G.AddBells(n, "pen");
        StartCoroutine(CoCollect(n));
        return n;
    }
    IEnumerator CoCollect(int n)
    {
        collecting = true;
        var fly = new List<Transform>(coins); coins.Clear();
        var cam = Camera.main;
        if (cam != null && HUD.I != null) HUD.I.Coins(cam.WorldToScreenPoint(TipWorld), () => HUD.I.BellsScreen(), 8);
        WorldLabels.I.Float(WorldMap.W2U(WorldMap.PEN_TIP.x, WorldMap.PEN_TIP.y - 8, 10), "+" + n, true, n >= G.BottlePrice * 4);
        AudioManager.I.Play("pick", 0.62f, 0.7f); AudioManager.I.Play("whoosh", 1.3f, 0.3f);
        JuiceFX.I.Ring(pile.position, Color.white, 16);
        // 토끼들: 플레이어 쪽을 보고 차례로 깡충
        for (int i = 0; i < hoppers.Count; i++) if (hoppers[i].ready) StartCoroutine(CoCheer(hoppers[i], 0.05f + i * 0.09f));
        // 더미의 동전을 윗장부터 플레이어에게
        for (int i = fly.Count - 1; i >= 0; i--)
        {
            var c = fly[i]; if (c == null) continue;
            ArcFlyer.I.Fly(c.gameObject, c.position, () => Player.I != null ? Player.I.body.position + Vector3.up * 1.4f : TipWorld, 0.26f, 34f);
            yield return new WaitForSeconds(0.03f);
        }
        yield return new WaitForSeconds(0.1f);
        collecting = false;
    }
    IEnumerator CoCheer(Hopper h, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (Player.I != null) { var dv = Player.I.pos - h.pos; if (dv.sqrMagnitude > 1) h.dir = dv.normalized; }
        Jump(h, 12f, 0.34f, true);
    }

    // ───────── 팁 더미·칩 ─────────
    // 동전 수: 팁 1회분(price*1.2)당 2개, 최대 15 (3더미 × 5단)
    int CoinsFor(int tip) => tip <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(tip / (G.BottlePrice * 0.6f)), 1, 15);
    static readonly Vector2[] STACK = { new Vector2(-5.4f, 2.6f), new Vector2(5.4f, 2.6f), new Vector2(0f, -6.4f) };   // 논리 (x, 남쪽+)
    Vector3 SlotLocal(int i)
    {
        var s = STACK[i % 3]; int lv = i / 3;
        float jx = Mathf.Sin(i * 12.9898f) * 0.7f, jz = Mathf.Cos(i * 78.233f) * 0.7f;
        return new Vector3((s.x + jx) * K, (2f + 1.1f + lv * 2.2f) * K, -(s.y + jz) * K);
    }
    Vector3 PileTopWorld()
    {
        int n = Mathf.Max(0, coins.Count);
        return pile.position + (n < 15 ? SlotLocal(n) : Vector3.up * 14 * K) + Vector3.up * 1.2f * K;
    }
    void SyncPile(bool fx)
    {
        int n = CoinsFor(shown);
        while (coins.Count > n) { var c = coins[coins.Count - 1]; coins.RemoveAt(coins.Count - 1); if (c) { c.SetParent(null, false); Destroy(c.gameObject); } }
        while (coins.Count < n)
        {
            int i = coins.Count;
            var go = Prim.Cyl(pile, "coin", SlotLocal(i), 10, 2.2f, GameConfig.GOLD);
            go.transform.localRotation = Quaternion.Euler(Mathf.Sin(i * 3.1f) * 4f, i * 47f, Mathf.Cos(i * 5.3f) * 4f);
            coins.Add(go.transform);
            if (fx) JuiceFX.I.Pop(go.transform, 0.2f, 0.35f);
        }
    }

    // ───────── 토끼 이동·애니 (원본 tickHoppers + 그리기) ─────────
    void TickHopper(Hopper h, float dt, float t)
    {
        if (!h.ready) return;
        h.moving = false; h.fleeCD -= dt;
        // 플레이어가 다가오면(대기 중일 때만) 반대쪽으로 비켜 깡충
        if (Player.I != null && h.wait > 0 && h.fleeCD <= 0 && (h.pos - Player.I.pos).sqrMagnitude < 28f * 28f) Flee(h, Player.I.pos);
        if (h.wait > 0)
        {
            h.wait -= dt;
            if (!h.nibble)
            {
                // 두리번: 가끔 고개(몸) 방향을 돌린다
                h.lookT -= dt;
                if (h.lookT <= 0) { h.lookT = Random.Range(1.2f, 2.6f); h.dir = Rot(h.dir, Random.Range(-70f, 70f)); }
                // 빙키: 드물게 제자리에서 비틀며 깡충
                h.binkT -= dt;
                if (h.binkT <= 0) { h.binkT = Random.Range(7f, 14f); Jump(h, 11f, 0.34f, true); AudioManager.I.Play("pick", 0.75f, 0.25f); }
            }
            // 다른 토끼와 겹쳐 서 있으면 곧 자리를 옮긴다
            if (!h.nibble && h.wait > 0.3f && Crowded(h)) h.wait = 0.3f;
            if (h.wait <= 0) { h.nibble = false; PickTarget(h); }
        }
        else
        {
            float d = MoveTo(h, h.tgt, HOP_SPEED * dt);
            h.hop += dt * 7.5f;
            if (d < 2f)
            {
                h.wait = 1f + Random.Range(0f, 3f); h.hop = 0;
                if (h.interest >= 0) { h.nibble = true; h.dir = SPOT_FACE[h.interest]; h.wait += 1f; }
            }
        }

        // 그리기: 깡충 |sin(hop)|*9 + 추가 점프, 그림자는 바닥에 남아 높이만큼 작아짐
        float hop = h.moving ? Mathf.Abs(Mathf.Sin(h.hop)) * 9f : 0f, jh = 0f, tw = 0f;
        if (h.jumpT >= 0)
        {
            h.jumpT += dt; float k = h.jumpT / h.jumpDur;
            if (k >= 1f) h.jumpT = -1f;
            else { jh = Mathf.Sin(Mathf.PI * k) * h.jumpAmp; if (h.jumpTwist) tw = Mathf.Sin(Mathf.PI * 2f * k) * 28f; }
        }
        float hh = hop + jh;
        h.view.position = WorldMap.W2U(h.pos);
        h.lift.localPosition = new Vector3(0, hh * K, 0);
        float sk = Mathf.Max(0.5f, 1f - hh / 60f);
        h.shadow.localScale = new Vector3(h.shadowBase.x * sk, h.shadowBase.y, h.shadowBase.z * sk);
        h.yaw = Mathf.LerpAngle(h.yaw, Yaw(h.dir), Mathf.Min(1f, dt * 14f));   // 원본 turnTo
        h.view.rotation = Quaternion.Euler(0, h.yaw + tw, 0);
        // 몸 숨쉬기/깡충 늘어남(원본 body.scale.y), 뛸 땐 앞뒤로 까딱, 오물거릴 땐 고개 숙여 까닥
        float ms = t * 1000f;
        float fb = h.moving ? 1f + Mathf.Cos(h.hop * 2f) * 0.08f : 1f + Mathf.Sin(ms / 400f + h.ph) * 0.04f;
        h.body.localScale = new Vector3(18f, 16.2f * fb, 21.6f) * K;
        float pitchT = h.moving ? -Mathf.Cos(h.hop * 2f) * 7f : h.nibble ? 9f + Mathf.Sin(t * 13f + h.ph) * 5f : 0f;
        if (jh > 0) pitchT = -Mathf.Cos(Mathf.PI * Mathf.Clamp01(h.jumpT / h.jumpDur)) * 9f;
        h.pitch = Mathf.Lerp(h.pitch, pitchT, Mathf.Min(1f, dt * 14f));
        h.root.localRotation = Quaternion.Euler(h.pitch, 0, 0);
        // 귀: 원본 흔들림(z) + 뛰는 동안 뒤로 젖힘(x)
        float ez = (0.1f + Mathf.Sin(ms / 350f + h.ph) * 0.12f) * Mathf.Rad2Deg;
        float earXT = h.moving || jh > 0 ? -(12f + 22f * Mathf.Clamp01(hh / 9f)) : 0f;
        h.earX = Mathf.Lerp(h.earX, earXT, Mathf.Min(1f, dt * 12f));
        h.earL.localRotation = Quaternion.Euler(h.earX, 0, ez);
        h.earR.localRotation = Quaternion.Euler(h.earX, 0, -ez);
        // 코 씰룩: 쉬는 동안 짧게 몰아서, 오물거릴 땐 계속
        float cyc = Mathf.Repeat(t * 0.55f + h.ph, 1f);
        float nt = !h.moving && (h.nibble || cyc < 0.28f) ? Mathf.Abs(Mathf.Sin(t * 28f)) * 0.45f : 0f;
        h.nose.localScale = new Vector3(h.noseBase.x * (1f + nt * 0.5f), h.noseBase.y * (1f + nt), h.noseBase.z);
    }

    // 원본 moveTo: 남은 거리 반환, 움직였으면 dir·moving 갱신
    static float MoveTo(Hopper h, Vector2 tg, float sp)
    {
        Vector2 d = tg - h.pos; float L = d.magnitude;
        if (L < 0.5f) return 0f;
        float m = Mathf.Min(sp, L);
        h.pos += d / L * m; h.dir = d / L; h.moving = true;
        return L - m;
    }

    void Jump(Hopper h, float amp, float dur, bool twist = false)
    {
        if (h.jumpT >= 0 && h.jumpT < h.jumpDur * 0.5f) return;   // 오르는 중엔 끊지 않는다
        h.jumpT = 0; h.jumpAmp = amp; h.jumpDur = dur; h.jumpTwist = twist;
    }

    // 원본: 대기 1~4s 뒤 우리 안 무작위 목표. 30%는 건초·물그릇·당근에 들러 오물거린다. 소품·다른 토끼 목표와 겹치거나 소품을 넘어가는 길은 다시 뽑는다
    void PickTarget(Hopper h)
    {
        h.interest = -1;
        if (Random.value < 0.3f)
        {
            int k = Random.Range(0, SPOT.Length);
            if (!SpotTaken(k, h) && PathClear(h.pos, SPOT[k], SPOT_OB[k])) { h.tgt = SPOT[k]; h.interest = k; return; }
        }
        for (int i = 0; i < 16; i++)
        {
            var c = new Vector2(Random.Range(RX0, RX1), Random.Range(RY0, RY1));
            if ((c - h.pos).sqrMagnitude < 18f * 18f) continue;
            if (!FreeAt(c, h) || (h.stuck < 2 && !PathClear(h.pos, c, -1))) continue;
            h.tgt = c; h.stuck = 0; return;
        }
        h.tgt = h.pos; h.stuck++;   // 못 찾으면 제자리(다음 대기 뒤 다시, 거듭 막히면 길 검사 없이)
    }

    void Flee(Hopper h, Vector2 from)
    {
        h.fleeCD = 1.4f;
        Vector2 away = h.pos - from; if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle; away.Normalize();
        for (int i = 0; i < 12; i++)
        {
            var c = h.pos + Rot(away, Random.Range(-65f, 65f)) * Random.Range(30f, 55f);
            c.x = Mathf.Clamp(c.x, RX0, RX1); c.y = Mathf.Clamp(c.y, RY0, RY1);
            if ((c - from).sqrMagnitude < 40f * 40f || !FreeAt(c, h) || !PathClear(h.pos, c, -1)) continue;
            h.tgt = c; h.wait = 0; h.nibble = false; h.interest = -1;
            Jump(h, 6f, 0.2f); AudioManager.I.Play("pick", 0.9f, 0.2f);
            return;
        }
    }

    bool Crowded(Hopper h)
    {
        foreach (var o in hoppers) if (o != h && o.ready && VisD2(o.pos, h.pos) < 28f * 28f) return true;
        return false;
    }
    bool SpotTaken(int k, Hopper self)
    {
        foreach (var o in hoppers) if (o != self && (o.interest == k || VisD2(o.pos, SPOT[k]) < 32f * 32f || VisD2(o.tgt, SPOT[k]) < 36f * 36f)) return true;
        return false;
    }
    bool FreeAt(Vector2 c, Hopper self)
    {
        for (int i = 0; i < OB_C.Length; i++) if ((c - OB_C[i]).sqrMagnitude < (OB_R[i] + 8f) * (OB_R[i] + 8f)) return false;
        // 팁 자리 바로 남쪽(화면 앞)에 서면 카메라에서 동전 더미를 가리므로 비워 둔다
        if (Mathf.Abs(c.x - WorldMap.PEN_TIP.x) < 22f && c.y > WorldMap.PEN_TIP.y && c.y < WorldMap.PEN_TIP.y + 40f) return false;
        foreach (var o in hoppers) if (o != self && (VisD2(o.tgt, c) < 36f * 36f || VisD2(o.pos, c) < 32f * 32f)) return false;   // 몸길이 ~33이라 서로 겹치지 않게
        return true;
    }
    // 소품을 넘어가는 길이면 false. 출발점이 이미 소품 곁이면 멀어지는 길만 허용, ignore는 목적지 소품(들르는 자리)
    static bool PathClear(Vector2 a, Vector2 b, int ignore)
    {
        for (int i = 0; i < OB_C.Length; i++)
        {
            if (i == ignore) continue;
            Vector2 o = OB_C[i]; float r = OB_PR[i] + 4f;
            if ((a - o).sqrMagnitude < r * r) { if ((b - o).sqrMagnitude < (a - o).sqrMagnitude) return false; continue; }
            if (SegDist(o, a, b) < r) return false;
        }
        return true;
    }
    // 화면상 겹침 거리: 카메라가 남쪽 위(고도 ~55°)에서 보므로 남북으로 늘어선 토끼는 키만큼 겹쳐 보인다 → 남북 거리는 0.7배로 친다
    static float VisD2(Vector2 a, Vector2 b) { var d = a - b; return d.x * d.x + d.y * d.y * 0.5f; }
    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float L2 = ab.sqrMagnitude; if (L2 < 0.0001f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / L2);
        return (p - (a + ab * t)).magnitude;
    }
    Vector2 RandomFree(Hopper self)
    {
        for (int i = 0; i < 30; i++)
        {
            var c = new Vector2(Random.Range(RX0, RX1), Random.Range(RY0 + 20, RY1));
            if (FreeAt(c, self)) return c;
        }
        return new Vector2(327, 850);
    }

    // ───────── 헬퍼(위치·크기 논리 단위). 토끼 부품(Sph·Ell)은 로컬 +z = 정면(CharacterKit 관례), 소품(Box·Cyl·Cap·EllS·SphS)의 z 인자는 "남쪽+"(논리 y, 화면 앞) → 로컬 −z ─────────
    static float Yaw(Vector2 dir) => Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;   // LookRotation(dir.x,0,−dir.y)
    static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }
    static Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * K;
    Transform Prop(string n, Vector2 at)
    {
        var t = new GameObject(n).transform; t.SetParent(propsRoot, false); t.position = WorldMap.W2U(at); props.Add(t); return t;
    }
    static Transform Group(Transform p, string n, float x, float y, float z) { var t = new GameObject(n).transform; t.SetParent(p, false); t.localPosition = P(x, y, z); return t; }
    // 소품 함수의 z 인자는 "남쪽+"(논리 y 방향) → 로컬 −z
    static GameObject Box(Transform p, string n, float x, float y, float zS, float w, float h, float d, Color c) => Prim.Box(p, n, P(x, y, -zS), new Vector3(w, h, d), c);
    static GameObject Cyl(Transform p, string n, float x, float y, float zS, float dia, float h, Color c) => Prim.Cyl(p, n, P(x, y, -zS), dia, h, c);
    static GameObject Cap(Transform p, string n, float x, float y, float zS, float dia, float h, Color c) => Prim.Capsule(p, n, P(x, y, -zS), dia, h, c);
    static GameObject Sph(Transform p, string n, float x, float y, float z, float dia, Color c) => Prim.Sphere(p, n, P(x, y, z), dia, c);
    static GameObject Ell(Transform p, string n, float x, float y, float z, float dx, float dy, float dz, Color c)
    {
        var g = Prim.Sphere(p, n, P(x, y, z), 1, c); g.transform.localScale = new Vector3(dx, dy, dz) * K; return g;
    }
    static GameObject EllS(Transform p, string n, float x, float y, float zS, float dx, float dy, float dz, Color c) => Ell(p, n, x, y, -zS, dx, dy, dz, c);
    static GameObject SphS(Transform p, string n, float x, float y, float zS, float dia, Color c) => Prim.Sphere(p, n, P(x, y, -zS), dia, c);
    static Material Emis(Color c)
    {
        var b = Resources.Load<Material>("Mats/Emissive");
        var m = b != null ? new Material(b) : new Material(Mats.Solid(c));
        m.color = c; return m;
    }
}

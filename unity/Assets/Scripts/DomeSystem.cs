using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §4.2·§5.2·§9 휴게 돔(dome, Phase 3). 원본 app/index.html bDome(629~637행)을 옮겨 조립한다:
// 바닥 발판(platform_large r84×1.04, 모래색 SK(orange))·유리 반구(아래 반은 지면에 가려짐)·갈비대 2·허리띠·바닥 테·
// 분수(fountain 30 + 물줄기·물방울·반짝임)·화단 2(trees/treesTall 34)·에어록(corridor_end, 신호등)·가로등·좌석 6(벤치).
// 좌석: 예약(freeSeat, 335~336행)·경로 seatPath/leavePath(850~851행)·팁 round(price*0.5)(868행)·
// 팁 수거(872행: 플레이어가 좌석 r34 안이면 즉시, 쿨다운 없음 → 동전 비행·벨·플로팅 텍스트·소리).
// 충돌(좌석 r16·분수·화단·에어록)은 WorldMap.Blocked가 domeOpen으로 처리한다. 좌석 팁은 저장하지 않는다(원본과 같음).
public partial class DomeSystem : MonoBehaviour
{
    public static DomeSystem I;
    GameState G => GameState.I;
    void Awake()
    {
        I = this;
        for (int i = 0; i < seats.Length; i++) seats[i] = new Seat { p = WorldMap.SEATS[i] };
    }
    public bool Open => G != null && G.PadLv("dome") >= 1;

    class Seat
    {
        public Vector2 p; public bool used; public int tip, tipN;
        public Transform bench, pile; public GameObject[] coins;
    }
    readonly Seat[] seats = new Seat[6];
    Transform root, pad, padFloor, glassGroup, beacon, spout, glint;
    readonly List<Transform> props = new List<Transform>();
    readonly List<Material> owned = new List<Material>();
    Material glassMat, coinMat, lampMat, beaconMat, glowMat;
    Transform[] drops;
    float glintT, glintNext = 1f, beaconT;
    bool built;
    bool qaLog;

    const float K = GameConfig.K;
    const float PAD_TOP = 0.15f;                                   // 발판 윗면(논리): 구역 바닥(0.1)과 접지 그림자(0.2) 사이
    const float SIT_DY = 5.5f;                                     // 앉은 자리 = 좌석 중심 + (0, 5.5): 엉덩이가 좌판 앞 모서리에, 등이 등받이에 닿게
    static readonly Color WOOD = ModelLib.C(0x9a6240), WOOD_D = ModelLib.C(0x6e3f26), DARK = ModelLib.C(ModelLib.COL.dark);
    static readonly Color STEEL = ModelLib.C(ModelLib.COL.steel);
    static readonly Color[] CUSHION = { GameConfig.Hex("5ed6c8"), GameConfig.Hex("ff8c69") };   // 서쪽 줄 청록 · 동쪽 줄 산호
    // 맑은 유리(다듬기 3차): 가운데는 거의 비치고(알파 0.04) 가장자리만 옅은 하늘빛 테(Mats.Glass 프레넬). 예전 알파 0.3 투명 확산 재질은 햇빛을 받아 분홍 판처럼 보였다
    static readonly Color GLASS_DAY = new Color(0.94f, 0.98f, 1f, 0.04f), GLASS_NIGHT = new Color(0.8f, 0.92f, 1f, 0.07f), GLASS_EDGE = new Color(0.8f, 0.9f, 0.98f, 0.55f);

    // 돔 구역 북쪽 변(게이트가 있는 y0)
    static float ZoneY0 { get { foreach (var z in WorldMap.ZONES) if (z.key == "dome") return z.y0; return 740f; } }

    void Start()
    {
        qaLog = DevArgs.Has("-shot");
        G.OnPadLevel += OnPad;
        if (Open) Build(false);
    }

    void OnDestroy()
    {
        if (G != null) G.OnPadLevel -= OnPad;
        foreach (var m in owned) if (m != null) Destroy(m);
    }

    void OnPad(string id, int lv) { if (id == "dome" && lv >= 1 && !built) Build(true); }

    /* ================= 좌석 API (Customers가 호출) ================= */

    /// 빈 좌석을 예약한다. 없거나 돔이 닫혀 있으면 false. 원본 freeSeat처럼 SEATS 순서상 첫 빈 좌석(게이트 쪽 북쪽부터 차서 팁이 모인다)
    public bool TryReserveSeat(out int seat)
    {
        seat = -1;
        if (!Open) return false;
        if (!built) Build(false);
        for (int i = 0; i < seats.Length; i++) if (!seats[i].used) { seat = i; break; }
        if (seat < 0) return false;
        seats[seat].used = true;
        return true;
    }

    public void ReleaseSeat(int seat) { if (Valid(seat)) seats[seat].used = false; }

    /// 팁이 가장 많이 쌓인 좌석(없으면 -1) — QA 봇이 좌석 팁을 수거하러 들르는 데 쓴다
    public int RichestSeat(out int tip)
    {
        int best = -1; tip = 0;
        for (int i = 0; i < seats.Length; i++) if (seats[i].tip > tip) { tip = seats[i].tip; best = i; }
        return best;
    }

    /// 현재 위치에서 좌석까지 경유점(원본 seatPath): 줄 머리 → 레일 끝 밑(QUEUE_X,700) → 돔 게이트 밖·안(x=DOME.x-2)
    /// → 분수 옆 통로(x=DOME.x∓18) → 좌석 앞(s.y+10). 이미 레일 끝보다 아래면 첫 점은 건너뛴다
    public List<Vector2> SeatPath(int seat, Vector2 from)
    {
        var s = WorldMap.SEATS[Mathf.Clamp(seat, 0, 5)]; var D = WorldMap.DOME;
        float ax = D.x + (s.x < D.x ? -18f : 18f), gx = D.x - 2f, y0 = ZoneY0;
        var l = new List<Vector2>();
        if (from.y < 700f) l.Add(new Vector2(WorldMap.QUEUE_X, 700f));
        l.Add(new Vector2(gx, y0 - 18f)); l.Add(new Vector2(gx, y0 + 8f));
        l.Add(new Vector2(ax, Mathf.Min(s.y + 10f, D.y + 7f))); l.Add(new Vector2(ax, s.y + 10f));
        l.Add(new Vector2(s.x, s.y + 10f));
        return l;
    }

    /// 좌석에서 입구까지 경유점(원본 leavePath): 좌석 앞 → 통로 → 게이트 안·밖 → y=722를 따라 동쪽 →
    /// 돔(x1=196)과 우리 사이 x=ENTRY.x+20으로 내려가 착륙장(ENTRY.y)
    public List<Vector2> LeavePath(int seat)
    {
        var s = WorldMap.SEATS[Mathf.Clamp(seat, 0, 5)]; var D = WorldMap.DOME;
        float ax = D.x + (s.x < D.x ? -18f : 18f), gx = D.x - 2f, y0 = ZoneY0;
        return new List<Vector2>
        {
            new Vector2(s.x, s.y + 10f), new Vector2(ax, s.y + 10f), new Vector2(ax, Mathf.Min(s.y + 10f, D.y + 7f)),
            new Vector2(gx, y0 + 8f), new Vector2(gx, y0 - 18f),
            new Vector2(WorldMap.ENTRY.x + 20f, y0 - 18f), new Vector2(WorldMap.ENTRY.x + 20f, WorldMap.ENTRY.y),
        };
    }

    /// 손님이 앉는 자리(논리). 좌석 앞(s.y+10)에 도착한 손님이 뒤로 폴짝 올라앉는 목표점
    public Vector2 SitPoint(int seat) => WorldMap.SEATS[Mathf.Clamp(seat, 0, 5)] + new Vector2(0, SIT_DY);

    /// 손님이 앉는 순간: 벤치 스쿼시 + 쿠션 먼지
    public void OnSit(int seat)
    {
        if (!Valid(seat) || seats[seat].bench == null) return;
        JuiceFX.I.Squash(seats[seat].bench, 0.7f);
        JuiceFX.I.Dust(WorldMap.W2U(SitPoint(seat), 10f), 2);
    }

    /// 좌석에 팁을 남긴다(플레이어가 좌석 r34 안에 들어오면 수거). 여러 손님 팁은 쌓이고 동전 더미도 커진다
    public void LeaveTip(int seat, int amount)
    {
        if (!Valid(seat) || amount <= 0) return;
        if (!built) Build(false);
        var s = seats[seat]; bool was = s.tip > 0;
        s.tip += amount; s.tipN++;
        SyncCoins(s);
        s.pile.gameObject.SetActive(true);
        if (!was) JuiceFX.I.Pop(s.pile, 0.001f, 0.45f); else JuiceFX.I.Squash(s.pile, 0.8f);
        JuiceFX.I.CoinPop(s.pile.position + Vector3.up * 0.25f, 3);
        AudioManager.I.Play("coin", 1.45f, 0.35f);
    }

    bool Valid(int seat) => seat >= 0 && seat < seats.Length && seats[seat] != null;

    /* ================= 조립 (원본 bDome) ================= */

    static Vector3 L(float dx, float dy, float h) => new Vector3(dx, h, -dy) * K;   // 돔 중심 기준 논리 오프셋 → 로컬

    Material Own(Material m) { owned.Add(m); return m; }
    Material Emissive(Color c, Color em)
    {
        var b = Resources.Load<Material>("Mats/Emissive");
        var m = Own(b != null ? new Material(b) : new Material(Mats.Solid(c)));
        m.color = c; m.SetColor("_EmissionColor", em);
        return m;
    }
    Transform Group(Transform parent, string name, Vector3 local)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t;
    }

    void Build(bool animate)
    {
        if (built) return; built = true;
        var D = WorldMap.DOME; float R = WorldMap.DOME_R;
        root = new GameObject("dome").transform; root.SetParent(transform, false); root.position = WorldMap.W2U(D);

        // 바닥 발판: platform_large r84×1.04, 높이 18(=1.8), 흰→모래·밝은 회색→황갈. 윗면이 PAD_TOP에 오게 내린다(접지 그림자 안 가리게)
        // 발판 모델(Kenney platform_large)은 SK(orange) 팔레트라 살구색으로 칠해져 유리 너머가 분홍 판처럼 보였다(다듬기 3차) → 숨기고 크림 원판 + 옅은 테로 바닥을 깐다(target_south_v1).
        // KayKit 통일(r7)에선 숨긴 모델을 아예 만들지 않는다(GLB 도 빌드에서 뺐다)
        if (!KK.On)
        {
            var re = ModelLib.SK(ModelLib.COL.orange); re[0xedf0f5] = 0xefe3cf; re[0xd6dbe4] = 0xdcc8aa;
            pad = ModelLib.Put(root, "pad", R * 1.04f, 18f, R * 1.04f, 0, PAD_TOP - 1.8f, 0, 0f, re).transform;
            var pr = pad.GetComponent<Renderer>(); if (pr != null) pr.enabled = false;
        }
        padFloor = Prim.Cyl(root, "padFloor", L(0, 0, PAD_TOP), R * 2.02f, 0.04f, GameConfig.Hex("efe4d2")).transform;
        Prim.NoShadow(padFloor.gameObject); padFloor.GetComponent<Renderer>().receiveShadows = true;
        var rimIn = Prim.Cyl(padFloor, "padRim", new Vector3(0, -0.2f, 0), 1.03f / GameConfig.K, 0.0f, GameConfig.Hex("d8c6aa"));   // 부모 스케일 기준
        rimIn.transform.localScale = new Vector3(1.035f, 0.6f, 1.035f); Prim.NoShadow(rimIn);
        // 밤 실내등 바닥빛(원판, 밤에만 보임)
        var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(glow.GetComponent<Collider>()); glow.name = "glow";
        glow.transform.SetParent(root, false); glow.transform.localPosition = L(0, 0, 0.18f); glow.transform.localScale = new Vector3(R * 1.8f, 0.1f, R * 1.8f) * K;
        var gc = GameConfig.Hex("ffd98a"); gc.a = 0f; glowMat = Own(Mats.Trans(gc)); glow.GetComponent<Renderer>().sharedMaterial = glowMat; Prim.NoShadow(glow);

        // 좌석 6: 나무 벤치(옆판 다리·좌판·쿠션·등받이). 손님 엉덩이 높이 = 좌판 윗면 11
        for (int i = 0; i < seats.Length; i++)
        {
            var s = seats[i]; float dx = s.p.x - D.x, dy = s.p.y - D.y;
            var b = Group(root, "bench" + i, L(dx, dy, 0)); s.bench = b;
            if (KK.On) KkSeat(b, i, s.p.x < D.x);   // KayKit 통일(r7): Restaurant 의자 + 작은 둥근 탁자(DomeSystemKK.cs)
            else
            {
            Prim.Box(b, "legL", L(-10, 0, 4), new Vector3(3, 8, 10), WOOD_D);
            Prim.Box(b, "legR", L(10, 0, 4), new Vector3(3, 8, 10), WOOD_D);
            Prim.Box(b, "seat", L(0, 0, 9), new Vector3(24, 4, 11), WOOD);
            Prim.Box(b, "cushion", L(0, 1, 11.7f), new Vector3(20, 1.4f, 8), CUSHION[s.p.x < D.x ? 0 : 1]);
            Prim.Box(b, "back", L(0, -6.5f, 17f), new Vector3(24, 10, 3), WOOD);
            Prim.Box(b, "backTop", L(0, -6.5f, 22.6f), new Vector3(26, 1.6f, 4), WOOD_D);
            }
            props.Add(b);
            // 팁 동전 더미(원본: 좌석 오른쪽 앞 (s.x+14, s.y+8), 금화 3장 발광 .5) — 팁이 쌓이면 6장까지
            var pile = Group(root, "tip" + i, L(dx + 14, dy + 8, PAD_TOP)); s.pile = pile;
            var at = new[] { new Vector3(0, 1, 0), new Vector3(2, 2.7f, 1.5f), new Vector3(-1, 4.4f, 0.5f), new Vector3(-7, 1, -3), new Vector3(-6, 2.7f, -2), new Vector3(5, 1, -5) };
            s.coins = new GameObject[at.Length];
            if (coinMat == null) coinMat = Emissive(ModelLib.C(ModelLib.COL.gold), GameConfig.Hex("ffd34d") * 0.5f);
            for (int c = 0; c < at.Length; c++)
            {
                var co = Prim.Cyl(pile, "coin", L(at[c].x, at[c].z, at[c].y), 10, 1.6f, GameConfig.GOLD);
                co.GetComponent<Renderer>().sharedMaterial = coinMat; co.transform.localRotation = Quaternion.Euler(c * 7f, c * 40f, c * 5f);
                s.coins[c] = co;
            }
            pile.gameObject.SetActive(false);
            // 금액 칩은 두지 않는다: 동전 더미로 보인다 (jake 피드백 10/24)
        }

        // 화단 2 (Kenney 나무 타일) — 나무 상자 38×6×38 위. 원본 gardenRe는 20896b(원뿔 나무 옆면)를 황갈로 바꿔 나무가 갈색 원뿔로 보여서
        // 온실답게 초록 3톤으로 바꿨다(옆면 숲색·윗면 밝은 초록·중간 2톤), 흙·줄기는 원본처럼 나무색
        var gardenRe = new Dictionary<int, int> { { 0x20896b, 0x3d8f45 }, { 0x27906f, 0x2f7a33 }, { 0x2e9872, 0x4aa84e }, { 0x5ac487, 0x72d46a }, { 0xea9168, 0x9a6240 }, { 0xb66545, 0x6e3f26 }, { 0xc1c1d8, ModelLib.COL.steel } };
        foreach (int sx in new[] { -58, 58 })
        {
            var bed = Group(root, sx < 0 ? "bedW" : "bedE", L(sx, 22, 0));
            if (KK.On) KkBed(bed, sx < 0);   // KayKit 나무 상자 화분 + 우리 통통한 나무(DomeSystemKK.cs)
            else
            {
                Prim.Box(bed, "box", L(0, 0, 3), new Vector3(38, 6, 38), WOOD);
                ModelLib.Put(bed, sx < 0 ? "treesTall" : "trees", 34, 34, 34, 0, 6, 0, sx < 0 ? 0f : Mathf.PI / 2, gardenRe);
            }
            props.Add(bed);
        }

        // 분수 (원본 fountRe) + 물줄기·물방울 8·반짝임
        // 원본 fountRe보다 한 단계 밝게(유리 아래에서 검은 통처럼 보여서): 수반 강철·물은 밝은 하늘색
        var fountRe = new Dictionary<int, int> { { 0x8f95b3, ModelLib.COL.steel }, { 0x989fbe, 0xe4e9ee }, { 0x7d829c, ModelLib.COL.steel }, { 0x5a6078, ModelLib.COL.steelD }, { 0x81869c, ModelLib.COL.steel }, { 0x89afe5, 0x6fc3f0 }, { 0xbcd8f8, 0xd6efff }, { 0xc5dffb, 0xd6efff } };
        var fo = Group(root, "fountain", L(0, 30, 0));
        if (KK.On) KkFountain(fo);   // KayKit 그릇 수반(DomeSystemKK.cs)
        else ModelLib.Put(fo, "fountain", 30, 30, 30, 0, PAD_TOP - 1.45f, 0, 0f, fountRe);   // 바닥 포장 판(두께 1.5)을 발판과 맞춰 묻는다: 얇은 단차에 외곽선 줄무늬가 생겼다
        var water = new Color(0.8f, 0.95f, 1f, 0.85f);
        var sGo = Prim.Cyl(fo, "spout", L(0, 0, 19), 4f, 8, Color.white); sGo.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(water); Prim.NoShadow(sGo); spout = sGo.transform;
        drops = new Transform[8];
        for (int i = 0; i < drops.Length; i++)
        {
            var d = Prim.Sphere(fo, "drop", L(0, 0, 18), 4f, Color.white); d.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(new Color(0.9f, 0.98f, 1f, 0.95f)); Prim.NoShadow(d); drops[i] = d.transform;
        }
        var gl = Prim.Sphere(fo, "glint", L(0, 0, 8), 3.5f, Color.white); gl.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(new Color(1f, 1f, 1f, 0.95f)); Prim.NoShadow(gl); glint = gl.transform; glint.localScale = Vector3.zero;
        props.Add(fo);

        // 가운데 가로등은 없앴다(jake 10/25 "돔 안 디자인 이상해": 분수 위를 가리는 기둥). 밤 발광 재질만 남겨 둔다
        lampMat = Emissive(GameConfig.Hex("fff2c0"), GameConfig.Hex("ffe9a0") * 0.4f);

        // 에어록 (corridor_end 30×34×30, 게이트 서쪽 옆, ry=π) + 지붕 신호등(손님이 게이트를 지나면 깜빡)
        var al = Group(root, "airlock", L(-55, -R - 4, 0));
        if (KK.On) KkAirlock(al);   // KayKit 통로(tunnel_straight_A)를 남북으로(DomeSystemKK.cs)
        else ModelLib.Put(al, "airlock", 30, 34, 30, 0, 0, 0, Mathf.PI, ModelLib.SK(ModelLib.COL.orange));
        beaconMat = Emissive(GameConfig.Hex("7df0a0"), GameConfig.Hex("3cf07a") * 0.3f);
        var bc = Prim.Sphere(al, "beacon", L(0, 0, KK.On ? 16 : 36), 5, GameConfig.Hex("7df0a0")); bc.GetComponent<Renderer>().sharedMaterial = beaconMat; Prim.NoShadow(bc); beacon = bc.transform;
        props.Add(al);

        // 유리 반구(아래 반은 지면이 가린다) + 바닥 테 + 유리 반사 줄 2
        glassGroup = Group(root, "glassDome", Vector3.zero);
        var glass = Prim.Sphere(glassGroup, "glass", Vector3.zero, R * 2f, Color.white);
        glassMat = Own(Mats.Glass(GLASS_DAY, GLASS_EDGE, 3f, 0.7f, 160f)); glass.GetComponent<Renderer>().sharedMaterial = glassMat; Prim.NoShadow(glass);
        // 갈비대(X자)·허리띠(원)는 없앴다: 위에서 내려다보면 철창처럼 돔 안을 가로질러 어수선했다(jake 10/25). 바닥 테 + 유리 반사만
        MeshGo(glassGroup, "rim", Tube(R * 1.01f * K, 2.4f * K, 0f, Mathf.PI * 2f, 48, 6), Mats.Solid(STEEL), new Vector3(0, 1.2f * K, 0), Quaternion.Euler(90, 0, 0), true);
        var shine = Mats.TransShared(new Color(1f, 1f, 1f, 0.7f));
        Highlight(R * 1.02f, 52f, 196f, 248f, 2.6f, shine);
        Highlight(R * 1.02f, 35f, 210f, 234f, 2f, shine);

        if (animate) StartCoroutine(Reveal());
    }

    // 위도 el(°)의 고리 위 방위 a0~a1(°, +x→+z) 구간 반사 줄. 반사·하이라이트는 외곽선이 안 생기게 투명
    void Highlight(float r, float el, float a0, float a1, float thick, Material m)
    {
        float e = el * Mathf.Deg2Rad;
        var mesh = Tube(r * Mathf.Cos(e) * K, thick * K, a0 * Mathf.Deg2Rad, a1 * Mathf.Deg2Rad, 12, 6);
        MeshGo(glassGroup, "shine", mesh, m, new Vector3(0, r * Mathf.Sin(e) * K, 0), Quaternion.Euler(90, 0, 0), false);
    }

    static GameObject MeshGo(Transform parent, string name, Mesh mesh, Material m, Vector3 pos, Quaternion rot, bool shadow)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = rot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
        mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = shadow;
        return go;
    }

    // 원호 관 메시: XY 평면 위 중심 원점·반지름 R의 원호(+x에서 +y 방향 a0~a1 rad)를 따라가는 반지름 r 관. 부드러운 법선
    static Mesh Tube(float R, float r, float a0, float a1, int seg, int sides)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)seg);
            var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            for (int j = 0; j <= sides; j++)
            {
                float b = j * Mathf.PI * 2f / sides;
                var nn = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                v.Add(c * R + nn * r); n.Add(nn);
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                t.Add(a); t.Add(b); t.Add(a + 1);          // Cross(b-a, c-a)가 바깥을 향하는 감김(Unity 앞면)
                t.Add(a + 1); t.Add(b); t.Add(b + 1);
            }
        var m = new Mesh { name = "tube" };
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    // 해제 연출: 발판 팝(구역 바닥과 같은 0.7s·되튐 .35) → 소품이 차례로 퐁퐁 → 유리 반구가 크게 튀어 오르며 덮임 + 휙·딩·링
    IEnumerator Reveal()
    {
        foreach (var p in props) p.gameObject.SetActive(false);
        glassGroup.gameObject.SetActive(false);
        JuiceFX.I.Pop(padFloor, 0.001f, 0.7f, 0.35f);
        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i];
            p.gameObject.SetActive(true); JuiceFX.I.Pop(p, 0.001f, 0.45f);
            if (i % 2 == 0) AudioManager.I.Play("pop", 0.9f + i * 0.05f, 0.3f);
            JuiceFX.I.Dust(p.position + Vector3.up * 0.1f, 2);
            yield return new WaitForSeconds(0.06f);
        }
        yield return new WaitForSeconds(0.12f);
        glassGroup.gameObject.SetActive(true);
        JuiceFX.I.Pop(glassGroup, 0.001f, 0.85f, 2.6f);
        AudioManager.I.Play("whoosh", 0.75f, 0.8f); AudioManager.I.Play("ding", 1.25f, 0.55f);
        JuiceFX.I.Ring(root.position + Vector3.up * 0.06f, new Color(0.75f, 0.92f, 1f), 80);
        JuiceFX.I.Shake(3); JuiceFX.I.Vibrate(20);
        beaconT = 1.6f;
    }

    /* ================= 매 프레임: 분수·불빛·에어록 신호·팁 수거 ================= */

    void Update()
    {
        if (!built) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX), t = Time.time;
        float night = DayNight.I != null ? DayNight.I.night : 0f;

        // 분수(모델 30: 가운데 기둥 높이 16, 수면 높이 5.6·반지름 4.9~8.2): 물줄기 맥동, 물방울 8개가 기둥 꼭대기에서
        // 포물선으로 퍼져 수면에 떨어짐(위상 제각각), 수면 반짝임
        float sh = 8f + Mathf.Sin(t * 7f) * 1.6f;
        spout.localScale = new Vector3(4f, sh / 2f, 4f) * K; spout.localPosition = L(0, 0, 16f + sh / 2f);
        for (int i = 0; i < drops.Length; i++)
        {
            float k = Mathf.Repeat(t * 0.85f + i * 0.37f, 1f), a = i * Mathf.PI * 2f / drops.Length + t * 0.25f;
            float r = 1f + 6.5f * k, h = 17f + 12f * k - 23.5f * k * k;
            drops[i].localPosition = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r) * K;
            drops[i].localScale = Vector3.one * 4f * (1f - 0.45f * k) * K;
        }
        glintT += dt;
        if (glintT >= glintNext) { glintT = 0; glintNext = Random.Range(0.8f, 1.6f); float ga = Random.value * Mathf.PI * 2f, gr = Random.Range(5.2f, 7.8f); glint.localPosition = L(Mathf.Cos(ga) * gr, Mathf.Sin(ga) * gr, 6.2f); }
        float gk = glintT / 0.35f; glint.localScale = Vector3.one * (gk < 1f ? Mathf.Sin(Mathf.PI * gk) * 3.5f : 0f) * K;

        // 낮밤: 유리 은은한 청록 발광, 실내 바닥빛, 가로등
        glassMat.color = Color.Lerp(GLASS_DAY, GLASS_NIGHT, night);
        var gc = glowMat.color; gc.a = night * 0.3f; glowMat.color = gc;
        lampMat.SetColor("_EmissionColor", GameConfig.Hex("ffe9a0") * (0.4f + 1.2f * night));
        coinMat.SetColor("_EmissionColor", GameConfig.Hex("ffd34d") * (0.45f + 0.25f * Mathf.Sin(t * 4f)));

        // 에어록 신호등: 게이트(DOME.x-2, y0) 근처에 손님·플레이어가 있으면 빠르게 깜빡
        var gate = new Vector2(WorldMap.DOME.x - 2f, ZoneY0);
        bool near = Player.I != null && (Player.I.pos - gate).sqrMagnitude < 26f * 26f;
        if (!near && CustomerSystem.I != null) foreach (var c in CustomerSystem.I.list) if ((c.pos - gate).sqrMagnitude < 26f * 26f) { near = true; break; }
        if (near) beaconT = 0.6f;
        if (beaconT > 0) beaconT -= dt;
        bool on = beaconT > 0 ? Mathf.Repeat(t * 6f, 1f) < 0.5f : true;
        beaconMat.SetColor("_EmissionColor", GameConfig.Hex("3cf07a") * (beaconT > 0 ? (on ? 2.2f : 0.15f) : 0.35f + 0.5f * night));
        if (beaconT > 0) beacon.localScale = Vector3.one * 5f * (on ? 1.15f : 1f) * K; else beacon.localScale = Vector3.one * 5f * K;

        // §5.2 좌석 팁 수거: 플레이어가 좌석 r34 안이면 즉시(쿨다운 없음)
        if (Player.I != null)
            for (int i = 0; i < seats.Length; i++)
                if (seats[i].tip > 0 && (Player.I.pos - seats[i].p).sqrMagnitude < WorldMap.SEAT_R * WorldMap.SEAT_R) Collect(i);
    }

    void SyncCoins(Seat s)
    {
        int n = Mathf.Min(s.coins.Length, 2 + s.tipN);
        for (int i = 0; i < s.coins.Length; i++) s.coins[i].SetActive(i < n);
    }

    void Collect(int i)
    {
        var s = seats[i]; int n = s.tip; s.tip = 0; s.tipN = 0;
        Vector3 from = s.pile.position + Vector3.up * 0.2f;
        s.pile.gameObject.SetActive(false);
        G.AddBells(n);   // 저장·종료 안전: 즉시 가산, 비행은 연출
        StartCoroutine(CoFlyCoins(from, 3));
        var cam = Camera.main;
        if (cam != null && HUD.I != null) HUD.I.Coins(cam.WorldToScreenPoint(from), () => HUD.I.BellsScreen(), 5);
        if (WorldLabels.BigMoney(n)) WorldLabels.I.Float(WorldMap.W2U(s.p.x, s.p.y - 10f, 30f), "+" + n, true, false);   // 작은 팁은 동전만
        JuiceFX.I.Ring(from, GameConfig.GOLD, 16);
        AudioManager.I.Play("coin", 1.25f, 0.7f); AudioManager.I.Play("pick", 1.5f, 0.4f);
        JuiceFX.I.Vibrate(10);
        if (qaLog) Debug.Log("dome tip seat=" + i + " +" + n + " bells=" + G.S.bells.ToString("0"));
    }

    IEnumerator CoFlyCoins(Vector3 from, int n)
    {
        for (int j = 0; j < n; j++)
        {
            int idx = j;
            ArcFlyer.I.FlyItem("coin", from, () => Player.I.body.position + Vector3.up * 1.4f, () =>
            {
                AudioManager.I.PlayHz("coin", 1100f * Mathf.Pow(1.08f, idx), 1100f, 0.45f);
                if (idx == 0 && Player.I != null) JuiceFX.I.Squash(Player.I.body, 0.5f);
            }, 0.35f, 36f);
            yield return new WaitForSeconds(0.05f);
        }
    }
}

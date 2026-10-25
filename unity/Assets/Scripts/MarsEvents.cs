using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 화성 이벤트: 가끔 일어나는 작은 사건 3종으로 하루에 변화를 준다.
//  · 조건: 튜토리얼이 끝나고(S.tut ≥ 3) 판매 20 이후. 220~380초마다 한 번 판정해 75%로 발생(화성 하루 300초에 대략 0~1번), 같은 이벤트 연속 금지, 한 번에 하나.
//  · 먼지 폭풍(40~60초): 화면이 살짝 탁해지고(PostFX 채도·대비·비네트 소폭, 해 조금 흐리게, 번쩍임 없음) 지면 가까이 옅은 먼지 띠 몇 줄이 화면 위·아래 가장자리로 흐른다.
//    태양광 발전·충전 정지(Frontier 전력 배율 1), 플레이어 이동 −15%(GameState.Speed). 시작 4초·끝 6초에 걸쳐 서서히. 시작·끝에 관제사 무전 한 줄.
//  · 보급선: 착륙장 북서쪽에 작은 보급 캡슐이 낙하산으로 내려앉는다. 닿으면 벨 + 물통 8개가 등짐으로(못 담는 몫은 벨로),
//    토양 공장이 있으면 반은 레골리스 묶음 8개가 토양 정화기로 바로 날아간다(등짐에 실으면 북쪽 생산 체인으로 끌려가 가게가 빈다). 60초 지나면 사라짐.
//  · 운석: 채굴장·개척지 등 열린 북쪽 구역(없으면 기지 근처) 빈 땅에 작은 운석이 떨어져(약한 흔들림) 얼음 박힌 덩어리가 남는다.
//    닿으면 얼음 16장이 해빙기로(해빙기 큐가 이미 많으면 벨). 90초 뒤 사라짐.
// 보급 캡슐·운석은 HintSystem 우선순위 위쪽에 잠깐 끼어 화살표·링으로만 위치를 알린다(라벨·말풍선 없음).
// 진행 중 이벤트는 저장하지 않는다(앱을 다시 켜면 없음). 저장은 다음 판정까지 남은 초 S.evNext 하나.
// 검증 인자: -event storm|supply|meteor [초] (그 초에 바로 발생, 조건 무시. 초 생략 시 1초)
public class MarsEvents : MonoBehaviour
{
    public static MarsEvents I;
    GameState G => GameState.I;
    SaveData S => G.S;

    // ---- 다른 시스템이 읽는 값 ----
    public static float StormK => I != null ? I.stormK : 0f;    // 0 맑음 ~ 1 폭풍 한가운데(서서히 오르내림)
    public static float MoveMul => 1f - 0.15f * StormK;          // 플레이어 이동 배율
    /// 바닥에 주울 이벤트 물건(보급 캡슐·얼음 운석)이 있으면 그 자리
    public bool PickupTarget(out Vector2 p)
    {
        p = cur != null ? cur.pos : Vector2.zero;
        return cur != null && cur.ready && !cur.done;
    }

    // ---- 수치 ----
    const int MIN_SOLD = 20;
    const float GAP_MIN = 220f, GAP_MAX = 380f, CHANCE = 0.75f;
    const float STORM_MIN = 40f, STORM_MAX = 60f, STORM_IN = 4f, STORM_OUT = 6f;
    const float SUPPLY_LIFE = 60f, METEOR_LIFE = 90f, R_PICK = 24f;
    const int SUPPLY_N = 8, METEOR_ICE = 16;
    static readonly string[] KINDS = { "storm", "supply", "meteor" };
    static string lastKind = "";   // 같은 이벤트 연속 금지(앱 실행 동안)

    // 착륙장 원(ENTRY r58)의 북서쪽 가장자리: 착륙선(84,988)과 손님 길(x 206 북쪽) 사이 빈 땅. 화면 맨 아래 업그레이드 버튼에 가리지 않게 조금 북쪽
    static readonly Vector2 SUPPLY_POS = new Vector2(162, 954);
    // 운석 후보: 열린 북쪽 구역 안 빈 땅(얼음·레골리스 더미·시설·집 사이) → 없으면 기지 근처 빈 땅
    static readonly (string zone, Vector2 p)[] METEOR_SPOTS =
    {
        ("mine", new Vector2(218, -100)), ("mine", new Vector2(150, -30)), ("mine", new Vector2(140, -110)), ("mine", new Vector2(30, -66)),
        ("o2", new Vector2(286, -150)), ("o2", new Vector2(362, -20)),
        ("hab", new Vector2(222, -598)), ("hab", new Vector2(30, -600)), ("hab", new Vector2(70, -430)),
        ("solar", new Vector2(338, -516)), ("solar", new Vector2(276, -510)),
    };
    static readonly Vector2[] METEOR_BASE = { new Vector2(212, 290), new Vector2(40, 680), new Vector2(232, 712), new Vector2(30, 300) };

    // ---- 상태 ----
    bool storming, stormEndSaid; float stormT, stormLen, stormK;
    class Pickup { public string kind, reward; public Vector2 pos; public Transform root, lid; public float t, life; public bool ready, done; public readonly List<Material> sparkle = new List<Material>(); }
    Pickup cur;
    public bool Active => storming || cur != null;
    static bool argDone; string devKind; float devAt, devT;
    bool qaLog;
    readonly List<Material> owned = new List<Material>();

    void Awake() { I = this; }

    void Start()
    {
        qaLog = DevArgs.Has("-shot");
        if (S.evNext <= 0f) S.evNext = Random.Range(120f, 240f);   // 처음엔 조건을 갖춘 뒤 2~4분
        if (!argDone)
        {
            argDone = true;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-event")
                {
                    devKind = a[i + 1]; devAt = 1f;
                    if (i + 2 < a.Length && float.TryParse(a[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) devAt = f;
                }
        }
    }

    void OnDestroy()
    {
        RestoreFX();
        foreach (var m in owned) if (m != null) Destroy(m);
        if (I == this) I = null;
    }

    // ================= 판정 =================
    bool Eligible => S.tut >= 3 && S.sold >= MIN_SOLD;
    bool Busy => (StoryManager.I != null && StoryManager.I.IntroActive) || (PrestigeManager.I != null && PrestigeManager.I.Busy);

    void Update()
    {
        if (G == null) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        if (devKind != null) { devT += dt; if (devT >= devAt) { Begin(devKind); devKind = null; } }
        else if (Eligible && !Active)
        {
            S.evNext -= dt;
            if (S.evNext <= 0f)
            {
                if (Busy) S.evNext = 10f;   // 인트로·이주 연출 중이면 조금 뒤 다시
                else
                {
                    S.evNext = Random.Range(GAP_MIN, GAP_MAX);
                    if (Random.value < CHANCE) Begin(PickKind());
                    else if (qaLog) Debug.Log("event: 이번 판정은 조용히 넘어감, 다음 " + S.evNext.ToString("0") + "초 뒤");
                }
            }
        }
        UpdateStorm(dt);
        UpdatePickup(dt);
    }

    string PickKind()
    {
        var l = new List<string>(); foreach (var k in KINDS) if (k != lastKind) l.Add(k);
        return l[Random.Range(0, l.Count)];
    }

    void Begin(string kind)
    {
        if (kind == "storm") { if (!storming) StartStorm(); }
        else if (kind == "supply") { if (cur == null) StartCoroutine(CoSupply()); }
        else if (kind == "meteor") { if (cur == null) StartCoroutine(CoMeteor()); }
        else { Debug.LogWarning("event: 모르는 이벤트 " + kind); return; }
        lastKind = kind;
        Debug.Log("event: " + kind + " 시작 (sold=" + S.sold + " day=" + S.day + ")");
    }

    static void Radio(string id, float delay) => StoryManager.I?.Push(id, delay, true);   // 이벤트마다 반복되는 무전(S.story에 안 남김)

    // ================= 먼지 폭풍 =================
    void StartStorm()
    {
        storming = true; stormEndSaid = false; stormT = 0; stormLen = Random.Range(STORM_MIN, STORM_MAX);
        Radio("ev_storm", 0.3f);
        AudioManager.I?.Play("whoosh", 0.55f, 0.5f);
    }

    void UpdateStorm(float dt)
    {
        if (!storming && stormK <= 0f) { if (bands.Count > 0) UpdateBands(dt, 0f); return; }
        stormT += dt;
        bool on = storming && stormT < stormLen;
        if (storming && !on && !stormEndSaid) { stormEndSaid = true; Radio("ev_storm_end", 1.5f); }
        stormK = Mathf.MoveTowards(stormK, on ? 1f : 0f, dt / (on ? STORM_IN : STORM_OUT));
        if (!on && stormK <= 0f) { storming = false; if (qaLog) Debug.Log("event: storm 끝"); }
        UpdateBands(dt, Mathf.SmoothStep(0f, 1f, stormK));
    }

    // 화면 톤: DayNight(Update)가 매 프레임 해·배경을 다시 정하므로 LateUpdate에서 그 위에 살짝 덮는다
    static readonly Color DUST_VIG = new Color(70 / 255f, 30 / 255f, 8 / 255f), DUST_SUN = new Color(1f, 0.88f, 0.74f), DUST_BG = GameConfig.Hex("b9805a");
    bool fxSaved; float baseSat, baseCon, baseVig; Color baseVigC;
    void LateUpdate()
    {
        float k = Mathf.SmoothStep(0f, 1f, stormK);
        var fx = PostFX.I;
        if (fx != null)
        {
            if (k > 0f && !fxSaved) { fxSaved = true; baseSat = fx.saturation; baseCon = fx.contrast; baseVig = fx.vignette; baseVigC = fx.vignetteColor; }
            if (fxSaved)
            {
                if (k <= 0f) RestoreFX();
                else
                {
                    fx.saturation = Mathf.Lerp(baseSat, baseSat * 0.84f, k);
                    fx.contrast = Mathf.Lerp(baseCon, baseCon * 0.95f, k);
                    fx.vignette = Mathf.Lerp(baseVig, baseVig + 0.14f, k);
                    fx.vignetteColor = Color.Lerp(baseVigC, DUST_VIG, k);
                }
            }
        }
        if (k > 0f)
        {
            if (World.sun != null) { World.sun.intensity *= 1f - 0.16f * k; World.sun.color = Color.Lerp(World.sun.color, World.sun.color * DUST_SUN, k); }
            if (CameraRig.I != null) CameraRig.I.cam.backgroundColor = Color.Lerp(CameraRig.I.cam.backgroundColor, DUST_BG, 0.35f * k);
        }
    }
    void RestoreFX()
    {
        if (!fxSaved) return;
        fxSaved = false;
        var fx = PostFX.I; if (fx == null) return;
        fx.saturation = baseSat; fx.contrast = baseCon; fx.vignette = baseVig; fx.vignetteColor = baseVigC;
    }

    // 먼지 띠: 땅에 눕힌 옅은 타원 몇 장(최대 6)이 서풍을 타고 화면 위·아래 가장자리를 가로지른다. 입자 대신 넓고 흐린 띠라 어수선하지 않다
    class Band { public Transform t; public Material m; public float x, y, h, w, speed, age, life, peak; }
    readonly List<Band> bands = new List<Band>();
    const int MAX_BANDS = 6;
    float bandSpawnT;
    static Texture2D bandTex;
    static readonly Color DUST_COL = GameConfig.Hex("ecc39c");

    void UpdateBands(float dt, float k)
    {
        var rig = CameraRig.I;
        if (rig != null && k > 0.05f)
        {
            bandSpawnT -= dt;
            if (bandSpawnT <= 0f && bands.Count < Mathf.RoundToInt(MAX_BANDS * k)) { bandSpawnT = Random.Range(0.6f, 1.4f); SpawnBand(rig); }
        }
        float dim = Mathf.Lerp(1f, 0.42f, DayNight.I != null ? DayNight.I.night : 0f);
        for (int i = bands.Count - 1; i >= 0; i--)
        {
            var b = bands[i]; b.age += dt; b.x += b.speed * dt;
            if (b.age >= b.life || b.t == null)
            {
                if (b.t != null) Destroy(b.t.gameObject);
                if (b.m != null) Destroy(b.m);
                bands.RemoveAt(i); continue;
            }
            float env = Mathf.Clamp01(b.age / 2f) * Mathf.Clamp01((b.life - b.age) / 2f);
            b.t.position = WorldMap.W2U(b.x, b.y, b.h);
            var c = DUST_COL * dim; c.a = b.peak * env * k; b.m.color = c;
        }
    }

    void SpawnBand(CameraRig rig)
    {
        float halfW = 210f / rig.Zoom, viewH = rig.ViewH;
        var b = new Band
        {
            w = Random.Range(170f, 260f), h = Random.Range(3f, 9f), speed = Random.Range(38f, 60f),
            life = Random.Range(7f, 10f), peak = Random.Range(0.42f, 0.58f),
        };
        // 위·아래 가장자리(가운데 플레이어 둘레는 비움)
        float side = Random.value < 0.5f ? -1f : 1f;
        b.y = rig.camY + side * viewH * 0.5f * Random.Range(0.5f, 0.92f);
        // 처음 몇 장은 화면 안에서 생기고(폭풍이 차오르는 동안), 그 뒤는 왼쪽 밖에서 들어온다
        b.x = bands.Count < 2 ? rig.camX + Random.Range(-halfW, halfW * 0.4f) : rig.camX - halfW - b.w * 0.3f + Random.Range(-20f, 40f);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = "dustBand";
        Destroy(go.GetComponent<Collider>()); go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, Random.Range(-4f, 4f));
        go.transform.localScale = new Vector3(b.w, b.w * Random.Range(0.18f, 0.26f), 1f) * GameConfig.K;
        // 바닥빛(ShopDecor)처럼 조명 없는 Sprites/Default + 부드러운 타원 텍스처. 밤엔 UpdateBands가 어둡게
        var sh = Shader.Find("Sprites/Default");
        b.m = sh != null ? new Material(sh) : Mats.Trans(Color.white);
        b.m.mainTexture = BandTex(); b.m.color = new Color(DUST_COL.r, DUST_COL.g, DUST_COL.b, 0f); b.m.renderQueue = 2995;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = b.m; Prim.NoShadow(go);
        b.t = go.transform; b.t.position = WorldMap.W2U(b.x, b.y, b.h);
        bands.Add(b);
    }

    // 가장자리로 갈수록 부드럽게 사라지는 타원(알파만)
    static Texture2D BandTex()
    {
        if (bandTex != null) return bandTex;
        const int W = 128, H = 48;
        bandTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                a *= 0.85f + 0.15f * Mathf.Sin(u * 7f + v * 3f);   // 아주 옅은 결
                px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
        bandTex.SetPixels(px); bandTex.Apply();
        return bandTex;
    }

    // ================= 주울 물건(보급 캡슐·얼음 운석) =================
    void UpdatePickup(float dt)
    {
        if (cur == null || !cur.ready || cur.done) return;
        cur.t += dt;
        // 반짝임: 얼음 결정의 발광이 천천히 오르내림(번쩍임 아님)
        foreach (var m in cur.sparkle) if (m != null) m.SetColor("_EmissionColor", GameConfig.ICE * (0.18f + 0.22f * (0.5f + 0.5f * Mathf.Sin(cur.t * 3f))));
        var pl = Player.I;
        if (pl != null && Vector2.Distance(pl.pos, cur.pos) < R_PICK) { cur.done = true; StartCoroutine(cur.kind == "supply" ? CoOpenSupply(cur) : CoTakeMeteor(cur)); return; }
        if (cur.t >= cur.life) { cur.done = true; StartCoroutine(CoVanish(cur)); }
    }

    static Vector3 L(float dx, float dy, float h) => new Vector3(dx, h, -dy) * GameConfig.K;   // 중심 기준 논리 오프셋(dy+ = 남쪽) → 로컬
    static readonly Color SHELL = MachineKit.SHELL, SHELL_D = MachineKit.SHELL_D, CORAL = GameConfig.Hex("f08a6a");

    Material Emissive(Color c, Color em)
    {
        var src = Resources.Load<Material>("Mats/Emissive");
        var m = src != null ? new Material(src) : new Material(Mats.Solid(c));
        m.color = c; m.SetColor("_EmissionColor", em); owned.Add(m); return m;
    }

    // 두 점을 잇는 가는 줄(낙하산 끈)
    static void Line(Transform parent, Vector3 a, Vector3 b, float thickL, Color c)
    {
        var go = Prim.Box(parent, "line", (a + b) * 0.5f, new Vector3(thickL, thickL, 1f), c);
        go.transform.localRotation = Quaternion.LookRotation(b - a);
        var s = go.transform.localScale; s.z = (b - a).magnitude; go.transform.localScale = s;
        Prim.NoShadow(go);
    }

    // ---- 보급선 ----
    IEnumerator CoSupply()
    {
        var p = new Pickup { kind = "supply", pos = SUPPLY_POS, life = SUPPLY_LIFE };
        bool rego = SettleStations.I != null && SettleStations.I.Open(1) && Random.value < 0.5f;   // 토양 공장이 있으면 반은 레골리스 묶음(정화기로)
        p.reward = rego ? "rego" : "bottle";
        cur = p;
        Radio("ev_supply", 0.2f);
        var root = new GameObject("supplyPod").transform; root.SetParent(transform, false); root.position = WorldMap.W2U(p.pos);
        p.root = root;
        var shadow = Prim.BlobShadow(root, 18); shadow.transform.localScale = Vector3.zero;
        var pod = new GameObject("pod").transform; pod.SetParent(root, false); pod.localScale = Vector3.one * 1.25f;
        // 캡슐: 짧은 다리 셋 → 흰 몸통 + 산호색 띠 + 둥근 지붕, 남쪽 면에 든 것의 색 창, 꼭대기 뚜껑
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 30f) * Mathf.Deg2Rad;
            Prim.Cyl(pod, "leg", L(Mathf.Sin(a) * 7f, Mathf.Cos(a) * 7f, 1.5f), 2.4f, 3f, SHELL_D);
        }
        Prim.Cyl(pod, "body", L(0, 0, 9), 16, 12, SHELL);
        Prim.Cyl(pod, "band", L(0, 0, 7), 16.8f, 3f, CORAL);
        var roof = Prim.Sphere(pod, "roof", L(0, 0, 15), 16, SHELL); roof.transform.localScale = new Vector3(16, 11, 16) * GameConfig.K;
        Prim.Box(pod, "window", L(0, 7.9f, 10.5f), new Vector3(6, 4.5f, 0.8f), Items.ColorOf(p.reward));
        p.lid = Prim.Cyl(pod, "lid", L(0, 0, 20.6f), 8, 1.6f, SHELL_D).transform;
        // 낙하산: 납작한 흰 돔 + 산호색 꼭지 + 끈 4줄
        var chute = new GameObject("chute").transform; chute.SetParent(pod, false); chute.localPosition = L(0, 0, 44);
        var canopy = Prim.Sphere(chute, "canopy", Vector3.zero, 36, SHELL); canopy.transform.localScale = new Vector3(36, 15, 36) * GameConfig.K;
        var cap = Prim.Sphere(chute, "cap", L(0, 0, 5f), 18, CORAL); cap.transform.localScale = new Vector3(18, 7, 18) * GameConfig.K;
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 45f) * Mathf.Deg2Rad;
            Line(chute, L(Mathf.Sin(a) * 15f, Mathf.Cos(a) * 15f, -3f), L(Mathf.Sin(a) * 4f, Mathf.Cos(a) * 4f, 20f - 44f), 0.7f, SHELL_D);
        }
        foreach (var r in chute.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 하강 2.6초(감속), 살짝 흔들림
        const float H0 = 300f, DUR = 2.6f; float el = 0;
        AudioManager.I?.Play("whoosh", 0.7f, 0.35f);
        while (el < DUR)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / DUR), e = 1f - (1f - k) * (1f - k);
            pod.localPosition = L(0, 0, H0 * (1f - e));   // (낙하산 끈 길이도 pod 배율을 따른다)
            pod.localRotation = Quaternion.Euler(Mathf.Sin(el * 2.3f) * 6f * (1f - k), 0, Mathf.Cos(el * 1.9f) * 5f * (1f - k));
            shadow.transform.localScale = new Vector3(18, 0.2f, 18) * GameConfig.K * Mathf.Lerp(0.3f, 1f, e);
            yield return null;
        }
        pod.localPosition = Vector3.zero; pod.localRotation = Quaternion.identity;
        JuiceFX.I.Squash(pod, 0.7f);
        JuiceFX.I.Dust(WorldMap.W2U(p.pos, 2f), 5);
        AudioManager.I?.Play("put", 0.7f, 0.6f);
        p.ready = true;
        if (qaLog) Debug.Log("event: supply 착륙 " + p.pos + " 보상=" + p.reward);
        // 낙하산은 바람에 밀려 눕듯이 접히며 사라짐
        el = 0; var c0 = chute.localPosition;
        while (el < 1.0f && chute != null)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / 1.0f);
            chute.localPosition = c0 + L(26f * k, -6f * k, -36f * k * k);
            chute.localRotation = Quaternion.Euler(0, 0, -70f * k);
            chute.localScale = Vector3.one * (1f - k * 0.9f);
            yield return null;
        }
        if (chute != null) Destroy(chute.gameObject);
    }

    IEnumerator CoOpenSupply(Pickup p)
    {
        var pl = Player.I; int price = G.BottlePrice;
        // 뚜껑이 톡 튀고 물통은 등짐으로, 레골리스는 토양 정화기 투입구로 날아간다(못 담으면 벨로)
        if (p.lid != null) StartCoroutine(CoFlyAway(p.lid, L(-6, 4, 26)));
        AudioManager.I?.Play("ding", 1.1f, 0.7f);
        JuiceFX.I.Vibrate(15);
        Vector3 from = p.root.position + L(0, 0, 18);
        int leftover = 0; var ss = SettleStations.I;
        for (int i = 0; i < SUPPLY_N; i++)
        {
            string k = p.reward;
            if (k == "rego")
            {
                if (ss != null && ss.Open(1) && S.purQ + ss.qPend[1] < GameConfig.SETTLE_Q_CAP)
                {
                    ss.qPend[1]++;
                    ArcFlyer.I.FlyItem("rego", from, () => ss.InTop(1), () => ss.Put(1), 0.9f, 140f);
                }
                else leftover++;
            }
            else if (pl != null && G.CanCarry(k))
            {
                G.pend++; G.pendK = k;
                var go = Items.CarryBlock(ArcFlyer.I.transform, k); go.transform.localScale = Vector3.one * 0.85f;
                ArcFlyer.I.Fly(go, from, pl.StackTop, GameConfig.FLY_T * 1.4f, 50f, () => ArriveItem(k));
            }
            else leftover++;
            yield return new WaitForSeconds(0.06f);
        }
        int bells = price * 6 + leftover * Mathf.Max(1, price / 2);
        GiveBells(bells, p.root.position + L(0, 0, 12));
        if (qaLog) Debug.Log("event: supply 획득 " + p.reward + " ×" + (SUPPLY_N - leftover) + " +" + bells + "벨");
        yield return CoShrink(p, 0.35f);
    }

    // ---- 운석 ----
    IEnumerator CoMeteor()
    {
        var p = new Pickup { kind = "meteor", pos = MeteorSpot(), life = METEOR_LIFE };
        cur = p;
        var root = new GameObject("meteor").transform; root.SetParent(transform, false); root.position = WorldMap.W2U(p.pos);
        p.root = root;
        // 떨어질 자리에 그림자가 먼저 커지고, 북서쪽 하늘에서 돌덩이가 비스듬히 내리꽂힌다(꼬리는 옅은 연기 몇 개)
        var shadow = Prim.BlobShadow(root, 22); shadow.transform.localScale = Vector3.zero;
        var rock = Prim.Sphere(transform, "fallingRock", WorldMap.W2U(p.pos, 400), 9, GameConfig.Hex("6b4636")).transform;
        Prim.NoShadow(rock.gameObject);
        AudioManager.I?.Play("whoosh", 0.45f, 0.5f);
        const float DUR = 0.9f; float el = 0, smokeT = 0;
        Vector3 a = WorldMap.W2U(p.pos.x - 110f, p.pos.y - 70f, 420f), b = WorldMap.W2U(p.pos, 4f);
        while (el < DUR)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / DUR), e = k * k;
            rock.position = Vector3.Lerp(a, b, e); rock.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0);
            shadow.transform.localScale = new Vector3(22, 0.2f, 22) * GameConfig.K * Mathf.Lerp(0.2f, 1f, k);
            smokeT -= Time.deltaTime;
            if (smokeT <= 0f) { smokeT = 0.05f; StartCoroutine(CoSmoke(rock.position)); }
            yield return null;
        }
        Destroy(rock.gameObject);
        // 착지: 약한 흔들림 + 흙먼지 + 옅은 고리, 움푹 팬 자국 위에 얼음 박힌 덩어리
        JuiceFX.I.Shake(1.6f);
        JuiceFX.I.Dust(WorldMap.W2U(p.pos, 2f), 8);
        JuiceFX.I.Ring(WorldMap.W2U(p.pos, 0.5f), GameConfig.Hex("e0b48a"), 18f);
        JuiceFX.I.Vibrate(20);
        AudioManager.I?.Play("put", 0.5f, 0.8f); AudioManager.I?.Play("pop", 0.55f, 0.6f);
        Destroy(shadow);
        var crater = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(crater.GetComponent<Collider>()); crater.name = "crater";
        crater.transform.SetParent(root, false); crater.transform.localPosition = L(0, 0, 0.15f); crater.transform.localScale = new Vector3(46, 0.2f, 34) * GameConfig.K;
        var cc = GameConfig.Hex("7a3a20"); cc.a = 0.32f; crater.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(cc); Prim.NoShadow(crater);
        var chunk = new GameObject("chunk").transform; chunk.SetParent(root, false); chunk.localScale = Vector3.one * 1.3f;
        Prim.Box(chunk, "rock", L(0, 0, 4), new Vector3(14, 8, 12), GameConfig.Hex("6b4636")).transform.localRotation = Quaternion.Euler(6, 20, -4);
        Prim.Box(chunk, "rock2", L(5, 2, 3), new Vector3(8, 6, 8), GameConfig.Hex("845642")).transform.localRotation = Quaternion.Euler(-8, 50, 6);
        Prim.Box(chunk, "rock3", L(-5, 3, 2.5f), new Vector3(7, 5, 6), GameConfig.Hex("5a3a2e")).transform.localRotation = Quaternion.Euler(4, -30, 8);
        // 얼음 결정 5개: 덩어리 위로 비스듬히 솟음, 발광이 천천히 오르내린다(UpdatePickup)
        var ice = Emissive(GameConfig.ICE, GameConfig.ICE * 0.2f); p.sparkle.Add(ice);
        float[][] XT = { new[] { -2f, -1f, 9f, 0f, -18f }, new[] { 3f, -3f, 8f, 25f, 15f }, new[] { 0f, 3f, 7f, -20f, 5f }, new[] { -5f, 1f, 6f, 10f, -35f }, new[] { 5f, 1f, 6.5f, -10f, 35f } };
        foreach (var x in XT)
        {
            var c = Prim.Box(chunk, "crystal", L(x[0], x[1], x[2] + 2f), new Vector3(3, 8, 3), GameConfig.ICE);
            c.transform.localRotation = Quaternion.Euler(x[3], 45, x[4]);
            c.GetComponent<Renderer>().sharedMaterial = ice;
        }
        JuiceFX.I.Pop(chunk, 0.3f, 0.4f);
        p.ready = true;
        if (qaLog) Debug.Log("event: meteor 착지 " + p.pos);
        Radio("ev_meteor", 0.4f);
    }

    IEnumerator CoSmoke(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>()); go.transform.SetParent(transform, false);
        var c = GameConfig.Hex("cfb39a"); c.a = 0.4f; var m = Mats.Trans(c); go.GetComponent<Renderer>().sharedMaterial = m; Prim.NoShadow(go);
        go.transform.position = at; float el = 0;
        while (el < 0.4f)
        {
            el += Time.deltaTime; float k = el / 0.4f;
            go.transform.localScale = Vector3.one * Mathf.Lerp(6f, 2f, k) * GameConfig.K;
            var cc = c; cc.a = 0.4f * (1f - k); m.color = cc; yield return null;
        }
        Destroy(m); Destroy(go);
    }

    // 열린 북쪽 구역(채굴장·산소동·개척지) 빈 땅 중 하나, 없으면 기지 근처. 막힌 땅·발판 자리는 빼고,
    // 떨어지는 모습이 보이기 쉽게 플레이어에게 가까운 세 곳 중에서 고른다
    Vector2 MeteorSpot()
    {
        var l = new List<Vector2>();
        foreach (var s in METEOR_SPOTS) if (WorldMap.openZones.Contains(s.zone) && FreeSpot(s.p)) l.Add(s.p);
        if (l.Count == 0) foreach (var p in METEOR_BASE) if (FreeSpot(p)) l.Add(p);
        if (l.Count == 0) l.Add(METEOR_BASE[0]);
        var me = Player.I != null ? Player.I.pos : WorldMap.PLAYER_START;
        l.Sort((a, b) => Vector2.Distance(a, me).CompareTo(Vector2.Distance(b, me)));
        return l[Random.Range(0, Mathf.Min(3, l.Count))];
    }
    bool FreeSpot(Vector2 p)
    {
        if (WorldMap.Blocked(p.x, p.y)) return false;
        foreach (var d in Pads.ALL) if (!d.menu && G.PadReq(d.id) && !G.PadMaxed(d.id) && Vector2.Distance(d.pos, p) < 40f) return false;   // 보이는 발판 위는 피함
        return true;
    }

    IEnumerator CoTakeMeteor(Pickup p)
    {
        AudioManager.I?.Play("ding", 1.25f, 0.7f);
        JuiceFX.I.Vibrate(15);
        Vector3 from = p.root.position + L(0, 0, 10);
        var st = Stations.I;
        // 해빙기 큐가 넉넉하면 얼음이 해빙기로 날아가고(2장씩 8번), 이미 많으면 벨
        if (st != null && S.meltQ < 24)
        {
            for (int i = 0; i < METEOR_ICE / 2; i++)
            {
                ArcFlyer.I.FlyItem("ice", from, st.MeltTop, () => { S.meltQ += 2; }, 0.9f, 140f);
                AudioManager.I?.PlayHz("pick", 600 + 50 * i, 520, 0.5f);
                yield return new WaitForSeconds(0.07f);
            }
            if (qaLog) Debug.Log("event: meteor 획득 얼음 " + METEOR_ICE + "장 → 해빙기");
        }
        else
        {
            int bells = G.BottlePrice * 8;
            GiveBells(bells, from);
            if (qaLog) Debug.Log("event: meteor 획득 +" + bells + "벨");
        }
        yield return CoShrink(p, 0.4f);
    }

    // ---- 공통 ----
    void ArriveItem(string kind)
    {
        G.pend = Mathf.Max(0, G.pend - 1); if (G.pend == 0) G.pendK = "";
        S.p.carry.Add(kind);
        AudioManager.I?.PlayHz("pick", 520 + 380 * (S.p.carry.Count / (float)G.CarryMax), 520);
    }

    void GiveBells(int n, Vector3 from)
    {
        G.AddBells(n, "event");   // 즉시 가산, 동전은 연출
        JuiceFX.I.CoinPop(from, 6);
        var cam = Camera.main;
        if (cam != null && HUD.I != null) HUD.I.Coins(cam.WorldToScreenPoint(from), () => HUD.I.BellsScreen(), 5);
        if (Player.I != null) WorldLabels.I.Float(Player.I.view.position + Vector3.up * 2f, "+" + n, true, false);
        AudioManager.I?.Play("coin", 1.1f, 0.6f);
    }

    IEnumerator CoFlyAway(Transform t, Vector3 offL)
    {
        var p0 = t.localPosition; var s0 = t.localScale; float el = 0;
        while (el < 0.35f && t != null)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / 0.35f);
            t.localPosition = p0 + offL * k; t.localScale = s0 * Mathf.Max(0.001f, 1f - k); t.Rotate(0, 0, 500f * Time.deltaTime);
            yield return null;
        }
        if (t != null) t.gameObject.SetActive(false);
    }

    // 주운 뒤: 작아지며 사라짐
    IEnumerator CoShrink(Pickup p, float dur)
    {
        if (p.root != null)
        {
            JuiceFX.I.Dust(p.root.position + Vector3.up * 0.2f, 3);
            var s0 = p.root.localScale; float el = 0;
            while (el < dur && p.root != null)
            {
                el += Time.deltaTime; float k = Mathf.Clamp01(el / dur);
                p.root.localScale = s0 * Mathf.Max(0.001f, 1f - k * k);
                yield return null;
            }
            if (p.root != null) Destroy(p.root.gameObject);
        }
        foreach (var m in p.sparkle) { owned.Remove(m); if (m != null) Destroy(m); }
        if (cur == p) cur = null;
    }

    // 시간이 다 되면: 조용히 작아지며 사라짐(보급 캡슐은 회수선이 끌어올리듯 살짝 떠오르며)
    IEnumerator CoVanish(Pickup p)
    {
        if (qaLog) Debug.Log("event: " + p.kind + " 시간 초과로 사라짐");
        if (p.kind == "supply" && p.root != null)
        {
            var pod = p.root.Find("pod"); float el = 0;
            while (el < 0.8f && pod != null) { el += Time.deltaTime; pod.localPosition = L(0, 0, 60f * el * el); yield return null; }
        }
        yield return CoShrink(p, 0.5f);
    }
}

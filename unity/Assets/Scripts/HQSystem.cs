using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 기지 본부(hq 발판 Lv 0~5, docs/chapter2-design.md "B 본부").
//  모형: 착륙장(H) 동쪽·토끼 우리 아래 빈 땅의 밝은 콘크리트 터 위에 레벨마다 한 덩어리씩 솟아오른다(착륙선은 착륙장 서쪽).
//   Lv0 빈 터(착륙선만) → Lv1 지휘 모듈(누운 흰 캡슐·문·둥근 창) → Lv2 통신탑(공 모양 레이돔·빨간 표시등) → Lv3 작은 유리 돔(창 불빛, 밤에 밝게)
//   → Lv4 태양 돛·깃발 → Lv5 관제탑(유리 관제실·꼭대기 표시등)
//   색은 MachineKit 문법(흰 외피·연회색·밝은 콘크리트 받침) + 착륙선과 같은 주황 띠 하나. 글자 라벨 없음, 둥글고 통통하게.
//  보너스(영구, GameState 파생값이 아래 정적 함수를 읽는다): Lv1 등짐 +4 · Lv2 판매가 +10% · Lv3 정착민 도착 간격 −10%
//   · Lv4 기계 속도 +10%(GameState.MachineSpeedMul) · Lv5 하루가 바뀔 때 본부 보조금 벨
//  레벨 업은 GameState.OnPadLevel("hq")를 듣는다(서사 쪽도 같은 이벤트를 쓴다, 따로 이벤트를 만들지 않음).
//  충돌: 레벨별 건물 발자국만 WorldMap.Blocked에 더한다(빈 터는 걸을 수 있음). 착륙장 원·손님 길(x 186~246)·입구와 떨어져 있다.
//  WorldDress.Dress(착륙선 주변)가 만든다. 검증: -pads hq=N(레벨 강제, 이벤트로 모형까지 반영) · -hqup 초(그 초에 발판 완료 경로로 한 레벨 올림, 솟아오름 확인용)
public class HQSystem : MonoBehaviour
{
    public static HQSystem I;
    GameState G => GameState.I;

    public const int MAX = 5;
    public static readonly Vector2 POS = new Vector2(346, 986);   // 터 중심(논리). 터 72×56 × SCALE = 90×70: x 301~391, y 951~1021(우리 울타리 934 바로 아래, 업그레이드 버튼에 덜 가리게)
    const float SCALE = 1.25f;                                     // 모형 전체 배율(기계 옆에서 본부답게 보이게, 충돌 상자도 같이)
    const float SLAB_W = 72f, SLAB_D = 56f, SLAB_H = 3f;

    // ---- 보너스 ----
    public static int CarryBonus(GameState g) => g.PadLv("hq") >= 1 ? 4 : 0;
    public static float PriceMul(GameState g) => g.PadLv("hq") >= 2 ? 1.1f : 1f;
    public static float GapMul(GameState g) => g.PadLv("hq") >= 3 ? 0.9f : 1f;
    public static float SpeedMul(GameState g) => g.PadLv("hq") >= 4 ? 1.1f : 1f;
    public static int Subsidy(GameState g) => g.PadLv("hq") >= 5 ? g.BottlePrice * 30 : 0;
    static readonly string[] BONUS = { "", "등짐 +4", "판매가 +10%", "정착민 도착 −10%", "기계 속도 +10%", "하루 끝 보조금" };
    public static string BonusText(int lv) => lv >= 1 && lv < BONUS.Length ? BONUS[lv] : "";

    // ---- 충돌 ----
    // 레벨별 건물 발자국(모형 좌표 dx0, dy0, dx1, dy1 — 아래 Build* 조각 크기 그대로). 절대좌표 = POS + d × SCALE, 걷는 몸 여유는 스테이션처럼 가로 +10·세로 +8
    static readonly Vector4[][] FOOT =
    {
        new Vector4[0],
        new[] { new Vector4(-33, -7, 17, 23) },                               // 지휘 모듈 + 앞 계단
        new[] { new Vector4(-32, -20, -20, -8) },                             // 안테나 받침
        new[] { new Vector4(3.5f, -22.5f, 36.5f, 10.5f) },                    // 돔
        new[] { new Vector4(24, 16, 32, 24), new Vector4(-33, 19, -27, 25) }, // 태양 돛 기둥 · 깃대
        new[] { new Vector4(-8, -28, 12, -8) },                               // 관제탑
    };
    static int colLv;   // 충돌에 반영된 레벨(재조립 때 Awake가 다시 맞춘다)
    public static bool Blocked(float x, float y)
    {
        if (colLv <= 0 || x < 280f || y < 930f) return false;
        float dx = (x - POS.x) / SCALE, dy = (y - POS.y) / SCALE, mx = 10f / SCALE, my = 8f / SCALE;
        for (int l = 1; l <= colLv && l < FOOT.Length; l++)
            foreach (var r in FOOT[l]) if (dx > r.x - mx && dx < r.z + mx && dy > r.y - my && dy < r.w + my) return true;
        return false;
    }
    static void SetCol(int lv) { if (lv != colLv) { colLv = lv; WorldMap.fenceVer++; } }   // fenceVer: 봇 격자 다시 만들기 신호

    // ---- 모형 ----
    static readonly Color SHELL = MachineKit.SHELL, SHELL_D = MachineKit.SHELL_D, STEEL_D = MachineKit.STEEL_D;
    static readonly Color ACCENT = ModelLib.C(ModelLib.COL.orange);          // 착륙선과 같은 주황
    static readonly Color DOOR = GameConfig.Hex("5f7390"), PANEL = GameConfig.Hex("2f4f8a"), PANEL_LINE = GameConfig.Hex("6f93cf");
    static readonly Color GLASS_D = GameConfig.Hex("40618a"), WARM = GameConfig.Hex("ffd27a"), BEACON = GameConfig.Hex("ff5a3c");
    static readonly Color PLANT = GameConfig.Hex("6cc45e");

    Transform root;
    readonly List<Transform>[] parts = new List<Transform>[MAX + 1];
    int shownLv = -1, lastDay = -1;
    Transform flagCloth;
    readonly List<Material> winMats = new List<Material>();
    readonly List<Material> beacons = new List<Material>();
    float t;

    static Vector3 L(float dx, float dy, float h) => new Vector3(dx * GameConfig.K, h * GameConfig.K, -dy * GameConfig.K);   // dx 동·dy 남(카메라 쪽)·h 높이

    void Awake()
    {
        I = this;
        colLv = 0;
        SetCol(G != null ? Mathf.Clamp(G.PadLv("hq"), 0, MAX) : 0);
    }

    void Start()
    {
        root = new GameObject("hqModel").transform; root.SetParent(transform, false);
        root.position = WorldMap.W2U(POS); root.localScale = Vector3.one * SCALE;
        for (int i = 0; i <= MAX; i++) parts[i] = new List<Transform>();
        BuildSlab(); BuildModule(); BuildAntenna(); BuildDome(); BuildSailFlag(); BuildTower();
        BuildLook();
        Show(Mathf.Clamp(G.PadLv("hq"), 0, MAX), false);
        G.OnPadLevel += OnPadLevel;
        lastDay = G.S.day;
        var up = DevArgs.Get("-hqup");
        if (up != null && !upDone && float.TryParse(up, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sec)) { upDone = true; StartCoroutine(CoDevUp(sec)); }
    }
    static bool upDone;   // 검증 인자는 실행당 1회
    IEnumerator CoDevUp(float sec) { yield return new WaitForSeconds(sec); if (!G.PadMaxed("hq")) PadManager.I.Complete("hq"); }

    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPadLevel; foreach (var m in winMats) Destroy(m); foreach (var m in beacons) Destroy(m); }

    void OnPadLevel(string id, int lv) { if (id == "hq") Show(Mathf.Clamp(lv, 0, MAX), true); }

    // ---- 레벨 외형 단계(LevelLook: Lv0~1 기본 · Lv2~3 개량 · Lv4~5 고급) ----
    //  레벨마다 솟는 건물 조각과 별개로, 단계가 오르면 터와 띠가 함께 세련돼진다.
    //  기본: 밝은 콘크리트 터 / 개량: 한 단 넓은 아랫단 + 크림 타일 윗면 / 고급: 터 둘레 금 테 + 주황 띠(모듈·통신탑·관제탑)·돔 테가 금
    TierLook look;
    static Mesh SlabMesh(int t) => ToyMesh.Cached("hqSlab" + t, m =>
    {
        // 좌표: dx 동·dy 남·h 높이(모형 단위, root 배율 SCALE 이 곱해진다)
        if (t >= 1) m.AtM(0, 0, 0.8f).RBox(new Vector3(SLAB_W + 7, 1.6f, SLAB_D + 7), 12, MachineKit.PAD, GameConfig.Hex("e6d6c0"));
        m.AtM(0, 0, SLAB_H / 2).RBox(new Vector3(SLAB_W, SLAB_H, SLAB_D), 9, MachineKit.PAD);
        if (t >= 2) m.AtM(0, 0, SLAB_H - 0.5f).RBox(new Vector3(SLAB_W + 0.8f, 1.2f, SLAB_D + 0.8f), 9.4f, LevelLook.TRIM, LevelLook.TRIM_L, LevelLook.TRIM_L);
        m.AtM(0, 0, SLAB_H + 0.1f).RBox(new Vector3(SLAB_W - 6, 0.6f, SLAB_D - 6), 6, t == 0 ? MachineKit.PAD_TOP : t == 1 ? GameConfig.Hex("f1dcc0") : GameConfig.Hex("f6e7cf"));
        return m;
    });
    void BuildLook()
    {
        look = new TierLook { squashRoot = null };
        look.Swap(slabMf, SlabMesh, t => ToyMesh.Mat(t >= 2 ? 0.3f : 0.12f));
        var accents = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (r.gameObject.name == "band" || r.gameObject.name == "rim") accents.Add(r);
        var gold = Mats.Gloss(LevelLook.TRIM, 0.5f); var orig = new Material[accents.Count];
        for (int k = 0; k < accents.Count; k++) orig[k] = accents[k].sharedMaterial;
        look.onTier = (t, anim) =>
        {
            for (int k = 0; k < accents.Count; k++) accents[k].sharedMaterial = t >= 2 ? gold : orig[k];
            if (anim) { JuiceFX.I.Pop(slabMf.transform, 0.92f, 0.45f, 2.2f); JuiceFX.I.Dust(root.position + Vector3.up * 0.2f, 8); }
        };
        look.Apply(LevelLook.Tier(Mathf.Clamp(G.PadLv("hq"), 0, MAX)), false);
    }

    // 레벨 lv까지 보이기. 올라간 레벨의 조각은 땅에서 솟아오른다(팝·먼지, 화면 번쩍임 없음)
    void Show(int lv, bool anim)
    {
        int prev = shownLv; shownLv = lv;
        look?.Apply(LevelLook.Tier(lv), anim);
        SetCol(lv);
        for (int l = 1; l <= MAX; l++)
        {
            bool on = l <= lv;
            foreach (var p in parts[l])
            {
                if (anim && on && prev >= 0 && l > prev) { StartCoroutine(CoRise(p, (l - prev - 1) * 0.35f + parts[l].IndexOf(p) * 0.18f)); continue; }
                p.gameObject.SetActive(on);
            }
        }
        // 새 발자국 안에 플레이어가 서 있으면(시트에서 산 경우 등) 발판 자리로 비켜 준다
        if (Player.I != null && Blocked(Player.I.pos.x, Player.I.pos.y)) Player.I.pos = Pads.Get("hq").pos;
    }

    IEnumerator CoRise(Transform p, float delay)
    {
        p.gameObject.SetActive(false);
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (p == null) yield break;
        p.gameObject.SetActive(true);
        var basePos = p.localPosition; const float dur = 0.6f; float depth = 16f * GameConfig.K, el = 0;
        JuiceFX.I.Pop(p, 0.35f, dur, 1.2f);
        JuiceFX.I.Dust(p.position + Vector3.up * 0.15f, 7);
        AudioManager.I?.Play("pop", 0.7f, 0.5f);
        while (el < dur && p != null)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / dur);
            p.localPosition = basePos + Vector3.down * depth * (1f - JuiceFX.EaseOutBack(k, 1.2f));   // 아래에서 솟아 살짝 넘쳤다가 자리
            yield return null;
        }
        if (p == null) yield break;
        p.localPosition = basePos;
        JuiceFX.I.Squash(root, 0.35f);
    }

    void Update()
    {
        float dt = Time.deltaTime; t += dt;
        // 창 불빛: Lv3부터 늘 따뜻하게, 밤엔 더 밝게(그 전엔 어두운 유리)
        float night = DayNight.I != null ? DayNight.I.night : 0f;
        float glow = shownLv >= 3 ? 0.45f + 0.55f * night : 0.1f * night;
        var wc = Color.Lerp(GLASS_D, WARM, glow); var we = shownLv >= 3 ? WARM * (0.15f + 0.9f * night) : Color.black;
        foreach (var m in winMats) { m.color = wc; m.SetColor("_EmissionColor", we); }
        bool on = Mathf.Repeat(t, 1.1f) < 0.55f;
        foreach (var m in beacons) { m.color = on ? BEACON : Color.Lerp(BEACON, STEEL_D, 0.7f); m.SetColor("_EmissionColor", on ? BEACON * (0.4f + 1.4f * night) : Color.black); }
        if (flagCloth != null && flagCloth.gameObject.activeInHierarchy) flagCloth.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 2.3f) * 12f, Mathf.Sin(t * 3.1f) * 3f);
        // Lv5: 하루가 바뀌면 본부 보조금
        int d = G.S.day;
        if (lastDay < 0 || d < lastDay) lastDay = d;
        else if (d > lastDay)
        {
            lastDay = d; int n = Subsidy(G);
            if (n > 0) StartCoroutine(CoSubsidy(n));
        }
    }

    IEnumerator CoSubsidy(int n)
    {
        yield return new WaitForSeconds(1.1f);   // DayNight의 "n일째" 라벨 뒤에
        G.AddBells(n, "subsidy");
        Debug.Log("hq subsidy +" + n + " day=" + G.S.day);
        var pl = Player.I; if (pl == null) yield break;
        WorldLabels.I.Float(pl.view.position + Vector3.up * 2.2f, "+" + n.ToString("N0"), true, true);   // [벨] +n(글자 없이)
        var cam = Camera.main;
        if (HUD.I != null && cam != null) HUD.I.Coins(cam.WorldToScreenPoint(pl.body.position + Vector3.up * 1.2f), () => HUD.I.BellsScreen(), 8);
    }

    // ================= 조각 =================
    Transform Part(int lv, string name, float dx, float dy)
    {
        var p = new GameObject(name).transform; p.SetParent(root, false); p.localPosition = L(dx, dy, 0);
        parts[lv].Add(p); return p;
    }
    // 발광 재질(Resources/Mats/Emissive 복제, _EMISSION 켜짐): 밤 조명에 묻히지 않고 빛나 보이게
    static Material Emissive(Color c)
    {
        var b = Resources.Load<Material>("Mats/Emissive");
        var m = b != null ? new Material(b) : new Material(Mats.Solid(c));
        m.color = c; m.SetColor("_EmissionColor", Color.black); return m;
    }
    Material Win(GameObject go) { var m = Emissive(GLASS_D); winMats.Add(m); go.GetComponent<Renderer>().sharedMaterial = m; return m; }
    void Beacon(Transform p, Vector3 at, float d)
    {
        var s = Prim.Sphere(p, "beacon", at, d, BEACON); Prim.NoShadow(s);
        var m = Emissive(BEACON); beacons.Add(m); s.GetComponent<Renderer>().sharedMaterial = m;
    }
    // 둥근 모서리 판(가로·세로 상자 두 개 + 모서리 원기둥 4개)
    static void RoundSlab(Transform p, string name, float dx, float dy, float w, float d, float h0, float h, float r, Color c)
    {
        Prim.Box(p, name, L(dx, dy, h0 + h / 2), new Vector3(w - 2 * r, h, d), c);
        Prim.Box(p, name, L(dx, dy, h0 + h / 2), new Vector3(w, h, d - 2 * r), c);
        foreach (var sx in new[] { -1, 1 }) foreach (var sy in new[] { -1, 1 })
            Prim.Cyl(p, name, L(dx + sx * (w / 2 - r), dy + sy * (d / 2 - r), h0 + h / 2), 2 * r, h, c);
    }

    // Lv0: 밝은 콘크리트 터(둥근 모서리, ToyMesh 한 덩어리). 걸을 수 있다. 단계마다 메시를 바꿔 끼운다(SlabMesh)
    MeshFilter slabMf;
    void BuildSlab() { slabMf = LevelLook.Holder(root, "slab"); }

    // Lv1: 지휘 모듈 — 받침 위에 누운 흰 캡슐, 앞(남)쪽 문·발판, 양 끝 둥근 창, 주황 띠 둘, 지붕 해치
    void BuildModule()
    {
        var p = Part(1, "module", -8, 6);
        const float R = 13f, HC = SLAB_H + 18f;   // 반지름·축 높이
        foreach (var x in new[] { -13f, 13f }) RoundSlab(p, "cradle", x, 0, 8, 22, SLAB_H, 8, 3, SHELL_D);
        var body = Prim.Capsule(p, "body", L(0, 0, HC), 2 * R, 50, SHELL); body.transform.localRotation = Quaternion.Euler(0, 0, 90);
        foreach (var x in new[] { -10f, 10f }) { var band = Prim.Cyl(p, "band", L(x, 0, HC), 2 * R + 0.9f, 3.2f, ACCENT); band.transform.localRotation = Quaternion.Euler(0, 0, 90); }
        // 문(남쪽 면): 연회색 문틀 + 푸른 회색 문 + 계단
        Prim.Box(p, "doorFrame", L(0, R - 1.6f, HC - 3), new Vector3(11, 17, 3), SHELL_D);
        Prim.Box(p, "door", L(0, R - 0.6f, HC - 3.5f), new Vector3(8, 14, 2), DOOR);
        Prim.Box(p, "doorLamp", L(0, R - 0.4f, HC + 6.5f), new Vector3(4, 1.6f, 2), WARM);
        RoundSlab(p, "step", 0, R + 3.5f, 12, 7, SLAB_H, 3, 2, SHELL_D);
        // 둥근 창 둘(양 끝 캡 쪽, 남쪽을 본다)
        foreach (var x in new[] { -17f, 17f })
        {
            var fr = Prim.Cyl(p, "winFrame", L(x, R - 2.4f, HC + 1), 9, 2, SHELL_D); fr.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var w = Prim.Cyl(p, "win", L(x, R - 1.6f, HC + 1), 6.4f, 2, GLASS_D); w.transform.localRotation = Quaternion.Euler(90, 0, 0); Win(w);
        }
        Prim.Cyl(p, "hatch", L(-4, -2, HC + R - 0.5f), 9, 3, SHELL_D);
        Prim.Cyl(p, "hatchTop", L(-4, -2, HC + R + 1.2f), 6, 1, SHELL);
    }

    // Lv2: 통신탑 — 둥근 받침 + 통통한 흰 기둥(주황 띠) + 꼭대기 공 모양 레이돔 + 그 위 작은 빨간 표시등(깜빡).
    //  예전 도는 접시는 얇은 원판이라 "판떼기만 붙여 둔 것", 돌 때 받침과 떨어져 보였다(jake fb2) → 돌지 않는 둥근 덩어리로 바꿨다. 발자국은 같다
    void BuildAntenna()
    {
        var p = Part(2, "antenna", -26, -14);
        RoundSlab(p, "base", 0, 0, 12, 12, SLAB_H, 6, 3, SHELL_D);
        Prim.Cyl(p, "mast", L(0, 0, SLAB_H + 6 + 15), 5.2f, 30, SHELL);
        Prim.Cyl(p, "band", L(0, 0, SLAB_H + 6 + 10), 6.2f, 3.2f, ACCENT);
        Prim.Cyl(p, "collar", L(0, 0, SLAB_H + 6 + 30.5f), 8.4f, 3f, SHELL_D);
        var dome = Prim.Sphere(p, "radome", L(0, 0, SLAB_H + 6 + 38f), 16, SHELL);
        dome.transform.localScale = new Vector3(16, 15, 16) * GameConfig.K;
        Prim.Cyl(p, "radomeBand", L(0, 0, SLAB_H + 6 + 36.6f), 16.4f, 2.4f, SHELL_D);
        Beacon(p, L(0, 0, SLAB_H + 6 + 47.2f), 4.2f);
    }

    // Lv3: 작은 돔 — 흰 원통 몸 + 주황 테 + 창 넷(불빛) + 유리 돔 지붕 안 작은 화분
    void BuildDome()
    {
        var p = Part(3, "dome", 20, -6);
        const float D = 30f, H0 = SLAB_H, H = 13f;
        Prim.Cyl(p, "foot", L(0, 0, H0 + 1.5f), D + 3, 3, SHELL_D);
        Prim.Cyl(p, "drum", L(0, 0, H0 + H / 2), D, H, SHELL);
        Prim.Cyl(p, "rim", L(0, 0, H0 + H), D + 1.6f, 1.8f, ACCENT);
        // 창: 남쪽 반원에 넷(카메라 쪽), 원통 면에 붙여 바깥을 본다
        foreach (var a in new[] { -54f, -18f, 18f, 54f })
        {
            float rad = a * Mathf.Deg2Rad, r = D / 2 - 0.2f;
            var w = Prim.Box(p, "win", L(Mathf.Sin(rad) * r, Mathf.Cos(rad) * r, H0 + 7.5f), new Vector3(5.5f, 5f, 1.6f), GLASS_D);
            w.transform.localRotation = Quaternion.Euler(0, -a, 0); Win(w);
        }
        Prim.Sphere(p, "plant", L(-3, 1, H0 + H + 2), 11, PLANT);
        Prim.Sphere(p, "plant2", L(5, -3, H0 + H + 1), 7, GameConfig.Hex("8fd67a"));
        var glass = Prim.Sphere(p, "glass", L(0, 0, H0 + H), D - 1, MachineKit.GLASS);
        glass.transform.localScale = new Vector3(D - 1, (D - 1) * 0.78f, D - 1) * GameConfig.K;
        glass.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(MachineKit.GLASS); Prim.NoShadow(glass);
        Prim.Sphere(p, "cap", L(0, 0, H0 + H + (D - 1) * 0.39f), 4, SHELL_D);
    }

    // Lv4: 태양 돛(앞 오른쪽 기둥 위 날개 두 장, 남쪽 해를 본다) + 깃발(앞 왼쪽, 주황·흰 띠, 펄럭)
    void BuildSailFlag()
    {
        var s = Part(4, "sail", 28, 20);
        RoundSlab(s, "sailBase", 0, 0, 8, 8, SLAB_H, 3, 2, SHELL_D);
        Prim.Cyl(s, "sailPost", L(0, 0, SLAB_H + 14), 3, 24, SHELL);
        Prim.Box(s, "sailHub", L(0, 0, SLAB_H + 26), new Vector3(4, 4, 4), SHELL_D);
        foreach (var sx in new[] { -1f, 1f })
        {
            var wing = new GameObject("wing").transform; wing.SetParent(s, false); wing.localPosition = L(sx * 9.5f, 0, SLAB_H + 26);
            wing.localRotation = Quaternion.Euler(-34f, 0, 0);
            Prim.Box(wing, "frame", Vector3.zero, new Vector3(16, 2.6f, 12), SHELL_D);
            Prim.Box(wing, "panel", new Vector3(0, 0.6f, 0) * GameConfig.K, new Vector3(14.2f, 2.2f, 10.2f), PANEL);
            Prim.Box(wing, "line", new Vector3(0, 0.9f, 0) * GameConfig.K, new Vector3(0.8f, 2.2f, 10.2f), PANEL_LINE);
        }
        var f = Part(4, "flag", -30, 22);
        RoundSlab(f, "flagBase", 0, 0, 6, 6, SLAB_H, 2, 2, SHELL_D);
        Prim.Cyl(f, "pole", L(0, 0, SLAB_H + 21), 2, 40, SHELL);
        Prim.Sphere(f, "poleTop", L(0, 0, SLAB_H + 41.5f), 3.4f, SHELL_D);
        flagCloth = new GameObject("cloth").transform; flagCloth.SetParent(f, false); flagCloth.localPosition = L(0, 0, SLAB_H + 35);
        Prim.Box(flagCloth, "cloth", L(7.5f, 0, 0), new Vector3(14, 9, 0.8f), ACCENT);
        Prim.Box(flagCloth, "stripe", L(7.5f, 0, 0), new Vector3(14, 2.6f, 1f), SHELL);
    }

    // Lv5: 관제탑 — 받침 + 흰 기둥(주황 띠) + 넓은 관제실(유리 띠 불빛) + 둥근 지붕 + 안테나 끝 표시등
    void BuildTower()
    {
        var p = Part(5, "tower", 2, -18);
        const float H0 = SLAB_H, STEM = 42f;
        Prim.Cyl(p, "foot", L(0, 0, H0 + 3), 20, 6, SHELL_D);
        Prim.Cyl(p, "stem", L(0, 0, H0 + 6 + STEM / 2), 13, STEM, SHELL);
        Prim.Cyl(p, "band", L(0, 0, H0 + 6 + STEM * 0.55f), 14, 3, ACCENT);
        float hc = H0 + 6 + STEM;
        Prim.Cyl(p, "deck", L(0, 0, hc + 1.5f), 28, 3, SHELL_D);
        var cab = Prim.Cyl(p, "cab", L(0, 0, hc + 3 + 5.5f), 26, 11, GLASS_D); Win(cab);   // 유리 관제실: 위에서도 띠가 보이게 지붕보다 넓게 두지 않는다
        foreach (var a in new[] { 0f, 60f, 120f })   // 창살(세로 기둥) 3쌍
        {
            float rad = a * Mathf.Deg2Rad;
            foreach (var sg in new[] { -1f, 1f })
                Prim.Box(p, "mullion", L(Mathf.Sin(rad) * 13f * sg, Mathf.Cos(rad) * 13f * sg, hc + 8.5f), new Vector3(1.6f, 11, 1.6f), SHELL);
        }
        Prim.Cyl(p, "roof", L(0, 0, hc + 15.5f), 27, 3, SHELL);
        var cap = Prim.Sphere(p, "roofCap", L(0, 0, hc + 17), 21, SHELL); cap.transform.localScale = new Vector3(21, 9, 21) * GameConfig.K;
        Prim.Cyl(p, "aerial", L(0, 0, hc + 26), 1.6f, 12, STEEL_D);
        Beacon(p, L(0, 0, hc + 33), 4.5f);
    }
}

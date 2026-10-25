using System.Collections.Generic;
using UnityEngine;

// §6.4 테라포밍: terra() = clamp(0.5*(ice2+pen+dome)/3 + 0.5*(Σ발판 레벨)/48), 초록 타일(TERRA_SLOTS) 점등, 땅색 lerp.
// 원본 index.html 528~544행(terra·TERRA_SLOTS·terraMat·syncTerra·tickTerra·tintGround).
//  - 켜진 타일 수 n = min(슬롯수, round(terra*24) + terraKeep). 구역 태그 슬롯(ice2·pen·dome 각 3개)은 그 구역이 열리면 앞 순서로 당겨진다
//  - 타일 = 둥근 이끼 방석(가장자리가 둥글게 내려가는 두 단 원판) 위 통통한 저폴리 나무(둥근 수관 + 짧은 줄기, 초록 2톤, ToyMesh 한 메시).
//    보통 타일은 큰 나무 + 작은 나무, 3번째마다 수관 두 덩어리를 얹은 키 큰 나무 하나(fb2: Kenney 나무 묶음이 엉성해 보였다, 개수는 그 이하)
//  - 점등 연출(레벨 업·구역 해제 때만): 0.12s 간격, 0.5s easeOutBack 0.001→1, 초록 링. 시작 시엔 연출 없이 바로 켠다
//  - 땅색: 원본은 흰 재질×텍스처를 white.lerp(0xb3683e, terra*0.45)로 곱하지만 우리 지면 텍스처는 이미 주황이라 그대로 곱하면 너무 어둡다.
//    같은 식(terra*0.45)에 은은한 초록 기운 색(TINT)을 써서 붉은기만 살짝 덜어낸다(GroundPainter.GroundMaterial 훅)
public class TerraformManager : MonoBehaviour
{
    public static TerraformManager I;
    GameState G => GameState.I;
    void Awake() { I = this; }

    public const float TS = 28f;          // 타일 지름(원본 모델 1단위 = 28)
    const int TILES_PER_TERRA = 12;       // terra 1.0 = 12장(원본 24장, 2026-10-25 소품 밀도 절반)
    const float PAD_MAX = 48f;            // 활성 발판 레벨 총합(§3 PAD_MAX)

    // §6.4 terra(): 구역 3개가 절반, 전체 발판 레벨이 절반
    public float Terra()
    {
        if (G == null) return 0f;
        float zones = (Mathf.Min(1, G.PadLv("ice2")) + Mathf.Min(1, G.PadLv("pen")) + Mathf.Min(1, G.PadLv("dome"))) / 3f;
        int sum = 0;
        foreach (var p in Pads.ALL) if (p.active && !Pads.IsFacSpd(p.id)) sum += G.PadLv(p.id);   // 시설 속도 발판(10/25)은 빼서 테라 진행은 예전과 같게
        return Mathf.Clamp01(0.5f * zones + 0.5f * sum / PAD_MAX);
    }
    /// 지금 켜져 있어야 할 타일 수
    public int TargetCount => Mathf.Min(slots.Count, Mathf.FloorToInt(Terra() * TILES_PER_TERRA + 0.5f) + (G != null ? G.S.terraKeep : 0));
    public int SlotCount => slots.Count;
    public int LitCount { get { int n = 0; foreach (var s in slots) if (s.on) n++; return n; } }

    // ---- 슬롯 ----
    // 원본 TERRA_SLOTS 후보(울타리 밖·손님 길 밖)를 옮기되, 우리 레이아웃(구역 바닥·스테이션·줄·착륙장·착륙선·로버·발판·가로등)과 겹치는 자리는
    // 가까운 맨땅으로 옮기고 몇 자리를 보충했다(45개). 그 뒤 원본처럼 바위와 r+14 이내로 겹치는 자리는 거른다.
    static readonly object[][] CANDIDATES =
    {
        // 구역 태그 슬롯: 그 구역이 열리면 '그 구역 옆부터' 켜진다
        // 예전 북쪽 가장자리(y 18)는 이제 북쪽 길이라, 그 줄의 슬롯 7개는 북쪽 메사 발치로 옮겼다(태그도 옆 구역 온실·토양 공장으로).
        // 2장 개척 길(y −410~−380)이 생겨 다시 새 북쪽 경계(−634) 밖 y −650으로
        new object[]{52,-650,"farm"}, new object[]{122,-650,"farm"}, new object[]{192,-650,"farm"},
        // 2026-10-25 소품 절반: 기지 안쪽(손님 길 옆·돔 위) 덤불을 줄여 우리·돔은 하나씩, 우리 몫은 바깥 가장자리로
        new object[]{396,846,"pen"},
        new object[]{26,686,"dome"},
        // 일반 슬롯(원본 순서: 매장 양옆부터 바깥으로)
        new object[]{398,440}, new object[]{-4,475}, new object[]{398,540}, new object[]{-4,590}, new object[]{-4,300}, new object[]{396,260},
        new object[]{-4,220}, new object[]{396,340}, new object[]{-6,178}, new object[]{396,160}, new object[]{-4,140}, new object[]{396,80},
        new object[]{258,704}, new object[]{-4,700}, new object[]{290,-650,"soil"}, new object[]{350,-650,"soil"}, new object[]{398,775}, new object[]{-4,740},
        new object[]{398,910}, new object[]{-4,780}, new object[]{146,960}, new object[]{300,958}, new object[]{-4,820}, new object[]{390,958},
        new object[]{120,950}, new object[]{-4,60}, new object[]{150,1060}, new object[]{398,490}, new object[]{20,-650}, new object[]{246,-652},
        // 보충(우리 레이아웃의 맨땅)
        new object[]{30,650},   // (246,14)는 북쪽 길, (160,690)은 기지 안 맨땅 한가운데라 뺐다 new object[]{268,1052}, new object[]{60,1050}, new object[]{330,1050},
        new object[]{-6,960}, new object[]{-10,324}, new object[]{-10,390}, new object[]{398,120}, new object[]{398,300}, new object[]{398,384},
        new object[]{-10,912}, new object[]{278,1002}, new object[]{14,1002},
    };

    class Slot
    {
        public float x, y; public string zone; public bool tall; public float ry;
        public bool on; public float a = 1f, d;        // a: 점등 진행 0..1(1=완료), d: 시작 지연
        public Transform root, trees; public float phase;
    }
    readonly List<Slot> slots = new List<Slot>();
    Transform tilesRoot;
    float syncT, swayT;
    bool started;

    // 바위 자리: WorldDress.Rocks와 같은 고정 난수(seed 11 Park–Miller)·같은 규칙으로 다시 뽑는다(원본 rockSpots 필터 재현)
    static List<Vector3> RockSpots()
    {
        long seed = 11; float Next() { seed = seed * 16807 % 2147483647; return (float)((seed - 1) / 2147483646.0); }
        var spots = new List<Vector3>(); var entry = WorldMap.ENTRY; var lander = WorldMap.LANDER;
        for (int tries = 0; spots.Count < 40 && tries < 400; tries++)
        {
            float side = Next(); float x, y;
            if (side < 0.3f) { x = -30 + Next() * 30; y = -60 + Next() * 1140; }
            else if (side < 0.55f) { x = 386 + Next() * 22; y = 600 + Next() * 480; }
            else if (side < 0.85f) { x = Next() < 0.5f ? 24 + Next() * 146 : 262 + Next() * 114; y = 590 + Next() * 130; }
            else { x = Next() * 400; y = 1040 + Next() * 60; }
            if (Vector2.Distance(new Vector2(x, y), entry) < 80 || Vector2.Distance(new Vector2(x, y), lander) < 50) continue;
            if (x > 0 && x < 400 && y > 30 && y < 1024) continue;
            float r = 6 + Next() * 10; Next();   // ry
            spots.Add(new Vector3(x, y, r));
        }
        spots.Add(new Vector3(-26, 90, 15)); spots.Add(new Vector3(-24, 250, 18)); spots.Add(new Vector3(-28, 430, 14));
        spots.Add(new Vector3(408, 720, 16)); spots.Add(new Vector3(406, 860, 19)); spots.Add(new Vector3(410, 990, 15));
        return spots;
    }

    void Start()
    {
        tilesRoot = new GameObject("terraTiles").transform; tilesRoot.SetParent(transform, false);
        var rocks = RockSpots();
        foreach (var c in CANDIDATES)
        {
            float x = System.Convert.ToSingle(c[0]), y = System.Convert.ToSingle(c[1]);
            bool hit = false;
            foreach (var r in rocks) if (Vector2.Distance(new Vector2(r.x, r.y), new Vector2(x, y)) < r.z + 14f) { hit = true; break; }
            if (hit) continue;
            int i = slots.Count;
            slots.Add(new Slot { x = x, y = y, zone = c.Length > 2 ? (string)c[2] : null, tall = i % 3 == 2, ry = (i * 2.4f) % 6.28f, phase = i * 1.37f });
        }
        G.OnPadLevel += OnPad;
        // 프레스티지 직후 새 기지: 남겨 둔 초록 타일이 덮개가 걷힐 즈음부터 팝으로 번진다(원본 prestige()의 syncTerra(true)). 그 외 시작은 연출 없이
        if (PrestigeManager.Arrived) Sync(true, 0.75f); else Sync(false);
        TintGround(true);
        started = true;
    }
    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPad; }

    void OnPad(string id, int lv)
    {
        if (!started) return;
        // 시작 직후(저장 복원·검증 인자 적용) 이벤트는 연출 없이
        Sync(Time.timeSinceLevelLoad > 0.5f);
    }

    bool ZoneOpen(string z) => G.PadLv(z) >= 1;

    // 원본 syncTerra(fx): 우선순위(열린 구역 태그 0 · 일반 1 · 닫힌 구역 태그 2) → 원래 순서로 정렬해 앞에서 n장 켠다
    public void Sync(bool fx) => Sync(fx, 0f);
    void Sync(bool fx, float delay0)
    {
        int n = TargetCount;
        var ord = new List<int>(); for (int i = 0; i < slots.Count; i++) ord.Add(i);
        int Pr(Slot s) => s.zone == null ? 1 : ZoneOpen(s.zone) ? 0 : 2;
        ord.Sort((a, b) => { int pa = Pr(slots[a]), pb = Pr(slots[b]); return pa != pb ? pa.CompareTo(pb) : a.CompareTo(b); });
        float d = delay0;
        for (int j = 0; j < ord.Count; j++)
        {
            var s = slots[ord[j]]; bool on = j < n;
            if (s.on == on) continue;
            s.on = on;
            if (on)
            {
                if (s.root == null) Build(s);
                s.root.gameObject.SetActive(true);
                if (fx) { s.a = 0f; s.d = d; d += 0.12f; s.root.localScale = Vector3.one * 0.001f; }
                else { s.a = 1f; s.root.localScale = Vector3.one; }
            }
            else if (s.root != null) { s.a = 1f; s.root.gameObject.SetActive(false); }
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 원본 tickTerra: 지연 뒤 초록 링 + 0.5s easeOutBack 팝
        foreach (var s in slots)
        {
            if (s.a >= 1f || s.root == null) continue;
            if (s.d > 0f)
            {
                s.d -= dt; if (s.d > 0f) continue;
                JuiceFX.I.Ring(WorldMap.W2U(s.x, s.y, 1f), GameConfig.Hex("6fcf7a"), 16f);
                AudioManager.I.Play("pop", 1.25f + Random.value * 0.35f, 0.35f);
            }
            s.a = Mathf.Min(1f, s.a + dt / 0.5f);
            s.root.localScale = Vector3.one * Mathf.Max(0.001f, JuiceFX.EaseOutBack(s.a));
            if (s.a >= 1f && JuiceFX.I != null) JuiceFX.I.Dust(WorldMap.W2U(s.x, s.y, 2f), 2);
        }
        // 작은 생기: 나무가 바람에 살랑(밑동 기준 2~3° 흔들림). 화면 밖도 싸서 그냥 돈다
        swayT += dt;
        foreach (var s in slots)
        {
            if (!s.on || s.trees == null) continue;
            float w = Mathf.Sin(swayT * 1.6f + s.phase) * 2.2f + Mathf.Sin(swayT * 0.7f + s.phase * 0.5f) * 1.2f;
            s.trees.localRotation = Quaternion.Euler(w, -s.ry * Mathf.Rad2Deg, w * 0.6f);
        }
        // 0.3초마다 개수 재확인(발판 이벤트를 놓친 경우 대비) + 땅색
        syncT += dt;
        if (syncT >= 0.3f)
        {
            syncT = 0f;
            int lit = LitCount;
            if (lit != TargetCount) Sync(Time.timeSinceLevelLoad > 0.5f);
            TintGround(false);
        }
    }

    // ---- 땅색 ----
    // 원본: white.lerp(0xb3683e, terra*0.45). 우리 지면은 이미 주황 텍스처라 붉은기만 살짝 덜어내는 연초록 쪽으로(어두워지지 않게)
    static readonly Color TINT = GameConfig.Hex("d2ffb0");
    Color groundCur = Color.white;
    void TintGround(bool snap)
    {
        var m = GroundPainter.GroundMaterial; if (m == null) return;
        var target = Color.Lerp(Color.white, TINT, Terra() * 0.45f);
        groundCur = snap ? target : Color.Lerp(groundCur, target, 0.25f);   // 0.3초 간격으로 부드럽게 따라감
        m.color = groundCur;
    }

    // ---- 타일 조립 ----
    // 이끼(연두, 나무 수관과 구분되게 한 톤 노랗게) · 방석 가장자리(짙은 초록) · 나무(수관 윗면 밝은 연두 → 옆 초록 → 아래 짙은 초록, 줄기 갈색)
    static readonly Color MOSS = GameConfig.Hex("9cc95e"), MOSS_L = GameConfig.Hex("b2d873"), MOSS_D = GameConfig.Hex("5f9442");
    static readonly Color LEAF_T = GameConfig.Hex("8ed36a"), LEAF = GameConfig.Hex("5cb04e"), LEAF_A = GameConfig.Hex("4fa046"), LEAF_D = GameConfig.Hex("3a8040");
    static readonly Color TRUNK = GameConfig.Hex("8a5a3a"), TRUNK_D = GameConfig.Hex("6b4229");
    const float TOP_H = 2.0f;   // 방석 윗면 높이(논리)

    void Build(Slot s)
    {
        var root = new GameObject("terra").transform; root.SetParent(tilesRoot, false);
        root.position = WorldMap.W2U(s.x, s.y, 0f);
        s.root = root;
        int v = (int)(Mathf.Abs(s.x * 7 + s.y * 13)) % 2;
        var moss = ToyMesh.Put(root, "moss", MossMesh(), ToyMesh.Mat(0.05f), Vector3.zero);
        var mr = moss.GetComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 나무: 방석 위에 세우고 밑동 기준으로 살랑(Update)
        var pivot = new GameObject("trees").transform; pivot.SetParent(root, false);
        pivot.localPosition = new Vector3(0, TOP_H * GameConfig.K - 0.02f, 0);
        ToyMesh.Put(pivot, "tree", TreeMesh(s.tall, v), ToyMesh.Mat(0.08f), Vector3.zero).transform.localScale = Vector3.one * 1.2f;   // 방석보다 조금 크게(예전 나무 묶음 폭과 비슷)
        s.trees = pivot;
        pivot.localRotation = Quaternion.Euler(0, -s.ry * Mathf.Rad2Deg, 0);
    }

    // 둥근 이끼 방석: 아랫단(짙은 가장자리 → 이끼) + 윗단(이끼 → 밝은 윗면), 매끈한 옆면이라 테두리가 깔끔한 원으로 읽힌다
    static Mesh MossMesh() => ToyMesh.Cached("terraMoss", m =>
    {
        float r = TS * 0.5f;
        m.At(0, 0, 0).Cyl(r, r * 0.95f, 1.1f, 28, MOSS_D, MOSS, true, MOSS);
        m.At(0, 1.1f, 0).Cyl(r * 0.95f, r * 0.76f, TOP_H - 1.1f, 28, MOSS, MOSS_L, true, MOSS_L);
        return m;
    });

    // 통통한 저폴리 나무(밑동 = 원점). tall: 수관 두 덩어리를 겹쳐 얹은 키 큰 나무 하나 / 보통: 큰 나무 + 옆 작은 나무
    static Mesh TreeMesh(bool tall, int v) => ToyMesh.Cached("terraTree" + (tall ? "T" : "N") + v, m =>
    {
        if (tall)
        {
            m.At(0, 0, 0).Cyl(1.9f, 1.4f, 7f, 8, TRUNK_D, TRUNK, false, TRUNK);
            m.At(0, 10.5f, 0).Blob(new Vector3(7.2f, 6.2f, 7.2f), 1, 3 + v, 0.09f, LEAF_T, LEAF, LEAF_D, LEAF_A);
            m.At(0.6f, 16.6f, -0.4f).Blob(new Vector3(5.2f, 4.6f, 5.2f), 1, 7 + v, 0.09f, LEAF_T, LEAF, LEAF_D, LEAF_A);
            return m;
        }
        float sx = v == 0 ? 1f : -1f;
        m.At(-2.6f * sx, 0, 2f).Cyl(1.8f, 1.3f, 5.5f, 8, TRUNK_D, TRUNK, false, TRUNK);
        m.At(-2.6f * sx, 10f, 2f).Blob(new Vector3(7.4f, 6.6f, 7.4f), 1, 11 + v, 0.09f, LEAF_T, LEAF, LEAF_D, LEAF_A);
        m.At(6.4f * sx, 0, -5f).Cyl(1.3f, 1f, 3.6f, 8, TRUNK_D, TRUNK, false, TRUNK);
        m.At(6.4f * sx, 6.6f, -5f).Blob(new Vector3(4.4f, 4f, 4.4f), 1, 17 + v, 0.1f, LEAF_T, LEAF, LEAF_D, LEAF_A);
        return m;
    });
}

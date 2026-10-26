using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 2장 C 미션 보드(docs/chapter2-design.md): 화성 하루(DayNight 300초)마다 미션 3개를 발급한다(S.missions, S.missionDay = S.day).
//  · 종류 풀(열린 시설만): 물·산소·식량 공급 n, 얼음 n개 줍기(드릴 전만), 레골리스 n개 캐기, 비료 n개 만들기, 정착민 n명, 벨 n 벌기, VIP n명, 업그레이드 n번(살 레벨이 4 이상 남았을 때만).
//    네 묶음(공급·채집·손님·살림)에서 서로 다른 묶음 셋을 골라 하루가 한쪽으로 쏠리지 않게 한다.
//  · 목표 수치는 진행도(열린 발판 수/3 + 정착 단계, 0~8)에 맞춰 하루 안에 할 만큼(QA 봇 기준 하루의 1/3~2/3), 보상 벨 = 목표 × 종류별 무게 × 병값(벨 벌기는 목표 × 0.12).
//  · S.missions[3]은 보너스 상자(id "bonus", goal 3, prog = 끝낸 미션 수, reward = 세 미션 보상 합의 절반).
//  · 진행 집계: GameState 이벤트(OnSupplied·OnPadLevel·OnBellsChanged) + 프레임마다 S 값 변화량(fertMade·custs) + Player 줍기·손님 VIP 결제 한 줄 호출(Count).
//  · 완료 순간에만 화면 아래 작은 토스트 1.5초([체크][미션 아이콘] +120, 글자 없음). 상단 칩·말풍선 없음. 보상은 시트 목표 탭의 [받기]로.
//  · 하루가 바뀌면 못 끝낸 미션은 사라지고, 끝냈지만 안 받은 보상(보너스 포함)은 자동 지급한 뒤 새로 발급한다.
//  · 튜토리얼(S.tut < 3) 동안은 발급하지 않는다.
// 검증 인자: -missions(튜토리얼과 무관하게 지금 새로 발급), -missionDone i[,j…][@초](i번 미션 완료 처리, 기본 1.5초 뒤)
public class MissionBoard : MonoBehaviour
{
    public static MissionBoard I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public const int COUNT = 3;
    public const string BONUS = "bonus";

    class Kind
    {
        public string id, group, icon; public float unit;
        public Func<int, int> goal;          // tier → 목표
        public Func<GameState, bool> open;   // 열린 시설만
        public Func<int, string> name;       // 목표 → 이름
    }
    static int R5(float v) => Mathf.Max(5, Mathf.RoundToInt(v / 5f) * 5);
    static int R50(float v) => Mathf.Max(50, Mathf.RoundToInt(v / 50f) * 50);
    static readonly Kind[] KINDS =
    {
        // 공급(정착민에게 결제로 건넨 개수)
        new Kind{ id="water", group="supply", icon="bottle", unit=0.6f, goal=t=>R5(20+6*t), open=g=>true,                 name=n=>"물 "+n+" 공급" },
        new Kind{ id="o2",    group="supply", icon="o2",     unit=0.9f, goal=t=>R5(10+3*t), open=g=>g.KindOpen("o2"),     name=n=>"산소 "+n+" 공급" },
        new Kind{ id="food",  group="supply", icon="food",   unit=1.4f, goal=t=>6+2*t,      open=g=>g.KindOpen("food"),   name=n=>"식량 "+n+" 공급" },
        // 채집·생산
        new Kind{ id="ice",   group="gather", icon="ice",    unit=0.25f, goal=t=>R5(50+15*t), open=g=>g.PadLv("drill")==0,                                                         name=n=>"얼음 "+n+"개 줍기" },
        new Kind{ id="rego",  group="gather", icon="rego",   unit=0.3f,  goal=t=>R5(30+5*t),  open=g=>SettleStations.I != null && SettleStations.I.MineOpen && SettleStations.I.Open(1), name=n=>"레골리스 "+n+"개 캐기" },
        new Kind{ id="fert",  group="gather", icon="fert",   unit=0.8f,  goal=t=>8+2*t,       open=g=>SettleStations.I != null && SettleStations.I.Open(2),          name=n=>"비료 "+n+"개 만들기" },
        // 손님
        new Kind{ id="cust",  group="people", icon="heart",  unit=0.8f, goal=t=>R5(15+5*t), open=g=>true,                              name=n=>"정착민 "+n+"명 받기" },
        new Kind{ id="vip",   group="people", icon="crown",  unit=4f,   goal=t=>1+t/3,      open=g=>g.S.sold >= 40,                    name=n=>"VIP "+n+"명 받기" },
        // 살림
        new Kind{ id="bell",  group="econ",   icon="bell",   unit=0.12f, goal=t=>R50(GameState.I.BottlePrice*(60+20*t)), open=g=>true, name=n=>"벨 "+n.ToString("N0")+" 벌기" },
        new Kind{ id="up",    group="econ",   icon="up",     unit=3f,    goal=t=>2+t/4,      open=g=>UpsLeft(g)>=4,                             name=n=>"업그레이드 "+n+"번" },
        // 우주(3·4장): 화물선 주문·수출 발사·연료
        new Kind{ id="order", group="space",  icon="ship",   unit=6f,    goal=t=>2+t/4,      open=g=>g.PadLv("port")>=1,                         name=n=>"화물선 주문 "+n+"건" },
        new Kind{ id="launch",group="space",  icon="rocket", unit=12f,   goal=t=>1+t/6,      open=g=>g.PadLv("launch")>=1,                       name=n=>"수출 발사 "+n+"번" },
        new Kind{ id="fuel",  group="space",  icon="fuel",   unit=0.6f,  goal=t=>R5(20+4*t), open=g=>g.PadLv("fuel")>=1 || g.PadLv("canyon")>=1, name=n=>"연료 "+n+"개 만들기" },
    };
    // 아직 살 수 있는 업그레이드 레벨 수(발판·시트 스탯, 지금 조건이 열린 것만). 다 사 버렸으면 "업그레이드 n번" 미션은 낼 수 없다
    static int UpsLeft(GameState g) { int n = 0; foreach (var d in Pads.ALL) if (d.active && g.PadReq(d.id)) n += Mathf.Max(0, d.max - g.PadLv(d.id)); return n; }
    static readonly string[] GROUPS = { "supply", "gather", "people", "econ", "space" };
    static Kind Def(string id) { foreach (var k in KINDS) if (k.id == id) return k; return null; }

    /// 시트 표시용
    public static string NameOf(MissionSave m) { if (m == null) return ""; if (m.id == BONUS) return "보너스 상자"; var k = Def(m.id); return k != null ? k.name(m.goal) : m.id; }
    public static string IconOf(MissionSave m) { if (m == null) return "flag"; if (m.id == BONUS) return "chest"; var k = Def(m.id); return k != null ? k.icon : "flag"; }

    public event Action OnChanged;
    bool forced;                 // -missions: 튜토리얼과 무관하게 발급
    float lastBells, bellAcc; int lastFert, lastCusts;
    bool granting;               // 미션 보상 지급 중(벨 벌기 집계에서 뺀다)
    readonly Queue<(string icon, string num)> toasts = new Queue<(string, string)>(); Coroutine toastCo;

    public bool Ready => G != null && (S.tut >= 3 || forced);
    /// 오늘 미션(보너스 제외). 아직 없으면 빈 목록
    public List<MissionSave> Today { get { var l = new List<MissionSave>(); foreach (var m in S.missions) if (m.id != BONUS) l.Add(m); return l; } }
    public MissionSave Bonus { get { foreach (var m in S.missions) if (m.id == BONUS) return m; return null; } }
    public bool Active => Ready && S.missions.Count > 0;
    public bool AnyClaimable { get { if (!Active) return false; foreach (var m in S.missions) if (m.done && !m.claimed) return true; return false; } }
    /// 다음 미션까지 남은 초(하루 끝)
    public float SecondsLeft => DayNight.DAY_LEN - Mathf.Repeat(S.clock, DayNight.DAY_LEN);

    void Awake() { I = this; }

    void Start()
    {
        Sync();
        G.OnSupplied += OnSupplied;
        G.OnPadLevel += OnPad;
        G.OnBellsChanged += OnBells;
        if (DevArgs.Has("-missions") || DevArgs.Get("-missionDone") != null) StartCoroutine(CoDev());
    }
    void OnDestroy()
    {
        if (G == null) return;
        G.OnSupplied -= OnSupplied; G.OnPadLevel -= OnPad; G.OnBellsChanged -= OnBells;
    }

    // 집계 기준값을 지금 값으로(발급 전·튜토리얼 중 진행은 세지 않는다)
    void Sync() { lastBells = S.bells; bellAcc = 0; lastFert = S.fertMade; lastCusts = S.custs; }

    void Update()
    {
        if (!Ready) { Sync(); return; }
        if (S.missionDay != S.day || S.missions.Count == 0) Rollover();
        // 이벤트가 없는 값은 변화량으로: 비료 생산 누계·결제한 정착민 수(이주로 0이 되면 기준만 맞춘다)
        if (S.fertMade > lastFert) Count("fert", S.fertMade - lastFert);
        lastFert = S.fertMade;
        if (S.custs > lastCusts) Count("cust", S.custs - lastCusts);
        lastCusts = S.custs;
        lastBells = S.bells;   // 이벤트 없이 바뀐 벨(이주 초기화 등)은 기준만 맞춘다
    }

    void OnSupplied(string kind, int n) { if (Ready) Count(kind == "o2" ? "o2" : kind == "food" ? "food" : "water", n); }
    void OnPad(string id, int lv) { if (Ready && lv > 0) Count("up", 1); }
    void OnBells()
    {
        float b = S.bells, d = b - lastBells; lastBells = b;
        if (!Ready || granting || d <= 0) return;
        bellAcc += d; int whole = Mathf.FloorToInt(bellAcc);
        if (whole > 0) { bellAcc -= whole; Count("bell", whole); }
    }

    // ───────────────────────── 진행 ─────────────────────────
    /// 진행 n 더하기. Player 줍기(ice·rego)·손님 VIP 결제도 이걸 부른다. 다른 종류는 무시
    public void Count(string kind, int n)
    {
        if (!Ready || n <= 0 || string.IsNullOrEmpty(kind)) return;
        foreach (var m in S.missions)
        {
            if (m.id != kind || m.done) continue;
            m.prog = Mathf.Min(m.goal, m.prog + n);
            if (m.prog >= m.goal) Complete(m);
        }
    }
    /// Player.Arrive에서: 얼음·레골리스를 등짐에 받은 순간
    public void Picked(string kind) { if (kind == "ice" || kind == "rego") Count(kind, 1); }

    void Complete(MissionSave m)
    {
        m.done = true; m.prog = m.goal;
        SettlementManager.I?.MissionDone();   // 9단계 자급 마을: 하루 미션 누계
        var b = Bonus;
        if (b != null && !b.done) { b.prog = Mathf.Min(b.goal, DoneCount()); if (b.prog >= b.goal) b.done = true; }
        Toast(IconOf(m), "+" + m.reward.ToString("N0"));
        if (b != null && b.done && !b.claimed) Toast("chest", "+" + b.reward.ToString("N0"));
        AudioManager.I?.Play("ding", 1.25f, 0.6f); JuiceFX.I?.Vibrate(20);
        Debug.Log("mission done: " + m.id + " " + m.prog + "/" + m.goal + " reward=" + m.reward);
        Changed(); G.Save();
    }
    int DoneCount() { int c = 0; foreach (var m in S.missions) if (m.id != BONUS && m.done) c++; return c; }

    /// 받기(시트 버튼). from: 동전이 출발할 화면 좌표
    public bool Claim(MissionSave m, Vector2 from)
    {
        if (m == null || !m.done || m.claimed) return false;
        m.claimed = true;
        Grant(m.reward);
        if (HUD.I != null) HUD.I.Coins(from, () => HUD.I.BellsScreen(), m.id == BONUS ? 14 : 8);
        if (m.id == BONUS) { AudioManager.I?.Play("up", 1.1f); JuiceFX.I?.Shake(3); JuiceFX.I?.Vibrate(30); }
        else AudioManager.I?.Play("up", 1.3f, 0.6f);
        Debug.Log("mission claim: " + m.id + " +" + m.reward);
        Changed(); G.Save();
        return true;
    }

    void Grant(int bells) { granting = true; G.AddBells(bells, "mission"); granting = false; lastBells = S.bells; }
    void Changed() { OnChanged?.Invoke(); HUD.I?.Refresh(); }

    // ───────────────────────── 발급 ─────────────────────────
    // 하루가 바뀌었거나 비었으면: 끝냈지만 안 받은 보상은 자동 지급 → 새 미션 3개 + 보너스
    void Rollover()
    {
        int auto = 0;
        foreach (var m in S.missions) if (m.done && !m.claimed) { m.claimed = true; auto += m.reward; }
        if (auto > 0) { Grant(auto); Debug.Log("mission auto-claim: +" + auto + " (day " + S.missionDay + " → " + S.day + ")"); }
        Issue();
    }

    void Issue()
    {
        int tier = Tier();
        var rnd = new System.Random(S.day * 7919 + S.prestige * 131 + S.sold * 17 + S.custN);
        // 열린 종류가 있는 묶음 중 셋(물·얼음·정착민·벨은 늘 열려 있어 넷 다 후보) → 묶음마다 한 종류(새로 열린 것에 무게 2)
        var groups = new List<string>(GROUPS);
        Shuffle(groups, rnd);
        if (G.PadLv("port") >= 1) { groups.Remove("space"); groups.Insert(0, "space"); }
        S.missions.Clear();
        int sum = 0, price = Mathf.Max(1, G.BottlePrice);
        foreach (var gname in groups)
        {
            if (S.missions.Count >= COUNT) break;
            var pool = new List<Kind>(); float wsum = 0;
            foreach (var k in KINDS) if (k.group == gname && k.open(G)) { pool.Add(k); wsum += Weight(k); }
            if (pool.Count == 0) continue;
            float r = (float)rnd.NextDouble() * wsum; Kind pick = pool[pool.Count - 1];
            foreach (var k in pool) { r -= Weight(k); if (r <= 0) { pick = k; break; } }
            int goal = Mathf.Max(1, pick.goal(tier));
            int reward = pick.id == "bell" ? R5(goal * pick.unit) : R5(goal * pick.unit * price);
            S.missions.Add(new MissionSave { id = pick.id, goal = goal, reward = reward });
            sum += reward;
        }
        S.missions.Add(new MissionSave { id = BONUS, goal = S.missions.Count, reward = R5(sum * 0.5f) });
        S.missionDay = S.day;
        Sync();
        var sb = new System.Text.StringBuilder();
        foreach (var m in S.missions) sb.Append(m.id).Append(' ').Append(m.goal).Append(" +").Append(m.reward).Append(" · ");
        Debug.Log("missions day " + S.day + " tier " + tier + ": " + sb);
        Changed(); G.Save();
    }
    static float Weight(Kind k) => k.id == "o2" || k.id == "food" || k.id == "rego" || k.id == "fert" || k.id == "vip" ? 2f : 1f;
    // 우주항을 연 뒤엔 우주 묶음을 늘 셋 중 하나로(3·4장 하루가 기지 일만으로 채워지지 않게)
    static void Shuffle<T>(List<T> l, System.Random r) { for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; } }

    // 진행도: 열린 발판(메뉴 스탯 제외) 수 / 3 + 정착 단계, 0~8
    int Tier()
    {
        int n = 0;
        foreach (var d in Pads.ALL) if (d.active && !d.menu && G.PadLv(d.id) >= 1) n++;
        return Mathf.Clamp(n / 3 + S.stage, 0, 8);
    }

    // ───────────────────────── 토스트 ─────────────────────────
    // 화면 아래(업그레이드 버튼 위) 작은 남색 알약 [체크][미션 아이콘] +보상, 1.5초. 여러 개면 차례로
    void Toast(string icon, string num) { toasts.Enqueue((icon, num)); if (toastCo == null) toastCo = StartCoroutine(CoToasts()); }
    IEnumerator CoToasts()
    {
        while (toasts.Count > 0)
        {
            var (ic, num) = toasts.Dequeue();
            if (HUD.I == null) continue;
            var chip = UIKit.Chip(HUD.I.transform, "missionToast", UIKit.Pill, "check", 30, num, 24, out var label, out _, 6);
            var mi = UIKit.RT(chip.transform, "ic_kind").gameObject.AddComponent<Image>(); mi.sprite = Icon(ic); mi.preserveAspect = true; mi.raycastTarget = false;
            var mle = mi.gameObject.AddComponent<LayoutElement>(); mle.preferredWidth = mle.minWidth = mle.preferredHeight = mle.minHeight = 30; mi.transform.SetSiblingIndex(1);
            label.color = UIKit.GOLD;
            var rt = chip.rectTransform; UIKit.Anchor(rt, new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(200, 46));
            var cg = chip.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
            float el = 0;
            while (el < 1.5f)
            {
                el += Time.unscaledDeltaTime;
                float a = el < 0.15f ? el / 0.15f : el > 1.25f ? (1.5f - el) / 0.25f : 1f;
                cg.alpha = Mathf.Clamp01(a);
                rt.anchoredPosition = new Vector2(0, 128 + (el < 0.15f ? (1 - el / 0.15f) * -14f : 0));
                yield return null;
            }
            Destroy(chip.gameObject);
        }
        toastCo = null;
    }

    // ───────────────────────── 아이콘 ─────────────────────────
    // UIKit에 없는 레골리스(붉은 돌)·비료(초록 자루)는 여기서 같은 방식(96px·아래 그림자·진한 테)으로 굽는다
    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string name)
    {
        if (name != "rego" && name != "fert") return UIKit.Icon(name);
        if (icons.TryGetValue(name, out var sp)) return sp;
        var p = new UIKit.Pix(96, 96);
        for (int pass = 0; pass < 2; pass++)
        {
            p.off = pass == 0 ? new Vector2(0, 6) : Vector2.zero; p.tint = pass == 0 ? new Color(0, 0, 0, 0.35f) : (Color?)null;
            if (name == "rego")
            {
                Vector2[] rock = { new Vector2(14, 66), new Vector2(22, 36), new Vector2(42, 20), new Vector2(66, 24), new Vector2(82, 46), new Vector2(80, 72), new Vector2(54, 82), new Vector2(26, 80) };
                p.Poly(rock, UIKit.Hex("5a2410"), 3.2f); p.Poly(rock, UIKit.Hex("b5532e"));
                if (pass == 1) { p.Circle(40, 38, 7, UIKit.Hex("d97a50")); p.Circle(62, 58, 5, UIKit.Hex("8e3d20")); }
            }
            else
            {
                p.RRect(18, 34, 60, 50, 14, UIKit.Hex("3f5a1e"), 3.2f); p.RRect(18, 34, 60, 50, 14, UIKit.Hex("8fae4a"));
                p.RRect(34, 14, 28, 24, 6, UIKit.Hex("3f5a1e"), 3.2f); p.RRect(34, 14, 28, 24, 6, UIKit.Hex("8fae4a"));
                if (pass == 1) { p.RRect(30, 34, 36, 6, 3, UIKit.Hex("6b4a33")); p.Circle(48, 60, 9, UIKit.Hex("cfe39a")); }
            }
        }
        var tex = p.ToTexture();
        sp = Sprite.Create(tex, new Rect(0, 0, 96, 96), new Vector2(0.5f, 0.5f), 100f);
        icons[name] = sp; return sp;
    }

    // ───────────────────────── 검증 인자 ─────────────────────────
    IEnumerator CoDev()
    {
        yield return null; yield return null;   // DevTools의 -pads·-tut·-sold가 먼저 적용되도록
        if (DevArgs.Has("-missions")) { forced = true; Rollover(); }
        var md = DevArgs.Get("-missionDone");
        if (string.IsNullOrEmpty(md)) yield break;
        float wait = 1.5f; int at = md.IndexOf('@');
        if (at >= 0) { float.TryParse(md.Substring(at + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out wait); md = md.Substring(0, at); }
        yield return new WaitForSeconds(wait);
        forced = true;
        if (S.missions.Count == 0) Rollover();
        var today = Today;
        foreach (var s in md.Split(','))
            if (int.TryParse(s, out var i) && i >= 0 && i < today.Count && !today[i].done) { today[i].prog = today[i].goal; Complete(today[i]); }
    }
}

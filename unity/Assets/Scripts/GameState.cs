using System;
using System.Collections.Generic;
using UnityEngine;

// 명세 §10.1 GameState: SaveData 래핑 + 파생값 + 이벤트. 자동저장 3초, 백그라운드 전환 시 저장.
public class GameState : MonoBehaviour
{
    public static GameState I;
    public SaveData S;

    public event Action OnBellsChanged;
    public event Action<string, int> OnPadLevel;
    public event Action OnSold;
    public event Action<string, int> OnSupplied;   // 정착민에게 공급(kind: bottle/o2/food, n)

    // 비행 중 아이템(등짐 도착 전)·선반 도착 전 병 수. 등짐은 여러 종류를 섞어 실으므로 비행 중인 것도 종류별로 센다(pendK는 마지막에 날린 종류)
    public int pend; public string pendK = "";
    readonly Dictionary<string, int> pendBy = new Dictionary<string, int>();
    public int shelfPend;
    public bool collectingCash;

    float saveT;
    public static readonly HashSet<string> ACTIVE_IDS = new HashSet<string>();

    void Awake()
    {
        I = this;
        foreach (var p in Pads.ALL) if (p.active) ACTIVE_IDS.Add(p.id);
        S = SaveSystem.Load();
        if (S.spawnT <= 0) S.spawnT = GameConfig.FIRST_SPAWN;
    }

    void Update()
    {
        saveT += Time.unscaledDeltaTime;
        if (saveT >= SaveSystem.AUTOSAVE) { saveT = 0; Save(); }
    }

    void OnApplicationPause(bool pause) { if (pause) Save(); }
    void OnApplicationQuit() { Save(); }
    public void Save() { SaveSystem.Save(S); }

    // ---- 발판 ----
    PadEntry Entry(string id, bool create)
    {
        foreach (var e in S.pads) if (e.id == id) return e;
        if (!create) return null;
        var n = new PadEntry { id = id }; S.pads.Add(n); return n;
    }
    public int PadLv(string id) { var e = Entry(id, false); return e == null ? 0 : e.lv; }
    public float PadProg(string id) { var e = Entry(id, false); return e == null ? 0 : e.prog; }
    public void SetPadProg(string id, float v) { Entry(id, true).prog = v; }
    public void SetPadLv(string id, int lv)
    {
        var e = Entry(id, true); e.lv = lv; e.prog = 0;
        OnPadLevel?.Invoke(id, lv);
        Save();
    }
    public float PadCost(string id) => Pads.Get(id).Cost(PadLv(id));
    public float PadRemain(string id) => PadCost(id) - PadProg(id);
    public bool PadReq(string id) => Pads.Get(id).req(this);
    public bool PadMaxed(string id) => PadLv(id) >= Pads.Get(id).max;

    // ---- 파생값 (§1.2) ----
    public int CarryMax => GameConfig.CARRY[Mathf.Min(6, PadLv("hand"))] + 3 * Mathf.Min(2, S.prestige) + HQSystem.CarryBonus(this);   // 본부 Lv1 +4
    public int ShelfCap => 8 + PadLv("shelf") * 7;
    public int BottlePrice => Mathf.FloorToInt((12 + PadLv("price") * 4) * Mathf.Pow(1.5f, S.prestige) * HQSystem.PriceMul(this) + 0.5f);   // 본부 Lv2 ×1.1
    public float MeltTime => 1.6f / (1 + PadLv("melter") * 0.35f) / MachineSpeedMul;
    public float BottleTime => 1.6f / (1 + PadLv("bottler") * 0.35f) / MachineSpeedMul;
    // 정착민 도착 간격: 본부 Lv3 ×0.9 · 거주 구역 인구만큼 조금 더 줄어듦
    public float BaseGap => Mathf.Max(2f, 6.5f - PadLv("ads") * 0.9f) * HQSystem.GapMul(this) * (Frontier.I != null ? Frontier.I.GapMul : 1f);
    // 공용 기계 속도 배율(곱셈 훅): 해빙기·병입기·생존 기지 시설(SettleStations) 처리 시간을 이 값으로 나눈다. 본부 Lv4 ×1.1 · 태양광 전력(낮·배터리 동안 ×1.2)
    public float MachineSpeedMul => HQSystem.SpeedMul(this) * Frontier.PowerMul;
    // 생존 기지 시설별 속도 배율(시트 전용 발판 elecspd·purspd·mixspd·grnspd): 공용 MachineSpeedMul 과 곱한다. i 는 SettleStations 시설 번호
    public float FacSpeedMul(int i) => i < 0 || i >= Pads.FAC_SPD.Length ? 1f : 1f + 0.3f * PadLv(Pads.FAC_SPD[i]);
    public float Speed => (GameConfig.BASE_SPEED + GameConfig.SPEED_PER_LV * PadLv("spd")) * MarsEvents.MoveMul;   // 먼지 폭풍 중 −15%
    public bool HasCashier => PadLv("cashier") >= 1;

    // ---- 등짐(섞어 싣기, 2026-10-25 jake "재료·물병·돈을 각각 모두 등에") ----
    // S.p.carry는 종류별로 묶여 있다(같은 종류는 붙어 있고, 처음 실은 종류가 아래). 줍기는 용량만 보면 되고, 넣기는 그 종류를 어디서든 하나 뺀다
    public int Carry => S.p.carry.Count;
    public string CarryTop => S.p.carry.Count == 0 ? "" : S.p.carry[S.p.carry.Count - 1];
    public int Held(string kind) { int n = 0; foreach (var k in S.p.carry) if (k == kind) n++; return n; }
    public bool Holds(string kind) => S.p.carry.Contains(kind);
    public int PendOf(string kind) => pendBy.TryGetValue(kind, out var n) ? n : 0;
    // 들고 있는 것 + 날아오는 것
    public int HeldAll(string kind) => Held(kind) + PendOf(kind);
    public bool CanCarry(string kind) => Carry + pend < CarryMax;
    public void PendAdd(string kind) { pend++; pendK = kind; pendBy[kind] = PendOf(kind) + 1; }
    public void PendDone(string kind)
    {
        pend = Mathf.Max(0, pend - 1); pendBy[kind] = Mathf.Max(0, PendOf(kind) - 1);
        if (pend == 0) { pendK = ""; pendBy.Clear(); }
    }
    // 실을 자리: 같은 종류 묶음의 맨 끝(없으면 맨 위)
    public int CarryInsertIndex(string kind) { int i = S.p.carry.LastIndexOf(kind); return i < 0 ? S.p.carry.Count : i + 1; }
    public int AddCarry(string kind) { int i = CarryInsertIndex(kind); S.p.carry.Insert(i, kind); return i; }
    // 그 종류 하나 빼기(묶음의 맨 위). 없으면 -1
    public int RemoveCarry(string kind) { int i = S.p.carry.LastIndexOf(kind); if (i >= 0) S.p.carry.RemoveAt(i); return i; }
    // 등짐 구성 요약(로그용): "ice×12 o2×4"
    public string CarrySummary()
    {
        var sb = new System.Text.StringBuilder(); string cur = null; int n = 0;
        foreach (var k in S.p.carry) { if (k != cur) { if (cur != null) sb.Append(cur).Append('×').Append(n).Append(' '); cur = k; n = 0; } n++; }
        if (cur != null) sb.Append(cur).Append('×').Append(n);
        return sb.ToString().Trim();
    }
    // 등짐 맨 아래 지폐 묶음 수(보유 벨에 비례, 용량은 차지하지 않음): 병값×4당 1장, 최대 BILL_LAYERS
    public int BillLayers => Mathf.Clamp(Mathf.FloorToInt(S.bells / Mathf.Max(1f, BottlePrice * 4f)), 0, GameConfig.BILL_LAYERS);

    // 얼음을 더 주울 이유가 있나: 해빙기 큐 + 등짐·비행 중 얼음이 상한(MELT_CAP) 아래이고 물탱크가 넉넉하지 않을 때(WATER_FULL). 아니면 얼음밭을 지나가도 줍지 않는다(Player 1번)
    // 등짐이 100개로 커져서(10/25) 들고 있는 얼음까지 센다: 안 그러면 해빙기에 100개를 한 번에 부어 큐가 넘친다
    public bool IceWantedHeld => S.meltQ + HeldAll("ice") < GameConfig.MELT_CAP && S.water < GameConfig.WATER_FULL;
    // 물통을 둘 곳이 있나: 카운터 물 칸·전기분해기(열려 있고 큐 절반·출력에 자리) 빈자리가 이미 든 물통보다 많을 때. 둘 다 가득이면 병입기를 지나가도 병을 집지 않는다(Player 3번)
    public bool BottleHasPlace
    {
        get
        {
            int room = Mathf.Max(0, ShelfCap - S.shelf - shelfPend);
            var ss = SettleStations.I;
            if (ss != null && ss.Open(0) && S.elecOut < GameConfig.SETTLE_OUT_CAP) room += Mathf.Max(0, GameConfig.SETTLE_Q_CAP / 2 - S.elecQ - ss.qPend[0]);
            room += SpacePort.Need("bottle");   // 3장: 화물선 주문·연료 공장 큐 몫
            return HeldAll("bottle") < room;
        }
    }
    // 등짐에 더 받을 이유가 있나(Player 줍기·받기 공통). 용량이 커져도 한 종류가 쓸 데 없이 쌓이지 않게 종류마다 상한을 둔다
    //  얼음·물통은 위 규칙, 레골리스는 정화기 큐 + 든 것이 큐 상한 아래, 산소통·식량은 카운터 칸 하나 분량(ShelfCap)까지, 흙·비료는 큐 상한(40)까지
    public bool WantsMore(string kind)
    {
        if (!CanCarry(kind)) return false;
        var ss = SettleStations.I;
        switch (kind)
        {
            case "ice": return IceWantedHeld || HeldAll("ice") < SpacePort.Need("ice");   // 3장: 연료 공장 큐 몫
            case "bottle": return BottleHasPlace;
            case "rego": return ss != null && S.purQ + ss.qPend[1] + HeldAll("rego") < GameConfig.SETTLE_Q_CAP;
            case "o2": case "food": return HeldAll(kind) < ShelfCap + SpacePort.Need(kind);   // 3장: 내려앉은 화물선 주문 몫까지
            case "fuel": return HeldAll(kind) < SpacePort.Need(kind);                          // 연료통은 발사대가 받을 만큼만
            default: return HeldAll(kind) < GameConfig.SETTLE_Q_CAP;
        }
    }

    // 수입 출처별 누계(페이싱 점검용, 저장 안 함): src = sales·mission·stage·quest·tax·subsidy·event·pen 등, 없으면 etc(돔 팁 등)
    public static readonly Dictionary<string, float> Income = new Dictionary<string, float>();
    public void AddBells(float n, string src = null)
    {
        S.bells += n;
        if (n > 0) { var k = src ?? "etc"; Income[k] = (Income.TryGetValue(k, out var v) ? v : 0f) + n; }
        OnBellsChanged?.Invoke();
    }
    public void NotifySold() { OnSold?.Invoke(); }

    // ---- 생존 기지: 정착민 공급 ----
    public static readonly string[] SUPPLY_KINDS = { "bottle", "o2", "food" };
    public static string KindName(string kind) => kind == "o2" ? "산소" : kind == "food" ? "식량" : kind == "bottle" ? "물" : kind == "rego" ? "레골리스" : kind == "soil" ? "흙" : kind == "fert" ? "비료" : kind == "ice" ? "얼음" : kind == "fuel" ? "연료" : kind;
    public bool KindOpen(string kind) => kind == "bottle" || (kind == "o2" && PadLv("o2") >= 1) || (kind == "food" && PadLv("farm") >= 1);
    public int PriceOf(string kind) => kind == "o2" ? Mathf.FloorToInt(BottlePrice * 1.6f + 0.5f) : kind == "food" ? Mathf.FloorToInt(BottlePrice * 2.2f + 0.5f) : BottlePrice;
    public int ShelfOf(string kind) => kind == "o2" ? S.shelfO2 : kind == "food" ? S.shelfFood : S.shelf;
    public void AddShelf(string kind, int n) { if (kind == "o2") S.shelfO2 += n; else if (kind == "food") S.shelfFood += n; else S.shelf += n; }
    public void NotifySupplied(string kind, int n)
    {
        if (kind == "o2") S.supO += n; else if (kind == "food") S.supF += n; else S.supW += n;
        OnSupplied?.Invoke(kind, n);
    }
}

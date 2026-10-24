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

    // 비행 중 아이템(등짐 도착 전)·선반 도착 전 병 수
    public int pend; public string pendK = "";
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
    public int BottlePrice => Mathf.FloorToInt((12 + PadLv("price") * 6) * Mathf.Pow(1.5f, S.prestige) * HQSystem.PriceMul(this) + 0.5f);   // 본부 Lv2 ×1.1
    public float MeltTime => 1.6f / (1 + PadLv("melter") * 0.35f) / MachineSpeedMul;
    public float BottleTime => 1.6f / (1 + PadLv("bottler") * 0.35f) / MachineSpeedMul;
    public float BaseGap => Mathf.Max(2f, 6.5f - PadLv("ads") * 0.9f) * HQSystem.GapMul(this);   // 본부 Lv3 정착민 도착 간격 ×0.9
    // 공용 기계 속도 배율(곱셈 훅): 해빙기·병입기·생존 기지 시설(SettleStations) 처리 시간을 이 값으로 나눈다.
    // 본부 Lv4 몫(×1.1)만 여기서 곱한다. 다른 배율(전력 등)은 이 줄에 곱해 넣는다
    public float MachineSpeedMul => HQSystem.SpeedMul(this);
    public float Speed => GameConfig.BASE_SPEED + GameConfig.SPEED_PER_LV * PadLv("spd");
    public bool HasCashier => PadLv("cashier") >= 1;

    public int Carry => S.p.carry.Count;
    public string CarryTop => S.p.carry.Count == 0 ? "" : S.p.carry[S.p.carry.Count - 1];
    public bool CanCarry(string kind) => Carry + pend < CarryMax && (Carry == 0 || CarryTop == kind) && (pend == 0 || pendK == kind);

    public void AddBells(float n)
    {
        S.bells += n;
        OnBellsChanged?.Invoke();
    }
    public void NotifySold() { OnSold?.Invoke(); }

    // ---- 생존 기지: 정착민 공급 ----
    public static readonly string[] SUPPLY_KINDS = { "bottle", "o2", "food" };
    public static string KindName(string kind) => kind == "o2" ? "산소" : kind == "food" ? "식량" : kind == "bottle" ? "물" : kind == "rego" ? "레골리스" : kind == "soil" ? "흙" : kind == "fert" ? "비료" : kind == "ice" ? "얼음" : kind;
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

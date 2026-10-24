using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 정착 목표 5단계(docs/settlement-design.md "정착 목표"). S.stage = 달성한 단계 수(0~5, 5 = 정착 완료).
//  1 물 확보: 물 공급 30 → 100벨 / 2 숨 쉴 공기: 전기분해기·산소 공급 20 → 300벨 / 3 흙 살리기: 채굴장·토양 공장·비료 20 → 600벨
//  4 첫 수확: 온실·식량 공급 20 → 1,000벨 / 5 정착 완료: 정착민 60·물 200·산소 100·식량 100 누적 → 큰 축하
// 공급 누계(S.supW/supO/supF)는 결제 때 GameState.NotifySupplied가, 비료 누계(S.fertMade)는 배합기가 올린다. 이 클래스는 읽기만 한다.
// 0.3초마다 판정, 걷는 중·아이템 비행 중·목표 상자 팝 중이면 최대 3초 보류(QuestManager와 같은 규칙). 달성: 진행·보상·저장을 한 번에 → 짧은 배너.
// 진행률 API(Items·StageMet·FocusZones)는 MapScreen(지도)과 HUD(정착 n/5 알약)가 쓴다.
// 검증 인자: -stage N(단계 강제, 보상 없음) · -sup 물,산소,식량[,비료[,정착민]](누계 강제). 앱 실행당 1회.
public class SettlementManager : MonoBehaviour
{
    public static SettlementManager I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public const int COUNT = 5;
    public class StageDef { public string name; public int reward; public string[] zones; }
    // zones: 이 단계에 필요한 구역(지도에서 금테로 강조)
    public static readonly StageDef[] STAGES =
    {
        new StageDef{ name="물 확보",    reward=100,  zones=new[]{ "bottle" } },
        new StageDef{ name="숨 쉴 공기", reward=300,  zones=new[]{ "o2" } },
        new StageDef{ name="흙 살리기",  reward=600,  zones=new[]{ "mine", "soil" } },
        new StageDef{ name="첫 수확",    reward=1000, zones=new[]{ "farm" } },
        new StageDef{ name="정착 완료",  reward=0,    zones=new[]{ "shop" } },
    };

    /// 단계 항목 하나: 이름·현재·목표. bin = 설치형(0/1, 진행 막대 대신 체크)
    public struct Item
    {
        public string name; public int cur, goal; public bool bin;
        public bool Done => cur >= goal;
        public float Frac => goal <= 0 ? 1f : Mathf.Clamp01((float)cur / goal);
    }

    /// 단계 달성 알림(달성한 단계 번호 1~5)
    public event Action<int> OnStage;

    public int Stage => G == null ? 0 : Mathf.Clamp(S.stage, 0, COUNT);
    public bool Settled => Stage >= COUNT;
    public StageDef Current => Settled ? null : STAGES[Stage];
    public string[] FocusZones => Settled ? new string[0] : STAGES[Stage].zones;

    static Item Bin(string n, bool done) => new Item { name = n, cur = done ? 1 : 0, goal = 1, bin = true };
    static Item Cnt(string n, int cur, int goal) => new Item { name = n, cur = Mathf.Max(0, cur), goal = goal };

    /// idx 0~4 단계의 항목 목록(지금 값 기준)
    public List<Item> Items(int idx)
    {
        var l = new List<Item>();
        if (G == null) return l;
        switch (idx)
        {
            case 0: l.Add(Cnt("물 공급", S.supW, 30)); break;
            case 1: l.Add(Bin("전기분해기 설치", G.PadLv("o2") >= 1)); l.Add(Cnt("산소 공급", S.supO, 20)); break;
            case 2: l.Add(Bin("채굴장 열기", G.PadLv("mine") >= 1)); l.Add(Bin("토양 공장 열기", G.PadLv("soil") >= 1)); l.Add(Cnt("비료 만들기", S.fertMade, 20)); break;
            case 3: l.Add(Bin("온실 설치", G.PadLv("farm") >= 1)); l.Add(Cnt("식량 공급", S.supF, 20)); break;
            case 4: l.Add(Cnt("정착민", S.custs, 60)); l.Add(Cnt("물 누적", S.supW, 200)); l.Add(Cnt("산소 누적", S.supO, 100)); l.Add(Cnt("식량 누적", S.supF, 100)); break;
        }
        return l;
    }
    public bool StageMet(int idx) { foreach (var it in Items(idx)) if (!it.Done) return false; return true; }
    /// 단계 진행률(항목 평균, 0~1)
    public float StageFrac(int idx)
    {
        var l = Items(idx); if (l.Count == 0) return 0f;
        float f = 0; foreach (var it in l) f += it.Frac; return f / l.Count;
    }

    float tickT, holdT, busyT;
    static bool devApplied;

    void Awake() { I = this; }

    void Start()
    {
        if (devApplied) return;   // 재조립(Restart) 뒤 다시 덮어쓰지 않게
        devApplied = true;
        if (int.TryParse(DevArgs.Get("-stage"), out var st)) S.stage = Mathf.Clamp(st, 0, COUNT);
        var sup = DevArgs.Get("-sup");
        if (!string.IsNullOrEmpty(sup))
        {
            var a = sup.Split(','); int v;
            if (a.Length > 0 && int.TryParse(a[0], out v)) S.supW = v;
            if (a.Length > 1 && int.TryParse(a[1], out v)) S.supO = v;
            if (a.Length > 2 && int.TryParse(a[2], out v)) S.supF = v;
            if (a.Length > 3 && int.TryParse(a[3], out v)) S.fertMade = v;
            if (a.Length > 4 && int.TryParse(a[4], out v)) S.custs = v;
        }
    }

    void Update()
    {
        if (G == null) return;
        float dt = Time.deltaTime;
        if (busyT > 0) { busyT -= dt; return; }
        tickT += dt; if (tickT < 0.3f) return; tickT = 0;
        if (Settled || PrestigeManager.I != null && PrestigeManager.I.Busy) return;
        if (!StageMet(Stage)) { holdT = 0; return; }
        // 보류: 걷는 중·아이템 비행 중·목표 상자 팝(QuestManager) 중 — 최대 3초
        bool busy = (Player.I != null && Player.I.walking) || (ArcFlyer.I != null && ArcFlyer.I.Active > 0) || (HUD.I != null && HUD.I.transform.Find("chest") != null);
        if (busy && holdT < 3f) { holdT += 0.3f; return; }
        holdT = 0;
        Complete(Stage);
    }

    void Complete(int idx)
    {
        var def = STAGES[idx];
        S.stage = idx + 1;
        if (def.reward > 0) G.AddBells(def.reward);
        G.Save();   // 진행·보상·저장을 한 번에(연출 중 종료돼도 유지)
        Debug.Log("settlement stage " + S.stage + " (" + def.name + ") supW=" + S.supW + " supO=" + S.supO + " supF=" + S.supF + " fert=" + S.fertMade + " custs=" + S.custs);
        OnStage?.Invoke(S.stage);
        bool final = S.stage >= COUNT;
        busyT = final ? 3.6f : 2.2f;
        StartCoroutine(final ? CoFinal() : CoBanner(idx));
    }

    // ---- 단계 달성: 금색 배너(화면 위쪽, 입력 막지 않음. 목표 상자 팝(중앙 +120)과 겹치지 않는 높이) 팝 0.4s → 1.1s → 떠오르며 사라짐 ----
    IEnumerator CoBanner(int idx)
    {
        if (HUD.I == null) yield break;
        var def = STAGES[idx];
        var box = UIKit.Panel(HUD.I.transform, "stageBanner", UIKit.SignOk);
        var brt = box.rectTransform; UIKit.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(340, 108));
        var cg = box.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
        var row = UIKit.Row(box.transform, "row", 8); UIKit.Anchor(row.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(320, 48));
        UIKit.IconImg(row.transform, "flag", 36);
        var t1 = UIKit.Text(row.transform, "정착 " + (idx + 1) + "/" + COUNT + " · " + def.name, 28, UIKit.SignOk.text); UIKit.Stroke(t1, Color.white, 1.2f);
        var row2 = UIKit.Row(box.transform, "row2", 6); UIKit.Anchor(row2.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(320, 38));
        UIKit.IconImg(row2.transform, "bell", 28);
        var t2 = UIKit.Text(row2.transform, "+" + def.reward.ToString("N0"), 30, UIKit.GOLD_INK); UIKit.Stroke(t2, Color.white, 1.2f);
        AudioManager.I?.Play("up", 1.1f); AudioManager.I?.Play("ding", 1.3f, 0.6f); JuiceFX.I?.Vibrate(30);
        float el = 0; bool burst = false;
        while (el < 0.4f)
        {
            el += Time.unscaledDeltaTime; brt.localScale = Vector3.one * Mathf.Max(0.001f, JuiceFX.EaseOutBack(Mathf.Clamp01(el / 0.4f)));
            if (!burst && el >= 0.25f)
            {
                burst = true; JuiceFX.I?.Flash(0.25f); JuiceFX.I?.Shake(3);
                HUD.I.Coins(brt.position, () => HUD.I.BellsScreen(), 10);
                if (Player.I != null) JuiceFX.I?.Confetti(Player.I.view.position + Vector3.up * 1.5f, 18);
            }
            yield return null;
        }
        brt.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(1.1f);
        yield return FadeUp(brt, cg, 0.35f);
    }

    // ---- 정착 완료: 큰 금색 판(왕관 · 정착 완료!) + 색종이·링·플래시, 2.2s ----
    IEnumerator CoFinal()
    {
        if (HUD.I == null) yield break;
        var box = UIKit.Panel(HUD.I.transform, "settledBanner", UIKit.SignOk);
        var brt = box.rectTransform; UIKit.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0, 320), new Vector2(400, 150));
        var cg = box.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
        var row = UIKit.Row(box.transform, "row", 10); UIKit.Anchor(row.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(380, 70));
        UIKit.IconImg(row.transform, "crown", 56);
        var t1 = UIKit.Text(row.transform, "정착 완료!", 48, UIKit.SignOk.text); UIKit.Stroke(t1, Color.white, 1.6f); UIKit.DropShadow(t1, 2f, 0.25f);
        var t2 = UIKit.Text(box.transform, "새 정착지로 떠날 수 있어요", 22, UIKit.GOLD_INK);
        UIKit.Anchor(t2.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(380, 34));
        AudioManager.I?.Play("up", 1f); AudioManager.I?.Play("ding", 1.2f, 0.8f);
        JuiceFX.I?.Flash(0.7f); JuiceFX.I?.Shake(7); JuiceFX.I?.Vibrate(60);
        if (Player.I != null)
        {
            var at = Player.I.view.position;
            JuiceFX.I?.Confetti(at + Vector3.up * 1.5f, 48);
            JuiceFX.I?.Ring(at, GameConfig.GOLD, 40); JuiceFX.I?.Ring(at, Color.white, 70);
        }
        float el = 0;
        while (el < 0.5f) { el += Time.unscaledDeltaTime; brt.localScale = Vector3.one * Mathf.Max(0.001f, JuiceFX.EaseOutBack(Mathf.Clamp01(el / 0.5f))); yield return null; }
        brt.localScale = Vector3.one;
        el = 0;
        while (el < 2.2f) { el += Time.unscaledDeltaTime; brt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(el * 5f) * 2f * (1f - el / 2.2f)); yield return null; }
        brt.localRotation = Quaternion.identity;
        yield return FadeUp(brt, cg, 0.4f);
    }

    static IEnumerator FadeUp(RectTransform brt, CanvasGroup cg, float dur)
    {
        float el = 0; var p0 = brt.anchoredPosition;
        while (el < dur) { el += Time.unscaledDeltaTime; float e = Mathf.Clamp01(el / dur); brt.anchoredPosition = p0 + new Vector2(0, 40f * e); cg.alpha = 1f - e; yield return null; }
        Destroy(brt.gameObject);
    }
}

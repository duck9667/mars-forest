using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 정착 목표 10단계, 두 장(docs/settlement-design.md "정착 목표" · docs/chapter2-design.md "정착 목표 2장"). S.stage = 달성한 단계 수(0~10, 10 = 화성 마을 선포).
//  1장 생존(1~5): 1 물 확보: 물 공급 30 → 100벨 / 2 숨 쉴 공기: 전기분해기·산소 공급 20 → 300벨 / 3 흙 살리기: 채굴장·토양 공장·비료 20 → 600벨
//   / 4 첫 수확: 온실·식량 공급 20 → 1,000벨 / 5 생존 완료: 정착민 60·물 200·산소 100·식량 100 누적 → 1,200벨(1장 끝, 2장 시작 무전)
//  2장 마을(6~10): 6 전력 확보: 태양광 단지·전력으로 기계 가동 180초 → 1,500벨 / 7 첫 이웃: 거주 구역·인구 10 → 2,000벨
//   / 8 기지 정비: 본부 Lv3·산소 누적 250 → 3,000벨 / 9 자급 마을: 인구 24·하루 미션 3개 모두 완료 1번(8단계 뒤) → 4,000벨
//   / 10 화성 마을 선포: 본부 Lv5·인구 32·물 1,500·산소 350·식량 350 누적 → 큰 축하(약한 번쩍임) + 엔딩 무전(StoryManager.QueueEnding)
//  (수치는 페이싱 점검 10/25, docs/pacing-report.md)
// 공급 누계(S.supW/supO/supF)는 결제 때 GameState.NotifySupplied가, 비료 누계(S.fertMade)는 배합기가, 인구(S.pop)는 Frontier가 올린다.
// 2장 전용 누계 두 개는 여기서 센다: S.powerRun(2장에 들어선 뒤 S.stage ≥ 5, 태양광 전력이 들어오는 동안 기계 하나라도 일한 초),
//  S.missionAll(8단계 뒤 S.stage ≥ 8, 하루 미션 3개를 모두 끝낸 적 있음 0/1, MissionBoard 보너스 상자가 다 찬 순간).
// 0.3초마다 판정, 걷는 중·아이템 비행 중·목표 상자 팝 중이면 최대 3초 보류(QuestManager와 같은 규칙). 달성: 진행·보상·저장을 한 번에 → 짧은 배너 + 무전(StoryManager.Push).
// 옛 저장(5단계에서 멈춤 = 예전 "정착 완료")은 S.stage 5 그대로 6단계부터 이어진다. 이주(PrestigeManager)하면 0부터.
// 진행률 API(Items·StageMet·FocusZones·Chapter)는 MapScreen(지도)과 HUD("1장 n/5" 알약)가 쓴다.
// 검증 인자: -stage N(단계 강제 0~10, 보상 없음) · -sup 물,산소,식량[,비료[,정착민]](누계 강제) · -power 초 · -mall(하루 미션 완료 처리). 앱 실행당 1회.
public class SettlementManager : MonoBehaviour
{
    public static SettlementManager I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public const int COUNT = 10, PER_CHAPTER = 5;
    public static readonly string[] CHAPTERS = { "생존", "마을" };
    public class StageDef { public string name; public int reward; public string[] zones; }
    // zones: 이 단계에 필요한 구역(지도에서 금테로 강조). "hq"는 구역이 아니라 본부 터
    public static readonly StageDef[] STAGES =
    {
        // 1장 생존
        new StageDef{ name="물 확보",    reward=100,  zones=new[]{ "bottle" } },
        new StageDef{ name="숨 쉴 공기", reward=300,  zones=new[]{ "o2" } },
        new StageDef{ name="흙 살리기",  reward=600,  zones=new[]{ "mine", "soil" } },
        new StageDef{ name="첫 수확",    reward=1000, zones=new[]{ "farm" } },
        new StageDef{ name="생존 완료",  reward=1200, zones=new[]{ "shop" } },
        // 2장 마을
        new StageDef{ name="전력 확보",  reward=1500, zones=new[]{ "solar" } },
        new StageDef{ name="첫 이웃",    reward=2000, zones=new[]{ "hab" } },
        new StageDef{ name="기지 정비",  reward=3000, zones=new[]{ "hq" } },
        new StageDef{ name="자급 마을",  reward=4000, zones=new[]{ "hab" } },
        new StageDef{ name="화성 마을 선포", reward=0, zones=new[]{ "hq", "hab" } },
    };
    public const float POWER_GOAL = 180f;

    /// 단계 항목 하나: 이름·현재·목표. bin = 설치형(0/1, 진행 막대 대신 체크)
    public struct Item
    {
        public string name; public int cur, goal; public bool bin;
        public bool Done => cur >= goal;
        public float Frac => goal <= 0 ? 1f : Mathf.Clamp01((float)cur / goal);
    }

    /// 단계 달성 알림(달성한 단계 번호 1~10)
    public event Action<int> OnStage;

    public int Stage => G == null ? 0 : Mathf.Clamp(S.stage, 0, COUNT);
    public bool Settled => Stage >= COUNT;
    public StageDef Current => Settled ? null : STAGES[Stage];
    public string[] FocusZones => Settled ? new string[0] : STAGES[Stage].zones;
    /// 지금 장(0 = 1장, 1 = 2장). 1장을 끝내면(5단계) 바로 2장, 마을 선포 뒤에도 2장
    public int Chapter => Mathf.Min(Stage / PER_CHAPTER, CHAPTERS.Length - 1);
    /// 지금 장 안에서 달성한 단계 수(0~5)
    public int ChapterDone => Stage - Chapter * PER_CHAPTER;
    /// "1장 생존" 같은 장 이름
    public static string ChapterName(int ch) => (ch + 1) + "장 " + CHAPTERS[Mathf.Clamp(ch, 0, CHAPTERS.Length - 1)];
    /// 단계 idx(0~9)의 장 안 번호 "n/5"
    public static string StageTag(int idx) => (idx / PER_CHAPTER + 1) + "장 " + (idx % PER_CHAPTER + 1) + "/" + PER_CHAPTER;

    static Item Bin(string n, bool done) => new Item { name = n, cur = done ? 1 : 0, goal = 1, bin = true };
    static Item Cnt(string n, int cur, int goal) => new Item { name = n, cur = Mathf.Max(0, cur), goal = goal };

    /// idx 0~9 단계의 항목 목록(지금 값 기준)
    public List<Item> Items(int idx)
    {
        var l = new List<Item>();
        if (G == null) return l;
        switch (idx)
        {
            // 1장 생존
            case 0: l.Add(Cnt("물 공급", S.supW, 30)); break;
            case 1: l.Add(Bin("전기분해기 설치", G.PadLv("o2") >= 1)); l.Add(Cnt("산소 공급", S.supO, 20)); break;
            case 2: l.Add(Bin("채굴장 열기", G.PadLv("mine") >= 1)); l.Add(Bin("토양 공장 열기", G.PadLv("soil") >= 1)); l.Add(Cnt("비료 만들기", S.fertMade, 20)); break;
            case 3: l.Add(Bin("온실 설치", G.PadLv("farm") >= 1)); l.Add(Cnt("식량 공급", S.supF, 20)); break;
            case 4: l.Add(Cnt("정착민", S.custs, 60)); l.Add(Cnt("물 누적", S.supW, 200)); l.Add(Cnt("산소 누적", S.supO, 100)); l.Add(Cnt("식량 누적", S.supF, 100)); break;
            // 2장 마을
            case 5: l.Add(Bin("태양광 단지 설치", G.PadLv("solar") >= 1)); l.Add(Cnt("전력 가동(초)", Mathf.FloorToInt(S.powerRun), (int)POWER_GOAL)); break;
            case 6: l.Add(Bin("거주 구역 열기", G.PadLv("hab") >= 1)); l.Add(Cnt("인구", S.pop, 10)); break;
            case 7: l.Add(Cnt("본부 레벨", G.PadLv("hq"), 3)); l.Add(Cnt("산소 누적", S.supO, 250)); break;
            case 8: l.Add(Cnt("인구", S.pop, 24)); l.Add(Bin("하루 미션 3개 완료", S.missionAll >= 1)); break;
            case 9: l.Add(Cnt("본부 레벨", G.PadLv("hq"), 5)); l.Add(Cnt("인구", S.pop, 32)); l.Add(Cnt("물 누적", S.supW, 1500)); l.Add(Cnt("산소 누적", S.supO, 350)); l.Add(Cnt("식량 누적", S.supF, 350)); break;
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
        if (float.TryParse(DevArgs.Get("-power"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pw)) S.powerRun = Mathf.Max(0, pw);
        if (DevArgs.Has("-mall")) S.missionAll = 1;
    }

    void Update()
    {
        if (G == null) return;
        float dt = Time.deltaTime;
        CountChapter2(dt);
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

    // 2장 누계: 2장에 들어선 뒤(S.stage ≥ 5)부터만 센다
    void CountChapter2(float dt)
    {
        if (S.stage < PER_CHAPTER) return;
        // 전력 가동: 태양광 전력(낮 햇빛·밤 배터리)이 들어오는 동안 기계 하나라도 입력이 있어 일하는 시간
        if (S.powerRun < POWER_GOAL && Frontier.I != null && Frontier.I.SolarOpen && Frontier.PowerMul > 1.05f)
        {
            bool working = S.meltQ > 0 || S.elecQ > 0 || S.purQ > 0 || S.mixQ > 0 || S.grnQ > 0;
            if (working) S.powerRun = Mathf.Min(POWER_GOAL, S.powerRun + dt);
        }
        // 하루 미션 3개 모두 완료: 9단계(자급 마을)가 지금 목표일 때(S.stage ≥ 8) 보너스 상자가 다 찬 날이 한 번이라도. 2장 첫날에 미리 채워 두면 9단계가 거저 지나가서
        if (S.missionAll == 0 && S.stage >= 8 && MissionBoard.I != null)
        {
            var b = MissionBoard.I.Bonus;
            if (b != null && b.done && b.goal >= MissionBoard.COUNT) { S.missionAll = 1; Debug.Log("settlement: 하루 미션 모두 완료(day " + S.day + ")"); }
        }
    }

    void Complete(int idx)
    {
        var def = STAGES[idx];
        S.stage = idx + 1;
        if (def.reward > 0) G.AddBells(def.reward, "stage");
        G.Save();   // 진행·보상·저장을 한 번에(연출 중 종료돼도 유지)
        Debug.Log("settlement stage " + S.stage + " (" + def.name + ") supW=" + S.supW + " supO=" + S.supO + " supF=" + S.supF + " fert=" + S.fertMade + " custs=" + S.custs
            + " pop=" + S.pop + " hq=" + G.PadLv("hq") + " powerRun=" + S.powerRun.ToString("0") + " missionAll=" + S.missionAll);
        OnStage?.Invoke(S.stage);
        // 무전: 단계마다 한 줄(배너 뒤에), 1장 끝이면 2장 시작 무전, 마을 선포면 엔딩 컷(큰 축하판이 사라진 뒤)
        var story = StoryManager.I;
        if (story != null)
        {
            story.Push("stage_" + S.stage, 1.0f);
            if (S.stage == PER_CHAPTER) story.Push("ch2_start", 1.3f);
            if (S.stage >= COUNT) story.QueueEnding(1.2f);
        }
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
        var t1 = UIKit.Text(row.transform, StageTag(idx) + " · " + def.name, 28, UIKit.SignOk.text); UIKit.Stroke(t1, Color.white, 1.2f);
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
                burst = true; JuiceFX.I?.Shake(3);
                HUD.I.Coins(brt.position, () => HUD.I.BellsScreen(), 10);
                if (Player.I != null) JuiceFX.I?.Confetti(Player.I.view.position + Vector3.up * 1.5f, 18);
            }
            yield return null;
        }
        brt.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(1.1f);
        yield return FadeUp(brt, cg, 0.35f);
    }

    // ---- 화성 마을 선포(10단계, 게임 하나의 끝): 큰 금색 판(왕관 · 화성 마을 선포!) + 색종이·링 + 약한 번쩍임, 2.2s ----
    IEnumerator CoFinal()
    {
        if (HUD.I == null) yield break;
        var box = UIKit.Panel(HUD.I.transform, "settledBanner", UIKit.SignOk);
        var brt = box.rectTransform; UIKit.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0, 320), new Vector2(400, 150));
        var cg = box.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
        var row = UIKit.Row(box.transform, "row", 10); UIKit.Anchor(row.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(380, 70));
        UIKit.IconImg(row.transform, "crown", 56);
        var t1 = UIKit.Text(row.transform, "화성 마을 선포!", 44, UIKit.SignOk.text); UIKit.Stroke(t1, Color.white, 1.6f); UIKit.DropShadow(t1, 2f, 0.25f);
        var t2 = UIKit.Text(box.transform, "새 정착지로 떠날 수 있어요", 22, UIKit.GOLD_INK);
        UIKit.Anchor(t2.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(380, 34));
        AudioManager.I?.Play("up", 1f); AudioManager.I?.Play("ding", 1.2f, 0.8f);
        JuiceFX.I?.Flash(0.18f); JuiceFX.I?.Shake(5); JuiceFX.I?.Vibrate(50);   // 번쩍임은 이 단계에만, 약하게(아트 바이블)
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

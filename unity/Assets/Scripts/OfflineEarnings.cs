using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// §5.4 부재 중 수익 offlineEarn(): 원본 index.html 884~887행.
//   el = now - savedAt(초); el < 120 또는 삐걱 없음 → 없음
//   secs = min(el, 3h); rate = min(1/bottleTime, 1.55/baseGap) × bottlePrice × (runner ? 0.5 : 0.25) × (drill ? 1 : 0.7)
//   n = round(secs × rate); n < 10 → 없음
// 시작(원본은 시작 버튼 탭) + 앱 복귀(OnApplicationPause(false)·OnApplicationFocus(true), 원본 visibilitychange) 때 계산.
// GameState 자동저장(3초)이 savedAt을 덮기 전에 Awake에서 저장된 값을 잡아 둔다. 한 번 계산한 시각 이후로만 다시 세서 중복 지급을 막는다.
// 모달: 남색 판 + 회전하는 금빛 살 위 보물 상자(들썩임) + 벨 "+n"(0부터 차오름) + "m분 동안 삐걱이 팔았어요" + 확인 → 동전 14개 HUD로.
// 검증 인자: -offline <초> (저장 시각을 그만큼 과거로 간주, 앱 실행당 1회). -shot 검증 실행에선 -offline이 있을 때만 계산,
// -autoplay 중엔 4초 뒤 스스로 확인(봇이 막히지 않게)
public class OfflineEarnings : MonoBehaviour
{
    public static OfflineEarnings I;
    GameState G => GameState.I;

    public const float MIN_AWAY = 120f, MAX_AWAY = 3 * 3600f;
    public const int MIN_BELLS = 10, COINS = 14;
    static bool devUsed;
    long baseMs;          // 부재 기준 시각(ms epoch): 시작 땐 저장된 savedAt, 이후엔 마지막 계산 시각
    bool ready;           // 시작 계산을 마친 뒤부터 복귀 이벤트를 받는다
    GameObject modal;

    void Awake()
    {
        I = this;
        // GameState.Awake에서 읽은 저장 시각(이후 자동저장·발판 저장이 now로 덮는다)
        long saved = G != null ? G.S.savedAt : 0;
        long now = NowMs();
        baseMs = saved > 0 ? saved : now;
        if (!devUsed && float.TryParse(DevArgs.Get("-offline"), out var secs)) { devUsed = true; baseMs = now - (long)(secs * 1000f); }
        // 검증 실행(-shot)에선 -offline을 줄 때만 계산한다(다른 검증의 스크린샷에 모달이 끼지 않게)
        devOff = DevArgs.Has("-shot") && !DevArgs.Has("-offline");
    }
    bool devOff;

    IEnumerator Start()
    {
        // 월드가 한 번 그려지고(검증 인자 발판 적용 포함) 나서 모달이 뜨도록 잠깐 기다린다
        yield return new WaitForSecondsRealtime(0.6f);
        Evaluate(baseMs);
        ready = true;
    }

    void OnApplicationPause(bool pause) { if (!pause && ready) Resume(); }
    void OnApplicationFocus(bool focus) { if (focus && ready) Resume(); }
    // 복귀: 백그라운드 전환 때 GameState가 저장한 savedAt(= 떠난 시각)과 마지막 계산 시각 중 늦은 쪽부터 센다
    void Resume() { Evaluate(Math.Max(baseMs, G.S.savedAt)); }

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// 기준 시각 이후 부재 수익(벨). 조건 미달이면 0
    public int Compute(long fromMs, out float secs)
    {
        secs = 0f;
        if (G == null || fromMs <= 0) return 0;
        float el = (NowMs() - fromMs) / 1000f;
        if (el < MIN_AWAY || !G.HasCashier) return 0;
        secs = Mathf.Min(el, MAX_AWAY);
        float rate = Mathf.Min(1f / G.BottleTime, 1.55f / G.BaseGap) * G.BottlePrice * (G.PadLv("runner") >= 1 ? 0.5f : 0.25f) * (G.PadLv("drill") >= 1 ? 1f : 0.7f);
        int n = Mathf.FloorToInt(secs * rate + 0.5f);
        return n < MIN_BELLS ? 0 : n;
    }

    int modalN, modalMins;   // 모달에 보이는 총액·분(이미 지급된 금액)
    void Evaluate(long fromMs)
    {
        if (G == null || devOff) return;
        int n = Compute(fromMs, out float secs);
        baseMs = NowMs();   // 여기까지는 셈이 끝났다(같은 복귀에 Pause·Focus가 둘 다 와도 한 번만)
        if (n <= 0) return;
        Debug.Log("offline earn: away=" + secs.ToString("0") + "s n=" + n + " price=" + G.BottlePrice + " bottleTime=" + G.BottleTime.ToString("0.00") + " baseGap=" + G.BaseGap.ToString("0.0"));
        // 원본처럼 모달을 띄우는 순간 지급·저장한다(모달이 떠 있는 동안 앱이 꺼져도 잃지 않게). 확인 버튼은 동전 연출만
        G.AddBells(n); G.Save();
        int mins = Mathf.RoundToInt(secs / 60f);
        if (modal != null) { modalN += n; modalMins += mins; return; }   // 모달 중 다시 다녀온 몫은 금액에 더해 다시 차오르게
        modalN = n; modalMins = mins;
        StartCoroutine(CoModal());
    }

    // ---- 모달 ----
    static Sprite rays;
    IEnumerator CoModal()
    {
        while (HUD.I == null) yield return null;
        var root = HUD.I.transform;
        if (UpgradeSheet.I != null) UpgradeSheet.I.Close();
        // 화면 막(터치 차단) + 판
        var scrim = UIKit.RT(root, "offlineModal").gameObject.AddComponent<Image>(); modal = scrim.gameObject;
        scrim.color = new Color(UIKit.SCRIM.r, UIKit.SCRIM.g, UIKit.SCRIM.b, 0f); scrim.raycastTarget = true;
        var srt = scrim.rectTransform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
        // 배치(판 중심 기준, 판 380×400): 상자 +150(판 위로 살짝 걸침) · 금액 +40 · 한 줄 −22 · 확인 −126
        var panel = UIKit.Panel(scrim.transform, "panel", UIKit.Navy);
        var prt = panel.rectTransform; UIKit.Anchor(prt, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(380, 400));
        RectTransform At(Component c, float y, Vector2 size) { var rt = c.GetComponent<RectTransform>(); UIKit.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0, y), size); return rt; }
        // 상자 뒤 금빛 살(천천히 회전) + 상자
        var rayImg = UIKit.RT(panel.transform, "rays").gameObject.AddComponent<Image>(); rayImg.sprite = Rays(); rayImg.raycastTarget = false;
        At(rayImg, 150, new Vector2(260, 260));
        var chest = UIKit.IconImg(panel.transform, "chest", 128); var crt = At(chest, 150, new Vector2(128, 128));
        // 벨 +n (큰 금색 숫자)
        var row = UIKit.Row(panel.transform, "amt", 8); At(row, 40, new Vector2(340, 74));
        UIKit.IconImg(row.transform, "bell", 52);
        var amt = UIKit.Text(row.transform, "+0", 60, UIKit.Hex("ffe58a")); UIKit.Stroke(amt, UIKit.Hex("7a3a00"), 3f); UIKit.DropShadow(amt, 3f, 0.35f);
        // 한 줄: [삐걱] m분 동안 삐걱이 팔았어요
        var row2 = UIKit.Row(panel.transform, "line", 6); At(row2, -22, new Vector2(340, 36));
        UIKit.IconImg(row2.transform, "robot", 28);
        var line = UIKit.Text(row2.transform, modalMins + "분 동안 삐걱이 팔았어요", 23, UIKit.SUB);
        // 확인 버튼
        bool ok = false;
        var btn = UIKit.Button(panel.transform, "ok", UIKit.Gold, new Vector2(220, 70), "확인", 32, () => ok = true, "check", 30);
        At(btn, -126, new Vector2(220, 70));

        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
        AudioManager.I.Play("up", 0.9f); AudioManager.I.Play("ding", 0.8f, 0.5f); JuiceFX.I.Vibrate(30);
        // 팝 인 + 숫자 차오름(0.7s, 틱 음 상승) + 상자 들썩 + 살 회전
        float el = 0f, tickT = 0f; int shown = 0;
        float autoOk = DevArgs.Has("-autoplay") ? 4f : -1f;
        while (!ok)
        {
            float dt = Time.unscaledDeltaTime; el += dt;
            if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;   // 시트가 닫히며 풀어도 다시 잡는다
            scrim.color = new Color(UIKit.SCRIM.r, UIKit.SCRIM.g, UIKit.SCRIM.b, UIKit.SCRIM.a * Mathf.Clamp01(el / 0.25f));
            prt.localScale = Vector3.one * Mathf.Max(0.001f, JuiceFX.EaseOutBack(Mathf.Clamp01(el / 0.45f)));
            int n = modalN; line.text = modalMins + "분 동안 삐걱이 팔았어요";
            float k = Mathf.Clamp01((el - 0.3f) / 0.7f); int v = Mathf.RoundToInt(n * (1f - (1f - k) * (1f - k)));
            if (v != shown) { shown = v; amt.text = "+" + v.ToString("N0"); tickT -= dt; if (tickT <= 0f) { tickT = 0.06f; AudioManager.I.PlayHz("tick", 600f + 700f * k, 480f, 0.4f); } }
            rayImg.rectTransform.localRotation = Quaternion.Euler(0, 0, -el * 25f);
            rayImg.color = new Color(1, 1, 1, 0.75f + Mathf.Sin(el * 3f) * 0.2f);
            // 상자: 1.6초마다 통통 튀며 좌우로 들썩
            float ph = el % 1.6f; float hop = ph < 0.35f ? Mathf.Sin(ph / 0.35f * Mathf.PI) : 0f;
            crt.anchoredPosition = new Vector2(0, 150 + hop * 14f);
            crt.localRotation = Quaternion.Euler(0, 0, ph < 0.35f ? Mathf.Sin(ph * 40f) * 7f : 0f);
            crt.localScale = new Vector3(1f + hop * 0.08f, 1f - hop * 0.06f, 1f);
            if (autoOk > 0f && el >= autoOk) ok = true;
            yield return null;
        }
        amt.text = "+" + modalN.ToString("N0");
        // 확인: (벨은 이미 지급됨) 상자에서 동전 14개가 HUD로, 상자 팝, 판 축소
        AudioManager.I.Play("coin", 1.2f); JuiceFX.I.Vibrate(20);
        HUD.I.Coins(crt.position, () => HUD.I.BellsScreen(), COINS);
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = false;
        el = 0f;
        while (el < 0.25f)
        {
            el += Time.unscaledDeltaTime; float e = Mathf.Clamp01(el / 0.25f);
            crt.localScale = Vector3.one * (1f + Mathf.Sin(e * Mathf.PI) * 0.3f);
            prt.localScale = Vector3.one * (1f - e * e);
            scrim.color = new Color(UIKit.SCRIM.r, UIKit.SCRIM.g, UIKit.SCRIM.b, UIKit.SCRIM.a * (1f - e));
            yield return null;
        }
        Destroy(modal); modal = null;
    }

    // 금빛 살 12갈래(UIKit.Pix로 한 번 그려 둠)
    static Sprite Rays()
    {
        if (rays != null) return rays;
        const int S = 256; var p = new UIKit.Pix(S, S); float c = S / 2f;
        var gold = new Color(1f, 0.86f, 0.4f, 0.32f);
        for (int i = 0; i < 12; i++)
        {
            float a0 = i * Mathf.PI * 2f / 12f, a1 = a0 + Mathf.PI / 12f * 0.9f;
            p.Poly(new[] { new Vector2(c, c), new Vector2(c + Mathf.Cos(a0) * c, c + Mathf.Sin(a0) * c), new Vector2(c + Mathf.Cos(a1) * c, c + Mathf.Sin(a1) * c) }, gold);
        }
        p.Circle(c, c, S * 0.22f, new Color(1f, 0.9f, 0.55f, 0.28f));
        var tex = p.ToTexture();
        rays = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return rays;
    }
}

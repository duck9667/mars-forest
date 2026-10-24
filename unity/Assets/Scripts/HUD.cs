using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// HUD: 벨 필(아이콘+숫자, 펀치), 설정 버튼(톱니, 소리·진동 토글 패널), 며칠째 필, 업그레이드 버튼(금색·점·바운스), 콤보/러시 배지, 화면 플래시, 동전 비행(coinsToHud)
// 상단 배치: 1행 벨(좌)·지도 알약 "정착 n/5"(톱니 왼쪽)·설정(우) / 2행 며칠째(좌)·러시(중앙)·콤보(우)
public class HUD : MonoBehaviour
{
    public static HUD I;
    GameState G => GameState.I;
    public Canvas canvas; public RectTransform bellsRt; Text bellsText; Image bellIcon; Image flash; Button upBtn; Image upDot; RectTransform upRt;
    float lastBells = -1; Coroutine punch;
    Text comboText, rushText; RectTransform comboRt, rushRt; int lastCombo;
    Text dayText; int lastDay = -1;
    GameObject settings; Button sndBtn, vibBtn;
    Image mapBg; Text mapText; RectTransform mapRt; int lastStage = -1; Coroutine mapPulse;

    // 예전 호출부 호환용 얇은 래퍼(UIKit로 위임)
    public static class UI
    {
        public static Image Panel(Transform parent, string name, Color c)
        {
            var img = UIKit.RT(parent, name).gameObject.AddComponent<Image>(); img.color = c; return img;
        }
        public static Text Label(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool bold = true)
        {
            var t = UIKit.Text(parent, s, size, c, a);
            var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }
        public static Button Btn(Transform parent, string s, Vector2 size, Color bg, Color fg, int fs, System.Action onClick)
            => UIKit.Button(parent, "btn", UIKit.Gold, size, s, fs, onClick);
        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size) => UIKit.Anchor(rt, anchor, pos, size);
    }

    void Awake()
    {
        I = this;
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
        }
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(540, 1170); sc.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // 1행 좌: 벨 필 [벨 아이콘] 숫자(금색, 남색 외곽선). 내용 크기에 맞춰 늘어남
        var bells = UIKit.Chip(transform, "bells", UIKit.Pill, "bell", 34, "0", 34, out bellsText, out bellIcon, 6);
        bellsText.color = UIKit.GOLD;
        bellsRt = bells.rectTransform; bellsRt.anchorMin = bellsRt.anchorMax = new Vector2(0, 1); bellsRt.pivot = new Vector2(0, 1); bellsRt.anchoredPosition = new Vector2(16, -26);
        var ble = bells.gameObject.AddComponent<LayoutElement>(); ble.minHeight = 56; ble.minWidth = 150;
        // 1행 우: 설정 버튼(둥근 남색 버튼 52, 톱니) → 소리·진동 토글 패널
        var gear = UIKit.RoundButton(transform, "settingsBtn", UIKit.Pill, 52, "gear", 30, ToggleSettings, out _);
        var mrt = gear.GetComponent<RectTransform>(); mrt.anchorMin = mrt.anchorMax = new Vector2(1, 1); mrt.pivot = new Vector2(1, 1); mrt.anchoredPosition = new Vector2(-16, -26);
        // 1행 우(톱니 왼쪽): 지도 알약 [지도] 정착 n/5 → 지도 화면(MapScreen). 단계가 바뀌면 잠깐 금색으로 반짝
        mapBg = UIKit.Chip(transform, "mapBtn", UIKit.Pill, null, 0, "정착 0/5", 22, out mapText, out _, 5);
        var mic = UIKit.RT(mapBg.transform, "ic_map").gameObject.AddComponent<Image>(); mic.sprite = MapScreen.MapIcon(); mic.preserveAspect = true; mic.raycastTarget = false;
        var mle = mic.gameObject.AddComponent<LayoutElement>(); mle.preferredWidth = mle.minWidth = mle.preferredHeight = mle.minHeight = 30; mic.transform.SetSiblingIndex(0);
        mapRt = mapBg.rectTransform; mapRt.anchorMin = mapRt.anchorMax = new Vector2(1, 1); mapRt.pivot = new Vector2(1, 1); mapRt.anchoredPosition = new Vector2(-78, -26);
        mapBg.gameObject.AddComponent<LayoutElement>().minHeight = 55;
        mapBg.raycastTarget = true; var mb = mapBg.gameObject.AddComponent<Button>(); mb.targetGraphic = mapBg; mb.transition = Selectable.Transition.None;
        mapBg.gameObject.AddComponent<UIPress>(); mb.onClick.AddListener(() => { AudioManager.I?.Play("pop", 1.3f, 0.4f); MapScreen.I?.Toggle(); });
        // 2행 좌: 며칠째 필(S.day, DayNight가 갱신). 2행 가운데는 목표·정착 칩 자리라 비워 둔다
        var day = UIKit.Chip(transform, "day", UIKit.Pill, "sun", 24, "1일째", 22, out dayText, out _, 5);
        var dyrt = day.rectTransform; dyrt.anchorMin = dyrt.anchorMax = new Vector2(0, 1); dyrt.pivot = new Vector2(0, 1); dyrt.anchoredPosition = new Vector2(18, -96);
        // 업그레이드 버튼(하단 중앙, 금색 베벨 + 위 화살표)
        upBtn = UIKit.Button(transform, "upBtn", UIKit.Gold, new Vector2(270, 76), "업그레이드", 32, () => UpgradeSheet.I?.Toggle(), "up", 36);
        upRt = upBtn.GetComponent<RectTransform>(); UIKit.Anchor(upRt, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(270, 76));
        upDot = UIKit.IconImg(upBtn.transform, "dot", 26); var drt = upDot.rectTransform; drt.anchorMin = drt.anchorMax = new Vector2(1, 1); drt.pivot = new Vector2(0.5f, 0.5f); drt.anchoredPosition = new Vector2(-4, -6);
        Destroy(upDot.GetComponent<LayoutElement>());
        // 3행: 콤보 배지(좌, 금색) · 러시 배지(중앙, 빨강)
        var cb = UIKit.Chip(transform, "combo", UIKit.SignOk, "bolt", 26, "", 24, out comboText, out _);
        comboRt = cb.rectTransform; comboRt.anchorMin = comboRt.anchorMax = new Vector2(1, 1); comboRt.pivot = new Vector2(1, 1); comboRt.anchoredPosition = new Vector2(-16, -96);   // 2행 오른쪽(목표 칩이 빠진 자리)
        cb.gameObject.SetActive(false);
        var rb = UIKit.Chip(transform, "rush", UIKit.Red, "bang", 26, "", 24, out rushText, out _);
        rushRt = rb.rectTransform; rushRt.anchorMin = rushRt.anchorMax = new Vector2(0.5f, 1); rushRt.pivot = new Vector2(0.5f, 1); rushRt.anchoredPosition = new Vector2(0, -96);   // 2행 가운데
        rb.gameObject.SetActive(false);
        // 플래시(원본 #flash 색 fff8e0)
        flash = UI.Panel(transform, "flash", new Color(1, 0.97f, 0.88f, 0)); flash.raycastTarget = false;
        var frt = flash.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
    }

    // ---- 설정 패널(소리·진동 토글 + 닫기) ----
    public bool SettingsOpen => settings != null;
    public void ToggleSettings() { if (settings != null) CloseSettings(); else OpenSettings(); }
    public void OpenSettings()
    {
        if (settings != null) return;
        if (UpgradeSheet.I != null) UpgradeSheet.I.Close();
        AudioManager.I?.Play("pop", 1.3f, 0.4f);
        // 화면 막(바깥 누르면 닫힘) + 판 340×300
        var scrim = UIKit.RT(transform, "settings").gameObject.AddComponent<Image>(); settings = scrim.gameObject;
        scrim.color = UIKit.SCRIM; scrim.raycastTarget = true;
        var srt = scrim.rectTransform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
        var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(CloseSettings);
        var panel = UIKit.Panel(scrim.transform, "panel", UIKit.Navy); panel.raycastTarget = true;
        var pb = panel.gameObject.AddComponent<Button>(); pb.transition = Selectable.Transition.None;   // 판 안쪽 빈 곳 누름이 막(닫기)으로 번지지 않게
        UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(340, 300));
        RectTransform At(Component c, float y, Vector2 size) { var rt = c.GetComponent<RectTransform>(); UIKit.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0, y), size); return rt; }
        var title = UIKit.Text(panel.transform, "설정", 30, Color.white); UIKit.Stroke(title, UIKit.EDGE, 2f); At(title, 106, new Vector2(300, 44));
        sndBtn = UIKit.Button(panel.transform, "sound", UIKit.Green, new Vector2(280, 64), "", 26, () => { G.S.muted = !G.S.muted; RefreshSettings(); G.Save(); AudioManager.I?.Play("pop", 1.3f, 0.4f); }, "sound", 32);
        At(sndBtn, 40, new Vector2(280, 64));
        vibBtn = UIKit.Button(panel.transform, "vibrate", UIKit.Green, new Vector2(280, 64), "", 26, () => { G.S.vibrate = !G.S.vibrate; RefreshSettings(); G.Save(); AudioManager.I?.Play("pop", 1.3f, 0.4f); JuiceFX.I?.Vibrate(20); }, "bolt", 32);
        At(vibBtn, -34, new Vector2(280, 64));
        var close = UIKit.Button(panel.transform, "close", UIKit.Gray, new Vector2(160, 54), "닫기", 24, CloseSettings);
        At(close, -106, new Vector2(160, 54));
        RefreshSettings();
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
    }
    public void CloseSettings()
    {
        if (settings == null) return;
        Destroy(settings); settings = null; sndBtn = vibBtn = null;
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = false;
    }
    void RefreshSettings()
    {
        if (sndBtn != null) UIKit.Restyle(sndBtn, G.S.muted ? UIKit.Gray : UIKit.Green, G.S.muted ? "소리 꺼짐" : "소리 켜짐", G.S.muted ? "mute" : "sound");
        if (vibBtn != null) UIKit.Restyle(vibBtn, G.S.vibrate ? UIKit.Green : UIKit.Gray, G.S.vibrate ? "진동 켜짐" : "진동 꺼짐", "bolt");
    }

    void Start()
    {
        G.OnBellsChanged += Refresh; G.OnPadLevel += (a, b) => Refresh(); Refresh();
        if (SettlementManager.I != null) SettlementManager.I.OnStage += _ => PulseMap();
        // -sheet: QA 스크린샷용 업그레이드 시트 자동 열기(20s)·닫기(30s)
        if (DevArgs.Has("-sheet")) StartCoroutine(CoAutoSheet());
        // -settings 초: QA 스크린샷용 설정 패널 자동 열기(그 초)·닫기(+4초)
        var st = DevArgs.Get("-settings");
        if (st != null && float.TryParse(st, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sec)) StartCoroutine(CoAutoSettings(sec));
    }
    IEnumerator CoAutoSettings(float sec) { yield return new WaitForSeconds(sec); OpenSettings(); yield return new WaitForSeconds(4f); CloseSettings(); }
    IEnumerator CoAutoSheet() { yield return new WaitForSeconds(20f); UpgradeSheet.I?.Open(); yield return new WaitForSeconds(10f); UpgradeSheet.I?.Close(); }

    public void Refresh()
    {
        if (G == null) return;
        int b = Mathf.FloorToInt(G.S.bells);
        bellsText.text = b.ToString("N0");
        if (lastBells >= 0 && b != (int)lastBells) { if (punch != null) StopCoroutine(punch); punch = StartCoroutine(Punch(bellsText.rectTransform)); }
        lastBells = b;
        // 메뉴 스탯 살 수 있으면 점
        bool dot = false;
        foreach (var d in Pads.ALL) if (d.active && d.menu && !G.PadMaxed(d.id) && G.PadReq(d.id) && G.S.bells >= G.PadRemain(d.id)) dot = true;
        upDot.gameObject.SetActive(dot); menuBuyable = dot;
    }

    bool menuBuyable;
    void Update()
    {
        // 튜토리얼 후 첫 시트 구매 전까지, 메뉴 스탯을 살 수 있을 때만 바운스(원본 970행)
        bool bounce = menuBuyable && G.S.tut >= 3 && G.PadLv("hand") == 0 && G.PadLv("spd") == 0 && G.PadLv("price") == 0;
        upRt.anchoredPosition = new Vector2(0, 36 + (bounce ? Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 10f : 0));
        if (G.S.day != lastDay) { lastDay = G.S.day; dayText.text = Mathf.Max(1, lastDay) + "일째"; }
        int stg = SettlementManager.I != null ? SettlementManager.I.Stage : 0;
        if (stg != lastStage)
        {
            lastStage = stg; bool done = stg >= SettlementManager.COUNT;
            mapText.text = done ? "정착 완료" : "정착 " + stg + "/" + SettlementManager.COUNT;
            if (mapPulse == null) { mapBg.sprite = UIKit.Panel9(done ? UIKit.SignOk : UIKit.Pill); mapText.color = done ? UIKit.SignOk.text : Color.white; MapStroke(!done); }
        }
    }

    // 정착 단계 달성: 지도 알약이 1.6초 금색으로 바뀌어 두근거린다
    public void PulseMap() { if (mapPulse != null) StopCoroutine(mapPulse); mapPulse = StartCoroutine(CoPulseMap()); }
    IEnumerator CoPulseMap()
    {
        mapBg.sprite = UIKit.Panel9(UIKit.SignOk); mapText.color = UIKit.SignOk.text; MapStroke(false);
        float el = 0;
        while (el < 1.6f) { el += Time.unscaledDeltaTime; mapRt.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(el * 7f)) * 0.14f * (1f - el / 1.6f)); yield return null; }
        mapRt.localScale = Vector3.one;
        bool done = SettlementManager.I != null && SettlementManager.I.Settled;
        if (!done) { mapBg.sprite = UIKit.Panel9(UIKit.Pill); mapText.color = Color.white; MapStroke(true); }
        mapPulse = null;
    }
    // 남색 알약일 땐 남색 외곽선, 금색일 땐 외곽선 없음(UIKit.Stroke는 두께 0이면 기존 외곽선을 안 지우므로 직접 지운다)
    void MapStroke(bool on)
    {
        if (on) UIKit.Stroke(mapText, UIKit.Pill.stroke, UIKit.Pill.strokeW);
        else foreach (var o in mapText.GetComponents<Outline>()) Destroy(o);
    }

    IEnumerator Punch(RectTransform rt)
    {
        float el = 0;
        while (el < 0.3f) { el += Time.deltaTime; float k = el / 0.3f; rt.localScale = Vector3.one * (1 + Mathf.Sin(Mathf.PI * k) * 0.35f); yield return null; }
        rt.localScale = Vector3.one;
    }

    // §1.3 콤보 배지 ×n (3~5) + 상승음
    public void SetCombo(int n)
    {
        bool show = n >= 3;
        comboRt.gameObject.SetActive(show);
        if (show) { comboText.text = "콤보 ×" + Mathf.Min(n, 5); if (n != lastCombo) { StartCoroutine(Punch(comboRt)); AudioManager.I.PlayHz("coin", 900 * Mathf.Pow(1.12f, Mathf.Min(n, 5)), 1100, 0.6f); } }
        lastCombo = n;
    }
    // §5.3 러시 카운트다운
    public void SetRush(float sec)
    {
        rushRt.gameObject.SetActive(sec > 0);
        if (sec > 0) { rushText.text = "러시! " + Mathf.CeilToInt(sec); rushRt.localScale = Vector3.one * (1 + Mathf.Sin(Time.time * 12f) * 0.04f); }
    }

    public void Flash(float a) { StartCoroutine(CoFlash(a)); }
    IEnumerator CoFlash(float a)
    {
        float el = 0;
        while (el < 0.45f) { el += Time.deltaTime; flash.color = new Color(1, 0.97f, 0.88f, a * (1 - el / 0.45f)); yield return null; }
        flash.color = new Color(1, 0.97f, 0.88f, 0);
    }

    // §7.4 coinsToHud: 0.3s 튀어오름(중력 1200) → 0.36s 아크로 목표 이동, 35ms 시차, 도착마다 음 높이 상승
    public void Coins(Vector2 fromScreen, System.Func<Vector2> toScreen, int n, System.Action onEach = null) { StartCoroutine(CoCoins(fromScreen, toScreen, n, onEach)); }
    public Vector2 BellsScreen() => bellIcon != null ? (Vector2)bellIcon.rectTransform.position : (Vector2)bellsRt.position;
    public Vector2 UpBtnScreen() => upRt.position;
    IEnumerator CoCoins(Vector2 from, System.Func<Vector2> to, int n, System.Action onEach)
    {
        for (int i = 0; i < n; i++) { StartCoroutine(CoCoin(from, to, i, onEach)); yield return new WaitForSeconds(0.035f); }
    }
    IEnumerator CoCoin(Vector2 from, System.Func<Vector2> to, int i, System.Action onEach)
    {
        var img = UIKit.IconImg(transform, "coin", 26); Destroy(img.GetComponent<LayoutElement>());
        var rt = img.rectTransform;
        float s = WorldLabels.Scale > 0 ? canvas.scaleFactor : 1f;
        Vector2 v = new Vector2(Random.Range(-220f, 220f), Random.Range(380f, 560f)) * s; Vector2 p = from; float el = 0;
        while (el < 0.3f) { float dt = Time.deltaTime; el += dt; v.y -= 1200f * s * dt; p += v * dt; rt.position = p; yield return null; }
        Vector2 st = p; el = 0;
        while (el < 0.36f) { el += Time.deltaTime; float k = el / 0.36f; float e = 1 - (1 - k) * (1 - k); rt.position = Vector2.Lerp(st, to(), e); yield return null; }
        Destroy(img.gameObject);
        AudioManager.I.PlayHz("coin", 1100 * Mathf.Pow(1.06f, Mathf.Min(14, i)), 1100, 0.5f);
        onEach?.Invoke();
    }

    static readonly List<RaycastResult> hits = new List<RaycastResult>();
    public static bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var pd = new PointerEventData(EventSystem.current) { position = screenPos };
        hits.Clear(); EventSystem.current.RaycastAll(pd, hits);
        return hits.Count > 0;
    }
}

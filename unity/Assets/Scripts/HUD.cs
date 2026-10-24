using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// HUD: 벨 필(아이콘+숫자, 펀치), 소리 토글(둥근 버튼), 업그레이드 버튼(금색·점·바운스), 콤보/러시 배지, 화면 플래시, 동전 비행(coinsToHud)
// 상단 배치: 1행 벨(좌)·소리(우) / 2행 목표 칩(중앙, QuestManager) / 3행 콤보(좌)·러시(중앙)
public class HUD : MonoBehaviour
{
    public static HUD I;
    GameState G => GameState.I;
    public Canvas canvas; public RectTransform bellsRt; Text bellsText; Image bellIcon; Image flash; Button upBtn; Image upDot; RectTransform upRt;
    float lastBells = -1; Coroutine punch;
    Text comboText, rushText; RectTransform comboRt, rushRt; int lastCombo;
    Image muteIcon;

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
        // 1행 우: 소리 토글(둥근 남색 버튼 52)
        var mute = UIKit.RoundButton(transform, "mute", UIKit.Pill, 52, "sound", 30, () => { G.S.muted = !G.S.muted; RefreshMute(); AudioManager.I?.Play("pop", 1.3f, 0.4f); }, out muteIcon);
        var mrt = mute.GetComponent<RectTransform>(); mrt.anchorMin = mrt.anchorMax = new Vector2(1, 1); mrt.pivot = new Vector2(1, 1); mrt.anchoredPosition = new Vector2(-16, -26);
        // 업그레이드 버튼(하단 중앙, 금색 베벨 + 위 화살표)
        upBtn = UIKit.Button(transform, "upBtn", UIKit.Gold, new Vector2(270, 76), "업그레이드", 32, () => UpgradeSheet.I?.Toggle(), "up", 36);
        upRt = upBtn.GetComponent<RectTransform>(); UIKit.Anchor(upRt, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(270, 76));
        upDot = UIKit.IconImg(upBtn.transform, "dot", 26); var drt = upDot.rectTransform; drt.anchorMin = drt.anchorMax = new Vector2(1, 1); drt.pivot = new Vector2(0.5f, 0.5f); drt.anchoredPosition = new Vector2(-4, -6);
        Destroy(upDot.GetComponent<LayoutElement>());
        // 3행: 콤보 배지(좌, 금색) · 러시 배지(중앙, 빨강)
        var cb = UIKit.Chip(transform, "combo", UIKit.SignOk, "bolt", 26, "", 24, out comboText, out _);
        comboRt = cb.rectTransform; comboRt.anchorMin = comboRt.anchorMax = new Vector2(0, 1); comboRt.pivot = new Vector2(0, 1); comboRt.anchoredPosition = new Vector2(16, -178);
        cb.gameObject.SetActive(false);
        var rb = UIKit.Chip(transform, "rush", UIKit.Red, "bang", 26, "", 24, out rushText, out _);
        rushRt = rb.rectTransform; rushRt.anchorMin = rushRt.anchorMax = new Vector2(0.5f, 1); rushRt.pivot = new Vector2(0.5f, 1); rushRt.anchoredPosition = new Vector2(40, -178);
        rb.gameObject.SetActive(false);
        // 플래시(원본 #flash 색 fff8e0)
        flash = UI.Panel(transform, "flash", new Color(1, 0.97f, 0.88f, 0)); flash.raycastTarget = false;
        var frt = flash.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        RefreshMute();
    }
    void RefreshMute() { if (muteIcon) UIKit.SetIcon(muteIcon, G.S.muted ? "mute" : "sound"); }

    void Start()
    {
        G.OnBellsChanged += Refresh; G.OnPadLevel += (a, b) => Refresh(); Refresh();
        // -sheet: QA 스크린샷용 업그레이드 시트 자동 열기(20s)·닫기(30s)
        if (DevArgs.Has("-sheet")) StartCoroutine(CoAutoSheet());
    }
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

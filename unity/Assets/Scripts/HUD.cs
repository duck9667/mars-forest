using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// HUD: 벨 카운터(펀치), 업그레이드 버튼(점·바운스), 소리 토글, 화면 플래시, 동전 비행(coinsToHud)
public class HUD : MonoBehaviour
{
    public static HUD I;
    GameState G => GameState.I;
    public Canvas canvas; public RectTransform bellsRt; Text bellsText; Image flash; Button upBtn; Text upText; Image upDot; RectTransform upRt;
    float lastBells = -1; Coroutine punch;

    public static class UI
    {
        public static Image Panel(Transform parent, string name, Color c)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = c; return img;
        }
        public static Text Label(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool bold = true)
        {
            var go = new GameObject("text"); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = UIFont.Get(); t.fontSize = size; t.color = c; t.alignment = a; t.text = s;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }
        public static Button Btn(Transform parent, string s, Vector2 size, Color bg, Color fg, int fs, System.Action onClick)
        {
            var img = Panel(parent, "btn", bg); var rt = img.rectTransform; rt.sizeDelta = size;
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img;
            var cb = b.colors; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); b.colors = cb;
            Label(img.transform, s, fs, fg);
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }
        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }
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

        // 벨
        var bells = UI.Panel(transform, "bells", new Color(0.13f, 0.12f, 0.22f, 0.9f));
        bellsRt = bells.rectTransform; UI.Anchor(bellsRt, new Vector2(0, 1), new Vector2(18, -54), new Vector2(190, 56));
        bellsText = UI.Label(bells.transform, "벨 0", 30, GameConfig.GOLD);
        // 소리
        var mute = UI.Btn(transform, "소리", new Vector2(92, 48), new Color(0.13f, 0.12f, 0.22f, 0.9f), Color.white, 22, () => { G.S.muted = !G.S.muted; RefreshMute(); });
        UI.Anchor(mute.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-18, -54), new Vector2(92, 48));
        muteText = mute.GetComponentInChildren<Text>();
        // 업그레이드 버튼
        upBtn = UI.Btn(transform, "업그레이드", new Vector2(260, 72), GameConfig.GOLD, GameConfig.NAVY, 30, () => UpgradeSheet.I?.Toggle());
        upRt = upBtn.GetComponent<RectTransform>(); UI.Anchor(upRt, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(260, 72));
        upText = upBtn.GetComponentInChildren<Text>();
        upDot = UI.Panel(upBtn.transform, "dot", GameConfig.Hex("ff5a5a")); UI.Anchor(upDot.rectTransform, new Vector2(1, 1), new Vector2(-6, -6), new Vector2(18, 18)); upDot.sprite = FloatingJoystick.CircleSprite(); upDot.raycastTarget = false;
        // 플래시
        flash = UI.Panel(transform, "flash", new Color(1, 1, 1, 0)); flash.raycastTarget = false;
        var frt = flash.rectTransform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        RefreshMute();
    }
    Text muteText;
    void RefreshMute() { if (muteText) muteText.text = G.S.muted ? "소리 끔" : "소리"; }

    void Start() { G.OnBellsChanged += Refresh; G.OnPadLevel += (a, b) => Refresh(); Refresh(); }

    public void Refresh()
    {
        if (G == null) return;
        int b = Mathf.FloorToInt(G.S.bells);
        bellsText.text = "벨 " + b.ToString("N0");
        if (lastBells >= 0 && b != (int)lastBells) { if (punch != null) StopCoroutine(punch); punch = StartCoroutine(Punch(bellsRt)); }
        lastBells = b;
        // 메뉴 스탯 살 수 있으면 점
        bool dot = false;
        foreach (var d in Pads.ALL) if (d.active && d.menu && !G.PadMaxed(d.id) && G.PadReq(d.id) && G.S.bells >= G.PadRemain(d.id)) dot = true;
        upDot.gameObject.SetActive(dot);
    }

    void Update()
    {
        // 튜토리얼 후 첫 시트 구매 전까지 바운스
        bool bounce = G.S.tut >= 3 && G.PadLv("hand") == 0 && G.PadLv("spd") == 0;
        upRt.anchoredPosition = new Vector2(0, 36 + (bounce ? Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 10f : 0));
    }

    IEnumerator Punch(RectTransform rt)
    {
        float el = 0;
        while (el < 0.25f) { el += Time.deltaTime; float k = el / 0.25f; rt.localScale = Vector3.one * (1 + Mathf.Sin(Mathf.PI * k) * 0.22f); yield return null; }
        rt.localScale = Vector3.one;
    }

    public void Flash(float a) { StartCoroutine(CoFlash(a)); }
    IEnumerator CoFlash(float a)
    {
        float el = 0;
        while (el < 0.45f) { el += Time.deltaTime; flash.color = new Color(1, 1, 1, a * (1 - el / 0.45f)); yield return null; }
        flash.color = new Color(1, 1, 1, 0);
    }

    // §7.4 coinsToHud: 0.3s 튀어오름(중력 1200) → 0.36s 아크로 목표 이동, 35ms 시차, 도착마다 음 높이 상승
    public void Coins(Vector2 fromScreen, System.Func<Vector2> toScreen, int n, System.Action onEach = null) { StartCoroutine(CoCoins(fromScreen, toScreen, n, onEach)); }
    public Vector2 BellsScreen() => bellsRt.position;
    public Vector2 UpBtnScreen() => upRt.position;
    IEnumerator CoCoins(Vector2 from, System.Func<Vector2> to, int n, System.Action onEach)
    {
        for (int i = 0; i < n; i++) { StartCoroutine(CoCoin(from, to, i, onEach)); yield return new WaitForSeconds(0.035f); }
    }
    IEnumerator CoCoin(Vector2 from, System.Func<Vector2> to, int i, System.Action onEach)
    {
        var img = UI.Panel(transform, "coin", GameConfig.GOLD); img.sprite = FloatingJoystick.CircleSprite(); img.raycastTarget = false;
        var rt = img.rectTransform; rt.sizeDelta = new Vector2(22, 22);
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

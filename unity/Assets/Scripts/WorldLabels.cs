using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UIFont
{
    static Font _f;
    // Resources/Fonts/Jua-Regular.ttf(Google Fonts, OFL) 우선. 없으면 OS 둥근 고딕 → 내장 폰트
    public static Font Get()
    {
        if (_f != null) return _f;
        _f = Resources.Load<Font>("Fonts/Jua-Regular");
        if (_f == null)
        {
            Debug.LogWarning("UIFont: Resources/Fonts/Jua-Regular 를 못 찾아 OS 폰트로 대체");
            try { _f = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Malgun Gothic", "Arial" }, 40); } catch { }
        }
        if (_f == null) _f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _f;
    }
}

// 원본의 HTML 오버레이 라벨을 그대로: 스크린 스페이스 캔버스에 라벨을 두고 매 프레임 월드→스크린 투영으로 위치를 맞춘다.
// 라벨 = 세로 레이아웃 루트(배경 9-slice) 안에 가로 행(아이콘·글자·뒤 아이콘) 1~2줄. 원본 .rc(칩)·.bub(말풍선)·.sg(가격표)·.fl(플로팅)에 대응.
public class WorldLabels : MonoBehaviour
{
    public static WorldLabels I;

    public class Label
    {
        public RectTransform rt; public Text text; public Image bg; public Image icon, icon2, tail; public Func<Vector3> world; public Vector2 offset; public bool hidden;
        public UIKit.Style style; public bool autoIcon = true; public bool plain;   // plain: 배경 없는 플로팅 글자
        internal Transform row0, row1; internal Text text2; internal Image iconB; internal bool empty; internal int size; internal string lastSet;
        internal VerticalLayoutGroup vl;

        // 글자 바꾸기. autoIcon이면 원본 라벨 관례의 접두어를 아이콘으로 바꾼다:
        // "얼음 3"→ice, "물 5"/"물 ×2"→drop, "산소 ×1"→o2, "식량 ×1"→food, "병 ×2"/"병 3/24"→bottle, "선반 2/8"→shelf, "+12"→cash 칩(스테이션)·bell(플로팅), "VIP …"→VIP 말풍선, 끝의 " !"→느낌표+화난 말풍선, "☀ "→sun
        public void Set(string s)
        {
            if (s == lastSet) return; lastSet = s;
            string body = s ?? ""; string ic = null, icB = null; UIKit.Style st = null;
            if (autoIcon)
            {
                bool vip = false, hot = false;
                if (body.StartsWith("VIP ")) { vip = true; body = body.Substring(4); }
                if (body.EndsWith(" !")) { hot = true; body = body.Substring(0, body.Length - 2); icB = "bang"; }
                foreach (var kv in PREFIX) if (body.StartsWith(kv.Key)) { ic = kv.Value; body = body.Substring(kv.Key.Length); break; }
                if (ic == null && body.StartsWith("☀")) { ic = "sun"; body = body.Substring(1).TrimStart(); }
                if (ic == null && body.StartsWith("+")) { ic = plain ? "bell" : "cash"; if (!plain) st = UIKit.Cash; }
                if (vip) st = UIKit.Vip;
                if (hot) st = UIKit.Hot;
                ApplyIcon(ref icon, row0, ic, true);
                ApplyIcon(ref iconB, row0, icB, false);
            }
            if (st != null && !plain) SetStyle(st);
            text.text = body;
            empty = string.IsNullOrEmpty(body) && icon == null;
            Apply();
        }
        // 앞 아이콘 직접 지정(자동 인식 끔). null이면 제거
        public void SetIcon(string name) { autoIcon = false; ApplyIcon(ref icon, row0, name, true); empty = string.IsNullOrEmpty(text.text) && icon == null; Apply(); }
        // 뒤 아이콘(글자 뒤)
        public void SetIcon2(string name) { ApplyIcon(ref iconB, row0, name, false); }
        // 둘째 줄(아이콘+글자). null이면 숨김
        public void SetLine2(string s, string iconName = null)
        {
            if (string.IsNullOrEmpty(s)) { if (row1 != null) row1.gameObject.SetActive(false); return; }
            if (row1 == null)
            {
                row1 = UIKit.Row(rt, "row1", 4).transform;
                text2 = UIKit.Text(row1, "", size, style != null ? style.text : text.color);
                if (style != null && style.name == "sign") text2.color = UIKit.GOLD;   // 가격표 둘째 줄(가격)은 금색
            }
            row1.gameObject.SetActive(true);
            if (text2.text != s) text2.text = s;
            ApplyIcon(ref icon2, row1, iconName, true);
        }
        public void SetStyle(UIKit.Style st)
        {
            if (st == style) return; style = st; plain = st == null;
            if (st == null) { if (bg != null) bg.enabled = false; if (tail != null) tail.gameObject.SetActive(false); vl.padding = new RectOffset(0, 0, 0, 0); return; }
            if (bg == null) { bg = rt.gameObject.AddComponent<Image>(); bg.raycastTarget = false; bg.type = Image.Type.Sliced; }
            bg.enabled = true; bg.sprite = UIKit.Panel9(st); bg.color = Color.white;
            vl.padding = new RectOffset(st.pad.left, st.pad.right, st.pad.top, st.pad.bottom + Mathf.RoundToInt(st.shadowH));
            text.color = st.text; if (text2 != null) text2.color = st.name == "sign" ? UIKit.GOLD : st.text;
            if (st.tail)
            {
                if (tail == null)
                {
                    tail = UIKit.RT(rt, "tail").gameObject.AddComponent<Image>(); tail.raycastTarget = false;
                    tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    var trt = tail.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0); trt.pivot = new Vector2(0.5f, 1);
                    trt.sizeDelta = new Vector2(UIKit.TAIL_W, UIKit.TAIL_H + st.shadowH);
                }
                tail.sprite = UIKit.TailSprite(st); tail.gameObject.SetActive(true);
                tail.rectTransform.anchoredPosition = new Vector2(0, st.shadowH + st.border + 1);
                tail.transform.SetAsLastSibling();
            }
            else if (tail != null) tail.gameObject.SetActive(false);
        }
        public void Show(bool v) { hidden = !v; Apply(); }
        internal void Apply() { bool on = !hidden && !empty; if (rt.gameObject.activeSelf != on) rt.gameObject.SetActive(on); }

        void ApplyIcon(ref Image img, Transform row, string name, bool front)
        {
            if (string.IsNullOrEmpty(name)) { if (img != null) { UnityEngine.Object.Destroy(img.gameObject); img = null; } return; }
            if (img == null) { img = UIKit.IconImg(row, name, Mathf.Round(size * 1.15f)); if (front) img.transform.SetAsFirstSibling(); else img.transform.SetAsLastSibling(); }
            else if (img.sprite != UIKit.Icon(name)) img.sprite = UIKit.Icon(name);
        }
        // 꼬리 있는 말풍선은 꼬리 끝이 기준점을 가리키도록 위로 띄운다(단위)
        internal float Lift => style != null && style.tail ? rt.rect.height * 0.5f + UIKit.TAIL_H - style.border - style.shadowH + 1 : 0;
    }
    static readonly KeyValuePair<string, string>[] PREFIX =
    {
        new KeyValuePair<string, string>("얼음 ", "ice"), new KeyValuePair<string, string>("물 ", "drop"), new KeyValuePair<string, string>("병 ×", "bottle"),
        new KeyValuePair<string, string>("병 ", "bottle"), new KeyValuePair<string, string>("선반 ", "shelf"), new KeyValuePair<string, string>("벨 ", "bell"),
        new KeyValuePair<string, string>("산소 ", "o2"), new KeyValuePair<string, string>("식량 ", "food"),   // 정착민 말풍선 "산소 ×1"·"식량 ×1"
    };

    readonly List<Label> labels = new List<Label>();
    Canvas canvas; Camera cam;

    void Awake()
    {
        I = this;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var sc = gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(540, 1170); sc.matchWidthOrHeight = 0.5f;
    }

    public static float Scale => I != null && I.canvas != null ? I.canvas.scaleFactor : 1f;

    // withBg=false → 크림 자원 칩(.rc), withBg=true → bgCol 없음: 흰 말풍선(.bub) / 어두운 색: 남색 가격표(.sg) / 금색: VIP 말풍선. 글자색은 스타일이 정한다.
    public Label Add(Func<Vector3> world, string text, int size, Color col, Vector2 offsetPx, bool withBg = false, Color? bgCol = null)
    {
        UIKit.Style st = UIKit.Cream;
        if (withBg)
        {
            if (!bgCol.HasValue) st = UIKit.Bubble;
            else { var c = bgCol.Value; float lum = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b; st = lum < 0.4f ? UIKit.Sign : (c.r > 0.8f && c.b < 0.5f ? UIKit.Vip : UIKit.Bubble); }
        }
        return Make(world, text, size, col, offsetPx, st);
    }

    Label Make(Func<Vector3> world, string text, int size, Color col, Vector2 offsetPx, UIKit.Style st)
    {
        var rt = UIKit.RT(transform, "label");
        var vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 0; vl.childAlignment = TextAnchor.MiddleCenter; vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = vl.childForceExpandHeight = false;
        var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var row0 = UIKit.Row(rt, "row0", 4).transform;
        var t = UIKit.Text(row0, "", size, col);
        var l = new Label { rt = rt, text = t, world = world, offset = offsetPx, row0 = row0, size = size, vl = vl, plain = st == null };
        l.SetStyle(st);
        l.Set(text);
        labels.Add(l);
        return l;
    }

    public void Remove(Label l)
    {
        if (l == null) return;
        labels.Remove(l);
        if (l.rt != null) Destroy(l.rt.gameObject);
    }

    // 플로팅 텍스트(.fl): 1.1s, 높이 40+50t, 두꺼운 외곽선. gold: 금색, big: 크고 더 두꺼운 갈색 외곽선. 처음 0.5s 튀어오름(.3→1.4→.95→1)
    public void Float(Vector3 world, string text, bool gold = false, bool big = false)
    {
        var l = Make(() => world, text, big ? 40 : 28, big ? UIKit.Hex("ffe58a") : gold ? UIKit.GOLD : Color.white, Vector2.zero, null);
        UIKit.Stroke(l.text, big ? UIKit.Hex("7a3a00") : UIKit.INK, big ? 3f : 2.4f);
        UIKit.DropShadow(l.text, 3f, 0.4f);
        StartCoroutine(CoFloat(l));
    }
    IEnumerator CoFloat(Label l)
    {
        float el = 0;
        while (el < 1.1f)
        {
            el += Time.deltaTime; float t = el / 1.1f;
            l.offset = new Vector2(0, (40 + 50 * t) * Scale);
            float k = Mathf.Clamp01(el / 0.5f);
            float sc = k < 0.3f ? Mathf.Lerp(0.3f, 1.4f, k / 0.3f) : k < 0.55f ? Mathf.Lerp(1.4f, 0.95f, (k - 0.3f) / 0.25f) : Mathf.Lerp(0.95f, 1f, (k - 0.55f) / 0.45f);
            l.rt.localScale = Vector3.one * sc;
            var c = l.text.color; c.a = 1 - Mathf.Pow(t, 2); l.text.color = c;
            if (l.icon != null) { var ic = l.icon.color; ic.a = c.a; l.icon.color = ic; }
            yield return null;
        }
        Remove(l);
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float s = Scale;
        foreach (var l in labels)
        {
            if (l.hidden || l.empty || l.rt == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(l.world());
            l.rt.position = new Vector3(sp.x + l.offset.x, sp.y + l.offset.y + l.Lift * s, 0);
        }
    }
}

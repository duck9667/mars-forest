using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UIFont
{
    static Font _f;
    public static Font Get()
    {
        if (_f != null) return _f;
        try { _f = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR", "Malgun Gothic", "Arial" }, 40); } catch { }
        if (_f == null) _f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _f;
    }
}

// 원본의 HTML 오버레이 라벨을 그대로: 스크린 스페이스 캔버스에 텍스트를 두고 매 프레임 월드→스크린 투영으로 위치를 맞춘다.
public class WorldLabels : MonoBehaviour
{
    public static WorldLabels I;
    public class Label
    {
        public RectTransform rt; public Text text; public Image bg; public Func<Vector3> world; public Vector2 offset; public bool hidden;
        public void Set(string s) { if (text.text != s) text.text = s; }
        public void Show(bool v) { hidden = !v; rt.gameObject.SetActive(v); }
    }
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

    public Label Add(Func<Vector3> world, string text, int size, Color col, Vector2 offsetPx, bool withBg = false, Color? bgCol = null)
    {
        var go = new GameObject("label");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260, 60);
        Image bg = null;
        if (withBg)
        {
            bg = go.AddComponent<Image>(); bg.color = bgCol ?? new Color(1, 0.98f, 0.92f, 0.95f);
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var lg = go.AddComponent<HorizontalLayoutGroup>(); lg.padding = new RectOffset(14, 14, 8, 8); lg.childForceExpandWidth = false; lg.childForceExpandHeight = false;
        }
        var tgo = new GameObject("t"); tgo.transform.SetParent(go.transform, false);
        var t = tgo.AddComponent<Text>();
        t.font = UIFont.Get(); t.fontSize = size; t.color = col; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.text = text;
        t.fontStyle = FontStyle.Bold;
        t.raycastTarget = false; if (bg != null) bg.raycastTarget = false;
        var trt = t.GetComponent<RectTransform>();
        if (!withBg) { trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero; }
        var sh = tgo.AddComponent<Outline>(); sh.effectColor = new Color(0, 0, 0, 0.5f); sh.effectDistance = new Vector2(1.5f, -1.5f);
        var l = new Label { rt = rt, text = t, bg = bg, world = world, offset = offsetPx };
        labels.Add(l);
        return l;
    }

    public void Remove(Label l)
    {
        if (l == null) return;
        labels.Remove(l);
        if (l.rt != null) Destroy(l.rt.gameObject);
    }

    // 플로팅 텍스트: 1.1s, 높이 40+50t, gold/big 변형
    public void Float(Vector3 world, string text, bool gold = false, bool big = false)
    {
        var l = Add(() => world, text, big ? 40 : 28, gold ? GameConfig.GOLD : Color.white, Vector2.zero);
        StartCoroutine(CoFloat(l));
    }
    IEnumerator CoFloat(Label l)
    {
        float el = 0;
        while (el < 1.1f)
        {
            el += Time.deltaTime; float t = el / 1.1f;
            l.offset = new Vector2(0, (40 + 50 * t) * Scale);
            var c = l.text.color; c.a = 1 - Mathf.Pow(t, 2); l.text.color = c;
            yield return null;
        }
        Remove(l);
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        foreach (var l in labels)
        {
            if (l.hidden || l.rt == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(l.world());
            l.rt.position = new Vector3(sp.x + l.offset.x, sp.y + l.offset.y, 0);
        }
    }
}

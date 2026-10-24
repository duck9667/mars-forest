using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI 킷: 런타임에 Texture2D로 만드는 9-slice 베벨 패널·말풍선 꼬리·벡터 아이콘 스프라이트와 Panel/Icon/Text/Button 헬퍼.
// 원본 app/index.html의 CSS(.pill .pnl .rc .bub .sg .btn .row .buy .ic 의 SVG)를 픽셀로 옮겼다. 에셋 파일 없이 전부 코드로 생성한다.
public static class UIKit
{
    // ── 색 (원본 CSS 변수) ──
    public static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        if (h.Length == 8) { c.a = Convert.ToInt32(h.Substring(6, 2), 16) / 255f; }
        return c;
    }
    public static readonly Color EDGE = Hex("0b1428"), INK = Hex("1b2740"), NAVY = Hex("15213a"), GOLD = Hex("ffd45a"), GOLD_D = Hex("7a4a00"), GOLD_INK = Hex("8a5200");
    public static readonly Color BLUE_D = Hex("0f3f99"), RED_D = Hex("8a1a10"), SUB = Hex("a9b8d6"), CREAM = Hex("fff6e6");

    // ── 패널 스타일 ──
    // top/mid/bot 세로 그라데이션, edge 테두리, shadow 아래쪽 하드 그림자(shadowH), hi/lo 안쪽 하이라이트·그림자 띠, text 기본 글자색, stroke 글자 외곽선
    public class Style
    {
        public string name; public Color top, mid, bot, edge, shadow, text, stroke;
        public float radius = 14, border = 3, shadowH = 3, hi = 0.2f, lo = 0.3f, strokeW = 0;
        public bool topOnly, tail; public RectOffset pad = new RectOffset(12, 12, 5, 5);
        public Style(string name, string top, string mid, string bot, string edge, string shadow, string text)
        { this.name = name; this.top = Hex(top); this.mid = Hex(mid); this.bot = Hex(bot); this.edge = Hex(edge); this.shadow = Hex(shadow); this.text = Hex(text); stroke = Color.clear; }
        public Style R(float r) { radius = r; return this; }
        public Style B(float b) { border = b; return this; }
        public Style Sh(float h) { shadowH = h; return this; }
        public Style HiLo(float h, float l) { hi = h; lo = l; return this; }
        public Style Stroke(string c, float w) { stroke = Hex(c); strokeW = w; return this; }
        public Style Pad(int l, int r, int t, int b) { pad = new RectOffset(l, r, t, b); return this; }
        public Style Top() { topOnly = true; return this; }
        public Style Tail() { tail = true; return this; }
        public Style Clone(string n) { var s = (Style)MemberwiseClone(); s.name = n; s.pad = new RectOffset(pad.left, pad.right, pad.top, pad.bottom); return s; }
    }

    // HUD 필(벨·소리) .pill
    public static readonly Style Pill = new Style("pill", "34508a", "1a2a4c", "15213a", "0b1428", "070d1a", "ffffff").R(22).Sh(3).HiLo(0.22f, 0.3f).Stroke("0b1428", 1.6f).Pad(10, 16, 4, 4);
    // 일반 남색 패널 .pnl / 목표 칩 #qchip
    public static readonly Style Navy = new Style("panel", "2c416b", "1a2844", "16213a", "0b1428", "070d1a", "ffffff").R(16).Sh(4).HiLo(0.18f, 0.35f).Pad(12, 14, 6, 6);
    // 크림색 자원 칩 .rc (스테이션 카운터)
    public static readonly Style Cream = new Style("chip", "ffffff", "f8f2e8", "f1e8dc", "2b3a55", "0000004d", "1b2740").R(14).Sh(3).HiLo(0.6f, 0.08f).Pad(8, 12, 3, 3);
    // 계산대 현금 칩 .rc.cash
    public static readonly Style Cash = new Style("cash", "f0ffe8", "e2fad7", "d4f5c6", "2e7d32", "0000004d", "1b5e20").R(14).Sh(3).HiLo(0.5f, 0.08f).Pad(8, 12, 3, 3);
    // 말풍선 .bub (손님) / 화난 .bub.hot / VIP .bub.vip / 튜토리얼 .bub.tip
    public static readonly Style Bubble = new Style("bub", "ffffff", "ffffff", "fdfbf7", "2b3a55", "0000004d", "1b2740").R(14).Sh(3).HiLo(0f, 0.06f).Pad(10, 14, 4, 4).Tail();
    public static readonly Style Hot = new Style("hot", "ffe3dc", "ffe3dc", "ffd6cc", "c23b2a", "0000004d", "1b2740").R(14).Sh(3).HiLo(0f, 0.06f).Pad(10, 14, 4, 4).Tail();
    public static readonly Style Vip = new Style("vip", "fff3b0", "ffe68a", "ffd45a", "8a5200", "0000004d", "5a3200").R(14).Sh(3).HiLo(0.3f, 0.1f).Pad(10, 14, 4, 4).Tail();
    public static readonly Style Tip = new Style("tip", "ffffff", "fff9e0", "fff3c4", "7a4a00", "0000004d", "3a2200").R(16).Sh(3).HiLo(0f, 0.06f).Pad(14, 18, 6, 6).Tail();
    // 발판 가격표 .sg / 살 수 있으면 .sg.ok
    public static readonly Style Sign = new Style("sign", "2a3d63", "1f2f4e", "15213a", "0f1a30", "0000006a", "ffffff").R(12).Sh(3).HiLo(0.2f, 0.3f).Pad(12, 14, 5, 5);
    public static readonly Style SignOk = new Style("signok", "ffe58a", "f8cc5c", "f2b533", "7a4a00", "8a5200", "3a2200").R(12).Sh(3).HiLo(0.5f, 0.12f).Pad(12, 14, 5, 5);
    // 버튼 .btn(금) .buy(파랑) #rush(빨강) .buy:disabled(회색)
    public static readonly Style Gold = new Style("gold", "ffe58a", "f4b02e", "e08f12", "7a4a00", "7a4a00", "ffffff").R(16).Sh(5).HiLo(0.55f, 0.15f).Stroke("8a5200", 2f);
    public static readonly Style Blue = new Style("blue", "7cc0ff", "2a74e8", "1a5bd0", "0b2f80", "0b2f80", "ffffff").R(14).Sh(4).HiLo(0.45f, 0.15f).Stroke("0f3f99", 1.8f);
    public static readonly Style Red = new Style("red", "ff8a7a", "e04a3a", "c0302a", "6a1410", "6a1410", "ffffff").R(16).Sh(4).HiLo(0.4f, 0.1f).Stroke("8a1a10", 1.8f);
    public static readonly Style Gray = new Style("gray", "3b4a66", "33415a", "2b3850", "0b1428", "0b1428", "c3cfe6").R(14).Sh(4).HiLo(0.12f, 0.2f);
    public static readonly Style Green = new Style("green", "8fe08a", "5cc45a", "3aa83a", "1f5a22", "1f5a22", "ffffff").R(14).Sh(4).HiLo(0.4f, 0.1f).Stroke("1f5a22", 1.8f);
    // 시트 .row / 노출 중 .row.cur / 완료 .row.done / 아이콘 타일 .row .ico
    public static readonly Style Card = new Style("row", "2d4169", "283b5f", "243655", "0b1428", "0b1428", "ffffff").R(16).Sh(3).HiLo(0.14f, 0.2f);
    public static readonly Style CardCur = new Style("rowcur", "2d4169", "283b5f", "243655", "ffd45a", "0b1428", "ffffff").R(16).Sh(3).HiLo(0.14f, 0.2f);
    public static readonly Style CardDone = new Style("rowdone", "22324f", "1f2e4a", "1c2a44", "0b1428", "0b1428", "ffffff").R(16).Sh(3).HiLo(0.1f, 0.2f);
    public static readonly Style Tile = new Style("tile", "ffe6c8", "f9d5ab", "f3c48f", "7a4a00", "7a4a00", "3a2200").R(15).Sh(3).HiLo(0.5f, 0.12f);
    public static readonly Style TileDark = new Style("tiledark", "4a6190", "3a4e78", "2b3c5e", "0b1428", "0b1428", "ffffff").R(15).Sh(3).HiLo(0.15f, 0.2f);
    // 시트 #sheet (위쪽만 둥글게) · 진행 바 트랙/채움 · 레벨 핍
    public static readonly Style Sheet = new Style("sheet", "263858", "1b2740", "1b2740", "0b1428", "000000", "ffffff").R(24).Sh(0).HiLo(0.15f, 0f).Top();
    public static readonly Style Track = new Style("track", "0b1428", "0b1428", "0b1428", "0b1428", "000000", "ffffff").R(4).B(0).Sh(0).HiLo(0f, 0.3f);
    public static readonly Style Bar = new Style("bar", "ffe58a", "f6c74a", "f0a428", "f0a428", "000000", "ffffff").R(4).B(0).Sh(0).HiLo(0.3f, 0.1f);
    public static readonly Style PipOff = new Style("pipoff", "0b1428", "0b1428", "0b1428", "0b1428", "000000", "ffffff").R(3).B(0).Sh(0).HiLo(0f, 0.4f);
    public static readonly Style PipOn = new Style("pipon", "ffe58a", "f6c74a", "f0a428", "f0a428", "7a4a00", "ffffff").R(3).B(0).Sh(1).HiLo(0.3f, 0.1f);
    // 반투명 덮개(시트 뒤) .scrim
    public static readonly Color SCRIM = new Color(10 / 255f, 16 / 255f, 30 / 255f, 0.55f);

    const int S = 2;   // 텍스처 배율(2배로 그려 레티나에서도 또렷하게). pixelsPerUnit=200 → 1 UI 단위 = 2픽셀

    // ── 픽셀 캔버스: 부호 거리(SDF)로 커버리지를 구해 안티앨리어싱한다. 좌표는 y-아래 방향(SVG와 같음) ──
    public class Pix
    {
        public readonly int w, h; readonly Color[] px;
        public Vector2 off;      // 그림자용 오프셋
        public Color? tint;      // 그림자용: 색을 덮어쓰고 알파는 max 합성(실루엣)
        public Pix(int w, int h) { this.w = w; this.h = h; px = new Color[w * h]; }

        void Blend(int x, int y, Color c, float cov)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || cov <= 0) return;
            int i = y * w + x; var d = px[i];
            if (tint.HasValue) { var t = tint.Value; float a = Mathf.Max(d.a, cov * t.a); px[i] = new Color(t.r, t.g, t.b, a); return; }
            float ca = c.a * cov, oa = ca + d.a * (1 - ca);
            if (oa <= 0) return;
            px[i] = new Color((c.r * ca + d.r * d.a * (1 - ca)) / oa, (c.g * ca + d.g * d.a * (1 - ca)) / oa, (c.b * ca + d.b * d.a * (1 - ca)) / oa, oa);
        }

        // sd: 점→부호 거리, bb: 도형 경계, expand>0 바깥으로 두껍게(외곽선) / <0 안으로, grad: y→색
        public void Draw(Func<Vector2, float> sd, Rect bb, Color col, float expand = 0, Func<float, Color> grad = null)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(bb.xMin + off.x - expand - 1)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(bb.xMax + off.x + expand + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(bb.yMin + off.y - expand - 1)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(bb.yMax + off.y + expand + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - off;
                    float d = sd(p) - expand; float cov = Mathf.Clamp01(0.5f - d);
                    if (cov > 0) Blend(x, y, grad != null ? grad(p.y) : col, cov);
                }
        }
        public void Circle(float cx, float cy, float r, Color c, float expand = 0)
        { var ce = new Vector2(cx, cy); Draw(p => (p - ce).magnitude - r, new Rect(cx - r, cy - r, r * 2, r * 2), c, expand); }
        public void Ring(float cx, float cy, float r, float thick, Color c, float expand = 0)
        { var ce = new Vector2(cx, cy); Draw(p => Mathf.Abs((p - ce).magnitude - r) - thick / 2, new Rect(cx - r - thick, cy - r - thick, (r + thick) * 2, (r + thick) * 2), c, expand); }
        public void Seg(Vector2 a, Vector2 b, float width, Color c, float expand = 0)
        {
            Draw(p => { Vector2 pa = p - a, ba = b - a; float hh = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-5f, Vector2.Dot(ba, ba))); return (pa - ba * hh).magnitude - width / 2; },
                Rect.MinMaxRect(Mathf.Min(a.x, b.x) - width, Mathf.Min(a.y, b.y) - width, Mathf.Max(a.x, b.x) + width, Mathf.Max(a.y, b.y) + width), c, expand);
        }
        // 모서리 반지름 r4 = (좌상, 우상, 우하, 좌하)
        public void RRect(float x, float y, float rw, float rh, Vector4 r4, Color c, float expand = 0, Func<float, Color> grad = null)
        {
            var ce = new Vector2(x + rw / 2, y + rh / 2); var half = new Vector2(rw / 2, rh / 2);
            Draw(p =>
            {
                Vector2 q = p - ce;
                float rad = q.x > 0 ? (q.y > 0 ? r4.z : r4.y) : (q.y > 0 ? r4.w : r4.x);
                rad = Mathf.Min(rad, Mathf.Min(half.x, half.y));
                Vector2 d = new Vector2(Mathf.Abs(q.x), Mathf.Abs(q.y)) - half + Vector2.one * rad;
                return Mathf.Min(Mathf.Max(d.x, d.y), 0) + new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude - rad;
            }, new Rect(x, y, rw, rh), c, expand, grad);
        }
        public void RRect(float x, float y, float rw, float rh, float r, Color c, float expand = 0) => RRect(x, y, rw, rh, new Vector4(r, r, r, r), c, expand);
        public void Poly(Vector2[] v, Color c, float expand = 0)
        {
            float mnx = float.MaxValue, mny = float.MaxValue, mxx = float.MinValue, mxy = float.MinValue;
            foreach (var p in v) { mnx = Mathf.Min(mnx, p.x); mny = Mathf.Min(mny, p.y); mxx = Mathf.Max(mxx, p.x); mxy = Mathf.Max(mxy, p.y); }
            Draw(p =>
            {
                float d = Vector2.Dot(p - v[0], p - v[0]); float s = 1;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    Vector2 e = v[j] - v[i], wv = p - v[i];
                    Vector2 b = wv - e * Mathf.Clamp01(Vector2.Dot(wv, e) / Mathf.Max(1e-6f, Vector2.Dot(e, e)));
                    d = Mathf.Min(d, Vector2.Dot(b, b));
                    bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * wv.y > e.y * wv.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * Mathf.Sqrt(d);
            }, Rect.MinMaxRect(mnx, mny, mxx, mxy), c, expand);
        }
        public Texture2D ToTexture()
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var flip = new Color[w * h];
            for (int y = 0; y < h; y++) Array.Copy(px, y * w, flip, (h - 1 - y) * w, w);   // y-아래 → Unity y-위
            tex.SetPixels(flip); tex.Apply(false, true);
            return tex;
        }
    }

    // ── 9-slice 패널 스프라이트 ──
    static readonly Dictionary<string, Sprite> panels = new Dictionary<string, Sprite>();
    public static Sprite Panel9(Style s)
    {
        if (panels.TryGetValue(s.name, out var sp)) return sp;
        float c = s.radius + s.border + 2;                       // 코너 영역(단위)
        int wu = Mathf.CeilToInt(c * 2 + 4), bodyH = wu;
        int texH = bodyH + Mathf.CeilToInt(s.shadowH);
        var p = new Pix(wu * S, texH * S);
        float W = wu * S, H = bodyH * S, r = s.radius * S, b = s.border * S;
        var r4 = s.topOnly ? new Vector4(r, r, 0, 0) : new Vector4(r, r, r, r);
        if (s.shadowH > 0) p.RRect(0, s.shadowH * S, W, H, r4, s.shadow);
        if (b > 0) p.RRect(0, 0, W, H, r4, s.edge);
        float ir = Mathf.Max(0, r - b); var ir4 = s.topOnly ? new Vector4(ir, ir, 0, 0) : new Vector4(ir, ir, ir, ir);
        float hiH = 2 * S, loH = 3 * S;
        p.RRect(b, b, W - 2 * b, H - 2 * b, ir4, Color.white, 0, y =>
        {
            float t = Mathf.Clamp01((y - b) / Mathf.Max(1, H - 2 * b));
            Color col = t < 0.5f ? Color.Lerp(s.top, s.mid, t * 2) : Color.Lerp(s.mid, s.bot, (t - 0.5f) * 2);
            if (y - b < hiH) col = Color.Lerp(col, Color.white, s.hi);
            else if (H - b - y < loH) col = Color.Lerp(col, Color.black, s.lo * 0.6f);
            return col;
        });
        var tex = p.ToTexture();
        var border = new Vector4(c * S, (c + s.shadowH) * S, c * S, c * S);   // 좌·하·우·상
        sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f * S, 0, SpriteMeshType.FullRect, border);
        panels[s.name] = sp; return sp;
    }

    // 둥근(원형) 패널: 지름 size 단위. 둥근 버튼용(슬라이스 아님)
    static readonly Dictionary<string, Sprite> rounds = new Dictionary<string, Sprite>();
    public static Sprite Round(Style s, float size)
    {
        string key = s.name + ":" + size;
        if (rounds.TryGetValue(key, out var sp)) return sp;
        var p = new Pix(Mathf.CeilToInt(size * S), Mathf.CeilToInt((size + s.shadowH) * S));
        float W = size * S, r = W / 2, b = s.border * S;
        if (s.shadowH > 0) p.Circle(r, r + s.shadowH * S, r, s.shadow);
        p.Circle(r, r, r, s.edge);
        float ir = r - b;
        p.Draw(q => (q - new Vector2(r, r)).magnitude - ir, new Rect(b, b, W - 2 * b, W - 2 * b), Color.white, 0, y =>
        {
            float t = Mathf.Clamp01((y - b) / Mathf.Max(1, W - 2 * b));
            Color col = t < 0.5f ? Color.Lerp(s.top, s.mid, t * 2) : Color.Lerp(s.mid, s.bot, (t - 0.5f) * 2);
            if (y - b < 3 * S) col = Color.Lerp(col, Color.white, s.hi);
            else if (W - b - y < 3 * S) col = Color.Lerp(col, Color.black, s.lo * 0.6f);
            return col;
        });
        var tex = p.ToTexture();
        sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f * S, 0, SpriteMeshType.FullRect);
        rounds[key] = sp; return sp;
    }

    // 말풍선 꼬리(아래 방향 삼각형, 두 빗변만 테두리). 폭 22·높이 11 단위 + 그림자
    public const float TAIL_W = 22, TAIL_H = 11;
    static readonly Dictionary<string, Sprite> tails = new Dictionary<string, Sprite>();
    public static Sprite TailSprite(Style s)
    {
        if (tails.TryGetValue(s.name, out var sp)) return sp;
        float W = TAIL_W * S, H = TAIL_H * S, sh = s.shadowH * S, b = s.border * S;
        var p = new Pix((int)W, (int)(H + sh));
        // 윗변을 텍스처 밖(위)으로 연장한 삼각형: 안쪽 오프셋을 줘도 윗변엔 테두리가 안 생겨 패널과 이어진다
        var tri = new[] { new Vector2(-6 * S, -12 * S), new Vector2(W + 6 * S, -12 * S), new Vector2(W / 2, H) };
        if (sh > 0) { p.off = new Vector2(0, sh); p.Poly(tri, s.shadow); p.off = Vector2.zero; }
        p.Poly(tri, s.edge);
        p.Poly(tri, Color.Lerp(s.bot, Color.black, s.lo * 0.6f), -b);
        var tex = p.ToTexture();
        sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 1f), 100f * S, 0, SpriteMeshType.FullRect);
        tails[s.name] = sp; return sp;
    }

    // 조이스틱 베이스(반투명 흰 원 + 흰 테두리) · 노브(흰 그라데이션 + 남색 테두리 + 그림자)
    static Sprite joyBase, joyKnob;
    public static Sprite JoyBase()
    {
        if (joyBase != null) return joyBase;
        int px = 128; var p = new Pix(px, px); float r = px / 2f;
        p.Circle(r, r, r, new Color(1, 1, 1, 0.5f)); p.Circle(r, r, r - 3, new Color(1, 1, 1, 0.22f));
        // 안쪽 원은 덮어쓰기여야 하므로 바깥 링 + 안쪽 채움으로 분리
        var q = new Pix(px, px); q.Ring(r, r, r - 1.5f, 3, new Color(1, 1, 1, 0.55f)); q.Circle(r, r, r - 3, new Color(1, 1, 1, 0.22f));
        var tex = q.ToTexture(); joyBase = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f); return joyBase;
    }
    public static Sprite JoyKnob()
    {
        if (joyKnob != null) return joyKnob;
        int px = 96, sh = 6; var p = new Pix(px, px + sh); float r = px / 2f, b = 5f;
        p.Circle(r, r + sh, r, new Color(0, 0, 0, 0.35f));
        p.Circle(r, r, r, EDGE);
        p.Draw(q => (q - new Vector2(r, r)).magnitude - (r - b), new Rect(0, 0, px, px), Color.white, 0, y => Color.Lerp(Color.white, Hex("e2e8f4"), Mathf.Clamp01(y / px)));
        var tex = p.ToTexture(); joyKnob = Sprite.Create(tex, new Rect(0, 0, px, px + sh), new Vector2(0.5f, 0.5f), 100f); return joyKnob;
    }

    // ── 아이콘 ──
    // 24×24 격자(원본 SVG viewBox)로 그리고 96px 텍스처로 굽는다. 그림자는 같은 도형을 1.5단위 아래 어두운 실루엣으로.
    class Ico
    {
        public readonly Pix p; public readonly float s;
        struct Part { public Func<Vector2, float> sd; public Rect bb; public Color fill; public Color stroke; public float sw; public bool hasStroke; }
        readonly List<Part> parts = new List<Part>();
        public Ico(Pix p, float s) { this.p = p; this.s = s; }
        Vector2 V(float x, float y) => new Vector2(x * s, y * s);
        void Add(Func<Vector2, float> sd, Rect bb, Color fill, Color? stroke, float sw) => parts.Add(new Part { sd = sd, bb = bb, fill = fill, stroke = stroke ?? Color.clear, hasStroke = stroke.HasValue, sw = sw * s / 2 });
        public void C(float cx, float cy, float r, Color fill, Color? stroke = null, float sw = 1.6f)
        { var ce = V(cx, cy); float rr = r * s; Add(q => (q - ce).magnitude - rr, new Rect(ce.x - rr, ce.y - rr, rr * 2, rr * 2), fill, stroke, sw); }
        public void Ring(float cx, float cy, float r, float thick, Color fill, Color? stroke = null, float sw = 1.6f)
        { var ce = V(cx, cy); float rr = r * s, t = thick * s; Add(q => Mathf.Abs((q - ce).magnitude - rr) - t / 2, new Rect(ce.x - rr - t, ce.y - rr - t, (rr + t) * 2, (rr + t) * 2), fill, stroke, sw); }
        public void R(float x, float y, float w, float h, float r, Color fill, Color? stroke = null, float sw = 1.6f)
        {
            var ce = V(x + w / 2, y + h / 2); var half = V(w / 2, h / 2); float rad = Mathf.Min(r * s, Mathf.Min(half.x, half.y));
            Add(q => { Vector2 d = new Vector2(Mathf.Abs(q.x - ce.x), Mathf.Abs(q.y - ce.y)) - half + Vector2.one * rad; return Mathf.Min(Mathf.Max(d.x, d.y), 0) + new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude - rad; },
                new Rect(x * s, y * s, w * s, h * s), fill, stroke, sw);
        }
        public void Seg(float x1, float y1, float x2, float y2, float w, Color fill, Color? stroke = null, float sw = 1.6f)
        {
            Vector2 a = V(x1, y1), b = V(x2, y2); float hw = w * s / 2;
            Add(q => { Vector2 pa = q - a, ba = b - a; float hh = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-5f, Vector2.Dot(ba, ba))); return (pa - ba * hh).magnitude - hw; },
                Rect.MinMaxRect(Mathf.Min(a.x, b.x) - hw, Mathf.Min(a.y, b.y) - hw, Mathf.Max(a.x, b.x) + hw, Mathf.Max(a.y, b.y) + hw), fill, stroke, sw);
        }
        public void Line(Color col, float w, Color? stroke, float sw, params float[] xy) { for (int i = 0; i + 3 < xy.Length; i += 2) Seg(xy[i], xy[i + 1], xy[i + 2], xy[i + 3], w, col, stroke, sw); }
        public void Line(Color col, float w, params float[] xy) => Line(col, w, null, 1.6f, xy);
        public void P(Color fill, Color? stroke, float sw, params float[] xy)
        {
            var v = new Vector2[xy.Length / 2]; for (int i = 0; i < v.Length; i++) v[i] = V(xy[i * 2], xy[i * 2 + 1]);
            float mnx = float.MaxValue, mny = float.MaxValue, mxx = float.MinValue, mxy = float.MinValue;
            foreach (var q in v) { mnx = Mathf.Min(mnx, q.x); mny = Mathf.Min(mny, q.y); mxx = Mathf.Max(mxx, q.x); mxy = Mathf.Max(mxy, q.y); }
            Add(q =>
            {
                float d = Vector2.Dot(q - v[0], q - v[0]); float sg = 1;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    Vector2 e = v[j] - v[i], wv = q - v[i];
                    Vector2 b = wv - e * Mathf.Clamp01(Vector2.Dot(wv, e) / Mathf.Max(1e-6f, Vector2.Dot(e, e)));
                    d = Mathf.Min(d, Vector2.Dot(b, b));
                    bool c1 = q.y >= v[i].y, c2 = q.y < v[j].y, c3 = e.x * wv.y > e.y * wv.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sg = -sg;
                }
                return sg * Mathf.Sqrt(d);
            }, Rect.MinMaxRect(mnx, mny, mxx, mxy), fill, stroke, sw);
        }
        // 그룹 확정: 외곽선 패스(합집합 실루엣) → 채움 패스
        public void Flush()
        {
            foreach (var pt in parts) if (pt.hasStroke) p.Draw(pt.sd, pt.bb, pt.stroke, pt.sw);
            foreach (var pt in parts) p.Draw(pt.sd, pt.bb, pt.fill, 0);
            parts.Clear();
        }
    }

    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static Sprite Icon(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (icons.TryGetValue(name, out var sp)) return sp;
        int grid = name == "hand" ? 64 : 24; int px = name == "hand" ? 128 : 96;
        var p = new Pix(px, px); float s = (float)px / grid; var g = new Ico(p, s);
        p.off = new Vector2(0, 1.5f * s * (24f / grid)); p.tint = new Color(0, 0, 0, 0.35f); DrawIcon(g, name);
        p.off = Vector2.zero; p.tint = null; DrawIcon(g, name);
        var tex = p.ToTexture();
        sp = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f);
        icons[name] = sp; return sp;
    }

    // 발판 id → 아이콘 이름(GameConfig의 이모지 대신)
    public static string PadIcon(string id)
    {
        switch (id)
        {
            case "hand": return "hand"; case "spd": return "boot"; case "price": return "tag"; case "cashier": return "robot";
            case "bottler": return "bottle"; case "melter": return "snow"; case "shelf": return "shelf"; case "ice2": return "ice";
            case "runner": case "runner2": return "cart"; case "ads": return "sign"; case "drill": return "drill"; case "runspd": return "bolt";
            case "pen": return "rabbit"; case "dome": return "home"; case "sold": return "bottle";
            default: return "flag";
        }
    }

    static void DrawIcon(Ico g, string n)
    {
        Color gold = Hex("ffc83d"), goldD = Hex("a06400"), blue = Hex("66c1f5"), blueD = Hex("2a78b0"), ink = Hex("1b2740"), navy = Hex("2b3a55"),
              white = Color.white, red = Hex("e03a3a"), brown = Hex("3a2208"), wood = Hex("c07a32"), pale = Hex("dbe6ff"), paleD = Hex("6b84b8");
        switch (n)
        {
            case "bell":
                g.C(12, 3.6f, 1.9f, gold, goldD); g.C(12, 10.6f, 5.7f, gold, goldD);
                g.P(gold, goldD, 1.6f, 6.3f, 10.6f, 17.7f, 10.6f, 18, 15, 20, 17, 20, 18.2f, 4, 18.2f, 4, 17, 6, 15); g.Flush();
                g.C(12, 20, 2, goldD); g.Flush(); break;
            case "gear":
                {
                    var pts = new List<float>();
                    for (int k = 0; k < 8; k++)
                    {
                        float a0 = k * 45f; float[] angs = { a0 - 11, a0 + 11, a0 + 14, a0 + 31 }; float[] rads = { 9.6f, 9.6f, 7.3f, 7.3f };
                        for (int i = 0; i < 4; i++) { float a = (angs[i] - 90) * Mathf.Deg2Rad; pts.Add(12 + Mathf.Cos(a) * rads[i]); pts.Add(12 + Mathf.Sin(a) * rads[i]); }
                    }
                    g.P(pale, paleD, 1.4f, pts.ToArray()); g.Flush(); g.C(12, 12, 3.2f, ink); g.Flush(); break;
                }
            case "ice":
                g.R(3, 3, 18, 18, 4, Hex("bfe6ff"), Hex("5a9ccc"), 1.8f); g.Flush();
                g.Seg(7, 9, 10, 6, 2.2f, white); g.Seg(7, 14, 14, 7, 2.2f, white); g.Flush(); break;
            case "bottle":
                g.R(9, 1.5f, 6, 4, 1, Hex("274060")); g.Flush();
                g.P(blue, blueD, 1.6f, 8, 6, 16, 6, 16, 9, 17.5f, 11, 17.5f, 21.5f, 6.5f, 21.5f, 6.5f, 11, 8, 9); g.Flush();
                g.Seg(9.5f, 12, 9.5f, 19, 2, white); g.Flush(); break;
            case "drop":
                g.C(12, 15, 7, blue, blueD); g.P(blue, blueD, 1.6f, 12, 2.5f, 18, 10.8f, 6, 10.8f); g.Flush();
                g.Seg(8.8f, 15, 10.6f, 18, 2, white); g.Flush(); break;
            case "o2":     // 산소통: 흰 봄베 + 파란 띠 + 밸브
                g.R(9.5f, 1.5f, 5, 3.5f, 1, navy); g.Flush();
                g.R(6.5f, 4.5f, 11, 17.5f, 5, Hex("eef7ff"), blueD, 1.6f); g.Flush();
                g.R(7.3f, 9.5f, 9.4f, 3.4f, 0.6f, blue); g.Flush(); break;
            case "food":   // 식량: 노란 그릇 + 초록 새싹
                g.C(9, 8.5f, 3.6f, Hex("7ed37a"), Hex("2f7a2c")); g.C(15, 7.5f, 3.6f, Hex("7ed37a"), Hex("2f7a2c")); g.Flush();
                g.P(Hex("f2b33d"), goldD, 1.6f, 2.5f, 11, 21.5f, 11, 17.5f, 20.5f, 6.5f, 20.5f); g.Flush(); break;
            case "lock":
                g.Ring(12, 7.6f, 4, 2.4f, goldD); g.Flush();
                g.R(4, 10, 16, 11, 2.5f, gold, goldD); g.Flush();
                g.C(12, 15.5f, 1.8f, goldD); g.Flush(); break;
            case "cash":
                g.R(2, 6, 20, 12, 2, Hex("55c35a"), Hex("1f6b25")); g.Flush();
                g.C(12, 12, 3, Hex("c8f5c0"), Hex("1f6b25"), 1.3f); g.Flush(); break;
            case "up":
                g.P(white, goldD, 1.8f, 12, 3, 20, 11.5f, 15, 11.5f, 15, 20, 9, 20, 9, 11.5f, 4, 11.5f); g.Flush(); break;
            case "heart":
                g.C(7.8f, 9.2f, 4.6f, Hex("ff6b8a"), Hex("b8294a")); g.C(16.2f, 9.2f, 4.6f, Hex("ff6b8a"), Hex("b8294a"));
                g.P(Hex("ff6b8a"), Hex("b8294a"), 1.6f, 3.6f, 11, 20.4f, 11, 12, 21); g.Flush(); break;
            case "bang":
                g.Seg(12, 3.5f, 12, 14, 4.5f, red); g.C(12, 19.5f, 2.4f, red); g.Flush(); break;
            case "flag":
                g.Seg(5, 3, 5, 21, 2.6f, goldD); g.Flush();
                g.P(Hex("ff6b5a"), Hex("b8294a"), 1.6f, 6, 4, 18, 4, 15, 8, 18, 12, 6, 12); g.Flush(); break;
            case "chest":
                g.R(3, 4.5f, 18, 8, 4.5f, Hex("d49040"), brown); g.Flush();
                g.R(3, 9, 18, 11, 2, Hex("b5722f"), brown); g.Flush();
                g.R(10, 9, 4, 5, 1, gold, goldD, 1.2f); g.Flush(); break;
            case "crown":
                g.P(gold, goldD, 1.6f, 3, 18, 4, 7, 9, 12, 12, 5, 15, 12, 20, 7, 21, 18); g.Flush();
                g.R(3, 17, 18, 4, 1, Hex("ffe27a"), goldD, 1.4f); g.Flush(); break;
            case "zz":
                g.Line(Hex("5a7bd6"), 3, 4, 8, 12, 8, 4, 17, 12, 17); g.Line(Hex("5a7bd6"), 2.4f, 14, 4, 20, 4, 14, 10, 20, 10); g.Flush(); break;
            case "shelf":
                for (int i = 0; i < 2; i++) { float y = 3 + i * 10; g.R(5, y, 3, 6, .8f, blue, blueD, 1); g.R(10.5f, y, 3, 6, .8f, blue, blueD, 1); g.R(16, y, 3, 6, .8f, blue, blueD, 1); }
                g.Flush(); g.R(2, 9, 20, 3, 1, wood, brown, 1.4f); g.R(2, 19, 20, 3, 1, wood, brown, 1.4f); g.Flush(); break;
            case "arrow":
                g.Line(navy, 3.2f, 4, 12, 17, 12); g.Line(navy, 3.2f, 12, 6, 18, 12, 12, 18); g.Flush(); break;
            case "sun":
                for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; g.Seg(12 + Mathf.Cos(a) * 7.5f, 12 + Mathf.Sin(a) * 7.5f, 12 + Mathf.Cos(a) * 10.5f, 12 + Mathf.Sin(a) * 10.5f, 2.4f, gold, goldD, 1.2f); }
                g.C(12, 12, 5.5f, gold, goldD); g.Flush(); break;
            case "coin":
                g.C(12, 12, 9.6f, Hex("ffc83d"), GOLD_D, 2); g.Flush(); g.C(9.6f, 9.2f, 3.2f, Hex("fff3b0")); g.Flush(); break;
            case "dot":
                g.C(12, 12, 8.5f, Hex("ff4d4d"), white, 2.4f); g.Flush(); break;
            case "sound":
            case "mute":
                g.P(pale, paleD, 1.6f, 3, 9, 8, 9, 13, 4, 13, 20, 8, 15, 3, 15); g.Flush();
                g.Line(pale, 2.2f, paleD, 1.2f, 16, 8.5f, 18.2f, 12, 16, 15.5f); g.Flush();
                if (n == "mute") { g.Seg(4.5f, 4.5f, 20, 20, 2.8f, red, Hex("7a1a10"), 1.2f); g.Flush(); }
                else { g.Line(pale, 2, paleD, 1.2f, 19.5f, 6, 22, 12, 19.5f, 18); g.Flush(); }
                break;
            case "close":
                g.Seg(6, 6, 18, 18, 3.4f, pale, paleD, 1.2f); g.Seg(18, 6, 6, 18, 3.4f, pale, paleD, 1.2f); g.Flush(); break;
            case "check":
                g.Line(Hex("8fe08a"), 3.4f, Hex("1f5a22"), 1.4f, 4.5f, 12.5f, 9.5f, 17.5f, 19.5f, 7); g.Flush(); break;
            case "robot":
                g.Seg(12, 6, 12, 2.5f, 2, Hex("8f949e")); g.C(12, 2.2f, 1.7f, red); g.Flush();
                g.R(4.5f, 6, 15, 12, 3, Hex("c1c1d8"), Hex("3a4260")); g.R(9.5f, 18, 5, 3.5f, 1, Hex("8f949e"), Hex("3a4260")); g.Flush();
                g.C(9, 12, 2, ink); g.C(15, 12, 2, ink); g.R(8.5f, 15, 7, 1.6f, .8f, ink); g.Flush(); break;
            case "cart":
                g.Seg(2, 4, 5, 4, 2, brown); g.Flush();
                g.P(Hex("ffb64b"), Hex("7a4a00"), 1.6f, 4, 6, 22, 6, 19.5f, 15, 6.5f, 15); g.Flush();
                g.C(8.5f, 19, 2.3f, ink, Hex("8f949e"), 1.2f); g.C(16.5f, 19, 2.3f, ink, Hex("8f949e"), 1.2f); g.Flush();
                g.Seg(8, 9, 18, 9, 1.4f, Hex("7a4a00")); g.Seg(8.5f, 12, 17.5f, 12, 1.4f, Hex("7a4a00")); g.Flush(); break;
            case "drill":
                g.R(3, 7.5f, 12, 8.5f, 2.5f, Hex("ffae44"), Hex("7a4a00")); g.R(6.5f, 15, 5.5f, 6.5f, 1.5f, Hex("ffae44"), Hex("7a4a00")); g.Flush();
                g.P(Hex("c8ccd6"), Hex("3a4260"), 1.4f, 15, 9.5f, 23, 11.8f, 15, 14); g.Flush();
                g.Seg(5.5f, 10, 10, 10, 1.4f, Hex("7a4a00")); g.Flush(); break;
            case "sign":
                g.P(Hex("ff6b5a"), Hex("b8294a"), 1.6f, 3, 9, 13, 4.5f, 13, 19.5f, 3, 15); g.R(12.5f, 8, 4.5f, 8, 1.2f, Hex("ffe58a"), Hex("a06400"), 1.4f); g.Flush();
                g.Line(navy, 1.8f, 18.5f, 7.5f, 21, 5.5f); g.Line(navy, 1.8f, 19, 12, 22.5f, 12); g.Line(navy, 1.8f, 18.5f, 16.5f, 21, 18.5f); g.Flush(); break;
            case "bolt":
                g.P(Hex("ffe58a"), goldD, 1.6f, 13.5f, 2, 5, 13.5f, 11, 13.5f, 9.5f, 22, 19, 10.5f, 13, 10.5f); g.Flush(); break;
            case "boot":
                g.P(Hex("ff9a34"), Hex("7a4a00"), 1.6f, 6, 3, 13, 3, 13, 11, 20.5f, 14.5f, 20.5f, 18, 3.5f, 18, 3.5f, 12, 6, 12); g.Flush();
                g.R(3.5f, 17, 17, 3.5f, 1.2f, brown, Hex("7a4a00"), 1.2f); g.Flush();
                g.Seg(7, 7, 11, 7, 1.4f, Hex("7a4a00")); g.Flush(); break;
            case "tag":
                g.P(GOLD, goldD, 1.6f, 3, 3, 13, 3, 21, 11, 11, 21, 3, 13); g.Flush(); g.C(7.8f, 7.8f, 1.9f, goldD); g.Flush(); break;
            case "snow":
                for (int i = 0; i < 3; i++) { float a = i * Mathf.PI / 3; g.Seg(12 - Mathf.Cos(a) * 9, 12 - Mathf.Sin(a) * 9, 12 + Mathf.Cos(a) * 9, 12 + Mathf.Sin(a) * 9, 2.6f, blue, blueD, 1.2f); }
                g.Flush(); g.C(12, 12, 2.6f, white, blueD, 1.2f); g.Flush(); break;
            case "home":
                g.P(Hex("ff8a7a"), Hex("6a1410"), 1.6f, 12, 2.5f, 22.5f, 12, 1.5f, 12); g.Flush();
                g.R(5, 11, 14, 10.5f, 1.5f, Hex("ffe6c8"), Hex("7a4a00")); g.Flush();
                g.R(9.8f, 14.5f, 4.4f, 7, 1.2f, Hex("7a4a00")); g.Flush(); break;
            case "rabbit":
                g.Seg(8.5f, 11, 7, 2.5f, 3.6f, white, Hex("7a7f99")); g.Seg(15.5f, 11, 17, 2.5f, 3.6f, white, Hex("7a7f99")); g.C(12, 14.5f, 6.5f, white, Hex("7a7f99")); g.Flush();
                g.Seg(8.5f, 10.5f, 7.6f, 4.5f, 1.4f, Hex("ffb3c6")); g.Seg(15.5f, 10.5f, 16.4f, 4.5f, 1.4f, Hex("ffb3c6")); g.C(9.6f, 14, 1.3f, ink); g.C(14.4f, 14, 1.3f, ink); g.C(12, 16.8f, 1, Hex("ff6b8a")); g.Flush(); break;
            case "hand":   // 64 격자 온보딩 손 포인터
                g.Seg(31.5f, 9, 31.5f, 32, 11, white, navy, 3.2f); g.R(21, 28, 32, 30, 10, white, navy, 3.2f); g.Seg(14, 42, 25, 50, 9.5f, white, navy, 3.2f); g.Flush();
                g.C(31.5f, 4.5f, 5.5f, new Color(1, 0.84f, 0.3f, 0.9f)); g.Flush(); break;
            default:
                g.C(12, 12, 8, Hex("c3cfe6"), navy); g.Flush(); break;
        }
    }

    // ── 헬퍼 ──
    public static Font Font => UIFont.Get();

    public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size; }

    public static RectTransform RT(Transform parent, string name)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.AddComponent<RectTransform>(); }

    // 9-slice 패널 이미지
    public static Image Panel(Transform parent, string name, Style s)
    {
        var img = RT(parent, name).gameObject.AddComponent<Image>();
        img.sprite = Panel9(s); img.type = Image.Type.Sliced; img.color = Color.white; img.raycastTarget = false;
        return img;
    }
    // 둥근 패널 이미지(지름 size). RectTransform은 (size, size+그림자)
    public static Image RoundPanel(Transform parent, string name, Style s, float size)
    {
        var img = RT(parent, name).gameObject.AddComponent<Image>();
        img.sprite = Round(s, size); img.type = Image.Type.Simple; img.color = Color.white; img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(size, size + s.shadowH);
        return img;
    }
    // 아이콘 이미지(정사각 size)
    public static Image IconImg(Transform parent, string name, float size)
    {
        var img = RT(parent, "ic_" + name).gameObject.AddComponent<Image>();
        img.sprite = Icon(name); img.preserveAspect = true; img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = size; le.preferredHeight = size; le.minWidth = size; le.minHeight = size;
        return img;
    }
    public static void SetIcon(Image img, string name) { if (img != null) img.sprite = Icon(name); }

    // 글자. 레이아웃 그룹 안에서 쓰는 기본형(앵커 미지정)
    public static Text Text(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
    {
        var t = RT(parent, "text").gameObject.AddComponent<Text>();
        t.font = Font; t.fontSize = size; t.color = c; t.alignment = a; t.text = s; t.fontStyle = FontStyle.Normal; t.supportRichText = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        return t;
    }
    // 글자 외곽선(두께 w). Outline 두 겹(대각 + 가로)으로 굵은 스트로크 근사. 알파는 글자 알파를 따른다(플로팅 페이드용)
    public static void Stroke(Text t, Color c, float w)
    {
        if (w <= 0 || c.a <= 0) return;
        foreach (var o in t.GetComponents<Outline>()) UnityEngine.Object.Destroy(o);
        var o1 = t.gameObject.AddComponent<Outline>(); o1.effectColor = c; o1.effectDistance = new Vector2(w * 0.72f, w * 0.72f); o1.useGraphicAlpha = true;
        if (w >= 1.5f) { var o2 = t.gameObject.AddComponent<Outline>(); o2.effectColor = c; o2.effectDistance = new Vector2(w, 0); o2.useGraphicAlpha = true; }
    }
    public static void DropShadow(Text t, float dy = 2f, float a = 0.35f)
    { var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, a); sh.effectDistance = new Vector2(0, -dy); sh.useGraphicAlpha = true; }

    // 가로 레이아웃 행(아이콘+글자). stretch=true면 부모에 꽉 채우고 가운데 정렬
    public static HorizontalLayoutGroup Row(Transform parent, string name, float spacing, TextAnchor align = TextAnchor.MiddleCenter, bool stretch = false, RectOffset pad = null)
    {
        var rt = RT(parent, name);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing; h.childAlignment = align; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
        if (pad != null) h.padding = pad;
        if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        return h;
    }
    // 내용 크기에 맞는 패널(가로 행 하나). 글자·아이콘은 out으로
    public static Image Chip(Transform parent, string name, Style s, string icon, float iconSize, string text, int fs, out Text label, out Image iconImg, float spacing = 6)
    {
        var img = Panel(parent, name, s);
        var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
        h.padding = new RectOffset(s.pad.left, s.pad.right, s.pad.top, s.pad.bottom + Mathf.RoundToInt(s.shadowH));
        var f = img.gameObject.AddComponent<ContentSizeFitter>(); f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        iconImg = icon != null ? IconImg(img.transform, icon, iconSize) : null;
        label = Text(img.transform, text, fs, s.text); Stroke(label, s.stroke, s.strokeW);
        return img;
    }

    // 버튼: 9-slice 패널 + (아이콘) + 글자 + 누름 축소. 글자색·외곽선은 스타일을 따른다
    public static Button Button(Transform parent, string name, Style s, Vector2 size, string text, int fs, Action onClick, string icon = null, float iconSize = 0, Color? textCol = null)
    {
        var img = Panel(parent, name, s); img.raycastTarget = true; img.rectTransform.sizeDelta = size;
        var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
        img.gameObject.AddComponent<UIPress>();
        var row = Row(img.transform, "row", 6, TextAnchor.MiddleCenter, true, new RectOffset(0, 0, 0, Mathf.RoundToInt(s.shadowH)));
        if (icon != null) IconImg(row.transform, icon, iconSize > 0 ? iconSize : fs * 1.1f);
        var t = Text(row.transform, text, fs, textCol ?? s.text); Stroke(t, s.stroke, s.strokeW);
        b.onClick.AddListener(() => onClick?.Invoke());
        return b;
    }
    // 버튼의 스타일·글자·아이콘 바꾸기
    public static void Restyle(Button b, Style s, string text = null, string icon = null)
    {
        var img = b.GetComponent<Image>(); img.sprite = Panel9(s);
        var row = b.transform.Find("row"); if (row == null) return;
        var t = row.GetComponentInChildren<Text>(); if (t != null) { if (text != null) t.text = text; t.color = s.text; Stroke(t, s.stroke, s.strokeW); }
        var ic = row.GetComponentInChildren<Image>(); if (ic != null && icon != null) ic.sprite = Icon(icon);
    }
    // 둥근 아이콘 버튼(지름 size, 44 이상 권장)
    public static Button RoundButton(Transform parent, string name, Style s, float size, string icon, float iconSize, Action onClick, out Image iconImg)
    {
        var img = RoundPanel(parent, name, s, size); img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
        img.gameObject.AddComponent<UIPress>();
        iconImg = IconImg(img.transform, icon, iconSize);
        var irt = iconImg.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f); irt.anchoredPosition = new Vector2(0, s.shadowH / 2);
        b.onClick.AddListener(() => onClick?.Invoke());
        return b;
    }
    // 진행 바: 트랙(size) + 채움. frac 0~1
    public static Image ProgressBar(Transform parent, string name, Vector2 size, out Image fill)
    {
        var track = Panel(parent, name, Track); track.rectTransform.sizeDelta = size;
        var le = track.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = size.x; le.preferredHeight = size.y; le.minWidth = size.x; le.minHeight = size.y;
        fill = Panel(track.transform, "fill", Bar);
        var frt = fill.rectTransform; frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(0, 1); frt.pivot = new Vector2(0, 0.5f); frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(0, 0);
        return track;
    }
    public static void SetBar(Image fill, float frac, float width) { fill.rectTransform.sizeDelta = new Vector2(Mathf.Clamp01(frac) * width, 0); fill.enabled = frac > 0.01f; }

    // 레벨 핍 행(켜짐 n / 전체 max)
    public static Transform Pips(Transform parent, int n, int max, float w = 16, float h = 8)
    {
        var row = Row(parent, "pips", 3, TextAnchor.MiddleLeft);
        for (int i = 0; i < max; i++)
        {
            var p = Panel(row.transform, "pip", i < n ? PipOn : PipOff);
            var le = p.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h + PipOn.shadowH; le.minWidth = w;
        }
        return row.transform;
    }
}

// 누르면 0.93배로 줄어드는 버튼 손맛(.btn:active)
public class UIPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    Vector3 baseScale = Vector3.one; bool down;
    public void OnPointerDown(PointerEventData e) { if (!down) { baseScale = transform.localScale; down = true; } transform.localScale = baseScale * 0.93f; }
    public void OnPointerUp(PointerEventData e) { if (down) { transform.localScale = baseScale; down = false; } }
    void OnDisable() { if (down) { transform.localScale = baseScale; down = false; } }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// §3.4 장식 DECOS(1회 구매, 프레스티지 후 유지): 차양·네온·선인장·줄전구·깃발·바닥 타일. 시트의 장식 탭(sold≥60)에서 산다.
// 원본 283행 DECOS · 826행 buyDeco · 716행 applyDeco. 시각물은 ShopDecor(매장 기본 차양·깃발 포함)가 맡고, 여기는 데이터·구매·검증 인자.
// 검증 인자: -decos all | id,id,…  (장식 즉시 보유), -decobuy id,id,… (2초부터 2.5초 간격으로 실제 구매 → 등장 연출, 벨은 -bells로),
//           -clock N (게임 시계 초: 174~234면 한밤, §7.3)
public class DecoDef { public string id, name, icon; public int price; public string desc; }

public class DecoManager : MonoBehaviour
{
    public static DecoManager I;
    GameState G => GameState.I;
    public static readonly DecoDef[] DECOS =
    {
        new DecoDef{ id="awning", name="파란 차양",   icon="home",  price=150, desc="차양이 파란 줄무늬로" },
        new DecoDef{ id="neon",   name="네온 간판",   icon="sign",  price=250, desc="밤에 빛나는 간판" },
        new DecoDef{ id="plant",  name="선인장 화분", icon="drop",  price=200, desc="매장 네 모서리에 선인장" },
        new DecoDef{ id="lights", name="줄전구",      icon="bolt",  price=300, desc="차양 아래 반짝이는 전구" },
        new DecoDef{ id="flag",   name="보라 깃발",   icon="flag",  price=120, desc="깃발 셋이 보라색으로" },
        new DecoDef{ id="tile",   name="체크무늬 바닥", icon="shelf", price=350, desc="매장 바닥이 체크무늬로" },
    };
    public const int UNLOCK_SOLD = 60;   // 시트 장식 탭이 열리는 판매 수

    /// 매장 장식 시각물(차양·깃발·네온·선인장·줄전구·타일)
    public ShopDecor View { get; private set; }
    /// 장식을 산 직후(id). 시트 갱신 등
    public event Action<string> OnBought;
    int lastSold = -1;

    void Awake() { I = this; View = gameObject.AddComponent<ShopDecor>(); }

    void Start()
    {
        var arg = DevArgs.Get("-decos");
        if (!string.IsNullOrEmpty(arg))
            foreach (var d in DECOS) if (arg == "all" || ("," + arg + ",").Contains("," + d.id + ",")) Set(d.id, 1);
        if (float.TryParse(DevArgs.Get("-clock"), out var ck)) G.S.clock = ck;
        View.Build();
        View.ApplyAll();
        lastSold = G.S.sold;
        G.OnSold += OnSold;
        var buy = DevArgs.Get("-decobuy");
        if (!string.IsNullOrEmpty(buy)) StartCoroutine(CoDevBuy(buy.Split(',')));
    }
    System.Collections.IEnumerator CoDevBuy(string[] ids)
    {
        yield return new WaitForSeconds(2f);
        foreach (var id in ids) { Buy(id.Trim()); yield return new WaitForSeconds(2.5f); }
    }
    void OnDestroy() { if (GameState.I != null) GameState.I.OnSold -= OnSold; }

    // 판매 60을 넘는 순간: 장식 탭이 열렸다고 알려 준다(머리 위 금색 플로팅 + 딩)
    void OnSold()
    {
        if (lastSold >= 0 && lastSold < UNLOCK_SOLD && G.S.sold >= UNLOCK_SOLD && Player.I != null)
        {
            WorldLabels.I.Float(Player.I.view.position + Vector3.up * 3.4f, "장식 상점 열림!", true, true);
            AudioManager.I.Play("ding", 1.1f, 0.7f);
        }
        lastSold = G.S.sold;
    }

    public static DecoDef Def(string id) { foreach (var d in DECOS) if (d.id == id) return d; return null; }
    public bool Unlocked => G != null && G.S.sold >= UNLOCK_SOLD;
    public bool CanBuy(string id) { var d = Def(id); return d != null && !Has(id) && G.S.bells >= d.price; }
    /// 아직 안 샀고 지금 살 수 있는 장식이 하나라도 있으면 true (탭 점 표시)
    public bool AnyAffordable() { foreach (var d in DECOS) if (CanBuy(d.id)) return true; return false; }

    public bool Has(string id)
    {
        var d = G.S.deco; if (d == null) return false;
        switch (id) { case "awning": return d.awning > 0; case "neon": return d.neon > 0; case "plant": return d.plant > 0; case "lights": return d.lights > 0; case "flag": return d.flag > 0; case "tile": return d.tile > 0; }
        return false;
    }
    void Set(string id, int v)
    {
        if (G.S.deco == null) G.S.deco = new DecoSave();
        var d = G.S.deco;
        switch (id) { case "awning": d.awning = v; break; case "neon": d.neon = v; break; case "plant": d.plant = v; break; case "lights": d.lights = v; break; case "flag": d.flag = v; break; case "tile": d.tile = v; break; }
    }

    /// 벨을 내고 장식을 산다. 성공하면 true
    public bool Buy(string id) => Buy(id, 0f);
    /// revealDelay: 시트가 내려가는 동안 등장 연출을 미룰 시간(초)
    public bool Buy(string id, float revealDelay)
    {
        var d = Def(id);
        if (d == null || Has(id) || G.S.bells < d.price) { AudioManager.I.Play("no", 1f, 0.8f); JuiceFX.I.Vibrate(20); return false; }
        G.AddBells(-d.price);
        Set(id, 1);
        G.Save();
        AudioManager.I.Play("up", 1f, 0.9f);
        JuiceFX.I.Vibrate(30);
        View.Reveal(id, revealDelay);   // 팝 + 색종이 20 + 플래시 0.3 + 장식별 등장 연출
        OnBought?.Invoke(id);
        return true;
    }
}

// 시트 카드용 장식 견본 그림(원본 .deco-sw: 46px 둥근 사각·갈색 테두리·아래 하드 그림자 + 장식 미니 그림). 24 격자로 그려 96px 텍스처로 굽는다.
// id: DECOS 6종 + "rocket"(이주 카드)
public static class DecoArt
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    const int N = 96; const float S = N / 24f;

    public static Sprite Swatch(string id)
    {
        if (cache.TryGetValue(id, out var sp) && sp != null) return sp;
        var p = new Painter();
        Color edge = Hex("7a4a00");
        // 카드 틀: 아래 하드 그림자 → 테두리 → 안쪽(클립)
        Func<Vector2, float> frame = q => RRect(q, 12, 11.4f, 10.6f, 10.0f, 4.4f);
        p.Fill(q => RRect(q, 12, 12.3f, 10.6f, 10.0f, 4.4f), edge, false);
        p.Fill(frame, edge, false);
        p.clip = q => RRect(q, 12, 11.4f, 9.85f, 9.25f, 3.7f);
        Draw(p, id);
        // 안쪽 위 하이라이트(inset 0 2px 0 rgba(255,255,255,.4))
        p.Fill(q => Mathf.Max(RRect(q, 12, 11.4f, 9.85f, 9.25f, 3.7f), -RRect(q, 12, 11.9f, 9.85f, 9.25f, 3.7f)), new Color(1, 1, 1, 0.4f));
        p.clip = null;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(p.px); tex.Apply(false, true);
        sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        cache[id] = sp; return sp;
    }

    static void Draw(Painter p, string id)
    {
        Color white = Color.white, navy = Hex("22304a"), ink = Hex("1b2740");
        switch (id)
        {
            case "awning":
                p.Fill(q => -1, Hex("ffe6c8"));                                                      // 매장 벽
                p.Fill(q => Box(q, 12, 18.2f, 12, 2.2f), Hex("c07a32"));                              // 카운터
                for (int i = 0; i < 8; i++) { float x = 1.5f + i * 3f; int k = i;
                    p.Fill(q => Box(q, x + 1.5f, 6.5f, 1.5f, 5f), k % 2 == 0 ? Hex("4fb0f0") : white); }
                for (int i = 0; i < 8; i++) { float x = 3f + i * 3f; int k = i;
                    p.Fill(q => Circ(q, x, 11.5f, 1.55f), k % 2 == 0 ? Hex("4fb0f0") : white, true, Hex("2a78b0"), 0.5f); }
                p.Fill(q => Box(q, 12, 11.2f, 12, 0.5f), Hex("2a78b0"));
                break;
            case "neon":
                p.Fill(q => -1, navy);
                p.Fill(q => Circ(q, 8.6f, 12, 6.2f), new Color(1f, 0.45f, 0.85f, 0.22f));
                p.Fill(q => Circ(q, 16.2f, 12, 5.6f), new Color(0.35f, 0.95f, 0.85f, 0.2f));
                p.Fill(q => Ring(q, 8.6f, 12, 3.6f, 1.7f), Hex("ff8ad8"));
                p.Fill(q => Ring(q, 8.6f, 12, 3.6f, 0.6f), Hex("ffe0f6"));
                p.Fill(q => RRect(q, 16.2f, 14.2f, 2.5f, 3.6f, 1.1f), Hex("5ed6c8"));
                p.Fill(q => Box(q, 16.2f, 9.6f, 1.0f, 1.3f), Hex("5ed6c8"));
                p.Fill(q => Box(q, 16.2f, 7.9f, 1.6f, 0.55f), Hex("5ed6c8"));
                p.Fill(q => Seg(q, 15.3f, 12.6f, 15.3f, 16f, 0.6f), Hex("e0fffb"));
                p.Fill(q => Circ(q, 20.4f, 6.4f, 0.8f), Hex("ffe58a"));
                break;
            case "plant":
                p.Fill(q => -1, Hex("ffe6c8"));
                p.Fill(q => Seg(q, 12, 17, 12, 7.2f, 4.6f), Hex("5cc45e"), true, Hex("2f7a33"), 0.7f);
                p.Fill(q => Seg(q, 12.5f, 12.4f, 16.2f, 12.4f, 2.6f), Hex("5cc45e"), true, Hex("2f7a33"), 0.7f);
                p.Fill(q => Seg(q, 16.2f, 12.4f, 16.2f, 8.6f, 2.6f), Hex("5cc45e"), true, Hex("2f7a33"), 0.7f);
                p.Fill(q => Seg(q, 11.5f, 14.4f, 8f, 14.4f, 2.2f), Hex("4aa84c"), true, Hex("2f7a33"), 0.7f);
                p.Fill(q => Seg(q, 8f, 14.4f, 8f, 11.6f, 2.2f), Hex("4aa84c"), true, Hex("2f7a33"), 0.7f);
                p.Fill(q => Seg(q, 11f, 9f, 11f, 15f, 0.5f), Hex("8fe08a"));
                p.Fill(q => Circ(q, 12, 6.2f, 1.3f), Hex("ff8ad8"), true, Hex("b8294a"), 0.5f);
                p.Fill(q => Poly(q, 7.2f, 16.6f, 16.8f, 16.6f, 15.6f, 21.6f, 8.4f, 21.6f), Hex("c4542a"), true, Hex("6e2a12"), 0.7f);
                p.Fill(q => Box(q, 12, 17, 5.2f, 0.9f), Hex("d4612f"), true, Hex("6e2a12"), 0.6f);
                break;
            case "lights":
                p.Fill(q => -1, Hex("2b3a55"));
                p.Fill(q => Seg(q, 1, 6, 6, 9.2f, 0.5f), Hex("0b1428")); p.Fill(q => Seg(q, 6, 9.2f, 12, 10.4f, 0.5f), Hex("0b1428"));
                p.Fill(q => Seg(q, 12, 10.4f, 18, 9.2f, 0.5f), Hex("0b1428")); p.Fill(q => Seg(q, 18, 9.2f, 23, 6, 0.5f), Hex("0b1428"));
                {
                    var b = new[] { new Vector2(3.4f, 8.6f), new Vector2(8.6f, 11.6f), new Vector2(14.2f, 12.2f), new Vector2(19.6f, 10.4f) };
                    for (int i = 0; i < b.Length; i++)
                    {
                        var c = b[i]; var col = i % 2 == 0 ? Hex("ffe08a") : Hex("ffb0b0");
                        p.Fill(q => Circ(q, c.x, c.y + 1.4f, 3.4f), new Color(col.r, col.g, col.b, 0.25f));
                        p.Fill(q => Box(q, c.x, c.y - 0.6f, 0.7f, 0.6f), Hex("0b1428"));
                        p.Fill(q => Circ(q, c.x, c.y + 1.2f, 1.5f), col);
                        p.Fill(q => Circ(q, c.x - 0.45f, c.y + 0.8f, 0.45f), white);
                    }
                }
                p.Fill(q => Circ(q, 5, 17, 0.5f), Hex("ffe58a")); p.Fill(q => Circ(q, 18, 18.5f, 0.4f), white);
                break;
            case "flag":
                p.Fill(q => -1, Hex("ffe6c8"));
                p.Fill(q => Seg(q, 7.4f, 3.6f, 7.4f, 20.4f, 1.3f), Hex("6e3f26"));
                p.Fill(q => Circ(q, 7.4f, 3.4f, 1.1f), Hex("ffc83d"), true, Hex("a06400"), 0.5f);
                p.Fill(q => Poly(q, 8.2f, 5f, 18.8f, 5.6f, 16f, 9f, 18.8f, 12.6f, 8.2f, 12.4f), Hex("8a5cd6"), true, Hex("4a2a8a"), 0.7f);
                p.Fill(q => Seg(q, 9.6f, 7f, 14.6f, 7.2f, 0.6f), Hex("c49bf0"));
                p.Fill(q => Box(q, 7.4f, 20.6f, 3.2f, 0.8f), Hex("8c98a4"));
                break;
            case "tile":
                for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                {
                    float cx = 1.6f + x * 4.6f + 2.3f, cy = 1.6f + y * 4.6f + 2.3f; bool dk = (x + y) % 2 == 1;
                    p.Fill(q => Box(q, cx, cy, 2.3f, 2.3f), dk ? Hex("d9a46c") : Hex("f3d9b4"));
                    if (dk) p.Fill(q => Box(q, cx, cy - 1.9f, 2.3f, 0.4f), Hex("e8bd88"));
                }
                break;
            case "rocket":
                p.Fill(q => -1, Hex("2c416b"));
                p.Fill(q => Circ(q, 5, 5, 0.6f), white); p.Fill(q => Circ(q, 19, 8, 0.5f), white); p.Fill(q => Circ(q, 17.5f, 18.5f, 0.45f), Hex("ffe58a"));
                p.Fill(q => Poly(q, 10.2f, 17.4f, 13.8f, 17.4f, 12f, 22.4f), Hex("ffb64b"));
                p.Fill(q => Poly(q, 11.1f, 17.4f, 12.9f, 17.4f, 12f, 20.4f), Hex("ffe58a"));
                p.Fill(q => Poly(q, 9.4f, 12.4f, 6.6f, 17.6f, 9.4f, 16.6f), Hex("ff6b5a"), true, Hex("6a1410"), 0.6f);
                p.Fill(q => Poly(q, 14.6f, 12.4f, 17.4f, 17.6f, 14.6f, 16.6f), Hex("ff6b5a"), true, Hex("6a1410"), 0.6f);
                p.Fill(q => Mathf.Max(RRect(q, 12, 11, 2.7f, 6.6f, 2.6f), q.y - 17.4f), Hex("f8f6f2"), true, ink, 0.7f);
                p.Fill(q => Poly(q, 9.4f, 7.6f, 12f, 2.4f, 14.6f, 7.6f), Hex("ff6b5a"), true, Hex("6a1410"), 0.6f);
                p.Fill(q => Circ(q, 12, 10.6f, 1.5f), Hex("66c1f5"), true, ink, 0.6f);
                break;
            default:
                p.Fill(q => -1, Hex("ffe6c8"));
                break;
        }
    }

    static Color Hex(string h) => UIKit.Hex(h);
    // SDF (24 격자, y 아래로)
    static float Circ(Vector2 q, float cx, float cy, float r) => (q - new Vector2(cx, cy)).magnitude - r;
    static float Ring(Vector2 q, float cx, float cy, float r, float w) => Mathf.Abs((q - new Vector2(cx, cy)).magnitude - r) - w / 2;
    static float Box(Vector2 q, float cx, float cy, float hx, float hy) => RRect(q, cx, cy, hx, hy, 0);
    static float RRect(Vector2 q, float cx, float cy, float hx, float hy, float r)
    {
        Vector2 d = new Vector2(Mathf.Abs(q.x - cx) - hx + r, Mathf.Abs(q.y - cy) - hy + r);
        return Mathf.Min(Mathf.Max(d.x, d.y), 0) + new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude - r;
    }
    static float Seg(Vector2 q, float x1, float y1, float x2, float y2, float w)
    {
        Vector2 a = new Vector2(x1, y1), b = new Vector2(x2, y2), pa = q - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-5f, Vector2.Dot(ba, ba)));
        return (pa - ba * h).magnitude - w / 2;
    }
    static float Poly(Vector2 q, params float[] xy)
    {
        int n = xy.Length / 2; float d = float.MaxValue, sg = 1;
        for (int i = 0, j = n - 1; i < n; j = i, i++)
        {
            Vector2 vi = new Vector2(xy[i * 2], xy[i * 2 + 1]), vj = new Vector2(xy[j * 2], xy[j * 2 + 1]);
            Vector2 e = vj - vi, w = q - vi;
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(1e-6f, Vector2.Dot(e, e)));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = q.y >= vi.y, c2 = q.y < vj.y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sg = -sg;
        }
        return sg * Mathf.Sqrt(d);
    }

    // 픽셀 칠하기: 도형마다 전체 픽셀을 돌며 SDF 커버리지(1px 안티에일리어싱)로 알파 합성. clip이 있으면 그 안쪽만
    class Painter
    {
        public readonly Color[] px = new Color[N * N];
        public Func<Vector2, float> clip;
        public void Fill(Func<Vector2, float> sd, Color c, bool useClip = true, Color? stroke = null, float sw = 0)
        {
            if (stroke.HasValue) Paint(q => sd(q) - sw, stroke.Value, useClip);
            Paint(sd, c, useClip);
        }
        void Paint(Func<Vector2, float> sd, Color c, bool useClip)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var q = new Vector2((x + 0.5f) / S, 24f - (y + 0.5f) / S);
                    float cov = Mathf.Clamp01(0.5f - sd(q) * S);
                    if (useClip && clip != null) cov *= Mathf.Clamp01(0.5f - clip(q) * S);
                    if (cov <= 0) continue;
                    float a = c.a * cov; int i = y * N + x; var d = px[i];
                    float oa = a + d.a * (1 - a);
                    if (oa <= 0) continue;
                    px[i] = new Color((c.r * a + d.r * d.a * (1 - a)) / oa, (c.g * a + d.g * d.a * (1 - a)) / oa, (c.b * a + d.b * d.a * (1 - a)) / oa, oa);
                }
        }
    }
}

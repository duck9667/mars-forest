using System.Collections.Generic;
using UnityEngine;

// 지면·구역 바닥·잠긴 구역 윤곽을 코드로 그린 텍스처. 원본 index.html 450~484행(캔버스 지면·floorTex·buildDash) 이식.
// 그리기 좌표는 논리 단위(400×1040, y↓)이고 버퍼는 캔버스처럼 (0,0)이 왼쪽 위다. 업로드 때 세로를 뒤집어
// 바닥 Quad(회전 X=90 → uv v↑ = 북쪽 = 논리 y 작은 쪽)에 맞춘다.
public class GroundPainter
{
    // ---------- 버퍼 ----------
    public readonly int TW, TH;            // 텍셀 크기
    readonly float ox, oy, s;              // 논리 원점(왼쪽 위)·텍셀/논리 배율
    readonly float[] R, G, B, A;           // 0..1 sRGB 값 그대로
    readonly System.Random rnd;

    public GroundPainter(float x0, float y0, float wL, float hL, float texelPerUnit, int seed, Color? clear)
    {
        ox = x0; oy = y0; s = texelPerUnit;
        TW = Mathf.CeilToInt(wL * s); TH = Mathf.CeilToInt(hL * s);
        int n = TW * TH; R = new float[n]; G = new float[n]; B = new float[n]; A = new float[n];
        rnd = new System.Random(seed);
        if (clear.HasValue) { var c = clear.Value; for (int i = 0; i < n; i++) { R[i] = c.r; G[i] = c.g; B[i] = c.b; A[i] = 1f; } }
    }

    float Rn() => (float)rnd.NextDouble();
    float Rn(float a, float b) => a + (b - a) * (float)rnd.NextDouble();
    static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

    // 텍셀 알파 블렌딩 (투명 버퍼는 알파도 누적)
    void Blend(int x, int y, float r, float g, float b, float a)
    {
        if ((uint)x >= (uint)TW || (uint)y >= (uint)TH || a <= 0f) return;
        if (a > 1f) a = 1f;
        int i = y * TW + x; float ia = 1f - a;
        R[i] = R[i] * ia + r * a; G[i] = G[i] * ia + g * a; B[i] = B[i] * ia + b * a; A[i] = A[i] + a * (1f - A[i]);
    }

    // ---------- 기본 도형 (논리 좌표, 가장자리 AA 1텍셀) ----------
    public void Rect(float x, float y, float w, float h, Color c, float a)
    {
        int x0 = Mathf.FloorToInt((x - ox) * s), y0 = Mathf.FloorToInt((y - oy) * s), x1 = Mathf.CeilToInt((x + w - ox) * s), y1 = Mathf.CeilToInt((y + h - oy) * s);
        for (int py = Mathf.Max(0, y0); py < Mathf.Min(TH, y1); py++) for (int px = Mathf.Max(0, x0); px < Mathf.Min(TW, x1); px++) Blend(px, py, c.r, c.g, c.b, a);
    }

    public void Disc(float cx, float cy, float r, Color c, float a) => Ellipse(cx, cy, r, r, 0f, c, a);

    public void Ellipse(float cx, float cy, float rx, float ry, float rot, Color c, float a)
    {
        float tcx = (cx - ox) * s, tcy = (cy - oy) * s, trx = rx * s, trY = ry * s, rm = Mathf.Max(trx, trY) + 1f;
        float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot), mn = Mathf.Min(trx, trY);
        int x0 = Mathf.Max(0, Mathf.FloorToInt(tcx - rm)), x1 = Mathf.Min(TW - 1, Mathf.CeilToInt(tcx + rm));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(tcy - rm)), y1 = Mathf.Min(TH - 1, Mathf.CeilToInt(tcy + rm));
        for (int py = y0; py <= y1; py++) for (int px = x0; px <= x1; px++)
        {
            float dx = px + 0.5f - tcx, dy = py + 0.5f - tcy;
            float u = (dx * cr + dy * sr) / trx, v = (-dx * sr + dy * cr) / trY;
            float nd = Mathf.Sqrt(u * u + v * v);
            float cov = Mathf.Clamp01((1f - nd) * mn + 0.5f);
            if (cov > 0f) Blend(px, py, c.r, c.g, c.b, a * cov);
        }
    }

    // 원 테두리(폭 width)
    public void Ring(float cx, float cy, float r, float width, Color c, float a)
    {
        float tcx = (cx - ox) * s, tcy = (cy - oy) * s, tr = r * s, hw = width * s * 0.5f, rm = tr + hw + 1f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(tcx - rm)), x1 = Mathf.Min(TW - 1, Mathf.CeilToInt(tcx + rm));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(tcy - rm)), y1 = Mathf.Min(TH - 1, Mathf.CeilToInt(tcy + rm));
        for (int py = y0; py <= y1; py++) for (int px = x0; px <= x1; px++)
        {
            float dx = px + 0.5f - tcx, dy = py + 0.5f - tcy;
            float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - tr);
            float cov = Mathf.Clamp01(hw - d + 0.5f);
            if (cov > 0f) Blend(px, py, c.r, c.g, c.b, a * cov);
        }
    }

    // ---- 획(stroke): 여러 선분의 커버리지를 max로 모았다가 한 번에 블렌딩 → 꺾은선 꼭짓점·점선 끝이 겹쳐 진해지지 않는다 ----
    float[] cov; List<int> touched; bool stroking;
    void BeginStroke()
    {
        if (cov == null) { cov = new float[TW * TH]; touched = new List<int>(1 << 14); }
        stroking = true;
    }
    void EndStroke(Color c, float a)
    {
        stroking = false;
        foreach (int i in touched) { float k = cov[i]; cov[i] = 0f; Blend(i % TW, i / TW, c.r, c.g, c.b, a * k); }
        touched.Clear();
    }
    void Mark(int x, int y, float k, Color c, float a)
    {
        if (!stroking) { Blend(x, y, c.r, c.g, c.b, a * k); return; }
        if ((uint)x >= (uint)TW || (uint)y >= (uint)TH) return;
        int i = y * TW + x; if (cov[i] <= 0f) touched.Add(i); if (k > cov[i]) cov[i] = k;
    }

    // 선분(둥근 끝). 긴 선은 32텍셀 조각으로 나눠(반열린 구간, 끝 조각만 둥근 끝) bbox 낭비를 줄인다
    public void Line(float x0, float y0, float x1, float y1, float width, Color c, float a)
    {
        float ax = (x0 - ox) * s, ay = (y0 - oy) * s, bx = (x1 - ox) * s, by = (y1 - oy) * s, hw = width * s * 0.5f;
        float L = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        int n = Mathf.Max(1, Mathf.CeilToInt(L / 32f));
        for (int i = 0; i < n; i++)
        {
            float t0 = i / (float)n, t1 = (i + 1) / (float)n;
            Seg(ax + (bx - ax) * t0, ay + (by - ay) * t0, ax + (bx - ax) * t1, ay + (by - ay) * t1, hw, i == 0, i == n - 1, c, a);
        }
    }
    void Seg(float ax, float ay, float bx, float by, float hw, bool capA, bool capB, Color c, float a)
    {
        int px0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, bx) - hw - 1)), px1 = Mathf.Min(TW - 1, Mathf.CeilToInt(Mathf.Max(ax, bx) + hw + 1));
        int py0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - hw - 1)), py1 = Mathf.Min(TH - 1, Mathf.CeilToInt(Mathf.Max(ay, by) + hw + 1));
        float vx = bx - ax, vy = by - ay, L2 = vx * vx + vy * vy;
        for (int py = py0; py <= py1; py++) for (int px = px0; px <= px1; px++)
        {
            float qx = px + 0.5f - ax, qy = py + 0.5f - ay;
            float t = L2 > 0f ? (qx * vx + qy * vy) / L2 : 0f;
            if (t < 0f) { if (!capA) continue; t = 0f; } else if (t >= 1f) { if (!capB) continue; t = 1f; }
            float dx = qx - vx * t, dy = qy - vy * t;
            float k = Mathf.Clamp01(hw - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            if (k > 0f) Mark(px, py, k, c, a);
        }
    }

    public void Polyline(IList<Vector2> p, float width, Color c, float a, bool closed = false)
    {
        int n = p.Count; if (n < 2) return;
        BeginStroke();
        for (int i = 0; i < (closed ? n : n - 1); i++) { var q = p[(i + 1) % n]; Line(p[i].x, p[i].y, q.x, q.y, width, c, a); }
        EndStroke(c, a);
    }

    // 점선: dash/gap 길이(논리), 둥근 끝 때문에 대시 양끝을 폭의 절반만큼 줄인다
    public void Dashed(IList<Vector2> p, float width, float dash, float gap, Color c, float a, bool closed = false)
    {
        int n = p.Count; if (n < 2) return;
        float period = dash + gap, phase = 0f, trim = Mathf.Min(width * 0.5f, dash * 0.3f);
        BeginStroke();
        for (int i = 0; i < (closed ? n : n - 1); i++)
        {
            Vector2 A0 = p[i], B0 = p[(i + 1) % n]; float L = Vector2.Distance(A0, B0); if (L <= 0.0001f) continue;
            Vector2 d = (B0 - A0) / L; float t = 0f;
            while (t < L)
            {
                if (phase < dash)
                {
                    float on = Mathf.Min(dash - phase, L - t);
                    float t0 = t + (phase <= 0.0001f ? trim : 0f), t1 = t + on - (phase + on >= dash - 0.0001f ? trim : 0f);
                    if (t1 > t0) { var s0 = A0 + d * t0; var s1 = A0 + d * t1; Line(s0.x, s0.y, s1.x, s1.y, width, c, a); }
                    t += on; phase += on;
                }
                else
                {
                    float off = Mathf.Min(period - phase, L - t); t += off; phase += off;
                }
                if (phase >= period - 0.0001f) phase = 0f;
            }
        }
        EndStroke(c, a);
    }

    // 다각형 채우기(스캔라인, 짝홀 규칙)
    public void Poly(IList<Vector2> p, Color c, float a)
    {
        int n = p.Count; if (n < 3) return;
        float ymin = float.MaxValue, ymax = float.MinValue;
        var tp = new Vector2[n];
        for (int i = 0; i < n; i++) { tp[i] = new Vector2((p[i].x - ox) * s, (p[i].y - oy) * s); ymin = Mathf.Min(ymin, tp[i].y); ymax = Mathf.Max(ymax, tp[i].y); }
        var xs = new List<float>();
        for (int py = Mathf.Max(0, Mathf.FloorToInt(ymin)); py <= Mathf.Min(TH - 1, Mathf.CeilToInt(ymax)); py++)
        {
            float yc = py + 0.5f; xs.Clear();
            for (int i = 0; i < n; i++)
            {
                var e0 = tp[i]; var e1 = tp[(i + 1) % n];
                if ((e0.y <= yc && e1.y > yc) || (e1.y <= yc && e0.y > yc)) xs.Add(e0.x + (yc - e0.y) / (e1.y - e0.y) * (e1.x - e0.x));
            }
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
                for (int px = Mathf.Max(0, Mathf.RoundToInt(xs[k])); px < Mathf.Min(TW, Mathf.RoundToInt(xs[k + 1])); px++) Blend(px, py, c.r, c.g, c.b, a);
        }
    }

    public static List<Vector2> RoundRectPath(float x, float y, float w, float h, float r, int segs = 6)
    {
        var p = new List<Vector2>(); r = Mathf.Min(r, w / 2, h / 2);
        void Arc(float cx, float cy, float a0) { for (int i = 0; i <= segs; i++) { float a = a0 + i / (float)segs * Mathf.PI / 2; p.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); } }
        Arc(x + w - r, y + r, -Mathf.PI / 2); Arc(x + w - r, y + h - r, 0f); Arc(x + r, y + h - r, Mathf.PI / 2); Arc(x + r, y + r, Mathf.PI);
        return p;
    }
    public void RoundRect(float x, float y, float w, float h, float r, Color c, float a) => Poly(RoundRectPath(x, y, w, h, r), c, a);

    public static List<Vector2> QuadCurve(Vector2 p0, Vector2 p1, Vector2 p2, int n = 24)
    {
        var o = new List<Vector2>(n + 1);
        for (int i = 0; i <= n; i++) { float t = i / (float)n, u = 1 - t; o.Add(u * u * p0 + 2 * u * t * p1 + t * t * p2); }
        return o;
    }
    public static List<Vector2> CubicCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int n = 24)
    {
        var o = new List<Vector2>(n + 1);
        for (int i = 0; i <= n; i++) { float t = i / (float)n, u = 1 - t; o.Add(u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3); }
        return o;
    }

    // ---------- 질감 ----------
    // 셀 명암: ts 단위 격자마다 밝은/어두운 면을 ±amp/2 알파로
    public void Cells(float ts, float amp, Color light, Color dark, float x0, float y0, float w, float h, bool checker = false)
    {
        int nx = Mathf.CeilToInt(w / ts), ny = Mathf.CeilToInt(h / ts);
        for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++)
        {
            float a = (Rn() - 0.5f) * amp; if (checker && ((i + j) & 1) == 1) a -= amp;
            Rect(x0 + i * ts, y0 + j * ts, ts, ts, a > 0 ? light : dark, Mathf.Abs(a));
        }
    }
    // 텍셀 단위 미세 그레인
    public void Grain(float amp)
    {
        int n = TW * TH;
        for (int i = 0; i < n; i++) { float d = (Rn() - 0.5f) * amp; R[i] = Mathf.Clamp01(R[i] + d); G[i] = Mathf.Clamp01(G[i] + d * 0.8f); B[i] = Mathf.Clamp01(B[i] + d * 0.6f); }
    }
    // 가장자리로 갈수록 어두워지는 띠(구역 바닥용)
    public void EdgeShade(float inset, float amp, Color c)
    {
        float tin = inset * s;
        for (int py = 0; py < TH; py++) for (int px = 0; px < TW; px++)
        {
            float d = Mathf.Min(Mathf.Min(px + 0.5f, TW - px - 0.5f), Mathf.Min(py + 0.5f, TH - py - 0.5f));
            if (d >= tin) continue;
            float k = 1f - d / tin; Blend(px, py, c.r, c.g, c.b, amp * k * k);
        }
    }

    // 기지 비네트: 기지 사각형(x 16~384, y Y_MIN+4~1010, 북쪽 생존 기지 포함) 바깥은 거리에 따라 어둡게·채도 낮게(최대 −38%·−50%).
    // 안쪽 밝기는 예전 기지(중심 200,525) 기준 그대로라 북쪽 개척지는 밝게도 어둡게도 하지 않은 맨땅 톤이 된다
    public void BaseVignette()
    {
        const float bx0 = 16f, bx1 = 384f, by0 = GameConfig.Y_MIN + 4f, by1 = 1010f, fall = 200f;
        for (int py = 0; py < TH; py++)
        {
            float ly = oy + (py + 0.5f) / s;
            float dy = ly < by0 ? by0 - ly : ly > by1 ? ly - by1 : 0f;
            for (int px = 0; px < TW; px++)
            {
                float lx = ox + (px + 0.5f) / s;
                float dx = lx < bx0 ? bx0 - lx : lx > bx1 ? lx - bx1 : 0f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                int i = py * TW + px;
                if (d <= 0f)
                {   // 기지 안: 가운데로 갈수록 아주 살짝 밝게(최대 +6%)
                    float cx = Mathf.Abs(lx - 200f) / 200f, cy = Mathf.Abs(ly - 525f) / 500f;
                    float b = 0.06f * (1f - Mathf.Clamp01(Mathf.Max(cx, cy) * 0.9f));
                    R[i] = Mathf.Min(1f, R[i] + (1f - R[i]) * b); G[i] = Mathf.Min(1f, G[i] + (1f - G[i]) * b); B[i] = Mathf.Min(1f, B[i] + (1f - B[i]) * b);
                    continue;
                }
                float t = Mathf.Clamp01(d / fall); t = t * t * (3f - 2f * t);
                float lum = R[i] * 0.3f + G[i] * 0.55f + B[i] * 0.15f, sat = 1f - 0.5f * t, dark = 1f - 0.38f * t;
                R[i] = (lum + (R[i] - lum) * sat) * dark; G[i] = (lum + (G[i] - lum) * sat) * dark; B[i] = (lum + (B[i] - lum) * sat) * dark;
            }
        }
    }

    // mips: 지면처럼 화면보다 촘촘한 텍스처는 밉맵을 켜야 카메라가 움직일 때 반짝임이 없다
    public Texture2D ToTexture(bool transparent, bool mips = false)
    {
        var tex = new Texture2D(TW, TH, transparent ? TextureFormat.RGBA32 : TextureFormat.RGB24, mips);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
        var px = new Color32[TW * TH];
        for (int y = 0; y < TH; y++)
        {
            int src = y * TW, dst = (TH - 1 - y) * TW;   // 세로 뒤집기: 텍스처 0행 = 남쪽
            for (int x = 0; x < TW; x++)
            {
                int i = src + x;
                px[dst + x] = new Color32((byte)(R[i] * 255f + 0.5f), (byte)(G[i] * 255f + 0.5f), (byte)(B[i] * 255f + 0.5f), (byte)((transparent ? A[i] : 1f) * 255f + 0.5f));
            }
        }
        tex.SetPixels32(px); tex.Apply(mips, true);
        return tex;
    }

    // =====================================================================
    // 지면: 화성 흙 바탕 + 넓은 명암·옅은 판·균열·얼룩·크레이터·점·그레인·비네트, 손님 길 점선, 착륙선·로버 자국, 줄 자리 발자국, 착륙장
    // + 생존 기지 길(북쪽 길·가운데 길·통로). 원본 452~469행.
    // 범위: 카메라가 보는 x −50~450(직교 반폭 210 + 약간 기울기)·y Y_MIN−100~1090에 여유를 둬 x −150~550, y Y_MIN−210~1240.
    // 예전(x −300~700, y −300~1340)은 옆 여백이 너무 넓어 북쪽을 늘리고도 텍셀 수는 오히려 줄었다(4.2M → 3.3M).
    // =====================================================================
    public const float GX0 = -150f, GY0 = GameConfig.Y_MIN - 210f;
    public const float GW = GameConfig.W + 300f, GH = GameConfig.H + 200f - GY0;
    public const float TPU = 1.6f;   // 텍셀/논리. 1120×2944 RGB24 ≈ 9.9MB(+밉맵)

    public static Texture2D PaintGround()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = new GroundPainter(GX0, GY0, GW, GH, TPU, 11, GameConfig.Hex("c97754"));   // 바탕 흙: cf5f2e에서 채도 한 단계 낮춰 뒤로 물러나게(2026-10-25)
        float X0 = GX0, Y0 = GY0, X1 = GX0 + GW, Y1 = GY0 + GH;
        Color light = C(255, 220, 190), dark = C(60, 20, 10);

        // jake 피드백("배경이 어설프다"): 잔무늬(균열·얼룩·자갈)는 원본의 절반 이하로 줄이고, 넓은 명암 그라데이션 + 비네트(기지 밝게·바깥 어둡고 채도 낮게)로 시선을 기지에 모은다
        // 1) 넓은 명암: 큰 부드러운 얼룩 몇 개 + 80 단위 셀 명암(약하게)
        // 개수는 범위가 바뀌어도 보이는 밀도가 예전과 같게(보이는 면적/전체 면적 비율로 환산)
        for (int i = 0; i < 12; i++)
        {
            bool lt = p.Rn() < 0.5f;
            p.Ellipse(p.Rn(X0, X1), p.Rn(Y0, Y1), 160f + p.Rn() * 220f, 120f + p.Rn() * 160f, p.Rn() * 3f, lt ? C(255, 215, 180) : C(90, 30, 10), 0.05f + p.Rn() * 0.04f);
        }
        p.Cells(80f, 0.04f, light, dark, X0, Y0, GW, GH);

        // 2) 갈라진 판: 보로노이풍 다각형 + 옅은 균열선 (원본 64개 → 24개)
        for (int i = 0; i < 24; i++)
        {
            float cx = p.Rn(X0, X1), cy = p.Rn(Y0, Y1); int n = 5 + (int)(p.Rn() * 4); float r = 70f + p.Rn() * 110f;
            var poly = new List<Vector2>();
            for (int k = 0; k < n; k++) { float a = k / (float)n * Mathf.PI * 2, rr = r * (0.7f + p.Rn() * 0.5f); poly.Add(new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr)); }
            float l = (p.Rn() - 0.5f) * 0.08f;
            p.Poly(poly, l > 0 ? C(255, 215, 180) : C(70, 20, 8), Mathf.Abs(l));
            p.Polyline(poly, 1.6f, C(70, 18, 6), 0.09f, true);
        }
        // 2b) 가는 균열 네트워크: 짧은 갈래가 뻗는 폴리라인(원본엔 없음, 70 → 18)
        for (int i = 0; i < 18; i++)
        {
            float x = p.Rn(X0, X1), y = p.Rn(Y0, Y1), ang = p.Rn() * Mathf.PI * 2;
            var pts = new List<Vector2> { new Vector2(x, y) };
            int segs = 3 + (int)(p.Rn() * 4);
            for (int k = 0; k < segs; k++) { ang += (p.Rn() - 0.5f) * 1.2f; float len = 10f + p.Rn() * 22f; x += Mathf.Cos(ang) * len; y += Mathf.Sin(ang) * len; pts.Add(new Vector2(x, y)); }
            p.Polyline(pts, 1.1f, C(70, 18, 6), 0.12f);
            if (p.Rn() < 0.6f) { var b = pts[1 + (int)(p.Rn() * (pts.Count - 2))]; float ba = ang + (p.Rn() < 0.5f ? 1f : -1f) * (0.8f + p.Rn() * 0.6f); p.Line(b.x, b.y, b.x + Mathf.Cos(ba) * 14f, b.y + Mathf.Sin(ba) * 14f, 0.9f, C(70, 18, 6), 0.10f); }
        }
        // 3) 얼룩 타원(원본 60개 → 22개, 옅게)
        for (int i = 0; i < 22; i++)
        {
            bool lt = p.Rn() < 0.5f;
            p.Ellipse(p.Rn(X0, X1), p.Rn(Y0, Y1), 16f + p.Rn() * 50f, 10f + p.Rn() * 28f, p.Rn() * 3f, lt ? C(255, 225, 200) : C(90, 30, 10), 0.04f + p.Rn() * 0.05f);
        }
        // 4) 크레이터(놀이 영역 밖): 어두운 테두리 + 밝은 안쪽 + 그늘 + 가는 림 하이라이트
        for (int made = 0, tries = 0; made < 8 && tries < 300; tries++)
        {
            float x = p.Rn(X0, X1), y = p.Rn(Y0, Y1), r = 10f + p.Rn() * 22f;
            if (x > -20 && x < 420 && y > GameConfig.Y_MIN - 10f && y < 960) continue;
            made++;
            p.Ellipse(x, y, r * 1.15f, r * 0.8f, 0f, C(60, 15, 5), 0.35f);
            p.Ellipse(x, y - r * 0.1f, r * 0.8f, r * 0.5f, 0f, C(255, 200, 170), 0.22f);
            p.Ellipse(x, y + r * 0.15f, r * 0.55f, r * 0.3f, 0f, C(90, 25, 8), 0.30f);
            p.Ellipse(x, y - r * 0.55f, r * 0.9f, r * 0.22f, 0f, C(255, 225, 200), 0.14f);
        }
        // 5) 자갈 점(원본 900개 → 320개, 옅게)
        for (int i = 0; i < 320; i++)
        {
            bool d = p.Rn() < 0.5f;
            p.Disc(p.Rn(X0, X1), p.Rn(Y0, Y1), 0.8f + p.Rn() * 1.6f, d ? C(80, 30, 15) : C(255, 225, 200), 0.10f + p.Rn() * 0.15f);
        }
        // 6) 미세 그레인(약하게) + 비네트
        p.Grain(0.03f);
        p.BaseVignette();

        // 7) 손님 길: 다져진 띠(연한 둥근 사각) + 바퀴 자국 점선 2줄(입구→줄 끝, 사인 흔들림)
        float QX = WorldMap.QUEUE_X; var E = WorldMap.ENTRY;
        p.RoundRect(QX - 30f, 560f, 60f, GameConfig.H - 560f + 60f, 26f, C(255, 236, 214), 0.13f);
        Color track = C(70, 22, 8);
        foreach (float o in new[] { -10f, 10f })
        {
            var pts = new List<Vector2> { new Vector2(E.x + o, E.y + 10f) };
            for (float y = E.y; y > 560f; y -= 20f) pts.Add(new Vector2(QX + o + Mathf.Sin(y / 70f) * 2.5f, y));
            p.Dashed(pts, 5f, 16f, 9f, track, 0.26f);
        }
        // 착륙선(84,988) → 입구 옆으로 이어지는 자국
        p.Dashed(QuadCurve(new Vector2(84f + 30f, 988f - 6f), new Vector2(150f, 1000f), new Vector2(E.x - 40f, 1010f)), 5f, 16f, 9f, track, 0.26f);
        // 왼쪽 위 로버 자국 2줄
        foreach (float o in new[] { -8f, 8f })
            p.Dashed(CubicCurve(new Vector2(-180f, 40f + o), new Vector2(-60f, 20f + o), new Vector2(-30f, 60f + o), new Vector2(10f, 30f + o)), 5f, 16f, 9f, track, 0.26f);

        // 7b) 생존 기지 길(손님 길보다 옅게): 북쪽 길(y 10~46, 항상 통행) = 다져진 띠 + 바퀴 자국 2줄,
        //     가운데 길(y −210~−180)·통로(x 236~256)는 구역이 열려야 다니는 "예정 길"이라 더 옅은 띠 + 가는 점선 한 줄
        float nY0 = WorldMap.NORTH_Y, nY1 = 46f, nMid = (nY0 + nY1) / 2f;
        p.RoundRect(14f, nY0 + 1f, 372f, nY1 - nY0 - 2f, 14f, C(255, 236, 214), 0.10f);
        foreach (float o in new[] { -8f, 8f })
        {
            var pts = new List<Vector2>();
            for (float x = 20f; x <= 380f; x += 20f) pts.Add(new Vector2(x, nMid + o + Mathf.Sin(x / 60f) * 1.5f));
            p.Dashed(pts, 4f, 16f, 9f, track, 0.18f);
        }
        float mY0 = WorldMap.MID_Y0, mY1 = WorldMap.MID_Y1, mMid = (mY0 + mY1) / 2f;
        p.RoundRect(16f, mY0 + 2f, 364f, mY1 - mY0 - 4f, 12f, C(255, 236, 214), 0.07f);
        p.Dashed(new List<Vector2> { new Vector2(24f, mMid), new Vector2(372f, mMid) }, 3f, 12f, 12f, track, 0.13f);
        p.RoundRect(238f, GameConfig.Y_MIN + 12f, 16f, nY0 - GameConfig.Y_MIN - 12f, 7f, C(255, 236, 214), 0.07f);
        p.Dashed(new List<Vector2> { new Vector2(246f, nY0 - 4f), new Vector2(246f, mY1 + 4f) }, 3f, 12f, 12f, track, 0.13f);
        p.Dashed(new List<Vector2> { new Vector2(246f, mY0 - 4f), new Vector2(246f, GameConfig.Y_MIN + 16f) }, 3f, 12f, 12f, track, 0.13f);

        // 8) 줄 서는 자리 발자국: QUEUE_X, y 584부터 46 간격 6자리, 발 두 개(살짝 벌어진 타원)
        for (int i = 0; i < GameConfig.QUEUE_MAX; i++)
        {
            var q = WorldMap.QueuePos(i);
            p.Ellipse(q.x - 5.5f, q.y + 1f, 2.4f, 4.2f, -0.18f, C(70, 22, 8), 0.22f);
            p.Ellipse(q.x + 5.5f, q.y - 1f, 2.4f, 4.2f, 0.18f, C(70, 22, 8), 0.22f);
        }

        // 9) 착륙장: 원 r58 흰 .14 채움 + 테두리 .55 폭4, 안쪽 r34 폭3, "H" 폭4 .5
        p.Disc(E.x, E.y, 58f, Color.white, 0.14f);
        p.Ring(E.x, E.y, 58f, 4f, Color.white, 0.55f);
        p.Ring(E.x, E.y, 34f, 3f, Color.white, 0.55f);
        p.Line(E.x - 12f, E.y - 14f, E.x - 12f, E.y + 14f, 4f, Color.white, 0.5f);
        p.Line(E.x + 12f, E.y - 14f, E.x + 12f, E.y + 14f, 4f, Color.white, 0.5f);
        p.Line(E.x - 12f, E.y, E.x + 12f, E.y, 4f, Color.white, 0.5f);

        var tex = p.ToTexture(false, true);
        Debug.Log("GroundPainter: 지면 " + p.TW + "×" + p.TH + " " + sw.ElapsedMilliseconds + "ms");
        return tex;
    }

    // =====================================================================
    // 구역 바닥(원본 floorTex 473~480행): 색 면 + 26 타일 명암·격자 + 얼룩 + 긁힘/서리 균열 + 가장자리 어둡게 + 점선 테두리 + 외곽선
    // =====================================================================
    public static Texture2D PaintFloor(WorldMap.Zone z)
    {
        float w = z.x1 - z.x0, h = z.y1 - z.y0;
        bool ice = z.key.StartsWith("ice"), shop = z.key == "shop";
        var p = new GroundPainter(0f, 0f, w, h, 2f, 31 + z.key.Length * 7 + (int)z.x0, z.floor);
        const float ts = 26f;
        p.Cells(ts, 0.09f, Color.white, Color.black, 0f, 0f, w, h);
        for (float x = 0; x <= w + 0.1f; x += ts) p.Line(x, 0f, x, h, 1.2f, Color.black, 0.09f);
        for (float y = 0; y <= h + 0.1f; y += ts) p.Line(0f, y, w, y, 1.2f, Color.black, 0.09f);
        int nBlot = Mathf.Max(6, Mathf.RoundToInt(w * h / 5000f));
        for (int i = 0; i < nBlot; i++)
            p.Ellipse(p.Rn() * w, p.Rn() * h, 12f + p.Rn() * 30f, 7f + p.Rn() * 16f, p.Rn() * 3f, ice ? Color.white : C(120, 50, 20), ice ? 0.12f + p.Rn() * 0.12f : 0.05f + p.Rn() * 0.07f);
        if (ice)
        {   // 서리 균열: 흰 꺾은선
            for (int i = 0; i < 12; i++)
            {
                float x = p.Rn() * w, y = p.Rn() * h; var pts = new List<Vector2> { new Vector2(x, y) };
                for (int k = 0; k < 3; k++) { x += (p.Rn() - 0.5f) * 44f; y += (p.Rn() - 0.5f) * 44f; pts.Add(new Vector2(x, y)); }
                p.Polyline(pts, 1.5f, Color.white, 0.55f);
            }
        }
        else
        {   // 긁힘: 어두운 꺾은선
            for (int i = 0; i < 8; i++)
            {
                float x = p.Rn() * w, y = p.Rn() * h; var pts = new List<Vector2> { new Vector2(x, y) };
                for (int k = 0; k < 4; k++) { x += (p.Rn() - 0.5f) * 30f; y += (p.Rn() - 0.5f) * 30f; pts.Add(new Vector2(x, y)); }
                p.Polyline(pts, 1f, C(60, 20, 8), 0.14f);
            }
        }
        p.Grain(0.03f);
        p.EdgeShade(14f, 0.12f, C(40, 15, 5));
        // 점선 테두리: 매장은 주황(원본), 얼음밭은 흰색, 그 외는 갈색
        Color dc = shop ? C(240, 122, 58) : ice ? Color.white : C(120, 60, 30);
        p.Dashed(RoundRectPath(6f, 6f, w - 12f, h - 12f, 4f, 3), 3f, 10f, 6f, dc, shop ? 0.6f : ice ? 0.5f : 0.4f, true);
        p.Polyline(RoundRectPath(1.5f, 1.5f, w - 3f, h - 3f, 2f, 2), 3f, Color.black, 0.16f, true);
        return p.ToTexture(false);
    }

    // 잠긴 구역 윤곽(원본 buildDash 483행): 구역보다 12 큰 투명 판에 어두운 둥근 면(.09) + 크림색 점선(.65, 폭3, [12,9])
    public static Texture2D PaintDash(WorldMap.Zone z)
    {
        float w = z.x1 - z.x0 + 12f, h = z.y1 - z.y0 + 12f;
        var p = new GroundPainter(0f, 0f, w, h, 2f, 5, null);
        p.RoundRect(6f, 6f, w - 12f, h - 12f, 10f, C(40, 10, 0), 0.09f);
        p.Dashed(RoundRectPath(8f, 8f, w - 16f, h - 16f, 10f), 3f, 12f, 9f, C(255, 240, 220), 0.65f, true);
        return p.ToTexture(true);
    }

    // 북쪽 개척 예정지(잠긴 생존 기지 구역): 어두운 면 없이 옅은 크림색 점선 + 모서리 ㄱ자 측량 표시만. 차례가 오면 PaintDash 윤곽으로 바뀐다
    public static Texture2D PaintSurvey(WorldMap.Zone z)
    {
        float w = z.x1 - z.x0 + 12f, h = z.y1 - z.y0 + 12f;
        var p = new GroundPainter(0f, 0f, w, h, 2f, 7, null);
        var cream = C(255, 240, 220);
        p.Dashed(RoundRectPath(8f, 8f, w - 16f, h - 16f, 6f), 2.5f, 6f, 12f, cream, 0.34f, true);
        const float L = 18f, m = 8f;
        foreach (var c in new[] { new Vector4(m, m, 1, 1), new Vector4(w - m, m, -1, 1), new Vector4(m, h - m, 1, -1), new Vector4(w - m, h - m, -1, -1) })
            p.Polyline(new List<Vector2> { new Vector2(c.x + c.z * L, c.y), new Vector2(c.x, c.y), new Vector2(c.x, c.y + c.w * L) }, 3f, cream, 0.6f);
        return p.ToTexture(true);
    }

    // =====================================================================
    // 오브젝트: 위를 보는 Quad(회전 X=90, 가로 = 논리 x, 세로 = 북쪽). 그림자는 받기만 한다.
    // =====================================================================
    static Material baseMat, transMat;
    static Material TexMat(Texture2D t, bool trans)
    {
        Material src;
        if (trans) { if (transMat == null) transMat = Resources.Load<Material>("Mats/Trans"); src = transMat; }
        else { if (baseMat == null) baseMat = Resources.Load<Material>("Mats/Base"); src = baseMat; }
        var m = src != null ? new Material(src) : new Material(Shader.Find(trans ? "Legacy Shaders/Transparent/Diffuse" : "Standard"));
        m.color = Color.white; m.mainTexture = t;
        if (!trans) m.SetFloat("_Glossiness", 0.02f);
        return m;
    }

    public static GameObject Quad(Transform parent, string name, Vector3 pos, float wL, float hL, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name;
        var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(wL * GameConfig.K, hL * GameConfig.K, 1f);
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = true;
        return go;
    }

    // §6.4 테라포밍 땅색 훅: 지면 재질(TerraformManager가 terra에 따라 color를 곱한다). 씬을 다시 조립하면 새 재질로 바뀐다
    public static Material GroundMaterial;
    public static GameObject GroundQuad(Transform parent)
    {
        GroundMaterial = TexMat(PaintGround(), false);
        return Quad(parent, "ground", WorldMap.W2U(GX0 + GW / 2f, GY0 + GH / 2f, 0f), GW, GH, GroundMaterial);
    }

    public static GameObject FloorQuad(Transform parent, WorldMap.Zone z, float hL)
        => Quad(parent, "floor", WorldMap.W2U((z.x0 + z.x1) / 2f, (z.y0 + z.y1) / 2f, hL), z.x1 - z.x0, z.y1 - z.y0, TexMat(PaintFloor(z), false));

    public static GameObject DashQuad(Transform parent, WorldMap.Zone z, float hL, bool survey = false)
    {
        var go = Quad(parent, survey ? "survey" : "outline", WorldMap.W2U((z.x0 + z.x1) / 2f, (z.y0 + z.y1) / 2f, hL), z.x1 - z.x0 + 12f, z.y1 - z.y0 + 12f, TexMat(survey ? PaintSurvey(z) : PaintDash(z), true));
        go.GetComponent<Renderer>().receiveShadows = false;
        return go;
    }
}

using System.Collections.Generic;
using UnityEngine;

// 저폴리 장난감 메시 빌더(정점 색 → Shaders/MarsVColor): 상자·깎은 상자·원뿔대·울퉁불퉁 덩어리를 한 메시로 합친다.
// 물건(병·산소통·자루·감자 상자·돌·얼음)·얼음 더미·나무·돌을 프리미티브 여러 개 대신 메시 하나 + 재질 하나로 그려
// 드로우콜을 늘리지 않고(같은 메시끼리는 인스턴싱), 얇은 판을 붙이는 대신 두툼한 덩어리로 모양을 낸다(jake 10/25 "판떼기만 붙여 둔 것 같다").
// 단위: 논리(Build 에서 ×K). 좌표는 Unity 로컬 그대로(x 동·y 높이·z 북). 조각은 볼록이라 조각 중심 기준으로 감김을 맞춘다.
public class ToyMesh
{
    readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
    readonly List<Color> c = new List<Color>();
    readonly List<int> t = new List<int>();
    Matrix4x4 xf = Matrix4x4.identity;

    // 다음 조각의 위치·회전(논리 단위)
    public ToyMesh At(Vector3 pos, Vector3 euler) { xf = Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one); return this; }
    public ToyMesh At(Vector3 pos) => At(pos, Vector3.zero);
    public ToyMesh At(float x, float y, float z, float ry = 0f) => At(new Vector3(x, y, z), new Vector3(0, ry, 0));
    public ToyMesh At(Vector3 pos, Quaternion q) { xf = Matrix4x4.TRS(pos, q, Vector3.one); return this; }
    // 기계 좌표(dx 동·dy 남(카메라 쪽)·h 높이, MachineKit.L 과 같은 규약)로 다음 조각 위치
    public ToyMesh AtM(float dx, float dy, float h, float ry = 0f) => At(new Vector3(dx, h, -dy), new Vector3(0, ry, 0));

    // 둥근 모서리 상자(가운데가 원점): 윗면 부채꼴 + 옆면 띠(모서리는 매끈한 법선). 십자 상자 둘 + 원기둥 넷 대신 겹침 없는 한 덩어리
    //  r 모서리 반지름, seg 모서리 하나의 토막 수, top 윗면 색, sideTop 옆면 위쪽 색(아래 side 에서 번지는 그라데이션)
    public ToyMesh RBox(Vector3 size, float r, Color side, Color? top = null, Color? sideTop = null, int seg = 4, bool bottom = false)
    {
        Color tc = top ?? side, st = sideTop ?? side;
        float hx = size.x / 2f, hy = size.y / 2f, hz = size.z / 2f;
        r = Mathf.Clamp(r, 0.01f, Mathf.Min(hx, hz) - 0.01f);
        var pts = new List<Vector3>(); var nrm = new List<Vector3>();
        float[] cx = { hx - r, -hx + r, -hx + r, hx - r }, cz = { hz - r, hz - r, -hz + r, -hz + r };
        for (int k = 0; k < 4; k++)
            for (int s = 0; s <= seg; s++)
            {
                float a = (k * 90f + s * 90f / seg) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                pts.Add(new Vector3(cx[k], 0, cz[k]) + d * r); nrm.Add(d);
            }
        var o = Vector3.zero; var up = Vector3.up * hy; int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector3 a0 = pts[i] - up, a1 = pts[j] - up, b0 = pts[i] + up, b1 = pts[j] + up;
            Tri(a0, b0, b1, side, st, st, o, nrm[i], nrm[i], nrm[j]);
            Tri(a0, b1, a1, side, st, side, o, nrm[i], nrm[j], nrm[j]);
            Tri(up, b0, b1, tc, tc, tc, o);
            if (bottom) Tri(-up, a0, a1, side, side, side, o);
        }
        return this;
    }

    // 매끈한 반 타원 돔(바닥 중심이 원점, 가로 반지름 rx·세로 rz·높이 h). 색은 아래 side → 꼭대기 top
    public ToyMesh Dome(float rx, float rz, float h, Color side, Color top, int sides = 20, int rings = 5)
        => Dome(rx, rz, h, (i, k) => Color.Lerp(side, top, k / (float)rings), sides, rings);
    // 정점마다 색을 정하는 돔: col(i 둘레 칸 0..sides, k 위도 칸 0..rings). 둘레 각 = i/sides·360°(0° = 동, 90° = 북), 위도 0 = 바닥 테
    public ToyMesh Dome(float rx, float rz, float h, System.Func<int, int, Color> col, int sides, int rings)
    {
        var o = Vector3.zero;
        Vector3 Pt(int i, int k) { float p = k * Mathf.PI / 2f / rings, a = i * Mathf.PI * 2f / sides; return new Vector3(Mathf.Cos(a) * Mathf.Cos(p) * rx, Mathf.Sin(p) * h, Mathf.Sin(a) * Mathf.Cos(p) * rz); }
        Vector3 Nm(Vector3 p) => new Vector3(p.x / (rx * rx), p.y / (h * h), p.z / (rz * rz)).normalized;
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                Vector3 a0 = Pt(i, k), a1 = Pt(i + 1, k), b0 = Pt(i, k + 1), b1 = Pt(i + 1, k + 1);
                if (k < rings - 1) Tri(a0, b0, b1, col(i, k), col(i, k + 1), col(i + 1, k + 1), o, Nm(a0), Nm(b0), Nm(b1));
                Tri(a0, b1, a1, col(i, k), col(i + 1, k + 1), col(i + 1, k), o, Nm(a0), Nm(b1), Nm(a1));
            }
        return this;
    }

    // 매끈한 공(가운데가 원점): 위아래 돔 둘
    public ToyMesh Ball(float r, Color c, Color? top = null, int sides = 14, int rings = 4)
    {
        var b = xf;
        Dome(r, r, r, c, top ?? c, sides, rings);
        xf = b * Matrix4x4.Rotate(Quaternion.Euler(180, 0, 0)); Dome(r, r, r, c, c, sides, rings);
        xf = b; return this;
    }

    // 관(a → b, 반지름 r): 방향에 맞춰 눕힌 원기둥 + 양 끝 마디(조금 굵은 고리). 좌표는 Unity 로컬(논리 단위)
    public ToyMesh Pipe(Vector3 a, Vector3 b, float r, Color c, bool joints = true, int sides = 12)
    {
        var d = b - a; float len = d.magnitude; if (len < 0.01f) return this;
        var q = Quaternion.FromToRotation(Vector3.up, d / len);
        At(a, q).Cyl(r, r, len, sides, c, c, true, null, true);
        if (joints)
        {
            At(a, q).Cyl(r + 0.9f, r + 0.9f, 1.6f, sides, c, c, true, null, true);
            At(b - d / len * 1.6f, q).Cyl(r + 0.9f, r + 0.9f, 1.6f, sides, c, c, true, null, true);
        }
        return this;
    }
    // KayKit 모델(KK.Load, 바닥 중심 원점) 한 덩어리를 지금 자리(At·AtM)에 붙인다. sc: KayKit 1단위 = 논리 몇(축별), re: 정점 색 바꾸기(기본 KK.Warm).
    // 축별 스케일이라 법선은 역스케일로 다시 맞춘다. 감김은 굽는 단계에서 Unity 기준으로 맞춰 두었으므로 Tri 를 거치지 않고 그대로 붙인다
    public ToyMesh Kk(string name, Vector3 sc, System.Func<Color, Color> re = null)
    {
        var md = KK.Load(name); if (md == null) return this;
        re = re ?? KK.Warm;
        int b = v.Count; var inv = new Vector3(1f / sc.x, 1f / sc.y, 1f / sc.z);
        for (int i = 0; i < md.p.Length; i++)
        {
            v.Add(xf.MultiplyPoint3x4(Vector3.Scale(md.p[i], sc)));
            n.Add(xf.MultiplyVector(Vector3.Scale(md.n[i], inv)).normalized);
            c.Add(re(md.c[i]));
        }
        for (int i = 0; i < md.t.Length; i++) t.Add(b + md.t[i]);
        return this;
    }
    public ToyMesh Kk(string name, float sc, System.Func<Color, Color> re = null) => Kk(name, Vector3.one * sc, re);
    // 자리에 따라 색 바꾸기(KayKit 원래 좌표 p 기준: 같은 색이라도 높이로 지붕·띠를 가를 때). keep 이 있으면 그 정점만 든 삼각형만 붙인다(태양판에서 판만 떼기 등)
    //  recenter: 남긴 정점들의 경계 중심을 원점으로 옮긴 뒤 놓는다(떼어 낸 판을 제자리 회전시킬 때)
    public ToyMesh KkP(string name, Vector3 sc, System.Func<Vector3, Color, Color> re, System.Func<Vector3, Color, bool> keep = null, bool recenter = false)
    {
        var md = KK.Load(name); if (md == null) return this;
        var ctr = Vector3.zero;
        if (recenter)
        {
            Vector3 lo = Vector3.one * 1e9f, hi = -lo;
            for (int i = 0; i < md.p.Length; i++) if (keep == null || keep(md.p[i], md.c[i])) { lo = Vector3.Min(lo, md.p[i]); hi = Vector3.Max(hi, md.p[i]); }
            ctr = (lo + hi) / 2f;
        }
        int b = v.Count; var inv = new Vector3(1f / sc.x, 1f / sc.y, 1f / sc.z);
        for (int i = 0; i < md.p.Length; i++)
        {
            v.Add(xf.MultiplyPoint3x4(Vector3.Scale(md.p[i] - ctr, sc)));
            n.Add(xf.MultiplyVector(Vector3.Scale(md.n[i], inv)).normalized);
            c.Add(re != null ? re(md.p[i], md.c[i]) : KK.Warm(md.c[i]));
        }
        for (int i = 0; i < md.t.Length; i += 3)
        {
            int a = md.t[i], d = md.t[i + 1], e = md.t[i + 2];
            if (keep != null && !(keep(md.p[a], md.c[a]) && keep(md.p[d], md.c[d]) && keep(md.p[e], md.c[e]))) continue;
            t.Add(b + a); t.Add(b + d); t.Add(b + e);
        }
        return this;
    }

    // 기계 좌표(dx, dy, h) 두 점 사이 관
    public ToyMesh PipeM(float ax, float ay, float ah, float bx, float by, float bh, float r, Color c, bool joints = true)
        => Pipe(new Vector3(ax, ah, -ay), new Vector3(bx, bh, -by), r, c, joints);

    // 삼각형 하나. 법선은 면 법선(nA 등이 주어지면 그 값, 매끈한 옆면용). 조각 중심 o 에서 바깥을 보게 감김을 뒤집는다
    void Tri(Vector3 a, Vector3 b, Vector3 d, Color ca, Color cb, Color cd, Vector3 o, Vector3? na = null, Vector3? nb = null, Vector3? nd = null)
    {
        a = xf.MultiplyPoint3x4(a); b = xf.MultiplyPoint3x4(b); d = xf.MultiplyPoint3x4(d); o = xf.MultiplyPoint3x4(o);
        var fn = Vector3.Cross(b - a, d - a);
        if (fn.sqrMagnitude < 1e-10f) return;
        if (Vector3.Dot(fn, (a + b + d) / 3f - o) < 0f)
        {
            var tv = b; b = d; d = tv; var tc = cb; cb = cd; cd = tc; var tn = nb; nb = nd; nd = tn; fn = -fn;
        }
        fn.Normalize();
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(d);
        n.Add(na.HasValue ? xf.MultiplyVector(na.Value).normalized : fn);
        n.Add(nb.HasValue ? xf.MultiplyVector(nb.Value).normalized : fn);
        n.Add(nd.HasValue ? xf.MultiplyVector(nd.Value).normalized : fn);
        c.Add(ca); c.Add(cb); c.Add(cd);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }
    void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color c0, Color c1, Color c2, Color c3, Vector3 o)
    {
        Tri(p0, p1, p2, c0, c1, c2, o); Tri(p0, p2, p3, c0, c2, c3, o);
    }

    // 상자(가운데가 원점). top: 윗면 색(없으면 col)
    public ToyMesh Box(Vector3 size, Color col, Color? top = null)
    {
        var h = size / 2f; var tc = top ?? col;
        Vector3 P(int x, int y, int z) => new Vector3(x * h.x, y * h.y, z * h.z);
        var o = Vector3.zero;
        Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), tc, tc, tc, tc, o);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), col, col, col, col, o);
        Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), col, col, col, col, o);
        Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), col, col, col, col, o);
        Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), col, col, col, col, o);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), col, col, col, col, o);
        return this;
    }

    // 얼음 윗면 반사 줄(10/26): 방금 놓은 Bevel(같은 At)의 윗면에 사선 흰 줄 둘(굵은 것 + 가는 것)을 살짝 띄워 얹는다.
    //  불투명 정점 색이라 정렬 깜빡임이 없고, 0.15 띄운 얇은 판이라 외곽선이 생기지 않는다. 투명 껍질 대신 윗면에 빛이 비친 느낌만
    public ToyMesh Glint(Vector3 size, Color c)
    {
        var b0 = xf; float y = size.y / 2f + 0.15f, L = Mathf.Min(size.x, size.z) * 0.62f, w = Mathf.Max(0.5f, size.x * 0.1f);
        xf = b0 * Matrix4x4.TRS(new Vector3(-size.x * 0.12f, y, size.z * 0.04f), Quaternion.Euler(0, 38, 0), Vector3.one); Box(new Vector3(w, 0.1f, L), c);
        xf = b0 * Matrix4x4.TRS(new Vector3(size.x * 0.1f, y, -size.z * 0.04f), Quaternion.Euler(0, 38, 0), Vector3.one); Box(new Vector3(w * 0.45f, 0.1f, L * 0.62f), c);
        xf = b0; return this;
    }

    // 모서리를 깎은 상자(가운데가 원점, 깎은 폭 b). 큰 면은 가운데 core → 둘레 face 로 번지는 그라데이션(얼음 속이 진해 보이게),
    // 깎은 모서리는 edge(위쪽 절반만, 아래 모서리는 face 색), 윗면은 top/topCore
    public ToyMesh Bevel(Vector3 size, float b, Color face, Color core, Color edge, Color top, Color topCore)
    {
        var h = size / 2f; b = Mathf.Min(b, h.x * 0.9f, h.y * 0.9f, h.z * 0.9f);
        // axis: 0 → x 가 면까지 나간 점, 1 → y, 2 → z
        Vector3 P(int sx, int sy, int sz, int axis) => new Vector3(
            sx * (axis == 0 ? h.x : h.x - b), sy * (axis == 1 ? h.y : h.y - b), sz * (axis == 2 ? h.z : h.z - b));
        var o = Vector3.zero;
        // 큰 면 6: 가운데 점에서 부채꼴
        for (int ax = 0; ax < 3; ax++)
            for (int s = -1; s <= 1; s += 2)
            {
                var ctr = Vector3.zero; ctr[ax] = s * h[ax];
                bool isTop = ax == 1 && s > 0;
                Color cf = isTop ? top : face, cc = isTop ? topCore : core;
                var ring = new Vector3[4];
                int[,] uv = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
                for (int k = 0; k < 4; k++)
                {
                    int u = uv[k, 0], w = uv[k, 1];
                    ring[k] = ax == 0 ? P(s, u, w, 0) : ax == 1 ? P(u, s, w, 1) : P(u, w, s, 2);
                }
                for (int k = 0; k < 4; k++) Tri(ctr, ring[k], ring[(k + 1) % 4], cc, cf, cf, o);
            }
        // 모서리 띠 12
        for (int s1 = -1; s1 <= 1; s1 += 2)
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                // z 방향 띠(x·y 면 사이)
                EdgeQuad(P(s1, s2, -1, 0), P(s1, s2, 1, 0), P(s1, s2, 1, 1), P(s1, s2, -1, 1), s2 > 0 ? edge : face, o);
                // x 방향 띠(y·z 면 사이)
                EdgeQuad(P(-1, s1, s2, 1), P(1, s1, s2, 1), P(1, s1, s2, 2), P(-1, s1, s2, 2), s1 > 0 ? edge : face, o);
                // y 방향 띠(x·z 면 사이): 세로 모서리는 위쪽 끝만 밝게
                var a0 = P(s1, -1, s2, 0); var a1 = P(s1, 1, s2, 0); var b1 = P(s1, 1, s2, 2); var b0 = P(s1, -1, s2, 2);
                Quad(a0, a1, b1, b0, face, Color.Lerp(face, edge, 0.6f), Color.Lerp(face, edge, 0.6f), face, o);
            }
        // 꼭짓점 삼각형 8
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var ce = sy > 0 ? edge : face;
                    Tri(P(sx, sy, sz, 0), P(sx, sy, sz, 1), P(sx, sy, sz, 2), ce, ce, ce, o);
                }
        return this;
    }
    void EdgeQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color col, Vector3 o) => Quad(p0, p1, p2, p3, col, col, col, col, o);

    // 원뿔대(바닥 중심이 원점, 높이 h, 바닥 반지름 r0·윗 반지름 r1). smooth 면 옆면 법선을 둥글게. sideTop: 옆면 위쪽 색(그라데이션)
    public ToyMesh Cyl(float r0, float r1, float h, int sides, Color side, Color top, bool smooth = true, Color? sideTop = null, bool bottom = false)
    {
        var st = sideTop ?? side; var o = new Vector3(0, h / 2f, 0);
        float slope = (r0 - r1) / Mathf.Max(0.001f, h);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 p0 = d0 * r0, p1 = d1 * r0, q0 = d0 * r1 + Vector3.up * h, q1 = d1 * r1 + Vector3.up * h;
            if (smooth)
            {
                var n0 = (d0 + Vector3.up * slope).normalized; var n1 = (d1 + Vector3.up * slope).normalized;
                Tri(p0, q0, q1, side, st, st, o, n0, n0, n1); Tri(p0, q1, p1, side, st, side, o, n0, n1, n1);
            }
            else Quad(p0, q0, q1, p1, side, st, st, side, o);
            if (r1 > 0.001f) Tri(Vector3.up * h, q0, q1, top, top, top, o);
            if (bottom && r0 > 0.001f) Tri(Vector3.zero, p0, p1, side, side, side, o);
        }
        return this;
    }

    // 고리 띠(바닥 중심이 원점): 원기둥 옆면만 살짝 두툼하게(병 라벨·탱크 띠). 윗·아랫면 테두리 포함
    public ToyMesh Band(float r, float h, int sides, Color col) => Cyl(r, r, h, sides, col, col, true, null, true);

    // 두툼한 고리(속이 빈 원통 벽: 바깥 r1·안 r0, 바닥이 원점, 높이 h). 수조 벽·탱크 테처럼 얇은 관 대신 두께가 보이게
    public ToyMesh Ring(float r0, float r1, float h, int sides, Color side, Color top, Color? inner = null)
    {
        var ic = inner ?? side;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            var up = Vector3.up * h;
            // 바깥 벽(바깥을 보게: 기준점을 축 위로), 안 벽(축을 보게: 기준점을 바깥 멀리), 윗면(위를 보게)
            Tri(d0 * r1, d0 * r1 + up, d1 * r1 + up, side, side, side, up / 2f, d0, d0, d1);
            Tri(d0 * r1, d1 * r1 + up, d1 * r1, side, side, side, up / 2f, d0, d1, d1);
            var far = (d0 + d1) * r1 * 4f + up / 2f;
            Tri(d0 * r0, d0 * r0 + up, d1 * r0 + up, ic, ic, ic, far, -d0, -d0, -d1);
            Tri(d0 * r0, d1 * r0 + up, d1 * r0, ic, ic, ic, far, -d0, -d1, -d1);
            Tri(d0 * r0 + up, d0 * r1 + up, d1 * r1 + up, top, top, top, up * 0.5f);
            Tri(d0 * r0 + up, d1 * r1 + up, d1 * r0 + up, top, top, top, up * 0.5f);
        }
        return this;
    }

    // 울퉁불퉁 덩어리(정이십면체 → sub 번 쪼갬, 꼭짓점을 jitter 만큼 흔듦, 면 평면 셰이딩). radii: 반지름(논리)
    //  색은 면 방향으로: 위를 보면 top, 옆은 mid(seed 따라 alt 와 섞임), 아래는 low. flat>0 이면 바닥을 -radii.y*flat 에서 자른 듯 평평하게
    public ToyMesh Blob(Vector3 radii, int sub, int seed, float jitter, Color top, Color mid, Color low, Color? alt = null, float flat = 0f)
    {
        var (pv, pf) = Ico(sub);
        var rnd = new System.Random(seed);
        var pts = new Vector3[pv.Count];
        for (int i = 0; i < pv.Count; i++)
        {
            float j = 1f + jitter * ((float)rnd.NextDouble() * 2f - 1f);
            var p = Vector3.Scale(pv[i] * j, radii);
            if (flat > 0f) p.y = Mathf.Max(p.y, -radii.y * flat);
            pts[i] = p;
        }
        var ac = alt ?? mid;
        for (int f = 0; f < pf.Count; f += 3)
        {
            Vector3 a = pts[pf[f]], b = pts[pf[f + 1]], d = pts[pf[f + 2]];
            var fn = Vector3.Cross(b - a, d - a).normalized; if (Vector3.Dot(fn, (a + b + d) / 3f) < 0) fn = -fn;
            Color col = fn.y > 0.55f ? top : fn.y < -0.35f ? low : ((f / 3) * 7 + seed) % 3 == 0 ? ac : mid;
            Tri(a, b, d, col, col, col, Vector3.zero);
        }
        return this;
    }

    static readonly Dictionary<int, (List<Vector3>, List<int>)> icoCache = new Dictionary<int, (List<Vector3>, List<int>)>();
    static (List<Vector3>, List<int>) Ico(int sub)
    {
        if (icoCache.TryGetValue(sub, out var hit)) return hit;
        float p = (1f + Mathf.Sqrt(5f)) / 2f;
        var vs = new List<Vector3>
        {
            new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
            new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
            new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
        };
        for (int i = 0; i < vs.Count; i++) vs[i] = vs[i].normalized;
        var fs = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                                 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        for (int s = 0; s < sub; s++)
        {
            var mid = new Dictionary<long, int>(); var nf = new List<int>();
            int M(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out var idx)) return idx;
                vs.Add(((vs[a] + vs[b]) / 2f).normalized); mid[key] = vs.Count - 1; return vs.Count - 1;
            }
            for (int f = 0; f < fs.Count; f += 3)
            {
                int a = fs[f], b = fs[f + 1], d = fs[f + 2], ab = M(a, b), bd = M(b, d), da = M(d, a);
                nf.AddRange(new[] { a, ab, da, b, bd, ab, d, da, bd, ab, bd, da });
            }
            fs = nf;
        }
        icoCache[sub] = (vs, fs);
        return (vs, fs);
    }

    // 저폴리 공·타원체(가운데가 원점, 반지름 radii, 캐릭터 r7): 면마다 평면 셰이딩이라 KayKit 처럼 면이 살짝 각져 보인다.
    //  색은 높이 따라 아래 low → 위 high(정점 보간이라 면은 각져도 색은 부드럽게 번진다). 앞(+z)에 면 하나가 정면으로 오게 둘레를 돌려 둔다
    //  facet: 법선을 면 법선 쪽으로 섞는 비율(1 이면 완전 평면 셰이딩 = 구겨진 종이처럼 보였다, 0 이면 매끈한 구). 기본 0.35 = 실루엣은 각지고 면은 살짝만
    public ToyMesh Gem(Vector3 radii, int sides, int rings, Color low, Color high, float facet = 0.35f) => GemCore(radii, sides, rings, low, high, facet, null, 0f, low);

    // 같은 칸 나눔의 공 위에 덧씌운 껍질 조각(헬멧 바이저 r7): mask(i 둘레 칸, k 위도 칸 0 = 남극)인 칸만 lift 만큼 띄워 그리고,
    //  가장자리는 아래 공 면까지 벽(wall 색)으로 막는다. 바이저 테두리가 헬멧 면 모서리를 그대로 따라가 깔끔한 각진 창이 된다(따로 만든 타원을 박으면 경계가 톱니로 보였다).
    //  i 칸 중심 각 = 90° + (i − sides/4)·360°/sides 라 i = sides/4 가 정면(+z)
    public ToyMesh GemShell(Vector3 radii, int sides, int rings, System.Func<int, int, bool> mask, float lift, Color low, Color high, Color wall, float facet = 0.35f)
        => GemCore(radii, sides, rings, low, high, facet, mask, lift, wall);

    ToyMesh GemCore(Vector3 radii, int sides, int rings, Color low, Color high, float facet, System.Func<int, int, bool> mask, float lift, Color wall)
    {
        var o = Vector3.zero;
        float rot = 90f - (sides / 4 + 0.5f) * 360f / sides;
        var ro = radii + Vector3.one * lift;
        Vector3 Pt(int i, int k, Vector3 r)
        {
            float p = -Mathf.PI / 2f + k * Mathf.PI / rings, a = (i * 360f / sides + rot) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * Mathf.Cos(p) * r.x, Mathf.Sin(p) * r.y, Mathf.Sin(a) * Mathf.Cos(p) * r.z);
        }
        Color C(Vector3 p) => Color.Lerp(low, high, p.y / (2f * ro.y) + 0.5f);
        Vector3 Sm(Vector3 p) => new Vector3(p.x / (ro.x * ro.x), p.y / (ro.y * ro.y), p.z / (ro.z * ro.z)).normalized;
        bool In(int i, int k) => mask == null || (k >= 0 && k < rings && mask(((i % sides) + sides) % sides, k));
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                if (!In(i, k)) continue;
                Vector3 a0 = Pt(i, k, ro), a1 = Pt(i + 1, k, ro), b0 = Pt(i, k + 1, ro), b1 = Pt(i + 1, k + 1, ro);
                // 이 칸(사다리꼴, 한 평면)의 바깥 면 법선
                var fn = Vector3.Cross(b0 - a0, b1 - a0);              // 북극 칸은 b0 = b1 이라 다른 두 변으로
                if (fn.sqrMagnitude < 1e-10f) fn = Vector3.Cross(b1 - a0, a1 - a0);
                if (Vector3.Dot(fn, a0 + a1 + b0 + b1) < 0f) fn = -fn;
                fn.Normalize();
                Vector3 N(Vector3 p) => Vector3.Lerp(Sm(p), fn, facet).normalized;
                Tri(a0, b0, b1, C(a0), C(b0), C(b1), o, N(a0), N(b0), N(b1));
                Tri(a0, b1, a1, C(a0), C(b1), C(a1), o, N(a0), N(b1), N(a1));
                if (mask == null || lift <= 0f) continue;
                // 벽: 이웃 칸이 껍질이 아닌 변마다 바깥 변 → 안쪽(공 면) 변. 칸 가운데를 기준점으로 바깥(칸 밖)을 보게
                var ctr = (Pt(i, k, radii) + Pt(i + 1, k, radii) + Pt(i, k + 1, radii) + Pt(i + 1, k + 1, radii)) / 4f;
                void Wall(int i0, int k0, int i1, int k1)
                {
                    Vector3 p0 = Pt(i0, k0, ro), p1 = Pt(i1, k1, ro), q0 = Pt(i0, k0, radii), q1 = Pt(i1, k1, radii);
                    if ((p0 - p1).sqrMagnitude < 1e-8f) return;
                    Quad(q0, p0, p1, q1, wall, wall, wall, wall, ctr);
                }
                if (!In(i, k - 1)) Wall(i, k, i + 1, k);
                if (!In(i, k + 1)) Wall(i, k + 1, i + 1, k + 1);
                if (!In(i - 1, k)) Wall(i, k, i, k + 1);
                if (!In(i + 1, k)) Wall(i + 1, k, i + 1, k + 1);
            }
        return this;
    }

    // 모서리를 깎은 상자에 세로 그라데이션(캐릭터 r7): 아래 low → 위 high, 윗면만 top(없으면 high). KayKit 부품처럼 위가 밝고 아래가 어둡다
    public ToyMesh Chamfer(Vector3 size, float b, Color low, Color high, Color? top = null)
    {
        int s = v.Count;
        Bevel(size, b, low, low, low, low, low);
        var inv = xf.inverse; float hy = size.y / 2f;
        for (int i = s; i < v.Count; i++)
        {
            var lp = inv.MultiplyPoint3x4(v[i]);
            bool up = top.HasValue && lp.y > hy - 0.001f && inv.MultiplyVector(n[i]).y > 0.99f;
            c[i] = up ? top.Value : Color.Lerp(low, high, lp.y / size.y + 0.5f);
        }
        return this;
    }

    public Mesh Build(string name)
    {
        float k = GameConfig.K; var vk = new List<Vector3>(v.Count);
        foreach (var p in v) vk.Add(p * k);
        var m = new Mesh { name = name };
        if (vk.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(vk); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    // ---- 캐시·배치 ----
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
    public static Mesh Cached(string key, System.Func<ToyMesh, ToyMesh> build)
    {
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        m = build(new ToyMesh()).Build(key); cache[key] = m; return m;
    }
    // 정점 색 재질(매끈함·가장자리 밝기·제 색 발광별 공유, 인스턴싱 켬). 셰이더가 없으면 흰 Standard(색은 잃지만 모양은 보인다)
    static Shader _sh; static bool shTried;
    static readonly Dictionary<(float, float, float), Material> mats = new Dictionary<(float, float, float), Material>();
    public static Material Mat(float gloss = 0.1f, float rim = 0f, float self = 0f)
    {
        if (mats.TryGetValue((gloss, rim, self), out var m) && m != null) return m;
        if (!shTried) { shTried = true; _sh = Resources.Load<Shader>("Shaders/MarsVColor"); if (_sh != null && !_sh.isSupported) _sh = null; }
        if (_sh == null) m = new Material(Mats.Solid(Color.white));
        else { m = new Material(_sh) { enableInstancing = true }; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Rim", rim); m.SetFloat("_Self", self); }
        mats[(gloss, rim, self)] = m; return m;
    }
    public static GameObject Put(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}

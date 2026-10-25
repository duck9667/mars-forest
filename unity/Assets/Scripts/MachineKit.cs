using System;
using System.Collections.Generic;
using UnityEngine;

// 기계 6종 공통 문법(jake 10/25 "건물 디자인이 제대로 된 게 맞나"): 무엇이 들어가 무엇이 나오는지가 모양으로 읽히게.
//  받침판(강철) → 투입부(호퍼·탱크: 들어가는 재료가 보임) → 가공부(일할 때 움직임: 코일 발광·거품·회전·벨트) → 산출 받침대(앞쪽, 산출물이 쌓임)
//  색은 흰 기계 외피 + 산출 자원 색 하나만. 글자 라벨은 없다.
// 좌표: 그룹은 스테이션 중심 바닥. L(dx, dy, h) 의 dx 동(+)·dy 남(+, 카메라 쪽)·h 높이, 모두 논리 단위.
public static class MachineKit
{
    public static readonly Color SHELL = GameConfig.Hex("eee8de"), SHELL_D = GameConfig.Hex("c9c1b4"), STEEL = GameConfig.Hex("5d6470"), STEEL_D = GameConfig.Hex("3a3f4a");
    public static readonly Color PAD = GameConfig.Hex("d9c8b2"), PAD_TOP = GameConfig.Hex("e8dccb");
    public static readonly Color GLASS = new Color(0.86f, 0.96f, 1f, 0.3f);

    public static Vector3 L(float dx, float dy, float h) => new Vector3(dx * GameConfig.K, h * GameConfig.K, -dy * GameConfig.K);

    // 플레이스홀더(body/top 등)를 숨기고 스테이션 중심 그룹 + 받침판
    public static Transform Group(Transform view, WorldMap.Stn s, float w, float d, params string[] hide)
    {
        foreach (Transform c in view)
        {
            if (c.name == "body" || c.name == "top" || c.name == "models") c.gameObject.SetActive(false);
            foreach (var h in hide) if (c.name == h) c.gameObject.SetActive(false);
        }
        var g = new GameObject("machine").transform; g.SetParent(view, false);
        g.position = WorldMap.W2U(s.x, s.y, 0f);
        // 밝은 콘크리트 받침(어두운 강철판은 무거워 보였다, 목표 그림 docs/art): 둥근 모서리 판 + 한 단 밝은 윗단(얇은 판 대신 두께 1.2)
        RoundSlab(g, "plate", 0, 0, 0, w, d, 5, 9, PAD);
        RoundSlab(g, "plateTop", 0, 0, 5, w - 8, d - 8, 1.2f, 6, PAD_TOP);
        return g;
    }

    // 둥근 모서리 판(목표 그림의 받침·쟁반): 십자 상자 둘 + 모서리 원기둥 넷. 같은 높이로 맞닿아 외곽선은 바깥 둘레에만 생긴다
    //  dx·dy 중심, h0 바닥 높이, hh 두께, r 모서리 반지름(모두 논리 단위)
    public static Transform RoundSlab(Transform g, string name, float dx, float dy, float h0, float w, float d, float hh, float r, Color c)
    {
        var t = new GameObject(name).transform; t.SetParent(g, false); t.localPosition = L(dx, dy, h0);
        r = Mathf.Min(r, w / 2 - 0.5f, d / 2 - 0.5f);
        Prim.Box(t, "x", L(0, 0, hh / 2), new Vector3(w, hh, d - 2 * r), c);
        Prim.Box(t, "z", L(0, 0, hh / 2), new Vector3(w - 2 * r, hh, d), c);
        foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            Prim.Cyl(t, "corner", L(sx * (w / 2 - r), sy * (d / 2 - r), hh / 2), 2 * r, hh, c);
        return t;
    }

    // 사각 호퍼(위가 열린 통): 바닥 어둡게, 네 벽은 외피색, 윗테는 강조색
    public static Transform Hopper(Transform g, float dx, float dy, float w, float d, float h0, float h1, Color rim)
    {
        var t = new GameObject("hopper").transform; t.SetParent(g, false); t.localPosition = L(dx, dy, 0);
        float hh = h1 - h0, th = 3f;
        Prim.Box(t, "leg", L(0, 0, h0 / 2), new Vector3(w * 0.6f, h0, d * 0.6f), STEEL_D);
        Prim.Box(t, "floor", L(0, 0, h0 + 1), new Vector3(w, 2, d), STEEL_D);
        Prim.Box(t, "wN", L(0, -d / 2 + th / 2, h0 + hh / 2), new Vector3(w, hh, th), SHELL);
        Prim.Box(t, "wS", L(0, d / 2 - th / 2, h0 + hh * 0.35f), new Vector3(w, hh * 0.7f, th), SHELL);   // 앞벽은 낮게(안이 보이게)
        Prim.Box(t, "wW", L(-w / 2 + th / 2, 0, h0 + hh / 2), new Vector3(th, hh, d), SHELL);
        Prim.Box(t, "wE", L(w / 2 - th / 2, 0, h0 + hh / 2), new Vector3(th, hh, d), SHELL);
        Prim.Box(t, "rimN", L(0, -d / 2 + th / 2, h1 + 0.8f), new Vector3(w + 1, 1.6f, th + 1), rim);
        Prim.Box(t, "rimS", L(0, d / 2 - th / 2, h0 + hh * 0.7f + 0.8f), new Vector3(w + 1, 1.6f, th + 1), rim);
        return t;
    }

    // 유리 탱크 + 안의 액체(높이가 level 0~1 로 오르내림)
    public static Transform Tank(Transform g, float dx, float dy, float dia, float h0, float h, Color liquid, out Transform liq)
    {
        var t = new GameObject("tank").transform; t.SetParent(g, false); t.localPosition = L(dx, dy, 0);
        Prim.Cyl(t, "base", L(0, 0, h0 / 2), dia + 4, h0, SHELL_D);   // 밝은 받침(유리 너머 바닥이 어두우면 탱크가 회색 통으로 읽힌다)
        var l = Prim.Cyl(t, "liquid", L(0, 0, h0 + h / 2), dia - 3, h - 2, liquid); liq = l.transform;
        // 유리는 뚜껑 없는 관(닫힌 원기둥이면 윗면이 회색 원판으로 물을 덮었다), 위에서 물 높이가 그대로 보이게
        var gl = Tube(t, "glass", L(0, 0, h0), dia, h, GLASS); gl.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(GLASS); Prim.NoShadow(gl);
        // 유리 윗테·아랫테: 두툼한 고리(예전 홑겹 관 테는 얇은 띠로 읽혔다)
        ToyMesh.Put(t, "rim", ToyMesh.Cached("tankRim" + dia, m => m.Ring(dia / 2 - 1.2f, dia / 2 + 1.4f, 3f, 24, SHELL, GameConfig.Hex("fbf8f3"), SHELL_D)), ToyMesh.Mat(0.3f), L(0, 0, h0 + h - 2.2f));
        ToyMesh.Put(t, "rimLow", ToyMesh.Cached("tankRimLow" + dia, m => m.Ring(dia / 2 - 1.2f, dia / 2 + 1.4f, 3f, 24, SHELL_D, SHELL_D)), ToyMesh.Mat(0.3f), L(0, 0, h0));
        return t;
    }

    // 뚜껑 없는 원통 관(안팎 양면): 아랫면 중심이 pos, 지름 dia·높이 h(논리)
    static Mesh _tube;
    public static GameObject Tube(Transform parent, string name, Vector3 pos, float dia, float h, Color c)
    {
        if (_tube == null)
        {
            const int N = 28; var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int b = v.Count; float sg = side == 0 ? 1f : -1f;
                for (int i = 0; i <= N; i++)
                {
                    float a = i * Mathf.PI * 2 / N; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(d * 0.5f); v.Add(d * 0.5f + Vector3.up); n.Add(d * sg); n.Add(d * sg);
                }
                for (int i = 0; i < N; i++)
                {
                    int a0 = b + i * 2, a1 = a0 + 2;
                    if (side == 0) tri.AddRange(new[] { a0, a0 + 1, a1, a1, a0 + 1, a1 + 1 });
                    else tri.AddRange(new[] { a0, a1, a0 + 1, a1, a1 + 1, a0 + 1 });
                }
            }
            _tube = new Mesh { name = "tube" }; _tube.SetVertices(v); _tube.SetNormals(n); _tube.SetTriangles(tri, 0); _tube.RecalculateBounds();
        }
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = new Vector3(dia, h, dia) * GameConfig.K;
        go.AddComponent<MeshFilter>().sharedMesh = _tube;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Solid(c);
        return go;
    }

    // 축 정렬 파이프(가로: dx 또는 dy 방향, 세로: 높이). 끝 마디 포함
    public static void PipeX(Transform g, float x0, float x1, float dy, float h, float dia, Color c)
    {
        var p = Prim.Cyl(g, "pipe", L((x0 + x1) / 2, dy, h), dia, Mathf.Abs(x1 - x0), c); p.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Prim.Cyl(g, "joint", L(x0, dy, h), dia + 2, 3, c).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Prim.Cyl(g, "joint", L(x1, dy, h), dia + 2, 3, c).transform.localRotation = Quaternion.Euler(0, 0, 90);
    }
    public static void PipeY(Transform g, float dx, float y0, float y1, float h, float dia, Color c)
    {
        var p = Prim.Cyl(g, "pipe", L(dx, (y0 + y1) / 2, h), dia, Mathf.Abs(y1 - y0), c); p.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim.Cyl(g, "joint", L(dx, y1, h), dia + 2, 3, c).transform.localRotation = Quaternion.Euler(90, 0, 0);
    }
    public static void PipeUp(Transform g, float dx, float dy, float h0, float h1, float dia, Color c)
        => Prim.Cyl(g, "pipe", L(dx, dy, (h0 + h1) / 2), dia, h1 - h0, c);

    // 파이프 아치(목표 그림의 파란 관): (ax,ay)에서 h0a로 솟아 높이 top으로 건너가 (bx,by) h0b로 내려온다. 꺾이는 곳은 둥근 마디
    public static void PipeArch(Transform g, float ax, float bx, float dy, float h0a, float h0b, float top, float dia, Color c)
    {
        PipeUp(g, ax, dy, h0a, top, dia, c);
        PipeUp(g, bx, dy, h0b, top, dia, c);
        var p = Prim.Cyl(g, "pipe", L((ax + bx) / 2, dy, top), dia, Mathf.Abs(bx - ax), c); p.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Prim.Sphere(g, "elbow", L(ax, dy, top), dia + 1.2f, c);
        Prim.Sphere(g, "elbow", L(bx, dy, top), dia + 1.2f, c);
        Prim.Cyl(g, "collar", L(ax, dy, h0a + 1.5f), dia + 3, 3, c);
        Prim.Cyl(g, "collar", L(bx, dy, h0b + 1.5f), dia + 3, 3, c);
    }

    // 발광 재질(블룸이 색으로 번지게 발광색은 알베도보다 진하게). 일할 때 켜고 쉴 때 끄는 쌍은 MachineAnim.AddGlowMat
    public static Material GlowMat(Color c, float e)
    {
        var b = Resources.Load<Material>("Mats/Emissive");
        var m = b != null ? new Material(b) : new Material(Mats.Solid(c));
        m.color = c; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * e);
        return m;
    }

    // 가열판(목표 그림의 주황 발광 창): 몸통 앞면(dy = 앞면)에 어두운 창틀 + 세로 막대 n개. 일하면 주황으로 달아오른다
    public static void HeatWindow(Transform g, float dx, float dyFace, float h, float w, float hh, int n, MachineAnim a)
    {
        Prim.Box(g, "heatFrame", L(dx, dyFace + 0.6f, h), new Vector3(w, hh, 1.2f), STEEL_D);
        var on = GlowMat(HEAT, 1.6f); var off = Mats.Solid(HEAT_OFF);
        float bw = (w - 4) / n;
        for (int i = 0; i < n; i++)
        {
            var b = Prim.Box(g, "heatBar", L(dx - (w - 4) / 2 + bw * (i + 0.5f), dyFace + 1.3f, h), new Vector3(bw * 0.55f, hh - 4, 0.8f), HEAT_OFF);
            Prim.NoShadow(b); a.AddGlowMat(b.GetComponent<Renderer>(), on, off);
        }
    }
    public static readonly Color HEAT = GameConfig.Hex("ff8a3d"), HEAT_OFF = GameConfig.Hex("8a3a22"), PIPE = GameConfig.Hex("4fa8e0");

    // 꼭대기 작은 표시등(기둥 없이): 일하면 산출 색으로 깜빡
    public static void Knob(Transform g, float dx, float dy, float h, Color on, MachineAnim a)
    {
        Prim.Cyl(g, "knobBase", L(dx, dy, h + 1), 6, 2, STEEL_D);
        var s = Prim.Sphere(g, "knob", L(dx, dy, h + 3.5f), 5, on); Prim.NoShadow(s);
        a.AddBlink(s.GetComponent<Renderer>(), on);
    }

    // 작동 표시등: 일하면 산출 색으로 깜빡, 쉬면 어둡게
    public static Renderer Lamp(Transform g, float dx, float dy, float h, Color on, MachineAnim a)
    {
        Prim.Cyl(g, "lampPost", L(dx, dy, h / 2), 2.4f, h, STEEL_D);
        var s = Prim.Sphere(g, "lamp", L(dx, dy, h + 2), 6, on); Prim.NoShadow(s);
        var r = s.GetComponent<Renderer>(); a.AddBlink(r, on);
        return r;
    }

    // 산출 받침대: 앞쪽 낮은 테이블(산출물이 이 위에 쌓인다). 반환값은 쌓기 기준 높이
    public static float Table(Transform g, float dx, float dy, float w, float d, float h, Color edge)
    {
        Prim.Box(g, "tableLeg", L(dx, dy, (h - 2) / 2), new Vector3(w * 0.7f, h - 2, d * 0.7f), STEEL_D);   // 상판 밑면까지만(윗면이 겹치면 깜박임)
        Prim.Box(g, "table", L(dx, dy, h - 1), new Vector3(w, 2, d), SHELL);
        Prim.Box(g, "tableEdge", L(dx, dy + d / 2 - 0.8f, h - 1.6f), new Vector3(w + 0.6f, 2.6f, 1.6f), edge);
        return h;
    }
}

// 기계 애니메이션: 상태는 람다로 읽는다(시스템 코드를 건드리지 않게)
public class MachineAnim : MonoBehaviour
{
    public Func<bool> working = () => false;
    public Func<float> level;            // 0~1, 액체 높이
    public Transform liquid; float liqH, liqY0;
    public readonly List<Transform> spin = new List<Transform>(); public float spinSpeed = 240f;
    readonly List<(Renderer r, Material on, Material off)> blink = new List<(Renderer, Material, Material)>();
    readonly List<(Renderer r, Material on, Material off)> glow = new List<(Renderer, Material, Material)>();
    public readonly List<Transform> bubbles = new List<Transform>(); public float bubbleH = 20f;
    public float bubbleS0 = 3f, bubbleS1 = 1.2f;                       // 거품(김) 크기: 시작 → 끝(논리). 해빙기 김은 크게 시작해 흩어진다
    public readonly List<Transform> showWhenWorking = new List<Transform>();   // 일할 때만 보이는 것(해빙기 물줄기)
    public readonly List<Transform> belt = new List<Transform>(); public float beltX0, beltX1; public float beltSpeed = 22f;
    float t, lv = -1;

    public void SetLiquid(Transform l) { liquid = l; liqH = l.localScale.y; liqY0 = l.localPosition.y - liqH; }
    public void AddBlink(Renderer r, Color on) => blink.Add((r, Mats.Solid(on), Mats.Solid(Color.Lerp(on, MachineKit.STEEL_D, 0.65f))));
    public void AddGlow(Renderer r, Color on, Color off) => glow.Add((r, Mats.Solid(on), Mats.Solid(off)));
    public void AddGlowMat(Renderer r, Material on, Material off) => glow.Add((r, on, off));

    void Update()
    {
        float dt = Time.deltaTime; bool w = working(); t += dt;
        if (liquid != null && level != null)
        {
            float target = Mathf.Clamp01(level());
            lv = lv < 0 ? target : Mathf.MoveTowards(lv, target, dt * 0.8f);
            float h = Mathf.Max(0.02f, lv) * liqH;
            liquid.localScale = new Vector3(liquid.localScale.x, h, liquid.localScale.z);
            liquid.localPosition = new Vector3(liquid.localPosition.x, liqY0 + h, liquid.localPosition.z);
        }
        foreach (var s in spin) if (w) s.Rotate(0, spinSpeed * dt, 0, Space.Self);
        bool on = w && Mathf.Repeat(t, 0.8f) < 0.5f;
        foreach (var b in blink) b.r.sharedMaterial = on ? b.on : b.off;
        foreach (var s in showWhenWorking) if (s.gameObject.activeSelf != w) s.gameObject.SetActive(w);
        foreach (var gl in glow) gl.r.sharedMaterial = w ? gl.on : gl.off;
        for (int i = 0; i < bubbles.Count; i++)
        {
            var b = bubbles[i]; b.gameObject.SetActive(w);
            if (!w) continue;
            float k = Mathf.Repeat(t * 0.9f + i / (float)bubbles.Count, 1f);
            var p = b.localPosition; b.localPosition = new Vector3(p.x, (bubbleH * k + 2) * GameConfig.K + bubbleBase, p.z);
            b.localScale = Vector3.one * Mathf.Lerp(bubbleS0, bubbleS1, k) * GameConfig.K;
        }
        if (w) for (int i = 0; i < belt.Count; i++)
        {
            var b = belt[i]; var p = b.localPosition;
            float x = p.x / GameConfig.K + beltSpeed * dt; if (x > beltX1) x = beltX0;
            b.localPosition = new Vector3(x * GameConfig.K, p.y, p.z);
        }
    }
    public float bubbleBase;
}

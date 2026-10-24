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
    public static readonly Color GLASS = new Color(0.82f, 0.94f, 1f, 0.42f);

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
        Prim.Box(g, "plate", L(0, 0, 2.5f), new Vector3(w, 5, d), STEEL);
        Prim.Box(g, "plateTop", L(0, 0, 5.2f), new Vector3(w - 6, 0.6f, d - 6), GameConfig.Hex("707885"));
        return g;
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
        Prim.Cyl(t, "base", L(0, 0, h0 / 2), dia + 4, h0, STEEL_D);
        var l = Prim.Cyl(t, "liquid", L(0, 0, h0 + h / 2), dia - 3, h - 2, liquid); liq = l.transform;
        var gl = Prim.Cyl(t, "glass", L(0, 0, h0 + h / 2), dia, h, GLASS); gl.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(GLASS); Prim.NoShadow(gl);
        // 뚜껑은 작게(위에서 내려다보는 카메라라 큰 뚜껑이면 물이 안 보인다), 유리 위로 물 높이가 보이게
        Prim.Cyl(t, "rim", L(0, 0, h0 + h - 0.6f), dia + 1.4f, 1.2f, SHELL_D);   // 유리 윗테(얇게): 투명 유리의 윤곽
        return t;
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
    public readonly List<Transform> belt = new List<Transform>(); public float beltX0, beltX1; public float beltSpeed = 22f;
    float t, lv = -1;

    public void SetLiquid(Transform l) { liquid = l; liqH = l.localScale.y; liqY0 = l.localPosition.y - liqH; }
    public void AddBlink(Renderer r, Color on) => blink.Add((r, Mats.Solid(on), Mats.Solid(Color.Lerp(on, MachineKit.STEEL_D, 0.65f))));
    public void AddGlow(Renderer r, Color on, Color off) => glow.Add((r, Mats.Solid(on), Mats.Solid(off)));

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
        foreach (var gl in glow) gl.r.sharedMaterial = w ? gl.on : gl.off;
        for (int i = 0; i < bubbles.Count; i++)
        {
            var b = bubbles[i]; b.gameObject.SetActive(w);
            if (!w) continue;
            float k = Mathf.Repeat(t * 0.9f + i / (float)bubbles.Count, 1f);
            var p = b.localPosition; b.localPosition = new Vector3(p.x, (bubbleH * k + 2) * GameConfig.K + bubbleBase, p.z);
            b.localScale = Vector3.one * Mathf.Lerp(3f, 1.2f, k) * GameConfig.K;
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

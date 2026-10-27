using System;
using System.Collections.Generic;
using UnityEngine;

// KayKit(Kay Lousberg, CC0) 모델: tools/kaykit_bake.py 가 팔레트 아틀라스 색을 정점 색으로 구워 둔 Resources/Models/kk_<이름>.bytes 를 읽는다.
// 게임은 텍스처 없이 정점 색 셰이더(Shaders/MarsVColor)로 그리므로 ToyMesh.Kk 로 다른 조각과 한 메시에 합친다(외곽선·그림자·인스턴싱 그대로).
// 좌표는 굽는 단계에서 Unity 로컬(x 동·y 위·z 북, 감김 뒤집음)로 바꿔 두었고, 단위는 KayKit 원래 단위(배치할 때 축별 스케일).
// 앞면(문·서랍)은 모두 −z(남, 카메라 쪽)를 본다.
// 스위치: 검증 인자 -kk 0 이면 예전 모형(ToyMesh·MachineKit)으로 돌아간다(비교용). 기본은 KayKit.
public static class KK
{
    static int on = -1;
    public static bool On
    {
        get
        {
            if (on < 0) on = DevArgs.Get("-kk") == "0" ? 0 : 1;
            return on == 1;
        }
    }

    public class Model { public Vector3[] p, n; public Color[] c; public int[] t; public Vector3 min, max; }
    static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();

    public static Model Load(string name)
    {
        if (cache.TryGetValue(name, out var m)) return m;
        m = null;
        var ta = Resources.Load<TextAsset>("Models/kk_" + name);
        if (ta == null) Debug.LogWarning("KK: Resources/Models/kk_" + name + ".bytes 없음");
        else
        {
            try { m = Parse(ta.bytes); }
            catch (Exception e) { Debug.LogWarning("KK: 읽기 실패 " + name + " — " + e.Message); }
        }
        cache[name] = m;
        return m;
    }

    static Model Parse(byte[] b)
    {
        if (b.Length < 36 || b[0] != 'K' || b[1] != 'K' || b[2] != 'M' || b[3] != '1') throw new Exception("KKM1 아님");
        int nv = BitConverter.ToInt32(b, 4), ni = BitConverter.ToInt32(b, 8), o = 12;
        float F() { float f = BitConverter.ToSingle(b, o); o += 4; return f; }
        var m = new Model { p = new Vector3[nv], n = new Vector3[nv], c = new Color[nv], t = new int[ni] };
        m.min = new Vector3(F(), F(), F()); m.max = new Vector3(F(), F(), F());
        for (int i = 0; i < nv; i++) m.p[i] = new Vector3(F(), F(), F());
        for (int i = 0; i < nv; i++) m.n[i] = new Vector3(F(), F(), F());
        for (int i = 0; i < nv; i++) { m.c[i] = new Color(b[o] / 255f, b[o + 1] / 255f, b[o + 2] / 255f, 1f); o += 4; }
        for (int i = 0; i < ni; i++) { m.t[i] = BitConverter.ToInt32(b, o); o += 4; }
        return m;
    }

    /* ---------- 색 바꾸기(정점 색 → 정점 색) ---------- */
    // KayKit 흰색·회색은 푸른 기가 있어 따뜻한 기지 팔레트(외피 eee8de)에 맞게 무채색만 살짝 데운다
    public static Color Warm(Color c)
    {
        Color.RGBToHSV(c, out _, out float s, out float v);
        if (s > 0.18f) return c;
        float k = 1f - s / 0.18f;   // 무채색일수록 많이
        return new Color(Mathf.Clamp01(c.r * (1f + 0.035f * k)), c.g * (1f + 0.005f * k), c.b * (1f - 0.05f * k), 1f);
    }

    // 색상 범위(도, h0~h1, 0 = 빨강)의 채도 있는 색을 to 의 색상·채도로 바꾸고 명도는 원래 명도 / refV 비율만큼 to 에 곱한다(그라데이션 유지).
    // 나머지 색은 Warm.
    public static Func<Color, Color> Hue(float h0, float h1, Color to, float refV = 0.8f, Func<Color, Color> rest = null)
    {
        Color.RGBToHSV(to, out float th, out float ts, out float tv);
        return c =>
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float hd = h * 360f;
            if (s > 0.3f && hd >= h0 && hd <= h1) return Color.HSVToRGB(th, ts, Mathf.Clamp01(tv * v / refV));
            return rest != null ? rest(c) : Warm(c);
        };
    }
    // 두 범위를 차례로
    public static Func<Color, Color> Hue2(float a0, float a1, Color ta, float b0, float b1, Color tb, float refA = 0.8f, float refB = 0.8f)
        => Hue(a0, a1, ta, refA, Hue(b0, b1, tb, refB));
}

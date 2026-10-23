using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §7.4 손맛: 스쿼시·팝·링·동전 팝·화면 플래시·흔들림. 플로팅 텍스트는 WorldLabels, HUD 동전은 HUD가 담당.
public class JuiceFX : MonoBehaviour
{
    public static JuiceFX I;
    readonly HashSet<Transform> busy = new HashSet<Transform>();

    void Awake() { I = this; }

    public static float EaseOutBack(float k, float c = 1.70158f)
    {
        k -= 1; return k * k * ((c + 1) * k + c) + 1;
    }

    // 0.32s, s=sin(πk)*0.22*a → (1+s, 1−s, 1+s)
    public void Squash(Transform t, float a = 1f) { if (t != null && !busy.Contains(t)) StartCoroutine(CoSquash(t, a)); }
    IEnumerator CoSquash(Transform t, float a)
    {
        busy.Add(t); var b = t.localScale; float el = 0;
        while (el < GameConfig.SQUASH_T && t != null)
        {
            el += Time.deltaTime; float s = Mathf.Sin(Mathf.PI * Mathf.Clamp01(el / GameConfig.SQUASH_T)) * 0.22f * a;
            t.localScale = new Vector3(b.x * (1 + s), b.y * (1 - s), b.z * (1 + s));
            yield return null;
        }
        if (t != null) t.localScale = b; busy.Remove(t);
    }

    // from→1 easeOutBack, 바닥 0.35
    public void Pop(Transform t, float from = 0.001f, float dur = GameConfig.POP_T) { if (t != null) StartCoroutine(CoPop(t, from, dur)); }
    IEnumerator CoPop(Transform t, float from, float dur)
    {
        var b = t.localScale; float el = 0;
        while (el < dur && t != null)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / dur);
            float s = Mathf.Max(0.35f, Mathf.LerpUnclamped(from, 1f, EaseOutBack(k)));
            t.localScale = b * s; yield return null;
        }
        if (t != null) t.localScale = b;
    }

    // 링: 0.6s, 스케일 1+2.2k, 페이드
    public void Ring(Vector3 pos, Color c, float rL = 30f) { StartCoroutine(CoRing(pos, c, rL)); }
    IEnumerator CoRing(Vector3 pos, Color c, float rL)
    {
        var go = Prim.Ring(transform, pos + Vector3.up * 0.02f, rL, c);
        var mat = go.GetComponent<Renderer>().material; float el = 0;
        while (el < 0.6f)
        {
            el += Time.deltaTime; float k = el / 0.6f;
            go.transform.localScale = Vector3.one * rL * GameConfig.K * (1 + 2.2f * k);
            var cc = c; cc.a = 1 - k; mat.color = cc; yield return null;
        }
        Destroy(go);
    }

    // 동전 팝: n개 튀김, 중력 380, 바닥 반발 −0.4, 0.9s
    public void CoinPop(Vector3 pos, int n) { for (int i = 0; i < n; i++) StartCoroutine(CoCoin(pos)); }
    IEnumerator CoCoin(Vector3 pos)
    {
        var go = Prim.Cyl(transform, "coin", pos, 7, 1.6f, GameConfig.GOLD); Prim.NoShadow(go);
        Vector3 v = new Vector3(Random.Range(-60f, 60f), Random.Range(120f, 220f), Random.Range(-60f, 60f)) * GameConfig.K;
        float g = 380f * GameConfig.K, el = 0; Vector3 p = pos; float floor = pos.y;
        while (el < 0.9f)
        {
            float dt = Time.deltaTime; el += dt; v.y -= g * dt; p += v * dt;
            if (p.y < floor) { p.y = floor; v.y *= -0.4f; }
            go.transform.position = p; go.transform.Rotate(0, 400 * dt, 0); yield return null;
        }
        Destroy(go);
    }

    public void Shake(float a) { if (CameraRig.I) CameraRig.I.Shake(a); }
    public void Flash(float a) { if (HUD.I) HUD.I.Flash(a); }
    public void Vibrate(int ms)
    {
#if UNITY_IOS || UNITY_ANDROID
        if (ms > 0) Handheld.Vibrate();
#endif
    }
}

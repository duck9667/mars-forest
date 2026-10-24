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

    // from→1 easeOutBack(되튐 진폭 c, 원본 기본 1.70158 / 구역 바닥 0.35). 같은 Transform에 겹쳐 호출되면 이전 팝을 끊고 기준 스케일을 복원한다
    readonly Dictionary<Transform, (Coroutine co, Vector3 baseScale)> pops = new Dictionary<Transform, (Coroutine, Vector3)>();
    public void Pop(Transform t, float from = 0.001f, float dur = GameConfig.POP_T, float c = 1.70158f)
    {
        if (t == null) return;
        Vector3 b = t.localScale;
        if (pops.TryGetValue(t, out var prev)) { if (prev.co != null) StopCoroutine(prev.co); b = prev.baseScale; t.localScale = b; }
        var co = StartCoroutine(CoPop(t, b, from, dur, c));
        pops[t] = (co, b);
    }
    IEnumerator CoPop(Transform t, Vector3 b, float from, float dur, float c)
    {
        float el = 0;
        while (el < dur && t != null)
        {
            el += Time.deltaTime; float k = Mathf.Clamp01(el / dur);
            float s = Mathf.Max(0.001f, Mathf.LerpUnclamped(from, 1f, EaseOutBack(k, c)));
            t.localScale = b * s; yield return null;
        }
        if (t != null) { t.localScale = b; pops.Remove(t); }
    }

    // 링: 0.6s, 스케일 1+2.2k, 페이드
    public void Ring(Vector3 pos, Color c, float rL = 30f) { StartCoroutine(CoRing(pos, c, rL)); }
    IEnumerator CoRing(Vector3 pos, Color c, float rL)
    {
        var go = Prim.Ring(transform, pos + Vector3.up * 0.02f, rL, c);
        var mat = go.GetComponent<Renderer>().sharedMaterial; float el = 0;   // Prim.Ring이 만든 고유 인스턴스
        while (el < 0.6f)
        {
            el += Time.deltaTime; float k = el / 0.6f;
            go.transform.localScale = Vector3.one * rL * GameConfig.K * (1 + 2.2f * k);
            var cc = c; cc.a = 1 - k; mat.color = cc; yield return null;
        }
        Destroy(mat); Destroy(go);
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

    // 색종이: 작은 네모 n개가 튀어 올랐다 떨어짐 (판매 18/36, 구역 32, 장식 20)
    static readonly Color[] CONFETTI = { GameConfig.GOLD, GameConfig.Hex("ff6b6b"), GameConfig.Hex("5ed6c8"), GameConfig.Hex("8a5cd6"), GameConfig.Hex("5cc45e"), Color.white };
    public void Confetti(Vector3 pos, int n) { for (int i = 0; i < n; i++) StartCoroutine(CoConfetti(pos, CONFETTI[i % CONFETTI.Length])); }
    IEnumerator CoConfetti(Vector3 pos, Color c)
    {
        var go = Prim.Box(transform, "confetti", pos, new Vector3(4, 0.8f, 3), c); Prim.NoShadow(go);
        Vector3 v = new Vector3(Random.Range(-90f, 90f), Random.Range(160f, 300f), Random.Range(-90f, 90f)) * GameConfig.K;
        Vector3 spin = Random.insideUnitSphere * 540f; float g = 300f * GameConfig.K, el = 0, life = Random.Range(1.1f, 1.6f); Vector3 p = pos;
        while (el < life)
        {
            float dt = Time.deltaTime; el += dt; v.y -= g * dt; v.x *= 0.995f; v.z *= 0.995f; p += v * dt;
            if (p.y < pos.y - 1.4f) { p.y = pos.y - 1.4f; v = Vector3.zero; }
            go.transform.position = p; go.transform.Rotate(spin * dt); yield return null;
        }
        Destroy(go);
    }

    // 먼지: 반투명 구가 퍼지며 사라짐 (발걸음·구역 모서리·드릴)
    public void Dust(Vector3 pos, int n) { for (int i = 0; i < n; i++) StartCoroutine(CoDust(pos)); }
    IEnumerator CoDust(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>()); go.transform.SetParent(transform, false);
        var c = GameConfig.Hex("e0b48a"); c.a = 0.55f; var mat = Mats.Trans(c); go.GetComponent<Renderer>().sharedMaterial = mat; Prim.NoShadow(go);
        Vector3 v = new Vector3(Random.Range(-40f, 40f), Random.Range(10f, 30f), Random.Range(-40f, 40f)) * GameConfig.K; Vector3 p = pos; float el = 0;
        while (el < 0.5f)
        {
            float dt = Time.deltaTime; el += dt; float k = el / 0.5f; p += v * dt;
            go.transform.position = p; go.transform.localScale = Vector3.one * (6 + 14 * k) * GameConfig.K;
            var cc = c; cc.a = 0.55f * (1 - k); mat.color = cc; yield return null;
        }
        Destroy(mat); Destroy(go);
    }

    public void Shake(float a) { if (CameraRig.I) CameraRig.I.Shake(a); }
    // 화면 전체 흰 번쩍임: 이주·정착 완료 같은 큰 순간에만(업그레이드·목표마다 쓰면 깜박임으로 보인다, jake 10/25)
    public void Flash(float a) { if (HUD.I) HUD.I.Flash(a); }
    // 진동: iOS는 짧은 햅틱(Haptics, ms로 세기 선택), 설정에서 끄면(S.vibrate=false) 아무것도 안 함
    public void Vibrate(int ms)
    {
        if (ms <= 0 || GameState.I == null || !GameState.I.S.vibrate) return;
        Haptics.Play(ms);
    }
}

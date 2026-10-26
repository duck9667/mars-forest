using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 우주 연출(글자 없이, docs/chapter3-design.md "우주 연출"):
//  · 궤도선 그림자: 1장 생존을 끝낸 뒤(S.stage ≥ 5) 1~2분마다 큰 우주선 그림자가 땅 위를 쓸고 지나간다(4~5초, 옅게).
//  · 밤 발사 불꽃: 3장부터(S.stage ≥ 10) 밤이면 먼 곳에서 로켓이 오른다(화면 위쪽, 작은 불꽃 + 꼬리). 화면 번쩍임 없음.
// 화면에 보이는 곳 기준으로 경로를 잡는다(카메라 중심 둘레). 검증 인자: -sky shadow|launch [초](그 초에 한 번)
public class SpaceSky : MonoBehaviour
{
    public static SpaceSky I;
    GameState G => GameState.I;
    float shadowT = 50f, launchT = 20f;
    Material shadowMat; Texture2D shadowTex;
    static bool devFired;

    void Awake() { I = this; }

    void Start()
    {
        shadowTex = PaintShadow();
        var d = DevArgs.Get("-sky");
        if (!devFired && !string.IsNullOrEmpty(d))
        {
            devFired = true; float at = 2f;
            var sec = DevArgs.Get("-skyAt"); if (sec != null) float.TryParse(sec, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out at);
            StartCoroutine(CoDev(d, at));
        }
    }
    IEnumerator CoDev(string what, float at)
    {
        yield return new WaitForSeconds(at);
        if (what == "launch") StartCoroutine(CoLaunch()); else StartCoroutine(CoShadow());
    }

    void Update()
    {
        if (G == null || CameraRig.I == null) return;
        if (G.S.tut < 3 || (StoryManager.I != null && StoryManager.I.IntroActive)) return;
        float dt = Time.deltaTime;
        if (G.S.stage >= 5)
        {
            shadowT -= dt;
            if (shadowT <= 0f) { shadowT = Random.Range(70f, 140f); StartCoroutine(CoShadow()); }
        }
        float night = DayNight.I != null ? DayNight.I.night : 0f;
        if (G.S.stage >= 10 && night > 0.55f)
        {
            launchT -= dt;
            if (launchT <= 0f) { launchT = Random.Range(28f, 55f); StartCoroutine(CoLaunch()); }
        }
    }

    // ───────────── 궤도선 그림자 ─────────────
    // 그림자 그림: 몸통(긴 타원) + 앞 고리 모듈 + 뒤로 젖힌 날개 둘. 가장자리는 겹친 옅은 띠로 흐리게(번지는 그림자)
    static Texture2D PaintShadow()
    {
        const float W = 260f, H = 140f;
        var p = new GroundPainter(0f, 0f, W, H, 1.6f, 3, null);
        var c = GameConfig.Hex("2a0e05");
        for (int k = 4; k >= 0; k--)
        {
            float g = k * 3.2f, a = k == 0 ? 0.55f : 0.09f;
            p.Ellipse(W * 0.5f, H * 0.5f, 92f + g, 18f + g, 0f, c, a);                     // 몸통
            p.Ellipse(W * 0.5f + 64f, H * 0.5f, 24f + g, 24f + g, 0f, c, a);               // 앞 고리 모듈
            p.Poly(new List<Vector2> { new Vector2(W * 0.5f - 10f - g, H * 0.5f - 12f - g), new Vector2(W * 0.5f - 64f - g, 10f - g), new Vector2(W * 0.5f - 86f - g, 14f - g), new Vector2(W * 0.5f - 52f + g, H * 0.5f - 10f) }, c, a);
            p.Poly(new List<Vector2> { new Vector2(W * 0.5f - 10f - g, H * 0.5f + 12f + g), new Vector2(W * 0.5f - 64f - g, H - 10f + g), new Vector2(W * 0.5f - 86f - g, H - 14f + g), new Vector2(W * 0.5f - 52f + g, H * 0.5f + 10f) }, c, a);
        }
        return p.ToTexture(true);
    }

    IEnumerator CoShadow()
    {
        var cam = CameraRig.I; if (cam == null || shadowTex == null) yield break;
        float halfW = 210f / cam.Zoom, viewH = cam.ViewH;
        // 화면 왼쪽 아래 바깥 → 오른쪽 위 바깥(조금 비스듬히)
        var a = new Vector2(cam.camX - halfW - 200f, cam.camY + viewH * Random.Range(0.05f, 0.3f));
        var b = new Vector2(cam.camX + halfW + 200f, cam.camY - viewH * Random.Range(0.1f, 0.35f));
        var go = GroundPainter.TexQuad(transform, "orbitShadow", WorldMap.W2U(a, 0.3f), 260f * 1.4f, 140f * 1.4f, shadowTex, true);
        var mr = go.GetComponent<Renderer>(); mr.receiveShadows = false; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var m = mr.material; var dir = (b - a).normalized;
        go.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 0f);   // 그림 가로(+x) = 진행 방향
        float dur = (b - a).magnitude / 170f, el = 0f;
        AudioManager.I?.Play("whoosh", 0.45f, 0.25f);
        while (el < dur && go != null)
        {
            el += Time.deltaTime; float k = el / dur;
            go.transform.position = WorldMap.W2U(Vector2.Lerp(a, b, k), 0.3f);
            float night = DayNight.I != null ? DayNight.I.night : 0f;
            var col = Color.white; col.a = (1f - 0.6f * night) * Mathf.Clamp01(Mathf.Min(k, 1f - k) * 6f) * 0.75f;
            m.color = col;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ───────────── 밤 발사 불꽃 ─────────────
    // 카메라에 붙여 화면 위쪽에서 오른다(카메라가 움직여도 하늘 높이에 있는 것처럼). 몸통 불꽃 + 남기는 꼬리(점점 작아짐)
    static Mesh ballMesh;
    IEnumerator CoLaunch()
    {
        var cam = CameraRig.I; if (cam == null) yield break;
        if (ballMesh == null) ballMesh = ToyMesh.Cached("skyBall", m => m.Ball(1f, GameConfig.Hex("ffd27a"), GameConfig.Hex("fff3c8"), 10, 4));
        var mat = ToyMesh.Mat(0f, 0f, 1f);
        float halfW = 210f / cam.Zoom, viewH = cam.ViewH;
        Vector2 off = new Vector2(Random.Range(-0.7f, 0.7f) * halfW, -viewH * Random.Range(0.08f, 0.2f));
        float drift = Random.Range(-14f, 14f);
        var head = ToyMesh.Put(transform, "skyRocket", ballMesh, mat, Vector3.zero).transform; Prim.NoShadow(head.gameObject);
        var trail = new List<(Transform t, Vector2 o, float h, float age)>();
        float h = 180f, v = 40f, el = 0f, puffT = 0f;
        while (el < 4.6f)
        {
            float dt = Time.deltaTime; el += dt;
            v += 120f * dt; h += v * dt; off.x += drift * dt;
            var basePt = new Vector2(cam.camX, cam.camY) + off;
            head.position = WorldMap.W2U(basePt, h);
            float s = 5.5f * (1f + Mathf.Sin(el * 40f) * 0.15f) * Mathf.Clamp01(el * 4f) * Mathf.Clamp01((4.6f - el) * 2f);
            head.localScale = Vector3.one * s * GameConfig.K;
            puffT += dt;
            if (puffT > 0.06f)
            {
                puffT = 0;
                var p = ToyMesh.Put(transform, "skyTrail", ballMesh, mat, Vector3.zero).transform; Prim.NoShadow(p.gameObject);
                trail.Add((p, off, h - 4f, 0f));
            }
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                var tr = trail[i]; tr.age += dt; trail[i] = tr;
                float ts = 3.6f * (1f - tr.age / 1.1f);
                if (ts <= 0.05f) { Destroy(tr.t.gameObject); trail.RemoveAt(i); continue; }
                tr.t.position = WorldMap.W2U(new Vector2(cam.camX, cam.camY) + tr.o, tr.h);
                tr.t.localScale = Vector3.one * ts * GameConfig.K;
            }
            yield return null;
        }
        Destroy(head.gameObject);
        foreach (var tr in trail) if (tr.t != null) Destroy(tr.t.gameObject);
    }
}

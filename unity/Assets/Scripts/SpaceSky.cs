using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 우주 연출(글자 없이, docs/chapter3-design.md "우주 연출"):
//  · 궤도선 그림자: 1장 생존을 끝낸 뒤(S.stage ≥ 5) 1~2분마다 큰 우주선 그림자가 땅 위를 쓸고 지나간다(4~5초, 옅게).
//  · 밤 발사 불꽃: 3장부터(S.stage ≥ 10) 밤이면 먼 곳에서 로켓이 오른다(화면 위쪽, 작은 불꽃 + 꼬리). 화면 번쩍임 없음.
//  · 궤도 풍경(r10): 3장부터 밤이면 화면 위쪽 하늘에 궤도 정거장(천천히 도는 불빛 고리 + 태양판 불빛 + 빨간 표시등)과
//    화물선 불빛(흰 앞등 + 빨간 꼬리등 + 짧은 불빛 꼬리)이 천천히 지나간다. 장이 진행될수록 늘어난다(화물선 동시 1~4대, 정거장은 3장 3/5부터).
//    정거장이 있으면 화물선 몇 대는 정거장으로 가서 도킹(작아지며 쏙, 정거장 가운데 불빛이 한 번 부푼다). 낮이 되면 서서히 사라진다.
//    모두 제 색 발광 알갱이(빛 오브젝트 없음)라 화면 번쩍임이 없고, 표시등은 크기만 천천히 숨 쉰다.
// 화면에 보이는 곳 기준으로 경로를 잡는다(카메라 중심 둘레). 검증 인자: -sky shadow|launch|orbit [초](그 초에 한 번, orbit은 장 조건 무시)
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
        if (what == "launch") StartCoroutine(CoLaunch()); else if (what == "orbit") DevOrbit(); else StartCoroutine(CoShadow());
    }

    void Update()
    {
        if (G == null || CameraRig.I == null) return;
        TickOrbit(Time.deltaTime);
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
        var mat = GlowMat();   // r10: 밤엔 _Self 발광이 꺼져 어두운 공으로 보였다
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
            head.localScale = Vector3.one * s;   // r10: 메시가 이미 ×K라 K를 두 번 곱해 불꽃이 거의 안 보였다
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
                tr.t.localScale = Vector3.one * ts;
            }
            yield return null;
        }
        Destroy(head.gameObject);
        foreach (var tr in trail) if (tr.t != null) Destroy(tr.t.gameObject);
    }

    // ───────────── 궤도 풍경(밤) ─────────────
    // 모든 불빛은 카메라 중심 기준 오프셋(off)과 높이(h)로 둔다: 카메라가 움직여도 하늘 멀리 있는 것처럼 화면에 붙어 있다
    class Mover
    {
        public Transform t, red, core; public Vector2 off, vel; public float h, age, spin, dock = -1f, pop;
        public bool station; public Mover target;
    }
    readonly List<Mover> movers = new List<Mover>();
    float shipSpawnT = 4f, stationSpawnT = 6f, orbitVis; bool forceOrbit;
    static Mesh warmMesh, whiteMesh, redMesh, blueMesh;
    const float ORBIT_H = 210f;

    /// 검증(-sky orbit, -event orbit): 장 조건 무시하고 정거장·화물선을 바로
    public void DevOrbit() { forceOrbit = true; shipSpawnT = 0f; stationSpawnT = 0f; Debug.Log("sky: orbit 강제"); }

    int OrbitShips => forceOrbit ? 4 : G.S.stage < 10 ? 0 : Mathf.Min(4, 1 + (G.S.stage - 10) / 3);   // 3장 1대 → 4장 끝 4대
    bool StationOn => forceOrbit || G.S.stage >= 12;

    void TickOrbit(float dt)
    {
        var cam = CameraRig.I;
        float night = DayNight.I != null ? DayNight.I.night : 0f;
        orbitVis = Mathf.MoveTowards(orbitVis, Mathf.InverseLerp(0.55f, 0.9f, night), dt * 0.5f);
        bool can = orbitVis > 0.05f && G.S.tut >= 3 && (StoryManager.I == null || !StoryManager.I.IntroActive);
        if (can)
        {
            int ships = 0; Mover st = null;
            foreach (var m in movers) { if (m.station) st = m; else ships++; }
            if (StationOn && st == null) { stationSpawnT -= dt; if (stationSpawnT <= 0f) { stationSpawnT = Random.Range(25f, 45f); SpawnStation(cam); } }
            if (ships < OrbitShips) { shipSpawnT -= dt; if (shipSpawnT <= 0f) { shipSpawnT = Random.Range(5f, 12f) / Mathf.Max(1, OrbitShips - 1); SpawnShip(cam, st); } }
        }
        if (movers.Count == 0) return;
        float halfW = 210f / cam.Zoom;
        for (int i = movers.Count - 1; i >= 0; i--)
        {
            var m = movers[i]; m.age += dt;
            if (m.target != null && !movers.Contains(m.target)) m.target = null;
            if (m.dock >= 0f) m.dock += dt;
            else if (m.target != null)
            {
                // 정거장으로: 방향만 천천히 틀어 따라간다(급회전 없음). 닿으면 도킹
                var to = m.target.off - m.off; float sp = m.vel.magnitude;
                m.vel = Vector2.Lerp(m.vel, to.normalized * sp, dt * 0.8f).normalized * sp;
                if (to.magnitude < 5f) { m.dock = 0f; m.target.pop = 1f; }
            }
            m.off += m.vel * dt;
            m.pop = Mathf.MoveTowards(m.pop, 0f, dt * 1.6f);
            bool gone = Mathf.Abs(m.off.x) > halfW + 90f || m.dock > 0.6f || (orbitVis <= 0.01f);
            if (gone) { Destroy(m.t.gameObject); movers.RemoveAt(i); continue; }
            float fade = Mathf.Clamp01(m.age / 1.5f) * orbitVis * (m.dock >= 0f ? 1f - m.dock / 0.6f : 1f);
            m.t.position = WorldMap.W2U(new Vector2(cam.camX, cam.camY) + m.off, m.h);
            m.t.localScale = Vector3.one * Mathf.Max(0.001f, fade) * (1f + 0.35f * m.pop);
            if (m.station) m.t.localRotation = Quaternion.Euler(0, m.spin += dt * 9f, 0);
            else m.t.localRotation = Quaternion.Euler(0, Mathf.Atan2(-m.vel.x, m.vel.y) * Mathf.Rad2Deg, 0);   // 로컬 +z(꼬리) = 진행 반대쪽(논리 y+ = 유니티 −z)
            if (m.red != null) m.red.localScale = Vector3.one * (0.7f + 0.5f * (0.5f + 0.5f * Mathf.Sin(m.age * 2.2f + i)));
            if (m.core != null) m.core.localScale = Vector3.one * 2.1f * (1f + 0.8f * m.pop);
        }
    }

    static void Meshes()
    {
        if (warmMesh != null) return;
        warmMesh = ToyMesh.Cached("orbWarm", m => m.Ball(1f, GameConfig.Hex("ffcf7a"), GameConfig.Hex("fff0c4"), 8, 3));
        whiteMesh = ToyMesh.Cached("orbWhite", m => m.Ball(1f, GameConfig.Hex("f4f6ff"), Color.white, 8, 3));
        redMesh = ToyMesh.Cached("orbRed", m => m.Ball(1f, GameConfig.Hex("ff4a3a"), GameConfig.Hex("ff8a70"), 8, 3));
        blueMesh = ToyMesh.Cached("orbBlue", m => m.Ball(1f, GameConfig.Hex("7fb8f0"), GameConfig.Hex("b8dcff"), 8, 3));
    }
    // 스스로 빛나는 알갱이 재질: 조명·밤 채도(_MarsNight)를 안 받는 Sprites/Default(정점 색 그대로). 불투명 셰이더(MarsVColor _Self)는
    // 밤에 제 색 발광이 꺼져 검은 점 + 외곽선으로 보였다. 투명 큐라 외곽선 마스크에도 안 걸린다. alpha < 1이면 번지는 후광
    static readonly Dictionary<int, Material> glowMats = new Dictionary<int, Material>();
    public static Material GlowMat(float alpha = 1f)
    {
        int key = Mathf.RoundToInt(alpha * 100f);
        if (glowMats.TryGetValue(key, out var m) && m != null) return m;
        var sh = Shader.Find("Sprites/Default");
        m = sh != null ? new Material(sh) : new Material(Mats.Solid(Color.white));
        m.color = new Color(1f, 1f, 1f, alpha); m.renderQueue = 3000 + key % 7;
        glowMats[key] = m; return m;
    }

    Transform Dot(Transform parent, Mesh mesh, Vector3 localL, float r, bool halo = false)
    {
        var go = ToyMesh.Put(parent, "dot", mesh, GlowMat(), localL * GameConfig.K); Prim.NoShadow(go);
        if (halo) { var h = ToyMesh.Put(go.transform, "halo", mesh, GlowMat(0.18f), Vector3.zero); Prim.NoShadow(h); h.transform.localScale = Vector3.one * 2.6f; }
        go.transform.localScale = Vector3.one * r; return go.transform;   // 메시가 이미 논리×K로 구워져 있어 배율은 논리 반지름 그대로
    }

    // 정거장: 수평 고리 불빛 10개(카메라 고도 때문에 납작한 타원으로 보임) + 가운데 큰 불빛 + 양옆 태양판 파란 불빛 셋씩 + 빨간 표시등
    void SpawnStation(CameraRig cam)
    {
        Meshes();
        float halfW = 210f / cam.Zoom, viewH = cam.ViewH; float dir = Random.value < 0.5f ? 1f : -1f;
        var root = new GameObject("orbitStation").transform; root.SetParent(transform, false);
        var m = new Mover { t = root, station = true, h = ORBIT_H + 30f, spin = Random.Range(0f, 360f) };
        for (int i = 0; i < 10; i++) { float a = i * Mathf.PI * 2f / 10f; Dot(root, warmMesh, new Vector3(Mathf.Cos(a) * 13f, 0, Mathf.Sin(a) * 13f), 1.25f); }
        m.core = Dot(root, whiteMesh, Vector3.zero, 2.1f, true);
        for (int s = -1; s <= 1; s += 2) for (int j = 0; j < 3; j++) Dot(root, blueMesh, new Vector3(s * (19f + j * 4.5f), 0, 0), 1.05f);
        m.red = Dot(root, redMesh, new Vector3(0, 3f, 0), 1f, true);
        m.off = new Vector2(-dir * (halfW + 60f), -viewH * Random.Range(0.26f, 0.36f) + 0.7f * m.h);
        m.vel = new Vector2(dir * Random.Range(6.5f, 8.5f), Random.Range(-1.2f, 1.2f));
        movers.Add(m);
        Debug.Log("sky: 궤도 정거장 등장 stage=" + G.S.stage);
    }

    // 화물선 불빛: 흰 앞등 + 뒤 빨간 꼬리등 + 점점 작아지는 불빛 꼬리 넷(진행 방향 반대쪽, 로컬 +z 뒤)
    void SpawnShip(CameraRig cam, Mover station)
    {
        Meshes();
        float halfW = 210f / cam.Zoom, viewH = cam.ViewH; float dir = Random.value < 0.5f ? 1f : -1f;
        var root = new GameObject("orbitShip").transform; root.SetParent(transform, false);
        var m = new Mover { t = root, h = ORBIT_H + Random.Range(-20f, 20f) };
        Dot(root, whiteMesh, Vector3.zero, 1.6f, true);
        m.red = Dot(root, redMesh, new Vector3(0, 0, 4.5f), 1f, true);
        for (int j = 0; j < 4; j++) Dot(root, warmMesh, new Vector3(0, 0, 8f + j * 4f), 0.9f - j * 0.17f);
        m.off = new Vector2(-dir * (halfW + 40f), -viewH * Random.Range(0.2f, 0.42f) + 0.7f * m.h);
        m.vel = new Vector2(dir * Random.Range(24f, 34f), Random.Range(-5f, 5f));
        if (station != null && Random.value < 0.45f) m.target = station;
        movers.Add(m);
    }
}

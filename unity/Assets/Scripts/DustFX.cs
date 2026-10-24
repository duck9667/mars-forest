using System.Collections.Generic;
using UnityEngine;

// §7.4 환경 연출 (jake 피드백 "둥둥 떠다니는 것들이 아쉽다" 반영): 원본은 먼지 입자 150 + 회오리 2개가 상시 떠돌았다.
// 지금은 입자 36개(1/4 이하)를 맵 좌우 가장자리 띠에만 옅게, 돌풍은 약하게(최대 ×2.5), 회오리는 하나가 30~60초에 한 번 가장자리를 지나간다.
public class DustFX : MonoBehaviour
{
    const int DUST_N = 36, DEV_N = 14;   // 가장자리 띠 2개에 반씩
    // 띠 세로 범위: 북쪽 생존 기지까지(Y_MIN−80 ~ 1120). 개수는 그대로라 북쪽으로 늘어난 만큼 조금 더 성기다
    const float DUST_Y0 = GameConfig.Y_MIN - 80f, DUST_Y1 = 1120f, DUST_CY = (DUST_Y0 + DUST_Y1) / 2f, DUST_LEN = DUST_Y1 - DUST_Y0;
    readonly List<ParticleSystem> systems = new List<ParticleSystem>();
    float gustT = 10f, gust = 0f;

    // 회오리 하나: 쉬는 동안 숨김, 차례가 오면 좌·우 가장자리 한쪽을 위→아래(또는 반대)로 30/s로 지나간 뒤 다시 숨는다
    class Devil { public Vector2 p, from, to; public float wait, life, dur; public bool on; public Transform root; public Transform[] beads; }
    Devil devil;
    static Material unlit;   // Sprites/Default(항상 포함 셰이더): 정점색·알파 지원, 조명 영향 없음

    static Material Unlit()
    {
        if (unlit != null) return unlit;
        var sh = Shader.Find("Sprites/Default");
        unlit = sh != null ? new Material(sh) : Mats.Trans(Color.white);
        return unlit;
    }

    // 32×32 방사형 그라데이션 스프라이트(원본 769행)
    static Texture2D Soft()
    {
        var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float d = Mathf.Sqrt((x + 0.5f - 16f) * (x + 0.5f - 16f) + (y + 0.5f - 16f) * (y + 0.5f - 16f)) / 16f;
            float a = d < 0.45f ? Mathf.Lerp(1f, 0.55f, d / 0.45f) : Mathf.Lerp(0.55f, 0f, Mathf.Clamp01((d - 0.45f) / 0.55f));
            px[y * 32 + x] = new Color32(255, 232, 205, (byte)(a * 255f));
        }
        t.SetPixels32(px); t.Apply(false, true);
        return t;
    }

    void Start()
    {
        BuildDust();
        BuildDevils();
    }

    void BuildDust()
    {
        BuildDustBand(-30f);    // 왼쪽 가장자리 띠 (x −70~10)
        BuildDustBand(430f);    // 오른쪽 가장자리 띠 (x 390~470)
    }

    void BuildDustBand(float cx)
    {
        float k = GameConfig.K;
        var go = new GameObject("dust"); go.transform.SetParent(transform, false);
        go.transform.position = WorldMap.W2U(cx, DUST_CY, 60f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.startLifetime = 16f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
        main.startColor = new Color(1f, 0.886f, 0.784f, 0.32f);
        main.maxParticles = DUST_N / 2; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
        var em = ps.emission; em.rateOverTime = DUST_N / 2 / 16f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(80f, 100f, DUST_LEN) * k;
        // 바람: 가장자리 띠 밖으로 새지 않게 x는 거의 0, 세로로 천천히 흐른다. 돌풍은 speedModifier로 배속
        var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(-1.5f * k, 1.5f * k); vol.y = new ParticleSystem.MinMaxCurve(-1.5f * k, 1.5f * k); vol.z = new ParticleSystem.MinMaxCurve(-6f * k, 6f * k);
        vol.speedModifier = new ParticleSystem.MinMaxCurve(1f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.08f; nz.frequency = 0.25f; nz.scrollSpeed = 0.2f; nz.quality = ParticleSystemNoiseQuality.Low;
        // 생성·소멸 때 투명 → 튀지 않게
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.material = new Material(Unlit()) { mainTexture = Soft() };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.sortingFudge = -10f;
        ps.Play();
        systems.Add(ps);
    }

    // 회오리: 반투명 구슬 14개가 나선 원뿔로 돈다. 처음엔 숨김, 30~60초 뒤 첫 등장
    void BuildDevils()
    {
        // 리니어 색공간에선 밝은 반투명이 더 불투명해 보여 원본 .4보다 낮춘다
        var mat = new Material(Unlit()) { color = new Color(1f, 0.851f, 0.749f, 0.22f) };
        devil = new Devil { wait = 30f + Random.value * 30f };
        devil.root = new GameObject("devil").transform; devil.root.SetParent(transform, false);
        devil.beads = new Transform[DEV_N];
        for (int i = 0; i < DEV_N; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "bead"; Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(devil.root, false);
            b.GetComponent<Renderer>().sharedMaterial = mat; Prim.NoShadow(b);
            b.transform.localScale = Vector3.one * (1.6f + i * 0.26f) * 2f * GameConfig.K;
            devil.beads[i] = b.transform;
        }
        devil.root.gameObject.SetActive(false);
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 돌풍(약하게)
        gustT -= dt;
        if (gustT <= 0f) { gustT = 18f + Random.value * 12f; gust = 2.6f; if (AudioManager.I != null) AudioManager.I.Play("whoosh", 1f, 0.4f); }
        gust = Mathf.Max(0f, gust - dt);
        float gk = gust > 0f ? 1f + 1.5f * Mathf.Sin(gust / 2.6f * Mathf.PI) : 1f;
        foreach (var ps in systems) { var vol = ps.velocityOverLifetime; vol.speedModifier = new ParticleSystem.MinMaxCurve(gk); }
        // 회오리: 쉬다가 가장자리 한쪽을 한 번 지나감
        var d = devil;
        if (!d.on)
        {
            d.wait -= dt; if (d.wait > 0f) return;
            bool left = Random.value < 0.5f, down = Random.value < 0.5f;
            float x = left ? -24f + Random.value * 16f : 408f + Random.value * 16f;
            float top = GameConfig.Y_MIN - 40f;
            d.from = new Vector2(x, down ? top : 1080f); d.to = new Vector2(x + (Random.value - 0.5f) * 20f, down ? 1080f : top);
            d.p = d.from; d.life = 0f; d.dur = Vector2.Distance(d.from, d.to) / 30f; d.on = true;
            d.root.gameObject.SetActive(true);
        }
        d.life += dt;
        float u = Mathf.Clamp01(d.life / d.dur);
        d.p = Vector2.Lerp(d.from, d.to, u);
        float grow = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 12f);   // 앞뒤 8%에서 커지고 작아짐
        float t = Time.time;
        for (int i = 0; i < DEV_N; i++)
        {
            float a = t * 9.1f + i * 1.15f, r = 2.5f + i * 1.15f, h = i * 4.6f + 2f;
            d.beads[i].position = WorldMap.W2U(d.p.x + Mathf.Cos(a) * r, d.p.y + Mathf.Sin(a) * r, h);
            d.beads[i].localScale = Vector3.one * (1.6f + i * 0.26f) * 2f * GameConfig.K * Mathf.Max(0.001f, grow);
        }
        if (u >= 1f) { d.on = false; d.wait = 30f + Random.value * 30f; d.root.gameObject.SetActive(false); }
    }
}

using UnityEngine;

// §7.4 환경 연출: 떠다니는 먼지 입자 150(돌풍 14~24s 주기, 2.6s 동안 최대 ×6) + 기지 밖을 떠도는 먼지 회오리 2개(구슬 14개씩).
// 원본 index.html 761~775행. World.Start가 붙인다.
public class DustFX : MonoBehaviour
{
    const int DUST_N = 150, DEV_N = 14;
    ParticleSystem ps;
    ParticleSystem.VelocityOverLifetimeModule vol;
    float gustT = 10f, gust = 0f;

    class Devil { public Vector2 p, t; public float wait; public Transform[] beads; }
    Devil[] devils;
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
        float k = GameConfig.K;
        var go = new GameObject("dust"); go.transform.SetParent(transform, false);
        go.transform.position = WorldMap.W2U(200f, 520f, 75f);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.loop = true; main.prewarm = true; main.startLifetime = 20f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
        main.startColor = new Color(1f, 0.886f, 0.784f, 0.6f);
        main.maxParticles = DUST_N; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
        var em = ps.emission; em.rateOverTime = DUST_N / 20f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(560f, 150f, 1400f) * k;
        // 바람: x 6~16 /s, 위아래·앞뒤로 살짝. 돌풍은 speedModifier로 배속
        vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(6f * k, 16f * k); vol.y = new ParticleSystem.MinMaxCurve(-2.5f * k, 2.5f * k); vol.z = new ParticleSystem.MinMaxCurve(-3f * k, 3f * k);
        vol.speedModifier = new ParticleSystem.MinMaxCurve(1f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.12f; nz.frequency = 0.25f; nz.scrollSpeed = 0.2f; nz.quality = ParticleSystemNoiseQuality.Low;
        // 생성·소멸 때 투명 → 튀지 않게
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.material = new Material(Unlit()) { mainTexture = Soft() };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.sortingFudge = -10f;
        ps.Play();
    }

    // 회오리: 반투명 구슬이 나선 원뿔로 돌며 좌우 가장자리(x −36~−2 / 398~432)를 5~11초마다 새 목표로 20/s 속도로 떠돈다
    void BuildDevils()
    {
        // 리니어 색공간에선 밝은 반투명이 더 불투명해 보여 원본 .4보다 낮춘다
        var mat = new Material(Unlit()) { color = new Color(1f, 0.851f, 0.749f, 0.3f) };
        devils = new[] { new Devil { p = new Vector2(-30f, 300f), t = new Vector2(-30f, 300f) }, new Devil { p = new Vector2(420f, 760f), t = new Vector2(420f, 760f) } };
        foreach (var d in devils)
        {
            d.beads = new Transform[DEV_N];
            for (int i = 0; i < DEV_N; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "devil"; Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(transform, false);
                b.GetComponent<Renderer>().sharedMaterial = mat; Prim.NoShadow(b);
                b.transform.localScale = Vector3.one * (1.6f + i * 0.26f) * 2f * GameConfig.K;
                d.beads[i] = b.transform;
            }
        }
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 돌풍
        gustT -= dt;
        if (gustT <= 0f) { gustT = 14f + Random.value * 10f; gust = 2.6f; if (AudioManager.I != null) AudioManager.I.Play("whoosh", 1f, 0.6f); }
        gust = Mathf.Max(0f, gust - dt);
        float gk = gust > 0f ? 1f + 5f * Mathf.Sin(gust / 2.6f * Mathf.PI) : 1f;
        vol.speedModifier = new ParticleSystem.MinMaxCurve(gk);
        // 회오리
        float t = Time.time;
        for (int di = 0; di < devils.Length; di++)
        {
            var d = devils[di];
            d.wait -= dt;
            if (d.wait <= 0f)
            {
                d.wait = 5f + Random.value * 6f;
                bool left = Random.value < 0.5f;
                d.t = new Vector2(left ? -36f + Random.value * 34f : 398f + Random.value * 34f, -40f + Random.value * 1120f);
            }
            var dv = d.t - d.p; float L = dv.magnitude;
            if (L > 1f) d.p += dv / L * Mathf.Min(L, 20f * dt);
            for (int i = 0; i < DEV_N; i++)
            {
                float a = t * 9.1f + i * 1.15f + di * 3f, r = 2.5f + i * 1.15f, h = i * 4.6f + 2f;
                d.beads[i].position = WorldMap.W2U(d.p.x + Mathf.Cos(a) * r, d.p.y + Mathf.Sin(a) * r, h);
            }
        }
    }
}

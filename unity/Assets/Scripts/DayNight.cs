using UnityEngine;

// §7.3 낮밤 DAYK: DAY_LEN=300s, 위상 = clock%300/300. 태양색·강도, 반구광(하늘·땅), 배경을 키프레임 보간. night 값은 발광 재질용.
// 강도는 톤매핑이 없는 이 프로젝트의 기준(태양 ×0.605, 반구광 ×0.524)에 맞춰 원본 수치를 줄인다.
public class DayNight : MonoBehaviour
{
    public static DayNight I;
    public const float DAY_LEN = 300f;
    public float night;   // 0 낮 ~ 1 밤
    GameState G => GameState.I;

    struct Key { public float p; public Color sun; public float sunI; public Color sky, ground; public float hemi; public Color bg; public float night; }
    static Key K(float p, string sun, float sunI, string sky, string ground, float hemi, string bg, float night)
        => new Key { p = p, sun = GameConfig.Hex(sun), sunI = sunI, sky = GameConfig.Hex(sky), ground = GameConfig.Hex(ground), hemi = hemi, bg = GameConfig.Hex(bg), night = night };
    static readonly Key[] DAYK =
    {
        K(0.00f, "fff0dc", 1.9f,  "fff1e2", "a8482a", 1.05f, "c9552b", 0f),
        K(0.32f, "ffe6c8", 1.85f, "ffe9d4", "a8482a", 1.05f, "c9552b", 0f),
        K(0.46f, "ffb47c", 1.6f,  "ffc8a4", "8a3a24", 1.05f, "b84a2a", 0.3f),
        K(0.58f, "8fa0e8", 1.05f, "7080c8", "3a2a50", 1.2f,  "4e2436", 1f),
        K(0.78f, "8fa0e8", 1.05f, "7080c8", "3a2a50", 1.2f,  "4e2436", 1f),
        K(0.90f, "ffc4a0", 1.6f,  "ffd4bc", "8a4034", 1.05f, "b84a2a", 0.3f),
        K(1.00f, "fff0dc", 1.9f,  "fff1e2", "a8482a", 1.05f, "c9552b", 0f),
    };
    const float SUN_SCALE = 1.15f / 1.9f, HEMI_SCALE = 0.55f / 1.05f;
    static readonly Color PRESTIGE_SKY = GameConfig.Hex("6fa6d8");
    int lastDay = -1;

    void Awake() { I = this; }

    void Update()
    {
        var S = G.S; float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        S.clock += dt;
        int day = Mathf.FloorToInt(S.clock / DAY_LEN) + 1;
        if (day != S.day || lastDay < 0)
        {
            if (lastDay >= 0 && day > S.day && Player.I != null) { WorldLabels.I.Float(Player.I.view.position + Vector3.up * 3f, "☀ " + day + "일째", true, true); AudioManager.I.Play("ding", 0.9f, 0.5f); }
            S.day = day; lastDay = day;
        }
        float p = (S.clock % DAY_LEN) / DAY_LEN;
        int i = 0; while (i < DAYK.Length - 2 && DAYK[i + 1].p <= p) i++;
        var a = DAYK[i]; var b = DAYK[i + 1]; float k = Mathf.InverseLerp(a.p, b.p, p);
        night = Mathf.Lerp(a.night, b.night, k);
        if (World.sun != null) { World.sun.color = Color.Lerp(a.sun, b.sun, k); World.sun.intensity = Mathf.Lerp(a.sunI, b.sunI, k) * SUN_SCALE; }
        float hemi = Mathf.Lerp(a.hemi, b.hemi, k) * HEMI_SCALE;
        var sky = Color.Lerp(a.sky, b.sky, k); var ground = Color.Lerp(a.ground, b.ground, k);
        RenderSettings.ambientSkyColor = sky * hemi;
        RenderSettings.ambientEquatorColor = Color.Lerp(sky, ground, 0.5f) * hemi;
        RenderSettings.ambientGroundColor = ground * hemi;
        if (CameraRig.I != null)
        {
            var bg = Color.Lerp(a.bg, b.bg, k);
            // §6.3 프레스티지 영구 보너스: 이주할 때마다 하늘이 푸른 쪽(0x6fa6d8)으로 min(0.5, 0.1*prestige)만큼 (원본 395행, 보간 결과의 마지막 단계)
            if (S.prestige > 0) bg = Color.Lerp(bg, PRESTIGE_SKY, Mathf.Min(0.5f, 0.1f * S.prestige));
            CameraRig.I.cam.backgroundColor = bg;
        }
    }
}

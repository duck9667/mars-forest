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
    // 밤 색조(다듬기 3차): 달빛 a8b6ec·하늘 8e9cd8은 하늘색 물건(얼음·카운터 물 칸)을 진한 파랑으로 밀어 올려 튀었다 → 채도를 뺀 라벤더 회색(cec6da·b2a8c2)
    // 밤(0.58~0.78): 예전 태양 8fa0e8×1.05·땅빛 3a2a50은 화면이 탁한 남보라로 가라앉아(2026-10-25 다듬기) 달빛을 밝히고 땅빛을 보라 기 도는 밤색으로 올렸다
    static readonly Key[] DAYK =
    {
        K(0.00f, "fff0dc", 1.9f,  "fff1e2", "a8482a", 1.05f, "c9552b", 0f),
        K(0.32f, "ffe6c8", 1.85f, "ffe9d4", "a8482a", 1.05f, "c9552b", 0f),
        K(0.46f, "ffb47c", 1.6f,  "ffc8a4", "8a3a24", 1.05f, "b84a2a", 0.3f),
        K(0.58f, "cec6da", 1.4f,  "b2a8c2", "6c4a62", 1.3f,  "4e2436", 1f),
        K(0.78f, "cec6da", 1.4f,  "b2a8c2", "6c4a62", 1.3f,  "4e2436", 1f),
        K(0.90f, "ffc4a0", 1.6f,  "ffd4bc", "8a4034", 1.05f, "b84a2a", 0.3f),
        K(1.00f, "fff0dc", 1.9f,  "fff1e2", "a8482a", 1.05f, "c9552b", 0f),
    };
    const float SUN_SCALE = 1.15f / 1.9f, HEMI_SCALE = 0.55f / 1.05f;
    static readonly Color PRESTIGE_SKY = GameConfig.Hex("6fa6d8");
    // 보조광(World.fill): 낮엔 하늘색 9fc6ff 그대로, 밤엔 달빛이 약해져 이 하늘색이 주광처럼 남아 물 칸·물병이 진한 파랑으로 튀었다 → 밤엔 연보라 회색으로
    // 밤에 채도를 덜어 낼 공용 재질(물 6fc3f0·카운터 물 칸 7fc3e8): 낮의 따뜻한 햇빛이 하늘색을 부드럽게 눌러 주던 몫을 밤엔 재질 색으로 대신한다.
    // Mats.Solid 캐시는 색별 공유 재질이라 이 색을 쓰는 물건(물병 더미·탱크 물·칸 받침) 전체에 한 번에 걸린다
    static readonly Color[] SOFT_DAY = { GameConfig.Hex("6fc3f0"), GameConfig.Hex("7fc3e8") };
    static readonly Color[] SOFT_NIGHT = { GameConfig.Hex("a6c6d8"), GameConfig.Hex("aac8da") };
    float lastSoft = -1f;
    static readonly Color FILL_DAY = GameConfig.Hex("9fc6ff"), FILL_NIGHT = GameConfig.Hex("c6bccf");
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
        if (Mathf.Abs(night - lastSoft) > 0.01f)
        {
            lastSoft = night;
            Shader.SetGlobalFloat("_MarsNight", night);   // 정점 색 모형(Mars/VColor: 물병·얼음 등)도 밤엔 같은 몫만큼 채도를 덜고 제 색 발광을 끈다
            for (int si = 0; si < SOFT_DAY.Length; si++) Mats.Solid(SOFT_DAY[si]).color = Color.Lerp(SOFT_DAY[si], SOFT_NIGHT[si], night);
        }
        if (World.fill != null) World.fill.color = Color.Lerp(FILL_DAY, FILL_NIGHT, night);
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

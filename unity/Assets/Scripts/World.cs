using UnityEngine;

// §4·§7.2 환경: 바닥·줄 난간·먼지·테두리(메사)·조명. Phase 1은 낮밤 없음(정오 고정).
public class World : MonoBehaviour
{
    void Start()
    {
        // 바닥(여유 폭 포함): 원본처럼 코드로 그린 텍스처(흙·균열·크레이터·바퀴 자국·손님 길 점선·착륙장 원과 H). GroundPainter 참고.
        GroundPainter.GroundQuad(transform);
        // 구역 바닥·울타리는 ZoneManager가 만든다 (해제 연출 때문에 부품 단위 관리)
        // 줄 난간 qL/qR: x=186/246, y 570~676 레일
        Rail(186, 570, 676); Rail(246, 570, 676);
        // 착륙장 원 r58(입구)은 지면 텍스처에 그려져 있다
        // 환경 연출: 가장자리에만 옅은 먼지 + 가끔 지나가는 먼지 회오리 (DustFX)
        new GameObject("DustFX").AddComponent<DustFX>().transform.SetParent(transform, false);
        // 테두리: 메사 절벽 + 포인트 바위 (WorldDress)
        var rocks = new GameObject("rocks").transform; rocks.SetParent(transform, false);
        Lights();
        WorldDress.Dress(transform, rocks);
    }

    void Rail(float x, float y0, float y1)
    {
        var c = GameConfig.Hex("8a8fa8");
        for (float y = y0; y <= y1; y += 53) Prim.Box(transform, "qpost", WorldMap.W2U(x, y, 10), new Vector3(4, 20, 4), c);
        Prim.Box(transform, "qrail", WorldMap.W2U(x, (y0 + y1) / 2, 18), new Vector3(2.5f, 2.5f, y1 - y0), c);
    }

    public static Light sun, fill;   // DayNight이 색·강도를 보간한다
    void Lights()
    {
        sun = new GameObject("sun").AddComponent<Light>();
        // 원본 1.9는 ACES 톤매핑 전제. 톤매핑 없는 Phase 1은 과노출을 피하려고 낮춘다.
        sun.type = LightType.Directional; sun.color = GameConfig.Hex("fff0dc"); sun.intensity = 1.15f;
        sun.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.75f, 0.8f, -0.55f).normalized, Vector3.up);
        sun.shadows = LightShadows.Soft; sun.shadowBias = 0.02f; sun.shadowNormalBias = 0.6f;
        fill = new GameObject("fill").AddComponent<Light>();
        fill.type = LightType.Directional; fill.color = GameConfig.Hex("9fc6ff"); fill.intensity = 0.2f; fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.LookRotation(-new Vector3(600, 500, 400).normalized, Vector3.up);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = GameConfig.Hex("fff1e2") * 0.55f;
        RenderSettings.ambientEquatorColor = Color.Lerp(GameConfig.Hex("fff1e2"), GameConfig.Hex("a8482a"), 0.5f) * 0.55f;
        RenderSettings.ambientGroundColor = GameConfig.Hex("a8482a") * 0.55f;
        QualitySettings.shadowDistance = 200f; // 직교 카메라 거리 90 + 월드 반경
        QualitySettings.shadowResolution = ShadowResolution.High;
    }
}

using UnityEngine;

// §7.1 직교 카메라: 가로 반폭 210 고정, cam.x=200 고정, y는 플레이어를 lead 보정으로 추적, 흔들림 감쇠 exp(-7dt)
public class CameraRig : MonoBehaviour
{
    public static CameraRig I;
    public Camera cam;
    public Transform target;          // 플레이어 뷰
    public System.Func<Vector2> velL;  // 논리 속도
    public float camY = 450f, lead = 0f, shakeA = 0f;
    static readonly Vector3 DIR = new Vector3(0.07f, 1.43f, -1f).normalized;
    const float DIST = 1800f * GameConfig.K;

    void Awake()
    {
        I = this;
        cam = GetComponent<Camera>();
        if (cam == null) cam = gameObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.nearClipPlane = 0.3f; cam.farClipPlane = DIST * 2.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = GameConfig.BG;
        gameObject.AddComponent<AudioListener>();
        gameObject.AddComponent<PostFX>();   // §7.2 외곽선·ACES·비네트·블룸(깊이 텍스처는 PostFX 가 켠다)
        camY = GameState.I ? GameState.I.S.p.y : 450f;
    }

    public void Shake(float a) { shakeA = Mathf.Max(shakeA, a); }

    public float ViewH => 2f * cam.orthographicSize / GameConfig.K / DIR.y;   // 직교 높이를 지면 투영 길이로(고도 ≈55°, DIR.y=0.819)

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        cam.orthographicSize = 210f * GameConfig.K * (Screen.height / (float)Screen.width);
        float viewH = ViewH;
        if (target != null)
        {
            var pl = WorldMap.U2W(target.position);
            float vy = velL != null ? velL().y : 0f;
            lead = Mathf.Lerp(lead, Mathf.Clamp(vy * 0.22f, -50f, 50f), dt * 3f);
            float ty = pl.y + lead + viewH * 0.04f;
            ty = Mathf.Clamp(ty, viewH * 0.5f - 80f, GameConfig.H - viewH * 0.5f + 50f);
            camY = Mathf.Lerp(camY, ty, dt * 5f);
        }
        shakeA *= Mathf.Exp(-7f * dt);
        if (shakeA < 0.05f) shakeA = 0;
        Vector3 t = WorldMap.W2U(200f, camY, 0f);
        t += new Vector3(Random.Range(-shakeA, shakeA), 0, Random.Range(-shakeA, shakeA)) * GameConfig.K;
        transform.position = t + DIR * DIST;
        transform.LookAt(t);
    }
}

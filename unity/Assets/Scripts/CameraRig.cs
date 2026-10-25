using UnityEngine;

// §7.1 직교 카메라: 가로 반폭 210/zoom, y는 플레이어를 lead 보정으로 추적(북쪽 생존 기지 Y_MIN까지), 흔들림 감쇠 exp(-7dt)
// 확대(실제 배율 1~2.2, 기본 1.4 = ZOOM_BASE, S.zoom은 기본 대비 배율로 저장): 폰은 두 손가락 핀치, 맥은 마우스 휠. 확대 중엔 x도 플레이어를 따라가되 맵(0~400) 밖은 덜 보이게 클램프. 검증 인자 -zoom f
public class CameraRig : MonoBehaviour
{
    public static CameraRig I;
    public Camera cam;
    public Transform target;          // 플레이어 뷰
    public System.Func<Vector2> velL;  // 논리 속도
    public float camY = 450f, camX = 200f, lead = 0f, shakeA = 0f;
    public const float ZOOM_MIN = 1f, ZOOM_MAX = 2.2f;   // 실제 배율 범위(1 = 예전 기본 화면, 핀치로 가장 넓게)
    // 기본 배율 1.4(2026-10-25). 저장값 S.zoom은 "기본 대비 배율"로 읽는다: 저장 1(옛 기본·새 저장 기본) → 실제 1.4
    public const float ZOOM_BASE = 1.4f;
    // 아래 여유(논리): 화면 아래 업그레이드 버튼(화면 높이 약 12%)이 착륙장·본부를 가리지 않게 맵 끝(H)보다 이만큼 더 내려간다(예전 50)
    const float BOTTOM_PAD = 130f;
    static bool zoomArgDone;   // -zoom 검증 인자는 실행당 1회(Restart 뒤 유지)
    bool snapped;              // 첫 추적 프레임에 바로 맞췄는지
    float pinchD = -1f;        // 직전 프레임 두 손가락 거리(픽셀), -1이면 핀치 아님
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
        camX = 200f;
        if (!zoomArgDone && GameState.I != null)
        {
            zoomArgDone = true;
            var z = DevArgs.Get("-zoom");
            if (z != null && float.TryParse(z, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var zf)) Zoom = zf;   // 검증 인자는 실제 배율
        }
    }

    public float Zoom
    {
        get { var g = GameState.I; return Mathf.Clamp((g != null && g.S.zoom > 0 ? g.S.zoom : 1f) * ZOOM_BASE, ZOOM_MIN, ZOOM_MAX); }
        set { var g = GameState.I; if (g != null) g.S.zoom = Mathf.Clamp(value, ZOOM_MIN, ZOOM_MAX) / ZOOM_BASE; }
    }

    // 핀치·휠 입력. 시트·모달이 떠 있으면(조이스틱 suppressed) 확대하지 않는다(시트 스크롤과 안 섞이게)
    void HandleZoomInput()
    {
        bool blocked = FloatingJoystick.I != null && FloatingJoystick.I.suppressed;
        if (Input.touchCount >= 2 && !blocked)
        {
            var a = Input.GetTouch(0); var b = Input.GetTouch(1);
            float d = Vector2.Distance(a.position, b.position);
            if (pinchD > 0f && d > 1f) Zoom = Zoom * (d / pinchD);
            pinchD = d;
        }
        else pinchD = -1f;
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && !blocked && Input.touchCount == 0) Zoom = Zoom * Mathf.Pow(1.1f, Mathf.Clamp(wheel, -5f, 5f));
    }

    public void Shake(float a) { shakeA = Mathf.Max(shakeA, a); }

    public float ViewH => 2f * cam.orthographicSize / GameConfig.K / DIR.y;   // 직교 높이를 지면 투영 길이로(고도 ≈55°, DIR.y=0.819)

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        HandleZoomInput();
        float zoom = Zoom, halfW = 210f / zoom;
        cam.orthographicSize = halfW * GameConfig.K * (Screen.height / (float)Screen.width);
        float viewH = ViewH;
        if (target != null)
        {
            var pl = WorldMap.U2W(target.position);
            float vy = velL != null ? velL().y : 0f;
            lead = Mathf.Lerp(lead, Mathf.Clamp(vy * 0.22f, -50f, 50f), dt * 3f);
            float ty = pl.y + lead + viewH * 0.04f;
            // 세로 제한: 위는 놀이 경계(Y_MIN+6)보다 170 바깥(북쪽 메사 띠 위)까지, 아래는 착륙장 아래 BOTTOM_PAD까지.
            // 2장 개척지(y −620~−410)는 맨 위라 예전 110이면 화면 위 1/3에 몰려 HUD에 가렸다 → 170으로 늘려 개척지가 화면 가운데 가까이 오게
            ty = Mathf.Clamp(ty, GameConfig.Y_MIN - 170f + viewH * 0.5f, GameConfig.H - viewH * 0.5f + BOTTOM_PAD);
            camY = snapped ? Mathf.Lerp(camY, ty, dt * 5f) : ty;
            // x: 기본(zoom 1)은 200 고정, 확대하면 플레이어를 따라가되 맵 가장자리(0~400) 밖은 10만큼만 보이게
            float minX = halfW - 10f, maxX = 400f - halfW + 10f;
            float tx = minX >= maxX ? 200f : Mathf.Clamp(pl.x, minX, maxX);
            camX = snapped ? Mathf.Lerp(camX, tx, dt * 5f) : tx;
            snapped = true;   // 첫 프레임은 플레이어 자리로 바로(저장 위치·-start 검증 인자가 Awake 뒤에 바뀌어도 화면이 미끄러져 오지 않게)
        }
        shakeA *= Mathf.Exp(-7f * dt);
        if (shakeA < 0.05f) shakeA = 0;
        Vector3 t = WorldMap.W2U(camX, camY, 0f);
        t += new Vector3(Random.Range(-shakeA, shakeA), 0, Random.Range(-shakeA, shakeA)) * GameConfig.K;
        transform.position = t + DIR * DIST;
        transform.LookAt(t);
    }
}

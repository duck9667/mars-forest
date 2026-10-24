using UnityEngine;
using UnityEngine.UI;

// §2.1 플로팅 조이스틱: 아무 곳 터치에 베이스 표시, 지름 120/50, 최대 반경 45, 데드존 6, 아날로그 출력.
// 출력 Vector2는 논리 좌표 방향(화면 아래 = +y). 키보드 WASD/화살표는 정규화 벡터.
// 두 번째 손가락이 닿으면 조이스틱을 끊고(핀치 확대는 CameraRig) 손가락을 모두 뗄 때까지 다시 잡지 않는다.
public class FloatingJoystick : MonoBehaviour
{
    public static FloatingJoystick I;
    public Vector2 Value { get; private set; }
    public bool Active { get; private set; }
    public bool suppressed;   // 시트 열림 등
    public static Vector2? Override; // 자동 플레이 봇(DevTools) 입력

    Canvas canvas; RectTransform baseRt, knobRt; Image baseImg, knobImg;
    int pointerId = -99; Vector2 origin;
    bool pinching;   // 핀치 중(두 손가락 이상 닿은 뒤 아직 다 떼지 않음)

    void Awake()
    {
        I = this;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 8;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        // 원본 #joy(반투명 흰 원 + 흰 테두리) · #knob(흰 그라데이션 + 남색 테두리 + 그림자). 스프라이트는 UIKit이 그린다
        baseRt = MakeCircle("base", GameConfig.JOY_BASE, UIKit.JoyBase(), out baseImg);
        knobRt = MakeCircle("knob", GameConfig.JOY_KNOB, UIKit.JoyKnob(), out knobImg);
        Show(false);
    }

    RectTransform MakeCircle(string n, float d, Sprite sp, out Image img)
    {
        var go = new GameObject(n); go.transform.SetParent(transform, false);
        img = go.AddComponent<Image>(); img.color = Color.white; img.raycastTarget = false;
        img.sprite = sp; img.preserveAspect = true;
        var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(d, d * sp.rect.height / sp.rect.width) * Dpi();
        return rt;
    }
    static float Dpi() => Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 160f : 1f, 1f, 3f);

    static Sprite _circle;
    public static Sprite CircleSprite()
    {
        if (_circle != null) return _circle;
        int s = 64; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1 - d) * s / 2f)));
            }
        tex.Apply();
        _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return _circle;
    }

    void Show(bool v) { baseImg.enabled = v; knobImg.enabled = v; }

    void Update()
    {
        Vector2 kb = Vector2.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) kb.y -= 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) kb.y += 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) kb.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) kb.x += 1;

        if (suppressed) { pointerId = -99; Show(false); Active = false; Value = kb.normalized; return; }
        if (Override.HasValue) { Value = Override.Value; return; }

        // 핀치: 두 번째 손가락이 닿는 순간 조이스틱을 끊고, 손가락을 다 뗄 때까지 무시
        if (Input.touchCount >= 2) pinching = true;
        else if (Input.touchCount == 0) pinching = false;
        if (pinching) { pointerId = -99; Show(false); Active = false; Value = kb.normalized; return; }

        // 터치 우선, 없으면 마우스
        bool down = false, held = false; Vector2 pos = Vector2.zero;
        if (Input.touchCount > 0)
        {
            if (pointerId == -99)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began && !HUD.IsPointerOverUI(t.position)) { pointerId = t.fingerId; down = true; pos = t.position; }
            }
            else
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.fingerId == pointerId) { held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled; pos = t.position; }
                }
            }
        }
        else
        {
            if (pointerId == -99 && Input.GetMouseButtonDown(0) && !HUD.IsPointerOverUI(Input.mousePosition)) { pointerId = -1; down = true; pos = Input.mousePosition; }
            else if (pointerId == -1) { held = Input.GetMouseButton(0); pos = Input.mousePosition; }
        }

        if (down) { origin = pos; baseRt.position = origin; knobRt.position = origin; Show(true); Active = true; held = true; }
        if (pointerId != -99 && !held) { pointerId = -99; Show(false); Active = false; Value = kb.normalized; return; }
        if (Active)
        {
            Vector2 d = pos - origin; float max = GameConfig.JOY_MAX * Dpi();
            if (d.magnitude > max) d = d.normalized * max;
            knobRt.position = origin + d;
            if (d.magnitude < GameConfig.JOY_DEAD * Dpi()) Value = Vector2.zero;
            else Value = new Vector2(d.x / max, -d.y / max);  // 화면 y↑ → 논리 y↓ 반전
        }
        else Value = kb.normalized;
    }
}

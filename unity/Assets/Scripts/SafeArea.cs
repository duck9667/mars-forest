using UnityEngine;

// 아이폰 다이내믹 아일랜드·홈 인디케이터를 피하는 세이프 에어리어 루트(jake 10/26 실기기 설치).
// HUD 캔버스 아래 "safe" 사각형을 Screen.safeArea에 맞추고, 화면 위·아래에 붙은 HUD 요소(벨·지도·톱니·업그레이드 버튼·무전 카드·토스트 등)를
// 생기는 대로 그 안으로 옮긴다. 화면 전체를 덮는 요소(번쩍임·가림막, 앵커 0~1)는 노치까지 덮어야 하니 그대로 둔다.
public class SafeArea : MonoBehaviour
{
    RectTransform safe; Rect last;
    public RectTransform Root => safe;

    void Awake()
    {
        safe = new GameObject("safe", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(transform, false);
        safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
        Fit();
    }

    void Fit()
    {
        Rect r = Screen.safeArea; last = r;
        // QA: -safetest 이면 iPhone 17 비율의 위 7%·아래 4% 여백을 흉내(맥엔 노치가 없어서)
        if (DevArgs.Has("-safetest")) r = new Rect(0, Screen.height * 0.04f, Screen.width, Screen.height * 0.89f);
        if (Screen.width <= 0 || Screen.height <= 0) return;
        safe.anchorMin = new Vector2(r.xMin / Screen.width, r.yMin / Screen.height);
        safe.anchorMax = new Vector2(r.xMax / Screen.width, r.yMax / Screen.height);
    }

    void LateUpdate()
    {
        if (Screen.safeArea != last) Fit();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (!(transform.GetChild(i) is RectTransform c) || c == safe) continue;
            if (c.anchorMin == Vector2.zero && c.anchorMax == Vector2.one) continue;   // 화면 전체 덮개
            c.SetParent(safe, false);
        }
    }

    // 이름으로 HUD 요소 찾기(세이프 루트로 옮겨졌어도)
    public static Transform Find(Transform hud, string name) => hud.Find(name) ?? hud.Find("safe/" + name);
}

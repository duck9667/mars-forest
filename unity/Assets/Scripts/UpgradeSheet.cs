using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// §3.3 하단 업그레이드 시트: 스탯 3줄(hand·spd·price, 레벨 핍) + 시설 카드(노출 중 → 구매 가능 → 잠김 → MAX). Phase 1: 탭 없이 업그레이드만.
public class UpgradeSheet : MonoBehaviour
{
    public static UpgradeSheet I;
    GameState G => GameState.I;
    Canvas canvas; RectTransform panel; Image dim; Transform list; bool open;

    void Awake()
    {
        I = this;
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 12;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(540, 1170); sc.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        dim = HUD.UI.Panel(transform, "dim", new Color(0, 0, 0, 0.35f));
        var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var p = HUD.UI.Panel(transform, "panel", GameConfig.Hex("fff6e6"));
        panel = p.rectTransform; panel.anchorMin = new Vector2(0, 0); panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(0.5f, 0);
        panel.offsetMin = new Vector2(0, 0); panel.offsetMax = new Vector2(0, 0); panel.sizeDelta = new Vector2(0, 760);
        var title = HUD.UI.Label(p.transform, "업그레이드", 32, GameConfig.NAVY, TextAnchor.UpperCenter);
        HUD.UI.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(400, 44));
        var close = HUD.UI.Btn(p.transform, "닫기", new Vector2(100, 48), GameConfig.NAVY, Color.white, 22, Close);
        HUD.UI.Anchor(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(100, 48));
        var lgo = new GameObject("list"); lgo.transform.SetParent(p.transform, false);
        var lrt = lgo.AddComponent<RectTransform>(); lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1); lrt.offsetMin = new Vector2(18, 18); lrt.offsetMax = new Vector2(-18, -84);
        var vl = lgo.AddComponent<VerticalLayoutGroup>(); vl.spacing = 10; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
        list = lgo.transform;
        gameObject.SetActive(false);
    }

    void Start() { G.OnBellsChanged += () => { if (open) Rebuild(); }; }

    public void Toggle() { if (open) Close(); else Open(); }
    public void Open()
    {
        open = true; gameObject.SetActive(true);
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
        Rebuild(); AudioManager.I.Play("pop", 1.2f, 0.5f);
        StartCoroutine(Slide(true));
    }
    public void Close()
    {
        if (!open) return; open = false;
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = false;
        StartCoroutine(Slide(false));
    }
    IEnumerator Slide(bool up)
    {
        float el = 0, h = panel.sizeDelta.y;
        while (el < 0.22f)
        {
            el += Time.unscaledDeltaTime; float k = Mathf.Clamp01(el / 0.22f); float e = up ? JuiceFX.EaseOutBack(k, 0.8f) : 1 - k * k;
            panel.anchoredPosition = new Vector2(0, -h * (1 - e)); yield return null;
        }
        panel.anchoredPosition = up ? Vector2.zero : new Vector2(0, -h);
        if (!up) gameObject.SetActive(false);
    }

    void Rebuild()
    {
        foreach (Transform c in list) Destroy(c.gameObject);
        // 스탯 3줄
        foreach (var d in Pads.ALL.Where(p => p.active && p.menu)) Row(d, true);
        // 시설 카드 정렬: 노출 중 → 구매 가능 → 잠김 → MAX
        int Rank(PadDef d) => G.PadMaxed(d.id) ? 3 : !G.PadReq(d.id) ? 2 : PadManager.I.visSet.Contains(d.id) ? 0 : 1;
        foreach (var d in Pads.ALL.Where(p => p.active && !p.menu).OrderBy(Rank)) Row(d, false);
    }

    void Row(PadDef d, bool stat)
    {
        int lv = G.PadLv(d.id); bool maxed = G.PadMaxed(d.id), req = G.PadReq(d.id);
        bool vis = !stat && PadManager.I.visSet.Contains(d.id);
        var row = HUD.UI.Panel(list, d.id, vis ? GameConfig.Hex("fff0c2") : Color.white);
        var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 84;
        if (vis) { var ol = row.gameObject.AddComponent<Outline>(); ol.effectColor = GameConfig.GOLD; ol.effectDistance = new Vector2(3, -3); }
        var name = HUD.UI.Label(row.transform, d.name, 26, GameConfig.NAVY, TextAnchor.MiddleLeft);
        HUD.UI.Anchor(name.rectTransform, new Vector2(0, 0.5f), new Vector2(18, 12), new Vector2(260, 36));
        string pips = ""; for (int i = 0; i < d.max; i++) pips += i < lv ? "●" : "○";
        var sub = HUD.UI.Label(row.transform, maxed ? "MAX" : pips, 20, GameConfig.Hex("7a7f99"), TextAnchor.MiddleLeft, false);
        HUD.UI.Anchor(sub.rectTransform, new Vector2(0, 0.5f), new Vector2(18, -16), new Vector2(260, 30));
        string label; Color bg = GameConfig.GOLD; bool can = false;
        if (maxed) { label = "완료"; bg = GameConfig.Hex("d9d9d9"); }
        else if (!req) { label = "잠김 · " + d.reqText; bg = GameConfig.Hex("d9d9d9"); }
        else { float rem = G.PadRemain(d.id); label = Mathf.CeilToInt(rem) + " 벨"; can = G.S.bells >= rem; if (!can) bg = GameConfig.Hex("e8d9b0"); }
        var btn = HUD.UI.Btn(row.transform, label, new Vector2(180, 56), bg, GameConfig.NAVY, 22, () => OnBuy(d, stat));
        HUD.UI.Anchor(btn.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(180, 56));
        btn.interactable = !maxed && req;
    }

    void OnBuy(PadDef d, bool stat)
    {
        if (!PadManager.I.Buy(d.id)) return;
        if (stat)
        {
            // 시트 닫고 HUD→플레이어 동전 6개
            Close();
            var cam = Camera.main;
            HUD.I.Coins(HUD.I.BellsScreen(), () => cam.WorldToScreenPoint(Player.I.body.position + Vector3.up * 1.2f), 6);
            if (d.id == "hand" || d.id == "spd") JuiceFX.I.Squash(Player.I.body, 1.2f);
        }
        else
        {
            HUD.I.Coins(HUD.I.UpBtnScreen(), () => HUD.I.BellsScreen(), 6);
            Rebuild();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// §3.3 하단 업그레이드 시트(원본 #sheet/.row/.buy): 스탯 3줄(hand·spd·price, 레벨 핍) + 시설 카드(노출 중 → 구매 가능 → 잠김 → MAX). Phase 1: 탭 없이 업그레이드만.
// 구역 발판(zone)은 월드 발판에서만 해제하므로 시트에 안 올린다. 갱신은 dirty 플래그 → LateUpdate에서 프레임당 1회(납입 중 OnBellsChanged 매 프레임 발화 대비).
public class UpgradeSheet : MonoBehaviour
{
    public static UpgradeSheet I;
    GameState G => GameState.I;
    Canvas canvas; RectTransform panel; Image dim; Transform list; bool open, dirty; Coroutine slide;
    const float H = 760f;

    class Row { public PadDef d; public bool stat; public Image bg; public Image tile; public Transform pips; public Button buy; public int lv = -1; public bool maxed, req, can, vis; public string label; }
    readonly List<Row> rows = new List<Row>();

    void Awake()
    {
        I = this;
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 12;
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(540, 1170); sc.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        dim = HUD.UI.Panel(transform, "dim", UIKit.SCRIM);
        var drt = dim.rectTransform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        // 시트 패널(남색, 위쪽만 둥글게)
        var p = UIKit.Panel(transform, "panel", UIKit.Sheet); p.raycastTarget = true;
        panel = p.rectTransform; panel.anchorMin = new Vector2(0, 0); panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(0.5f, 0);
        panel.offsetMin = new Vector2(0, 0); panel.offsetMax = new Vector2(0, 0); panel.sizeDelta = new Vector2(0, H);
        // 손잡이 · 제목 · 닫기
        var grip = UIKit.Panel(p.transform, "grip", UIKit.Track); UIKit.Anchor(grip.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(44, 6)); grip.color = UIKit.Hex("4a6190");
        var title = UIKit.Text(p.transform, "업그레이드", 34, Color.white); UIKit.Stroke(title, UIKit.EDGE, 2.2f);
        UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(400, 48));
        var close = UIKit.RoundButton(p.transform, "close", UIKit.Gray, 48, "close", 26, Close, out _);
        var crt = close.GetComponent<RectTransform>(); crt.anchorMin = crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1); crt.anchoredPosition = new Vector2(-16, -16);
        // 목록: 뷰포트(마스크·세로 스크롤) 안에 세로 레이아웃 콘텐츠. 행이 시트 높이를 넘치면 드래그로 스크롤
        var view = UIKit.RT(p.transform, "viewport"); view.anchorMin = new Vector2(0, 0); view.anchorMax = new Vector2(1, 1); view.offsetMin = new Vector2(16, 18); view.offsetMax = new Vector2(-16, -84);
        var vimg = view.gameObject.AddComponent<Image>(); vimg.color = Color.clear; vimg.raycastTarget = true;   // 빈 자리 드래그도 받도록
        view.gameObject.AddComponent<RectMask2D>();
        var lrt = UIKit.RT(view, "list"); lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 1); lrt.offsetMin = new Vector2(0, 0); lrt.offsetMax = new Vector2(0, 0);
        var vl = lrt.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 8; vl.padding = new RectOffset(2, 2, 2, 8); vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
        var lf = lrt.gameObject.AddComponent<ContentSizeFitter>(); lf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = lrt; sr.viewport = view; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30; sr.decelerationRate = 0.12f;
        list = lrt;
        gameObject.SetActive(false);
    }

    void Start()
    {
        G.OnBellsChanged += () => { if (open) dirty = true; };
        G.OnPadLevel += (a, b) => { if (open) dirty = true; };
        // (-sheet QA 자동 열기는 HUD.Start에서: 이 오브젝트는 Awake 끝에 비활성화돼 첫 Open 전엔 Start·코루틴이 안 돈다)
    }

    void LateUpdate() { if (open && dirty) { dirty = false; Rebuild(); } }

    public void Toggle() { if (open) Close(); else Open(); }
    public void Open()
    {
        open = true; gameObject.SetActive(true);
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
        Rebuild(); AudioManager.I.Play("pop", 1.2f, 0.5f);
        if (slide != null) StopCoroutine(slide);
        slide = StartCoroutine(Slide(true));
    }
    public void Close()
    {
        if (!open) return; open = false;
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = false;
        if (slide != null) StopCoroutine(slide);
        slide = StartCoroutine(Slide(false));
    }
    IEnumerator Slide(bool up)
    {
        float el = 0, h = panel.sizeDelta.y;
        float from = panel.anchoredPosition.y, to = up ? 0 : -h;
        while (el < 0.22f)
        {
            el += Time.unscaledDeltaTime; float k = Mathf.Clamp01(el / 0.22f); float e = up ? JuiceFX.EaseOutBack(k, 0.8f) : 1 - (1 - k) * (1 - k);
            panel.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(from, to, e)); yield return null;
        }
        panel.anchoredPosition = new Vector2(0, to);
        slide = null;
        if (!up) gameObject.SetActive(false);
    }

    // 행 목록(정렬 포함)을 만들고, 이미 있는 행은 상태만 갱신한다. 순서·구성이 바뀌면 전부 다시 만든다.
    void Rebuild()
    {
        var want = new List<PadDef>();
        foreach (var d in Pads.ALL.Where(p => p.active && p.menu)) want.Add(d);
        int Rank(PadDef d) => G.PadMaxed(d.id) ? 3 : !G.PadReq(d.id) ? 2 : PadManager.I.visSet.Contains(d.id) ? 0 : 1;
        foreach (var d in Pads.ALL.Where(p => p.active && !p.menu && !p.zone).OrderBy(Rank)) want.Add(d);
        bool same = rows.Count == want.Count; if (same) for (int i = 0; i < rows.Count; i++) if (rows[i].d != want[i]) { same = false; break; }
        if (!same)
        {
            foreach (Transform c in list) Destroy(c.gameObject);
            rows.Clear();
            foreach (var d in want) rows.Add(MakeRow(d, d.menu));
            ((RectTransform)list).anchoredPosition = Vector2.zero;   // 구성이 바뀌면 맨 위로
        }
        foreach (var r in rows) UpdateRow(r);
    }

    Row MakeRow(PadDef d, bool stat)
    {
        var r = new Row { d = d, stat = stat };
        r.bg = UIKit.Panel(list, d.id, UIKit.Card); r.bg.raycastTarget = true;   // 행 위 드래그가 ScrollRect로 올라가도록
        var le = r.bg.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 88;
        // 아이콘 타일(크림) + 발판 아이콘
        r.tile = UIKit.Panel(r.bg.transform, "tile", UIKit.Tile); UIKit.Anchor(r.tile.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 1), new Vector2(60, 63));
        var ic = UIKit.IconImg(r.tile.transform, UIKit.PadIcon(d.id), 40); var irt = ic.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f); irt.anchoredPosition = new Vector2(0, 1.5f);
        // 이름 · 레벨 핍
        var name = UIKit.Text(r.bg.transform, d.name, 24, Color.white, TextAnchor.MiddleLeft);
        UIKit.Anchor(name.rectTransform, new Vector2(0, 0.5f), new Vector2(88, 14), new Vector2(240, 32));
        r.pips = UIKit.Pips(r.bg.transform, 0, d.max);
        var prt = r.pips.GetComponent<RectTransform>(); UIKit.Anchor(prt, new Vector2(0, 0.5f), new Vector2(88, -15), new Vector2(240, 12));
        var fit = r.pips.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        // 구매 버튼(파랑 [벨] 가격 / 회색 잠김 / 금색 MAX)
        r.buy = UIKit.Button(r.bg.transform, "buy", UIKit.Blue, new Vector2(164, 56), "", 24, () => OnBuy(d, stat), "bell", 26);
        UIKit.Anchor(r.buy.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-14, 2), new Vector2(164, 56));
        return r;
    }

    void UpdateRow(Row r)
    {
        var d = r.d; int lv = G.PadLv(d.id); bool maxed = G.PadMaxed(d.id), req = G.PadReq(d.id);
        bool vis = !r.stat && PadManager.I.visSet.Contains(d.id);
        float rem = maxed || !req ? 0 : G.PadRemain(d.id); bool can = req && !maxed && G.S.bells >= rem;
        string label = maxed ? "MAX" : !req ? "잠김 · " + d.reqText : Mathf.CeilToInt(rem).ToString("N0");
        if (lv != r.lv)
        {
            // 핍 다시 칠하기
            int i = 0; foreach (Transform p in r.pips) { var img = p.GetComponent<Image>(); bool on = i < lv; img.sprite = UIKit.Panel9(on ? UIKit.PipOn : UIKit.PipOff); i++; }
        }
        if (vis != r.vis || maxed != r.maxed) { r.bg.sprite = UIKit.Panel9(vis ? UIKit.CardCur : maxed ? UIKit.CardDone : UIKit.Card); r.tile.sprite = UIKit.Panel9(maxed || !req ? UIKit.TileDark : UIKit.Tile); }
        if (req != r.req || maxed != r.maxed || can != r.can || label != r.label)
        {
            var st = maxed ? UIKit.Gold : !req ? UIKit.Gray : can ? UIKit.Blue : UIKit.Gray;
            UIKit.Restyle(r.buy, st, label, maxed ? "check" : !req ? "lock" : "bell");
            var t = r.buy.GetComponentInChildren<Text>(); if (t) t.fontSize = !req ? (label.Length > 9 ? 15 : 17) : 24;
            r.buy.interactable = !maxed && req;
            var ic = r.buy.transform.Find("row").GetComponentInChildren<Image>(); if (ic) ic.rectTransform.sizeDelta = Vector2.one * (!req ? 20 : 26);
            var le = ic ? ic.GetComponent<LayoutElement>() : null; if (le) { le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = !req ? 20 : 26; }
        }
        r.lv = lv; r.maxed = maxed; r.req = req; r.can = can; r.vis = vis; r.label = label;
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
            dirty = true;
        }
    }
}

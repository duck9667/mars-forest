using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// §3.3 하단 업그레이드 시트(원본 #sheet .tabs/.row/.buy, 976~1000행). 탭 3개:
//  · 업그레이드: ① 이주 카드(sold≥60 or prestige>0, §6.3 2회 탭 확인 3s) ② 스탯 3줄(hand·spd·price, 레벨 핍) ③ 시설 카드(구역·메뉴 제외; 노출 중 금테 → 구매 가능 → 잠김 → MAX)
//  · 장식: sold≥60부터 탭이 보인다. §3.4 DECOS 6 카드(견본 그림·이름·설명·가격 / 보유 시 금색 "보유")
//  · 목표: 상단 목표 칩으로만 진입(그 탭에 있는 동안만 탭 단추가 보인다). §6.1 목표 12개(완료 체크 / 현재 진행 바·v/g / 이후 잠김, 보상 상자+벨)
// 탭이 하나뿐이면(장식 전, 업그레이드 탭) 탭 바 대신 제목. 구역 발판(zone)은 월드 발판에서만 해제하므로 시트에 안 올린다.
// 행은 key로 캐싱해 구성이 바뀔 때만 다시 만들고, 상태 갱신은 dirty → LateUpdate 프레임당 1회(납입 중 OnBellsChanged 매 프레임 발화 대비)
// + 열려 있는 동안 0.25s 폴링(판매·목표·테라 진행처럼 이벤트가 없는 값).
// 검증 인자: -sheettab upgrade|deco|quest[@초] (기본 3초 뒤 그 탭으로 시트를 연다)
public class UpgradeSheet : MonoBehaviour
{
    public static UpgradeSheet I;
    GameState G => GameState.I;
    public const string UP = "upgrade", DECO = "deco", QUEST = "quest";
    Canvas canvas; RectTransform panel, list, viewport, tabBar; Image dim; CanvasGroup listCg; ScrollRect scroll; Text title;
    bool open, dirty; Coroutine slide, tabAnim; float pollT;
    string tab = UP;
    const float H = 760f, ROW_H = 88f, GAP = 8f;
    public bool IsOpen => open;
    public string CurrentTab => tab;
    bool DecoOn => G != null && G.S.sold >= DecoManager.UNLOCK_SOLD;

    class TabBtn { public string id; public Button btn; public Image dot; public int state = -1; }
    readonly List<TabBtn> tabs = new List<TabBtn>();

    enum Kind { Pad, Prest, Deco, Quest }
    class Row
    {
        public Kind kind; public string key;
        public PadDef d; public bool stat; public DecoDef deco; public int qi = -1;
        public Image bg, tile, barFill; public RectTransform barTrack; public Transform pips; public Button buy; public Text name, sub, tag; public CanvasGroup cg;
        public int lv = -1, state = -1; public bool maxed, req, can, vis; public string label;
    }
    readonly List<Row> rows = new List<Row>();
    Row prestRow; float armUntil;   // 이주 2회 탭 확인(3초 창, 언스케일 시간)

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
        // 손잡이 · 제목(탭이 하나일 때) · 탭 바 · 닫기
        var grip = UIKit.Panel(p.transform, "grip", UIKit.Track); UIKit.Anchor(grip.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(44, 6)); grip.color = UIKit.Hex("4a6190");
        title = UIKit.Text(p.transform, "업그레이드", 34, Color.white); UIKit.Stroke(title, UIKit.EDGE, 2.2f);
        UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(400, 48));
        tabBar = UIKit.RT(p.transform, "tabs"); tabBar.anchorMin = new Vector2(0, 1); tabBar.anchorMax = new Vector2(1, 1); tabBar.pivot = new Vector2(0.5f, 1);
        tabBar.offsetMin = new Vector2(16, -76); tabBar.offsetMax = new Vector2(-80, -20);
        var hl = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 8; hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = true;
        foreach (var id in new[] { UP, DECO, QUEST })
        {
            string tid = id;
            var b = UIKit.Button(tabBar, "tab_" + id, UIKit.Gray, new Vector2(100, 56), TabLabel(id), 22, () => SetTab(tid), TabIcon(id), 24);
            b.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var tb = new TabBtn { id = id, btn = b };
            if (id == DECO)
            {
                tb.dot = UIKit.IconImg(b.transform, "dot", 22); Destroy(tb.dot.GetComponent<LayoutElement>());
                var drt2 = tb.dot.rectTransform; drt2.anchorMin = drt2.anchorMax = new Vector2(1, 1); drt2.pivot = new Vector2(0.5f, 0.5f); drt2.anchoredPosition = new Vector2(-6, -6);
            }
            tabs.Add(tb);
        }
        var close = UIKit.RoundButton(p.transform, "close", UIKit.Gray, 48, "close", 26, Close, out _);
        var crt = close.GetComponent<RectTransform>(); crt.anchorMin = crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1); crt.anchoredPosition = new Vector2(-16, -20);
        // 목록: 뷰포트(마스크·세로 스크롤) 안에 세로 레이아웃 콘텐츠. 행이 시트 높이를 넘치면 드래그로 스크롤
        viewport = UIKit.RT(p.transform, "viewport"); viewport.anchorMin = new Vector2(0, 0); viewport.anchorMax = new Vector2(1, 1); viewport.offsetMin = new Vector2(16, 18); viewport.offsetMax = new Vector2(-16, -86);
        var vimg = viewport.gameObject.AddComponent<Image>(); vimg.color = Color.clear; vimg.raycastTarget = true;   // 빈 자리 드래그도 받도록
        viewport.gameObject.AddComponent<RectMask2D>();
        list = UIKit.RT(viewport, "list"); list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1); list.pivot = new Vector2(0.5f, 1); list.offsetMin = new Vector2(0, 0); list.offsetMax = new Vector2(0, 0);
        var vl = list.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = GAP; vl.padding = new RectOffset(2, 2, 4, 8); vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
        var lf = list.gameObject.AddComponent<ContentSizeFitter>(); lf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        listCg = list.gameObject.AddComponent<CanvasGroup>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.content = list; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 30; scroll.decelerationRate = 0.12f;
        // -sheettab: QA 스크린샷용(이 오브젝트는 곧 비활성화돼 코루틴을 못 돌리므로 따로 띄운 러너가 연다)
        var st = DevArgs.Get("-sheettab");
        if (!string.IsNullOrEmpty(st)) new GameObject("SheetDev").AddComponent<SheetDevRunner>().tab = st;
        gameObject.SetActive(false);
    }

    void Start()
    {
        G.OnBellsChanged += () => { if (open) dirty = true; };
        G.OnPadLevel += (a, b) => { if (open) dirty = true; };
        G.OnSold += () => { if (open) dirty = true; };
        if (DecoManager.I != null) DecoManager.I.OnBought += _ => { if (open) dirty = true; };
        // (-sheet QA 자동 열기는 HUD.Start에서: 이 오브젝트는 Awake 끝에 비활성화돼 첫 Open 전엔 Start·코루틴이 안 돈다)
    }

    void LateUpdate()
    {
        if (!open) return;
        pollT += Time.unscaledDeltaTime; if (pollT >= 0.25f) { pollT = 0; dirty = true; }
        if (armUntil > 0 && Time.unscaledTime >= armUntil) { armUntil = 0; dirty = true; }
        if (dirty) { dirty = false; Rebuild(); }
        // 이주 확인 대기 중엔 버튼이 두근거리고, 장식 탭 점은 살짝 맥동
        if (prestRow != null && prestRow.buy != null) prestRow.buy.transform.localScale = Vector3.one * (armUntil > 0 ? 1 + Mathf.Sin(Time.unscaledTime * 14f) * 0.05f : 1f);
        foreach (var tb in tabs) if (tb.dot != null && tb.dot.gameObject.activeSelf) tb.dot.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f);
    }

    public void Toggle() { if (open) Close(); else Open(UP); }
    public void Open() => Open(UP);
    /// tab: UP(upgrade) / DECO(deco, 판매 60 전이면 업그레이드로) / QUEST(quest). 이미 열려 있으면 탭만 바꾼다
    public void Open(string t)
    {
        if (t != UP && t != DECO && t != QUEST) t = UP;
        if (t == DECO && !DecoOn) t = UP;
        bool was = open;
        if (was && t == tab) return;
        int dir = TabIndex(t) >= TabIndex(tab) ? 1 : -1;
        tab = t; armUntil = 0;
        open = true; gameObject.SetActive(true);
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
        Rebuild(true);
        if (!was)
        {
            AudioManager.I.Play("pop", 1.2f, 0.5f);
            if (slide != null) StopCoroutine(slide);
            slide = StartCoroutine(Slide(true));
        }
        else TabSwitchFx(dir);
    }
    public void Close()
    {
        if (!open) return; open = false; armUntil = 0;
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = false;
        if (slide != null) StopCoroutine(slide);
        slide = StartCoroutine(Slide(false));
    }
    void SetTab(string id)
    {
        if (id == tab || !open) return;
        int dir = TabIndex(id) >= TabIndex(tab) ? 1 : -1;
        tab = id; armUntil = 0;
        Rebuild(true);
        TabSwitchFx(dir);
        AudioManager.I.Play("pop", 1.45f, 0.45f);
    }
    static int TabIndex(string id) => id == UP ? 0 : id == DECO ? 1 : 2;
    static string TabLabel(string id) => id == UP ? "업그레이드" : id == DECO ? "장식" : "목표";
    static string TabIcon(string id) => id == UP ? "up" : id == DECO ? "home" : "flag";

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
    // 탭 전환: 목록이 옆에서 미끄러져 들어오며 밝아지고, 고른 탭이 톡 튄다
    void TabSwitchFx(int dir)
    {
        if (tabAnim != null) StopCoroutine(tabAnim);
        tabAnim = StartCoroutine(CoTab(dir));
        foreach (var tb in tabs) if (tb.id == tab && tb.btn.gameObject.activeInHierarchy) JuiceFX.I.Pop(tb.btn.transform, 0.86f, 0.3f);
    }
    IEnumerator CoTab(int dir)
    {
        float el = 0;
        while (el < 0.2f)
        {
            el += Time.unscaledDeltaTime; float k = Mathf.Clamp01(el / 0.2f); float e = 1 - (1 - k) * (1 - k);
            list.anchoredPosition = new Vector2(Mathf.Lerp(56f * dir, 0, e), list.anchoredPosition.y); listCg.alpha = Mathf.Lerp(0.15f, 1f, e);
            yield return null;
        }
        list.anchoredPosition = new Vector2(0, list.anchoredPosition.y); listCg.alpha = 1; tabAnim = null;
    }

    // ───────────────────────── 탭 바 ─────────────────────────
    void RefreshTabs()
    {
        bool decoOn = DecoOn; int n = 0;
        foreach (var tb in tabs)
        {
            bool show = tb.id == UP || (tb.id == DECO && decoOn) || (tb.id == QUEST && tab == QUEST);
            if (tb.btn.gameObject.activeSelf != show) tb.btn.gameObject.SetActive(show);
            if (show) n++;
            int st = tb.id == tab ? 1 : 0;
            if (st != tb.state) { tb.state = st; SetBtn(tb.btn, st == 1 ? UIKit.Gold : UIKit.Gray, TabLabel(tb.id), TabIcon(tb.id), 22, 24); }
            if (tb.dot != null)
            {
                bool dot = tb.id == DECO && tab != DECO && DecoManager.I != null && DecoManager.I.AnyAffordable();
                if (tb.dot.gameObject.activeSelf != dot) tb.dot.gameObject.SetActive(dot);
            }
        }
        bool bar = n >= 2;
        if (tabBar.gameObject.activeSelf != bar) tabBar.gameObject.SetActive(bar);
        if (title.gameObject.activeSelf == bar) title.gameObject.SetActive(!bar);
    }

    // ───────────────────────── 목록 ─────────────────────────
    List<string> Keys()
    {
        var k = new List<string>();
        if (tab == UP)
        {
            if (PrestigeManager.I != null && PrestigeManager.I.ShowCard) k.Add("prest");
            foreach (var d in Pads.ALL.Where(p => p.active && p.menu)) k.Add("pad:" + d.id);
            int Rank(PadDef d) => G.PadMaxed(d.id) ? 3 : !G.PadReq(d.id) ? 2 : PadManager.I.visSet.Contains(d.id) ? 0 : 1;
            foreach (var d in Pads.ALL.Where(p => p.active && !p.menu && !p.zone).OrderBy(Rank)) k.Add("pad:" + d.id);
        }
        else if (tab == DECO) foreach (var d in DecoManager.DECOS) k.Add("deco:" + d.id);
        else for (int i = 0; i < QuestManager.QUESTS.Length; i++) k.Add("quest:" + i);
        return k;
    }

    // 행 목록(정렬 포함)을 만들고, 이미 있는 행은 상태만 갱신한다. 순서·구성이 바뀌면 전부 다시 만든다.
    void Rebuild(bool resetScroll = false)
    {
        if (tab == DECO && !DecoOn) tab = UP;
        RefreshTabs();
        var want = Keys();
        bool same = rows.Count == want.Count; if (same) for (int i = 0; i < rows.Count; i++) if (rows[i].key != want[i]) { same = false; break; }
        if (!same)
        {
            foreach (Transform c in list) { c.gameObject.SetActive(false); Destroy(c.gameObject); }   // 비활성은 레이아웃에서 바로 빠진다
            rows.Clear(); prestRow = null;
            foreach (var k in want) rows.Add(MakeRow(k));
            resetScroll = true;
        }
        foreach (var r in rows) UpdateRow(r);
        if (resetScroll) ScrollToStart();
    }
    // 맨 위로. 목표 탭은 진행 중인 목표가 위에서 두 번째 줄에 오도록
    void ScrollToStart()
    {
        scroll.StopMovement();
        float y = 0;
        if (tab == QUEST && QuestManager.I != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            int cur = Mathf.Min(G.S.quest, QuestManager.QUESTS.Length - 1);
            float max = Mathf.Max(0, list.rect.height - viewport.rect.height);
            y = Mathf.Clamp((cur - 1) * (ROW_H + GAP), 0, max);
        }
        list.anchoredPosition = new Vector2(list.anchoredPosition.x, y);
    }

    Row MakeRow(string key)
    {
        if (key == "prest") return MakePrest();
        var parts = key.Split(':');
        if (parts[0] == "pad") { var d = Pads.Get(parts[1]); return MakePad(d, d.menu); }
        if (parts[0] == "deco") return MakeDeco(DecoManager.Def(parts[1]));
        return MakeQuest(int.Parse(parts[1]));
    }

    // 공통 조각: 카드 · 아이콘 타일 · 견본 그림 · 이름 · 설명 · 오른쪽 버튼
    Row Card(string key, Kind kind, float h)
    {
        var r = new Row { key = key, kind = kind };
        r.bg = UIKit.Panel(list, key, UIKit.Card); r.bg.raycastTarget = true;   // 행 위 드래그가 ScrollRect로 올라가도록
        var le = r.bg.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = h;
        return r;
    }
    void IconTile(Row r, string icon)
    {
        r.tile = UIKit.Panel(r.bg.transform, "tile", UIKit.Tile); UIKit.Anchor(r.tile.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 1), new Vector2(60, 63));
        var ic = UIKit.IconImg(r.tile.transform, icon, 40); var irt = ic.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f); irt.anchoredPosition = new Vector2(0, 1.5f);
    }
    void ArtTile(Row r, string art)
    {
        var img = UIKit.RT(r.bg.transform, "art").gameObject.AddComponent<Image>(); img.sprite = DecoArt.Swatch(art); img.raycastTarget = false;
        UIKit.Anchor(img.rectTransform, new Vector2(0, 0.5f), new Vector2(11, 0), new Vector2(68, 68));
    }
    Text Name(Row r, string s, float y)
    {
        var t = UIKit.Text(r.bg.transform, s, 24, Color.white, TextAnchor.MiddleLeft);
        UIKit.Anchor(t.rectTransform, new Vector2(0, 0.5f), new Vector2(88, y), new Vector2(240, 32)); return t;
    }
    Text Sub(Row r, string s, float y)
    {
        var t = UIKit.Text(r.bg.transform, s, 17, UIKit.SUB, TextAnchor.MiddleLeft);
        UIKit.Anchor(t.rectTransform, new Vector2(0, 0.5f), new Vector2(88, y), new Vector2(226, 24));
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = 17;
        return t;
    }
    Button BuyBtn(Row r, UIKit.Style st, string icon, System.Action onClick, float w = 164)
    {
        var b = UIKit.Button(r.bg.transform, "buy", st, new Vector2(w, 56), "", 24, onClick, icon, 26);
        UIKit.Anchor(b.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-14, 2), new Vector2(w, 56));
        return b;
    }
    // 버튼 스타일·글자·아이콘(없으면 숨김) 한 번에. 외곽선 없는 스타일로 바뀔 때 이전 외곽선도 지운다
    static void SetBtn(Button b, UIKit.Style st, string label, string icon, int fs = 24, float iconSize = 26)
    {
        b.GetComponent<Image>().sprite = UIKit.Panel9(st);
        var row = b.transform.Find("row"); if (row == null) return;
        var t = row.GetComponentInChildren<Text>(true);
        if (t != null)
        {
            t.text = label; t.color = st.text; t.fontSize = fs;
            if (st.strokeW <= 0) foreach (var o in t.GetComponents<Outline>()) Destroy(o);
            UIKit.Stroke(t, st.stroke, st.strokeW);
        }
        var ic = row.GetComponentInChildren<Image>(true);
        if (ic != null)
        {
            bool on = !string.IsNullOrEmpty(icon); ic.gameObject.SetActive(on);
            if (on) { ic.sprite = UIKit.Icon(icon); ic.rectTransform.sizeDelta = Vector2.one * iconSize; var le = ic.GetComponent<LayoutElement>(); if (le) { le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = iconSize; } }
        }
    }
    static void SetCard(Row r, UIKit.Style st) => r.bg.sprite = UIKit.Panel9(st);

    // ① 이주 카드 (§6.3): 로켓 견본 · "새 기지로 이주 ×n" · 조건 진행 한 줄 · [이주] (2회 탭, 3초)
    Row MakePrest()
    {
        var r = Card("prest", Kind.Prest, 96); prestRow = r;
        ArtTile(r, "rocket");
        r.name = Name(r, "새 기지로 이주", 16);
        r.tag = UIKit.Text(r.bg.transform, "", 19, UIKit.GOLD, TextAnchor.MiddleLeft); UIKit.Stroke(r.tag, UIKit.EDGE, 1.4f);
        UIKit.Anchor(r.tag.rectTransform, new Vector2(0, 0.5f), new Vector2(88 + r.name.preferredWidth + 8, 15), new Vector2(80, 28));
        r.sub = Sub(r, "", -15);
        r.buy = BuyBtn(r, UIKit.Gray, "lock", OnPrest);
        return r;
    }
    void UpdatePrest(Row r)
    {
        var pm = PrestigeManager.I; if (pm == null) return;
        bool can = pm.CanPrestige(); bool armed = can && armUntil > 0;
        r.tag.text = "×" + (Mathf.Pow(1.5f, G.S.prestige + 1)).ToString("0.0");
        string s = pm.ProgressText(); if (string.IsNullOrEmpty(s)) s = FallbackProgress();
        if (r.sub.text != s) r.sub.text = s;
        int st = armed ? 2 : can ? 1 : 0;
        if (st != r.state)
        {
            r.state = st;
            SetCard(r, can ? UIKit.CardCur : UIKit.Card);
            if (armed) SetBtn(r.buy, UIKit.Red, "한 번 더 누르면 이주", null, 15);
            else if (can) SetBtn(r.buy, UIKit.Gold, "이주", "up", 26, 28);
            else SetBtn(r.buy, UIKit.Gray, "잠김", "lock", 20, 22);
        }
    }
    // PrestigeManager.ProgressText가 비어 있을 때(구현 전) 보여 줄 최소 진행: 판매/300 · 구역 n/3
    string FallbackProgress()
    {
        int z = (G.PadLv("ice2") >= 1 ? 1 : 0) + (G.PadLv("pen") >= 1 ? 1 : 0) + (G.PadLv("dome") >= 1 ? 1 : 0);
        return "판매 " + Mathf.Min(G.S.sold, 300) + "/300 · 구역 " + z + "/3";
    }
    void OnPrest()
    {
        var pm = PrestigeManager.I; if (pm == null || prestRow == null) return;
        if (!pm.CanPrestige()) { AudioManager.I.Play("no", 1f, 0.8f); Wiggle(prestRow.buy.transform); return; }
        if (armUntil > 0 && Time.unscaledTime < armUntil)
        {
            armUntil = 0; Close(); pm.Prestige(); return;
        }
        armUntil = Time.unscaledTime + 3f;
        AudioManager.I.Play("tick", 1.3f, 0.8f); JuiceFX.I.Vibrate(15);
        dirty = true;
    }

    // ② 스탯 3줄 · ③ 시설 카드 (발판)
    Row MakePad(PadDef d, bool stat)
    {
        var r = Card("pad:" + d.id, Kind.Pad, ROW_H); r.d = d; r.stat = stat;
        IconTile(r, UIKit.PadIcon(d.id));
        r.name = Name(r, d.name, 14);
        r.pips = UIKit.Pips(r.bg.transform, 0, d.max);
        var prt = r.pips.GetComponent<RectTransform>(); UIKit.Anchor(prt, new Vector2(0, 0.5f), new Vector2(88, -15), new Vector2(240, 12));
        var fit = r.pips.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        // 구매 버튼(파랑 [벨] 가격 / 회색 잠김 / 금색 MAX)
        r.buy = BuyBtn(r, UIKit.Blue, "bell", () => OnBuyPad(d, stat));
        return r;
    }
    void UpdatePad(Row r)
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
        if (vis != r.vis || maxed != r.maxed || r.state < 0) { SetCard(r, vis ? UIKit.CardCur : maxed ? UIKit.CardDone : UIKit.Card); r.tile.sprite = UIKit.Panel9(maxed || !req ? UIKit.TileDark : UIKit.Tile); }
        if (req != r.req || maxed != r.maxed || can != r.can || label != r.label || r.state < 0)
        {
            var st = maxed ? UIKit.Gold : !req ? UIKit.Gray : can ? UIKit.Blue : UIKit.Gray;
            SetBtn(r.buy, st, label, maxed ? "check" : !req ? "lock" : "bell", !req ? (label.Length > 9 ? 15 : 17) : 24, !req ? 20 : 26);
            r.buy.interactable = !maxed && req;
        }
        r.lv = lv; r.maxed = maxed; r.req = req; r.can = can; r.vis = vis; r.label = label; r.state = 0;
    }
    void OnBuyPad(PadDef d, bool stat)
    {
        if (!PadManager.I.Buy(d.id)) { var r = rows.FirstOrDefault(x => x.d == d); if (r != null) Wiggle(r.buy.transform); return; }
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

    // 장식 카드 (§3.4): 견본 그림 · 이름 · 설명 · [벨 가격] / 보유 시 금색 "보유"
    Row MakeDeco(DecoDef def)
    {
        var r = Card("deco:" + def.id, Kind.Deco, ROW_H); r.deco = def;
        ArtTile(r, def.id);
        r.name = Name(r, def.name, 14);
        r.sub = Sub(r, def.desc, -16);
        r.buy = BuyBtn(r, UIKit.Blue, "bell", () => OnBuyDeco(r));
        return r;
    }
    void UpdateDeco(Row r)
    {
        var dm = DecoManager.I; if (dm == null) return;
        bool owned = dm.Has(r.deco.id), can = !owned && G.S.bells >= r.deco.price;
        int st = owned ? 2 : can ? 1 : 0;
        if (st == r.state) return;
        r.state = st;
        SetCard(r, owned ? UIKit.CardDone : UIKit.Card);
        if (owned) SetBtn(r.buy, UIKit.Gold, "보유", "check", 24, 26);
        else SetBtn(r.buy, can ? UIKit.Blue : UIKit.Gray, r.deco.price.ToString("N0"), "bell", 24, 26);
        r.buy.interactable = !owned;
    }
    // 매장이 화면에 보이면 시트를 내리고 그 자리에서 등장 연출을 보여 준다(동전은 벨 HUD → 장식으로). 아니면 시트를 열어 둔 채 카드만 팝
    void OnBuyDeco(Row r)
    {
        var dm = DecoManager.I; if (dm == null) return;
        bool show = ShopOnScreen();
        if (!dm.Buy(r.deco.id, show ? 0.3f : 0f)) { Wiggle(r.buy.transform); return; }
        if (show)
        {
            Close();
            var cam = Camera.main; var focus = dm.View.Focus(r.deco.id);
            HUD.I.Coins(HUD.I.BellsScreen(), () => cam.WorldToScreenPoint(focus), 10);
        }
        else { JuiceFX.I.Pop(r.bg.transform, 1.08f, 0.35f); dirty = true; }
    }
    static bool ShopOnScreen()
    {
        var cam = Camera.main; if (cam == null) return false;
        var v = cam.WorldToViewportPoint(WorldMap.W2U(178, 520, 40));
        return v.z > 0 && v.y > 0.08f && v.y < 0.95f;
    }

    // 목표 카드 (§6.1): 번호·내용 · 진행(현재만 바) · [상자 +보상] — 완료 초록 체크 / 현재 금테 / 이후 잠김
    Row MakeQuest(int i)
    {
        var q = QuestManager.QUESTS[i];
        var r = Card("quest:" + i, Kind.Quest, ROW_H); r.qi = i;
        r.cg = r.bg.gameObject.AddComponent<CanvasGroup>();
        IconTile(r, UIKit.PadIcon(q.key));
        r.name = Name(r, (i + 1) + ". " + q.text, 16);
        r.sub = Sub(r, "", -10);
        var tr = UIKit.ProgressBar(r.bg.transform, "bar", new Vector2(190, 10), out r.barFill); r.barTrack = tr.rectTransform;
        UIKit.Anchor(r.barTrack, new Vector2(0, 0.5f), new Vector2(88, -30), new Vector2(190, 10));
        r.buy = BuyBtn(r, UIKit.Gray, "chest", null, 150); r.buy.interactable = false;
        var press = r.buy.GetComponent<UIPress>(); if (press) Destroy(press);
        return r;
    }
    void UpdateQuest(Row r)
    {
        var q = QuestManager.QUESTS[r.qi]; int cur = G.S.quest;
        int st = r.qi < cur ? 2 : r.qi == cur ? 1 : 0;
        int v = QuestManager.I != null ? Mathf.Min(QuestManager.I.ValueOf(q), q.goal) : 0;
        if (st != r.state)
        {
            r.state = st;
            SetCard(r, st == 2 ? UIKit.CardDone : st == 1 ? UIKit.CardCur : UIKit.Card);
            r.tile.sprite = UIKit.Panel9(st == 0 ? UIKit.TileDark : UIKit.Tile);
            r.name.color = st == 0 ? UIKit.SUB : Color.white;
            r.cg.alpha = st == 0 ? 0.72f : 1f;
            r.barTrack.gameObject.SetActive(st == 1);
            r.sub.rectTransform.anchoredPosition = new Vector2(88, st == 1 ? -8 : -15);
            if (st == 2) SetBtn(r.buy, UIKit.Green, "완료", "check", 22, 26);
            else if (st == 1) SetBtn(r.buy, UIKit.Gold, "+" + q.reward, "chest", 24, 30);
            else SetBtn(r.buy, UIKit.Gray, "+" + q.reward, "lock", 22, 22);
        }
        string s = st == 2 ? "달성!" : st == 1 ? v + " / " + q.goal + " · 보상 " + q.reward + "벨" : "보상 " + q.reward + "벨";
        if (r.sub.text != s) r.sub.text = s;
        r.sub.color = st == 2 ? UIKit.Hex("8fe08a") : st == 1 ? UIKit.GOLD : UIKit.SUB;
        if (st == 1) UIKit.SetBar(r.barFill, (float)v / q.goal, 190);
    }

    void UpdateRow(Row r)
    {
        switch (r.kind)
        {
            case Kind.Pad: UpdatePad(r); break;
            case Kind.Prest: UpdatePrest(r); break;
            case Kind.Deco: UpdateDeco(r); break;
            case Kind.Quest: UpdateQuest(r); break;
        }
    }

    // 못 사는 버튼: 좌우로 도리도리
    void Wiggle(Transform t) { if (t != null && gameObject.activeInHierarchy) StartCoroutine(CoWiggle(t)); }
    IEnumerator CoWiggle(Transform t)
    {
        float el = 0;
        while (el < 0.32f && t != null) { el += Time.unscaledDeltaTime; float k = el / 0.32f; t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(el * 55f) * 9f * (1 - k)); yield return null; }
        if (t != null) t.localRotation = Quaternion.identity;
    }
}

// -sheettab 검증 러너: tab[@초](기본 3초) 뒤 지정 탭으로 시트를 연다(시트 오브젝트는 닫혀 있는 동안 비활성이라 직접 못 기다린다)
public class SheetDevRunner : MonoBehaviour
{
    public string tab;
    IEnumerator Start()
    {
        float wait = 3f; var t = tab; int at = t.IndexOf('@');
        if (at >= 0) { float.TryParse(t.Substring(at + 1), out wait); t = t.Substring(0, at); }
        yield return new WaitForSeconds(wait);
        if (UpgradeSheet.I != null) UpgradeSheet.I.Open(t);
    }
}

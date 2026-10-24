using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 지도 화면(정착 목표): HUD "정착 n/5" 알약을 누르면 열린다. 화면 막 + 남색 판 하나.
//  · 위: [깃발] 정착 목표 [n/5] · 닫기(X)
//  · 왼쪽: 전체 맵(논리 x 0~400, y Y_MIN~1040, 북쪽 위)을 위에서 본 그림. 구역 사각형(열림 = 구역 바닥색, 잠김 = 어둡게 + 자물쇠),
//    지금 단계에 필요한 구역은 금테 펄스, 시설 아이콘, 얼음·레골리스 더미 점, 착륙장, 플레이어 점(맥동 링)
//  · 오른쪽: 5단계 체크리스트(완료 ✓ / 지금 단계는 항목별 진행 막대 / 다음 단계는 흐리게), 정착 완료면 "새 정착지로" 안내 버튼
// 세로 화면 기준(참조 540×1170) 판 520×950. 더 낮은 화면(태블릿 등)에서는 판 전체를 줄여 맞춘다.
// 검증 인자: -map [초] (그 초에 지도 열기, 기본 0.8초)
public class MapScreen : MonoBehaviour
{
    public static MapScreen I;
    GameState G => GameState.I;
    SettlementManager SM => SettlementManager.I;

    Canvas canvas; GameObject root; RectTransform panel, mapRt, listRt, meRt, meRing;
    CanvasGroup focusCg; float refreshT, openT; int builtStage = -1;
    public bool IsOpen => root != null;

    const float S = 0.6f;                       // 논리 1 → UI 0.6
    const float PW = 520, TITLE_H = 74, PAD = 16, GAP_X = 14;
    static float MW => GameConfig.W * S;        // 240
    static float MH => (GameConfig.H - GameConfig.Y_MIN) * S;   // 858
    static float PH => TITLE_H + MH + PAD;      // 948
    static float LX => PAD + MW + GAP_X;        // 체크리스트 왼쪽
    static float LW => PW - LX - PAD;           // 체크리스트 폭 234

    // 진행 중 단계 항목 갱신용
    class ItemUI { public Text val; public Image fill; public float barW; public Image mark; }
    readonly List<ItemUI> items = new List<ItemUI>();

    static readonly UIKit.Style MapBg = new UIKit.Style("mapbg", "a4502d", "974627", "8a3e22", "0b1428", "070d1a", "ffffff").R(12).B(3).Sh(3).HiLo(0.06f, 0.2f);
    static readonly UIKit.Style Cell = new UIKit.Style("mapcell", "ffffff", "ffffff", "ffffff", "8c8c8c", "000000", "ffffff").R(5).B(1.5f).Sh(0).HiLo(0f, 0f);
    static readonly UIKit.Style Dot = new UIKit.Style("mapdot", "ffffff", "ffffff", "ffffff", "0b1428", "000000", "ffffff").B(2.5f).Sh(0).HiLo(0f, 0f);
    static readonly UIKit.Style Soft = new UIKit.Style("mapsoft", "ffffff", "ffffff", "ffffff", "ffffff", "000000", "ffffff").B(0).Sh(0).HiLo(0f, 0f);

    static readonly Dictionary<string, string> ZONE_NAME = new Dictionary<string, string>
    {
        { "ice", "얼음밭" }, { "ice2", "얼음밭" }, { "melt", "해빙기" }, { "bottle", "병입기" }, { "shop", "가게" },
        { "dome", "휴게 돔" }, { "pen", "우리" }, { "mine", "채굴장" }, { "o2", "산소동" }, { "farm", "온실" }, { "soil", "토양 공장" },
        { "hab", "거주 구역" }, { "solar", "태양광" },
    };

    void Awake()
    {
        I = this;
        canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 14;   // HUD 10·시트 12 위
        var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(540, 1170); sc.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
    }

    static bool devOpened;
    void Start()
    {
        if (DevArgs.Has("-map") && !devOpened)
        {
            devOpened = true;
            float sec = 0.8f; float.TryParse(DevArgs.Get("-map"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sec);
            StartCoroutine(CoDevOpen(sec > 0 ? sec : 0.8f));
        }
    }
    IEnumerator CoDevOpen(float sec) { yield return new WaitForSeconds(sec); Open(); }

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    public void Open()
    {
        if (IsOpen || G == null) return;
        if (UpgradeSheet.I != null) UpgradeSheet.I.Close();
        if (HUD.I != null) HUD.I.CloseSettings();
        AudioManager.I?.Play("pop", 1.2f, 0.5f);
        // 화면 막(바깥 누르면 닫힘)
        var scrim = UIKit.RT(transform, "map").gameObject.AddComponent<Image>(); root = scrim.gameObject;
        scrim.color = UIKit.SCRIM; scrim.raycastTarget = true;
        var srt = scrim.rectTransform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
        var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(Close);
        var p = UIKit.Panel(scrim.transform, "panel", UIKit.Navy); p.raycastTarget = true;
        var pb = p.gameObject.AddComponent<Button>(); pb.transition = Selectable.Transition.None;   // 판 안쪽 누름이 막(닫기)으로 번지지 않게
        panel = p.rectTransform; UIKit.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PW, PH));
        Build();
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
        openT = 0f;
    }

    public void Close()
    {
        if (!IsOpen) return;
        Destroy(root); root = null; panel = mapRt = listRt = meRt = meRing = null; focusCg = null; items.Clear(); builtStage = -1;
        if (FloatingJoystick.I && (UpgradeSheet.I == null || !UpgradeSheet.I.IsOpen)) FloatingJoystick.I.suppressed = false;
    }

    // 판 내용 전체를 (다시) 만든다. 단계가 바뀌면 다시 부른다
    void Build()
    {
        for (int i = panel.childCount - 1; i >= 0; i--) Destroy(panel.GetChild(i).gameObject);
        items.Clear();
        builtStage = SM != null ? SM.Stage : 0;
        BuildTitle();
        BuildMap();
        BuildList();
    }

    // ---- 제목 줄: [깃발] 정착 목표 [n/5] · X ----
    void BuildTitle()
    {
        int st = builtStage; bool done = st >= SettlementManager.COUNT;
        var row = UIKit.Row(panel, "title", 8, TextAnchor.MiddleLeft);
        UIKit.Anchor(row.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(PAD + 4, -12), new Vector2(380, 52));
        UIKit.IconImg(row.transform, "flag", 34);
        var t = UIKit.Text(row.transform, "정착 목표", 32, Color.white); UIKit.Stroke(t, UIKit.EDGE, 2.2f);
        UIKit.Chip(row.transform, "prog", done ? UIKit.SignOk : UIKit.Pill, done ? "crown" : null, 24, st + "/" + SettlementManager.COUNT, 24, out _, out _, 4);
        var close = UIKit.RoundButton(panel, "close", UIKit.Gray, 48, "close", 26, Close, out _);
        var crt = close.GetComponent<RectTransform>(); crt.anchorMin = crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1); crt.anchoredPosition = new Vector2(-14, -12);
    }

    // ---- 지도 ----
    static Vector2 M(float x, float y) => new Vector2(x * S, -(y - GameConfig.Y_MIN) * S);   // 논리 → 지도 안 좌표(왼쪽 위 원점)
    static RectTransform Place(Component c, Vector2 at, Vector2 size)
    {
        var rt = c is RectTransform r ? r : c.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = at; rt.sizeDelta = size; return rt;
    }

    void BuildMap()
    {
        var bg = UIKit.Panel(panel, "mapbg", MapBg);
        mapRt = bg.rectTransform; mapRt.anchorMin = mapRt.anchorMax = new Vector2(0, 1); mapRt.pivot = new Vector2(0, 1);
        mapRt.anchoredPosition = new Vector2(PAD, -TITLE_H); mapRt.sizeDelta = new Vector2(MW, MH + MapBg.shadowH);
        var focus = new HashSet<string>(SM != null ? SM.FocusZones : new string[0]);
        bool settled = SM != null && SM.Settled;

        // 북쪽 길·가운데 길(옅은 띠): 북쪽 구역이 하나라도 열리면 보인다
        bool near = WorldMap.openZones.Contains("mine") || WorldMap.openZones.Contains("o2") || G.PadLv("ice2") >= 1;
        bool far = WorldMap.openZones.Contains("farm") || WorldMap.openZones.Contains("soil");
        if (near) Strip(16, WorldMap.NORTH_Y, 380, 46);
        if (far) Strip(16, WorldMap.MID_Y0, 380, WorldMap.MID_Y1);
        if (far) Strip(16, WorldMap.ROAD3_Y0, 380, WorldMap.ROAD3_Y1);   // 2장 개척 길

        // 구역
        var focusRoot = UIKit.RT(mapRt, "focus"); focusRoot.anchorMin = Vector2.zero; focusRoot.anchorMax = Vector2.one; focusRoot.offsetMin = focusRoot.offsetMax = Vector2.zero;
        foreach (var z in WorldMap.ZONES)
        {
            bool open = ZoneOpen(z);
            var cell = UIKit.Panel(mapRt, "z_" + z.key, Cell);
            var crt = cell.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0, 1); crt.pivot = new Vector2(0, 1);
            crt.anchoredPosition = M(z.x0, z.y0); crt.sizeDelta = new Vector2((z.x1 - z.x0) * S, (z.y1 - z.y0) * S);
            cell.color = open ? z.floor : Color.Lerp(z.floor, UIKit.EDGE, 0.68f);
            if (z.key != "ice2" && ZONE_NAME.TryGetValue(z.key, out var nm))
            {
                var lab = UIKit.Text(cell.transform, nm, 14, open ? UIKit.INK : UIKit.SUB, TextAnchor.UpperLeft);
                var lrt = lab.rectTransform; lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1); lrt.pivot = new Vector2(0, 1); lrt.anchoredPosition = new Vector2(5, -3); lrt.sizeDelta = new Vector2(120, 18);
                if (open) UIKit.Stroke(lab, new Color(1, 1, 1, 0.55f), 1f);
            }
            // 잠김: 오른쪽 위 모서리 자물쇠(가운데는 흐린 시설 아이콘 자리)
            if (!open) { var lk = UIKit.IconImg(cell.transform, "lock", 20); UIKit.Anchor(lk.rectTransform, new Vector2(1, 1), new Vector2(-4, -4), new Vector2(20, 20)); }
            if (!settled && focus.Contains(z.key)) Frame(focusRoot, z);
        }
        focusRoot.SetAsLastSibling();
        focusCg = focusRoot.gameObject.AddComponent<CanvasGroup>(); focusCg.blocksRaycasts = false;

        // 더미 점: 얼음(열린 얼음밭)·레골리스(채굴장)
        for (int i = 0; i < WorldMap.ICE_SPOTS.Length; i++)
        {
            if (i >= GameConfig.BASE_SPOTS && G.PadLv("ice2") < 1) continue;
            Spot(WorldMap.ICE_SPOTS[i], GameConfig.ICE, 8);
        }
        if (G.PadLv("mine") >= 1) foreach (var r in WorldMap.REGO_SPOTS) Spot(r, GameConfig.REGO, 8);

        // 시설
        Fac(Stn("melter"), UIKit.Icon("snow"), "melt");
        Fac(Stn("bottler"), UIKit.Icon("bottle"), "bottle");
        Fac(Stn("counter"), UIKit.Icon("shelf"), "shop");
        Fac(Stn("register"), UIKit.Icon("cash"), "shop", 22);
        if (WorldMap.drillInstalled || G.PadLv("drill") >= 1) Fac(Stn("drill"), UIKit.Icon("drill"), "ice2");
        Fac(Stn("elec"), Glyph("tank"), "o2");
        Fac(Stn("purif"), Glyph("mound"), "soil");
        Fac(Stn("mixer"), Glyph("sack"), "soil");
        Fac(Stn("green"), Glyph("leaf"), "farm", 30);
        Fac(WorldMap.DOME, UIKit.Icon("home"), "dome");
        Fac(new Vector2(126, -515), UIKit.Icon("home"), "hab", 30);
        Fac(new Vector2(318, -515), UIKit.Icon("sun"), "solar", 30);
        Fac(new Vector2((WorldMap.PEN_X0 + WorldMap.PEN_X1) / 2, (WorldMap.PEN_Y0 + WorldMap.PEN_Y1) / 2), UIKit.Icon("rabbit"), "pen");
        // 착륙장: 회색 원판 + 깃발 + 이름
        var pad = UIKit.RoundPanel(mapRt, "lander", Dot, 30); pad.color = UIKit.Hex("c3cfe6"); Place(pad, M(WorldMap.LANDER.x, WorldMap.LANDER.y), new Vector2(30, 30));
        var fl = UIKit.IconImg(pad.transform, "flag", 20); var frt = fl.rectTransform; frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f); frt.anchoredPosition = new Vector2(0, 1);
        var ll = UIKit.Text(mapRt, "착륙장", 14, Color.white); UIKit.Stroke(ll, UIKit.EDGE, 1.4f); Place(ll, M(WorldMap.LANDER.x + 62, WorldMap.LANDER.y), new Vector2(80, 20));

        // 플레이어: 맥동 링 + 흰 점
        var ring = UIKit.RoundPanel(mapRt, "meRing", Soft, 24); ring.color = new Color(1f, 0.85f, 0.35f, 0.6f); meRing = Place(ring, Vector2.zero, new Vector2(24, 24));
        var me = UIKit.RoundPanel(mapRt, "me", Dot, 16); me.color = Color.white; meRt = Place(me, Vector2.zero, new Vector2(16, 16));
        var core = UIKit.RoundPanel(me.transform, "core", Soft, 8); core.color = UIKit.Hex("2a74e8"); var co = core.rectTransform; co.anchorMin = co.anchorMax = new Vector2(0.5f, 0.5f); co.anchoredPosition = Vector2.zero;
        UpdateMe();

        // 정착 완료: 가게와 돔 사이 빈 땅에 금색 리본
        if (settled)
        {
            var rib = UIKit.Chip(mapRt, "settled", UIKit.SignOk, "crown", 28, "정착 완료", 24, out _, out _, 6);
            var rrt = rib.rectTransform; rrt.anchorMin = rrt.anchorMax = new Vector2(0, 1); rrt.pivot = new Vector2(0.5f, 0.5f); rrt.anchoredPosition = M(200, 650);
        }
    }

    static Vector2 Stn(string key) { var s = WorldMap.STN[key]; return new Vector2(s.x, s.y); }
    bool ZoneOpen(WorldMap.Zone z) => z.open || (!string.IsNullOrEmpty(z.pad) && G.PadLv(z.pad) >= 1);
    bool ZoneOpen(string key) { foreach (var z in WorldMap.ZONES) if (z.key == key) return ZoneOpen(z); return true; }

    void Strip(float x0, float y0, float x1, float y1)
    {
        var s = UIKit.Panel(mapRt, "road", Soft); s.color = new Color(1f, 0.9f, 0.8f, 0.16f);
        var rt = s.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = M(x0, y0); rt.sizeDelta = new Vector2((x1 - x0) * S, (y1 - y0) * S);
    }
    void Spot(Vector2 p, Color c, float size)
    {
        var d = UIKit.RoundPanel(mapRt, "spot", Soft, size); d.color = c; Place(d, M(p.x, p.y), new Vector2(size, size));
    }
    void Fac(Vector2 p, Sprite sp, string zone, float size = 28)
    {
        var img = UIKit.RT(mapRt, "fac").gameObject.AddComponent<Image>(); img.sprite = sp; img.preserveAspect = true; img.raycastTarget = false;
        Place(img, M(p.x, p.y), new Vector2(size, size));
        if (!ZoneOpen(zone)) img.color = new Color(1, 1, 1, 0.35f);
    }
    // 금테(4변 막대 + 모서리 점), 구역보다 4 바깥
    void Frame(RectTransform parent, WorldMap.Zone z)
    {
        var f = UIKit.RT(parent, "f_" + z.key); f.anchorMin = f.anchorMax = new Vector2(0, 1); f.pivot = new Vector2(0, 1);
        f.anchoredPosition = M(z.x0, z.y0) + new Vector2(-4, 4); f.sizeDelta = new Vector2((z.x1 - z.x0) * S + 8, (z.y1 - z.y0) * S + 8);
        const float T = 3.5f;
        void Bar(Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var b = UIKit.RT(f, "bar").gameObject.AddComponent<Image>(); b.color = UIKit.GOLD; b.raycastTarget = false;
            var rt = b.rectTransform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        }
        Bar(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -T), Vector2.zero);
        Bar(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, T));
        Bar(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(T, 0));
        Bar(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-T, 0), Vector2.zero);
    }

    void UpdateMe()
    {
        if (meRt == null) return;
        Vector2 p = Player.I != null ? Player.I.pos : new Vector2(G.S.p.x, G.S.p.y);
        var at = M(Mathf.Clamp(p.x, 0, GameConfig.W), Mathf.Clamp(p.y, GameConfig.Y_MIN, GameConfig.H));
        meRt.anchoredPosition = at; meRing.anchoredPosition = at;
    }

    // ---- 체크리스트 ----
    void BuildList()
    {
        listRt = UIKit.RT(panel, "list"); listRt.anchorMin = listRt.anchorMax = new Vector2(0, 1); listRt.pivot = new Vector2(0, 1);
        listRt.anchoredPosition = new Vector2(LX, -TITLE_H); listRt.sizeDelta = new Vector2(LW, MH);
        int st = builtStage; float y = 0;
        for (int i = 0; i < SettlementManager.COUNT; i++)
        {
            var def = SettlementManager.STAGES[i];
            if (i < st) y += Simple(y, i, def, true) + 8;
            else if (i == st) y += Cur(y, i, def) + 8;
            else y += Simple(y, i, def, false) + 8;
        }
        if (SM != null && SM.Settled) Done(y + 4);
    }

    Image Card(float y, float h, UIKit.Style st, string name)
    {
        var c = UIKit.Panel(listRt, name, st);
        var rt = c.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(0, -y); rt.sizeDelta = new Vector2(LW, h);
        return c;
    }
    static RectTransform At(Component c, Vector2 anchor, Vector2 pos, Vector2 size) { var rt = c.GetComponent<RectTransform>(); UIKit.Anchor(rt, anchor, pos, size); return rt; }

    // 완료(✓) 또는 다음 단계(흐리게, 자물쇠) 한 줄
    float Simple(float y, int i, SettlementManager.StageDef def, bool done)
    {
        const float H = 44;
        var c = Card(y, H, done ? UIKit.CardDone : UIKit.Card, "stage" + i);
        var row = UIKit.Row(c.transform, "row", 6, TextAnchor.MiddleLeft);
        At(row, new Vector2(0, 0.5f), new Vector2(10, 1.5f), new Vector2(LW - 20, 34));
        UIKit.IconImg(row.transform, done ? "check" : "lock", done ? 24 : 20);
        var t = UIKit.Text(row.transform, (i + 1) + ". " + def.name, 20, done ? UIKit.SUB : Color.white, TextAnchor.MiddleLeft); UIKit.Stroke(t, UIKit.EDGE, 1.2f);
        if (!done) { var cg = c.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0.45f; }
        return H;
    }

    // 지금 단계: 제목 + 보상 + 항목(이름·값·막대 / 설치형은 체크)
    float Cur(float y, int i, SettlementManager.StageDef def)
    {
        var list = SM != null ? SM.Items(i) : new List<SettlementManager.Item>();
        float h = 46; foreach (var it in list) h += it.bin ? 32 : 48; h += 10;
        var c = Card(y, h, UIKit.CardCur, "stage" + i);
        var title = UIKit.Text(c.transform, (i + 1) + ". " + def.name, 22, Color.white, TextAnchor.MiddleLeft); UIKit.Stroke(title, UIKit.EDGE, 1.6f);
        At(title, new Vector2(0, 1), new Vector2(12, -8), new Vector2(150, 32));
        if (def.reward > 0)
        {
            var rw = UIKit.Row(c.transform, "reward", 3, TextAnchor.MiddleRight);
            At(rw, new Vector2(1, 1), new Vector2(-10, -8), new Vector2(90, 32));
            UIKit.IconImg(rw.transform, "bell", 20);
            var rt = UIKit.Text(rw.transform, "+" + def.reward.ToString("N0"), 18, UIKit.GOLD); UIKit.Stroke(rt, UIKit.EDGE, 1.2f);
        }
        float iy = 46;
        foreach (var it in list)
        {
            var ui = new ItemUI();
            var nm = UIKit.Text(c.transform, it.name, 18, UIKit.CREAM, TextAnchor.MiddleLeft); UIKit.Stroke(nm, UIKit.EDGE, 1f);
            At(nm, new Vector2(0, 1), new Vector2(14, -iy), new Vector2(LW - 80, 26));
            if (it.bin)
            {
                ui.mark = UIKit.IconImg(c.transform, it.Done ? "check" : "lock", 20);
                At(ui.mark, new Vector2(1, 1), new Vector2(-14, -iy - 3), new Vector2(20, 20));
                if (!it.Done) ui.mark.color = new Color(1, 1, 1, 0.6f);
                iy += 32;
            }
            else
            {
                ui.val = UIKit.Text(c.transform, "", 18, UIKit.GOLD, TextAnchor.MiddleRight); UIKit.Stroke(ui.val, UIKit.EDGE, 1.2f);
                At(ui.val, new Vector2(1, 1), new Vector2(-14, -iy), new Vector2(90, 26));
                ui.barW = LW - 28;
                var track = UIKit.ProgressBar(c.transform, "bar", new Vector2(ui.barW, 10), out ui.fill);
                At(track, new Vector2(0, 1), new Vector2(14, -iy - 28), new Vector2(ui.barW, 10));
                iy += 48;
            }
            items.Add(ui);
        }
        RefreshItems();
        return h;
    }

    // 정착 완료 카드: 왕관 + "정착 완료!" + [새 정착지로] (업그레이드 시트의 이주 카드로 안내)
    void Done(float y)
    {
        var c = Card(y, 132, UIKit.SignOk, "settled");
        var row = UIKit.Row(c.transform, "row", 6);
        At(row, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(LW - 20, 40));
        UIKit.IconImg(row.transform, "crown", 30);
        var t = UIKit.Text(row.transform, "정착 완료!", 26, UIKit.SignOk.text); UIKit.Stroke(t, Color.white, 1.2f);
        var b = UIKit.Button(c.transform, "go", UIKit.Gold, new Vector2(LW - 36, 56), "새 정착지로", 24, () =>
        {
            Close();
            if (UpgradeSheet.I != null) UpgradeSheet.I.Open(UpgradeSheet.UP);
        }, "up", 26);
        At(b, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(LW - 36, 56));
    }

    void RefreshItems()
    {
        if (SM == null || builtStage >= SettlementManager.COUNT) return;
        var list = SM.Items(builtStage);
        for (int k = 0; k < list.Count && k < items.Count; k++)
        {
            var it = list[k]; var ui = items[k];
            if (ui.val != null)
            {
                string s = Mathf.Min(it.cur, it.goal) + "/" + it.goal;
                if (ui.val.text != s) ui.val.text = s;
                ui.val.color = it.Done ? UIKit.Hex("8fe08a") : UIKit.GOLD;
                UIKit.SetBar(ui.fill, it.Frac, ui.barW);
            }
            else if (ui.mark != null)
            {
                UIKit.SetIcon(ui.mark, it.Done ? "check" : "lock");
                ui.mark.color = it.Done ? Color.white : new Color(1, 1, 1, 0.6f);
            }
        }
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        float dt = Time.unscaledDeltaTime; openT += dt;
        // 열 때 팝(0.25s), 화면이 낮으면 판 전체를 줄여 맞춘다
        var cr = ((RectTransform)canvas.transform).rect;
        float fit = Mathf.Min(1f, (cr.height - 24f) / PH, (cr.width - 12f) / PW);
        float pop = openT < 0.25f ? Mathf.Lerp(0.88f, 1f, JuiceFX.EaseOutBack(openT / 0.25f)) : 1f;
        panel.localScale = Vector3.one * fit * pop;
        // 단계가 바뀌었으면 다시 만들고, 아니면 0.25s마다 값만
        if (SM != null && SM.Stage != builtStage) { Build(); return; }
        refreshT += dt; if (refreshT >= 0.25f) { refreshT = 0; RefreshItems(); }
        UpdateMe();
        float k = (Time.unscaledTime * 1.2f) % 1f;
        if (meRing != null) { meRing.localScale = Vector3.one * (1f + k * 1.2f); var im = meRing.GetComponent<Image>(); var c = im.color; c.a = 0.7f * (1f - k); im.color = c; }
        if (focusCg != null) focusCg.alpha = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
    }

    // ---- 그림 아이콘(UIKit에 없는 것: 지도·산소통·흙더미·비료 자루·새싹). 24 격자 → 96px, 아래 1.5 그림자 ----
    static readonly Dictionary<string, Sprite> glyphs = new Dictionary<string, Sprite>();
    public static Sprite MapIcon() => Glyph("map");
    public static Sprite Glyph(string key)
    {
        if (glyphs.TryGetValue(key, out var sp)) return sp;
        const int PX = 96; float s = PX / 24f;
        var p = new UIKit.Pix(PX, PX);
        p.off = new Vector2(0, 1.5f * s); p.tint = new Color(0, 0, 0, 0.35f); DrawGlyph(p, s, key);
        p.off = Vector2.zero; p.tint = null; DrawGlyph(p, s, key);
        var tex = p.ToTexture();
        sp = Sprite.Create(tex, new Rect(0, 0, PX, PX), new Vector2(0.5f, 0.5f), 100f);
        glyphs[key] = sp; return sp;
    }
    static void DrawGlyph(UIKit.Pix p, float s, string key)
    {
        Vector2 V(float x, float y) => new Vector2(x * s, y * s);
        Color H(string h) => UIKit.Hex(h);
        float w = 1.5f * s;   // 외곽선 두께
        switch (key)
        {
            case "map":
                {
                    var all = new[] { V(3, 5), V(9, 3), V(15, 5), V(21, 3), V(21, 19), V(15, 21), V(9, 19), V(3, 21) };
                    p.Poly(all, H("7a4a00"), w * 0.9f);
                    p.Poly(new[] { V(3, 5), V(9, 3), V(9, 19), V(3, 21) }, H("fff1d0"));
                    p.Poly(new[] { V(9, 3), V(15, 5), V(15, 21), V(9, 19) }, H("ead19c"));
                    p.Poly(new[] { V(15, 5), V(21, 3), V(21, 19), V(15, 21) }, H("fff1d0"));
                    p.Seg(V(5, 17), V(9, 13), 1.3f * s, H("5cc45a")); p.Seg(V(9, 13), V(12.5f, 15), 1.3f * s, H("5cc45a"));
                    var pin = new[] { V(13.4f, 10.8f), V(18.6f, 10.8f), V(16, 15.8f) };
                    p.Circle(16 * s, 9 * s, 3.4f * s, H("8a1a10"), 0.9f * s); p.Poly(pin, H("8a1a10"), 0.9f * s);
                    p.Circle(16 * s, 9 * s, 3.4f * s, H("e04a3a")); p.Poly(pin, H("e04a3a"));
                    p.Circle(16 * s, 9 * s, 1.3f * s, Color.white);
                    break;
                }
            case "tank":   // 산소통(전기분해기)
                p.RRect(8 * s, 5 * s, 8 * s, 16.5f * s, 4 * s, H("3a5a80"), w); p.RRect(10 * s, 2 * s, 4 * s, 4 * s, 1 * s, H("3a5a80"), w);
                p.RRect(8 * s, 5 * s, 8 * s, 16.5f * s, 4 * s, H("e9f4ff")); p.RRect(10 * s, 2 * s, 4 * s, 4 * s, 1 * s, H("8f949e"));
                p.RRect(8 * s, 11 * s, 8 * s, 3.5f * s, 0, H("66c1f5"));
                p.Seg(V(10.4f, 7.5f), V(10.4f, 9.5f), 1.3f * s, Color.white);
                break;
            case "mound":   // 흙더미 + 물방울(토양 정화기)
                {
                    var dome = new List<Vector2>();
                    for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12f; dome.Add(V(12 - Mathf.Cos(a) * 8.5f, 20 - Mathf.Sin(a) * 9f)); }
                    var d = dome.ToArray();
                    p.Poly(d, H("3a2208"), w); p.Poly(d, H("8a5f40"));
                    p.Circle(9 * s, 16 * s, 1.1f * s, H("c79a72")); p.Circle(14 * s, 14.5f * s, 1.1f * s, H("c79a72")); p.Circle(15.5f * s, 18 * s, 1f * s, H("c79a72"));
                    var drop = new[] { V(18.5f, 1.8f), V(21, 6.2f), V(16, 6.2f) };
                    p.Circle(18.5f * s, 7f * s, 2.6f * s, H("2a78b0"), 0.9f * s); p.Poly(drop, H("2a78b0"), 0.9f * s);
                    p.Circle(18.5f * s, 7f * s, 2.6f * s, H("66c1f5")); p.Poly(drop, H("66c1f5"));
                    break;
                }
            case "sack":   // 비료 자루(배합기)
                {
                    var body = new[] { V(7, 9), V(17, 9), V(19.5f, 18), V(18, 21), V(6, 21), V(4.5f, 18) };
                    var neck = new[] { V(9, 9.5f), V(15, 9.5f), V(14, 4.5f), V(10, 4.5f) };
                    p.Poly(body, H("3e5a1a"), w); p.Poly(neck, H("3e5a1a"), w);
                    p.Poly(body, H("8fae4a")); p.Poly(neck, H("a9c862"));
                    p.Seg(V(9.2f, 8.5f), V(14.8f, 8.5f), 1.6f * s, H("5a3a10"));
                    p.Circle(12 * s, 15 * s, 2.2f * s, H("cfe39a"));
                    break;
                }
            case "leaf":   // 새싹 화분(온실)
                {
                    var l1 = new[] { V(12, 13), V(8, 12.5f), V(4.5f, 9), V(5.5f, 6.5f), V(9.5f, 8) };
                    var l2 = new[] { V(12, 10.5f), V(14, 6), V(18.5f, 3.5f), V(19.5f, 6.5f), V(16.5f, 10) };
                    p.Seg(V(12, 19), V(12, 9), 1.8f * s, H("1f5a22"), w * 0.5f);
                    p.Poly(l1, H("1f5a22"), w); p.Poly(l2, H("1f5a22"), w);
                    p.Seg(V(12, 19), V(12, 9), 1.8f * s, H("3aa83a"));
                    p.Poly(l1, H("5cc45a")); p.Poly(l2, H("8fe08a"));
                    p.RRect(6.5f * s, 17 * s, 11 * s, 5 * s, 1.5f * s, H("7a4a00"), w * 0.8f);
                    p.RRect(6.5f * s, 17 * s, 11 * s, 5 * s, 1.5f * s, H("c07a32"));
                    break;
                }
        }
    }
}

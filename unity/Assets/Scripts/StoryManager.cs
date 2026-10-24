using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 서사(docs/chapter2-design.md "A 서사"): 지구 관제 센터의 관제사 "하나"가 짧은 무전으로 말을 건다.
//  - 새 게임 인트로: 저장이 새것(S.sold==0 && S.story 비어 있음)이면 3컷 무전. 탭으로 넘기고 건너뛰기 가능. 끝나야 튜토리얼(TutorialManager)이 시작된다(IntroActive).
//  - 사건 무전: 화면 위쪽(HUD 알약 아래) 작은 카드 하나 = 초상 + 이름 + 한두 줄. 3~4초 뒤 접히고 탭하면 바로 닫힘.
//    같은 id는 S.story에 넣어 다시 안 뜬다. 여러 개면 큐로 차례대로, 시트·지도·설정·정착 배너·이주 연출 중이면 기다렸다가.
//  - 사건: 구역 처음 해제(GameState.OnPadLevel lv 1), 정착 단계 1~5(SettlementManager.OnStage), 본부 레벨 업(OnPadLevel "hq"),
//    첫 산소통·첫 비료·첫 식량·첫 VIP·첫 밤(0.5초마다 폴링). 옛 저장은 시작할 때 이미 지난 "첫 ○○"를 조용히 본 것으로 표시.
// 대본은 아래 LINES 표(id → 문장)에 모은다. 검증 인자(DevTools): -story id(그 무전 바로) · -intro(인트로 강제).
public class StoryManager : MonoBehaviour
{
    public static StoryManager I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public const string WHO = "관제사 하나";

    // ---- 대본 ----
    public static readonly string[] INTRO =
    {
        "착륙 확인! 여긴 지구 관제 센터, 관제사 하나예요. 개척 1호 기지에 온 걸 환영해요.",
        "곧 정착민들이 내려와요. 화성에서 살아남으려면 무엇보다 물이 먼저예요.",
        "주변 얼음을 주워 해빙기에 넣어 보세요. 녹으면 물이 돼요. 제가 계속 지켜볼게요!",
    };
    public static readonly Dictionary<string, string> LINES = new Dictionary<string, string>
    {
        // 구역 처음 해제 (zone_ + 발판 id)
        { "zone_ice2",  "얼음밭이 넓어졌어요. 얼음은 두 배, 걱정은 반!" },
        { "zone_o2",    "전기분해기 가동! 물통을 넣으면 산소가 나와요." },
        { "zone_mine",  "채굴장 개방! 붉은 흙 레골리스, 마음껏 캐세요." },
        { "zone_soil",  "토양 공장 가동. 레골리스를 씻으면 흙이 돼요." },
        { "zone_farm",  "온실 완성! 비료랑 물만 주면 감자가 자라요." },
        { "zone_solar", "태양광 연결! 해 있는 동안 기계들이 힘을 내요." },
        { "zone_hab",   "거주 구역 개방! 정착민들에게 집이 생겼어요." },
        { "zone_pen",   "먼지 토끼 우리 완성. 너무 오래 구경하면 안 돼요." },
        { "zone_dome",  "휴게 돔 개장! 정착민들도 숨 돌릴 곳이 생겼네요." },
        // 정착 단계 달성
        { "stage_1", "물 확보 성공! 첫 고비를 넘었어요. 다음은 공기!" },
        { "stage_2", "산소가 돌아요. 관제 센터에 박수가 터졌어요!" },
        { "stage_3", "흙이 살아났어요. 화성에 씨앗 심을 땅이 생겼어요." },
        { "stage_4", "첫 수확 달성! 정착민 식탁이 드디어 차려졌어요." },
        { "stage_5", "정착 완료! 개척 1호 기지가 진짜 마을이 됐어요." },
        // 처음 일어난 일
        { "first_o2",    "첫 산소통 완성! 다들 크게 한 번 숨 쉬어 봐요." },
        { "first_fert",  "첫 비료 완성. 냄새는… 헬멧 덕에 괜찮죠?" },
        { "first_food",  "온실에서 첫 식량이! 화성산 감자, 역사적이에요." },
        { "first_vip",   "VIP 정착민 도착! 지구 높은 분이래요. 잘 부탁해요." },
        { "first_night", "화성의 첫 밤이에요. 춥지만 별은 지구보다 잘 보여요." },
        // 기지 본부 레벨 업
        { "hq_1", "기지 본부가 섰어요! 무전이 한결 또렷해졌어요." },
        { "hq_2", "본부 Lv.2! 통신탑 덕에 지구 소식도 빨라져요." },
        { "hq_3", "본부 Lv.3! 관제 센터 동료들이 다들 부러워해요." },
        { "hq_4", "본부 Lv.4! 이제 지구 망원경으로도 기지가 보인대요." },
        { "hq_5", "본부 최고 레벨! 이제 제 도움 없이도 되겠어요." },
    };
    // 처음 해제하면 무전하는 구역 발판
    static readonly HashSet<string> ZONE_PADS = new HashSet<string> { "ice2", "o2", "mine", "soil", "farm", "solar", "hab", "pen", "dome" };

    // ---- 상태 ----
    class Msg { public string id, text; public float readyAt; }
    readonly List<Msg> queue = new List<Msg>();
    bool introPending, introShowing;
    /// 인트로가 곧 뜨거나 떠 있음(튜토리얼 말풍선·손 포인터는 이 동안 숨는다)
    public bool IntroActive => introPending || introShowing;

    // 사건 카드
    RectTransform card; CanvasGroup cardCg; Msg curMsg; Text cardMsg; float cardT, cardLife; bool cardClosing;
    // 인트로
    GameObject introRoot; RectTransform introPanel; Text introText; Image[] introDots; Button introBtn; int introCut; float typeT; bool typing;
    bool AutoIntro => DevArgs.Has("-autoplay") || DevArgs.Has("-shot");   // QA 실행은 컷을 1.5초마다 저절로 넘긴다
    float autoT;

    void Awake()
    {
        I = this;
        // 새 저장이면 인트로 예약(첫 프레임 뒤 다시 확인: DevTools -sold 등이 Start에서 바꿀 수 있음)
        introPending = G != null && S.sold == 0 && S.story.Count == 0;
    }

    void Start()
    {
        G.OnPadLevel += OnPad;
        if (SettlementManager.I != null) SettlementManager.I.OnStage += n => Push("stage_" + n, 1.0f);
        // 옛 저장: 이미 지난 "첫 ○○"는 조용히 본 것으로(업데이트 직후 한꺼번에 쏟아지지 않게)
        foreach (var id in new[] { "first_o2", "first_fert", "first_food", "first_vip", "first_night" })
            if (!Seen(id) && FirstMet(id, true)) S.story.Add(id);
        StartCoroutine(CoBoot());
    }

    void OnDestroy() { if (G != null) G.OnPadLevel -= OnPad; }

    // ---- 검증 인자(DevTools가 부름, 앱 실행당 1회) ----
    public void DevIntro()
    {
        S.story.Remove("intro"); introPending = true;
    }
    public void DevStory(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (id == "intro") { DevIntro(); return; }
        S.story.Remove(id); Push(id, 0.6f);
    }

    IEnumerator CoBoot()
    {
        yield return null;   // 모든 Start(DevTools 인자 포함)가 끝난 뒤
        if (introPending && S.sold > 0 && !S.story.Contains("intro") && !DevArgs.Has("-intro")) introPending = false;
        if (introPending) ShowIntro();
    }

    // ---- 사건 ----
    void OnPad(string id, int lv)
    {
        if (id == "hq" && lv >= 1) Push("hq_" + Mathf.Min(lv, 5), 1.2f);
        else if (lv == 1 && ZONE_PADS.Contains(id)) Push("zone_" + id, 0.9f);   // 해제 연출(색종이·울타리)이 먼저 보이게 잠깐 뒤
    }

    bool Seen(string id) => S.story.Contains(id);

    /// 무전 예약(같은 id는 한 번만). delay초 뒤부터 뜰 수 있음
    public void Push(string id, float delay = 0f)
    {
        if (G == null || Seen(id) || !LINES.TryGetValue(id, out var text)) return;
        foreach (var m in queue) if (m.id == id) return;
        queue.Add(new Msg { id = id, text = text, readyAt = Time.unscaledTime + delay });
    }

    // 폴링 사건: 처음 일어났는지. boot=true면 옛 저장 판정(지난 일로 볼지)
    bool FirstMet(string id, bool boot)
    {
        switch (id)
        {
            case "first_o2": return S.elecOut > 0 || S.supO > 0 || S.shelfO2 > 0 || G.CarryTop == "o2";
            case "first_fert": return S.fertMade > 0;
            case "first_food": return S.grnOut > 0 || S.supF > 0 || S.shelfFood > 0 || G.CarryTop == "food";
            case "first_vip":
                if (boot) return S.sold >= 40 && S.custN >= 10;   // 10번째 손님마다 VIP(판매 40 이후)
                if (CustomerSystem.I != null) foreach (var c in CustomerSystem.I.list) if (c.vip && c.st != Customer.St.Gone) return true;
                return false;
            case "first_night":
                if (boot) return S.clock >= DayNight.DAY_LEN * 0.58f;
                return DayNight.I != null && DayNight.I.night > 0.8f;
        }
        return false;
    }

    // 화면이 다른 것으로 바쁠 때(카드를 띄우지 않고 기다림)
    bool Busy()
    {
        if (IntroActive) return true;
        if (UpgradeSheet.I != null && UpgradeSheet.I.IsOpen) return true;
        if (MapScreen.I != null && MapScreen.I.IsOpen) return true;
        if (PrestigeManager.I != null && PrestigeManager.I.Busy) return true;
        if (HUD.I == null) return true;
        if (HUD.I.SettingsOpen) return true;
        var h = HUD.I.transform;
        return h.Find("stageBanner") != null || h.Find("settledBanner") != null || h.Find("offlineModal") != null;
    }

    float pollT;
    void Update()
    {
        if (G == null) return;
        if (introShowing) { UpdateIntro(); return; }
        pollT += Time.unscaledDeltaTime;
        if (pollT >= 0.5f)
        {
            pollT = 0;
            foreach (var id in new[] { "first_o2", "first_fert", "first_food", "first_vip", "first_night" })
                if (!Seen(id) && FirstMet(id, false)) Push(id, id == "first_vip" ? 0.4f : 0.2f);
        }
        bool busy = Busy();
        if (card != null)
        {
            cardT += Time.unscaledDeltaTime;
            if (!cardClosing && (busy || cardT >= cardLife))
            {
                // 시트·지도 등이 막 열려 카드가 금방 가려졌으면 닫힌 뒤 다시 띄운다
                if (busy && cardT < 1.5f && curMsg != null) { queue.Insert(0, curMsg); Debug.Log("story card " + curMsg.id + " 가려짐 → 다시 대기"); }
                CloseCard(busy ? 0.12f : 0.3f);
            }
            return;
        }
        if (busy || queue.Count == 0) return;
        for (int i = 0; i < queue.Count; i++)
            if (Time.unscaledTime >= queue[i].readyAt) { var m = queue[i]; queue.RemoveAt(i); ShowCard(m); break; }
    }

    // ---- 사건 카드(화면 위쪽, HUD 2행 아래) ----
    const float CARD_TOP = -152f, CARD_H = 92f;
    float CanvasW => HUD.I != null ? ((RectTransform)HUD.I.transform).rect.width : 540f;

    void ShowCard(Msg m)
    {
        if (!Seen(m.id)) S.story.Add(m.id);
        curMsg = m; m.readyAt = 0;
        float w = Mathf.Min(470f, CanvasW - 32f);
        var bg = UIKit.Panel(HUD.I.transform, "storyCard", UIKit.Navy); bg.raycastTarget = true;
        card = bg.rectTransform; card.anchorMin = card.anchorMax = new Vector2(0.5f, 1); card.pivot = new Vector2(0.5f, 1);
        card.sizeDelta = new Vector2(w, CARD_H); card.anchoredPosition = new Vector2(0, CARD_TOP + 16f);
        cardCg = bg.gameObject.AddComponent<CanvasGroup>(); cardCg.alpha = 0;
        var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (!cardClosing) CloseCard(0.15f); });
        float body = CARD_H - UIKit.Navy.shadowH;   // 그림자 띠를 뺀 본문 높이
        var pic = Portrait(bg.transform, 62f);
        UIKit.Anchor(pic.rectTransform, new Vector2(0, 1), new Vector2(14, -(body - 62f) / 2f), new Vector2(62, 62));
        var name = UIKit.Text(bg.transform, WHO, 17, UIKit.SUB, TextAnchor.UpperLeft);
        UIKit.Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(88, -10), new Vector2(w - 104, 22));
        cardMsg = UIKit.Text(bg.transform, m.text, 22, Color.white, TextAnchor.UpperLeft);
        cardMsg.horizontalOverflow = HorizontalWrapMode.Wrap; cardMsg.verticalOverflow = VerticalWrapMode.Truncate;
        cardMsg.resizeTextForBestFit = true; cardMsg.resizeTextMinSize = 16; cardMsg.resizeTextMaxSize = 22; cardMsg.lineSpacing = 0.95f;
        UIKit.Anchor(cardMsg.rectTransform, new Vector2(0, 1), new Vector2(88, -32), new Vector2(w - 104, body - 40));
        cardT = 0; cardClosing = false;
        cardLife = Mathf.Clamp(2.6f + m.text.Length * 0.04f, 3f, 4.2f);
        AudioManager.I?.Play("tick", 1.6f, 0.5f); AudioManager.I?.Play("pop", 1.7f, 0.25f);   // 무전 잡음 같은 짧은 딸깍
        Debug.Log("story card " + m.id + ": " + m.text);
        StartCoroutine(CoCardIn(card, cardCg));
    }

    IEnumerator CoCardIn(RectTransform rt, CanvasGroup cg)
    {
        float el = 0;
        while (el < 0.25f && rt != null && !cardClosing)
        {
            el += Time.unscaledDeltaTime; float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(el / 0.25f), 3f);
            rt.anchoredPosition = new Vector2(0, CARD_TOP + 16f * (1f - e)); cg.alpha = e; yield return null;
        }
        if (rt != null && !cardClosing) { rt.anchoredPosition = new Vector2(0, CARD_TOP); cg.alpha = 1; }
    }

    void CloseCard(float dur)
    {
        if (card == null) return;
        cardClosing = true;
        StartCoroutine(CoCardOut(card, cardCg, dur));
    }
    // 접힘: 세로로 납작해지며 위로 사라짐
    IEnumerator CoCardOut(RectTransform rt, CanvasGroup cg, float dur)
    {
        float el = 0; var p0 = rt.anchoredPosition; float a0 = cg.alpha;
        while (el < dur && rt != null)
        {
            el += Time.unscaledDeltaTime; float e = Mathf.Clamp01(el / dur);
            rt.localScale = new Vector3(1f, 1f - 0.35f * e, 1f); rt.anchoredPosition = p0 + new Vector2(0, 10f * e); cg.alpha = a0 * (1f - e); yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
        if (card == rt) { card = null; cardCg = null; cardMsg = null; cardClosing = false; curMsg = null; }
    }

    // ---- 인트로(3컷) ----
    void ShowIntro()
    {
        introPending = false; introShowing = true; introCut = 0; autoT = 0;
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
        if (card != null) { Destroy(card.gameObject); card = null; cardClosing = false; curMsg = null; }
        var h = HUD.I.transform;
        // 화면 막(어디를 눌러도 다음 컷)
        var scrim = UIKit.RT(h, "storyIntro").gameObject.AddComponent<Image>(); introRoot = scrim.gameObject;
        scrim.color = new Color(UIKit.SCRIM.r, UIKit.SCRIM.g, UIKit.SCRIM.b, 0.45f); scrim.raycastTarget = true;
        var srt = scrim.rectTransform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
        var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(Next);
        scrim.transform.SetAsLastSibling();

        // 판은 화면 아래쪽(업그레이드 버튼 위): 가운데의 플레이어·착륙 지점이 가려지지 않게
        float w = Mathf.Min(480f, CanvasW - 32f), ph = 262f, py = -230f;
        var panel = UIKit.Panel(scrim.transform, "panel", UIKit.Navy); panel.raycastTarget = false;   // 판 위를 눌러도 다음 컷
        introPanel = panel.rectTransform; UIKit.Anchor(introPanel, new Vector2(0.5f, 0.5f), new Vector2(0, py), new Vector2(w, ph));
        var pic = Portrait(panel.transform, 80f);
        UIKit.Anchor(pic.rectTransform, new Vector2(0, 1), new Vector2(20, -18), new Vector2(80, 80));
        var name = UIKit.Text(panel.transform, WHO, 26, Color.white, TextAnchor.LowerLeft); UIKit.Stroke(name, UIKit.EDGE, 1.6f);
        UIKit.Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(116, -24), new Vector2(w - 136, 36));
        var sub = UIKit.Text(panel.transform, "지구 관제 센터 · 무전 수신 중", 17, UIKit.SUB, TextAnchor.UpperLeft);
        UIKit.Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(116, -62), new Vector2(w - 136, 24));
        introText = UIKit.Text(panel.transform, "", 24, Color.white, TextAnchor.UpperLeft);
        introText.horizontalOverflow = HorizontalWrapMode.Wrap; introText.verticalOverflow = VerticalWrapMode.Overflow; introText.lineSpacing = 1.05f;
        UIKit.Anchor(introText.rectTransform, new Vector2(0, 1), new Vector2(22, -112), new Vector2(w - 44, 84));
        // 아래: 컷 점 3개(좌) · 다음/시작(우) · 건너뛰기는 판 바깥 오른쪽 위
        var dots = UIKit.Row(panel.transform, "dots", 8, TextAnchor.MiddleLeft);
        UIKit.Anchor(dots.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(24, 22), new Vector2(80, 16));
        introDots = new Image[INTRO.Length];
        for (int i = 0; i < INTRO.Length; i++)
        {
            var d = UIKit.RT(dots.transform, "dot").gameObject.AddComponent<Image>(); d.sprite = Dot(); d.raycastTarget = false;
            var le = d.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = 12;
            introDots[i] = d;
        }
        introBtn = UIKit.Button(panel.transform, "next", UIKit.Blue, new Vector2(132, 48), "다음", 22, Next);
        UIKit.Anchor(introBtn.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(-18, 12), new Vector2(132, 48));
        var skip = UIKit.Button(scrim.transform, "skip", UIKit.Gray, new Vector2(124, 44), "건너뛰기", 20, EndIntro);
        UIKit.Anchor(skip.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(w / 2f - 62f, py + ph / 2f + 34f), new Vector2(124, 44));
        SetCut(0);
        Debug.Log("story intro start");
        StartCoroutine(CoPop(introPanel, 0.3f));
    }

    void SetCut(int i)
    {
        introCut = i; typeT = 0; typing = true; introText.text = "";
        for (int k = 0; k < introDots.Length; k++) introDots[k].color = k == i ? Color.white : new Color(1, 1, 1, k < i ? 0.55f : 0.22f);
        bool last = i >= INTRO.Length - 1;
        UIKit.Restyle(introBtn, last ? UIKit.Gold : UIKit.Blue, last ? "시작!" : "다음");
        AudioManager.I?.Play("tick", 1.4f, 0.5f);
        if (i > 0) StartCoroutine(CoPop(introPanel, 0.18f));
    }

    void UpdateIntro()
    {
        // 인트로 중엔 첫 정착민이 내려오지 않게 스폰 타이머를 붙잡아 둔다
        if (S.spawnT < GameConfig.FIRST_SPAWN) S.spawnT = GameConfig.FIRST_SPAWN;
        if (typing)
        {
            typeT += Time.unscaledDeltaTime;
            var full = INTRO[introCut]; int n = Mathf.Min(full.Length, Mathf.FloorToInt(typeT * 42f));
            introText.text = full.Substring(0, n);
            if (n >= full.Length) typing = false;
        }
        if (AutoIntro) { autoT += Time.unscaledDeltaTime; if (autoT >= 1.5f) { autoT = 0; typing = false; Next(); } }
    }

    // 탭: 글자가 나오는 중이면 한 번에 다 보이고, 다 보였으면 다음 컷
    void Next()
    {
        if (!introShowing) return;
        if (typing) { typing = false; introText.text = INTRO[introCut]; return; }
        if (introCut + 1 < INTRO.Length) { SetCut(introCut + 1); AudioManager.I?.Play("pop", 1.4f, 0.3f); }
        else EndIntro();
    }

    void EndIntro()
    {
        if (!introShowing) return;
        introShowing = false;
        if (!Seen("intro")) S.story.Add("intro");
        G.Save();
        if (FloatingJoystick.I && (UpgradeSheet.I == null || !UpgradeSheet.I.IsOpen)) FloatingJoystick.I.suppressed = false;
        AudioManager.I?.Play("up", 1.2f, 0.5f);
        Debug.Log("story intro end at cut " + (introCut + 1));
        if (introRoot != null) StartCoroutine(CoFadeOut(introRoot, 0.25f));
        introRoot = null; introPanel = null; introText = null; introBtn = null; introDots = null;
    }

    static IEnumerator CoPop(RectTransform rt, float dur)
    {
        float el = 0;
        while (el < dur && rt != null) { el += Time.unscaledDeltaTime; rt.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, JuiceFX.EaseOutBack(Mathf.Clamp01(el / dur))); yield return null; }
        if (rt != null) rt.localScale = Vector3.one;
    }
    static IEnumerator CoFadeOut(GameObject go, float dur)
    {
        var cg = go.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false;
        float el = 0;
        while (el < dur && go != null) { el += Time.unscaledDeltaTime; cg.alpha = 1f - Mathf.Clamp01(el / dur); yield return null; }
        if (go != null) Destroy(go);
    }

    // ---- 초상: 코드로 그린 원형 헤드셋 아이콘(관제사 하나) ----
    static Sprite portrait, dot;
    static Image Portrait(Transform parent, float size)
    {
        var img = UIKit.RT(parent, "portrait").gameObject.AddComponent<Image>();
        img.sprite = PortraitSprite(); img.preserveAspect = true; img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }
    static Sprite PortraitSprite()
    {
        if (portrait != null) return portrait;
        const int N = 128; float s = N / 96f;   // 96 격자로 설계해 128px로 굽는다
        var p = new UIKit.Pix(N, N);
        Vector2 V(float x, float y) => new Vector2(x * s, y * s);
        Vector2 C0 = V(48, 48); float R = 44 * s;
        float InBg(Vector2 q) => (q - C0).magnitude - R;   // 배경 원 안쪽만(클립)
        Rect all = new Rect(0, 0, N, N);
        Color edge = UIKit.EDGE, navy = UIKit.Hex("2b3a55"), ink = UIKit.Hex("1b2740");
        // 배경 원: 남색 테두리 + 하늘색 그라데이션
        p.Circle(C0.x, C0.y, 47 * s, edge);
        p.Draw(InBg, all, Color.white, 0, y => Color.Lerp(UIKit.Hex("7cc0ff"), UIKit.Hex("3f7fd6"), Mathf.Clamp01(y / N)));
        // 어깨(흰 작업복, 배경 원 안에서 잘림) + 깃 남색 띠
        var sh = V(48, 104); float shR = 34 * s;
        p.Draw(q => Mathf.Max((q - sh).magnitude - shR, InBg(q)), all, UIKit.Hex("f3efe8"));
        p.Draw(q => Mathf.Max(Mathf.Abs((q - sh).magnitude - shR + 3 * s) - 2.2f * s, InBg(q)), all, UIKit.Hex("5b7bb5"));
        // 목
        p.RRect(V(43, 60).x, V(43, 60).y, 10 * s, 12 * s, 3 * s, UIKit.Hex("eec3a0"));
        // 머리카락(단발) 뒤판
        p.Circle(V(48, 42).x, V(48, 42).y, 23 * s, UIKit.Hex("3a2a2e"));
        p.RRect(V(25, 42).x, V(25, 42).y, 46 * s, 22 * s, new Vector4(0, 0, 8 * s, 8 * s), UIKit.Hex("3a2a2e"));
        // 얼굴
        p.Circle(V(48, 47).x, V(48, 47).y, 17.5f * s, UIKit.Hex("f7d4b4"));
        // 앞머리(위쪽 반원 덮개)
        var hc = V(48, 40); float hr = 19 * s;
        p.Draw(q => Mathf.Max((q - hc).magnitude - hr, q.y - (V(0, 39).y + (q.x - hc.x) * 0.18f)), all, UIKit.Hex("3a2a2e"));
        // 눈·볼·입
        p.Circle(V(41.5f, 49).x, V(41.5f, 49).y, 2.3f * s, ink); p.Circle(V(54.5f, 49).x, V(54.5f, 49).y, 2.3f * s, ink);
        p.Circle(V(38, 54).x, V(38, 54).y, 3f * s, new Color(1f, 0.45f, 0.45f, 0.35f)); p.Circle(V(58, 54).x, V(58, 54).y, 3f * s, new Color(1f, 0.45f, 0.45f, 0.35f));
        var mc = V(48, 53.5f); float mr = 4.2f * s;
        p.Draw(q => Mathf.Max(Mathf.Abs((q - mc).magnitude - mr) - 0.9f * s, mc.y + 1.2f * s - q.y), all, ink);
        // 헤드셋: 머리 위 띠(위쪽 반원) + 귀덮개 + 마이크 대
        var bc = V(48, 46); float br = 25 * s;
        p.Draw(q => Mathf.Max(Mathf.Abs((q - bc).magnitude - br) - 2.6f * s, q.y - bc.y), all, navy);
        p.RRect(V(19, 39).x, V(19, 39).y, 10 * s, 17 * s, 4.5f * s, navy);
        p.RRect(V(67, 39).x, V(67, 39).y, 10 * s, 17 * s, 4.5f * s, navy);
        p.RRect(V(21.5f, 42).x, V(21.5f, 42).y, 4 * s, 11 * s, 2 * s, UIKit.Hex("8fd0ff"));
        p.Seg(V(24, 55), V(36, 63), 2.6f * s, navy);
        p.Circle(V(38, 63.5f).x, V(38, 63.5f).y, 3.4f * s, navy);
        var tex = p.ToTexture();
        portrait = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return portrait;
    }
    static Sprite Dot()
    {
        if (dot != null) return dot;
        var p = new UIKit.Pix(32, 32); p.Circle(16, 16, 14, Color.white);
        var tex = p.ToTexture(); dot = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
        return dot;
    }
}

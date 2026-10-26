using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 서사(docs/chapter2-design.md "A 서사"): 지구 관제 센터의 관제사 "하나"가 짧은 무전으로 말을 건다.
//  - 새 게임 인트로: 저장이 새것(S.sold==0 && S.story 비어 있음)이면 3컷 무전. 탭으로 넘기고 건너뛰기 가능. 끝나야 튜토리얼(TutorialManager)이 시작된다(IntroActive).
//  - 사건 무전: 화면 위쪽(HUD 알약 아래) 작은 카드 하나 = 초상 + 한 줄(12자 안팎). 2.8초 뒤 접히고 탭하면 바로 닫힘.
//    같은 id는 S.story에 넣어 다시 안 뜬다. 여러 개면 큐로 차례대로, 시트·지도·설정·정착 배너·이주 연출 중이면 기다렸다가.
//  - 사건: 새 지역·핵심 시설 처음 해제(GameState.OnPadLevel lv 1, ZONE_PADS), 본부 처음 세움(hq Lv1), 2장 시작(ch2_start, SettlementManager.Complete가 Push).
//    정착 단계 무전은 대본에서 뺐다(Push해도 대본에 없으면 조용히 무시). 이미 2장(S.stage ≥ 5)인데 2장 시작 무전을 못 봤으면 한 번 띄운다.
//  - 엔딩(10단계 화성 마을 선포): 인트로와 같은 탭으로 넘기는 판에 3컷(ENDING) → 마지막 [출발!]이 업그레이드 시트(이주 카드)를 연다. 본 뒤 S.story "ending".
//    SettlementManager가 QueueEnding으로 예약(큰 축하판이 사라진 뒤), 이미 선포한 저장인데 못 봤으면 시작할 때 예약.
// 대본은 아래 INTRO·ENDING·LINES 표에 모은다. 검증 인자(DevTools): -story id(그 무전 바로, "ending"이면 엔딩 컷) · -intro(인트로 강제).
public class StoryManager : MonoBehaviour
{
    public static StoryManager I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public const string WHO = "관제사 하나";

    // ---- 대본 ----
    // 플레이 화면 글자 규칙(docs/art/art-bible.md): 무전은 정말 중요한 순간만, 한 줄 12자 안팎.
    //  남김: 인트로·엔딩 컷, 2장 시작, 새 지역·핵심 시설 처음 해제(산소·채굴장·온실·태양광·거주 구역), 본부 처음 세움.
    //  뺌(10/26 텍스트 다이어트): 덜 중요한 구역(얼음밭 확장·토양 공장·토끼 우리·휴게 돔), 정착 단계 1~9(금색 배너가 알림),
    //  첫 산소·비료·식량·VIP·첫 밤, 본부 Lv2~5(지도 알약이 반짝임), 화성 이벤트(HUD.IconToast 아이콘 알약으로)
    public static readonly string[] INTRO =
    {
        "착륙 확인! 관제사 하나예요.",
        "살아남으려면 물이 먼저예요.",
        "얼음을 주워 해빙기에 넣어요!",
    };
    // 10단계 화성 마을 선포 엔딩(인트로와 같은 판, 탭으로 넘김)
    public static readonly string[] ENDING =
    {
        "오늘부터 정식 화성 마을!",
        "돔마다 불이 켜졌어요.",
        "새 착륙지가 기다려요!",
    };
    public static readonly Dictionary<string, string> LINES = new Dictionary<string, string>
    {
        // 새 지역·핵심 시설 처음 해제 (zone_ + 발판 id)
        { "zone_o2",    "이제 산소를 만들어요!" },
        { "zone_mine",  "채굴장이 열렸어요!" },
        { "zone_farm",  "온실에서 감자가 자라요!" },
        { "zone_solar", "태양광 연결 완료!" },
        { "zone_hab",   "정착민 집이 생겼어요!" },
        // 2장 마을 시작(1장 완료 직후)
        { "ch2_start", "2장: 마을을 만들어요!" },
        // 기지 본부 처음 세움
        { "hq_1", "기지 본부가 섰어요!" },
    };
    // 처음 해제하면 무전하는 구역 발판
    static readonly HashSet<string> ZONE_PADS = new HashSet<string> { "o2", "mine", "farm", "solar", "hab" };

    // ---- 상태 ----
    class Msg { public string id, text; public float readyAt; public bool repeat; }
    readonly List<Msg> queue = new List<Msg>();
    bool introPending, introShowing;   // introShowing: 컷 판(인트로·엔딩)이 떠 있음
    bool endingPending; float endingAt;
    /// 인트로가 곧 뜨거나 컷 판(인트로·엔딩)이 떠 있음(튜토리얼 말풍선·손 포인터는 이 동안 숨는다)
    public bool IntroActive => introPending || introShowing;

    // 사건 카드
    RectTransform card; CanvasGroup cardCg; Msg curMsg; Text cardMsg; float cardT, cardLife; bool cardClosing;
    // 인트로
    GameObject introRoot; RectTransform introPanel; Text introText; Image[] introDots; Button introBtn; int introCut; float typeT; bool typing;
    string[] cuts = INTRO; string cutsId = "intro", lastLabel = "시작!"; System.Action onLast;   // 지금 컷 판의 대본·본 표시 id·마지막 버튼
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
        if (id == "ending") { S.story.Remove("ending"); QueueEnding(0.6f); return; }
        S.story.Remove(id); Push(id, 0.6f);
    }

    IEnumerator CoBoot()
    {
        yield return null;   // 모든 Start(DevTools 인자 포함)가 끝난 뒤
        if (introPending && S.sold > 0 && !S.story.Contains("intro") && !DevArgs.Has("-intro")) introPending = false;
        if (introPending) ShowIntro();
        // 옛 저장·검증 인자(-stage N): 이미 2장인데 2장 시작 무전을 못 봤으면, 이미 마을을 선포했는데 엔딩을 못 봤으면
        var sm = SettlementManager.I;
        if (sm != null && sm.Stage >= SettlementManager.PER_CHAPTER && !Seen("ch2_start")) Push("ch2_start", 1.5f);
        if (sm != null && sm.Settled && !Seen("ending")) QueueEnding(1.5f);
    }

    /// 엔딩 컷 예약(delay초 뒤부터, 화면이 한가해지면). 한 번 본 뒤엔 무시
    public void QueueEnding(float delay = 0f)
    {
        if (G == null || Seen("ending")) return;
        endingPending = true; endingAt = Time.unscaledTime + delay;
    }

    // ---- 사건 ----
    void OnPad(string id, int lv)
    {
        if (id == "hq" && lv == 1) Push("hq_1", 1.2f);
        else if (lv == 1 && ZONE_PADS.Contains(id)) Push("zone_" + id, 0.9f);   // 해제 연출(색종이·울타리)이 먼저 보이게 잠깐 뒤
    }

    bool Seen(string id) => S.story.Contains(id);

    /// 무전 예약(같은 id는 한 번만). delay초 뒤부터 뜰 수 있음. repeat=true면 S.story에 남기지 않아 다음에도 뜬다(화성 이벤트)
    public void Push(string id, float delay = 0f, bool repeat = false)
    {
        if (G == null || (!repeat && Seen(id)) || !LINES.TryGetValue(id, out var text)) return;
        foreach (var m in queue) if (m.id == id) return;
        queue.Add(new Msg { id = id, text = text, readyAt = Time.unscaledTime + delay, repeat = repeat });
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

    void Update()
    {
        if (G == null) return;
        if (introShowing) { UpdateIntro(); return; }
        bool busy = Busy();
        // 엔딩 컷: 카드가 없고 화면이 한가할 때 맨 먼저
        if (endingPending && !busy && card == null && Time.unscaledTime >= endingAt && SafeArea.Find(HUD.I.transform, "chest") == null) { endingPending = false; ShowEnding(); return; }   // 목표 상자 팝(QuestManager)과도 겹치지 않게
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

    // ---- 사건 카드(화면 위쪽, HUD 2행 아래): 초상 + 한 줄만(이름·설명 줄 없음) ----
    const float CARD_TOP = -152f, CARD_H = 66f;
    float CanvasW => HUD.I != null ? ((RectTransform)HUD.I.transform).rect.width : 540f;

    void ShowCard(Msg m)
    {
        if (!m.repeat && !Seen(m.id)) S.story.Add(m.id);
        curMsg = m; m.readyAt = 0;
        float w = Mathf.Min(360f, CanvasW - 32f);
        var bg = UIKit.Panel(HUD.I.transform, "storyCard", UIKit.Navy); bg.raycastTarget = true;
        card = bg.rectTransform; card.anchorMin = card.anchorMax = new Vector2(0.5f, 1); card.pivot = new Vector2(0.5f, 1);
        card.sizeDelta = new Vector2(w, CARD_H); card.anchoredPosition = new Vector2(0, CARD_TOP + 16f);
        cardCg = bg.gameObject.AddComponent<CanvasGroup>(); cardCg.alpha = 0;
        var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (!cardClosing) CloseCard(0.15f); });
        float body = CARD_H - UIKit.Navy.shadowH;   // 그림자 띠를 뺀 본문 높이
        var pic = Portrait(bg.transform, 48f);
        UIKit.Anchor(pic.rectTransform, new Vector2(0, 1), new Vector2(10, -(body - 48f) / 2f), new Vector2(48, 48));
        cardMsg = UIKit.Text(bg.transform, m.text, 24, Color.white, TextAnchor.MiddleLeft);
        cardMsg.horizontalOverflow = HorizontalWrapMode.Wrap; cardMsg.verticalOverflow = VerticalWrapMode.Truncate;
        cardMsg.resizeTextForBestFit = true; cardMsg.resizeTextMinSize = 16; cardMsg.resizeTextMaxSize = 24;
        UIKit.Stroke(cardMsg, UIKit.EDGE, 1.2f);
        UIKit.Anchor(cardMsg.rectTransform, new Vector2(0, 1), new Vector2(68, -4), new Vector2(w - 80, body - 8));
        cardT = 0; cardClosing = false;
        cardLife = 2.8f;   // 한 줄이라 짧게
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

    // ---- 컷 판: 인트로(3컷)·엔딩(3컷)이 같은 판을 쓴다 ----
    void ShowIntro()
    {
        introPending = false;
        ShowCuts(INTRO, "intro", "시작!", null);
    }

    // 엔딩: 마지막 [새 정착지로] → 업그레이드 시트(맨 위 이주 카드)
    void ShowEnding()
    {
        ShowCuts(ENDING, "ending", "출발!", () =>
        {
            if (UpgradeSheet.I != null) UpgradeSheet.I.Open(UpgradeSheet.UP);
        });
        AudioManager.I?.Play("ding", 1.1f, 0.5f);
    }

    void ShowCuts(string[] lines, string id, string last, System.Action onLastCut)
    {
        cuts = lines; cutsId = id; lastLabel = last; onLast = onLastCut;
        introShowing = true; introCut = 0; autoT = 0;
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
        float w = Mathf.Min(440f, CanvasW - 32f), ph = 172f, py = -250f;
        var panel = UIKit.Panel(scrim.transform, "panel", UIKit.Navy); panel.raycastTarget = false;   // 판 위를 눌러도 다음 컷
        introPanel = panel.rectTransform; UIKit.Anchor(introPanel, new Vector2(0.5f, 0.5f), new Vector2(0, py), new Vector2(w, ph));
        // 초상(좌) + 한 줄(우). 이름·부제 줄은 없앴다(텍스트 다이어트 10/26)
        var pic = Portrait(panel.transform, 80f);
        UIKit.Anchor(pic.rectTransform, new Vector2(0, 1), new Vector2(18, -16), new Vector2(80, 80));
        introText = UIKit.Text(panel.transform, "", 28, Color.white, TextAnchor.MiddleLeft); UIKit.Stroke(introText, UIKit.EDGE, 1.4f);
        introText.horizontalOverflow = HorizontalWrapMode.Wrap; introText.verticalOverflow = VerticalWrapMode.Overflow; introText.lineSpacing = 1.05f;
        UIKit.Anchor(introText.rectTransform, new Vector2(0, 1), new Vector2(112, -16), new Vector2(w - 128, 80));
        // 아래: 컷 점 3개(좌) · 다음/시작(우) · 건너뛰기는 판 바깥 오른쪽 위
        var dots = UIKit.Row(panel.transform, "dots", 8, TextAnchor.MiddleLeft);
        UIKit.Anchor(dots.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(24, 22), new Vector2(80, 16));
        introDots = new Image[cuts.Length];
        for (int i = 0; i < cuts.Length; i++)
        {
            var d = UIKit.RT(dots.transform, "dot").gameObject.AddComponent<Image>(); d.sprite = Dot(); d.raycastTarget = false;
            var le = d.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = 12;
            introDots[i] = d;
        }
        float bw = 132f;
        introBtn = UIKit.Button(panel.transform, "next", UIKit.Blue, new Vector2(bw, 48), "", 22, Next, "arrow", 30);
        UIKit.Anchor(introBtn.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(-18, 12), new Vector2(bw, 48));
        // 건너뛰기·닫기: 판 바깥 오른쪽 위 둥근 X
        var skip = UIKit.RoundButton(scrim.transform, "skip", UIKit.Gray, 48, "close", 26, () => EndIntro(false), out _);
        UIKit.Anchor(skip.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(w / 2f - 24f, py + ph / 2f + 34f), new Vector2(48, 48));
        SetCut(0);
        Debug.Log("story cuts start: " + id);
        StartCoroutine(CoPop(introPanel, 0.3f));
    }

    void SetCut(int i)
    {
        introCut = i; typeT = 0; typing = true; introText.text = "";
        for (int k = 0; k < introDots.Length; k++) introDots[k].color = k == i ? Color.white : new Color(1, 1, 1, k < i ? 0.55f : 0.22f);
        bool last = i >= cuts.Length - 1;
        UIKit.Restyle(introBtn, last ? UIKit.Gold : UIKit.Blue, last ? lastLabel : "", last ? "check" : "arrow");
        AudioManager.I?.Play("tick", 1.4f, 0.5f);
        if (i > 0) StartCoroutine(CoPop(introPanel, 0.18f));
    }

    void UpdateIntro()
    {
        // 인트로 중엔 첫 정착민이 내려오지 않게 스폰 타이머를 붙잡아 둔다
        if (cutsId == "intro" && S.spawnT < GameConfig.FIRST_SPAWN) S.spawnT = GameConfig.FIRST_SPAWN;
        if (typing)
        {
            typeT += Time.unscaledDeltaTime;
            var full = cuts[introCut]; int n = Mathf.Min(full.Length, Mathf.FloorToInt(typeT * 42f));
            introText.text = full.Substring(0, n);
            if (n >= full.Length) typing = false;
        }
        if (AutoIntro) { autoT += Time.unscaledDeltaTime; if (autoT >= 1.5f) { autoT = 0; typing = false; Next(); } }
    }

    // 탭: 글자가 나오는 중이면 한 번에 다 보이고, 다 보였으면 다음 컷
    void Next()
    {
        if (!introShowing) return;
        if (typing) { typing = false; introText.text = cuts[introCut]; return; }
        if (introCut + 1 < cuts.Length) { SetCut(introCut + 1); AudioManager.I?.Play("pop", 1.4f, 0.3f); }
        else EndIntro(true);
    }

    // 컷 판 닫기. byLast: 마지막 버튼(시작!/새 정착지로)으로 닫음 → onLast 실행. 건너뛰기·닫기는 false
    void EndIntro(bool byLast)
    {
        if (!introShowing) return;
        introShowing = false;
        if (!Seen(cutsId)) S.story.Add(cutsId);
        G.Save();
        if (FloatingJoystick.I && (UpgradeSheet.I == null || !UpgradeSheet.I.IsOpen)) FloatingJoystick.I.suppressed = false;
        AudioManager.I?.Play("up", 1.2f, 0.5f);
        Debug.Log("story cuts end: " + cutsId + " at cut " + (introCut + 1) + (byLast ? " (last)" : " (skip)"));
        if (introRoot != null) StartCoroutine(CoFadeOut(introRoot, 0.25f));
        introRoot = null; introPanel = null; introText = null; introBtn = null; introDots = null;
        var act = byLast ? onLast : null; onLast = null;
        act?.Invoke();
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

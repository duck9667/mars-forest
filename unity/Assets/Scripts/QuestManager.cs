using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// §6.1 목표 QUESTS 순차 12개(qv=2): 0.3초마다 판정, 걷는 중·아이템 비행 중·발판 완료 0.6s 이내면 최대 3s 보류.
// 완료: 비차단 상자 팝 1.5s(chestT=1.6 동안 다음 목표 보류), 0.4s 후 동전 14개 HUD + 보상, 플래시 0.3, 흔들림 4. 전부 끝나면 칩에 왕관.
// 상단 칩을 누르면 업그레이드 시트의 목표 탭이 열린다(§3.3, 목표 탭은 칩으로만 진입).
public class QuestManager : MonoBehaviour
{
    public static QuestManager I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public class Quest { public string text, key; public int goal, reward; }
    public static readonly Quest[] QUESTS =
    {
        new Quest{ text="물병 1개 팔기", key="sold", goal=1, reward=30 },
        new Quest{ text="물병 5개 팔기", key="sold", goal=5, reward=50 },
        new Quest{ text="손 크기 올리기", key="hand", goal=1, reward=40 },
        new Quest{ text="삐걱 고용", key="cashier", goal=1, reward=80 },
        new Quest{ text="얼음밭 확장", key="ice2", goal=1, reward=60 },
        new Quest{ text="물병 30개", key="sold", goal=30, reward=100 },
        new Quest{ text="뽀삐 고용", key="runner", goal=1, reward=150 },
        new Quest{ text="간판 Lv.2", key="ads", goal=2, reward=150 },
        new Quest{ text="얼음 드릴", key="drill", goal=1, reward=250 },
        new Quest{ text="토끼 우리", key="pen", goal=1, reward=200 },
        new Quest{ text="휴게 돔", key="dome", goal=1, reward=300 },
        new Quest{ text="물병 300개", key="sold", goal=300, reward=500 },
    };

    float tickT, holdT, chestT;
    // HUD 칩(원본 #qchip): [목표 아이콘] 제목 · 진행(금색) · 진행 바 · [상자] +보상
    RectTransform chip; Image chipBg, qIcon; Text title, prog; Image bar; Text reward; Image chestIc; int lastQuest = -1;
    const float BAR_W = 64f;

    void Awake() { I = this; }

    void Start()
    {
        BuildChip();
        Refresh();
    }

    int Value(Quest q) => q.key == "sold" ? S.sold : G.PadLv(q.key);
    /// 시트 목표 탭 표시용(판정과 같은 값)
    public int ValueOf(Quest q) => Value(q);
    public int Index => S.quest;
    public Quest Current => S.quest < QUESTS.Length ? QUESTS[S.quest] : null;
    public bool AllDone => S.quest >= QUESTS.Length;

    void BuildChip()
    {
        var root = HUD.I.transform;
        // 2행 중앙: 남색 패널 한 줄 레이아웃, 내용 크기에 맞춤
        chipBg = UIKit.Chip(root, "quest", UIKit.Navy, "bottle", 28, "", 21, out title, out qIcon, 7);
        chip = chipBg.rectTransform; chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1); chip.pivot = new Vector2(0.5f, 1); chip.anchoredPosition = new Vector2(0, -94);
        var le = chipBg.gameObject.AddComponent<LayoutElement>(); le.minHeight = 54;
        // 칩을 누르면 시트 목표 탭(원본 #qchip pointerup → openSheet('quest')). 누름 축소 손맛, 조이스틱은 UI 위 터치를 무시한다
        chipBg.raycastTarget = true;
        var btn = chipBg.gameObject.AddComponent<Button>(); btn.targetGraphic = chipBg; btn.transition = Selectable.Transition.None;
        chipBg.gameObject.AddComponent<UIPress>();
        btn.onClick.AddListener(() => UpgradeSheet.I?.Open(UpgradeSheet.QUEST));
        prog = UIKit.Text(chipBg.transform, "", 22, UIKit.GOLD);
        UIKit.ProgressBar(chipBg.transform, "track", new Vector2(BAR_W, 10), out bar);
        chestIc = UIKit.IconImg(chipBg.transform, "chest", 26);
        reward = UIKit.Text(chipBg.transform, "", 21, UIKit.GOLD);
    }

    public void Refresh()
    {
        var q = Current;
        if (q == null)
        {
            if (lastQuest != QUESTS.Length) { lastQuest = QUESTS.Length; chipBg.sprite = UIKit.Panel9(UIKit.SignOk); UIKit.SetIcon(qIcon, "crown"); title.color = prog.color = UIKit.SignOk.text; }
            title.text = "모든 목표 달성"; prog.text = ""; UIKit.SetBar(bar, 1, BAR_W); reward.text = ""; chestIc.gameObject.SetActive(false); return;
        }
        if (lastQuest != S.quest) { lastQuest = S.quest; UIKit.SetIcon(qIcon, UIKit.PadIcon(q.key)); }
        int v = Mathf.Min(Value(q), q.goal);
        title.text = q.text;
        prog.text = v + "/" + q.goal;
        UIKit.SetBar(bar, (float)v / q.goal, BAR_W);
        reward.text = "+" + q.reward;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (chestT > 0) { chestT -= dt; return; }
        tickT += dt; if (tickT < 0.3f) return; tickT = 0;
        // 이번 Phase에 없는 발판(pen·dome 등)을 요구하는 목표는 보상 없이 건너뛴다 (Phase 3에서 활성화되면 그대로 살아남)
        while (Current != null && Current.key != "sold" && !GameState.ACTIVE_IDS.Contains(Current.key)) S.quest++;
        // qskip: 옛 저장(Phase 2)에서 이미 보상받은 목표는 되살린 목표를 지난 뒤 보상 없이 넘긴다(원본 qskip과 같은 뜻)
        while (Current != null && S.qskip != null && S.qskip.Contains(S.quest) && Value(Current) >= Current.goal) { S.qskip.Remove(S.quest); S.quest++; }
        Refresh();
        var q = Current; if (q == null) return;
        if (Value(q) < q.goal) { holdT = 0; return; }
        // 보류 조건: 걷는 중·아이템 비행 중·발판 완료 0.6s 이내 (최대 3s)
        bool busy = (Player.I != null && Player.I.walking) || (ArcFlyer.I != null && ArcFlyer.I.Active > 0) || (PadManager.I != null && Time.time - PadManager.I.lastCompleteT < 0.6f);
        if (busy && holdT < 3f) { holdT += 0.3f; return; }
        holdT = 0;
        StartCoroutine(Complete(q));
    }

    IEnumerator Complete(Quest q)
    {
        chestT = 1.6f;
        S.quest++;
        G.AddBells(q.reward); G.Save();   // 원본 completeQuest: 진행·보상·저장을 한 번에(연출 중 종료돼도 보상 유지)
        // 비차단 상자 팝 (UI 중앙, 원본 #chestPop): 남색 패널에 "목표 달성! · 목표명" + [상자][벨] +보상(큰 금색 숫자)
        var box = UIKit.Panel(HUD.I.transform, "chest", UIKit.Navy);
        var brt = box.rectTransform; UIKit.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(340, 136));
        var t1 = UIKit.Text(box.transform, "목표 달성! · " + q.text, 24, Color.white); UIKit.Stroke(t1, UIKit.EDGE, 1.6f);
        UIKit.Anchor(t1.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(320, 36));
        var row = UIKit.Row(box.transform, "row", 8); var rrt = row.GetComponent<RectTransform>(); UIKit.Anchor(rrt, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(320, 64));
        UIKit.IconImg(row.transform, "chest", 48); UIKit.IconImg(row.transform, "bell", 36);
        var t2 = UIKit.Text(row.transform, "+" + q.reward, 44, UIKit.Hex("ffe58a")); UIKit.Stroke(t2, UIKit.Hex("7a3a00"), 3f); UIKit.DropShadow(t2, 3f, 0.35f);
        AudioManager.I.Play("up", 1.1f); JuiceFX.I.Vibrate(30);   // 팝 시작: 소리·진동만
        float el = 0; bool opened = false;
        while (el < 0.5f)
        {
            el += Time.deltaTime; brt.localScale = Vector3.one * Mathf.Max(0.001f, JuiceFX.EaseOutBack(Mathf.Clamp01(el / 0.5f)));
            // §6.1: 0.4s 후 뚜껑 열림 + 동전 14개 HUD + 플래시 0.3 + 흔들림 4
            if (!opened && el >= 0.4f) { opened = true; JuiceFX.I.Flash(0.3f); JuiceFX.I.Shake(4); HUD.I.Coins(brt.position, () => HUD.I.BellsScreen(), 14); }
            yield return null;
        }
        brt.localScale = Vector3.one;
        yield return new WaitForSeconds(1.0f);
        el = 0;
        while (el < 0.25f) { el += Time.deltaTime; brt.localScale = Vector3.one * (1 - el / 0.25f); yield return null; }
        Destroy(box.gameObject);
        Refresh();
        if (AllDone) { AudioManager.I.Play("ding", 1.2f); }
    }
}

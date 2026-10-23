using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// §6.1 목표 QUESTS 순차 12개(qv=2): 0.3초마다 판정, 걷는 중·아이템 비행 중·발판 완료 0.6s 이내면 최대 3s 보류.
// 완료: 비차단 상자 팝 1.5s(chestT=1.6 동안 다음 목표 보류), 0.4s 후 동전 14개 HUD + 보상, 플래시 0.3, 흔들림 4. 전부 끝나면 칩에 왕관.
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
    // HUD 칩
    RectTransform chip; Text title, prog; Image bar; Text reward;

    void Awake() { I = this; }

    void Start()
    {
        BuildChip();
        Refresh();
    }

    int Value(Quest q) => q.key == "sold" ? S.sold : G.PadLv(q.key);
    public Quest Current => S.quest < QUESTS.Length ? QUESTS[S.quest] : null;
    public bool AllDone => S.quest >= QUESTS.Length;

    void BuildChip()
    {
        var root = HUD.I.transform;
        var panel = HUD.UI.Panel(root, "quest", new Color(0.13f, 0.12f, 0.22f, 0.9f));
        chip = panel.rectTransform; HUD.UI.Anchor(chip, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(360, 64));
        title = HUD.UI.Label(panel.transform, "", 22, Color.white, TextAnchor.UpperLeft);
        HUD.UI.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(14, -6), new Vector2(260, 28));
        prog = HUD.UI.Label(panel.transform, "", 20, GameConfig.GOLD, TextAnchor.UpperRight);
        HUD.UI.Anchor(prog.rectTransform, new Vector2(1, 1), new Vector2(-14, -6), new Vector2(90, 28));
        var track = HUD.UI.Panel(panel.transform, "track", new Color(1, 1, 1, 0.15f));
        HUD.UI.Anchor(track.rectTransform, new Vector2(0, 0), new Vector2(14, 10), new Vector2(240, 10));
        bar = HUD.UI.Panel(track.transform, "bar", GameConfig.GOLD);
        var brt = bar.rectTransform; brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(0, 1); brt.pivot = new Vector2(0, 0.5f); brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(0, 0);
        reward = HUD.UI.Label(panel.transform, "", 20, GameConfig.GOLD, TextAnchor.LowerRight);
        HUD.UI.Anchor(reward.rectTransform, new Vector2(1, 0), new Vector2(-14, 6), new Vector2(90, 24));
        panel.raycastTarget = false;
    }

    public void Refresh()
    {
        var q = Current;
        if (q == null) { title.text = "모든 목표 달성"; prog.text = "👑"; bar.rectTransform.sizeDelta = new Vector2(240, 0); reward.text = ""; return; }
        int v = Mathf.Min(Value(q), q.goal);
        title.text = "목표 " + (S.quest + 1) + "/" + QUESTS.Length + " · " + q.text;
        prog.text = v + "/" + q.goal;
        bar.rectTransform.sizeDelta = new Vector2(240f * v / q.goal, 0);
        reward.text = "상자 +" + q.reward;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (chestT > 0) { chestT -= dt; return; }
        tickT += dt; if (tickT < 0.3f) return; tickT = 0;
        // 이번 Phase에 없는 발판(pen·dome 등)을 요구하는 목표는 보상 없이 건너뛴다 (Phase 3에서 활성화되면 그대로 살아남)
        while (Current != null && Current.key != "sold" && !GameState.ACTIVE_IDS.Contains(Current.key)) S.quest++;
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
        // 비차단 상자 팝 (UI 중앙)
        var box = HUD.UI.Panel(HUD.I.transform, "chest", GameConfig.Hex("fff6e6"));
        var brt = box.rectTransform; HUD.UI.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(300, 120));
        box.raycastTarget = false;
        var ol = box.gameObject.AddComponent<Outline>(); ol.effectColor = GameConfig.GOLD; ol.effectDistance = new Vector2(4, -4);
        var t1 = HUD.UI.Label(box.transform, "목표 달성!", 30, GameConfig.NAVY, TextAnchor.UpperCenter); HUD.UI.Anchor(t1.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(280, 40));
        var t2 = HUD.UI.Label(box.transform, q.text + "  +" + q.reward + " 벨", 24, GameConfig.Hex("7a7f99"), TextAnchor.LowerCenter); HUD.UI.Anchor(t2.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(280, 36));
        AudioManager.I.Play("up", 1.1f); JuiceFX.I.Flash(0.3f); JuiceFX.I.Shake(4); JuiceFX.I.Vibrate(30);
        float el = 0;
        while (el < 0.5f) { el += Time.deltaTime; brt.localScale = Vector3.one * Mathf.Max(0.001f, JuiceFX.EaseOutBack(Mathf.Clamp01(el / 0.5f))); yield return null; }
        brt.localScale = Vector3.one;
        // 0.4s 후 동전 14개 HUD로 + 보상
        int per = Mathf.CeilToInt(q.reward / 14f), given = 0;
        HUD.I.Coins(brt.position, () => HUD.I.BellsScreen(), 14, () => { int add = Mathf.Min(per, q.reward - given); given += add; G.AddBells(add); });
        yield return new WaitForSeconds(1.0f);
        el = 0;
        while (el < 0.25f) { el += Time.deltaTime; brt.localScale = Vector3.one * (1 - el / 0.25f); yield return null; }
        Destroy(box.gameObject);
        if (given < q.reward) G.AddBells(q.reward - given);
        Refresh();
        if (AllDone) { AudioManager.I.Play("ding", 1.2f); }
    }
}

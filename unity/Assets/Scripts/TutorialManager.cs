using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// §6.2 튜토리얼 S.tut 0→3: 머리 위 말풍선 3단계 + 시작 시 손 포인터 "드래그" + 튜토리얼 후 첫 발판 위 손 포인터 탭
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager I;
    GameState G => GameState.I;
    SaveData S => G.S;
    WorldLabels.Label bubble; Image hand; Text handText; bool moved; float handT, bobT;
    WorldLabels.Label padHand; float padHandT = -1;

    void Awake() { I = this; }

    void Start()
    {
        // 머리 위 말풍선(원본 .bub.tip: 크림 그라데이션·갈색 테두리·꼬리, 위아래 둥실)
        bubble = WorldLabels.I.Add(() => Player.I.view.position + Vector3.up * 4.4f, "", 26, GameConfig.NAVY, new Vector2(0, 8), true);
        bubble.SetStyle(UIKit.Tip);
        // 드래그 손 포인터(원본 #hand SVG: 흰 손·남색 외곽선·금색 손끝)
        hand = UIKit.IconImg(HUD.I.transform, "hand", 72); Destroy(hand.GetComponent<LayoutElement>());
        UIKit.Anchor(hand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(72, 72));
        handText = UIKit.Text(HUD.I.transform, "드래그해서 이동", 28, Color.white); UIKit.Stroke(handText, UIKit.EDGE, 2.4f); UIKit.DropShadow(handText);
        UIKit.Anchor(handText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -296), new Vector2(400, 40));
        bool show = S.tut < 3;
        hand.gameObject.SetActive(show); handText.gameObject.SetActive(show);
        if (S.tut >= 3 && S.handShown == 0) padHandT = 0;   // 저장된 게임을 이어서 할 때도 발판 손 포인터
    }

    public void Advance(int step)
    {
        if (S.tut >= step) return;
        S.tut = step;
        AudioManager.I.Play("up", 1f, 0.5f);
        if (step >= 3) { JuiceFX.I.Ring(Player.I.view.position, GameConfig.GOLD, 40); padHandT = 0; G.Save(); }
    }

    void Update()
    {
        var pl = Player.I; if (pl == null) return;
        // 새 게임 인트로(StoryManager 무전) 중엔 말풍선·손 포인터를 숨기고 단계도 멈춘다. 인트로가 끝나면 튜토리얼 시작
        bool held = StoryManager.I != null && StoryManager.I.IntroActive;
        hand.enabled = handText.enabled = !held;
        if (held) { bubble.Show(false); return; }
        if (S.tut < 3) { bubble.Set(GameConfig.TUT[S.tut]); bobT += Time.deltaTime; bubble.offset = new Vector2(0, 8 + Mathf.Sin(bobT * 4.8f) * 5f * WorldLabels.Scale); }
        bubble.Show(S.tut < 3);
        // 단계 조건 (3은 Player에서 선반 올릴 때)
        if (S.tut == 0)
        {
            bool anyOpen = false; for (int i = 0; i < Stations.I.SpotCount; i++) if (Stations.I.SpotOpen(i)) anyOpen = true;
            if (G.Carry >= G.CarryMax || (!anyOpen && G.Carry > 0 && G.pend == 0)) Advance(1);
        }
        else if (S.tut == 1)
        {
            if (G.Carry == 0 && G.pend == 0 && S.meltQ > 0) Advance(2);
        }
        // 드래그 손: 첫 이동 시 사라짐. 좌우로 끌리며 누를 때 작아짐(원본 handDrag)
        if (hand.gameObject.activeSelf)
        {
            if (pl.walking) moved = true;
            if (moved || S.tut >= 3) { hand.gameObject.SetActive(false); handText.gameObject.SetActive(false); }
            else
            {
                handT += Time.deltaTime; float ph = Mathf.Repeat(handT / 1.4f, 1f);
                float x = ph < 0.24f ? 0 : ph < 0.7f ? Mathf.Lerp(0, 70, (ph - 0.24f) / 0.46f) : 70;
                float sc = ph > 0.2f && ph < 0.78f ? 0.84f : 1f;
                hand.rectTransform.anchoredPosition = new Vector2(x - 35, -220); hand.rectTransform.localScale = Vector3.one * sc;
            }
        }
        // 튜토리얼 직후 가장 가까운 발판에 손 포인터 탭 3.6s
        if (padHandT >= 0 && S.handShown == 0)
        {
            var v = PadManager.I.NearestVisible(pl.pos);
            if (v != null)
            {
                if (padHand == null) { padHand = WorldLabels.I.Add(() => v.root.position, "여기서 멈추면 납입", 24, Color.white, new Vector2(0, 110), true, new Color(0.13f, 0.12f, 0.22f, 0.9f)); padHand.SetIcon("hand"); }
                padHandT += Time.deltaTime; padHand.offset = new Vector2(0, 110 + Mathf.Abs(Mathf.Sin(padHandT * 5f)) * 14f);
                if (padHandT > 3.6f) { S.handShown = 1; WorldLabels.I.Remove(padHand); padHand = null; padHandT = -1; }
            }
        }
    }
}

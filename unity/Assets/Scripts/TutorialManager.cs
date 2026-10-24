using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// §6.2 튜토리얼 S.tut 0→3: 머리 위 말풍선 3단계 + 시작 시 손 포인터 "드래그" + 튜토리얼 후 첫 발판 위 손 포인터 탭
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager I;
    GameState G => GameState.I;
    SaveData S => G.S;
    WorldLabels.Label bubble; Image hand; Text handText; bool moved; float handT;
    WorldLabels.Label padHand; float padHandT = -1;

    void Awake() { I = this; }

    void Start()
    {
        bubble = WorldLabels.I.Add(() => Player.I.view.position + Vector3.up * 4.4f, "", 26, GameConfig.NAVY, new Vector2(0, 36), true);
        // 드래그 손 포인터 (UI)
        hand = HUD.UI.Panel(HUD.I.transform, "hand", new Color(1, 1, 1, 0.9f)); hand.sprite = FloatingJoystick.CircleSprite(); hand.raycastTarget = false;
        HUD.UI.Anchor(hand.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(56, 56));
        handText = HUD.UI.Label(HUD.I.transform, "드래그해서 이동", 26, Color.white); handText.raycastTarget = false;
        HUD.UI.Anchor(handText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -290), new Vector2(400, 40));
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
        if (S.tut < 3) bubble.Set(GameConfig.TUT[S.tut]);
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
        // 드래그 손: 첫 이동 시 사라짐
        if (hand.gameObject.activeSelf)
        {
            if (pl.walking) moved = true;
            if (moved || S.tut >= 3) { hand.gameObject.SetActive(false); handText.gameObject.SetActive(false); }
            else { handT += Time.deltaTime; hand.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(handT * 2.5f) * 90f, -220); }
        }
        // 튜토리얼 직후 가장 가까운 발판에 손 포인터 탭 3.6s
        if (padHandT >= 0 && S.handShown == 0)
        {
            var v = PadManager.I.NearestVisible(pl.pos);
            if (v != null)
            {
                if (padHand == null) padHand = WorldLabels.I.Add(() => v.root.position, "여기서 멈추면 납입", 24, Color.white, new Vector2(0, 110), true, new Color(0.13f, 0.12f, 0.22f, 0.9f));
                padHandT += Time.deltaTime; padHand.offset = new Vector2(0, 110 + Mathf.Abs(Mathf.Sin(padHandT * 5f)) * 14f);
                if (padHandT > 3.6f) { S.handShown = 1; WorldLabels.I.Remove(padHand); padHand = null; padHandT = -1; }
            }
        }
    }
}

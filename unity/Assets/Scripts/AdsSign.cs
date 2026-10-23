using UnityEngine;

// 간판(ads) 시각물: 발판 자리(50,598) 옆 입간판. Lv마다 판이 커지고 Lv2+엔 줄 상한 +2(§5.2). 효과 수치는 BaseGap·want·QueueCap에 반영됨.
public class AdsSign : MonoBehaviour
{
    GameState G => GameState.I;
    Transform root, board; WorldLabels.Label label; float t;

    void Start()
    {
        G.OnPadLevel += (id, lv) => { if (id == "ads") Refresh(true); };
        Refresh(false);
    }

    void Refresh(bool fx)
    {
        int lv = G.PadLv("ads");
        if (lv <= 0) { if (root) root.gameObject.SetActive(false); return; }
        float k = GameConfig.K; var pos = new Vector2(Pads.Get("ads").pos.x, Pads.Get("ads").pos.y - 40);
        if (root == null)
        {
            root = new GameObject("adsSign").transform; root.SetParent(transform, false); root.position = WorldMap.W2U(pos);
            Prim.Box(root, "post", new Vector3(0, 20 * k, 0), new Vector3(4, 40, 4), GameConfig.Hex("8a5a3c"));
            board = Prim.Box(root, "board", new Vector3(0, 44 * k, 0), new Vector3(34, 22, 3), GameConfig.Hex("fff4dc")).transform;
            Prim.Box(board, "rim", Vector3.zero, new Vector3(36, 24, 2), GameConfig.NAVY);
            label = WorldLabels.I.Add(() => board.position, "", 18, GameConfig.NAVY, Vector2.zero);
            root.rotation = Quaternion.LookRotation(new Vector3(0, 0, -1), Vector3.up); // 입구(남쪽) 향함
        }
        root.gameObject.SetActive(true);
        float s = 1 + 0.18f * (lv - 1);
        board.localScale = new Vector3(34 * s, 22 * s, 3) * k;
        board.localPosition = new Vector3(0, (44 + 11 * (s - 1)) * k, 0);
        label.Set("물병 가게 Lv." + lv);
        if (fx) { JuiceFX.I.Pop(root, 0.001f, 0.6f); JuiceFX.I.Ring(root.position, GameConfig.GOLD, 30); }
    }

    void Update()
    {
        if (root == null || !root.gameObject.activeSelf) return;
        t += Time.deltaTime;
        board.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 1.2f) * 6f, 0);
    }
}

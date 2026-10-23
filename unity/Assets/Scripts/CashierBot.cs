using UnityEngine;

// §5.1 삐걱(cashier): 계산대 옆 고정, 존재만으로 자동 결제. 회색 몸체 0x9aa7b2.
public class CashierBot : MonoBehaviour
{
    GameState G => GameState.I;
    Transform view, head; float t;

    void Start()
    {
        G.OnPadLevel += (id, lv) => { if (id == "cashier") Refresh(); };
        Refresh();
    }

    void Refresh()
    {
        bool has = G.HasCashier;
        if (has && view == null)
        {
            float k = GameConfig.K; var grey = GameConfig.Hex("9aa7b2");
            view = new GameObject("cashier").transform; view.SetParent(transform, false);
            view.position = WorldMap.W2U(WorldMap.CashierSpot);
            view.rotation = Quaternion.LookRotation(new Vector3(0, 0, -1), Vector3.up); // 손님(남쪽) 보기
            Prim.BlobShadow(view, 24);
            Prim.Box(view, "body", new Vector3(0, 14 * k, 0), new Vector3(16, 22, 12), grey);
            head = Prim.Box(view, "head", new Vector3(0, 32 * k, 0), new Vector3(14, 12, 12), grey).transform;
            Prim.Box(head, "eyeL", new Vector3(-3.5f * k, 1 * k, 6 * k), new Vector3(3, 3, 1), GameConfig.Hex("5fc8ff"));
            Prim.Box(head, "eyeR", new Vector3(3.5f * k, 1 * k, 6 * k), new Vector3(3, 3, 1), GameConfig.Hex("5fc8ff"));
            Prim.Cyl(head, "ant", new Vector3(0, 9 * k, 0), 1.5f, 8, GameConfig.Hex("3b3a52"));
            JuiceFX.I.Pop(view, 0.001f, 0.6f);
            JuiceFX.I.Ring(view.position, GameConfig.GOLD, 30);
        }
        else if (!has && view != null) { Destroy(view.gameObject); view = null; }
    }

    void Update()
    {
        if (view == null) return;
        t += Time.deltaTime;
        head.localPosition = new Vector3(0, (32 + Mathf.Sin(t * 3f) * 1.2f) * GameConfig.K, 0);
        head.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 1.3f) * 12f, 0);
    }
}

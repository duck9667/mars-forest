using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §5.2 손님: 입장 → 줄 → (맨 앞) 결제 → 병 머리 위로 → 점프 → 퇴장. Phase 1: VIP·러시·좌석 제외.
public class Customer
{
    public enum St { WalkIn, Queue, Leave, Gone }
    public St st = St.WalkIn;
    public Vector2 pos; public int want; public float wait, pay; public bool angry, bought;
    public Transform view, body, head, hold; public WorldLabels.Label bubble;
    public float jumpT = -1;
}

public class CustomerSystem : MonoBehaviour
{
    public static CustomerSystem I;
    GameState G => GameState.I;
    SaveData S => G.S;
    public readonly List<Customer> list = new List<Customer>();

    void Awake() { I = this; }

    int Waiting() { int n = 0; foreach (var c in list) if (c.st == Customer.St.WalkIn || c.st == Customer.St.Queue) n++; return n; }
    int QueueIndex(Customer c) { int n = 0; foreach (var o in list) { if (o == c) return n; if (o.st == Customer.St.WalkIn || o.st == Customer.St.Queue) n++; } return n; }
    public Customer Front() { foreach (var o in list) if (o.st == Customer.St.Queue) return QueueIndex(o) == 0 ? o : null; return null; }
    public bool AnyWaitingToPay() { var f = Front(); return f != null && S.shelf >= f.want; }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 스폰 (§5.2)
        S.spawnT -= dt;
        if (S.spawnT <= 0)
        {
            if (Waiting() < GameConfig.QUEUE_MAX) Spawn();
            S.spawnT = S.tut < 3 ? GameConfig.TUT_GAP : G.BaseGap * Random.Range(0.7f, 1.3f);
        }
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var c = list[i];
            Tick(c, dt);
            if (c.st == Customer.St.Gone) { Remove(c); list.RemoveAt(i); }
        }
    }

    void Spawn()
    {
        S.custN++;
        var c = new Customer { pos = WorldMap.ENTRY };
        c.want = S.tut < 3 ? 1 : 1 + (Random.value < 0.3f ? 1 : 0) + (Random.value < 0.1f ? 1 : 0);
        BuildView(c);
        list.Add(c);
    }

    void BuildView(Customer c)
    {
        float k = GameConfig.K;
        c.view = new GameObject("cust").transform; c.view.SetParent(transform, false); c.view.position = WorldMap.W2U(c.pos);
        Prim.BlobShadow(c.view, 22);
        c.body = new GameObject("body").transform; c.body.SetParent(c.view, false);
        var col = GameConfig.CUST_COLS[Random.Range(0, GameConfig.CUST_COLS.Length)];
        Prim.Capsule(c.body, "torso", new Vector3(0, 16 * k, 0), 14, 24, col);
        c.head = Prim.Sphere(c.body, "head", new Vector3(0, 33 * k, 0), 14, GameConfig.Hex("ffe0c2")).transform;
        int acc = Random.Range(0, 6);
        if (acc == 0) Prim.Box(c.head, "hat", new Vector3(0, 7 * k, 0), new Vector3(14, 4, 14), GameConfig.Hex("3b3a52"));
        else if (acc == 1) Prim.Box(c.body, "bag", new Vector3(0, 18 * k, -8 * k), new Vector3(10, 12, 5), GameConfig.Hex("8a5a3c"));
        else if (acc == 2) Prim.Cyl(c.head, "ant", new Vector3(0, 10 * k, 0), 1.5f, 10, GameConfig.Hex("3b3a52"));
        else if (acc == 3) Prim.Box(c.body, "scarf", new Vector3(0, 26 * k, 0), new Vector3(16, 4, 16), GameConfig.Hex("f05a5a"));
        c.hold = new GameObject("hold").transform; c.hold.SetParent(c.body, false); c.hold.localPosition = new Vector3(0, 56 * k, 0);
        c.bubble = WorldLabels.I.Add(() => c.head.position + Vector3.up * 1.0f, "병 ×" + c.want, 20, GameConfig.NAVY, Vector2.zero, true);
        c.bubble.Show(false);
    }

    void Remove(Customer c)
    {
        if (c.bubble != null) WorldLabels.I.Remove(c.bubble);
        if (c.view != null) Destroy(c.view.gameObject);
    }

    static bool MoveTo(Customer c, Vector2 target, float speed, float dt)
    {
        Vector2 d = target - c.pos; float dist = d.magnitude;
        if (dist < 1.5f) { c.pos = target; return true; }
        float step = Mathf.Min(dist, speed * dt);
        c.pos += d / dist * step;
        c.view.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, -d.y), Vector3.up);
        return false;
    }

    void Tick(Customer c, float dt)
    {
        float k = GameConfig.K;
        switch (c.st)
        {
            case Customer.St.WalkIn:
                {
                    var target = WorldMap.QueuePos(QueueIndex(c));
                    if (MoveTo(c, target, GameConfig.CUST_WALK, dt)) { c.st = Customer.St.Queue; c.bubble.Show(true); }
                    Bob(c, true);
                    break;
                }
            case Customer.St.Queue:
                {
                    int qi = QueueIndex(c);
                    var target = WorldMap.QueuePos(qi);
                    bool arrived = MoveTo(c, target, GameConfig.CUST_WALK, dt);
                    Bob(c, !arrived);
                    if (arrived) c.view.rotation = Quaternion.LookRotation(new Vector3(0, 0, 1), Vector3.up); // 카운터(북쪽) 보기
                    if (qi == 0)
                    {
                        // 인내 (§5.2): 튜토리얼 후에만
                        if (S.tut >= 3 && !c.bought)
                        {
                            c.wait += dt;
                            if (c.wait > GameConfig.CUST_WARN && !c.angry) { c.angry = true; c.bubble.Set("병 ×" + c.want + " !"); c.bubble.text.color = GameConfig.Hex("e03c3c"); }
                            if (c.wait > GameConfig.CUST_PATIENCE) { S.lost++; c.bubble.Show(false); c.st = Customer.St.Leave; AudioManager.I.Play("no"); break; }
                        }
                        bool canPay = arrived && S.shelf >= c.want && (G.HasCashier || WorldMap.NearStn(Player.I.pos, "register", GameConfig.R_PAY));
                        if (canPay)
                        {
                            c.pay += dt;
                            c.bubble.bg.color = Color.Lerp(new Color(1, 0.98f, 0.92f, 0.95f), GameConfig.GOLD, c.pay / GameConfig.PAY_TIME);
                            if (c.pay >= GameConfig.PAY_TIME) Pay(c);
                        }
                        else c.pay = 0;
                    }
                    if (c.bought)
                    {
                        // 점프 0.5s(높이 22) 후 퇴장
                        c.jumpT += dt; float j = Mathf.Clamp01(c.jumpT / 0.5f);
                        c.body.localPosition = new Vector3(0, Mathf.Sin(Mathf.PI * j) * 22 * k, 0);
                        if (j >= 1) { c.body.localPosition = Vector3.zero; c.st = Customer.St.Leave; }
                    }
                    break;
                }
            case Customer.St.Leave:
                if (MoveTo(c, WorldMap.ENTRY, GameConfig.CUST_LEAVE, dt)) c.st = Customer.St.Gone;
                Bob(c, true);
                break;
        }
        c.view.position = WorldMap.W2U(c.pos);
    }

    static void Bob(Customer c, bool walking)
    {
        if (c.bought) return;
        float t = Time.time * 1000f;
        c.body.localPosition = new Vector3(0, walking ? Mathf.Abs(Mathf.Sin(t / 90f)) * 2.5f * GameConfig.K : 0, 0);
    }

    // §1.3 결제: 선반에서 want만큼 차감, 수익은 계산대 더미(S.cash)로, 병은 머리 위로 날아가 쌓임
    void Pay(Customer c)
    {
        int want = c.want;
        S.shelf -= want;
        int earn = G.BottlePrice * want;
        S.cash += earn; S.sold += want; S.custs++;
        c.bought = true; c.jumpT = 0; c.pay = 0;
        c.bubble.Show(false);
        for (int i = 0; i < want; i++)
        {
            int idx = i;
            ArcFlyer.I.FlyItem("bottle", Stations.I.ShelfTop(), () => c.hold.position + Vector3.up * idx * Items.ITEM_H["bottle"] * GameConfig.K, () =>
            {
                if (c.hold != null) { var s = Items.Slab(c.hold, "bottle"); s.transform.localPosition = new Vector3(0, idx * Items.ITEM_H["bottle"] * GameConfig.K, 0); }
            }, 0.3f, 30f);
        }
        AudioManager.I.Play("sell");
        WorldLabels.I.Float(c.head.position + Vector3.up * 1.5f, "+" + earn, true, want >= 3);
        JuiceFX.I.Squash(Stations.I.registerView, 0.4f);
        if (want >= 3) JuiceFX.I.Shake(2);
        G.NotifySold();
    }
}

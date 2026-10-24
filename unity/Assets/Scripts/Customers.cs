using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §5.2 손님: 입장 → 줄 → (맨 앞) 결제 → 병 머리 위로 → 점프 → 퇴장. Phase 1: VIP·러시·좌석 제외.
public class Customer
{
    public enum St { WalkIn, Queue, Leave, Gone }
    public St st = St.WalkIn;
    public Vector2 pos; public int want; public float wait, pay; public bool angry, bought, vip;
    public Transform view, body, head, hold; public WorldLabels.Label bubble;
    public float jumpT = -1;
}

public class CustomerSystem : MonoBehaviour
{
    public static CustomerSystem I;
    GameState G => GameState.I;
    SaveData S => G.S;
    public readonly List<Customer> list = new List<Customer>();
    // §1.3 콤보 · §5.3 러시
    public int combo; float lastSaleT = -10f;
    public float rush;            // 남은 러시 초(0이면 평상시)
    public const float RUSH_EVERY = 240f, RUSH_LEN = 20f;

    void Awake() { I = this; }

    public int QueueCap => rush > 0 ? Mathf.FloorToInt((1000f - WorldMap.QUEUE_Y0) / WorldMap.QUEUE_GAP) + 1 : GameConfig.QUEUE_MAX + (G.PadLv("ads") >= 2 ? 2 : 0);
    float SpawnGap => G.BaseGap / (rush > 0 ? 3f : 1f);

    int Waiting() { int n = 0; foreach (var c in list) if (c.st == Customer.St.WalkIn || c.st == Customer.St.Queue) n++; return n; }
    int QueueIndex(Customer c) { int n = 0; foreach (var o in list) { if (o == c) return n; if (o.st == Customer.St.WalkIn || o.st == Customer.St.Queue) n++; } return n; }
    public Customer Front() { foreach (var o in list) if (o.st == Customer.St.Queue) return QueueIndex(o) == 0 ? o : null; return null; }
    public bool AnyWaitingToPay() { var f = Front(); return f != null && S.shelf >= f.want; }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        // 러시 (§5.3): 삐걱 고용 후 240s 누적마다 20s. 간격 ÷3, 줄 상한 10
        if (rush > 0) { rush -= dt; if (rush <= 0) { rush = 0; HUD.I?.SetRush(0); } else HUD.I?.SetRush(rush); }
        else if (G.HasCashier && S.tut >= 3)
        {
            S.rushT += dt;
            if (S.rushT >= RUSH_EVERY) { S.rushT = 0; rush = RUSH_LEN; HUD.I?.SetRush(rush); JuiceFX.I.Shake(4); AudioManager.I.Play("no", 1.6f, 0.8f); AudioManager.I.Play("ding", 0.6f, 0.8f); }
        }
        // 콤보는 직전 판매 후 2초 지나면 끊김
        if (combo > 0 && Time.time - lastSaleT > 2f) { combo = 0; HUD.I?.SetCombo(0); }
        // 스폰 (§5.2)
        S.spawnT -= dt;
        if (S.spawnT <= 0 && Waiting() < QueueCap)   // 실제로 스폰할 때만 타이머 재설정(줄이 꽉 차면 자리 나는 즉시)
        {
            Spawn();
            S.spawnT = S.tut < 3 ? GameConfig.TUT_GAP : SpawnGap * Random.Range(0.7f, 1.3f);
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
        int ads = G.PadLv("ads");
        c.vip = S.tut >= 3 && S.sold >= 40 && S.custN % 10 == 0;   // 10번째 손님마다 VIP
        c.want = S.tut < 3 ? 1 : c.vip ? Random.Range(2, 4) : 1 + (Random.value < 0.3f + 0.06f * ads ? 1 : 0) + (Random.value < 0.1f + 0.05f * ads ? 1 : 0);
        BuildView(c);
        list.Add(c);
    }

    void BuildView(Customer c)
    {
        float k = GameConfig.K;
        c.view = new GameObject("cust").transform; c.view.SetParent(transform, false); c.view.position = WorldMap.W2U(c.pos);
        Prim.BlobShadow(c.view, 22);
        c.body = new GameObject("body").transform; c.body.SetParent(c.view, false);
        // 여행자 치비(원본 bTraveler): 7색 × 액세서리 0~5, VIP 는 금색 몸+왕관. c.head 는 머리 중심 그룹(말풍선·병 기준)
        var col = GameConfig.CUST_COLS[Random.Range(0, GameConfig.CUST_COLS.Length)];
        int acc = Random.Range(0, 6);
        CharacterKit.BuildCustomer(c.body, col, acc, c.vip, out c.head);
        c.hold = new GameObject("hold").transform; c.hold.SetParent(c.body, false); c.hold.localPosition = new Vector3(0, (c.vip ? 66 : 56) * k, 0);
        c.bubble = WorldLabels.I.Add(() => c.head.position + Vector3.up * (c.vip ? 1.3f : 1.0f), (c.vip ? "VIP 병 ×" : "병 ×") + c.want, 20, GameConfig.NAVY, Vector2.zero, true, c.vip ? GameConfig.GOLD : null);
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
                    if (qi == 0 && !c.bought)
                    {
                        bool canPay = arrived && S.shelf >= c.want && (G.HasCashier || WorldMap.NearStn(Player.I.pos, "register", GameConfig.R_PAY));
                        if (canPay)
                        {
                            c.pay += dt;
                            c.bubble.bg.color = Color.Lerp(new Color(1, 0.98f, 0.92f, 0.95f), GameConfig.GOLD, c.pay / GameConfig.PAY_TIME);
                            if (c.pay >= GameConfig.PAY_TIME) Pay(c);
                        }
                        else
                        {
                            c.pay = 0;
                            // 인내 (§5.2): 결제가 진행되지 않을 때만, 튜토리얼 후에만
                            if (S.tut >= 3)
                            {
                                c.wait += dt;
                                if (c.wait > GameConfig.CUST_WARN && !c.angry) { c.angry = true; c.bubble.Set("병 ×" + c.want + " !"); c.bubble.text.color = GameConfig.Hex("e03c3c"); }
                                if (c.wait > GameConfig.CUST_PATIENCE) { S.lost++; c.bubble.Show(false); c.st = Customer.St.Leave; AudioManager.I.Play("no"); break; }
                            }
                        }
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

    // 걷기 bob + 팔다리 스윙(위상은 개체마다 다름), 정지 시 호흡. 결제 점프 중(Queue)엔 Tick 이 몸 높이를 잡으므로 건드리지 않는다
    static void Bob(Customer c, bool walking)
    {
        if (c.bought && c.st == Customer.St.Queue) return;
        var rig = c.body.GetComponent<CharRig>(); float ph = rig ? rig.ph : 0f;
        float t = Time.time * 1000f, dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        float s = walking ? Mathf.Sin(t / 90f + ph) : 0f;
        c.body.localPosition = new Vector3(0, (walking ? Mathf.Abs(s) * 2.5f : Mathf.Sin(t / 600f + ph) * 0.8f) * GameConfig.K, 0);
        CharacterKit.Swing(rig, s, 40f, dt);
    }

    // §1.3 결제: 선반에서 want만큼 차감, 수익은 계산대 더미(S.cash)로, 병은 머리 위로 날아가 쌓임
    void Pay(Customer c)
    {
        int want = c.want;
        S.shelf -= want;
        int price = G.BottlePrice;
        float earnF = price * want;
        int tip = c.vip ? price * 2 : 0;                                 // VIP 팁은 콤보 배수 밖
        if (c.vip) earnF *= 3;                                           // VIP ×3
        combo = Time.time - lastSaleT < 2f ? combo + 1 : 1; lastSaleT = Time.time;
        if (combo >= 3) earnF *= 1 + 0.1f * Mathf.Min(combo, 5);        // 콤보 최대 +50%
        int earn = Mathf.RoundToInt(earnF) + tip;
        S.cash += earn; S.sold += want; S.custs++;
        HUD.I?.SetCombo(combo);
        bool big = c.vip || want >= 3 || combo >= 3;
        if (big) { JuiceFX.I.Confetti(c.head.position + Vector3.up * 1.2f, c.vip ? 36 : 18); JuiceFX.I.Shake(c.vip ? 4 : 2); }
        if (c.vip) AudioManager.I.Play("up", 1.3f);
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
        WorldLabels.I.Float(c.head.position + Vector3.up * 1.5f, "+" + earn + (combo >= 3 ? "  ×" + Mathf.Min(combo, 5) : ""), true, big);
        JuiceFX.I.Squash(Stations.I.registerView, 0.4f);
        G.NotifySold();
    }
}

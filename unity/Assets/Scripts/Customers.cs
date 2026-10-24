using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// §5.2 정착민(구 손님): 입장 → 줄 → (맨 앞) 결제 → 받은 물자 머리 위로 → 점프 → (휴게 돔 빈 좌석이 있으면 착석 → 팁) → 퇴장.
// 정착민마다 원하는 종류 하나(kind: bottle 물·o2 산소·food 식량) × 개수(want). 카운터도 종류별 칸에서 꺼낸다.
public class Customer
{
    public enum St { WalkIn, Queue, ToSeat, Sit, Leave, Gone }
    public St st = St.WalkIn;
    public string name;   // 정착민 이름(성+이름, 거주 구역 패널·지도에서 씀. 말풍선엔 안 씀)
    public Vector2 pos; public string kind = "bottle"; public int want; public float wait, pay; public bool angry, bought, vip, switched;   // switched: 원하는 칸이 비어 다른 종류로 한 번 바꿨음
    public Transform view, body, head, hold; public WorldLabels.Label bubble;
    public float jumpT = -1;
    // §5.2 휴게 돔: 예약 좌석(-1 없음)·경유점 큐(seatPath/leavePath)·착석 남은 시간·올라앉기 진행·앉은 지 경과
    public int seat = -1; public List<Vector2> wp; public float sitT, sitK, sitAge; public Vector2 sitFrom, sitTo; public bool sitZz;

    // 한국식 짧은 이름: 성 16 × 이름 30
    static readonly string[] SURNAMES = { "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임", "한", "오", "서", "신", "권", "황" };
    static readonly string[] GIVEN = { "하늘", "민준", "서연", "지우", "도윤", "수아", "예준", "하린", "시우", "지호", "유나", "은우", "서진", "다온", "로아",
                                       "태오", "나래", "보람", "슬기", "한결", "이안", "소율", "주원", "채원", "윤서", "건우", "아린", "도담", "새봄", "별이" };
    public static string RandomName() => SURNAMES[Random.Range(0, SURNAMES.Length)] + GIVEN[Random.Range(0, GIVEN.Length)];
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
    public bool AnyWaitingToPay() { var f = Front(); return f != null && G.ShelfOf(f.kind) >= f.want; }

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
        var c = new Customer { pos = WorldMap.ENTRY, name = Customer.RandomName() };
        int ads = G.PadLv("ads");
        c.vip = S.tut >= 3 && S.sold >= 40 && S.custN % 10 == 0;   // 10번째 손님마다 VIP
        c.want = S.tut < 3 ? 1 : c.vip ? Random.Range(2, 4) : 1 + (Random.value < 0.3f + 0.06f * ads ? 1 : 0) + (Random.value < 0.1f + 0.05f * ads ? 1 : 0);
        c.kind = S.tut < 3 ? "bottle" : PickKind();
        BuildView(c);
        list.Add(c);
    }

    // 열린 종류 중 가중치로: 물만 → 물 100 / 산소 열림 → 물 60·산소 40 / 식량 열림 → 물 40·산소 30·식량 30
    string PickKind()
    {
        bool o2 = G.KindOpen("o2"), food = G.KindOpen("food");
        float wW = food ? 40 : o2 ? 60 : 100, wO = o2 ? (food ? 30 : 40) : 0, wF = food ? 30 : 0;
        float r = Random.value * (wW + wO + wF);
        return r < wW ? "bottle" : r < wW + wO ? "o2" : "food";
    }

    // 말풍선 글자: "물 ×2"·"산소 ×1"·"식량 ×1"(VIP는 앞에 "VIP "). 앞 낱말은 WorldLabels가 아이콘으로 바꾼다
    // 원하는 칸이 비었으면 칸에 있는 다른 종류로 한 번만 바꾼다(없으면 그대로 기다림). 개수는 그 칸 재고 안에서
    void SwitchKind(Customer c)
    {
        string best = null; int bestN = 0;
        foreach (var k in GameState.SUPPLY_KINDS) if (k != c.kind && G.KindOpen(k) && G.ShelfOf(k) > bestN) { best = k; bestN = G.ShelfOf(k); }
        if (best == null) return;
        c.switched = true; c.kind = best; c.want = Mathf.Min(c.want, bestN);
        c.bubble.Set(BubbleText(c)); c.bubble.text.color = GameConfig.NAVY;
        JuiceFX.I.Squash(c.body, 0.6f);
    }

    static string BubbleText(Customer c) => (c.vip ? "VIP " : "") + GameState.KindName(c.kind) + " ×" + c.want;

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
        if (c.vip) HullOutline.Hull(c.body, HullOutline.GOLD, 2f);   // §5.2 VIP: 금 외곽선(0xd49000)
        c.hold = new GameObject("hold").transform; c.hold.SetParent(c.body, false); c.hold.localPosition = new Vector3(0, (c.vip ? 66 : 56) * k, 0);
        c.bubble = WorldLabels.I.Add(() => c.head.position + Vector3.up * (c.vip ? 1.3f : 1.0f), BubbleText(c), 20, GameConfig.NAVY, Vector2.zero, true, c.vip ? GameConfig.GOLD : null);
        c.bubble.Show(false);
    }

    void Remove(Customer c)
    {
        if (c.seat >= 0) { DomeSystem.I?.ReleaseSeat(c.seat); c.seat = -1; }
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
                    if (MoveTo(c, target, GameConfig.CUST_WALK, dt)) c.st = Customer.St.Queue;
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
                    // 말풍선은 줄 맨 앞(결제 대상) 한 명만: 나머지는 숨겨 화면을 비운다(화난 느낌표도 맨 앞일 때만 보임)
                    c.bubble.Show(qi == 0 && arrived && !c.bought);
                    if (qi == 0 && !c.bought && arrived && !c.switched && c.wait > 1.5f && G.ShelfOf(c.kind) < c.want) SwitchKind(c);
                    if (qi == 0 && !c.bought)
                    {
                        bool canPay = arrived && G.ShelfOf(c.kind) >= c.want && (G.HasCashier || WorldMap.NearStn(Player.I.pos, "register", GameConfig.R_PAY));
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
                                if (c.wait > GameConfig.CUST_WARN && !c.angry) { c.angry = true; c.bubble.Set(BubbleText(c) + " !"); c.bubble.text.color = GameConfig.Hex("e03c3c"); }
                                if (c.wait > GameConfig.CUST_PATIENCE) { S.lost++; c.bubble.Show(false); c.st = Customer.St.Leave; AudioManager.I.Play("no"); break; }
                            }
                        }
                    }
                    if (c.bought)
                    {
                        // 점프 0.5s(높이 22) 후 퇴장
                        c.jumpT += dt; float j = Mathf.Clamp01(c.jumpT / 0.5f);
                        c.body.localPosition = new Vector3(0, Mathf.Sin(Mathf.PI * j) * 22 * k, 0);
                        if (j >= 1) { c.body.localPosition = Vector3.zero; AfterPurchase(c); }
                    }
                    break;
                }
            case Customer.St.ToSeat:
                if (FollowPath(c, GameConfig.CUST_WALK, dt)) SitDown(c);
                else Bob(c, true);
                break;
            case Customer.St.Sit:
                TickSit(c, dt);
                break;
            case Customer.St.Leave:
                if (c.wp != null && c.wp.Count > 0) { if (FollowPath(c, GameConfig.CUST_LEAVE, dt)) c.st = Customer.St.Gone; }   // 돔에서 나가는 손님: leavePath
                else if (MoveTo(c, WorldMap.ENTRY, GameConfig.CUST_LEAVE, dt)) c.st = Customer.St.Gone;
                Bob(c, true);
                break;
        }
        c.view.position = WorldMap.W2U(c.pos);
    }

    // §5.2 휴게 돔: 결제·점프가 끝나면 빈 좌석을 예약해 seatPath로 걸어가고, 없으면 기존대로 퇴장
    void AfterPurchase(Customer c)
    {
        if (Frontier.I != null && Frontier.I.TryMoveIn(c)) return;   // 2장: 일부(3명 중 1명)는 거주 구역 집으로 걸어가 산다(경유점·상태는 Frontier가 채움)
        var dome = DomeSystem.I;
        if (dome != null && dome.TryReserveSeat(out int s)) { c.seat = s; c.wp = dome.SeatPath(s, c.pos); c.st = Customer.St.ToSeat; }
        else c.st = Customer.St.Leave;
    }

    // 경유점 큐를 앞에서부터 따라 걷는다. 다 지나면 true
    static bool FollowPath(Customer c, float speed, float dt)
    {
        if (c.wp == null || c.wp.Count == 0) return true;
        if (MoveTo(c, c.wp[0], speed, dt)) c.wp.RemoveAt(0);
        return c.wp.Count == 0;
    }

    // 좌석 앞(s.y+10) 도착 → 뒤로 폴짝 올라앉기 시작. 5~8초 앉는다(원본 sitT=5+rand*3). 착석 손님 말풍선(하트·zz)은 두지 않는다
    void SitDown(Customer c)
    {
        var dome = DomeSystem.I;
        c.st = Customer.St.Sit; c.sitT = Random.Range(5f, 8f); c.sitK = 0; c.sitAge = 0; c.sitZz = false;
        c.sitFrom = c.pos; c.sitTo = dome != null ? dome.SitPoint(c.seat) : c.pos;
        c.bubble.Show(false);
        AudioManager.I.Play("pop", 1.4f, 0.35f);
    }

    // 앉아 있기: 0.32s 동안 뒤로 폴짝(높이 8) 올라앉으며 남쪽(카메라) 보기, 다리는 앞으로 뻗어 발장구, 팔은 무릎 위,
    // 숨쉬기·두리번. 시간이 다 되면 팁 round(price*0.5)을 좌석에 남기고 leavePath로 퇴장(CUST_LEAVE)
    void TickSit(Customer c, float dt)
    {
        float k = GameConfig.K, t = Time.time;
        var rig = c.body.GetComponent<CharRig>(); float ph = rig ? rig.ph : 0f;
        bool wasUp = c.sitK < 1f;
        c.sitK = Mathf.Min(1f, c.sitK + dt / 0.32f); c.sitAge += dt;
        float e = 1f - (1f - c.sitK) * (1f - c.sitK);
        c.pos = Vector2.Lerp(c.sitFrom, c.sitTo, e);
        bool seated = c.sitK >= 1f;
        float hop = Mathf.Sin(Mathf.PI * c.sitK) * 8f, breath = seated ? Mathf.Sin(t * 2.2f + ph) * 0.5f : 0f;
        c.body.localPosition = new Vector3(0, (1f + hop + breath) * k, 0);   // 엉덩이(다리 피벗 11)를 좌판 윗면(11) 위로 +1
        c.view.rotation = Quaternion.Slerp(c.view.rotation, Quaternion.LookRotation(Vector3.back, Vector3.up), Mathf.Min(1f, dt * 12f));
        if (wasUp && seated) DomeSystem.I?.OnSit(c.seat);   // 엉덩이가 닿는 순간 한 번
        if (rig != null)
        {
            float a = Mathf.Min(1f, dt * 12f), kick = seated ? Mathf.Sin(t * 3.4f + ph) * 12f : 0f;
            if (rig.legL) rig.legL.localRotation = Quaternion.Slerp(rig.legL.localRotation, Quaternion.Euler(-80f + kick, 0, 0), a);
            if (rig.legR) rig.legR.localRotation = Quaternion.Slerp(rig.legR.localRotation, Quaternion.Euler(-80f - kick, 0, 0), a);
            if (rig.armL) rig.armL.localRotation = Quaternion.Slerp(rig.armL.localRotation, Quaternion.Euler(-35f, 0, -8f), a);
            if (rig.armR) rig.armR.localRotation = Quaternion.Slerp(rig.armR.localRotation, Quaternion.Euler(-35f, 0, 8f), a);
            if (rig.head && seated) rig.head.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f + ph) * 4f, Mathf.Sin(t * 0.6f + ph) * 22f, 0);
        }
        c.sitT -= dt;
        if (c.sitT <= 0)
        {
            var dome = DomeSystem.I;
            if (dome != null)
            {
                dome.LeaveTip(c.seat, Mathf.FloorToInt(G.PriceOf(c.kind) * 0.5f + 0.5f));   // JS Math.round와 같은 반올림
                dome.ReleaseSeat(c.seat);
                c.wp = dome.LeavePath(c.seat);
            }
            c.seat = -1; c.st = Customer.St.Leave;
            c.bubble.Show(false);
            if (rig != null && rig.head) rig.head.localRotation = Quaternion.identity;
        }
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

    // §1.3 결제: 그 종류 칸에서 want만큼 차감, 수익은 계산대 더미(S.cash)로, 물자는 머리 위로 날아가 쌓임
    void Pay(Customer c)
    {
        int want = c.want; string kind = c.kind;
        G.AddShelf(kind, -want);
        int price = G.PriceOf(kind);
        float earnF = price * want;
        int tip = c.vip ? price * 2 : 0;                                 // VIP 팁은 콤보 배수 밖
        if (c.vip) earnF *= 3;                                           // VIP ×3
        combo = Time.time - lastSaleT < 2f ? combo + 1 : 1; lastSaleT = Time.time;
        if (combo >= 3) earnF *= 1 + 0.1f * Mathf.Min(combo, 5);        // 콤보 최대 +50%
        int earn = Mathf.RoundToInt(earnF) + tip;
        S.cash += earn; S.custs++;
        if (kind == "bottle") S.sold += want;                            // 기존 목표·통계(병 판매)는 물만 센다
        G.NotifySupplied(kind, want);
        HUD.I?.SetCombo(combo);
        bool big = c.vip || want >= 3 || combo >= 3;
        if (big) { JuiceFX.I.Confetti(c.head.position + Vector3.up * 1.2f, c.vip ? 36 : 18); JuiceFX.I.Shake(c.vip ? 4 : 2); }
        if (c.vip) AudioManager.I.Play("up", 1.3f);
        if (c.vip) MissionBoard.I?.Count("vip", 1);   // 하루 미션: VIP 받기
        c.bought = true; c.jumpT = 0; c.pay = 0;
        c.bubble.Show(false);
        for (int i = 0; i < want; i++)
        {
            int idx = i;
            ArcFlyer.I.FlyItem(kind, Stations.I.ShelfTop(kind), () => c.hold.position + Vector3.up * idx * Items.ITEM_H[kind] * GameConfig.K, () =>
            {
                if (c.hold != null) { var s = Items.Slab(c.hold, kind); s.transform.localPosition = new Vector3(0, idx * Items.ITEM_H[kind] * GameConfig.K, 0); }
            }, 0.3f, 30f);
        }
        AudioManager.I.Play("sell");
        WorldLabels.I.Float(c.head.position + Vector3.up * 1.5f, "+" + earn + (combo >= 3 ? "  ×" + Mathf.Min(combo, 5) : ""), true, big);
        JuiceFX.I.Squash(Stations.I.registerView, 0.4f);
        G.NotifySold();
    }
}

using System.Collections;
using UnityEngine;

// §2 플레이어: 이동(축분리 충돌)·걷기 애니·등짐 스택(흔들림)·§1.1 상호작용 우선순위 표
public class Player : MonoBehaviour
{
    public static Player I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public Vector2 pos, dir = new Vector2(0, 1), vel;
    public bool walking;
    public float actT;
    public Transform view, body, stackAnchor;
    Transform armL, armR, legL, legR, head;
    float swX, swZ, swVX, swVZ;   // 등짐 흔들림(스프링: 위치·속도)
    WorldLabels.Label maxTag;      // 가득 찼을 때만 탑 위 "MAX"
    int lastCarry = -1; string stackKind = "";
    float popT = 1f;               // 맨 위 블록 팝(0→1)
    const float LAYER = Items.CARRY_H;

    void Awake() { I = this; }

    void Start()
    {
        pos = new Vector2(S.p.x, S.p.y); dir = new Vector2(S.p.dx, S.p.dy); if (dir.sqrMagnitude < 0.01f) dir = new Vector2(0, 1);
        BuildView();
        // QA: -carry N 이면 시작부터 얼음 N개를 진 채로(등짐 탑 확인용, 저장 분리 -save 와 함께 쓸 것)
        var cf = DevArgs.Get("-carry");
        if (!string.IsNullOrEmpty(cf) && int.TryParse(cf, out var cn) && S.p.carry.Count == 0) for (int i = 0; i < cn; i++) S.p.carry.Add("ice");
        // 머리 위 "얼음 n/32" 칩은 없앴다(jake 10/24). 가득 찼을 때만 탑 꼭대기에 금색 말풍선 "MAX"
        maxTag = WorldLabels.I.Add(() => MaxWorld(), "MAX", 18, Color.white, Vector2.zero, true, GameConfig.GOLD);
        maxTag.text.color = GameConfig.Hex("e03c3c");
        maxTag.Show(false);
        if (CameraRig.I) { CameraRig.I.target = view; CameraRig.I.velL = () => vel; }
    }

    void BuildView()
    {
        view = new GameObject("player").transform; view.SetParent(transform, false);
        view.position = WorldMap.W2U(pos);
        Prim.BlobShadow(view, 26);
        body = new GameObject("body").transform; body.SetParent(view, false);
        float k = GameConfig.K;
        // 우주복 치비(원본 bPerson, 키 ≈ 64). 팔다리는 피벗이 위쪽 끝이라 Animate 가 회전시킨다
        CharacterKit.BuildPlayer(body, out head, out armL, out armR, out legL, out legR);
        // 등짐 탑: 피자레디처럼 등 뒤에 높이. 몸(body)이 걸을 때 앞으로 숙여도 탑은 서 있도록 view 에 달고 들썩임만 따로 더한다.
        // 카메라(남쪽 위 55°)에 몸으로 덜 가리게 등에서 살짝 위·뒤(0, 34, -25), 아주 조금 뒤로 기울임
        stackAnchor = new GameObject("stack").transform; stackAnchor.SetParent(view, false);
        stackAnchor.localPosition = new Vector3(0, 34 * k, -25 * k);
        stackAnchor.localRotation = Quaternion.Euler(-0.08f * Mathf.Rad2Deg, 0, 0);
    }

    // i층(실수 허용) 블록 중심의 앵커 로컬 위치. 휨은 위로 갈수록 더 커진다(선형+제곱, 상한)
    float Bend(float i) => Mathf.Min(i * 0.07f + i * i * 0.0035f, 3.6f);
    Vector3 LayerPos(float i) => new Vector3(swX * Bend(i) * GameConfig.K, (LAYER / 2 + i * LAYER) * GameConfig.K, swZ * Bend(i) * GameConfig.K);
    // 아이템 비행 도착점: 지금 맨 위(대기 중인 비행 포함) 블록의 윗면. 흔들림까지 따라간다
    public Vector3 StackTop() => stackAnchor.TransformPoint(LayerPos(Mathf.Min(G.Carry + G.pend, GameConfig.STACK_MAX) - 0.5f));
    Vector3 MaxWorld() => stackAnchor.TransformPoint(LayerPos(Mathf.Min(G.Carry, GameConfig.STACK_MAX) - 0.5f)) + Vector3.up * 9 * GameConfig.K;

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        Move(dt);
        Animate(dt);
        if (actT > 0) actT -= dt;
        Interact();
        SyncStack(dt);
        S.p.x = pos.x; S.p.y = pos.y; S.p.dx = dir.x; S.p.dy = dir.y;
    }

    void Move(float dt)
    {
        Vector2 inp = FloatingJoystick.I ? FloatingJoystick.I.Value : Vector2.zero;
        if (inp.magnitude > 1) inp.Normalize();
        float sp = G.Speed * inp.magnitude;
        walking = inp.sqrMagnitude > 0.0001f;
        Vector2 v = walking ? inp.normalized * sp : Vector2.zero;
        vel = v;
        if (walking)
        {
            // 한 걸음이 울타리 두께(10)를 넘지 않게 4 단위로 나눠 검사(느린 프레임에서 벽 통과 방지)
            int n = Mathf.Max(1, Mathf.CeilToInt(sp * dt / 4f)); float sdt = dt / n;
            for (int k = 0; k < n; k++)
            {
                float nx = pos.x + v.x * sdt; if (!WorldMap.Blocked(nx, pos.y)) pos.x = nx;
                float ny = pos.y + v.y * sdt; if (!WorldMap.Blocked(pos.x, ny)) pos.y = ny;
            }
            dir = Vector2.Lerp(dir, inp.normalized, dt * 14f).normalized;
        }
        view.position = WorldMap.W2U(pos);
        view.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, -dir.y), Vector3.up);
    }

    void Animate(float dt)
    {
        float t = Time.time * 1000f, k = GameConfig.K;
        if (walking)
        {
            float s = Mathf.Sin(t / 90f);
            body.localPosition = new Vector3(0, Mathf.Abs(s) * 2.5f * k, 0);
            armL.localRotation = Quaternion.Euler(s * 0.7f * 40f, 0, 0); armR.localRotation = Quaternion.Euler(-s * 0.7f * 40f, 0, 0);
            legL.localRotation = Quaternion.Euler(-s * 40f, 0, 0); legR.localRotation = Quaternion.Euler(s * 40f, 0, 0);
            var e = body.localRotation.eulerAngles; float tilt = Mathf.LerpAngle(e.x, 0.14f * Mathf.Rad2Deg, dt * 8f);
            body.localRotation = Quaternion.Euler(tilt, 0, s * 0.05f * Mathf.Rad2Deg);
        }
        else
        {
            body.localPosition = new Vector3(0, Mathf.Sin(t / 600f) * 0.8f * k, 0);
            var e = body.localRotation.eulerAngles;
            body.localRotation = Quaternion.Euler(Mathf.LerpAngle(e.x, 0, dt * 8f), 0, 0);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.identity, dt * 8f); armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.identity, dt * 8f);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.identity, dt * 8f); legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.identity, dt * 8f);
        }
    }

    // §2.2 흔들림(탄력판): 로컬 속도 ×0.06 → 목표 clamp(-v*0.9, ±9). 원본의 lerp 대신 덜 감쇠된 스프링이라
    // 멈추거나 방향을 틀면 탑이 관성으로 넘어갈 듯 휘청이다 되돌아온다. 걷는 동안엔 발걸음 따라 좌우로 살짝 출렁
    void SyncStack(float dt)
    {
        var carry = S.p.carry; string kind = G.CarryTop;
        int n = Mathf.Min(carry.Count, GameConfig.STACK_MAX);
        if (n != lastCarry || kind != stackKind)
        {
            // 종류가 바뀌면 전부 교체(빈 손이 되면 전부 비움)
            if (kind != stackKind) { ClearStack(); stackKind = kind; }
            // 내려놓기: 맨 위부터 빠진다
            for (int i = stackAnchor.childCount - 1; i >= n; i--) { var c = stackAnchor.GetChild(i); c.SetParent(null, false); Destroy(c.gameObject); }
            bool grew = stackAnchor.childCount < n;
            for (int i = stackAnchor.childCount; i < n; i++) Items.CarryBlock(stackAnchor, kind);
            if (grew) popT = 0f;      // 줍는 순간 맨 위 블록 팝
            lastCarry = n;
        }
        Vector3 fwd = new Vector3(dir.x, 0, -dir.y), right = Vector3.Cross(Vector3.up, fwd);
        Vector3 vu = new Vector3(vel.x, 0, -vel.y);
        float lf = Vector3.Dot(vu, fwd) * 0.06f, ls = Vector3.Dot(vu, right) * 0.06f;
        float tz = Mathf.Clamp(-lf * 0.9f, -9, 9), tx = Mathf.Clamp(-ls * 0.9f, -9, 9);
        if (walking) tx += Mathf.Sin(Time.time * 1000f / 180f) * 1.2f;   // 발걸음(두 걸음 주기) 출렁
        const float KS = 70f, KD = 7.5f;   // 스프링 강성·감쇠(감쇠비≈0.45, 한두 번 튕기고 멈춤)
        swVX += ((tx - swX) * KS - swVX * KD) * dt; swX += swVX * dt;
        swVZ += ((tz - swZ) * KS - swVZ * KD) * dt; swZ += swVZ * dt;
        swX = Mathf.Clamp(swX, -14, 14); swZ = Mathf.Clamp(swZ, -14, 14);
        if (popT < 1f) popT = Mathf.Min(1f, popT + dt / 0.22f);
        float bob = body.localPosition.y;   // 몸 들썩임만 따라감
        stackAnchor.localPosition = new Vector3(0, 34 * GameConfig.K + bob, -25 * GameConfig.K);
        int cnt = stackAnchor.childCount;
        for (int i = 0; i < cnt; i++)
        {
            var c = stackAnchor.GetChild(i);
            c.localPosition = LayerPos(i);
            // 기울기 = 휨 곡선의 기울기(층 사이 수평 차이 / 층 높이)
            float slope = (Bend(i + 0.5f) - Bend(i - 0.5f)) / LAYER * 0.8f;
            c.localRotation = Quaternion.Euler(-Mathf.Atan(swZ * slope) * Mathf.Rad2Deg, 0, Mathf.Atan(swX * slope) * Mathf.Rad2Deg);
            float sc = 1f;
            if (i == cnt - 1 && popT < 1f)
            {
                // easeOutBack 0.45→1(살짝 넘쳤다 돌아옴)
                float p = popT, c1 = 2.2f, c3 = c1 + 1f;
                float e = 1f + c3 * Mathf.Pow(p - 1f, 3) + c1 * Mathf.Pow(p - 1f, 2);
                sc = Mathf.LerpUnclamped(0.45f, 1f, e);
            }
            c.localScale = Vector3.one * sc;
        }
        maxTag.Show(n > 0 && n >= G.CarryMax);
        if (n > 0 && n >= G.CarryMax) maxTag.rt.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 7f) * 0.06f);
    }

    void ClearStack()
    {
        for (int i = stackAnchor.childCount - 1; i >= 0; i--) { var c = stackAnchor.GetChild(i); c.SetParent(null, false); Destroy(c.gameObject); }
        lastCarry = 0;
    }

    // §1.1 순서대로 첫 번째로 성립한 행동 1개만
    void Interact()
    {
        if (actT > 0) return;
        var st = Stations.I; if (st == null) return;
        // 1. 얼음 줍기
        if (G.CanCarry("ice"))
        {
            int best = -1; float bd = GameConfig.R_ICE;
            for (int i = 0; i < st.SpotCount; i++)
            {
                if (!st.SpotOpen(i)) continue;
                float d = Vector2.Distance(pos, WorldMap.ICE_SPOTS[i]);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0)
            {
                S.spots[best] = GameConfig.SpotRespawn();
                G.pend++; G.pendK = "ice";
                ArcFlyer.I.Fly(FlyBlock("ice"), st.SpotWorld(best), StackTop, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => Arrive("ice"));
                actT = GameConfig.CD_ICE; return;
            }
        }
        // 2. 해빙기에 넣기
        if (G.CarryTop == "ice" && WorldMap.NearStn(pos, "melter", GameConfig.R_MELT))
        {
            PopTop();
            ArcFlyer.I.Fly(FlyBlock("ice"), StackTop(), st.MeltTop, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { S.meltQ++; });
            AudioManager.I.Play("put", 1f + Random.Range(-0.05f, 0.05f));
            actT = GameConfig.CD_MELT; return;
        }
        // 3. 병 받기
        if (S.bottles > 0 && G.CanCarry("bottle") && WorldMap.NearStn(pos, "bottler", GameConfig.R_BOTTLE))
        {
            S.bottles--; G.pend++; G.pendK = "bottle";
            ArcFlyer.I.Fly(FlyBlock("bottle"), st.TrayTop(), StackTop, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => Arrive("bottle"));
            actT = GameConfig.CD_BOTTLE; return;
        }
        // 4. 선반 올리기 (카운터 뒤쪽 p.y < counter.y)
        if (G.CarryTop == "bottle" && S.shelf + G.shelfPend < G.ShelfCap && pos.y < WorldMap.STN["counter"].y && WorldMap.NearStn(pos, "counter", GameConfig.R_SHELF))
        {
            PopTop(); G.shelfPend++;
            ArcFlyer.I.Fly(FlyBlock("bottle"), StackTop(), st.ShelfTop, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { G.shelfPend--; S.shelf++; if (S.tut == 2) TutorialManager.I?.Advance(3); });
            AudioManager.I.Play("put", 1.2f);
            actT = GameConfig.CD_SHELF; return;
        }
        // 5. 지폐 수거
        if (S.cash > 0 && !G.collectingCash && WorldMap.NearStn(pos, "register", GameConfig.R_CASH))
        {
            StartCoroutine(CollectCash());
            actT = GameConfig.CD_CASH; return;
        }
        // 6. 토끼 벨 수거 (S.penTip>0, PEN_TIP 원 r40, 쿨다운 0.2). 벨 가산·연출은 PenSystem.Collect
        if (S.penTip > 0 && PenSystem.I != null && Vector2.Distance(pos, WorldMap.PEN_TIP) < PenSystem.R_TIP)
        {
            PenSystem.I.Collect();
            JuiceFX.I.Squash(body, 1f); JuiceFX.I.Vibrate(15);
            actT = PenSystem.CD_TIP; return;
        }
    }

    void PopTop() { S.p.carry.RemoveAt(S.p.carry.Count - 1); }
    // 탑과 같은 모양의 블록이 날아오고 날아간다(조금 작게)
    GameObject FlyBlock(string kind) { var go = Items.CarryBlock(ArcFlyer.I.transform, kind); go.transform.localScale = Vector3.one * 0.85f; return go; }

    void Arrive(string kind)
    {
        G.pend = Mathf.Max(0, G.pend - 1); if (G.pend == 0) G.pendK = "";
        S.p.carry.Add(kind);
        JuiceFX.I.Squash(body, 0.9f);
        int n = S.p.carry.Count, max = G.CarryMax;
        AudioManager.I.PlayHz("pick", 520 + 380 * (n / (float)max), 520);
        if (n % 3 == 0) JuiceFX.I.Vibrate(6);
        if (n >= max) AudioManager.I.Play("ding", 1f, 0.7f);
        if (kind == "ice" && S.tut == 0 && n >= max) TutorialManager.I?.Advance(1);
    }

    // §1.2 수거 연출: 윗장부터 40ms 간격으로 플레이어에게 날림(0.22s), 도착마다 벨 가산
    IEnumerator CollectCash()
    {
        G.collectingCash = true;
        int amount = S.cash; S.cash = 0;
        G.AddBells(amount);   // 저장·종료 안전: 전액을 즉시 가산하고 비행은 연출만
        int n = Stations.Bills(amount); var st = Stations.I;
        bool big = amount >= G.BottlePrice * 8;
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            int layer = (n - 1 - i) / 3, col = (n - 1 - i) % 3;
            Vector3 from = st.cashAnchor.position + new Vector3((col - 1) * 16 * GameConfig.K, (layer + 0.5f) * Items.ITEM_H["bill"] * GameConfig.K, 0);
            var go = Items.Slab(ArcFlyer.I.transform, "bill");
            ArcFlyer.I.Fly(go, from, () => body.position + Vector3.up * 1.4f, 0.22f, 30f, () =>
            {
                AudioManager.I.PlayHz("coin", 1100 * Mathf.Pow(1.06f, Mathf.Min(12, idx)), 1100, 0.5f);
            });
            yield return new WaitForSeconds(0.04f);
        }
        yield return new WaitForSeconds(0.22f);
        WorldLabels.I.Float(body.position + Vector3.up * 2f, "+" + amount, true, amount >= G.BottlePrice * 4);
        if (big) { JuiceFX.I.CoinPop(body.position + Vector3.up * 1f, 12); JuiceFX.I.Shake(3); }
        G.collectingCash = false;
    }
}

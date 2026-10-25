using System.Collections;
using System.Collections.Generic;
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
    // 등짐 탑(섞어 싣기 10/25): 종류마다 블록 풀을 두고 S.p.carry 순서(종류별 묶음)대로 칸에 앉힌다. 맨 아래는 보유 벨 비례 지폐 묶음
    readonly Dictionary<string, List<Transform>> pools = new Dictionary<string, List<Transform>>();
    readonly Dictionary<string, int> want = new Dictionary<string, int>(), used = new Dictionary<string, int>();
    readonly List<Transform> bills = new List<Transform>();
    readonly Dictionary<Transform, float> pops = new Dictionary<Transform, float>();   // 새로 실린 블록 팝(0→1)
    readonly List<Transform> popDone = new List<Transform>();
    // 한 층에 여러 개(2×2, 용량 120 넘으면 3×2)를 원래 블록의 0.6배로 앉힌다: 100개를 한 줄로 쌓으면 화면을 뚫는다
    const float BS = 0.6f, GX = 10.8f, GZ = 8.0f;   // 블록 배율·칸 간격(논리)
    const float LH = Items.CARRY_H * BS;            // 한 층 높이
    const float BILL_LH = 2.4f;                     // 지폐 묶음 한 층 높이
    const int ROWS = 2;
    int Cols => G.CarryMax > 120 ? 3 : 2;
    int PerLayer => Cols * ROWS;
    int MaxShow => GameConfig.STACK_LAYERS * PerLayer;
    float BillH => bills.Count * BILL_LH;

    void Awake() { I = this; }

    void Start()
    {
        pos = new Vector2(S.p.x, S.p.y); dir = new Vector2(S.p.dx, S.p.dy); if (dir.sqrMagnitude < 0.01f) dir = new Vector2(0, 1);
        BuildView();
        // QA: -carry N 이면 시작부터 얼음 N개를 진 채로(등짐 탑 확인용, 저장 분리 -save 와 함께 쓸 것)
        var cf = DevArgs.Get("-carry");
        // -carrymix 를 같이 주면 얼음·물통·레골리스·흙·비료·산소통·식량을 돌아가며 섞어 싣는다(종류별로 묶여 쌓이는지 확인)
        bool mix = DevArgs.Has("-carrymix"); string[] mixK = { "ice", "bottle", "rego", "soil", "fert", "o2", "food" };
        if (!string.IsNullOrEmpty(cf) && int.TryParse(cf, out var cn) && S.p.carry.Count == 0) for (int i = 0; i < cn; i++) G.AddCarry(mix ? mixK[i % mixK.Length] : "ice");
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

    // 높이 h(논리)에서의 휨. 예전 한 줄 탑(층 높이 CARRY_H)의 i층 곡선을 높이로 옮긴 것: 위로 갈수록 더 커진다(선형+제곱, 상한)
    float Bend(float i) => Mathf.Min(i * 0.07f + i * i * 0.0035f, 3.6f);
    float BendH(float h) => Bend(Mathf.Max(0f, h / Items.CARRY_H - 0.5f));
    Vector3 At(float x, float h, float z) => new Vector3((x + swX * BendH(h)) * GameConfig.K, h * GameConfig.K, (z + swZ * BendH(h)) * GameConfig.K);
    // 등짐 i번째 칸 중심(앵커 로컬): 지폐 층 위로 층마다 Cols×ROWS
    Vector3 SlotLocal(int i)
    {
        int per = PerLayer, layer = i / per, cell = i % per, c = cell % Cols, r = cell / Cols;
        return At((c - (Cols - 1) / 2f) * GX, BillH + (layer + 0.5f) * LH, (r - (ROWS - 1) / 2f) * GZ);
    }
    int Layers(int n) => Mathf.Min((n + PerLayer - 1) / PerLayer, GameConfig.STACK_LAYERS);
    float TopH(int n) => BillH + Layers(n) * LH;
    // 아이템 비행 도착점: 지금 맨 위(대기 중인 비행 포함) 층의 윗면. 흔들림까지 따라간다
    public Vector3 StackTop() => stackAnchor.TransformPoint(At(0, TopH(G.Carry + G.pend), 0));
    // 그 종류가 실릴 자리(같은 종류 묶음 끝). 화면에 안 그리는 칸이면 맨 위
    public Vector3 InsertWorld(string kind) { int i = G.CarryInsertIndex(kind); return i < MaxShow ? stackAnchor.TransformPoint(SlotLocal(i)) : StackTop(); }
    // 지폐 묶음 맨 위(계산대 지폐가 날아와 앉는 곳)
    public Vector3 BillTop() => stackAnchor.TransformPoint(At(0, BillH + BILL_LH * 0.5f, 0));
    Vector3 MaxWorld() => stackAnchor.TransformPoint(At(0, TopH(G.Carry), 0)) + Vector3.up * 9 * GameConfig.K;
    // 그 종류 하나를 등짐에서 빼고, 빠진 자리(비행 출발점)를 돌려준다
    Vector3 TakeOut(string kind)
    {
        int i = S.p.carry.LastIndexOf(kind);
        Vector3 from = i >= 0 && i < MaxShow ? stackAnchor.TransformPoint(SlotLocal(i)) : StackTop();
        G.RemoveCarry(kind);
        return from;
    }

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
        var carry = S.p.carry;
        int show = Mathf.Min(carry.Count, MaxShow);
        // 종류별 필요 수 → 풀 크기 맞추기. 같은 종류 묶음의 맨 위가 풀의 끝이라, 실으면 끝에 붙고 넣으면 끝에서 빠진다(나머지는 자연스럽게 내려앉음)
        want.Clear();
        for (int i = 0; i < show; i++) { var k = carry[i]; want[k] = (want.TryGetValue(k, out var w) ? w : 0) + 1; }
        foreach (var kv in pools)
            if (!want.ContainsKey(kv.Key)) Shrink(kv.Value, 0);
        foreach (var kv in want)
        {
            if (!pools.TryGetValue(kv.Key, out var pool)) { pool = new List<Transform>(); pools[kv.Key] = pool; }
            Shrink(pool, kv.Value);
            while (pool.Count < kv.Value) { var t = Items.CarryBlock(stackAnchor, kv.Key).transform; pool.Add(t); pops[t] = 0f; }
        }
        // 지폐 묶음(맨 아래 고정): 보유 벨 비례. 벨을 쓰면 위에서부터 줄어든다
        int nb = G.BillLayers;
        Shrink(bills, nb);
        while (bills.Count < nb) { var t = Items.CarryBlock(stackAnchor, "bill").transform; bills.Add(t); pops[t] = 0f; }

        Vector3 fwd = new Vector3(dir.x, 0, -dir.y), right = Vector3.Cross(Vector3.up, fwd);
        Vector3 vu = new Vector3(vel.x, 0, -vel.y);
        float lf = Vector3.Dot(vu, fwd) * 0.06f, ls = Vector3.Dot(vu, right) * 0.06f;
        float tz = Mathf.Clamp(-lf * 0.9f, -9, 9), tx = Mathf.Clamp(-ls * 0.9f, -9, 9);
        if (walking) tx += Mathf.Sin(Time.time * 1000f / 180f) * 1.2f;   // 발걸음(두 걸음 주기) 출렁
        const float KS = 70f, KD = 7.5f;   // 스프링 강성·감쇠(감쇠비≈0.45, 한두 번 튕기고 멈춤)
        swVX += ((tx - swX) * KS - swVX * KD) * dt; swX += swVX * dt;
        swVZ += ((tz - swZ) * KS - swVZ * KD) * dt; swZ += swVZ * dt;
        swX = Mathf.Clamp(swX, -14, 14); swZ = Mathf.Clamp(swZ, -14, 14);
        float bob = body.localPosition.y;   // 몸 들썩임만 따라감
        stackAnchor.localPosition = new Vector3(0, 34 * GameConfig.K + bob, -25 * GameConfig.K);
        // 팝 진행
        if (pops.Count > 0)
        {
            popDone.Clear(); popDone.AddRange(pops.Keys);
            foreach (var t in popDone) { float p = pops[t] + dt / 0.22f; if (p >= 1f || t == null) pops.Remove(t); else pops[t] = p; }
        }
        // 지폐: 탑 바닥 넓이만큼 넓고 얇게
        float bx = Cols * GX / 17f;
        for (int j = 0; j < bills.Count; j++)
        {
            float h = (j + 0.5f) * BILL_LH;
            Place(bills[j], At(0, h, 0), h, new Vector3(bx, BILL_LH / Items.CARRY_H, 1.25f));
        }
        // 물건: carry 순서대로 종류 풀에서 하나씩 꺼내 칸에 앉힌다
        used.Clear();
        for (int i = 0; i < show; i++)
        {
            var k = carry[i]; int u = used.TryGetValue(k, out var uu) ? uu : 0; used[k] = u + 1;
            var t = pools[k][u];
            var lp = SlotLocal(i);
            Place(t, lp, lp.y / GameConfig.K, Vector3.one * BS);
        }
        int n = carry.Count;
        maxTag.Show(n > 0 && n >= G.CarryMax);
        if (n > 0 && n >= G.CarryMax) maxTag.rt.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 7f) * 0.06f);
    }

    // 블록 하나 배치: 기울기 = 휨 곡선의 기울기(높이 차이 / 예전 층 높이), 새로 실린 블록은 easeOutBack 팝
    void Place(Transform c, Vector3 lp, float h, Vector3 scale)
    {
        c.localPosition = lp;
        float slope = (BendH(h + Items.CARRY_H / 2) - BendH(h - Items.CARRY_H / 2)) / Items.CARRY_H * 0.8f;
        c.localRotation = Quaternion.Euler(-Mathf.Atan(swZ * slope) * Mathf.Rad2Deg, 0, Mathf.Atan(swX * slope) * Mathf.Rad2Deg);
        float sc = 1f;
        if (pops.TryGetValue(c, out var p))
        {
            // easeOutBack 0.45→1(살짝 넘쳤다 돌아옴)
            float c1 = 2.2f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(p - 1f, 3) + c1 * Mathf.Pow(p - 1f, 2);
            sc = Mathf.LerpUnclamped(0.45f, 1f, e);
        }
        c.localScale = scale * sc;
    }

    void Shrink(List<Transform> pool, int n)
    {
        for (int i = pool.Count - 1; i >= n; i--) { var c = pool[i]; pool.RemoveAt(i); pops.Remove(c); c.SetParent(null, false); Destroy(c.gameObject); }
    }

    // §1.1 순서대로 첫 번째로 성립한 행동 1개만. 등짐은 섞어 싣는다(10/25): 줍기는 용량과 종류별 상한(G.WantsMore)만 보고,
    // 넣기는 그 시설이 받는 종류를 등짐 어디에 있든 하나 빼서 넣는다
    void Interact()
    {
        if (actT > 0) return;
        var st = Stations.I; if (st == null) return;
        // 1. 얼음 줍기. 해빙기 큐 + 든 얼음이 상한(MELT_CAP)이거나 물탱크가 넉넉하면(WATER_FULL) 지나가다 줍지 않는다(북쪽 갈 때 얼음밭을 지나며 얼음만 쌓이는 고리 방지, 레골리스와 같은 규칙)
        if (G.WantsMore("ice"))
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
                G.PendAdd("ice");
                ArcFlyer.I.Fly(FlyBlock("ice"), st.SpotWorld(best), () => InsertWorld("ice"), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => Arrive("ice"));
                actT = GameConfig.CD_ICE; return;
            }
        }
        // 2. 해빙기에 넣기(등짐 속 얼음 아무거나)
        if (G.Holds("ice") && WorldMap.NearStn(pos, "melter", GameConfig.R_MELT))
        {
            ArcFlyer.I.Fly(FlyBlock("ice"), TakeOut("ice"), st.MeltTop, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { S.meltQ++; });
            AudioManager.I.Play("put", 1f + Random.Range(-0.05f, 0.05f));
            actT = GameConfig.CD_MELT; return;
        }
        // 3. 병 받기. 물통을 둘 곳(카운터 물 칸·전기분해기) 빈자리가 이미 든 물통보다 많을 때만 집는다(지나가다 손이 병으로 묶여 카운터 앞에서 기다리는 고리 방지)
        if (S.bottles > 0 && G.WantsMore("bottle") && WorldMap.NearStn(pos, "bottler", GameConfig.R_BOTTLE))
        {
            S.bottles--; G.PendAdd("bottle");
            ArcFlyer.I.Fly(FlyBlock("bottle"), st.TrayTop(), () => InsertWorld("bottle"), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => Arrive("bottle"));
            actT = GameConfig.CD_BOTTLE; return;
        }
        // 4. 선반 올리기 (카운터 뒤쪽 p.y < counter.y): 물병·산소통·식량을 각자 칸으로, 칸마다 ShelfCap. 등짐에서 칸마다 맞는 종류를 꺼낸다
        if (pos.y < WorldMap.STN["counter"].y && WorldMap.NearStn(pos, "counter", GameConfig.R_SHELF))
            foreach (var sk in GameState.SUPPLY_KINDS)
            {
                if (!G.Holds(sk) || G.ShelfOf(sk) + st.ShelfPendOf(sk) >= G.ShelfCap) continue;
                var from = TakeOut(sk); st.AddShelfPend(sk, 1);
                ArcFlyer.I.Fly(FlyBlock(sk), from, () => st.ShelfTop(sk), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => { st.AddShelfPend(sk, -1); G.AddShelf(sk, 1); if (sk == "bottle" && S.tut == 2) TutorialManager.I?.Advance(3); });
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
        // 7~9. 생존 기지 생산 체인
        SettleInteract();
    }

    // 7. 레골리스 줍기 → 8. 시설에 원료 넣기(물통→전기분해기, 레골리스→정화기, 흙→배합기, 비료→온실) → 9. 출력 받기(산소통·흙·비료·식량)
    // 넣기가 받기보다 먼저라, 정화기·배합기 사이 틈에선 흙을 받자마자 배합기에 넣는다
    void SettleInteract()
    {
        var ss = SettleStations.I; if (ss == null) return;
        // 정화기 큐 + 든 레골리스가 이미 상한이면 지나가다 줍지 않는다(출력이 가득 차 멈춘 동안 레골리스만 계속 쌓이는 고리 방지)
        if (G.WantsMore("rego"))
        {
            int best = ss.NearestRego(pos, SettleStations.R_REGO);
            if (best >= 0)
            {
                S.rspots[best] = GameConfig.SpotRespawn();
                G.PendAdd("rego");
                ArcFlyer.I.Fly(FlyBlock("rego"), ss.RegoWorld(best), () => InsertWorld("rego"), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => Arrive("rego"));
                actT = SettleStations.CD_REGO; return;
            }
        }
        for (int i = 0; i < SettleStations.KEYS.Length; i++)
        {
            string ik = SettleStations.IN_K[i];
            if (!G.Holds(ik) || !ss.Near(i, pos) || !ss.CanPut(i)) continue;
            int fi = i;
            var from = TakeOut(ik); ss.qPend[i]++;
            ArcFlyer.I.Fly(FlyBlock(ik), from, () => ss.InTop(fi), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => ss.Put(fi));
            AudioManager.I.Play("put", 1f + Random.Range(-0.05f, 0.05f));
            actT = SettleStations.CD_PUT; return;
        }
        for (int i = 0; i < SettleStations.KEYS.Length; i++)
        {
            string k = SettleStations.OUT_K[i];
            if (!ss.CanTake(i) || !G.WantsMore(k) || !ss.Near(i, pos)) continue;
            Vector3 from = ss.OutTop(i);
            ss.Out(i)--; G.PendAdd(k);
            ArcFlyer.I.Fly(FlyBlock(k), from, () => InsertWorld(k), GameConfig.FLY_T, GameConfig.FLY_LIFT, () => Arrive(k));
            actT = SettleStations.CD_TAKE; return;
        }
    }

    // 탑과 같은 모양의 블록이 날아오고 날아간다(조금 작게)
    GameObject FlyBlock(string kind) { var go = Items.CarryBlock(ArcFlyer.I.transform, kind); go.transform.localScale = Vector3.one * FLY_S; return go; }
    public const float FLY_S = 0.7f;   // 나는 블록 배율(탑 블록 0.6보다 조금 크게)

    void Arrive(string kind)
    {
        G.PendDone(kind);
        G.AddCarry(kind);
        JuiceFX.I.Squash(body, 0.9f);
        int n = S.p.carry.Count, max = G.CarryMax;
        AudioManager.I.PlayHz("pick", 520 + 380 * (n / (float)max), 520);
        if (n % 3 == 0) JuiceFX.I.Vibrate(6);
        if (n >= max) AudioManager.I.Play("ding", 1f, 0.7f);
        if (kind == "ice" && S.tut == 0 && G.Held("ice") >= Mathf.Min(max, GameConfig.ICE_BATCH)) TutorialManager.I?.Advance(1);
        MissionBoard.I?.Picked(kind);   // 하루 미션: 얼음·레골리스 줍기
    }

    // §1.2 수거 연출: 윗장부터 40ms 간격으로 등짐 맨 아래 지폐 묶음으로 날림(0.22s). 벨은 즉시 가산되고 지폐 층(보유 벨 비례)이 같이 늘어난다
    IEnumerator CollectCash()
    {
        G.collectingCash = true;
        int amount = S.cash; S.cash = 0;
        G.AddBells(amount, "sales");   // 저장·종료 안전: 전액을 즉시 가산하고 비행은 연출만
        int n = Stations.Bills(amount); var st = Stations.I;
        bool big = amount >= G.BottlePrice * 8;
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            int layer = (n - 1 - i) / 3, col = (n - 1 - i) % 3;
            Vector3 from = st.cashAnchor.position + new Vector3((col - 1) * 16 * GameConfig.K, (layer + 0.5f) * Items.ITEM_H["bill"] * GameConfig.K, 0);
            var go = Items.Slab(ArcFlyer.I.transform, "bill");
            ArcFlyer.I.Fly(go, from, BillTop, 0.22f, 30f, () =>
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

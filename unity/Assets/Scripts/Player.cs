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
    float swX, swZ;      // 등짐 흔들림
    WorldLabels.Label chip;
    int lastCarry = -1;

    void Awake() { I = this; }

    void Start()
    {
        pos = new Vector2(S.p.x, S.p.y); dir = new Vector2(S.p.dx, S.p.dy); if (dir.sqrMagnitude < 0.01f) dir = new Vector2(0, 1);
        BuildView();
        chip = WorldLabels.I.Add(() => ChipWorld(), "", 24, Color.white, Vector2.zero, true, new Color(0.12f, 0.1f, 0.2f, 0.85f));
        chip.Show(false);
        if (CameraRig.I) { CameraRig.I.target = view; CameraRig.I.velL = () => vel; }
    }

    void BuildView()
    {
        view = new GameObject("player").transform; view.SetParent(transform, false);
        view.position = WorldMap.W2U(pos);
        Prim.BlobShadow(view, 26);
        body = new GameObject("body").transform; body.SetParent(view, false);
        float k = GameConfig.K;
        var suit = GameConfig.Hex("f4f1ea"); var dark = GameConfig.Hex("3b3a52");
        Prim.Capsule(body, "torso", new Vector3(0, 18 * k, 0), 15, 26, suit);
        head = Prim.Sphere(body, "head", new Vector3(0, 36 * k, 0), 15, suit).transform;
        Prim.Box(head, "visor", new Vector3(0, 0, 6 * k), new Vector3(10, 7, 4), GameConfig.Hex("5fc8ff"));
        Prim.Box(body, "pack", new Vector3(0, 20 * k, -9 * k), new Vector3(12, 14, 6), GameConfig.Hex("c9c3b6"));
        armL = Limb(body, "armL", new Vector3(-10 * k, 26 * k, 0), 4.5f, 14, suit);
        armR = Limb(body, "armR", new Vector3(10 * k, 26 * k, 0), 4.5f, 14, suit);
        legL = Limb(body, "legL", new Vector3(-4.5f * k, 9 * k, 0), 5.5f, 11, dark);
        legR = Limb(body, "legR", new Vector3(4.5f * k, 9 * k, 0), 5.5f, 11, dark);
        stackAnchor = new GameObject("stack").transform; stackAnchor.SetParent(body, false);
        stackAnchor.localPosition = new Vector3(0, 30 * k, -17 * k);
        stackAnchor.localRotation = Quaternion.Euler(-0.1f * Mathf.Rad2Deg, 0, 0);
    }
    // 피벗이 위쪽 끝인 팔다리
    static Transform Limb(Transform parent, string n, Vector3 pivot, float dL, float lenL, Color c)
    {
        var p = new GameObject(n).transform; p.SetParent(parent, false); p.localPosition = pivot;
        Prim.Capsule(p, "m", new Vector3(0, -lenL / 2 * GameConfig.K, 0), dL, lenL, c);
        return p;
    }

    public Vector3 StackTop() => stackAnchor.position + stackAnchor.up * (G.Carry + G.pend) * (Items.ITEM_H.TryGetValue(G.CarryTop == "" ? G.pendK : G.CarryTop, out var h) ? h : 3f) * GameConfig.K;
    Vector3 ChipWorld()
    {
        int n = G.Carry; float h = Mathf.Max(72, 34 + n * 3f + 14);
        return view.position + Vector3.up * h * GameConfig.K;
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
            float nx = pos.x + v.x * dt; if (!WorldMap.Blocked(nx, pos.y)) pos.x = nx;
            float ny = pos.y + v.y * dt; if (!WorldMap.Blocked(pos.x, ny)) pos.y = ny;
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

    // §2.2 흔들림: 로컬 속도 ×0.06 → 목표 clamp(-v*0.9, ±9), lerp dt*9
    void SyncStack(float dt)
    {
        var carry = S.p.carry; string kind = G.CarryTop;
        if (carry.Count != lastCarry)
        {
            lastCarry = carry.Count;
            if (carry.Count == 0) Items.SyncStack(stackAnchor, "ice", 0, 1, 1, 0, 0, GameConfig.STACK_MAX);
            else
            {
                // 종류가 바뀌면 전부 교체
                if (stackAnchor.childCount > 0 && stackAnchor.GetChild(0).name != kind) Items.SyncStack(stackAnchor, kind, 0, 1, 1, 0, 0, GameConfig.STACK_MAX);
                Items.SyncStack(stackAnchor, kind, carry.Count, 1, 1, 0, 0, GameConfig.STACK_MAX);
            }
        }
        // 로컬 속도: 전방·측방
        Vector3 fwd = new Vector3(dir.x, 0, -dir.y), right = Vector3.Cross(Vector3.up, fwd);
        Vector3 vu = new Vector3(vel.x, 0, -vel.y);
        float lf = Vector3.Dot(vu, fwd) * 0.06f, ls = Vector3.Dot(vu, right) * 0.06f;
        swZ = Mathf.Lerp(swZ, Mathf.Clamp(-lf * 0.9f, -9, 9), dt * 9f);
        swX = Mathf.Lerp(swX, Mathf.Clamp(-ls * 0.9f, -9, 9), dt * 9f);
        float h = kind == "" ? 0.1f : Items.ITEM_H[kind] * GameConfig.K;
        for (int i = 0; i < stackAnchor.childCount; i++)
        {
            int kk = Mathf.Min(i, 18); var c = stackAnchor.GetChild(i);
            c.localPosition = new Vector3(swX * kk * 0.11f * GameConfig.K, h / 2 + i * h, swZ * kk * 0.11f * GameConfig.K);
            c.localRotation = Quaternion.Euler(-swZ * 0.007f * kk * Mathf.Rad2Deg, 0, swX * 0.007f * kk * Mathf.Rad2Deg);
        }
        int n = carry.Count;
        if (n > 0 || G.pend > 0)
        {
            bool full = n >= G.CarryMax;
            chip.Set((kind == "ice" || G.pendK == "ice" ? "얼음 " : "병 ") + n + "/" + G.CarryMax);
            chip.text.color = full ? GameConfig.Hex("ff6b6b") : Color.white;
            chip.Show(true);
        }
        else chip.Show(false);
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
            for (int i = 0; i < GameConfig.BASE_SPOTS; i++)
            {
                if (!st.SpotOpen(i)) continue;
                float d = Vector2.Distance(pos, WorldMap.ICE_SPOTS[i]);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0)
            {
                S.spots[best] = GameConfig.SpotRespawn();
                G.pend++; G.pendK = "ice";
                ArcFlyer.I.FlyItem("ice", st.SpotWorld(best), StackTop, () => Arrive("ice"));
                actT = GameConfig.CD_ICE; return;
            }
        }
        // 2. 제빙기에 넣기
        if (G.CarryTop == "ice" && WorldMap.NearStn(pos, "melter", GameConfig.R_MELT))
        {
            PopTop();
            ArcFlyer.I.FlyItem("ice", StackTop(), st.MeltTop, () => { S.meltQ++; });
            AudioManager.I.Play("put", 1f + Random.Range(-0.05f, 0.05f));
            actT = GameConfig.CD_MELT; return;
        }
        // 3. 병 받기
        if (S.bottles > 0 && G.CanCarry("bottle") && WorldMap.NearStn(pos, "bottler", GameConfig.R_BOTTLE))
        {
            S.bottles--; G.pend++; G.pendK = "bottle";
            ArcFlyer.I.FlyItem("bottle", st.TrayTop(), StackTop, () => Arrive("bottle"));
            actT = GameConfig.CD_BOTTLE; return;
        }
        // 4. 선반 올리기 (카운터 뒤쪽 p.y < counter.y)
        if (G.CarryTop == "bottle" && S.shelf + G.shelfPend < G.ShelfCap && pos.y < WorldMap.STN["counter"].y && WorldMap.NearStn(pos, "counter", GameConfig.R_SHELF))
        {
            PopTop(); G.shelfPend++;
            ArcFlyer.I.FlyItem("bottle", StackTop(), st.ShelfTop, () => { G.shelfPend--; S.shelf++; if (S.tut == 2) TutorialManager.I?.Advance(3); });
            AudioManager.I.Play("put", 1.2f);
            actT = GameConfig.CD_SHELF; return;
        }
        // 5. 지폐 수거
        if (S.cash > 0 && !G.collectingCash && WorldMap.NearStn(pos, "register", GameConfig.R_CASH))
        {
            StartCoroutine(CollectCash());
            actT = GameConfig.CD_CASH; return;
        }
    }

    void PopTop() { S.p.carry.RemoveAt(S.p.carry.Count - 1); }

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
        int n = Stations.Bills(amount); float per = amount / (float)n; var st = Stations.I;
        bool big = amount >= G.BottlePrice * 8;
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            int layer = (n - 1 - i) / 3, col = (n - 1 - i) % 3;
            Vector3 from = st.cashAnchor.position + new Vector3((col - 1) * 16 * GameConfig.K, (layer + 0.5f) * Items.ITEM_H["bill"] * GameConfig.K, 0);
            var go = Items.Slab(ArcFlyer.I.transform, "bill");
            ArcFlyer.I.Fly(go, from, () => body.position + Vector3.up * 1.4f, 0.22f, 30f, () =>
            {
                G.AddBells(per);
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

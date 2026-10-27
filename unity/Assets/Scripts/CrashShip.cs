using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 고장 난 우주선 불시착(화성 이벤트 하나, docs/chapter3-design.md "우주 연출"). 글자 없이 화면 연출로만 읽힌다.
//  · 조건: 3장 이후(S.stage ≥ 10, 우주항 열림)이고 플레이어가 기지 동쪽(1번 도로·우주항·협곡)에 있을 때. MarsEvents가 다른 이벤트처럼 뽑는다.
//  · 떨어짐(3초): 북서쪽 하늘에서 작은 착륙선(KayKit lander_A)이 비스듬히 내려오며 검은 연기 꼬리를 끌고, 엔진 불꽃이 콜록콜록 꺼졌다 켜진다.
//    플레이어 근처 빈터(걷는 땅·시설·발판·화물선 착륙장을 피해 화면 안)에 기우뚱 내려앉는다(흔들림·흙먼지).
//  · 기다림: 엔진에서 가는 연기가 오르고 꼭대기 표시등이 빨갛게 천천히 숨 쉰다. 머리 위 [아이콘] 숫자 칩 = 수리에 필요한 물통·연료통(연료 공장 전엔 산소통).
//    가까이 가면 등짐에서 하나씩 날아가 칩 숫자가 준다(화물선 주문과 같은 문법). 들고 있으면 힌트 화살표가 이쪽을 가리킨다(MarsEvents.PickupTarget).
//  · 수리 → 이륙: 다 채우면 용접 불티가 튀고(작은 불티, 화면 번쩍임 없음) 기체가 바로 서며 표시등이 초록, 정착민 몇 명이 내려 길로 걸어가고 벨,
//    역추진 불꽃과 먼지 고리 속에 북동쪽 하늘로 떠난다. 정착민은 S.arrivals에 더한다(화물선이 데려온 정착민과 같은 누계).
//  · 기다려도 안 고치면(LIFE초) 연기를 뿜으며 혼자 힘겹게 떠난다(보상 없음).
// 진행 중 상태는 저장하지 않는다(MarsEvents와 같음). 첫 등장 때만 관제사 무전 한 줄(StoryManager "ev_crash").
// 검증: -event crash [초] (조건 무시) · -evcarry(내려앉을 때 필요한 물건을 등짐에 바로 실어 봇이 배달하게)
public class CrashShip : MonoBehaviour
{
    public static CrashShip I;
    GameState G => GameState.I;
    SaveData S => G.S;

    enum Phase { None, Falling, Waiting, Repair, Leaving }
    Phase phase;
    public bool Active => phase != Phase.None;

    const float LIFE = 210f, R_GIVE = 72f, FALL_T = 3.0f;
    const float SHIP_S = 44f;   // lander_A 배율(지름 ≈ 59, 플레이어보다 확실히 크게. 30이면 작은 로봇처럼 보였다)

    Vector2 pos;
    Transform root, ship, tilt, flame, beaconT;
    GameObject shadow;
    Material beaconMat;
    readonly List<Material> owned = new List<Material>();
    string[] kinds; int[] need, got, pend; WorldLabels.Label[] chips;
    float waitT, smokeT;
    bool qaLog;
    int reward, settlers;

    static readonly Color SMOKE_D = GameConfig.Hex("4a3c38"), SMOKE_L = GameConfig.Hex("6e605a");
    static readonly Color BEACON_R = GameConfig.Hex("ff4a32"), BEACON_G = GameConfig.Hex("6fe08a");

    void Awake() { I = this; }
    void Start() { qaLog = DevArgs.Has("-shot"); }
    void OnDestroy()
    {
        foreach (var m in owned) if (m != null) Destroy(m);
        ClearChips();
        if (I == this) I = null;
    }

    /// 이번 판정에 뽑을 수 있나(3장 이후, 우주항 열림, 플레이어가 기지 동쪽)
    public static bool Eligible
    {
        get
        {
            var g = GameState.I; if (g == null || I == null || I.Active) return false;
            if (g.S.stage < 10 || SpacePort.I == null || !SpacePort.I.PortOpen) return false;
            return Player.I != null && Player.I.pos.x > Regions.EAST_X + 40f;
        }
    }

    /// 힌트: 수리에 필요한 물건을 들고 있으면 우주선 남쪽(상호작용 반경 안)
    public bool HintTarget(out Vector2 p)
    {
        p = pos + new Vector2(0, 40f);
        if (phase != Phase.Waiting) return false;
        for (int c = 0; c < kinds.Length; c++) if (need[c] - got[c] - pend[c] > 0 && G.Holds(kinds[c])) return true;
        return false;
    }

    // ================= 시작 =================
    public void Begin()
    {
        if (Active) return;
        var me = Player.I != null ? Player.I.pos : WorldMap.PLAYER_START;
        if (!FindSpot(me, out pos)) { Debug.Log("event: crash 내려앉을 빈터 없음(취소)"); return; }
        // 수리 주문: 물통 + (연료 공장이 있으면 연료통, 없으면 산소통). 진행할수록 조금씩 많아진다
        var sp = SpacePort.I;
        string second = sp != null && sp.FuelBuilt ? "fuel" : G.KindOpen("o2") ? "o2" : null;
        int w = 5 + Mathf.Min(4, S.orders / 6), x = 3 + Mathf.Min(3, S.launches / 2);
        if (second == null) { kinds = new[] { "bottle" }; need = new[] { w + 2 }; }
        else { kinds = new[] { "bottle", second }; need = new[] { w, x }; }
        got = new int[kinds.Length]; pend = new int[kinds.Length];
        float v = 0; for (int c = 0; c < kinds.Length; c++) v += need[c] * G.PriceOf(kinds[c]);
        reward = Mathf.Max(50, Mathf.RoundToInt(v * 3f / 50f) * 50);
        settlers = 3 + (S.orders >= 12 ? 1 : 0);
        waitT = 0; phase = Phase.Falling;
        StartCoroutine(CoFall());
        Debug.Log("event: crash 시작 자리=" + pos.ToString("0") + " 수리=" + string.Join(",", kinds) + " " + string.Join(",", System.Array.ConvertAll(need, n => n.ToString())) + " 보상=" + reward + " 정착민=" + settlers);
    }

    // 플레이어 둘레에서 화면 안 빈터 찾기: 걸을 수 있는 땅(시설 발자국 밖)이고, 보이는 발판·화물선 착륙장과 떨어져 있고, 플레이어와도 조금 떨어진 곳.
    // 화면 위쪽(북쪽)·플레이어에서 120 안팎을 좋아한다(아래쪽은 업그레이드 버튼에 가린다)
    public static bool FindSpot(Vector2 me, out Vector2 best)
    {
        best = me; float bs = float.MaxValue;
        var g = GameState.I; var rig = CameraRig.I;
        float halfW = rig != null ? 210f / rig.Zoom : 210f, viewH = rig != null ? rig.ViewH : 700f;
        for (float dx = -halfW + 40f; dx <= halfW - 40f; dx += 12f)
            for (float dy = -viewH * 0.36f; dy <= viewH * 0.2f; dy += 12f)
            {
                var p = new Vector2(me.x + dx, me.y + dy);
                float d = Vector2.Distance(p, me);
                if (d < 80f) continue;
                if (!Clear(g, p, 36f)) continue;
                float score = Mathf.Abs(d - 125f) + (dy > 0 ? dy * 1.2f : 0f) + Mathf.Abs(dx) * 0.15f;
                if (score < bs) { bs = score; best = p; }
            }
        return bs < float.MaxValue;
    }
    static bool Clear(GameState g, Vector2 p, float r)
    {
        if (WorldMap.Blocked(p.x, p.y)) return false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            if (WorldMap.Blocked(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r)) return false;
        }
        // 길 위는 피한다(정착민·봇이 지나는 크림 길에 우주선이 서 있으면 어색하다)
        if (p.x > Regions.EAST_X && Mathf.Abs(p.y - Regions.ROAD_Y) < 20f + r) return false;
        foreach (var pd in SpacePort.PADS) if (Vector2.Distance(pd, p) < 72f) return false;
        if (g != null) foreach (var d in Pads.ALL) if (!d.menu && g.PadReq(d.id) && !g.PadMaxed(d.id) && Vector2.Distance(d.pos, p) < 48f) return false;
        return true;
    }

    // ================= 모형 =================
    static Mesh ShipMesh() => ToyMesh.Cached("evCrashShip", m =>
    {
        m.At(0, 0, 0, 160).Kk("lander_A", SHIP_S);
        return m;
    });
    static Mesh sparkMesh, flameBall;
    static Mesh SparkMesh() => sparkMesh != null ? sparkMesh : (sparkMesh = ToyMesh.Cached("evSpark", m => m.Ball(1f, GameConfig.Hex("ffb04a"), GameConfig.Hex("fff0b8"), 8, 3)));

    void Build()
    {
        root = new GameObject("crashShip").transform; root.SetParent(transform, false); root.position = WorldMap.W2U(pos);
        shadow = Prim.BlobShadow(root, 62); shadow.transform.localScale = Vector3.zero;
        ship = new GameObject("ship").transform; ship.SetParent(root, false);
        tilt = new GameObject("tilt").transform; tilt.SetParent(ship, false);
        RegionDress.Put(tilt, "body", ShipMesh(), 0.25f);
        // 꼭대기 표시등(제 색 발광, 숨 쉬듯 천천히. 빛 오브젝트 없음 → 화면 번쩍임 없음)
        beaconMat = new Material(SpaceSky.GlowMat()) { color = BEACON_R }; owned.Add(beaconMat);   // 조명 없는 제 색(밤에도 빛남)
        var bk = Prim.Sphere(tilt, "beacon", new Vector3(0, SHIP_S * 1.2f + 3f, 0) * GameConfig.K, 6.5f, BEACON_R);
        bk.GetComponent<Renderer>().sharedMaterial = beaconMat; Prim.NoShadow(bk); beaconT = bk.transform;
        // 엔진 불꽃(화물선과 같은 원뿔 불꽃을 작게)
        flame = new GameObject("flame").transform; flame.SetParent(tilt, false); flame.localPosition = new Vector3(0, 3f * GameConfig.K, 0);
        var fl = RegionDress.Put(flame, "cone", RegionDress.Flame(), 0f, 1f); Prim.NoShadow(fl);
        flame.localScale = Vector3.zero;
    }

    // ================= 떨어짐 =================
    IEnumerator CoFall()
    {
        Build();
        // 북서쪽 높은 하늘 → 낮게 깔려 미끄러지듯 → 빈터. 진행 방향으로 기수가 숙고 좌우로 흔들린다
        Vector3 a = new Vector3(-250f, 380f, 150f), mid = new Vector3(-70f, 70f, 40f), b = Vector3.zero;   // (동 x, 높이, 북 z) 논리
        float el = 0, puffT = 0, coughT = 0; bool lit = true;
        AudioManager.I?.Play("whoosh", 0.5f, 0.55f);
        while (el < FALL_T)
        {
            float dt = Time.deltaTime; el += dt;
            float k = Mathf.Clamp01(el / FALL_T), e = 1f - (1f - k) * (1f - k) * (1f - k) * 0.6f - 0.4f * (1f - k) * (1f - k);
            e = Mathf.Clamp01(e);
            Vector3 q = Bez(a, mid, b, e), q2 = Bez(a, mid, b, Mathf.Min(1f, e + 0.02f));
            ship.localPosition = new Vector3(q.x, q.y, q.z) * GameConfig.K;
            var dir = q2 - q; float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            ship.localRotation = Quaternion.Euler(0, yaw, 0);
            tilt.localRotation = Quaternion.Euler(22f * (1f - k) + 6f, 0, Mathf.Sin(el * 5.3f) * 9f * (1f - 0.6f * k));
            shadow.transform.localScale = new Vector3(62, 0.2f, 62) * GameConfig.K * Mathf.Lerp(0.15f, 1f, k * k);
            // 엔진이 콜록거린다: 켜졌다 꺼졌다(작은 불꽃만, 빛 없음)
            coughT -= dt;
            if (coughT <= 0f) { lit = !lit; coughT = lit ? Random.Range(0.12f, 0.3f) : Random.Range(0.05f, 0.14f); if (!lit) AudioManager.I?.Play("sputter", Random.Range(0.85f, 1.1f), 0.5f); }
            flame.localScale = Vector3.one * (lit ? Random.Range(0.35f, 0.55f) : 0.05f);
            // 검은 연기 꼬리(공중에 남아 커지며 옅어짐)
            puffT -= dt;
            if (puffT <= 0f) { puffT = 0.06f; StartCoroutine(CoPuff(tilt.position + Vector3.up * 0.6f + Random.insideUnitSphere * 0.25f, SMOKE_D, 0.5f, Random.Range(8f, 12f), Random.Range(24f, 34f), 1.6f, 10f)); }
            yield return null;
        }
        ship.localPosition = Vector3.zero;
        flame.localScale = Vector3.zero;
        // 착지: 기우뚱 박힌 채(기수 숙임 6°, 옆으로 10°) + 흔들림 + 흙먼지 + 옅은 모래 고리
        tilt.localRotation = Quaternion.Euler(6f, 0, -10f);
        JuiceFX.I.Squash(ship, 1.1f);
        JuiceFX.I.Shake(2.4f);
        JuiceFX.I.Dust(WorldMap.W2U(pos, 2f), 12);
        JuiceFX.I.Ring(WorldMap.W2U(pos, 0.5f), GameConfig.Hex("e0b48a"), 34f);
        JuiceFX.I.Vibrate(25);
        AudioManager.I?.Play("put", 0.5f, 0.9f); AudioManager.I?.Play("pop", 0.45f, 0.6f); AudioManager.I?.Play("sputter", 0.7f, 0.6f);
        if (qaLog) Debug.Log("event: crash 착지 " + pos.ToString("0"));
        yield return new WaitForSeconds(0.5f);
        // 수리 칩: [아이콘] 남은 수
        chips = new WorldLabels.Label[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            float off = (i - (kinds.Length - 1) / 2f) * 54f;
            chips[i] = WorldLabels.I.Add(() => root != null ? root.position + Vector3.up * 3.4f : Vector3.zero, "", 17, GameConfig.NAVY, new Vector2(off, 0), false);
            chips[i].SetIcon(kinds[i]);
        }
        phase = Phase.Waiting;
        SyncChips();
        HUD.I?.IconToast("ship");
        StoryManager.I?.Push("ev_crash", 0.8f);
        if (DevArgs.Has("-evcarry")) StartCoroutine(CoDevBring());
    }

    // 검증(-evcarry): 기다리는 모습을 2.5초 찍은 뒤 필요한 물건을 등짐에 싣고 플레이어를 우주선 남쪽에 세운다(-walk 0,0으로 서 있어도 배달되게)
    IEnumerator CoDevBring()
    {
        yield return new WaitForSeconds(2.5f);
        for (int c = 0; c < kinds.Length; c++) for (int n = 0; n < need[c] + 2; n++) if (G.Carry < G.CarryMax) G.AddCarry(kinds[c]);
        if (Player.I != null) Player.I.pos = pos + new Vector2(-34f, 58f);
        Debug.Log("event: crash 검증 배달 준비(등짐 " + G.Carry + ")");
    }

    static Vector3 Bez(Vector3 a, Vector3 c, Vector3 b, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;

    // 연기 한 덩이: 공중에 남아 커지며 옅어지고 조금 오른다
    IEnumerator CoPuff(Vector3 at, Color col, float a0, float s0, float s1, float life, float riseL)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>()); go.name = "smoke";
        go.transform.SetParent(transform, false);
        var c = col; c.a = a0; var m = Mats.Trans(c); go.GetComponent<Renderer>().sharedMaterial = m; Prim.NoShadow(go);
        float el = 0; var p0 = at;
        float dim = Mathf.Lerp(1f, 0.55f, DayNight.I != null ? DayNight.I.night : 0f);
        while (el < life && go != null)
        {
            el += Time.deltaTime; float k = el / life;
            go.transform.position = p0 + Vector3.up * riseL * GameConfig.K * k;
            go.transform.localScale = Vector3.one * Mathf.Lerp(s0, s1, 1f - (1f - k) * (1f - k)) * GameConfig.K;
            var cc = col * dim; cc.a = a0 * (1f - k) * Mathf.Clamp01(el * 10f); m.color = cc;
            yield return null;
        }
        Destroy(m); if (go != null) Destroy(go);
    }

    // ================= 기다림·배달 =================
    void Update()
    {
        if (G == null || phase == Phase.None) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        if (beaconMat != null && phase != Phase.Leaving)
        {
            float br = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f));
            var bc = phase == Phase.Waiting ? BEACON_R : BEACON_G;
            var col = bc * br; col.a = 1f; beaconMat.color = col;
        }
        if (phase != Phase.Waiting) return;
        waitT += dt;
        // 엔진에서 가는 연기(드문드문 오른다)
        smokeT -= dt;
        if (smokeT <= 0f) { smokeT = Random.Range(0.35f, 0.5f); StartCoroutine(CoPuff(tilt.TransformPoint(new Vector3(-8f, 14f, 6f) * GameConfig.K), SMOKE_L, 0.55f, 8f, 24f, 2.6f, 40f)); }
        if (waitT >= LIFE) { phase = Phase.Leaving; ClearChips(); StartCoroutine(CoLeave(false)); return; }
        Deliver();
    }

    void Deliver()
    {
        var pl = Player.I; if (pl == null || pl.actT > 0f) return;
        if ((pl.pos - pos).sqrMagnitude > R_GIVE * R_GIVE) return;
        for (int c = 0; c < kinds.Length; c++)
        {
            string k = kinds[c];
            if (got[c] + pend[c] >= need[c] || !G.Holds(k)) continue;
            int cc = c; pend[c]++;
            var from = pl.TakeOut(k);
            ArcFlyer.I.Fly(pl.FlyBlock(k), from, () => root != null ? root.position + Vector3.up * 1.4f : from, GameConfig.FLY_T, GameConfig.FLY_LIFT, () => Arrive(cc));
            AudioManager.I?.Play("put", 1.05f + Random.Range(-0.05f, 0.05f));
            pl.actT = 0.07f;
            return;
        }
    }

    void Arrive(int c)
    {
        pend[c]--; got[c]++;
        if (phase != Phase.Waiting) return;
        // 받을 때마다 작은 불티 몇 알(수리 중이라는 신호)
        if (tilt != null) Sparks(tilt.TransformPoint(new Vector3(Random.Range(-12f, 12f), Random.Range(14f, 30f), -18f) * GameConfig.K), 3);
        AudioManager.I?.PlayHz("pick", 560 + 40 * Total(got), 520, 0.6f);
        SyncChips();
        bool done = true; for (int i = 0; i < kinds.Length; i++) if (got[i] < need[i]) done = false;
        if (done) { phase = Phase.Repair; StartCoroutine(CoRepair()); }
    }
    static int Total(int[] a) { int n = 0; foreach (var x in a) n += x; return n; }

    void SyncChips()
    {
        if (chips == null) return;
        int nShow = 0; for (int i = 0; i < kinds.Length; i++) if (need[i] - got[i] > 0) nShow++;
        int j = 0;
        for (int i = 0; i < kinds.Length; i++)
        {
            int left = need[i] - got[i]; bool show = left > 0 && phase == Phase.Waiting;
            chips[i].Show(show);
            if (show) { chips[i].Set(left.ToString()); chips[i].offset = new Vector2((j - (nShow - 1) / 2f) * 54f, 0); j++; }
        }
    }
    void ClearChips()
    {
        if (chips == null) return;
        foreach (var l in chips) if (l != null && WorldLabels.I != null) WorldLabels.I.Remove(l);
        chips = null;
    }

    // ================= 수리 → 이륙 =================
    IEnumerator CoRepair()
    {
        ClearChips();
        if (qaLog) Debug.Log("event: crash 수리 시작");
        // 용접 불티: 선체 세 군데에서 번갈아 작은 불티가 튄다(1.6초)
        Vector3[] spots = { new Vector3(-13f, 18f, -16f), new Vector3(14f, 26f, -13f), new Vector3(0f, 9f, -20f) };
        float el = 0, sparkT = 0;
        while (el < 1.6f)
        {
            el += Time.deltaTime; sparkT -= Time.deltaTime;
            if (sparkT <= 0f)
            {
                sparkT = 0.09f;
                Sparks(tilt.TransformPoint(spots[Random.Range(0, spots.Length)] * GameConfig.K), 6);
                AudioManager.I?.Play("weld", Random.Range(0.9f, 1.15f), 0.45f);
            }
            yield return null;
        }
        // 기체가 바로 서고(통 튀며) 표시등 초록
        AudioManager.I?.Play("ding", 1.2f, 0.7f);
        var r0 = tilt.localRotation; el = 0;
        while (el < 0.45f) { el += Time.deltaTime; tilt.localRotation = Quaternion.Slerp(r0, Quaternion.identity, JuiceFX.EaseOutBack(Mathf.Clamp01(el / 0.45f))); yield return null; }
        tilt.localRotation = Quaternion.identity;
        JuiceFX.I.Squash(ship, 0.8f);
        // 정착민이 내려 길 쪽으로 걸어간다 + 벨
        S.arrivals += settlers;
        for (int k = 0; k < settlers; k++) StartCoroutine(CoWalker(k, settlers));
        GiveBells(reward, root.position + Vector3.up * 1.8f);
        if (qaLog) Debug.Log("event: crash 수리 완료 +" + reward + "벨 정착민 +" + settlers + " → " + S.arrivals);
        G.Save();
        yield return new WaitForSeconds(1.6f);
        phase = Phase.Leaving;
        yield return CoLeave(true);
    }

    // 이륙: 고쳤으면 힘찬 불꽃으로 곧게 솟아 북동쪽으로, 못 고쳤으면 콜록거리며 연기를 끌고 비틀비틀
    IEnumerator CoLeave(bool fixedUp)
    {
        if (!fixedUp && qaLog) Debug.Log("event: crash 시간 초과로 혼자 떠남");
        AudioManager.I?.Play("whoosh", fixedUp ? 0.7f : 0.5f, 0.6f);
        if (fixedUp) { AudioManager.I?.Play("up", 1.1f, 0.6f); JuiceFX.I.Ring(WorldMap.W2U(pos, 0.5f), Color.white, 30f); }
        JuiceFX.I.Dust(WorldMap.W2U(pos, 2f), 8);
        float el = 0, puffT = 0, dur = fixedUp ? 2.6f : 3.4f; var r0 = tilt.localRotation;
        while (el < dur && ship != null)
        {
            float dt = Time.deltaTime; el += dt; float k = Mathf.Clamp01(el / dur);
            float h = (fixedUp ? 420f : 360f) * k * k, dx = 160f * k * k * k, dn = 90f * k * k * k;
            ship.localPosition = new Vector3(dx, h, dn) * GameConfig.K;
            if (fixedUp) flame.localScale = Vector3.one * Mathf.Clamp01(el * 3f) * (0.9f + Mathf.Sin(el * 40f) * 0.08f);
            else
            {
                flame.localScale = Vector3.one * (Mathf.Sin(el * 23f) > 0.2f ? 0.45f : 0.05f);
                tilt.localRotation = r0 * Quaternion.Euler(Mathf.Sin(el * 6f) * 6f, 0, Mathf.Cos(el * 5f) * 6f);
            }
            shadow.transform.localScale = new Vector3(62, 0.2f, 62) * GameConfig.K * Mathf.Lerp(1f, 0.1f, k);
            puffT -= dt;
            if (puffT <= 0f)
            {
                puffT = fixedUp ? 0.07f : 0.05f;
                if (fixedUp && el < 1.2f) StartCoroutine(CoPuff(ship.position, Color.white * 0.92f, 0.35f, 6f, 16f, 1.0f, 4f));
                else if (!fixedUp) StartCoroutine(CoPuff(ship.position + Vector3.up * 0.4f, SMOKE_D, 0.5f, 5f, 14f, 1.2f, 6f));
            }
            yield return null;
        }
        if (root != null) Destroy(root.gameObject);
        root = null; ship = tilt = flame = beaconT = null; shadow = null;
        if (beaconMat != null) { owned.Remove(beaconMat); Destroy(beaconMat); beaconMat = null; }
        phase = Phase.None;
        if (qaLog) Debug.Log("event: crash 끝");
    }

    // 작은 불티: 사방으로 튀어 떨어지며 줄어든다(제 색 발광 알갱이, 빛 없음)
    void Sparks(Vector3 at, int n) { for (int i = 0; i < n; i++) StartCoroutine(CoSpark(at)); }
    IEnumerator CoSpark(Vector3 at)
    {
        var go = ToyMesh.Put(transform, "spark", SparkMesh(), SpaceSky.GlowMat(), Vector3.zero); Prim.NoShadow(go);
        var v = new Vector3(Random.Range(-1f, 1f), Random.Range(0.7f, 1.5f), Random.Range(-1.2f, 0.2f)) * 52f * GameConfig.K;
        var p = at; float el = 0, life = Random.Range(0.3f, 0.48f);
        while (el < life)
        {
            float dt = Time.deltaTime; el += dt;
            v += Vector3.down * 160f * GameConfig.K * dt; p += v * dt;
            go.transform.position = p;
            go.transform.localScale = Vector3.one * 2.4f * (1f - 0.7f * el / life);   // 메시가 이미 논리×K
            yield return null;
        }
        Destroy(go);
    }

    // 정착민: 우주선 남쪽으로 내려 → 길(동쪽 지역이면 크림 길)까지 → 서쪽으로 걷다 작아지며 사라짐(SpacePort 화물선 정착민과 같은 모습)
    static readonly Color[] PAL = { GameConfig.Hex("e48a55"), GameConfig.Hex("e6b552"), GameConfig.Hex("82b0cf"), GameConfig.Hex("a4b384") };
    IEnumerator CoWalker(int k, int n)
    {
        yield return new WaitForSeconds(0.3f * k);
        var view = new GameObject("crashSettler").transform; view.SetParent(transform, false);
        var body = new GameObject("body").transform; body.SetParent(view, false);
        CharacterKit.BuildCustomer(body, PAL[(k + 1) % PAL.Length], (k * 2 + 1) % 6, false, out _);
        Prim.BlobShadow(view, 20);
        var rig = body.GetComponent<CharRig>();
        float side = (k - (n - 1) / 2f) * 10f;
        Vector2 p = pos + new Vector2(side * 0.5f, 18f);
        float roadY = Mathf.Abs(pos.y - Regions.ROAD_Y) < 260f ? Regions.ROAD_Y + (k % 2 == 0 ? -8f : 8f) : pos.y + 70f;
        var pts = new List<Vector2> { pos + new Vector2(side, 40f), new Vector2(pos.x + side, roadY), new Vector2(pos.x + side - 150f, roadY) };
        view.position = WorldMap.W2U(p);
        JuiceFX.I.Pop(view, 0.2f, 0.3f);
        AudioManager.I?.Play("hop", 1.2f + 0.1f * k, 0.5f);
        float speed = 70f + k * 5f;
        foreach (var q in pts)
        {
            int guard = 0;
            while ((q - p).sqrMagnitude > 4f && guard++ < 600)
            {
                float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
                var d = (q - p).normalized; p = Vector2.MoveTowards(p, q, speed * dt);
                view.position = WorldMap.W2U(p);
                view.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, -d.y), Vector3.up);
                float s = Mathf.Sin(Time.time * 11f + k);
                body.localPosition = new Vector3(0, Mathf.Abs(s) * 2.5f * GameConfig.K, 0);
                if (rig != null) CharacterKit.Swing(rig, s, 40f, dt);
                yield return null;
            }
        }
        float el = 0; var sc0 = view.localScale;
        while (el < 0.35f) { el += Time.deltaTime; view.localScale = sc0 * Mathf.Max(0.001f, 1f - el / 0.35f); yield return null; }
        Destroy(view.gameObject);
    }

    void GiveBells(int n, Vector3 from)
    {
        G.AddBells(n, "event");
        JuiceFX.I.CoinPop(from, 10);
        var cam = Camera.main;
        if (cam != null && HUD.I != null) HUD.I.Coins(cam.WorldToScreenPoint(from), () => HUD.I.BellsScreen(), 6);
        if (WorldLabels.BigMoney(n)) WorldLabels.I.Float(from + Vector3.up * 0.6f, "+" + n.ToString("N0"), true, true);
        AudioManager.I?.Play("coin", 1.05f, 0.65f);
    }
}

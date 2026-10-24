using UnityEngine;

// §6.2 힌트: 바닥 화살표(타깃 70 초과로 멀 때, 플레이어 앞 30) + 타깃 링(r26 펄스). 우선순위는 hint() Phase 1 부분집합.
public class HintSystem : MonoBehaviour
{
    public static HintSystem I;
    GameState G => GameState.I;
    SaveData S => G.S;
    Transform arrow, ring; public Vector2 target; public string targetKey = "";
    float t;

    void Awake() { I = this; }

    void Start()
    {
        arrow = new GameObject("arrow").transform; arrow.SetParent(transform, false);
        var mf = arrow.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = ArrowMesh();
        var mr = arrow.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mats.Solid(GameConfig.GOLD);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        arrow.localScale = Vector3.one * 22 * GameConfig.K;
        ring = Prim.Ring(transform, Vector3.zero, 26, GameConfig.GOLD).transform;
    }

    static Mesh ArrowMesh()
    {
        // 평면 화살표: 꼬리 사각 + 머리 삼각, +z 방향
        var v = new Vector3[] { new Vector3(-0.25f, 0, -1), new Vector3(0.25f, 0, -1), new Vector3(0.25f, 0, 0.2f), new Vector3(-0.25f, 0, 0.2f), new Vector3(-0.7f, 0, 0.2f), new Vector3(0.7f, 0, 0.2f), new Vector3(0, 0, 1) };
        var tri = new int[] { 0, 2, 1, 0, 3, 2, 4, 6, 5 };
        var n = new Vector3[v.Length]; for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
        return new Mesh { vertices = v, triangles = tri, normals = n };
    }

    // 힌트 우선순위 (§6.2) Phase 1 부분집합
    Vector2 Pick()
    {
        var st = WorldMap.STN; int price = G.BottlePrice; string top = G.CarryTop;
        bool anyOpen = false; for (int i = 0; i < Stations.I.SpotCount; i++) if (Stations.I.SpotOpen(i)) anyOpen = true;
        // 타깃 점은 상호작용 반경 안쪽(스테이션 남쪽 가장자리 + 18)
        Vector2 P(string k) { var s = st[k]; targetKey = k; return new Vector2(s.x, s.y + s.h / 2 + 18); }
        Vector2 Cnt() { targetKey = "counter"; return new Vector2(st["counter"].x, st["counter"].y - st["counter"].h / 2 - 14); }
        Vector2 Ice()
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Stations.I.SpotCount; i++) if (Stations.I.SpotOpen(i)) { float d = Vector2.Distance(Player.I.pos, WorldMap.ICE_SPOTS[i]); if (d < bd) { bd = d; best = i; } }
            targetKey = "ice:" + best; return WorldMap.ICE_SPOTS[best];
        }
        bool iceFull = G.Carry + G.pend >= G.CarryMax;
        if (top == "ice" || G.pendK == "ice") return (iceFull || !anyOpen) ? P("melter") : Ice();   // 얼음 가득/밭 비었음→제빙기, 아니면 계속 줍기
        if (top == "bottle" && S.shelf + G.shelfPend < G.ShelfCap) return Cnt();
        if (top == "bottle") return G.HasCashier ? Cnt() : P("register");
        if (S.cash > 0 && (!G.HasCashier || S.cash > price * 4)) return P("register");
        if (S.bottles > 0 && G.Carry == 0 && S.shelf + G.shelfPend < G.ShelfCap && G.PadLv("runner") == 0) return P("bottler");   // 뽀삐가 있으면 운반은 뽀삐 몫
        if (CustomerSystem.I.AnyWaitingToPay() && !G.HasCashier) return P("register");
        // 토끼 팁 ≥ price*4 → 우리 팁 자리 (§6.2)
        if (S.penTip >= price * 4 && G.PadLv("pen") >= 1) { targetKey = "pen"; return WorldMap.PEN_TIP; }
        // 다음 발판: sold≥1 & bells≥남은 금액의 50%
        if (S.sold >= 1 && PadManager.I.visOrder.Count > 0)
        {
            string id = PadManager.I.visOrder[0];   // ORDER상 차례인 발판
            if (S.bells >= G.PadRemain(id) * 0.5f) { targetKey = "pad:" + id; return Pads.Get(id).pos; }
        }
        if (S.meltQ == 0 && anyOpen && G.Carry == 0) return Ice();
        if (S.bottles > 0) return P("bottler");
        if (anyOpen && G.Carry < G.CarryMax) return Ice();
        return P("bottler");
    }

    void Update()
    {
        var pl = Player.I; if (pl == null || Stations.I == null) return;
        t += Time.deltaTime;
        target = Pick();
        float d = Vector2.Distance(pl.pos, target);
        bool far = d > 70f;
        arrow.gameObject.SetActive(far);
        if (far)
        {
            Vector2 dir = (target - pl.pos).normalized;
            Vector2 ap = pl.pos + dir * 30f;
            arrow.position = WorldMap.W2U(ap, 30f) + Vector3.up * Mathf.Sin(t * 5f) * 0.1f;
            arrow.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, -dir.y), Vector3.up);
        }
        ring.position = WorldMap.W2U(target, 0.5f);
        ring.localScale = Vector3.one * 26 * GameConfig.K * (1 + Mathf.Sin(t * 6f) * 0.12f);
    }
}

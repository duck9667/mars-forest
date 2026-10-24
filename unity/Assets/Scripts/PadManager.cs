using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// §3 업그레이드 발판: 노출 규칙 visiblePads, 정지 납입 tickPads, 완료 completePad, 시트 일시불 buyPad
public class PadManager : MonoBehaviour
{
    public static PadManager I;
    GameState G => GameState.I;
    SaveData S => G.S;

    public class PadView
    {
        public PadDef def; public Transform root, disc, prog, ring; public WorldLabels.Label label; public bool shown; public float coinT;
    }
    public readonly Dictionary<string, PadView> views = new Dictionary<string, PadView>();
    public HashSet<string> visSet = new HashSet<string>();
    public List<string> visOrder = new List<string>();   // ORDER 순서 보존(봇·시트 정렬용)
    float visT; public float lastCompleteT = -10;
    public System.Action OnChanged;

    void Awake() { I = this; }

    void Start()
    {
        foreach (var d in Pads.ALL)
        {
            if (!d.active || d.menu) continue;
            var v = new PadView { def = d };
            v.root = new GameObject("pad_" + d.id).transform; v.root.SetParent(transform, false);
            v.root.position = WorldMap.W2U(d.pos);
            v.disc = Prim.Cyl(v.root, "disc", new Vector3(0, 0.03f, 0), 60, 3, GameConfig.Hex("fff4dc")).transform;
            Prim.NoShadow(v.disc.gameObject);
            v.prog = Prim.Cyl(v.root, "prog", new Vector3(0, 0.12f, 0), 52, 1.5f, GameConfig.NAVY).transform;
            Prim.NoShadow(v.prog.gameObject);
            v.ring = Prim.Ring(v.root, new Vector3(0, 0.2f, 0), 34, GameConfig.GOLD).transform;
            v.label = WorldLabels.I.Add(() => v.root.position + Vector3.up * 0.6f, "", 22, Color.white, new Vector2(0, 44), true, new Color(0.13f, 0.12f, 0.22f, 0.9f));
            v.root.gameObject.SetActive(false); v.label.Show(false);
            views[d.id] = v;
        }
        RefreshVis();
    }

    // §3.1 노출 규칙
    public void RefreshVis()
    {
        var seen = new Dictionary<string, int>();
        var next = new List<string>();
        foreach (var id in Pads.ORDER)
        {
            if (!GameState.ACTIVE_IDS.Contains(id)) continue;
            seen[id] = seen.TryGetValue(id, out var n) ? n + 1 : 1;
            var d = Pads.Get(id);
            if (d.menu || next.Contains(id)) continue;
            if (G.PadLv(id) < seen[id] && !G.PadMaxed(id) && G.PadReq(id)) { next.Add(id); if (next.Count >= GameConfig.VIS_MAX) break; }
        }
        if (next.Count < GameConfig.VIS_MAX)
        {
            foreach (var d in Pads.ALL.Where(p => p.active && !p.menu && !next.Contains(p.id) && !G.PadMaxed(p.id) && G.PadReq(p.id)).OrderBy(p => G.PadCost(p.id)))
            {
                next.Add(d.id); if (next.Count >= GameConfig.VIS_MAX) break;
            }
        }
        foreach (var d in Pads.ALL) if (d.active && !d.menu && G.PadProg(d.id) > 0 && !G.PadMaxed(d.id) && !next.Contains(d.id)) next.Add(d.id);
        var ns = new HashSet<string>(next);
        foreach (var kv in views)
        {
            bool show = ns.Contains(kv.Key); var v = kv.Value;
            if (show && !v.shown)
            {
                v.root.gameObject.SetActive(true); v.label.Show(true);
                JuiceFX.I.Pop(v.disc, 1.4f, 0.5f); JuiceFX.I.Ring(v.root.position, Color.white, 30);
            }
            else if (!show && v.shown) { v.root.gameObject.SetActive(false); v.label.Show(false); }
            v.shown = show;
        }
        visSet = ns; visOrder = next;
        OnChanged?.Invoke();
    }

    public PadView NearestVisible(Vector2 p)
    {
        PadView best = null; float bd = float.MaxValue;
        foreach (var id in visSet) { var v = views[id]; float d = Vector2.Distance(p, v.def.pos); if (d < bd) { bd = d; best = v; } }
        return best;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        visT += dt; if (visT >= GameConfig.VIS_REFRESH) { visT = 0; RefreshVis(); }
        var pl = Player.I; if (pl == null) return;
        foreach (var id in visSet)
        {
            var v = views[id]; float cost = G.PadCost(id), prog = G.PadProg(id);
            bool near = !pl.walking && Vector2.Distance(pl.pos, v.def.pos) < GameConfig.R_PAD;
            // §3.2 납입
            if (near && S.bells > 0 && prog < cost)
            {
                float take = Mathf.Min(S.bells, cost - prog, Mathf.Max(GameConfig.PAD_MIN_TAKE, cost * dt * GameConfig.PAD_RATE));
                S.bells -= take; prog += take; G.SetPadProg(id, prog); G.AddBells(0);
                v.coinT += dt;
                if (v.coinT >= GameConfig.PAD_COIN_GAP)
                {
                    v.coinT = 0;
                    var coin = Items.Slab(ArcFlyer.I.transform, "coin"); var target = v.root.position;
                    ArcFlyer.I.Fly(coin, pl.body.position + Vector3.up * 1.2f, () => target + Vector3.up * 0.15f, 0.22f, 20f);
                    AudioManager.I.PlayHz("tick", 480 + 700 * prog / cost, 480, 0.6f);
                }
                if (prog >= cost - 0.01f) { Complete(id); continue; }
            }
            // 시각: 진행 원판 sqrt(prog/cost), 살 수 있으면 금색 링 펄스
            float s = Mathf.Sqrt(Mathf.Clamp01(prog / cost));
            v.prog.localScale = new Vector3(52 * s, 0.75f, 52 * s) * GameConfig.K;
            bool afford = S.bells >= cost - prog;
            v.ring.gameObject.SetActive(afford);
            if (afford) v.ring.localScale = Vector3.one * 34 * GameConfig.K * (1 + Mathf.Sin(Time.time * 1000f / 250f) * 0.04f);
            v.label.Set(v.def.name + " Lv." + (G.PadLv(id) + 1) + "\n" + Mathf.CeilToInt(cost - prog) + " 벨");
        }
    }

    // §3.2 완료
    public void Complete(string id)
    {
        var d = Pads.Get(id); int lv = G.PadLv(id) + 1;
        G.SetPadLv(id, lv);
        lastCompleteT = Time.time;
        if (views.TryGetValue(id, out var v))
        {
            JuiceFX.I.Ring(v.root.position, GameConfig.GOLD, 30); JuiceFX.I.Ring(v.root.position, Color.white, 50);
            JuiceFX.I.Pop(v.disc, 1.3f, 0.4f);
            WorldLabels.I.Float(v.root.position + Vector3.up * 1f, d.name + " Lv." + lv, true, true);
        }
        JuiceFX.I.Flash(d.zone ? 0.5f : 0.3f); JuiceFX.I.Shake(d.zone ? 7 : 3); JuiceFX.I.Vibrate(20);
        AudioManager.I.Play("pad"); AudioManager.I.Play("up", 1f, 0.6f);
        RefreshVis();
        HUD.I?.Refresh();
    }

    // §3.3 시트 구매: 남은 금액 일시불
    public bool Buy(string id)
    {
        if (G.PadMaxed(id) || !G.PadReq(id)) return false;
        float rem = G.PadRemain(id);
        if (S.bells < rem) { AudioManager.I.Play("no"); return false; }
        S.bells -= rem; G.AddBells(0);
        Complete(id);
        return true;
    }
}

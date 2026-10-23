using System.Collections.Generic;
using UnityEngine;

// §5.1 뽀삐(runner) 최대 2: toBottler → load → toCounter → unload. 속도 95+25*runspd, 적재 0.18s/병 최대 8, 하역 0.15s/병(선반 가득이면 대기)
public class RunnerSystem : MonoBehaviour
{
    GameState G => GameState.I;
    SaveData S => G.S;

    class Runner
    {
        public enum St { ToBottler, Load, ToCounter, Unload }
        public St st = St.ToBottler; public Vector2 pos; public int carry; public float t, stepT;
        public Transform view, body, cart, stack; public int lastCarry = -1;
    }
    readonly List<Runner> runners = new List<Runner>();

    void Start()
    {
        G.OnPadLevel += (id, lv) => { if (id == "runner" || id == "runner2") Refresh(); };
        Refresh();
    }

    int Want => (G.PadLv("runner") >= 1 ? 1 : 0) + (G.PadLv("runner2") >= 1 ? 1 : 0);
    float Speed => GameConfig.RUNNER_BASE_SPEED + GameConfig.RUNNER_SPEED_PER_LV * G.PadLv("runspd");

    void Refresh()
    {
        while (runners.Count < Want)
        {
            var r = new Runner { pos = WorldMap.RunnerLoad + new Vector2(0, runners.Count * 24) };
            BuildView(r); runners.Add(r);
            JuiceFX.I.Pop(r.view, 0.001f, 0.6f); JuiceFX.I.Ring(r.view.position, GameConfig.GOLD, 30);
        }
    }

    void BuildView(Runner r)
    {
        float k = GameConfig.K; Color pink = GameConfig.Hex("f29bc8"), dark = GameConfig.Hex("3b3a52");
        r.view = new GameObject("runner").transform; r.view.SetParent(transform, false); r.view.position = WorldMap.W2U(r.pos);
        Prim.BlobShadow(r.view, 24);
        r.body = new GameObject("body").transform; r.body.SetParent(r.view, false);
        Prim.Box(r.body, "torso", new Vector3(0, 13 * k, 0), new Vector3(14, 18, 12), pink);
        var head = Prim.Box(r.body, "head", new Vector3(0, 29 * k, 0), new Vector3(13, 11, 11), pink);
        Prim.Box(head.transform, "eyeL", new Vector3(-3 * k, 1 * k, 5.5f * k), new Vector3(3, 3, 1), dark);
        Prim.Box(head.transform, "eyeR", new Vector3(3 * k, 1 * k, 5.5f * k), new Vector3(3, 3, 1), dark);
        Prim.Cyl(head.transform, "ant", new Vector3(0, 8 * k, 0), 1.5f, 7, dark);
        Prim.Cyl(r.body, "wheelL", new Vector3(-7 * k, 4 * k, 0), 8, 2, dark).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Prim.Cyl(r.body, "wheelR", new Vector3(7 * k, 4 * k, 0), 8, 2, dark).transform.localRotation = Quaternion.Euler(0, 0, 90);
        // 손수레: 뒤쪽에 끌고 다님
        r.cart = new GameObject("cart").transform; r.cart.SetParent(r.view, false); r.cart.localPosition = new Vector3(0, 0, -16 * k);
        Prim.Box(r.cart, "bed", new Vector3(0, 6 * k, 0), new Vector3(18, 2, 14), GameConfig.Hex("c9c3b6"));
        Prim.Box(r.cart, "bar", new Vector3(0, 7 * k, 7 * k), new Vector3(3, 2, 8), dark);
        Prim.Cyl(r.cart, "w1", new Vector3(-8 * k, 3.5f * k, -3 * k), 7, 2, dark).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Prim.Cyl(r.cart, "w2", new Vector3(8 * k, 3.5f * k, -3 * k), 7, 2, dark).transform.localRotation = Quaternion.Euler(0, 0, 90);
        r.stack = new GameObject("stack").transform; r.stack.SetParent(r.cart, false); r.stack.localPosition = new Vector3(0, 7 * k, 0);
    }

    static bool MoveTo(Runner r, Vector2 target, float speed, float dt)
    {
        Vector2 d = target - r.pos; float dist = d.magnitude;
        if (dist < 1.5f) { r.pos = target; return true; }
        r.pos += d / dist * Mathf.Min(dist, speed * dt);
        r.view.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, -d.y), Vector3.up);
        return false;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        for (int i = 0; i < runners.Count; i++)
        {
            var r = runners[i]; Vector2 offs = new Vector2(0, i * 24); // 두 번째 뽀삐는 한 칸 아래에서 작업
            bool moving = false;
            switch (r.st)
            {
                case Runner.St.ToBottler:
                    moving = !MoveTo(r, WorldMap.RunnerLoad + offs, Speed, dt);
                    if (!moving) { r.st = Runner.St.Load; r.t = 0; r.stepT = 0; r.view.rotation = Quaternion.LookRotation(new Vector3(1, 0, 0), Vector3.up); }
                    break;
                case Runner.St.Load:
                    r.t += dt; r.stepT += dt;
                    if (r.stepT >= GameConfig.RUNNER_LOAD_T && S.bottles > 0 && r.carry < GameConfig.RUNNER_CAP)
                    {
                        r.stepT = 0; S.bottles--; r.carry++;
                        int idx = r.carry - 1; var rr = r;
                        ArcFlyer.I.FlyItem("bottle", Stations.I.TrayTop(), () => rr.stack.position + Vector3.up * (idx / 4 + 0.5f) * Items.ITEM_H["bottle"] * GameConfig.K, null, 0.25f, 30f);
                        AudioManager.I.Play("put", 1.3f, 0.4f);
                    }
                    if (r.carry >= GameConfig.RUNNER_CAP || (r.carry > 0 && S.bottles == 0 && r.t > GameConfig.RUNNER_DEPART_T)) { r.st = Runner.St.ToCounter; }
                    break;
                case Runner.St.ToCounter:
                    moving = !MoveTo(r, WorldMap.RunnerUnload + offs, Speed, dt);
                    if (!moving) { r.st = Runner.St.Unload; r.stepT = 0; r.view.rotation = Quaternion.LookRotation(new Vector3(0, 0, -1), Vector3.up); }
                    break;
                case Runner.St.Unload:
                    r.stepT += dt;
                    if (r.stepT >= GameConfig.RUNNER_UNLOAD_T && r.carry > 0 && S.shelf + G.shelfPend < G.ShelfCap)
                    {
                        r.stepT = 0; r.carry--; G.shelfPend++;
                        ArcFlyer.I.FlyItem("bottle", r.stack.position + Vector3.up * 0.3f, Stations.I.ShelfTop, () => { G.shelfPend--; S.shelf++; }, 0.25f, 30f);
                        AudioManager.I.Play("put", 1.1f, 0.4f);
                    }
                    if (r.carry == 0) r.st = Runner.St.ToBottler;
                    break;
            }
            r.view.position = WorldMap.W2U(r.pos);
            float t = Time.time * 1000f;
            r.body.localPosition = new Vector3(0, moving ? Mathf.Abs(Mathf.Sin(t / 80f)) * 2f * GameConfig.K : Mathf.Sin(t / 500f) * 0.6f * GameConfig.K, 0);
            if (r.carry != r.lastCarry) { r.lastCarry = r.carry; Items.SyncStack(r.stack, "bottle", r.carry, 2, 2, 8, 7, GameConfig.RUNNER_CAP); }
        }
    }
}

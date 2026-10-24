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
        r.view = new GameObject("runner").transform; r.view.SetParent(transform, false); r.view.position = WorldMap.W2U(r.pos);
        Prim.BlobShadow(r.view, 24);
        r.body = new GameObject("body").transform; r.body.SetParent(r.view, false);
        // 분홍 로봇(원본 bRobot 골격) + 뒤에 끄는 손수레. r.stack 은 손수레 짐칸 윗면
        CharacterKit.BuildRunner(r.body, out _);
        r.cart = CharacterKit.BuildCart(r.view, out r.stack);
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
            if (moving) CharacterKit.SpinWheels(r.cart.GetComponent<CharRig>(), Speed, dt);   // 손수레 바퀴 굴림
            if (r.carry != r.lastCarry) { r.lastCarry = r.carry; Items.SyncStack(r.stack, "bottle", r.carry, 2, 2, 8, 7, GameConfig.RUNNER_CAP); }
        }
    }
}

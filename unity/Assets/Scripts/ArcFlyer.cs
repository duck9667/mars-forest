using System;
using System.Collections.Generic;
using UnityEngine;

// §7.4 아이템 아크 비행: ease-out quad, 높이 +sin(πk)*lift, y축 회전 8rad/s
public class ArcFlyer : MonoBehaviour
{
    public static ArcFlyer I;
    class Flight
    {
        public GameObject go; public Vector3 from; public Func<Vector3> to; public float t, dur, lift; public Action onArrive; public bool keep;
    }
    readonly List<Flight> flights = new List<Flight>();

    void Awake() { I = this; }

    // lift는 논리 단위. keep=true면 도착 후 오브젝트를 파괴하지 않고 onArrive에 맡긴다.
    public void Fly(GameObject go, Vector3 from, Func<Vector3> to, float dur = GameConfig.FLY_T, float lift = GameConfig.FLY_LIFT, Action onArrive = null, bool keep = false)
    {
        go.transform.SetParent(transform, true);
        go.transform.position = from;
        Prim.NoShadow(go);
        flights.Add(new Flight { go = go, from = from, to = to, dur = dur, lift = lift * GameConfig.K, onArrive = onArrive, keep = keep });
    }
    public void FlyItem(string kind, Vector3 from, Func<Vector3> to, Action onArrive, float dur = GameConfig.FLY_T, float lift = GameConfig.FLY_LIFT)
    {
        var go = Items.Slab(transform, kind);
        Fly(go, from, to, dur, lift, onArrive);
    }

    public int Active => flights.Count;

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            var f = flights[i];
            f.t += dt;
            float k = Mathf.Clamp01(f.t / f.dur);
            float e = 1 - (1 - k) * (1 - k);
            Vector3 p = Vector3.LerpUnclamped(f.from, f.to(), e);
            p.y += Mathf.Sin(Mathf.PI * k) * f.lift;
            if (f.go != null)
            {
                f.go.transform.position = p;
                f.go.transform.Rotate(0, 8f * Mathf.Rad2Deg * dt, 0, Space.World);
            }
            if (k >= 1)
            {
                flights.RemoveAt(i);
                if (!f.keep && f.go != null) Destroy(f.go);
                f.onArrive?.Invoke();
            }
        }
    }
}

using System.Collections;
using UnityEngine;

// §5.1 얼음 드릴: 설치 후 drillT 2.0s마다 얼음 2장(0.15s 차, 0.9s 아크, lift 140)을 meltQ<40일 때 제빙기로
public class DrillStation : MonoBehaviour
{
    GameState G => GameState.I;
    SaveData S => G.S;
    Transform bit; float t, drillT;

    void Start()
    {
        G.OnPadLevel += (id, lv) => { if (id == "drill") Install(true); };
        if (G.PadLv("drill") >= 1) Install(false);
    }

    void Install(bool fx)
    {
        var st = Stations.I; if (st == null || st.drillView == null) return;
        st.RefreshZoneGated();
        if (bit == null)
        {
            var d = WorldMap.STN["drill"];
            bit = Prim.Cyl(st.drillView, "bit", WorldMap.W2U(d.x, d.y, 80), 10, 40, GameConfig.Hex("5b6b8c")).transform;
            StationDress.DressDrill(st.drillView);
        }
        if (fx) { JuiceFX.I.Pop(st.drillView, 0.001f, 0.6f); JuiceFX.I.Dust(WorldMap.W2U(WorldMap.STN["drill"].x, WorldMap.STN["drill"].y, 2), 8); }
    }

    void Update()
    {
        if (!WorldMap.drillInstalled || Stations.I == null) return;
        float dt = Mathf.Min(Time.deltaTime, GameConfig.DT_MAX);
        t += dt;
        if (bit != null) { bit.Rotate(0, 720 * dt, 0); bit.position = WorldMap.W2U(WorldMap.STN["drill"].x, WorldMap.STN["drill"].y, 80 + Mathf.Sin(t * 6f) * 6f); }
        drillT += dt;
        if (drillT >= GameConfig.DRILL_PERIOD)
        {
            drillT = 0;
            if (S.meltQ < GameConfig.MELT_CAP) StartCoroutine(Fire());
        }
    }

    IEnumerator Fire()
    {
        var d = WorldMap.STN["drill"];
        for (int i = 0; i < 2; i++)
        {
            Vector3 from = WorldMap.W2U(d.x, d.y, 60);
            ArcFlyer.I.FlyItem("ice", from, Stations.I.MeltTop, () => { S.meltQ++; }, GameConfig.DRILL_FLY_T, GameConfig.DRILL_LIFT);
            JuiceFX.I.Dust(WorldMap.W2U(d.x, d.y, 4), 2);
            AudioManager.I.Play("pick", 0.7f, 0.35f);
            yield return new WaitForSeconds(GameConfig.DRILL_GAP);
        }
    }
}

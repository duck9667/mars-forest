using UnityEngine;

// §9 Kenney Space Kit 모델로 스테이션을 입힌다. 원본 index.html bMelter(585행)·bBottler(596행)·bDrill(623행)의 put 호출과 같은 배치.
// 규약: 각 view의 플레이스홀더 자식 "body"/"top"을 숨기고, 스테이션 중심 W2U(stn.x, stn.y, 0)에 둔 그룹 "models" 아래에 모델을 넣는다
// (view 자체는 월드 원점에 있고 JuiceFX가 view를 스쿼시한다).
public static class StationDress
{
    public static void Dress(Stations st)
    {
        Melter(st);
        Bottler(st);
        DressDrill(st.drillView);
    }

    // 해빙기(얼음 → 물): 왼쪽 호퍼에 얼음이 쌓이고(meltQ), 가운데 가열 통의 코일이 달아오르며 김이 나고, 오른쪽 유리 탱크에 물이 찬다(S.water).
    // 탱크에서 남쪽으로 파이프가 나가 병입기로 이어진다. 접근은 서쪽 통로(호퍼 쪽)
    static void Melter(Stations st)
    {
        var S = GameState.I.S; var m = WorldMap.STN["melter"];
        var g = MachineKit.Group(st.melterView, m, 96, 64, "core");
        var a = st.melterView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.meltQ > 0;
        MachineKit.Hopper(g, -26, 2, 34, 32, 10, 34, GameConfig.ICE);
        st.meltAnchor.position = WorldMap.W2U(m.x - 26, m.y + 2, 12);
        // 가열 통: 어두운 강철 원통 + 코일 띠 3줄(작동 중 주황 발광)
        Prim.Cyl(g, "heater", MachineKit.L(6, -4, 23), 28, 36, MachineKit.STEEL_D);
        Prim.Cyl(g, "heaterCap", MachineKit.L(6, -4, 42), 30, 4, MachineKit.SHELL);
        for (int i = 0; i < 3; i++)
        {
            var c = Prim.Cyl(g, "coil", MachineKit.L(6, -4, 14 + i * 9), 30.5f, 3, GameConfig.Hex("6a2a20"));
            a.AddGlow(c.GetComponent<Renderer>(), GameConfig.Hex("ff8a3d"), GameConfig.Hex("6a2a20"));
        }
        // 김: 가열 통 위로 오르는 작은 구슬
        a.bubbleBase = 44 * GameConfig.K; a.bubbleH = 22;
        for (int i = 0; i < 4; i++) { var b = Prim.Sphere(g, "steam", MachineKit.L(6 + (i % 2 == 0 ? -4 : 4), -4 + (i < 2 ? -3 : 3), 46), 3, GameConfig.Hex("f4f1ec")); Prim.NoShadow(b); a.bubbles.Add(b.transform); }
        // 물탱크(물 60 = 가득)
        MachineKit.Tank(g, 33, 6, 24, 6, 40, GameConfig.BOTTLE, out var liq);
        a.SetLiquid(liq); a.level = () => S.water / 60f;
        MachineKit.PipeX(g, 20, 21, 6, 24, 6, MachineKit.SHELL_D);
        MachineKit.PipeX(g, 18, 22, -2, 26, 5, MachineKit.SHELL_D);
        MachineKit.PipeY(g, 33, 18, 34, 6, 6, GameConfig.Hex("4f9fd0"));   // 남쪽 병입기로
        MachineKit.Lamp(g, -44, -26, 30, GameConfig.BOTTLE, a);
    }

    // 병입기(물 → 물통): 뒤쪽 유리 탱크의 물(S.water)이 충전 노즐로 내려가 앞 벨트 위 병을 채우고, 오른쪽 받침대에 물통이 쌓인다(S.bottles).
    static void Bottler(Stations st)
    {
        var S = GameState.I.S; var b = WorldMap.STN["bottler"];
        var g = MachineKit.Group(st.bottlerView, b, 96, 64);
        var a = st.bottlerView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.water >= 10 && S.bottles < GameConfig.BOTTLE_CAP;
        MachineKit.Tank(g, -30, -14, 26, 6, 38, GameConfig.BOTTLE, out var liq);
        a.SetLiquid(liq); a.level = () => S.water / 60f;
        MachineKit.PipeY(g, -30, -32, -27, 46, 6, GameConfig.Hex("4f9fd0"));   // 북쪽 해빙기에서 들어오는 물
        MachineKit.PipeUp(g, -30, -32, 46, 52, 6, GameConfig.Hex("4f9fd0"));
        // 충전 노즐: 탱크에서 벨트 위로 뻗은 팔
        Prim.Box(g, "fillPost", MachineKit.L(-14, 6, 18), new Vector3(8, 36, 8), MachineKit.SHELL_D);
        Prim.Box(g, "fillArm", MachineKit.L(-14, 14, 34), new Vector3(12, 8, 22), MachineKit.SHELL);
        Prim.Cyl(g, "nozzle", MachineKit.L(-14, 20, 27), 5, 8, GameConfig.Hex("4f9fd0"));
        // 벨트(앞쪽, 서→동)와 그 위를 지나가는 병 3개
        Prim.Box(g, "belt", MachineKit.L(0, 20, 9), new Vector3(88, 6, 14), MachineKit.STEEL_D);
        Prim.Box(g, "beltTop", MachineKit.L(0, 20, 12.2f), new Vector3(86, 0.6f, 12), GameConfig.Hex("2a2e36"));
        foreach (float x in new[] { -44f, 44f }) Prim.Cyl(g, "roller", MachineKit.L(x, 20, 9), 6, 14, MachineKit.SHELL_D).transform.localRotation = Quaternion.Euler(90, 0, 0);
        a.beltX0 = -40; a.beltX1 = 40;
        for (int i = 0; i < 3; i++)
        {
            var bt = new GameObject("beltBottle").transform; bt.SetParent(g, false); bt.localPosition = MachineKit.L(-40 + i * 27, 20, 13);
            Prim.Cyl(bt, "b", MachineKit.L(0, 0, 4), 6, 8, GameConfig.BOTTLE); Prim.Cyl(bt, "cap", MachineKit.L(0, 0, 8.6f), 3.6f, 1.4f, Color.white);
            a.belt.Add(bt);
        }
        // 산출 받침대(뒤 오른쪽): 물통이 6×4로 쌓인다
        float th = MachineKit.Table(g, 18, -12, 62, 36, 16, GameConfig.BOTTLE);
        st.trayAnchor.position = WorldMap.W2U(b.x + 18, b.y - 12, th);
        MachineKit.Lamp(g, 44, -28, 30, GameConfig.BOTTLE, a);
    }

    // 얼음 드릴: 바닥판 80×6×60 + 철골 데릭 2단·발전기·드럼통 묶음 (설치 전엔 view가 꺼져 있어 같이 숨는다)
    public static void DressDrill(Transform drillView)
    {
        if (drillView == null || drillView.Find("models") != null) return;
        var d = WorldMap.STN["drill"];
        var g = Group(drillView, d, 80, 60, 6);
        var re = ModelLib.SK(ModelLib.COL.orange);
        ModelLib.Put(g, "tower", 36, 44, 36, 0, 6, 0, 0f, re);
        ModelLib.Put(g, "tower", 30, 40, 30, 0, 50, 0, Mathf.PI / 4, re); // 철골 데릭 2단 (구멍 사이로 드릴이 보인다)
        ModelLib.Put(g, "gen", 34, 34, 34, -26, 6, 18, 0f, re);
        ModelLib.Put(g, "barrels", 34, 34, 34, 28, 6, -16, 0.4f, ModelLib.SK(ModelLib.COL.blue));
    }

    // 플레이스홀더 숨기고 스테이션 중심 그룹 + 원본의 steelD 바닥판(w×plateH×h)
    static Transform Group(Transform view, WorldMap.Stn s, float w, float h, float plateH = 5f)
    {
        foreach (Transform c in view) if (c.name == "body" || c.name == "top") c.gameObject.SetActive(false);
        var g = new GameObject("models").transform;
        g.SetParent(view, false);
        g.position = WorldMap.W2U(s.x, s.y, 0f);   // 월드 기준(view 피벗이 스테이션 중심)
        Prim.Box(g, "plate", new Vector3(0f, plateH / 2 * GameConfig.K, 0f), new Vector3(w, plateH, h), ModelLib.C(ModelLib.COL.steelD));
        return g;
    }
}

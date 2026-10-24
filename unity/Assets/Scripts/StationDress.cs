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

    // 해빙기(얼음 → 물): 왼쪽 호퍼에 얼음이 쌓이고(meltQ), 가운데 흰 가열 몸통의 앞 창이 주황으로 달아오르고, 파란 관 아치로 오른쪽 유리 탱크에 물이 찬다(S.water).
    // 병입기와 같은 문법(흰 몸통·주황 창·파란 관 아치·유리 탱크)이라 두 기계가 한 라인으로 읽힌다(목표 그림 docs/art/target_base_v1.png). 접근은 서쪽 통로(호퍼 쪽)
    static void Melter(Stations st)
    {
        var S = GameState.I.S; var m = WorldMap.STN["melter"];
        var g = MachineKit.Group(st.melterView, m, 96, 64, "core");
        var a = st.melterView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.meltQ > 0;
        MachineKit.Hopper(g, -26, 2, 34, 32, 10, 34, GameConfig.ICE);
        st.meltAnchor.position = WorldMap.W2U(m.x - 26, m.y + 2, 12);
        // 가열 몸통: 흰 상자 + 밝은 뚜껑, 앞면에 주황 발광 창(세로 막대 3)
        Body(g, 5, -3, 24, 26, 32);
        MachineKit.HeatWindow(g, 5, 10, 17, 18, 16, 3, a);
        // 물탱크(물 60 = 가득) + 안에서 오르는 기포(일할 때만)
        MachineKit.Tank(g, 31, -3, 26, 6, 40, GameConfig.BOTTLE, out var liq);
        a.SetLiquid(liq); a.level = () => 0.3f + 0.7f * S.water / 60f;   // 빈 유리관은 회색으로 읽혀 바닥 30%는 늘 물
        a.bubbleBase = 8 * GameConfig.K; a.bubbleH = 26;
        for (int i = 0; i < 4; i++) { var bb = Prim.Sphere(g, "bubble", MachineKit.L(31 + (i % 2 == 0 ? -5 : 4), -3 + (i < 2 ? -3 : 4), 10), 3, GameConfig.Hex("eaf8ff")); Prim.NoShadow(bb); a.bubbles.Add(bb.transform); }
        // 파란 관 아치: 가열 몸통 → 탱크
        MachineKit.PipeArch(g, 5, 31, -3, 34, 40, 56, 6, MachineKit.PIPE);   // 탱크 쪽은 열린 윗구멍으로 물속까지 내려간다
        MachineKit.Knob(g, -2, -12, 34, GameConfig.BOTTLE, a);
    }

    // 병입기(물 → 물통): 뒤 왼쪽 유리 탱크의 물(S.water)이 파란 관 아치로 흰 충전 몸통(주황 창)에 들어가고, 앞 컨베이어 위 병을 노즐이 채워
    // 동쪽 산출 받침대(금색 테두리)에 물통이 쌓인다(S.bottles). 접근은 서쪽(뽀삐 적재점·게이트)
    static void Bottler(Stations st)
    {
        var S = GameState.I.S; var b = WorldMap.STN["bottler"];
        var g = MachineKit.Group(st.bottlerView, b, 96, 64);
        var a = st.bottlerView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.water >= 10 && S.bottles < GameConfig.BOTTLE_CAP;
        MachineKit.Tank(g, -30, -12, 26, 6, 40, GameConfig.BOTTLE, out var liq);
        a.SetLiquid(liq); a.level = () => 0.3f + 0.7f * S.water / 60f;   // 빈 유리관은 회색으로 읽혀 바닥 30%는 늘 물
        // 충전 몸통 + 주황 창, 탱크에서 넘어오는 관 아치
        Body(g, -1, -15, 22, 22, 30);
        MachineKit.HeatWindow(g, -5, -4, 16, 12, 14, 3, a);
        MachineKit.PipeArch(g, -30, -1, -12, 40, 32, 56, 6, MachineKit.PIPE);
        // 노즐 팔: 몸통 앞에서 컨베이어 위로 뻗어 병을 채운다
        Prim.Box(g, "fillArm", MachineKit.L(6, 7, 27), new Vector3(7, 6, 22), MachineKit.SHELL);
        Prim.Cyl(g, "nozzle", MachineKit.L(6, 18, 22), 5, 7, MachineKit.PIPE);
        MachineKit.Knob(g, -7, -22, 32, GameConfig.BOTTLE, a);
        // 컨베이어(앞쪽, 서→동, 산출 받침대로 들어간다)와 그 위를 지나가는 병 3개
        Prim.Box(g, "belt", MachineKit.L(-16, 20, 9), new Vector3(60, 6, 13), MachineKit.STEEL_D);
        Prim.Box(g, "beltTop", MachineKit.L(-16, 20, 12.2f), new Vector3(58, 0.6f, 11), GameConfig.Hex("2a2e36"));
        foreach (float x in new[] { -46f, 14f }) Prim.Cyl(g, "roller", MachineKit.L(x, 20, 9), 6, 13, MachineKit.SHELL_D).transform.localRotation = Quaternion.Euler(90, 0, 0);
        foreach (float x in new[] { -40f, 8f }) Prim.Box(g, "beltLeg", MachineKit.L(x, 20, 3), new Vector3(3, 6, 10), MachineKit.STEEL_D);
        a.beltX0 = -42; a.beltX1 = 10;
        for (int i = 0; i < 3; i++)
        {
            var bt = new GameObject("beltBottle").transform; bt.SetParent(g, false); bt.localPosition = MachineKit.L(-42 + i * 17, 20, 13);
            Prim.Cyl(bt, "b", MachineKit.L(0, 0, 4.5f), 6.5f, 9, GameConfig.BOTTLE); Prim.Cyl(bt, "neck", MachineKit.L(0, 0, 9.6f), 3.2f, 1.6f, GameConfig.BOTTLE); Prim.Cyl(bt, "cap", MachineKit.L(0, 0, 11f), 3.8f, 1.4f, Color.white);
            a.belt.Add(bt);
        }
        // 산출 받침대(동쪽, 금색 테두리): 물통이 2×3으로 층층이 쌓인다(Stations.TRAY_*)
        float th = MachineKit.Table(g, 31, -2, 32, 42, 16, GameConfig.GOLD);
        st.trayAnchor.position = WorldMap.W2U(b.x + 31, b.y - 2, th);
    }

    // 흰 기계 몸통: 외피 상자 + 한 톤 밝은 뚜껑(살짝 안쪽) + 아래 회색 띠
    static void Body(Transform g, float dx, float dy, float w, float d, float h)
    {
        Prim.Box(g, "bodyBase", MachineKit.L(dx, dy, 8), new Vector3(w + 1, 6, d + 1), MachineKit.SHELL_D);
        Prim.Box(g, "body", MachineKit.L(dx, dy, 5 + h / 2), new Vector3(w, h - 4, d), MachineKit.SHELL);
        Prim.Box(g, "bodyTop", MachineKit.L(dx, dy, h + 3.5f), new Vector3(w - 3, 2, d - 3), GameConfig.Hex("f8f4ee"));
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

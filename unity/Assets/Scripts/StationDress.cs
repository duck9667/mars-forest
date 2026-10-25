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

    // 해빙기(얼음 → 물, fb2 "제빙기·해빙기가 비슷하다"): 열·녹임의 기계. 병입기와 실루엣·색을 확실히 다르게.
    //  숯빛 화로(앞면 주황 불꽃 창 + 굵은 창살) 위 넓은 호퍼에 얼음이 4×3으로 펼쳐지고, 일하면 호퍼에서 김이 피어오르고
    //  동쪽 주둥이에서 물줄기가 떨어져 둥근 수조에 고인다(S.water). 강조색은 주황·빨강(호퍼 테·굴뚝 띠·표시등). 접근은 서쪽 통로(호퍼 쪽)
    static readonly Color FIRE = GameConfig.Hex("4b4f58"), FIRE_TOP = GameConfig.Hex("5c616b"), HOT_RIM = GameConfig.Hex("e5603a"), HOT_RED = GameConfig.Hex("d9483a");
    static void Melter(Stations st)
    {
        var S = GameState.I.S; var m = WorldMap.STN["melter"];
        var g = MachineKit.Group(st.melterView, m, 96, 64, "core");
        var a = st.melterView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.meltQ > 0;
        const float FX = -14, FY = -4, BASE = 6.2f, FIRE_H = 11f, TOP = BASE + FIRE_H;   // 화로·호퍼 중심, 화로 윗면 높이
        // 화로: 둥근 모서리 숯빛 몸통 + 한 단 밝은 윗단
        MachineKit.RoundSlab(g, "firebox", FX, FY, BASE, 52, 36, FIRE_H - 1.2f, 6, FIRE);
        MachineKit.RoundSlab(g, "fireTop", FX, FY, TOP - 1.2f, 50, 34, 1.2f, 5, FIRE_TOP);
        // 앞면(남) 불꽃 창: 안쪽 발광 상자(일하면 주황) + 위·아래 턱 + 굵은 원기둥 창살 5
        var glow = Prim.Box(g, "fireGlow", MachineKit.L(FX, FY + 17.4f, BASE + 5.4f), new Vector3(34, 6.2f, 2.6f), MachineKit.HEAT_OFF);
        Prim.NoShadow(glow); a.AddGlowMat(glow.GetComponent<Renderer>(), MachineKit.GlowMat(MachineKit.HEAT, 1.5f), Mats.Solid(MachineKit.HEAT_OFF));
        Prim.Box(g, "fireLipT", MachineKit.L(FX, FY + 18.2f, BASE + 9.2f), new Vector3(38, 1.8f, 2.6f), MachineKit.STEEL_D);
        Prim.Box(g, "fireLipB", MachineKit.L(FX, FY + 18.2f, BASE + 1.6f), new Vector3(38, 1.8f, 2.6f), MachineKit.STEEL_D);
        for (int i = 0; i < 5; i++) Prim.Cyl(g, "fireBar", MachineKit.L(FX - 13.6f + i * 6.8f, FY + 18.8f, BASE + 5.4f), 1.9f, 7.4f, MachineKit.STEEL_D);
        // 넓은 호퍼: 흰 벽(두께 3) + 주황·빨강 윗테, 앞벽은 낮게(얼음이 보이게). 얼음은 화로 윗면 위에 한 층으로(Stations.MELT_*)
        float hw = 50, hd = 32, wt = 3f, wall = 7f;
        Prim.Box(g, "wN", MachineKit.L(FX, FY - hd / 2 + wt / 2, TOP + wall / 2), new Vector3(hw, wall, wt), MachineKit.SHELL);
        Prim.Box(g, "wW", MachineKit.L(FX - hw / 2 + wt / 2, FY, TOP + wall / 2), new Vector3(wt, wall, hd), MachineKit.SHELL);
        Prim.Box(g, "wE", MachineKit.L(FX + hw / 2 - wt / 2, FY, TOP + wall / 2), new Vector3(wt, wall, hd), MachineKit.SHELL);
        Prim.Box(g, "wS", MachineKit.L(FX, FY + hd / 2 - wt / 2, TOP + 2f), new Vector3(hw, 4f, wt), MachineKit.SHELL);
        Prim.Box(g, "rimN", MachineKit.L(FX, FY - hd / 2 + wt / 2, TOP + wall + 0.9f), new Vector3(hw + 1, 1.8f, wt + 0.8f), HOT_RIM);
        Prim.Box(g, "rimW", MachineKit.L(FX - hw / 2 + wt / 2, FY, TOP + wall + 0.9f), new Vector3(wt + 0.8f, 1.8f, hd + 1), HOT_RIM);
        Prim.Box(g, "rimE", MachineKit.L(FX + hw / 2 - wt / 2, FY, TOP + wall + 0.9f), new Vector3(wt + 0.8f, 1.8f, hd + 1), HOT_RIM);
        Prim.Box(g, "rimS", MachineKit.L(FX, FY + hd / 2 - wt / 2, TOP + 4.9f), new Vector3(hw + 1, 1.8f, wt + 0.8f), HOT_RIM);
        st.meltAnchor.position = WorldMap.W2U(m.x + FX, m.y + FY, TOP);
        // 굴뚝(뒤 동쪽): 숯빛 원통 + 빨간 띠 + 뚜껑. 높이가 실루엣을 만든다(병입기는 유리 탱크)
        Prim.Cyl(g, "chimney", MachineKit.L(FX + 20, FY - 11, TOP + 9), 7, 18, FIRE);
        ToyMesh.Put(g, "chimneyBand", ToyMesh.Cached("chimBand", t => t.Band(3.8f, 2.6f, 16, HOT_RED)), ToyMesh.Mat(0.2f), MachineKit.L(FX + 20, FY - 11, TOP + 11));
        Prim.Cyl(g, "chimneyCap", MachineKit.L(FX + 20, FY - 11, TOP + 18.8f), 9, 1.8f, MachineKit.STEEL_D);
        // 김: 일할 때 호퍼에서 몽글한 반투명 흰 덩어리 넷이 피어올라 작아지며 흩어진다(불투명이면 외곽선이 생겨 눈덩이처럼 보였다)
        var puff = ToyMesh.Cached("steamPuff", t => t.Blob(Vector3.one * (0.5f / GameConfig.K), 1, 21, 0.1f, Color.white, Color.white, Color.white));
        var steamMat = Mats.TransShared(new Color(1f, 0.99f, 0.97f, 0.55f));
        for (int i = 0; i < 4; i++)
        {
            var p = ToyMesh.Put(g, "steam", puff, steamMat, MachineKit.L(FX + (i % 2 == 0 ? -11 : 9), FY + (i < 2 ? -5 : 4), TOP));
            Prim.NoShadow(p); a.bubbles.Add(p.transform);
        }
        a.bubbleBase = (TOP + 4) * GameConfig.K; a.bubbleH = 22; a.bubbleS0 = 9f; a.bubbleS1 = 3.5f;
        // 물: 화로 동쪽 주둥이 → 떨어지는 물줄기(일할 때만) → 둥근 수조(두툼한 흰 벽, 물 높이 = S.water) → 동쪽 끝 배관(북쪽 물 배관으로)
        MachineKit.PipeX(g, FX + 26, 24, 7, BASE + 8.5f, 3.6f, MachineKit.PIPE);
        Prim.Cyl(g, "spout", MachineKit.L(25, 7, BASE + 6.6f), 4.4f, 3.4f, MachineKit.PIPE);
        var stream = Prim.Cyl(g, "stream", MachineKit.L(25, 7, BASE + 3.4f), 2.4f, 6.2f, GameConfig.Hex("8fd3f5"));
        Prim.NoShadow(stream); a.showWhenWorking.Add(stream.transform);
        ToyMesh.Put(g, "basin", ToyMesh.Cached("meltBasin", t => t.Ring(11.6f, 14.2f, 7.4f, 28, MachineKit.SHELL, GameConfig.Hex("fbf8f3"), MachineKit.SHELL_D)), ToyMesh.Mat(0.25f), MachineKit.L(32, 8, BASE));
        Prim.Cyl(g, "basinFloor", MachineKit.L(32, 8, BASE + 0.4f), 24, 0.8f, MachineKit.SHELL_D);
        var liq = Prim.Cyl(g, "liquid", MachineKit.L(32, 8, BASE + 0.8f + 3.2f), 23.4f, 6.4f, GameConfig.BOTTLE);
        a.SetLiquid(liq.transform); a.level = () => 0.3f + 0.7f * S.water / 60f;   // 빈 수조는 회색으로 읽혀 바닥 30%는 늘 물
        MachineKit.PipeX(g, 46.5f, 50, 8, 9f, 3.6f, MachineKit.PIPE);
        MachineKit.Knob(g, FX - 21, FY - 12, TOP + wall + 1.8f, MachineKit.HEAT, a);
    }

    // 병입기(물 → 물병, fb2): 물·포장의 기계. 뒤 왼쪽 키 큰 유리 물탱크(S.water) → 파란 관 아치 → 흰 제어함(파란 화면)
    //  → 컨베이어 위로 매달린 충전 헤드의 파란 노즐 둘이 지나가는 병을 채우고 → 동쪽 파란 병 상자(금색 윗테)에 물병이 3×4로 선다(S.bottles).
    //  강조색은 파랑. 접근은 서쪽(뽀삐 적재점·게이트)
    static readonly Color CRATE_B = GameConfig.Hex("4a9bdc"), CRATE_B_D = GameConfig.Hex("3a7fbd"), SCREEN = GameConfig.Hex("2f6fb3");
    static void Bottler(Stations st)
    {
        var S = GameState.I.S; var b = WorldMap.STN["bottler"];
        var g = MachineKit.Group(st.bottlerView, b, 96, 64);
        var a = st.bottlerView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.water >= 10 && S.bottles < GameConfig.BOTTLE_CAP;
        MachineKit.Tank(g, -30, -12, 26, 6, 40, GameConfig.BOTTLE, out var liq);
        a.SetLiquid(liq); a.level = () => 0.3f + 0.7f * S.water / 60f;   // 빈 유리관은 회색으로 읽혀 바닥 30%는 늘 물
        for (int i = 0; i < 3; i++) { var bb = Prim.Sphere(g, "bubble", MachineKit.L(-30 + (i - 1) * 5, -12 + (i == 1 ? 3 : -2), 10), 3, GameConfig.Hex("eaf8ff")); Prim.NoShadow(bb); a.bubbles.Add(bb.transform); }
        a.bubbleBase = 8 * GameConfig.K; a.bubbleH = 26;
        // 제어함 + 앞면 파란 화면(두께 있는 상자) + 탱크에서 넘어오는 관 아치
        Body(g, -1, -15, 22, 22, 30);
        ToyMesh.Put(g, "screen", ToyMesh.Cached("bottlerScreen", t => t.Bevel(new Vector3(14, 9, 2.6f), 0.8f, SCREEN, SCREEN, GameConfig.Hex("5d9ad6"), SCREEN, SCREEN)), ToyMesh.Mat(0.4f), MachineKit.L(-1, -3.6f, 21));
        MachineKit.PipeArch(g, -30, -1, -12, 40, 32, 56, 6, MachineKit.PIPE);
        MachineKit.Knob(g, -7, -22, 32, GameConfig.BOTTLE, a);
        // 컨베이어(앞쪽, 서→동, 병 상자로 들어간다)와 그 위를 지나가는 물병 3개(물건 모형 그대로)
        Prim.Box(g, "belt", MachineKit.L(-16, 20, 9), new Vector3(60, 6, 13), MachineKit.STEEL_D);
        Prim.Box(g, "beltTop", MachineKit.L(-16, 20, 12.2f), new Vector3(58, 0.6f, 11), GameConfig.Hex("2a2e36"));
        foreach (float x in new[] { -46f, 14f }) Prim.Cyl(g, "roller", MachineKit.L(x, 20, 9), 6, 13, MachineKit.SHELL_D).transform.localRotation = Quaternion.Euler(90, 0, 0);
        foreach (float x in new[] { -40f, 8f }) Prim.Box(g, "beltLeg", MachineKit.L(x, 20, 3), new Vector3(3, 6, 10), MachineKit.STEEL_D);
        a.beltX0 = -42; a.beltX1 = 10;
        for (int i = 0; i < 3; i++)
        {
            var bt = new GameObject("beltBottle").transform; bt.SetParent(g, false); bt.localPosition = MachineKit.L(-42 + i * 17, 20, 12.5f + Items.ITEM_H["bottle"] / 2);
            Items.Slab(bt, "bottle");
            a.belt.Add(bt);
        }
        // 충전 헤드: 컨베이어 뒤 기둥에서 앞으로 뻗은 흰 머리(파란 띠) + 아래로 파란 노즐 둘(병 바로 위)
        Prim.Cyl(g, "fillPost", MachineKit.L(-12, 10, 6 + 14.5f), 4.4f, 29, MachineKit.SHELL);
        ToyMesh.Put(g, "fillHead", ToyMesh.Cached("fillHead", t =>
        {
            t.At(0, 0, 0).Bevel(new Vector3(22, 8, 16), 2f, MachineKit.SHELL, MachineKit.SHELL_D, GameConfig.Hex("fbf8f3"), GameConfig.Hex("f8f4ee"), MachineKit.SHELL);
            t.At(0, -1.6f, 0).Bevel(new Vector3(22.8f, 2.8f, 16.8f), 0.8f, MachineKit.PIPE, MachineKit.PIPE, GameConfig.Hex("7cc0ea"), MachineKit.PIPE, MachineKit.PIPE);   // 파란 띠
            return t;
        }), ToyMesh.Mat(0.3f), MachineKit.L(-12, 18, 31));
        foreach (float nx in new[] { -18f, -6f })
        {
            Prim.Cyl(g, "nozzle", MachineKit.L(nx, 20, 25.4f), 3.4f, 4.4f, MachineKit.PIPE);
            Prim.Cyl(g, "nozzleTip", MachineKit.L(nx, 20, 22.6f), 1.8f, 1.6f, MachineKit.STEEL_D);
        }
        // 병 상자(동쪽): 다리 + 파란 상자(두툼한 바닥·벽) + 금색 윗테(산출 받침대 규칙). 앞벽은 낮게(병이 보이게)
        const float CX = 31, CY = -2, CW = 32, CD = 42, FL = 12f;
        Prim.Box(g, "crateLeg", MachineKit.L(CX, CY, (6 + FL) / 2), new Vector3(CW * 0.7f, FL - 6, CD * 0.7f), MachineKit.STEEL_D);
        MachineKit.RoundSlab(g, "crateFloor", CX, CY, FL, CW, CD, 2.2f, 3, CRATE_B_D);
        float wt = 2.6f, top = FL + 2.2f;
        Prim.Box(g, "crateN", MachineKit.L(CX, CY - CD / 2 + wt / 2, top + 3.5f), new Vector3(CW, 7, wt), CRATE_B);
        Prim.Box(g, "crateW", MachineKit.L(CX - CW / 2 + wt / 2, CY, top + 3.5f), new Vector3(wt, 7, CD), CRATE_B);
        Prim.Box(g, "crateE", MachineKit.L(CX + CW / 2 - wt / 2, CY, top + 3.5f), new Vector3(wt, 7, CD), CRATE_B);
        Prim.Box(g, "crateS", MachineKit.L(CX, CY + CD / 2 - wt / 2, top + 1.8f), new Vector3(CW, 3.6f, wt), CRATE_B);
        Prim.Box(g, "crateRimN", MachineKit.L(CX, CY - CD / 2 + wt / 2, top + 7.7f), new Vector3(CW + 0.6f, 1.6f, wt + 0.6f), GameConfig.GOLD);
        Prim.Box(g, "crateRimW", MachineKit.L(CX - CW / 2 + wt / 2, CY, top + 7.7f), new Vector3(wt + 0.6f, 1.6f, CD + 0.6f), GameConfig.GOLD);
        Prim.Box(g, "crateRimE", MachineKit.L(CX + CW / 2 - wt / 2, CY, top + 7.7f), new Vector3(wt + 0.6f, 1.6f, CD + 0.6f), GameConfig.GOLD);
        Prim.Box(g, "crateRimS", MachineKit.L(CX, CY + CD / 2 - wt / 2, top + 4.4f), new Vector3(CW + 0.6f, 1.6f, wt + 0.6f), GameConfig.GOLD);
        st.trayAnchor.position = WorldMap.W2U(b.x + CX, b.y + CY, top);
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

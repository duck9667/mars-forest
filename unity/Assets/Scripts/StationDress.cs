using UnityEngine;

// 스테이션 모형: 해빙기·병입기(MachineKit 문법)·카운터·계산대는 레벨 외형 3단계(LevelLook: 기본 Lv0~1 → 개량 Lv2~3 → 고급 Lv4~)로,
// 드릴은 Kenney Space Kit 모델(원본 index.html bDrill 623행 put 배치).
// 규약: 각 view 의 플레이스홀더 자식 "body"/"top" 을 숨기고, 스테이션 중심 W2U(stn.x, stn.y, 0)에 둔 그룹 아래에 모형을 넣는다
// (view 자체가 스테이션 중심이라 JuiceFX 스쿼시가 제자리에서 일어난다).
// 정적인 부분은 단계마다 ToyMesh 두 덩어리(본체 = 받침·몸통, 단계 부품 = 테·굴뚝·화면처럼 단계가 바뀌면 솟아오르는 것)로 그려
// 예전 프리미티브 수십 개(해빙기 약 60) 대신 드로우콜 몇 개로 줄였다. 움직이는 것(불꽃 창·김·물줄기·액체·벨트 병·표시등)만 따로 둔다.
public static class StationDress
{
    public static void Dress(Stations st)
    {
        st.melterLook = Melter(st);
        st.bottlerLook = Bottler(st);
        st.counterLook = Counter(st);
        st.registerLook = Register(st);
        DressDrill(st.drillView);
    }

    static Color Hex(string h) => GameConfig.Hex(h);
    static readonly Color SHELL = MachineKit.SHELL, SHELL_D = MachineKit.SHELL_D, STEEL_D = MachineKit.STEEL_D, PIPE = MachineKit.PIPE;
    static readonly Color GLOSS = LevelLook.GLOSS, GLOSS_D = LevelLook.GLOSS_D, TRIM = LevelLook.TRIM, TRIM_L = LevelLook.TRIM_L,
        METAL = LevelLook.METAL, METAL_L = LevelLook.METAL_L, METAL_D = LevelLook.METAL_D;

    // 받침(밝은 콘크리트 둥근 판 + 한 단 밝은 윗단, MachineKit.Group 과 같은 치수)을 메시에 함께
    public static void Plate(ToyMesh m, float w, float d)
    {
        m.AtM(0, 0, 2.5f).RBox(new Vector3(w, 5, d), 9, MachineKit.PAD);
        m.AtM(0, 0, 5.6f).RBox(new Vector3(w - 8, 1.2f, d - 8), 6, MachineKit.PAD_TOP);
    }
    static MeshFilter Holder(Transform g, string name) => LevelLook.Holder(g, name);
    static GameObject GlassPart(Transform g, string name, Mesh mesh, Vector3 local)
    {
        var go = ToyMesh.Put(g, name, mesh, LevelLook.GlassMat, local); Prim.NoShadow(go); return go;
    }

    // ═════════════ 해빙기(얼음 → 물) ═════════════
    // 열·녹임의 기계(fb2): 숯빛 화로(앞면 주황 불꽃 창 + 굵은 창살) 위 넓은 호퍼에 얼음 4×3, 일하면 김이 피어오르고 동쪽 주둥이 물줄기가 둥근 수조에 고인다.
    //  기본: 주황·빨강 호퍼 테 · 굴뚝 하나 · 불꽃 창
    //  개량: 호퍼 테가 금속(굵게) · 굴뚝 둘(뒤쪽, 금속 띠) · 불꽃 창이 넓어지고 창살 6
    //  고급: 매끈한 흰 외피 + 샴페인 금 테 · 호퍼 위 유리 돔 덮개 · 불꽃이 더 밝음 · 물 수조 둘(금 윗테)
    static readonly Color FIRE = Hex("4b4f58"), FIRE_S = Hex("3c4049"), FIRE_TOP = Hex("5c616b"), HOT_RIM = Hex("e5603a"), HOT_RIM_L = Hex("f08a62"), HOT_RED = Hex("d9483a");
    const float FX = -14, FY = -4, BASE = 6.2f, FIRE_H = 11f, TOP = BASE + FIRE_H, HW = 50, HD = 32, WT = 3f, WALL = 7f;
    static readonly Vector2[] FIRE_WIN = { new Vector2(34, 6.2f), new Vector2(42, 7.4f), new Vector2(44, 7.4f) };   // 불꽃 창 폭·높이(단계별)
    static readonly Vector2[] BASINS_HI = { new Vector2(33, 12), new Vector2(33, -14) };                         // 고급: 수조 둘(가운데 dx, dy)

    static Mesh MeltBody(int t) => ToyMesh.Cached("meltBody" + t, m =>
    {
        bool hi = t >= 2;
        Plate(m, 96, 64);
        // 화로: 둥근 모서리 몸통(아래가 한 톤 어둡게) + 한 단 밝은 윗단. 고급은 매끈한 흰 외피
        Color fire = hi ? GLOSS : FIRE, fireS = hi ? GLOSS_D : FIRE_S, fireTop = hi ? Hex("ebe5db") : FIRE_TOP;
        m.AtM(FX, FY, BASE + (FIRE_H - 1.2f) / 2).RBox(new Vector3(52, FIRE_H - 1.2f, 36), 6, fireS, fire, fire);
        m.AtM(FX, FY, TOP - 0.6f).RBox(new Vector3(50, 1.2f, 34), 5, fireTop);
        // 호퍼 벽(두께 3, 모서리 살짝 깎음): 북·서·동은 높게, 앞(남)은 낮게(얼음이 보이게). 얼음은 화로 윗면 위 한 층(Stations.MELT_*)
        Color w = hi ? GLOSS : SHELL, we = hi ? Color.white : Hex("fbf8f3");
        m.AtM(FX, FY - HD / 2 + WT / 2, TOP + WALL / 2).Bevel(new Vector3(HW, WALL, WT), 0.7f, w, w, we, w, w);
        m.AtM(FX - HW / 2 + WT / 2, FY, TOP + WALL / 2).Bevel(new Vector3(WT, WALL, HD), 0.7f, w, w, we, w, w);
        m.AtM(FX + HW / 2 - WT / 2, FY, TOP + WALL / 2).Bevel(new Vector3(WT, WALL, HD), 0.7f, w, w, we, w, w);
        m.AtM(FX, FY + HD / 2 - WT / 2, TOP + 2).Bevel(new Vector3(HW, 4, WT), 0.7f, w, w, we, w, w);
        // 물: 화로 동쪽 주둥이 관 → 둥근 수조(두툼한 흰 벽) → 동쪽 끝 배관(북쪽 물 배관으로)
        m.PipeM(12, 7, BASE + 8.5f, 23.5f, 7, BASE + 8.5f, 1.8f, PIPE);
        m.AtM(25, 7, BASE + 4.9f).Cyl(2.2f, 2.2f, 3.4f, 12, PIPE, PIPE, true, null, true);
        if (!hi)
        {
            m.AtM(32, 8, BASE).Ring(11.6f, 14.2f, 7.4f, 28, SHELL, Hex("fbf8f3"), SHELL_D);
            m.AtM(32, 8, BASE).Cyl(12f, 12f, 0.8f, 24, SHELL_D, SHELL_D);
            m.PipeM(46.5f, 8, 9, 50, 8, 9, 1.8f, PIPE);
        }
        else
        {
            foreach (var b in BASINS_HI)
            {
                m.AtM(b.x, b.y, BASE).Ring(8.8f, 11.2f, 7.4f, 26, GLOSS, TRIM, GLOSS_D);
                m.AtM(b.x, b.y, BASE).Cyl(9.2f, 9.2f, 0.8f, 20, SHELL_D, SHELL_D);
            }
            m.PipeM(43.4f, 8, 9, 50, 8, 9, 1.8f, PIPE);
            m.PipeM(33, 1.2f, BASE + 3.4f, 33, -3.2f, BASE + 3.4f, 1.7f, PIPE, false);   // 두 수조를 잇는 짧은 관
        }
        return m;
    });

    static Mesh MeltParts(int t) => ToyMesh.Cached("meltParts" + t, m =>
    {
        // 호퍼 윗테: 기본 주황·빨강 / 개량 금속(굵게) / 고급 금
        Color rim = t == 0 ? HOT_RIM : t == 1 ? METAL_L : TRIM, rimE = t == 0 ? HOT_RIM_L : t == 1 ? Hex("e6ebf0") : TRIM_L;
        float rh = t == 0 ? 1.8f : 2.4f, rw = WT + (t == 0 ? 0.8f : 1.6f);
        m.AtM(FX, FY - HD / 2 + WT / 2, TOP + WALL + rh / 2).Bevel(new Vector3(HW + 1, rh, rw), 0.5f, rim, rim, rimE, rimE, rim);
        m.AtM(FX - HW / 2 + WT / 2, FY, TOP + WALL + rh / 2).Bevel(new Vector3(rw, rh, HD + 1), 0.5f, rim, rim, rimE, rimE, rim);
        m.AtM(FX + HW / 2 - WT / 2, FY, TOP + WALL + rh / 2).Bevel(new Vector3(rw, rh, HD + 1), 0.5f, rim, rim, rimE, rimE, rim);
        m.AtM(FX, FY + HD / 2 - WT / 2, TOP + 4 + rh / 2).Bevel(new Vector3(HW + 1, rh, rw), 0.5f, rim, rim, rimE, rimE, rim);
        // 불꽃 창 틀: 위·아래 턱 + 굵은 원기둥 창살
        var win = FIRE_WIN[t]; float wc = BASE + 5.4f, fy = FY + 18.2f;
        Color frame = t == 0 ? STEEL_D : t == 1 ? METAL_D : TRIM, frameE = t == 2 ? TRIM_L : Color.Lerp(frame, Color.white, 0.25f), bar = t == 2 ? METAL_L : frame;
        m.AtM(FX, fy, wc + win.y / 2 + 0.9f).Bevel(new Vector3(win.x + 4, 1.8f, 2.6f), 0.5f, frame, frame, frameE, frameE, frame);
        m.AtM(FX, fy, wc - win.y / 2 - 0.9f).Bevel(new Vector3(win.x + 4, 1.8f, 2.6f), 0.5f, frame, frame, frameE, frameE, frame);
        int nb = t == 0 ? 5 : t == 1 ? 6 : 4; float span = win.x - 6;
        for (int i = 0; i < nb; i++) m.AtM(FX - span / 2 + i * span / (nb - 1), FY + 18.8f, wc - (win.y + 1) / 2).Cyl(0.95f, 0.95f, win.y + 1, 10, bar, bar);
        if (t == 0)
        {
            // 굴뚝 하나(뒤 동쪽, 화로 윗면 위): 숯빛 원통 + 빨간 띠 + 뚜껑
            m.AtM(FX + 20, FY - 11, TOP).Cyl(3.5f, 3.5f, 18, 16, FIRE, FIRE);
            m.AtM(FX + 20, FY - 11, TOP + 9.7f).Band(3.8f, 2.6f, 16, HOT_RED);
            m.AtM(FX + 20, FY - 11, TOP + 17.9f).Cyl(4.5f, 4.5f, 1.8f, 16, STEEL_D, STEEL_D, true, null, true);
        }
        else
        {
            // 굴뚝 둘(화로 뒤에서 솟음, 높이 다르게): 개량 금속 + 어두운 띠 / 고급 흰 외피 + 금 띠 + 은빛 뚜껑
            Color body = t == 1 ? METAL : GLOSS, bodyD = t == 1 ? METAL_D : GLOSS_D, band = t == 1 ? METAL_D : TRIM, cap = t == 1 ? STEEL_D : METAL_L;
            foreach (var (cx, hh) in new[] { (-26f, 24f), (2f, 19f) })
            {
                float h = TOP - BASE + hh;
                m.AtM(cx, -24, BASE).Cyl(3.9f, 3.4f, h, 16, bodyD, body, true, body);
                m.AtM(cx, -24, TOP + hh * 0.4f).Band(3.75f, 2.4f, 16, band);
                m.AtM(cx, -24, TOP + hh - 3.4f).Band(3.55f, 2.4f, 16, band);
                m.AtM(cx, -24, TOP + hh).Cyl(4.8f, 3.4f, 2.2f, 16, cap, cap, true, null, true);
            }
            // 화로 아래 띠: 개량 금속 / 고급 금(흰 외피에 두르는 테)
            Color sk = t == 1 ? METAL_D : TRIM, skL = t == 1 ? METAL : TRIM_L;
            m.AtM(FX, FY, BASE + 2.2f).RBox(new Vector3(53.4f, 2.2f, 37.4f), 6.7f, sk, skL, skL);
        }
        return m;
    });

    public static TierLook Melter(Stations st)
    {
        var S = GameState.I.S; var mm = WorldMap.STN["melter"];
        var g = MachineKit.Group(st.melterView, mm, 96, 64, false, "core");
        var a = st.melterView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.meltQ > 0;
        var body = Holder(g, "meltBody"); var parts = Holder(g, "meltParts");
        st.meltAnchor.position = WorldMap.W2U(mm.x + FX, mm.y + FY, TOP);
        // 앞면(남) 불꽃 창 안쪽 발광 상자(일하면 주황, 고급은 더 밝게). 크기는 단계마다
        var glow = Prim.Box(g, "fireGlow", MachineKit.L(FX, FY + 17.4f, BASE + 5.4f), new Vector3(34, 6.2f, 2.6f), MachineKit.HEAT_OFF);
        Prim.NoShadow(glow); var glowR = glow.GetComponent<Renderer>();
        Material glowOn = MachineKit.GlowMat(MachineKit.HEAT, 1.5f), glowHi = MachineKit.GlowMat(Hex("ffa04a"), 2.1f);
        a.AddGlowMat(glowR, glowOn, Mats.Solid(MachineKit.HEAT_OFF));
        // 김: 일할 때 호퍼에서 몽글한 반투명 흰 덩어리 넷이 피어올라 작아지며 흩어진다
        var puff = ToyMesh.Cached("steamPuff", t => t.Blob(Vector3.one * (0.5f / GameConfig.K), 1, 21, 0.1f, Color.white, Color.white, Color.white));
        var steamMat = Mats.TransShared(new Color(1f, 0.99f, 0.97f, 0.55f));
        for (int i = 0; i < 4; i++)
        {
            var p = ToyMesh.Put(g, "steam", puff, steamMat, MachineKit.L(FX + (i % 2 == 0 ? -11 : 9), FY + (i < 2 ? -5 : 4), TOP));
            Prim.NoShadow(p); a.bubbles.Add(p.transform);
        }
        a.bubbleBase = (TOP + 4) * GameConfig.K; a.bubbleH = 22; a.bubbleS0 = 9f; a.bubbleS1 = 3.5f;
        // 물줄기(일할 때만) · 수조 물(높이 = S.water, 빈 수조는 회색으로 읽혀 바닥 30%는 늘 물). 고급은 수조 둘
        var stream = Prim.Cyl(g, "stream", MachineKit.L(25, 7, BASE + 3.4f), 2.4f, 6.2f, Hex("8fd3f5"));
        Prim.NoShadow(stream); a.showWhenWorking.Add(stream.transform);
        var liq = Prim.Cyl(g, "liquid", MachineKit.L(32, 8, BASE + 0.8f + 3.2f), 23.4f, 6.4f, GameConfig.BOTTLE);
        a.SetLiquid(liq.transform); a.level = () => 0.3f + 0.7f * S.water / 60f;
        var liq2 = Prim.Cyl(g, "liquid2", MachineKit.L(BASINS_HI[1].x, BASINS_HI[1].y, BASE + 0.8f + 3.2f), 17.8f, 6.4f, GameConfig.BOTTLE);
        a.AddLiquid(liq2.transform);
        MachineKit.Knob(g, FX - 21, FY - 12, TOP + WALL + 1.8f, MachineKit.HEAT, a);
        // 고급: 호퍼를 덮는 낮은 유리 돔(금 윗테 위). 유리는 깊이를 안 써서 안의 얼음·날아드는 얼음이 그대로 보인다
        var dome = GlassPart(g, "glassDome", ToyMesh.Cached("meltDome", m => m.Dome(27.5f, 18.5f, 12f, Color.white, Color.white, 28, 7)), MachineKit.L(FX, FY, TOP + WALL + 2.4f));
        var look = new TierLook { squashRoot = st.melterView };
        look.Swap(body, MeltBody).Swap(parts, MeltParts, null, true).Part(dome, 2).Part(liq2, 2);
        look.onTier = (t, anim) =>
        {
            var win = FIRE_WIN[t];
            glow.transform.localScale = new Vector3(win.x, win.y, 2.6f) * GameConfig.K;
            a.SetGlowOn(glowR, t >= 2 ? glowHi : glowOn);
            // 수조 물: 기본·개량은 큰 수조 하나, 고급은 작은 수조 둘
            bool hi = t >= 2; var p0 = hi ? BASINS_HI[0] : new Vector2(32, 8); float d = hi ? 17.8f : 23.4f;
            var lp = MachineKit.L(p0.x, p0.y, 0); liq.transform.localPosition = new Vector3(lp.x, liq.transform.localPosition.y, lp.z);
            liq.transform.localScale = new Vector3(d * GameConfig.K, liq.transform.localScale.y, d * GameConfig.K);
        };
        look.Apply(LevelLook.TierOf("melter"), false);
        return look;
    }

    // ═════════════ 병입기(물 → 물병) ═════════════
    // 물·포장의 기계(fb2): 뒤 왼쪽 키 큰 유리 물탱크(S.water) → 파란 관 아치 → 흰 제어함 → 컨베이어 위 충전 헤드의 파란 노즐 → 동쪽 파란 병 상자(금색 윗테).
    //  기본: 탱크 하나 · 제어함 앞면 둥근 버튼 둘 · 상자 하나(3×4)
    //  개량: 작은 탱크 하나 더(관이 아치로 합류) · 제어함 앞면 파란 화면
    //  고급: 흰 외피 + 금 테(탱크 테·제어함 띠·관 마디) · 컨베이어가 동쪽 끝까지 길어지고 병 상자 둘(3×2씩) · 상자로 병을 옮기는 작은 로봇 팔(일할 때 까딱)
    static readonly Color CRATE_B = Hex("4a9bdc"), CRATE_B_D = Hex("3a7fbd"), SCREEN = Hex("2f6fb3"), SCREEN_L = Hex("5d9ad6");
    const float TAX = -33, TAY = -14, TAD = 24, TBX = -11, TBY = -22, TBD = 14;   // 탱크 A·B 중심·지름
    const float BX = 6, BY = -14, BW = 18, BD = 20;                             // 제어함 중심·크기(앞면 y −4)
    const float FL = 12f;                                                       // 병 상자 바닥 높이
    public static readonly Vector2 CRATE_LO = new Vector2(31, -2), CRATE_HI_A = new Vector2(31, -17), CRATE_HI_B = new Vector2(31, 5);
    static float BeltX1(int t) => t >= 2 ? 42 : 14;
    static float BeltY(int t) => t >= 2 ? 23 : 20;

    static void TankFrame(ToyMesh m, float dx, float dy, float dia, float h0, float h, bool hi)
    {
        m.AtM(dx, dy, 0).Cyl((dia + 4) / 2, (dia + 4) / 2, h0, 24, SHELL_D, SHELL_D);   // 밝은 받침(유리 너머 바닥이 어두우면 회색 통으로 읽힌다)
        Color rim = hi ? TRIM : SHELL, rimT = hi ? TRIM_L : Hex("fbf8f3"), rimIn = hi ? TRIM : SHELL_D;
        m.AtM(dx, dy, h0 + h - 2.2f).Ring(dia / 2 - 1.2f, dia / 2 + 1.4f, 3f, 24, rim, rimT, rimIn);
        m.AtM(dx, dy, h0).Ring(dia / 2 - 1.2f, dia / 2 + 1.4f, 3f, 24, hi ? TRIM : SHELL_D, hi ? TRIM_L : SHELL_D);
    }
    // 병 상자: 다리 + 둥근 바닥 + 파란 벽(앞벽은 낮게) + 금색 윗테(산출 받침대 규칙)
    static void Crate(ToyMesh m, float cx, float cy, float cw, float cd)
    {
        m.AtM(cx, cy, (6 + FL) / 2).RBox(new Vector3(cw * 0.7f, FL - 6, cd * 0.7f), 3, STEEL_D);
        m.AtM(cx, cy, FL + 1.1f).RBox(new Vector3(cw, 2.2f, cd), 3, CRATE_B_D);
        float wt = 2.6f, top = FL + 2.2f; Color e = Hex("6db3ea");
        m.AtM(cx, cy - cd / 2 + wt / 2, top + 3.5f).Bevel(new Vector3(cw, 7, wt), 0.6f, CRATE_B, CRATE_B, e, CRATE_B, CRATE_B);
        m.AtM(cx - cw / 2 + wt / 2, cy, top + 3.5f).Bevel(new Vector3(wt, 7, cd), 0.6f, CRATE_B, CRATE_B, e, CRATE_B, CRATE_B);
        m.AtM(cx + cw / 2 - wt / 2, cy, top + 3.5f).Bevel(new Vector3(wt, 7, cd), 0.6f, CRATE_B, CRATE_B, e, CRATE_B, CRATE_B);
        m.AtM(cx, cy + cd / 2 - wt / 2, top + 1.8f).Bevel(new Vector3(cw, 3.6f, wt), 0.6f, CRATE_B, CRATE_B, e, CRATE_B, CRATE_B);
        Color gd = GameConfig.GOLD, gl = Hex("f8d77c");
        m.AtM(cx, cy - cd / 2 + wt / 2, top + 7.7f).Bevel(new Vector3(cw + 0.6f, 1.6f, wt + 0.6f), 0.4f, gd, gd, gl, gl, gd);
        m.AtM(cx - cw / 2 + wt / 2, cy, top + 7.7f).Bevel(new Vector3(wt + 0.6f, 1.6f, cd + 0.6f), 0.4f, gd, gd, gl, gl, gd);
        m.AtM(cx + cw / 2 - wt / 2, cy, top + 7.7f).Bevel(new Vector3(wt + 0.6f, 1.6f, cd + 0.6f), 0.4f, gd, gd, gl, gl, gd);
        m.AtM(cx, cy + cd / 2 - wt / 2, top + 4.4f).Bevel(new Vector3(cw + 0.6f, 1.6f, wt + 0.6f), 0.4f, gd, gd, gl, gl, gd);
    }

    static Mesh BotBody(int t) => ToyMesh.Cached("botBody" + t, m =>
    {
        bool hi = t >= 2; Color sh = hi ? GLOSS : SHELL, shD = hi ? GLOSS_D : SHELL_D;
        Plate(m, 96, 64);
        TankFrame(m, TAX, TAY, TAD, 6, 40, hi);
        // 제어함: 아래 띠 + 둥근 몸통(아래가 한 톤 어둡게) + 한 톤 밝은 뚜껑
        m.AtM(BX, BY, 8).RBox(new Vector3(BW + 1, 6, BD + 1), 3, shD);
        m.AtM(BX, BY, 20).RBox(new Vector3(BW, 26, BD), 3, shD, sh, sh);
        m.AtM(BX, BY, 33.5f).RBox(new Vector3(BW - 3, 2, BD - 3), 2, hi ? Color.white : Hex("f8f4ee"));
        // 탱크 → 제어함 파란 관 아치
        m.PipeM(TAX, TAY, 40, TAX, TAY, 56, 3, PIPE, false);
        m.PipeM(BX, BY, 32, BX, BY, 56, 3, PIPE, false);
        m.PipeM(TAX, TAY, 56, BX, BY, 56, 3, PIPE, false);
        m.AtM(TAX, TAY, 56).Ball(3.6f, PIPE); m.AtM(BX, BY, 56).Ball(3.6f, PIPE);
        // 컨베이어(앞쪽, 서 → 동): 몸통 + 검은 벨트 + 양 끝 롤러 + 다리
        float x0 = -46, x1 = BeltX1(t), by = BeltY(t), len = x1 - x0, cx = (x0 + x1) / 2;
        m.AtM(cx, by, 9).RBox(new Vector3(len, 6, 13), 2.5f, STEEL_D, Hex("474d58"), Hex("474d58"));
        m.AtM(cx, by, 12.3f).Box(new Vector3(len - 2, 0.6f, 11), Hex("2a2e36"));
        foreach (float x in new[] { x0, x1 }) m.PipeM(x, by - 6.6f, 9, x, by + 6.6f, 9, 3, shD, false);
        foreach (float x in hi ? new[] { -38f, 0f, 34f } : new[] { -40f, 8f }) m.AtM(x, by, 3).RBox(new Vector3(3, 6, 10), 1, STEEL_D);
        // 충전 헤드: 컨베이어 뒤 기둥에서 앞으로 뻗은 흰 머리(파란 띠, 고급은 금 띠) + 병 바로 위 파란 노즐 둘
        float py = hi ? 12 : 10, hy = by - 2;
        m.AtM(-12, py, 6).Cyl(2.2f, 2.2f, 29, 14, shD, sh, true, sh);
        m.AtM(-12, hy, 31).Bevel(new Vector3(22, 8, 16), 2f, sh, shD, Color.white, Hex("f8f4ee"), sh);
        Color hb = hi ? TRIM : PIPE, hbL = hi ? TRIM_L : Hex("7cc0ea");
        m.AtM(-12, hy, 29.4f).Bevel(new Vector3(22.8f, 2.8f, 16.8f), 0.8f, hb, hb, hbL, hb, hb);
        foreach (float nx in new[] { -18f, -6f })
        {
            m.AtM(nx, by, 23.2f).Cyl(1.7f, 1.7f, 4.4f, 12, PIPE, PIPE);
            m.AtM(nx, by, 21.8f).Cyl(0.9f, 0.9f, 1.6f, 10, STEEL_D, STEEL_D);
        }
        // 병 상자: 기본·개량 하나(3×4) / 고급 둘(3×2씩, 남북으로)
        if (!hi) Crate(m, CRATE_LO.x, CRATE_LO.y, 32, 42);
        else { Crate(m, CRATE_HI_A.x, CRATE_HI_A.y, 30, 22); Crate(m, CRATE_HI_B.x, CRATE_HI_B.y, 30, 22); }
        return m;
    });

    static Mesh BotParts(int t) => ToyMesh.Cached("botParts" + t, m =>
    {
        bool hi = t >= 2;
        if (t == 0)
        {
            // 제어함 앞면 둥근 버튼 둘(파랑·주황): 화면은 개량부터
            m.PipeM(BX - 4, -4.2f, 22, BX - 4, -2.2f, 22, 2.2f, PIPE, false);
            m.PipeM(BX + 4, -4.2f, 22, BX + 4, -2.2f, 22, 2.2f, MachineKit.HEAT, false);
            return m;
        }
        // 작은 탱크 B 받침·테 + 관(위로 솟아 아치에 합류)
        TankFrame(m, TBX, TBY, TBD, 6, 30, hi);
        m.PipeM(TBX, TBY, 32, TBX, TBY, 56, 2.2f, PIPE, false);
        m.PipeM(TBX, TBY, 56, TBX, TAY, 56, 2.2f, PIPE, false);
        m.AtM(TBX, TBY, 56).Ball(2.8f, PIPE);
        // 제어함 앞면 파란 화면(두께 있는 상자), 고급은 금 테 두름
        if (hi) m.AtM(BX, -3.9f, 21).Bevel(new Vector3(16.4f, 11.4f, 2.4f), 0.8f, TRIM, TRIM, TRIM_L, TRIM_L, TRIM);
        m.AtM(BX, -3.4f, 21).Bevel(new Vector3(14, 9, 2.6f), 0.8f, SCREEN, SCREEN, SCREEN_L, SCREEN, SCREEN);
        if (hi)
        {
            // 금 테: 제어함 윗띠 · 관 마디(아치 양 끝) · 탱크 꼭대기 장식 고리
            m.AtM(BX, BY, 29).RBox(new Vector3(BW + 0.9f, 1.8f, BD + 0.9f), 3.4f, TRIM, TRIM_L, TRIM_L);
            foreach (var (x, y, h) in new[] { (TAX, TAY, 47f), (BX, BY, 34f), (TBX, TBY, 37f) }) m.AtM(x, y, h).Cyl(4.6f, 4.6f, 2.6f, 16, TRIM, TRIM_L, true, null, true);
        }
        return m;
    });

    // 로봇 팔(고급): 둥근 받침 → 흰 기둥 → 금 어깨 → 위팔 → 금 팔꿈치 → 아래팔 → 은빛 집게. 피벗은 받침 중심(일할 때 좌우로 까딱)
    static Mesh ArmMesh() => ToyMesh.Cached("botArm", m =>
    {
        m.AtM(0, 0, 0).Cyl(4.8f, 4.2f, 4, 18, GLOSS_D, TRIM, true, GLOSS);
        m.AtM(0, 0, 4).Cyl(2.6f, 2.3f, 23, 14, GLOSS_D, GLOSS, true, GLOSS);
        m.AtM(0, 0, 28).Ball(3.4f, TRIM, TRIM_L);
        m.PipeM(0, 0, 28, 12, 0, 33, 2.2f, GLOSS, false);
        m.AtM(12, 0, 33).Ball(2.7f, TRIM, TRIM_L);
        m.PipeM(12, 0, 33, 18, 0, 29.5f, 1.8f, GLOSS, false);
        m.AtM(18.6f, 0, 27.6f).RBox(new Vector3(4.4f, 3, 5.4f), 1.2f, METAL_D, METAL_L, METAL_L);
        foreach (float fy in new[] { -1.8f, 1.8f }) m.AtM(18.6f, fy, 24.8f).RBox(new Vector3(1.6f, 3.6f, 1.2f), 0.4f, METAL_D, METAL);
        return m;
    });

    public static TierLook Bottler(Stations st)
    {
        var S = GameState.I.S; var b = WorldMap.STN["bottler"];
        var g = MachineKit.Group(st.bottlerView, b, 96, 64, false);
        var a = st.bottlerView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.water >= 10 && S.bottles < GameConfig.BOTTLE_CAP;
        var body = Holder(g, "botBody"); var parts = Holder(g, "botParts");
        // 유리 탱크(뚜껑 없는 관) + 물(바닥 30%는 늘 물: 빈 유리관은 회색으로 읽힌다). B 는 개량부터
        var liq = Prim.Cyl(g, "liquid", MachineKit.L(TAX, TAY, 6 + 20), TAD - 3, 38, GameConfig.BOTTLE);
        a.SetLiquid(liq.transform); a.level = () => 0.3f + 0.7f * S.water / 60f;
        var glA = MachineKit.Tube(g, "glass", MachineKit.L(TAX, TAY, 6), TAD, 40, MachineKit.GLASS); glA.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(MachineKit.GLASS); Prim.NoShadow(glA);
        var liqB = Prim.Cyl(g, "liquidB", MachineKit.L(TBX, TBY, 6 + 15), TBD - 3, 28, GameConfig.BOTTLE);
        a.AddLiquid(liqB.transform);
        var glB = MachineKit.Tube(g, "glassB", MachineKit.L(TBX, TBY, 6), TBD, 30, MachineKit.GLASS); glB.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(MachineKit.GLASS); Prim.NoShadow(glB);
        for (int i = 0; i < 3; i++) { var bb = Prim.Sphere(g, "bubble", MachineKit.L(TAX + (i - 1) * 5, TAY + (i == 1 ? 3 : -2), 10), 3, Hex("eaf8ff")); Prim.NoShadow(bb); a.bubbles.Add(bb.transform); }
        a.bubbleBase = 8 * GameConfig.K; a.bubbleH = 26;
        MachineKit.Knob(g, 12, -21, 32, GameConfig.BOTTLE, a);
        // 컨베이어 위를 지나가는 물병 3개(물건 모형 그대로). 단계마다 벨트 길이·위치가 달라 onTier 에서 다시 놓는다
        var beltBottles = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var bt = new GameObject("beltBottle").transform; bt.SetParent(g, false);
            Items.Slab(bt, "bottle"); a.belt.Add(bt); beltBottles[i] = bt;
        }
        // 로봇 팔(고급): 제어함과 컨베이어 사이, 동쪽 병 상자 쪽으로 뻗는다
        var arm = ToyMesh.Put(g, "robotArm", ArmMesh(), ToyMesh.Mat(0.5f), MachineKit.L(10, 7, 6.2f));
        a.swing.Add(arm.transform); a.swingDeg = 32f;
        // 병 상자 둘째 자리(고급)
        if (st.trayAnchor2 == null) { st.trayAnchor2 = new GameObject("tray2").transform; st.trayAnchor2.SetParent(st.bottlerView, false); }
        var look = new TierLook { squashRoot = st.bottlerView };
        look.Swap(body, BotBody).Swap(parts, BotParts, null, true).Part(glB, 1).Part(liqB.gameObject, 1).Part(arm, 2);
        look.onTier = (t, anim) =>
        {
            float by = BeltY(t), x1 = BeltX1(t);
            a.beltX0 = -42; a.beltX1 = x1 - 4;
            for (int i = 0; i < 3; i++) beltBottles[i].localPosition = MachineKit.L(-42 + i * (x1 - 4 + 42) / 3f, by, 12.5f + Items.ITEM_H["bottle"] / 2);
            // 병 상자 자리: 기본·개량 한 상자 3×4 / 고급 두 상자 3×2(Stations 가 번갈아 채운다)
            float top = FL + 2.2f;
            bool twin = t >= 2;
            var pa = twin ? CRATE_HI_A : CRATE_LO;
            st.trayAnchor.position = WorldMap.W2U(b.x + pa.x, b.y + pa.y, top);
            st.trayAnchor2.position = WorldMap.W2U(b.x + CRATE_HI_B.x, b.y + CRATE_HI_B.y, top);
            st.SetTrayTwin(twin);
        };
        look.Apply(LevelLook.TierOf("bottler"), false);
        return look;
    }

    // ═════════════ 카운터(선반 칸) ═════════════
    // 기본: 밝은 나무 몸통 + 나무 상판(한 톤 진한 테) · 칸 표지는 각진 옅은 판
    // 개량: 하얀 상판(연회색 테) · 칸 표지가 둥근 판
    // 고급: 흰 몸통 + 금 테 상판 · 뒤쪽과 양 끝 유리 진열 벽(금 기둥) · 유리 벽 밑 은은한 조명 띠
    // 상판 윗면 높이는 늘 42(Stations.SHELF_H): 물건 앵커는 단계와 상관없이 같은 자리
    const float CW = 220, CD = 36, SHELF_TOP = 42f;
    static readonly Color WOOD = Hex("d9b38a"), WOOD_D = Hex("bf9467"), WOOD_TOP = Hex("ead2ad"), WOOD_EDGE = Hex("cfa679");

    static Mesh CounterBody(int t) => ToyMesh.Cached("counterBody" + t, m =>
    {
        bool hi = t >= 2;
        Color b = hi ? GLOSS : WOOD, bD = hi ? GLOSS_D : WOOD_D;
        m.AtM(0, 0, 19.5f).RBox(new Vector3(CW, 39, CD), 5, bD, null, b);
        // 상판: 테(둘레 3) + 안쪽 윗면. 테 색: 기본 진한 나무 / 개량 연회색 / 고급 금
        Color edge = t == 0 ? WOOD_EDGE : t == 1 ? Hex("d6d0c6") : TRIM, top = t == 0 ? WOOD_TOP : GLOSS;
        m.AtM(0, 0, 40.3f).RBox(new Vector3(CW + 4, 2.6f, CD + 4), 6, edge, edge, edge);
        m.AtM(0, 0, 41.8f).RBox(new Vector3(CW - 2, 0.4f, CD - 2), 4, top);
        return m;
    });
    static Mesh CounterParts(int t) => t < 2 ? null : ToyMesh.Cached("counterParts" + t, m =>
    {
        // 금 기둥: 네 귀 + 뒤쪽 칸 경계 둘, 머리는 둥근 금 구슬
        float ex = CW / 2 - 1.5f, ey = CD / 2 - 1.5f;
        foreach (var (x, y) in new[] { (-ex, -ey), (ex, -ey), (-ex, ey), (ex, ey), (-CW / 6, -ey), (CW / 6, -ey) })
        {
            m.AtM(x, y, SHELF_TOP).Cyl(1.5f, 1.5f, 9.5f, 12, TRIM, TRIM);
            m.AtM(x, y, SHELF_TOP + 10f).Ball(1.9f, TRIM, TRIM_L);
        }
        // 유리 벽 위 금 난간(뒤쪽)
        m.PipeM(-ex, -ey, SHELF_TOP + 9.6f, ex, -ey, SHELF_TOP + 9.6f, 0.8f, TRIM, false);
        return m;
    });

    public static TierLook Counter(Stations st)
    {
        var c = WorldMap.STN["counter"];
        var g = MachineKit.Group(st.counterView, c, CW, CD, false);
        var body = Holder(g, "counterBody"); var parts = Holder(g, "counterParts");
        // 유리 진열 벽(고급): 뒤쪽 + 양 끝(앞은 손님 쪽이라 열어 둔다). 두께 있는 유리판
        var glass = ToyMesh.Put(g, "counterGlass", ToyMesh.Cached("counterGlass", m =>
        {
            float ex = CW / 2 - 1.5f, ey = CD / 2 - 1.5f;
            m.AtM(0, -ey, SHELF_TOP + 4.8f).RBox(new Vector3(CW - 3, 9.6f, 1.6f), 0.6f, Color.white);
            m.AtM(-ex, 0, SHELF_TOP + 4.8f).RBox(new Vector3(1.6f, 9.6f, CD - 3), 0.6f, Color.white);
            m.AtM(ex, 0, SHELF_TOP + 4.8f).RBox(new Vector3(1.6f, 9.6f, CD - 3), 0.6f, Color.white);
            return m;
        }), CounterGlassMat, Vector3.zero);
        Prim.NoShadow(glass);
        // 조명 띠(고급): 뒤쪽 유리 벽 밑 상판 위 얇은 따뜻한 빛줄(은은하게, 깜빡이지 않음)
        var strip = ToyMesh.Put(g, "lightStrip", ToyMesh.Cached("counterStrip", m => m.AtM(0, -(CD / 2 - 4f), SHELF_TOP + 0.3f).RBox(new Vector3(CW - 10, 0.6f, 2.2f), 0.6f, Color.white)),
            MachineKit.GlowMat(Hex("ffc76e"), 1.1f), Vector3.zero);
        Prim.NoShadow(strip);
        var look = new TierLook { squashRoot = st.counterView, squashAmp = 0.15f };
        look.Swap(body, CounterBody).Swap(parts, CounterParts, null, true).Part(glass, 2).Part(strip, 2);
        look.onTier = (t, anim) => st.DressShelfMarks(t, anim);
        look.Apply(LevelLook.TierOf("shelf"), false);
        return look;
    }
    // 진열장 유리판: 평평한 판은 가장자리 테가 잘 안 생겨 가운데를 조금 더 하늘빛으로(휴게 돔 유리보다 진하게)
    static Material _cGlass;
    static Material CounterGlassMat => _cGlass != null ? _cGlass : (_cGlass = Mats.Glass(new Color(0.84f, 0.94f, 1f, 0.2f), new Color(0.9f, 0.97f, 1f, 0.7f), 2f, 0.8f, 120f));
    // 칸 표지(물 하늘·산소 흰빛·식량 밀색): 기본 각진 판 / 개량 둥근 판 / 고급 둥근 판 + 금 테. 두께 0.7(물건 앵커가 그 위)
    public static Mesh ShelfMarkMesh(int t, Color col, float w, float d) => ToyMesh.Cached("shelfMark" + t + col + w, m =>
    {
        if (t >= 2) m.AtM(0, 0, -0.06f).RBox(new Vector3(w + 2.4f, 0.58f, d + 2.4f), 8f, TRIM, TRIM_L);
        m.AtM(0, 0, 0).RBox(new Vector3(w, 0.7f, d), t == 0 ? 1.2f : 8f, col);
        return m;
    });

    // ═════════════ 계산대(프리미엄 라벨 레벨) ═════════════
    // 기본: 남색 둥근 몸통 + 밝은 화면 하나 · 개량: 위 흰 띠 두 톤 몸통 + 조금 기운 큰 파란 화면 + 카드 단말
    // 고급: 흰 매끈한 몸통 + 남색 아랫단 + 금 테 · 금 테 화면, 화면은 은은하게 스스로 빛남
    static readonly Color REG = Hex("5b6b8c"), REG_D = Hex("46536f"), REG_TOP = Hex("7d8aa6"), BEZEL = Hex("2e3a52"), DISPLAY = Hex("c9f0ff");

    static Mesh RegBody(int t) => ToyMesh.Cached("regBody" + t, m =>
    {
        if (t < 2)
        {
            m.AtM(0, 0, 14).RBox(new Vector3(40, 28, 40), 7, REG_D, null, REG);
            m.AtM(0, 0, 29.2f).RBox(new Vector3(36, 2.4f, 36), 6, t == 0 ? REG_TOP : SHELL);
            if (t == 1) m.AtM(0, 0, 24.5f).RBox(new Vector3(40.4f, 5, 40.4f), 7.2f, SHELL_D, SHELL, SHELL);   // 위 흰 띠(두 톤)
        }
        else
        {
            m.AtM(0, 0, 4).RBox(new Vector3(40.6f, 8, 40.6f), 7.3f, REG_D, null, REG);   // 남색 아랫단
            m.AtM(0, 0, 18).RBox(new Vector3(40, 22, 40), 7, GLOSS_D, null, GLOSS);
            m.AtM(0, 0, 8.6f).RBox(new Vector3(41, 1.6f, 41), 7.5f, TRIM, TRIM_L, TRIM_L);
            m.AtM(0, 0, 29.6f).RBox(new Vector3(37, 2.6f, 37), 6, TRIM, TRIM_L, TRIM_L);
            m.AtM(0, 0, 30.7f).RBox(new Vector3(33, 0.8f, 33), 5, GLOSS);
        }
        return m;
    });
    static Mesh RegParts(int t) => ToyMesh.Cached("regParts" + t, m =>
    {
        // 화면 받침 목 + 화면 틀(남쪽 손님 쪽을 본다, 개량부터 살짝 뒤로 기울임)
        float sw = t == 0 ? 24 : 28, sh = t == 0 ? 14 : 16;
        Color bez = t == 2 ? TRIM : BEZEL, bezL = t == 2 ? TRIM_L : Hex("44506a");
        m.AtM(0, 1, 30).Cyl(2.4f, 2.4f, 5, 12, bez, bez);
        m.At(new Vector3(0, 35 + sh / 2, -5), Quaternion.Euler(t == 0 ? 0 : 14, 0, 0)).Bevel(new Vector3(sw, sh, 4), 1.2f, bez, bez, bezL, bezL, bez);
        if (t >= 1)
        {
            // 카드 단말(동쪽 앞)
            m.AtM(13, 10, 32).RBox(new Vector3(8, 3.4f, 10), 1.5f, t == 2 ? Hex("3a4560") : BEZEL, Hex("5a6680"));
            m.AtM(13, 12.5f, 33.9f).Box(new Vector3(5, 0.5f, 3), t == 2 ? TRIM : Hex("7cc0ea"));
        }
        return m;
    });
    static Mesh RegDisplay(int t) => ToyMesh.Cached("regDisplay" + t, m =>
    {
        float sw = t == 0 ? 20 : 24, sh = t == 0 ? 10 : 12;
        Color face = t == 0 ? DISPLAY : SCREEN, faceL = t == 0 ? Color.white : Hex("8fd0ff");
        m.At(new Vector3(0, 35 + (t == 0 ? 14 : 16) / 2f, -7.2f), Quaternion.Euler(t == 0 ? 0 : 14, 0, 0)).Bevel(new Vector3(sw, sh, 1), 0.4f, face, face, faceL, face, face);
        if (t >= 1) m.At(new Vector3(-3, 35 + 8f + 1f, -7.8f), Quaternion.Euler(14, 0, 0)).Box(new Vector3(12, 2.2f, 0.4f), DISPLAY);   // 화면 속 밝은 줄(금액 칸)
        return m;
    });

    public static TierLook Register(Stations st)
    {
        var r = WorldMap.STN["register"];
        foreach (Transform ch in st.registerView) if (ch.name == "screen") ch.gameObject.SetActive(false);
        var g = MachineKit.Group(st.registerView, r, 40, 40, false);
        var body = Holder(g, "regBody"); var parts = Holder(g, "regParts"); var disp = Holder(g, "regDisplay");
        var look = new TierLook { squashRoot = st.registerView };
        look.Swap(body, RegBody).Swap(parts, RegParts, null, true).Swap(disp, RegDisplay, t => ToyMesh.Mat(0.5f, 0f, t >= 2 ? 0.45f : 0.2f), true);
        look.Apply(LevelLook.TierOf("price"), false);
        return look;
    }

    // ═════════════ 얼음 드릴 ═════════════
    // 바닥판 80×6×60 + 철골 데릭 2단·발전기·드럼통 묶음 (설치 전엔 view가 꺼져 있어 같이 숨는다)
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

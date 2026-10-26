using UnityEngine;

// KayKit 한 장면 시범(2026-10-26, kk-slice): 기지 중심(해빙기·병입기·카운터·계산대·드릴) + 착륙선 + 소품 몇 개를
// 프로 키트(Kay Lousberg, CC0: Space Base Bits·Restaurant Bits) 부품 조합으로 다시 꾸민다. 예전 모형은 -kk 0(StationDress).
// 원칙은 그대로: 투입부(재료가 보임) → 가공부(일할 때 움직임) → 산출 받침대(앞쪽, 금색 테). 충돌·반경·스테이션 좌표는 그대로이고 앵커는 높이만 새 모형에 맞춘다.
// 정적인 부분은 단계마다 ToyMesh 두 덩어리(본체·단계 부품)에 KayKit 메시를 합쳐(ToyMesh.Kk) 그린다: 드로우콜·외곽선·그림자·솟아오름(TierLook) 모두 예전 그대로.
// 우리 게임 고유 부품(얼음 호퍼·유리 물탱크·수조·컨베이어·충전 헤드)은 KayKit 톤(밝은 회색 외피·짙은 회색 틀·주황 띠)으로 최소한만 남긴다.
// 좌표: 기계 좌표 AtM(dx 동, dy 남(카메라 쪽), h 높이), 논리 단위. KayKit 모델 앞면(문·서랍)은 남쪽을 본다.
public static class KayKitDress
{
    static Color Hex(string h) => GameConfig.Hex(h);
    // KayKit 팔레트에 맞춘 우리 부품 색(아틀라스 흰·회·짙은 회 칸을 KK.Warm 으로 살짝 데운 값)
    public static readonly Color KW = Hex("e9e8e1"), KW_D = Hex("c9ccc7"), KG = Hex("8e9597"), KD = Hex("545b5f"), KD_D = Hex("3f4549"),
        KO = Hex("f07b36"), KO_L = Hex("f6a067"), KGOLD = GameConfig.GOLD, KGOLD_L = Hex("f8d77c");
    static readonly Color TRIM = LevelLook.TRIM, TRIM_L = LevelLook.TRIM_L, METAL = LevelLook.METAL, METAL_L = LevelLook.METAL_L, METAL_D = LevelLook.METAL_D, PIPE = MachineKit.PIPE;

    public static void Dress(Stations st)
    {
        st.melterLook = Melter(st);
        st.bottlerLook = Bottler(st);
        st.counterLook = Counter(st);
        st.registerLook = Register(st);
        DressDrill(st.drillView);
    }

    // 받침: KayKit 팔각 지붕판(roofmodule_base)을 시설 발자국만큼 눌러 편 판(높이 BASE)
    const float BASE = 5.6f;
    static void Plinth(ToyMesh m, float w, float d) => m.AtM(0, 0, BASE / 2).Kk("roofmodule_base", new Vector3(w / 1.4f, BASE / 0.2f, d / 1.4f));

    // ═════════════ 해빙기(얼음 → 물) ═════════════
    // 서쪽: KayKit 기지 모듈(basemodule_B, 문이 얼음밭 쪽 서쪽을 보게 돌림) = 화로. 지붕 위 호퍼에 얼음 4×3, 남쪽 벽 불꽃 창이 일하면 빛나고 김이 오른다.
    // 동쪽: 모듈 옆 관 → 둥근 수조(물 높이 = S.water). 단계: 기본 주황 호퍼 테·굴뚝 하나 / 개량 금속 테·굴뚝 둘·파란 물통 묶음(containers_C)
    //  / 고급 금 테·유리 돔·물통 위 태양판(solarpanel)
    const float MX = -14, MY = 0, MS = 26, MTOP = BASE + 0.95f * MS;   // 모듈 중심·스케일, 지붕 안쪽 높이(호퍼 바닥)
    const float HW = 46, HD = 31, WT = 2.4f, WALL = 3.6f;
    static readonly Vector2 BASIN = new Vector2(31, 10);
    const float FACE = MY + 1.04f * MS;   // 모듈 남쪽 벽(dy)

    static Mesh MeltBody(int t) => ToyMesh.Cached("kkMeltBody", m =>
    {
        Plinth(m, 96, 64);
        m.AtM(MX, MY, BASE, 90).Kk("basemodule_B", MS);
        // 호퍼: 지붕 위 짙은 회색 바닥판 + 밝은 회색 벽(앞은 낮게, 얼음이 보이게)
        m.AtM(MX, MY, MTOP + 0.6f).RBox(new Vector3(HW - 2, 1.2f, HD - 2), 3, Hex("b4babb"));
        Color w = KW_D, we = KW;
        m.AtM(MX, MY - HD / 2 + WT / 2, MTOP + WALL / 2).Bevel(new Vector3(HW, WALL, WT), 0.7f, w, w, we, w, w);
        m.AtM(MX - HW / 2 + WT / 2, MY, MTOP + WALL / 2).Bevel(new Vector3(WT, WALL, HD), 0.7f, w, w, we, w, w);
        m.AtM(MX + HW / 2 - WT / 2, MY, MTOP + WALL / 2).Bevel(new Vector3(WT, WALL, HD), 0.7f, w, w, we, w, w);
        m.AtM(MX, MY + HD / 2 - WT / 2, MTOP + 1.2f).Bevel(new Vector3(HW, 2.4f, WT), 0.7f, w, w, we, w, w);
        // 불꽃 창 틀(남쪽 벽): 짙은 회색 테
        m.AtM(MX, FACE + 0.6f, BASE + 10).Bevel(new Vector3(22, 10.5f, 1.6f), 0.5f, KD, KD, KG, KG, KD);
        // 물: 모듈 동쪽 벽 → 수조 관, 수조(밝은 회색 벽 + 짙은 회색 윗테), 수조 → 동쪽 끝 배관
        m.PipeM(MX + 28, BASIN.y - 3, BASE + 10, BASIN.x - 6.5f, BASIN.y - 3, BASE + 10, 1.8f, PIPE);
        m.AtM(BASIN.x - 6, BASIN.y - 3, BASE + 6.2f).Cyl(2.2f, 2.2f, 3.4f, 12, PIPE, PIPE, true, null, true);
        m.AtM(BASIN.x, BASIN.y, BASE).Ring(11.2f, 13.8f, 7.4f, 28, KW, KW_D, KW_D);
        m.AtM(BASIN.x, BASIN.y, BASE + 7.4f).Ring(11.2f, 14.2f, 1.4f, 28, KD, KG, KD);
        m.AtM(BASIN.x, BASIN.y, BASE).Cyl(11.6f, 11.6f, 0.8f, 24, KD, KD);
        return m;
    });

    static Mesh MeltParts(int t) => ToyMesh.Cached("kkMeltParts" + t, m =>
    {
        // 호퍼 윗테: 기본 주황 / 개량 금속 / 고급 금
        Color rim = t == 0 ? KO : t == 1 ? METAL_L : TRIM, rimE = t == 0 ? KO_L : t == 1 ? Hex("e6ebf0") : TRIM_L;
        float rh = t == 0 ? 1.8f : 2.4f, rw = WT + (t == 0 ? 0.8f : 1.6f);
        m.AtM(MX, MY - HD / 2 + WT / 2, MTOP + WALL + rh / 2).Bevel(new Vector3(HW + 1, rh, rw), 0.5f, rim, rim, rimE, rimE, rim);
        m.AtM(MX - HW / 2 + WT / 2, MY, MTOP + WALL + rh / 2).Bevel(new Vector3(rw, rh, HD + 1), 0.5f, rim, rim, rimE, rimE, rim);
        m.AtM(MX + HW / 2 - WT / 2, MY, MTOP + WALL + rh / 2).Bevel(new Vector3(rw, rh, HD + 1), 0.5f, rim, rim, rimE, rimE, rim);
        m.AtM(MX, MY + HD / 2 - WT / 2, MTOP + 2.4f + rh / 2).Bevel(new Vector3(HW + 1, rh, rw), 0.5f, rim, rim, rimE, rimE, rim);
        // 불꽃 창 창살(단계마다 굵기·수)
        int nb = t == 0 ? 4 : 5; float span = 15f; Color bar = t == 2 ? TRIM : KD;
        for (int i = 0; i < nb; i++) m.AtM(MX - span / 2 + i * span / (nb - 1), FACE + 1.6f, BASE + 6f).Cyl(0.8f, 0.8f, 8f, 10, bar, bar);
        // 굴뚝: 모듈 뒤(북) 동쪽에서 솟는 짙은 회색 관 + 주황 띠(개량부터 둘, 고급은 금 띠)
        Color band = t == 0 ? KO : t == 1 ? METAL_L : TRIM;
        var stacks = t == 0 ? new[] { (21f, 34f) } : new[] { (21f, 34f), (30f, 27f) };
        foreach (var (cx, hh) in stacks)
        {
            m.AtM(cx, -25, BASE).Cyl(3.6f, 3.2f, hh, 14, KD_D, KG, true, KG);
            m.AtM(cx, -25, BASE + hh * 0.62f).Band(3.5f, 2.6f, 14, band);
            m.AtM(cx, -25, BASE + hh).Cyl(4.6f, 3.4f, 2.2f, 14, KD, KD, true, null, true);
        }
        if (t >= 1) m.AtM(40, -12, BASE).Kk("containers_C", new Vector3(34, 40, 34));   // 파란 물통 묶음(물 저장)
        if (t >= 2) m.AtM(40, -12, BASE + 8f).Kk("solarpanel", 19);   // 물통 위 태양판
        return m;
    });

    // 유리 돔 하이라이트(StationDress.MeltDomeHi 와 같은 값): 바닥 테 + 북서쪽 굽은 반사 줄 + 반짝 점 + 남동쪽 옅은 반사
    static Color DomeHi(int i, int k)
    {
        float a = i * 9f, tt = k / 10f;
        float Bump(float x, float c, float wd) => Mathf.Clamp01((1f - Mathf.Abs(x - c) / wd) * 1.6f);
        float Ang(float c) => Mathf.Abs(Mathf.DeltaAngle(a, c));
        float hi = k == 0 ? 0.65f : k == 1 ? 0.18f : 0f;
        hi = Mathf.Max(hi, Bump(Ang(138f), 0f, 38f) * Bump(tt, 0.55f, 0.2f));
        hi = Mathf.Max(hi, Bump(Ang(80f), 0f, 16f) * Bump(tt, 0.82f, 0.14f));
        hi = Mathf.Max(hi, 0.45f * Bump(Ang(315f), 0f, 34f) * Bump(tt, 0.22f, 0.13f));
        return new Color(1f, 1f, 1f, hi);
    }

    static TierLook Melter(Stations st)
    {
        var S = GameState.I.S; var mm = WorldMap.STN["melter"];
        var g = MachineKit.Group(st.melterView, mm, 96, 64, false, "core");
        var a = st.melterView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.meltQ > 0;
        var body = LevelLook.Holder(g, "meltBody"); var parts = LevelLook.Holder(g, "meltParts");
        st.meltAnchor.position = WorldMap.W2U(mm.x + MX, mm.y + MY, MTOP + 1.2f);
        // 불꽃 창 발광(일하면 주황, 고급은 더 밝게)
        var glow = Prim.Box(g, "fireGlow", MachineKit.L(MX, FACE + 0.9f, BASE + 10), new Vector3(18, 7.4f, 1.4f), MachineKit.HEAT_OFF);
        Prim.NoShadow(glow); var glowR = glow.GetComponent<Renderer>();
        Material glowOn = MachineKit.GlowMat(MachineKit.HEAT, 1.5f), glowHi = MachineKit.GlowMat(Hex("ffa04a"), 2.1f);
        a.AddGlowMat(glowR, glowOn, Mats.Solid(MachineKit.HEAT_OFF));
        // 김: 일할 때 호퍼에서 피어올라 흩어진다
        var puff = ToyMesh.Cached("steamPuff", m => m.Blob(Vector3.one * (0.5f / GameConfig.K), 1, 21, 0.1f, Color.white, Color.white, Color.white));
        var steamMat = Mats.TransShared(new Color(1f, 0.99f, 0.97f, 0.55f));
        for (int i = 0; i < 4; i++)
        {
            var p = ToyMesh.Put(g, "steam", puff, steamMat, MachineKit.L(MX + (i % 2 == 0 ? -11 : 9), MY + (i < 2 ? -5 : 4), MTOP));
            Prim.NoShadow(p); a.bubbles.Add(p.transform);
        }
        a.bubbleBase = (MTOP + 4) * GameConfig.K; a.bubbleH = 22; a.bubbleS0 = 9f; a.bubbleS1 = 3.5f;
        // 물줄기(일할 때만) · 수조 물(바닥 30%는 늘 물)
        var stream = Prim.Cyl(g, "stream", MachineKit.L(BASIN.x - 6, BASIN.y - 3, BASE + 3.6f), 2.4f, 6.4f, Hex("8fd3f5"));
        Prim.NoShadow(stream); a.showWhenWorking.Add(stream.transform);
        var liq = Prim.Cyl(g, "liquid", MachineKit.L(BASIN.x, BASIN.y, BASE + 0.8f + 3.2f), 22.6f, 6.4f, GameConfig.BOTTLE);
        a.SetLiquid(liq.transform); a.level = () => 0.3f + 0.7f * S.water / 60f;
        MachineKit.Knob(g, MX - 18, MY - 11, MTOP + WALL + 1.8f, MachineKit.HEAT, a);
        // 고급: 호퍼를 덮는 낮은 유리 돔
        var dome = ToyMesh.Put(g, "glassDome", ToyMesh.Cached("kkMeltDome", m => m.Dome(26.5f, 18f, 12f, DomeHi, 40, 10)), LevelLook.GlassHiMat, MachineKit.L(MX, MY, MTOP + WALL + 2.4f));
        Prim.NoShadow(dome);
        var look = new TierLook { squashRoot = st.melterView };
        look.Swap(body, MeltBody).Swap(parts, MeltParts, null, true).Part(dome, 2);
        look.onTier = (t, anim) => a.SetGlowOn(glowR, t >= 2 ? glowHi : glowOn);
        look.Apply(LevelLook.TierOf("melter"), false);
        return look;
    }

    // ═════════════ 병입기(물 → 물병) ═════════════
    // 서쪽 뒤: 키 큰 유리 물탱크(S.water) → 파란 관 아치 → KayKit 기지 모듈(basemodule_C, 문이 남쪽 컨베이어를 본다) = 충전실.
    // 문 앞 컨베이어 위를 물병이 지나 동쪽 나무 상자(Restaurant crate, 금색 윗테 = 산출 받침대)에 쌓인다.
    // 단계: 기본 탱크 하나·상자 하나(3×4) / 개량 지붕 위 파란 물통 묶음 / 고급 금 테·긴 컨베이어·상자 둘(3×2씩)·로봇 팔
    const float TAX = -33, TAY = -14, TAD = 24;
    const float BX = -2, BY = -12, BS = 16, BROOF = BASE + 0.95f * BS;
    const float CFL = 13.6f;   // 상자 받침 윗면(상자 바닥)
    static readonly Vector2 CRATE_LO = StationDress.CRATE_LO, CRATE_HI_A = StationDress.CRATE_HI_A, CRATE_HI_B = StationDress.CRATE_HI_B;
    static float BeltX1(int t) => t >= 2 ? 42 : 14;
    static float BeltY(int t) => t >= 2 ? 23 : 20;

    static void TankFrame(ToyMesh m, float dx, float dy, float dia, float h0, float h, bool hi)
    {
        m.AtM(dx, dy, BASE).Cyl((dia + 4) / 2, (dia + 4) / 2, h0 - BASE, 24, KW_D, KW);
        Color rim = hi ? TRIM : KD, rimT = hi ? TRIM_L : KG;
        m.AtM(dx, dy, h0 + h - 2.2f).Ring(dia / 2 - 1.2f, dia / 2 + 1.4f, 3f, 24, rim, rimT, rim);
        m.AtM(dx, dy, h0).Ring(dia / 2 - 1.2f, dia / 2 + 1.4f, 3f, 24, rim, rimT, rim);
    }
    // 병 상자: 짙은 회색 받침 + KayKit 나무 상자 + 금색 윗테(산출 받침대 규칙)
    static void Crate(ToyMesh m, float cx, float cy, float cw, float cd)
    {
        m.AtM(cx, cy, BASE + (CFL - BASE) / 2).RBox(new Vector3(cw - 6, CFL - BASE, cd - 6), 2.5f, KD_D, KD, KD);
        const float CH = 7f;
        m.AtM(cx, cy, CFL).Kk("crate", new Vector3(cw / 2f, CH / 0.8f, cd / 2f));
        float top = CFL + CH, wt = 0.11f * cw;
        Color gd = KGOLD, gl = KGOLD_L;
        m.AtM(cx, cy - cd / 2 + 1.1f, top + 0.5f).Bevel(new Vector3(cw + 0.4f, 1.2f, 2.4f), 0.4f, gd, gd, gl, gl, gd);
        m.AtM(cx, cy + cd / 2 - 1.1f, top + 0.5f).Bevel(new Vector3(cw + 0.4f, 1.2f, 2.4f), 0.4f, gd, gd, gl, gl, gd);
        m.AtM(cx - cw / 2 + 1.1f, cy, top + 0.5f).Bevel(new Vector3(2.4f, 1.2f, cd + 0.4f), 0.4f, gd, gd, gl, gl, gd);
        m.AtM(cx + cw / 2 - 1.1f, cy, top + 0.5f).Bevel(new Vector3(2.4f, 1.2f, cd + 0.4f), 0.4f, gd, gd, gl, gl, gd);
    }

    static Mesh BotBody(int t) => ToyMesh.Cached("kkBotBody" + t, m =>
    {
        bool hi = t >= 2;
        Plinth(m, 96, 64);
        TankFrame(m, TAX, TAY, TAD, 8, 40, hi);
        m.AtM(BX, BY, BASE).Kk("basemodule_C", BS);
        // 탱크 → 모듈 지붕 파란 관 아치
        m.PipeM(TAX, TAY, 48, TAX, TAY, 56, 3, PIPE, false);
        m.PipeM(BX - 5, BY, BROOF, BX - 5, BY, 56, 3, PIPE, false);
        m.PipeM(TAX, TAY, 56, BX - 5, BY, 56, 3, PIPE, false);
        m.AtM(TAX, TAY, 56).Ball(3.6f, PIPE); m.AtM(BX - 5, BY, 56).Ball(3.6f, PIPE);
        m.AtM(BX - 5, BY, BROOF).Cyl(4.6f, 4.6f, 2f, 14, KD, KG, true, null, true);
        // 컨베이어(문 앞, 서 → 동): 짙은 회색 몸통 + 검은 벨트 + 양 끝 롤러 + 다리
        float x0 = -46, x1 = BeltX1(t), by = BeltY(t), len = x1 - x0, cx = (x0 + x1) / 2;
        m.AtM(cx, by, 9).RBox(new Vector3(len, 6, 13), 2.5f, KD_D, KD, KD);
        m.AtM(cx, by, 12.3f).Box(new Vector3(len - 2, 0.6f, 11), Hex("2a2e33"));
        foreach (float x in new[] { x0, x1 }) m.PipeM(x, by - 6.6f, 9, x, by + 6.6f, 9, 3, KG, false);
        foreach (float x in hi ? new[] { -38f, 0f, 34f } : new[] { -40f, 8f }) m.AtM(x, by, BASE).RBox(new Vector3(3, 4, 10), 1, KD_D);
        // 충전 헤드: 벨트 뒤 기둥에서 앞으로 뻗은 머리(주황 띠, 고급은 금 띠) + 파란 노즐 둘
        float hy = by - 2;
        m.AtM(-26, hy - 9, BASE).Cyl(2.2f, 2.2f, 26, 14, KD, KG, true, KG);
        m.AtM(-22, hy, 31).Bevel(new Vector3(20, 7, 15), 2f, KG, KD, Hex("b4babb"), Hex("a3aaac"), KG);
        Color hb = hi ? TRIM : KO, hbL = hi ? TRIM_L : KO_L;
        m.AtM(-22, hy, 29.6f).Bevel(new Vector3(20.8f, 2.6f, 15.8f), 0.8f, hb, hb, hbL, hb, hb);
        foreach (float nx in new[] { -27f, -17f })
        {
            m.AtM(nx, by, 23.6f).Cyl(1.7f, 1.7f, 4.4f, 12, PIPE, PIPE);
            m.AtM(nx, by, 22.2f).Cyl(0.9f, 0.9f, 1.6f, 10, KD, KD);
        }
        if (!hi) Crate(m, CRATE_LO.x, CRATE_LO.y, 32, 42);
        else { Crate(m, CRATE_HI_A.x, CRATE_HI_A.y, 30, 22); Crate(m, CRATE_HI_B.x, CRATE_HI_B.y, 30, 22); }
        return m;
    });

    static Mesh BotParts(int t) => ToyMesh.Cached("kkBotParts" + t, m =>
    {
        bool hi = t >= 2;
        // 기본: 지붕 위 작은 표시등 상자 / 개량부터: 지붕 위 파란 물통 묶음(containers_C, 물 저장 하나 더) / 고급: 금 테(탱크·관 마디)
        if (t == 0) { m.AtM(BX + 6, BY + 2, BROOF).RBox(new Vector3(9, 3.4f, 7), 1.4f, KD, KG); return m; }
        m.AtM(BX + 6, BY + 3, BROOF - 0.4f).Kk("containers_C", new Vector3(32, 36, 32));
        if (hi)
        {
            m.AtM(TAX, TAY, 47).Cyl(13.6f, 13.6f, 2.6f, 24, TRIM, TRIM_L, true, null, true);
            m.AtM(BX - 5, BY, BROOF + 2).Cyl(4.8f, 4.8f, 2f, 14, TRIM, TRIM_L, true, null, true);
        }
        return m;
    });

    static TierLook Bottler(Stations st)
    {
        var S = GameState.I.S; var b = WorldMap.STN["bottler"];
        var g = MachineKit.Group(st.bottlerView, b, 96, 64, false);
        var a = st.bottlerView.gameObject.AddComponent<MachineAnim>();
        a.working = () => S.water >= 10 && S.bottles < GameConfig.BOTTLE_CAP;
        var body = LevelLook.Holder(g, "botBody"); var parts = LevelLook.Holder(g, "botParts");
        var liq = Prim.Cyl(g, "liquid", MachineKit.L(TAX, TAY, 8 + 20), TAD - 3, 38, GameConfig.BOTTLE);
        a.SetLiquid(liq.transform); a.level = () => 0.3f + 0.7f * S.water / 60f;
        var glA = MachineKit.Tube(g, "glass", MachineKit.L(TAX, TAY, 8), TAD, 40, MachineKit.GLASS); glA.GetComponent<Renderer>().sharedMaterial = Mats.TransShared(MachineKit.GLASS); Prim.NoShadow(glA);
        for (int i = 0; i < 3; i++) { var bb = Prim.Sphere(g, "bubble", MachineKit.L(TAX + (i - 1) * 5, TAY + (i == 1 ? 3 : -2), 12), 3, Hex("eaf8ff")); Prim.NoShadow(bb); a.bubbles.Add(bb.transform); }
        a.bubbleBase = 10 * GameConfig.K; a.bubbleH = 26;
        MachineKit.Knob(g, BX - 11, BY + 6, BROOF, GameConfig.BOTTLE, a);
        var beltBottles = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var bt = new GameObject("beltBottle").transform; bt.SetParent(g, false);
            Items.Slab(bt, "bottle"); a.belt.Add(bt); beltBottles[i] = bt;
        }
        var arm = ToyMesh.Put(g, "robotArm", StationDress.ArmMesh(), ToyMesh.Mat(0.5f), MachineKit.L(10, 7, BASE));
        a.swing.Add(arm.transform); a.swingDeg = 32f;
        if (st.trayAnchor2 == null) { st.trayAnchor2 = new GameObject("tray2").transform; st.trayAnchor2.SetParent(st.bottlerView, false); }
        var look = new TierLook { squashRoot = st.bottlerView };
        look.Swap(body, BotBody).Swap(parts, BotParts, null, true).Part(arm, 2);
        look.onTier = (t, anim) =>
        {
            float by = BeltY(t), x1 = BeltX1(t);
            a.beltX0 = -42; a.beltX1 = x1 - 4;
            for (int i = 0; i < 3; i++) beltBottles[i].localPosition = MachineKit.L(-42 + i * (x1 - 4 + 42) / 3f, by, 12.5f + Items.ITEM_H["bottle"] / 2);
            float top = CFL + 0.1f * 7f / 0.8f;   // 상자 안 바닥(crate 바닥판 0.1)
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
    // Restaurant Bits 주방 카운터(서랍 A·B 번갈아) 6칸을 220×36 에 맞춰 잇는다. 상판 주황 나무 → 단계 색(기본 밝은 나무 / 개량·고급 하얀 상판),
    // 고급은 금 기둥·뒤 유리 진열 벽·조명 띠(예전과 같은 부품, 높이만 새 상판에). 상판 윗면 = Stations.ShelfH(KayKit 32)
    public const float CW = 220, CD = 36, KH = 32f;
    static Mesh CounterBody(int t) => ToyMesh.Cached("kkCounterBody" + t, m =>
    {
        Color top = t == 0 ? Hex("dcb88c") : LevelLook.GLOSS;
        var re = KK.Hue(0, 45, top, 0.78f);
        float pw = CW / 6f;
        for (int i = 0; i < 6; i++)
            m.AtM(-CW / 2 + (i + 0.5f) * pw, 0, 0).Kk(i % 2 == 0 ? "kitchencounter_straight_B" : "kitchencounter_straight_A", new Vector3(pw / 2f + 0.02f, KH, CD / 2.04f), re);
        // 상판 테: 개량 연회색 / 고급 금(얇은 띠)
        if (t >= 1)
        {
            Color e = t == 1 ? Hex("d6d0c6") : TRIM, eL = t == 1 ? Hex("ebe6de") : TRIM_L;
            m.AtM(0, 0, KH - 0.9f).RBox(new Vector3(CW + 1.6f, 1.4f, CD + 1.6f), 2.5f, e, eL, eL);
        }
        return m;
    });
    static Mesh CounterParts(int t) => t < 2 ? null : ToyMesh.Cached("kkCounterParts" + t, m =>
    {
        float ex = CW / 2 - 1.5f, ey = CD / 2 - 1.5f;
        foreach (var (x, y) in new[] { (-ex, -ey), (ex, -ey), (-ex, ey), (ex, ey), (-CW / 6, -ey), (CW / 6, -ey) })
        {
            m.AtM(x, y, KH).Cyl(1.5f, 1.5f, 9.5f, 12, TRIM, TRIM);
            m.AtM(x, y, KH + 10f).Ball(1.9f, TRIM, TRIM_L);
        }
        m.PipeM(-ex, -ey, KH + 9.6f, ex, -ey, KH + 9.6f, 0.8f, TRIM, false);
        return m;
    });

    static TierLook Counter(Stations st)
    {
        var c = WorldMap.STN["counter"];
        var g = MachineKit.Group(st.counterView, c, CW, CD, false);
        var body = LevelLook.Holder(g, "counterBody"); var parts = LevelLook.Holder(g, "counterParts");
        var glass = ToyMesh.Put(g, "counterGlass", ToyMesh.Cached("kkCounterGlassHi", m =>
        {
            float ex = CW / 2 - 1.5f, ey = CD / 2 - 1.5f;
            Color clear = new Color(1f, 1f, 1f, 0f), glow = new Color(1f, 1f, 1f, 0.4f), rim = new Color(1f, 1f, 1f, 1f);
            m.AtM(0, -ey, KH + 4.8f).RBox(new Vector3(CW - 3, 9.6f, 1.6f), 0.6f, clear, rim, glow);
            m.AtM(-ex, 0, KH + 4.8f).RBox(new Vector3(1.6f, 9.6f, CD - 3), 0.6f, clear, rim, glow);
            m.AtM(ex, 0, KH + 4.8f).RBox(new Vector3(1.6f, 9.6f, CD - 3), 0.6f, clear, rim, glow);
            foreach (float x in new[] { -78f, -6f, 66f })
            {
                m.At(new Vector3(x, KH + 4.8f, ey - 1.0f), new Vector3(0, 0, -32)).Box(new Vector3(1.8f, 7f, 0.25f), new Color(1f, 1f, 1f, 0.75f));
                m.At(new Vector3(x + 3.4f, KH + 5.2f, ey - 1.0f), new Vector3(0, 0, -32)).Box(new Vector3(0.7f, 5f, 0.25f), new Color(1f, 1f, 1f, 0.6f));
            }
            return m;
        }), StationDress.CounterGlass, Vector3.zero);
        Prim.NoShadow(glass);
        var strip = ToyMesh.Put(g, "lightStrip", ToyMesh.Cached("kkCounterStrip", m => m.AtM(0, -(CD / 2 - 4f), KH + 0.3f).RBox(new Vector3(CW - 10, 0.6f, 2.2f), 0.6f, Color.white)),
            MachineKit.GlowMat(Hex("ffc76e"), 1.1f), Vector3.zero);
        Prim.NoShadow(strip);
        var look = new TierLook { squashRoot = st.counterView, squashAmp = 0.15f };
        look.Swap(body, CounterBody).Swap(parts, CounterParts, null, true).Part(glass, 2).Part(strip, 2);
        look.onTier = (t, anim) => st.DressShelfMarks(t, anim);
        look.Apply(LevelLook.TierOf("shelf"), false);
        return look;
    }

    // ═════════════ 계산대(프리미엄 라벨 레벨) ═════════════
    // 카운터와 같은 Restaurant 주방 칸 하나(40×40) + 상판 색(기본 남색 / 개량 흰 / 고급 흰 + 금 테), 화면·카드 단말은 예전 부품을 새 상판 높이로 내려 얹는다
    const float RH = 26f, REG_OLD_TOP = 30.4f;
    static Mesh RegBody(int t) => ToyMesh.Cached("kkRegBody" + t, m =>
    {
        Color top = t == 0 ? Hex("5b6b8c") : LevelLook.GLOSS;
        m.Kk("kitchencounter_straight_A", new Vector3(20, RH, 40 / 2.04f), KK.Hue(0, 45, top, 0.78f));
        if (t >= 2) m.AtM(0, 0, RH - 0.9f).RBox(new Vector3(41.4f, 1.4f, 41.4f), 2.5f, TRIM, TRIM_L, TRIM_L);
        return m;
    });

    static TierLook Register(Stations st)
    {
        var r = WorldMap.STN["register"];
        foreach (Transform ch in st.registerView) if (ch.name == "screen") ch.gameObject.SetActive(false);
        var g = MachineKit.Group(st.registerView, r, 40, 40, false);
        var body = LevelLook.Holder(g, "regBody");
        var up = new GameObject("kkUp").transform; up.SetParent(g, false); up.localPosition = new Vector3(0, (RH - REG_OLD_TOP) * GameConfig.K, 0);
        var parts = LevelLook.Holder(up, "regParts"); var disp = LevelLook.Holder(up, "regDisplay");
        var look = new TierLook { squashRoot = st.registerView };
        look.Swap(body, RegBody).Swap(parts, StationDress.RegParts, null, true).Swap(disp, StationDress.RegDisplay, t => ToyMesh.Mat(0.5f, 0f, t >= 2 ? 0.45f : 0.2f), true);
        look.Apply(LevelLook.TierOf("price"), false);
        return look;
    }

    // ═════════════ 얼음 드릴 ═════════════
    // KayKit 드릴 구조물(drill_structure, 빨간 다리 철골 + 가운데 드릴 머리) + 팔각 받침 + 옆 회색 화물 상자
    public static void DressDrill(Transform drillView)
    {
        if (drillView == null || drillView.Find("models") != null) return;
        var d = WorldMap.STN["drill"];
        foreach (Transform c in drillView) if (c.name == "body" || c.name == "top") c.gameObject.SetActive(false);
        var g = new GameObject("models").transform; g.SetParent(drillView, false);
        g.position = WorldMap.W2U(d.x, d.y, 0f);
        var mesh = ToyMesh.Cached("kkDrill", m =>
        {
            Plinth(m, 80, 60);
            m.AtM(-4, 0, BASE).Kk("drill_structure", 30);
            m.AtM(30, 16, BASE, 20).Kk("cargo_B_stacked", 15);
            return m;
        });
        ToyMesh.Put(g, "drill", mesh, ToyMesh.Mat(0.15f), Vector3.zero);
    }

    // ═════════════ 착륙선 ═════════════
    // 팔각 착륙장(landingpad_large) 위 KayKit 착륙선(lander_A) + 꼭대기 빨간 표시등. 기계처럼 외곽선을 받게 배경 루트(World) 밖에 둔다
    public static void Lander(float x, float y)
    {
        var root = new GameObject("KKLander").transform;
        root.position = WorldMap.W2U(x, y, 0f);
        var mesh = ToyMesh.Cached("kkLander", m =>
        {
            m.Kk("landingpad_large", new Vector3(30, 24, 30));
            m.AtM(0, 0, 12, 200).Kk("lander_A", 40);
            return m;
        });
        ToyMesh.Put(root, "lander", mesh, ToyMesh.Mat(0.2f), Vector3.zero);
        var bk = Prim.Sphere(root, "beacon", new Vector3(0, 12 + 1.19f * 40 + 3, 0) * GameConfig.K, 5, ModelLib.C(0xff5a3c)); Prim.NoShadow(bk);
    }

    // ═════════════ 소품(과하지 않게, 외곽선 없음) ═════════════
    // 해빙·병입 구역 빈 모서리에 화물 상자 더미 둘, 얼음밭 서쪽 가장자리 밖에 돌 둘
    public static void Props(Transform worldRoot)
    {
        void Put(string key, float x, float y, float ry, System.Func<ToyMesh, ToyMesh> build)
        {
            var go = ToyMesh.Put(worldRoot, key, ToyMesh.Cached(key, build), ToyMesh.Mat(0.1f), WorldMap.W2U(x, y, 0f) - worldRoot.position);
            go.transform.localRotation = Quaternion.Euler(0, ry, 0);
        }
        Put("kkPropCargoA", 364, 390, 12, m => m.Kk("cargo_A_stacked", 16));
        Put("kkPropCargoB", 270, 62, -8, m => m.Kk("cargo_B_stacked", 15));
        var rock = KK.Hue(0, 40, WorldDress.ROCK_M, 0.75f);
        Put("kkPropRocksA", 2, 262, 70, m => m.Kk("rocks_A", 20, rock));
        Put("kkPropRockB", 4, 380, 20, m => m.Kk("rock_B", 18, rock));
    }
}

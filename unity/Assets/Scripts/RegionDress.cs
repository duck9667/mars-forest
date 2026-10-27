using UnityEngine;

// 3·4장 지역 모형(docs/chapter3-design.md): KayKit Space Base Bits(Kay Lousberg, CC0) 부품을 ToyMesh.Kk로 한 메시에 합친다(외곽선·그림자·인스턴싱 그대로).
// 우리 고유 부품(로켓 몸통·불꽃·호퍼 벽·산출 받침대)은 KayKit 톤(밝은 회색 외피·짙은 회색 틀·주황 띠)으로 최소한만.
// 좌표: AtM(dx 동, dy 남(카메라 쪽), h 높이), 논리 단위. KayKit 앞면(문·서랍)은 남쪽(카메라)을 본다. 1단위 = 논리 22~40.
public static class RegionDress
{
    static Color Hex(string h) => GameConfig.Hex(h);
    static readonly Color KW = KayKitDress.KW, KW_D = KayKitDress.KW_D, KG = KayKitDress.KG, KD = KayKitDress.KD, KD_D = KayKitDress.KD_D, KO = KayKitDress.KO, KO_L = KayKitDress.KO_L;
    static readonly Color GOLD = GameConfig.GOLD, GOLD_L = Hex("f8d77c");

    public static GameObject Put(Transform parent, string name, Mesh mesh, float gloss = 0.15f, float self = 0f)
        => ToyMesh.Put(parent, name, mesh, ToyMesh.Mat(gloss, 0f, self), Vector3.zero);

    // 산출 받침대: 낮은 회색 받침 + 금색 윗테(아트 바이블: 금색은 산출·상호작용에만)
    static void Tray(ToyMesh m, float dx, float dy, float w, float d)
    {
        m.AtM(dx, dy, 1.6f).RBox(new Vector3(w, 3.2f, d), 3f, KW_D, KW);
        m.AtM(dx, dy, 3.4f).RBox(new Vector3(w + 1.2f, 0.8f, d + 1.2f), 3.4f, GOLD, GOLD_L);
        m.AtM(dx, dy, 3.6f).RBox(new Vector3(w - 3f, 0.6f, d - 3f), 2f, KD, KG);
    }
    // 지붕 호퍼(원료가 보이게 앞벽은 낮게): 짙은 바닥 + 밝은 벽 + 주황 윗테
    static void Hopper(ToyMesh m, float dx, float dy, float h, float w, float d)
    {
        const float WT = 2.2f, WALL = 4f;
        m.AtM(dx, dy, h + 0.5f).RBox(new Vector3(w - 2, 1f, d - 2), 2.5f, Hex("b4babb"));
        m.AtM(dx, dy - d / 2 + WT / 2, h + WALL / 2).Bevel(new Vector3(w, WALL, WT), 0.6f, KW_D, KW_D, KW, KW_D, KW_D);
        m.AtM(dx - w / 2 + WT / 2, dy, h + WALL / 2).Bevel(new Vector3(WT, WALL, d), 0.6f, KW_D, KW_D, KW, KW_D, KW_D);
        m.AtM(dx + w / 2 - WT / 2, dy, h + WALL / 2).Bevel(new Vector3(WT, WALL, d), 0.6f, KW_D, KW_D, KW, KW_D, KW_D);
        m.AtM(dx, dy + d / 2 - WT / 2, h + 1.2f).Bevel(new Vector3(w, 2.4f, WT), 0.6f, KW_D, KW_D, KW, KW_D, KW_D);
        m.AtM(dx, dy - d / 2 + WT / 2, h + WALL + 0.8f).Bevel(new Vector3(w + 1, 1.6f, WT + 0.8f), 0.4f, KO, KO, KO_L, KO_L, KO);
        m.AtM(dx - w / 2 + WT / 2, dy, h + WALL + 0.8f).Bevel(new Vector3(WT + 0.8f, 1.6f, d + 1), 0.4f, KO, KO, KO_L, KO_L, KO);
        m.AtM(dx + w / 2 - WT / 2, dy, h + WALL + 0.8f).Bevel(new Vector3(WT + 0.8f, 1.6f, d + 1), 0.4f, KO, KO, KO_L, KO_L, KO);
    }

    // ═════════════ 관문: 둥근 기둥 둘(밝은 회색 + 주황 띠 + 짙은 받침·머리, 꼭대기 빨간 등은 따로) · 차단봉은 따로(올라가는 연출) ═════════════
    public const float GATE_POST_H = 30f, GATE_HALF = 30f;   // 기둥 높이, 길 가운데에서 기둥까지(논리 y)
    public static Mesh Gate() => ToyMesh.Cached("rgGate", m =>
    {
        foreach (float s in new[] { -1f, 1f })
        {
            m.AtM(0, s * GATE_HALF, 0).Cyl(7.5f, 7f, 4f, 16, KD, KG, true, KD);
            m.AtM(0, s * GATE_HALF, 4f).Cyl(4.6f, 4.2f, GATE_POST_H - 8f, 16, KW_D, KW, true, KW);
            m.AtM(0, s * GATE_HALF, 12f).Band(4.7f, 4f, 16, KO);
            m.AtM(0, s * GATE_HALF, GATE_POST_H - 4f).Cyl(5.6f, 5.2f, 4f, 16, KD, KG, true, KD);
        }
        return m;
    });
    // 차단봉: 피벗(북쪽 기둥 축)에서 남쪽으로 뻗은 굵은 봉, 주황·흰 줄무늬. 닫힘 = 수평, 열림 = 위로 80°
    public static Mesh Boom() => ToyMesh.Cached("rgBoom", m =>
    {
        float L = GATE_HALF * 2f + 6f; int n = 6;
        for (int i = 0; i < n; i++)
        {
            Color c = i % 2 == 0 ? KO : KW, cl = i % 2 == 0 ? KO_L : Color.white;
            m.At(0, 0, -(i + 0.5f) * L / n).RBox(new Vector3(3.6f, 3.6f, L / n), 1.2f, c, cl);
        }
        m.At(0, 0, 0).Cyl(3.4f, 3.4f, 2f, 12, KD, KG);
        return m;
    });

    // ═════════════ 착륙장(팔각) ═════════════
    public const float PAD_TOP = 12f;   // 착륙장 윗면 높이(우주선 다리 바닥)
    public static Mesh LandingPad() => ToyMesh.Cached("rgPad", m =>
    {
        m.Kk("landingpad_large", new Vector3(32, 24, 32));
        return m;
    });
    // 화물 우주선: 금빛 돔 착륙선(lander_B) + 짐칸 상자 둘. 피벗 = 다리 바닥
    public static Mesh Ship() => ToyMesh.Cached("rgShip", m =>
    {
        m.At(0, 0, 0, 200).Kk("lander_B", 38);
        m.AtM(-22, 8, 0, 15).Kk("cargo_A_packed", 13);
        return m;
    });
    public const float SHIP_ENGINE_H = 4f;   // 불꽃 붙는 높이(우주선 피벗 기준)
    // 화물선 역추진: 가운데 큰 불꽃 + 다리 끝 작은 불꽃 넷(몸통이 넓어 카메라(남쪽 위 55°)에선 가운데 불꽃이 몸통에 가리므로 앞 다리 불꽃이 보이게)
    public static Mesh ShipFlames() => ToyMesh.Cached("rgShipFlames", m =>
    {
        m.At(0, -36f, 0).Cyl(0.8f, 11f, 36f, 16, Hex("ff9a3a"), Hex("ffd27a"), true, Hex("ffc35a"));
        m.At(0, -24f, 0).Cyl(0.5f, 6.5f, 24.5f, 12, Hex("fff1b8"), Hex("fff6d6"), true, Hex("ffe58a"));
        foreach (var (x, z) in new[] { (-17f, -17f), (17f, -17f), (-17f, 17f), (17f, 17f) })
        {
            m.At(x, -15f, z).Cyl(0.4f, 3.6f, 16f, 10, Hex("ff9a3a"), Hex("ffd27a"), true, Hex("ffc35a"));
            m.At(x, -10f, z).Cyl(0.3f, 2f, 10.5f, 8, Hex("fff1b8"), Hex("fff6d6"), true, Hex("ffe58a"));
        }
        return m;
    });
    // 역추진 불꽃: 아래로 뾰족한 원뿔(제 색 발광), 겉 주황 → 속 노랑
    public static Mesh Flame() => ToyMesh.Cached("rgFlame", m =>
    {
        m.At(0, -22f, 0).Cyl(0.6f, 8.5f, 22f, 14, Hex("ff9a3a"), Hex("ffd27a"), true, Hex("ffc35a"));
        m.At(0, -14f, 0).Cyl(0.4f, 5f, 14.5f, 12, Hex("fff1b8"), Hex("fff6d6"), true, Hex("ffe58a"));
        return m;
    });

    // ═════════════ 연료 공장: KayKit 기지 모듈(basemodule_D) + 서쪽 초록 탱크 + 지붕 호퍼(물통) + 동쪽 받침대(연료통) ═════════════
    public const float FUEL_TOP = 31f;                       // 모듈 지붕(호퍼 바닥)
    public static readonly Vector2 FUEL_HOP = new Vector2(-4, -2), FUEL_TRAY = new Vector2(56, 6);   // 기계 좌표(dx, dy)
    public static Mesh FuelPlant() => ToyMesh.Cached("rgFuel", m =>
    {
        m.AtM(0, 0, 2f).RBox(new Vector3(104, 4f, 70), 6f, Hex("d9c8b2"), Hex("e6d8c4"));
        m.AtM(-4, 0, 4f).Kk("basemodule_D", 29);
        Hopper(m, FUEL_HOP.x, FUEL_HOP.y, FUEL_TOP, 34, 26);
        // 서쪽 초록 탱크 묶음(연료 저장) + 관
        m.AtM(-44, -4, 4f).Kk("roofmodule_cargo_A", 22);
        m.PipeM(-34, 10, 10, -26, 10, 10, 1.6f, KG);
        // 동쪽 받침대(연료통) + 모듈에서 받침대로 내려오는 짧은 관
        Tray(m, FUEL_TRAY.x, FUEL_TRAY.y, 26, 24);
        m.PipeM(30, 2, 14, 44, 2, 14, 1.8f, KG);
        m.AtM(44, 2, 10).Cyl(2.4f, 2.4f, 5f, 12, KD, KG);
        return m;
    });

    // ═════════════ 발사대: 작은 착륙장 + 발사탑(structure_tall, 동쪽) + 탑 팔 + 연료 눈금 받침. 로켓은 따로(발사 연출) ═════════════
    public const float LAUNCH_TOP = 11f;   // 로켓이 서는 높이
    public static readonly Vector2 GANTRY = new Vector2(40, -4);
    public static Mesh LaunchPad() => ToyMesh.Cached("rgLaunch", m =>
    {
        m.Kk("landingpad_small", new Vector3(36, 22, 36));
        m.AtM(GANTRY.x, GANTRY.y, 0).Kk("structure_tall", new Vector3(20, 46, 20));
        m.AtM(GANTRY.x - 12, GANTRY.y, 74).RBox(new Vector3(22, 3.4f, 5), 1.2f, KD, KG);    // 로켓 쪽 팔(위)
        m.AtM(GANTRY.x - 12, GANTRY.y, 40).RBox(new Vector3(22, 3f, 4.4f), 1.2f, KD, KG);   // 로켓 쪽 팔(아래)
        // 눈금 받침(탑 앞면): 짙은 판 + 칸 5개 자리(불은 SpacePort가 칸마다 켬)
        m.AtM(GANTRY.x, GANTRY.y + 12f, 30).RBox(new Vector3(10, 34, 2.4f), 1.4f, KD_D, KD);
        return m;
    });
    // 연료 눈금 칸(하나): 둥근 작은 상자. 색은 재질로(꺼짐 짙은 초록, 켜짐 밝은 초록 발광)
    public static Mesh GaugeCell() => ToyMesh.Cached("rgGauge", m => m.RBox(new Vector3(6.4f, 4.6f, 1.6f), 1f, Color.white));
    public static Vector3 GaugeCellLocal(int i) => new Vector3(GANTRY.x, 17f + i * 6.4f, -(GANTRY.y + 13.6f)) * GameConfig.K;
    // 로켓: 흰 몸통 + 주황 띠 + 원뿔 코 + 창 + 핀 넷 + 엔진 종. 피벗 = 엔진 바닥
    public const float ROCKET_H = 86f;
    public static Mesh Rocket() => ToyMesh.Cached("rgRocket", m =>
    {
        m.At(0, 0, 0).Cyl(4.6f, 6.6f, 6f, 16, KD, KG, true, KD);                  // 엔진 종
        m.At(0, 6, 0).Cyl(9.2f, 9.2f, 50f, 20, KW_D, KW, true, KW);                // 몸통
        m.At(0, 12, 0).Band(9.35f, 4f, 20, KO);
        m.At(0, 46, 0).Band(9.35f, 3f, 20, KO);
        m.At(0, 56, 0).Cyl(9.2f, 6.4f, 10f, 20, KW, KW, true, KW);                 // 어깨
        m.At(0, 66, 0).Cyl(6.4f, 0.6f, 18f, 20, KW, Hex("f6f4ee"), true, KW);       // 코
        m.At(0, 66, 0).Band(6.5f, 2f, 20, KO);
        m.At(new Vector3(0, 36, -8.9f), new Vector3(90, 0, 0)).Cyl(3.2f, 3.2f, 1.2f, 14, Hex("3a5a80"), Hex("6fc3f0"));   // 창(남쪽)
        for (int i = 0; i < 4; i++)
        {
            float a = 45f + i * 90f;
            m.At(new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 10f, 9f, Mathf.Cos(a * Mathf.Deg2Rad) * 10f), new Vector3(0, a, 0))
             .Bevel(new Vector3(1.8f, 14f, 9f), 0.5f, KO, KO, KO_L, KO_L, KO);
        }
        return m;
    });

    // ═════════════ 관제탑: 받침 구조물(structure_tall) 위 돔 관제실(basemodule_E). Lv2 레이더, Lv3 옆 모듈 + 금 테 ═════════════
    public const float TOWER_TOP = 58f + 36f;   // 돔 꼭대기 높이(표시등)
    public static Mesh TowerBase() => ToyMesh.Cached("rgTowerBase", m =>
    {
        m.AtM(0, 0, 2f).RBox(new Vector3(60, 4f, 60), 6f, Hex("d9c8b2"), Hex("e6d8c4"));
        m.AtM(0, 0, 4f).Kk("structure_tall", new Vector3(26, 27, 26));
        m.AtM(0, 0, 58f).Kk("basemodule_E", 21);
        return m;
    });
    public static Mesh TowerLv2() => ToyMesh.Cached("rgTowerLv2", m =>
    {
        m.AtM(-22, 12, 4f).Kk("lights", new Vector3(22, 34, 22));
        m.AtM(24, 14, 4f).Kk("containers_C", new Vector3(36, 44, 36));
        return m;
    });
    public static Mesh TowerLv3() => ToyMesh.Cached("rgTowerLv3", m =>
    {
        m.AtM(-40, -6, 4f, 90).Kk("basemodule_A", 18);
        m.AtM(0, 0, 57f).Ring(24f, 26.4f, 2f, 24, GOLD, GOLD_L, GOLD);
        return m;
    });
    // 레이더 접시(돌아감): 짧은 기둥 + 기운 접시 + 끝 표시등. 피벗 = 돔 꼭대기
    public static Mesh Radar() => ToyMesh.Cached("rgRadar", m =>
    {
        m.At(0, 0, 0).Cyl(1.6f, 1.6f, 8f, 10, KD, KG);
        m.At(new Vector3(0, 11f, -2f), new Vector3(-35, 0, 0)).Cyl(10f, 3f, 4f, 18, KW, KW_D, true, KW, true);
        m.At(new Vector3(0, 11f, -2f), new Vector3(-35, 0, 0)).Cyl(1.2f, 1.2f, 9f, 8, KD, KD);
        return m;
    });

    // ═════════════ 얼음 협곡 ═════════════
    // 거대한 얼음 광맥: 깎은 얼음 덩어리 더미(Shaders/MarsIce, 물건 얼음과 같은 재질) + 발치 돌. 피벗 = 더미 가운데 바닥
    public static Mesh IceVein() => ToyMesh.Cached("rgVein", m =>
    {
        var blocks = new (Vector3 p, Vector3 sz, float ry)[]
        {
            (new Vector3(0, 0, 0), new Vector3(44, 34, 36), 8), (new Vector3(-34, 0, 6), new Vector3(34, 26, 30), -14), (new Vector3(34, 0, 4), new Vector3(36, 28, 30), 20),
            (new Vector3(-6, 30, 4), new Vector3(30, 24, 26), 30), (new Vector3(18, 24, 10), new Vector3(24, 18, 22), -24), (new Vector3(-24, 22, 12), new Vector3(22, 16, 20), 12),
            (new Vector3(4, 50, 8), new Vector3(18, 16, 16), 40), (new Vector3(-50, 0, -10), new Vector3(22, 16, 18), 34), (new Vector3(52, 0, -12), new Vector3(20, 14, 18), -30),
            (new Vector3(-14, 0, -24), new Vector3(26, 14, 20), -8), (new Vector3(20, 0, -22), new Vector3(24, 12, 18), 16),
        };
        foreach (var b in blocks)
            m.At(b.p + new Vector3(0, b.sz.y / 2f, 0), new Vector3(0, b.ry, 0)).Bevel(b.sz, 2.4f, Items.ICE_FACE, Items.ICE_CORE, Items.ICE_EDGE, Items.ICE_TOP, Items.ICE_TOP_CORE).Glint(b.sz, Items.ICE_GLINT);
        return m;
    });
    public static Mesh VeinRocks() => ToyMesh.Cached("rgVeinRocks", m =>
    {
        var rock = KK.Hue(0, 40, WorldDress.ROCK_M, 0.75f);
        m.AtM(-66, 8, 0, 40).Kk("rocks_B", 20, rock);
        m.AtM(64, 12, 0, -60).Kk("rocks_B", 16, rock);
        return m;
    });
    // 광맥 둘레 얼음 조각(줍는 자리): 작은 덩어리 셋
    public static Mesh IceChunk() => ToyMesh.Cached("rgChunk", m =>
    {
        m.At(new Vector3(0, 3.4f, 0), new Vector3(0, 20, 0)).Bevel(new Vector3(10, 6.8f, 8), 1.1f, Items.ICE_FACE, Items.ICE_CORE, Items.ICE_EDGE, Items.ICE_TOP, Items.ICE_TOP_CORE).Glint(new Vector3(10, 6.8f, 8), Items.ICE_GLINT);
        m.At(new Vector3(7, 2.4f, 3), new Vector3(0, -30, 0)).Bevel(new Vector3(7, 4.8f, 6), 0.9f, Items.ICE_FACE, Items.ICE_CORE, Items.ICE_EDGE, Items.ICE_TOP, Items.ICE_TOP_CORE);
        m.At(new Vector3(-6, 2f, 4), new Vector3(0, 50, 0)).Bevel(new Vector3(6, 4f, 5), 0.8f, Items.ICE_FACE, Items.ICE_CORE, Items.ICE_EDGE, Items.ICE_TOP, Items.ICE_TOP_CORE);
        return m;
    });

    // 정제소: KayKit 차고 모듈(basemodule_garage) + 뒤 노란 탱크 + 지붕 호퍼(얼음) + 동쪽 받침대(연료통)
    public const float REF_TOP = 34f;
    public static readonly Vector2 REF_HOP = new Vector2(-2, -4), REF_TRAY = new Vector2(56, 8);
    public static Mesh Refinery() => ToyMesh.Cached("rgRefinery", m =>
    {
        m.AtM(0, 0, 2f).RBox(new Vector3(104, 4f, 76), 6f, Hex("d9c8b2"), Hex("e6d8c4"));
        m.AtM(-2, 0, 4f).Kk("basemodule_garage", 30);
        Hopper(m, REF_HOP.x, REF_HOP.y, REF_TOP, 34, 28);
        m.AtM(-44, -20, 4f).Kk("containers_B", new Vector3(44, 60, 44));
        m.AtM(-44, 14, 4f).Kk("containers_B", new Vector3(36, 48, 36));
        Tray(m, REF_TRAY.x, REF_TRAY.y, 26, 24);
        m.PipeM(30, 0, 14, 44, 0, 14, 1.8f, KG);
        m.AtM(44, 0, 10).Cyl(2.4f, 2.4f, 5f, 12, KD, KG);
        return m;
    });

    // 채굴 로버: 큰 트럭(spacetruck_large) + 트레일러. 피벗 = 트럭 바닥 가운데, 앞 = +z(북)으로 굽고 진행 방향으로 돌린다
    public static Mesh Rover() => ToyMesh.Cached("rgRover", m =>
    {
        m.At(0, 0, 0, 180).Kk("spacetruck_large", 36);
        return m;
    });
    public static Mesh Trailer() => ToyMesh.Cached("rgTrailer", m =>
    {
        m.At(0, 0, 0, 180).Kk("spacetruck_trailer", 36);
        return m;
    });
    public const float TRAILER_LEN = 36f, TRAILER_TOP = 11f;

    // 협곡 바위: KayKit 돌(rocks_B 무더기·rock_A 하나)을 메사 돌 색으로. v 0 큰 무더기, 1 작은 돌 둘
    public static Mesh Boulder(int v) => ToyMesh.Cached("rgBoulder" + v, m =>
    {
        var rock = KK.Hue(0, 40, WorldDress.ROCK_M, 0.75f);
        if (v == 0) { m.Kk("rocks_B", 22, rock); m.AtM(24, 6, 0, 50).Kk("rock_A", 26, rock); }
        else { m.Kk("rock_A", 34, rock); m.AtM(-14, 4, 0, -30).Kk("rock_A", 22, rock); }
        return m;
    });
}

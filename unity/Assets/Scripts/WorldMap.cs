using System.Collections.Generic;
using UnityEngine;

// 명세 §0·§4. 시뮬레이션은 논리 좌표(x 0..400, y 0..1040, y↑ = 화면 아래)로 돌리고 뷰만 W2U로 배치한다.
public static class WorldMap
{
    public struct Stn { public string key; public float x, y, w, h; }

    public static readonly Dictionary<string, Stn> STN = new Dictionary<string, Stn>
    {
        { "melter",   new Stn{ key="melter",   x=322, y=124, w=96,  h=64 } },
        { "bottler",  new Stn{ key="bottler",  x=322, y=304, w=96,  h=64 } },
        { "counter",  new Stn{ key="counter",  x=178, y=504, w=220, h=36 } },
        { "register", new Stn{ key="register", x=316, y=504, w=40,  h=40 } },
        { "drill",    new Stn{ key="drill",    x=62,  y=104, w=80,  h=60 } }, // 설치 전 숨김·충돌 없음
        // 생존 기지 시설(구역이 열리기 전엔 충돌 없음, STN_ZONE)
        { "elec",     new Stn{ key="elec",     x=322, y=-85,  w=96,  h=64 } },  // 전기분해기: 물통 → 산소통
        { "purif",    new Stn{ key="purif",    x=322, y=-340, w=96,  h=56 } },  // 토양 정화기: 레골리스 → 흙
        { "mixer",    new Stn{ key="mixer",    x=322, y=-252, w=96,  h=56 } },  // 비료 배합기: 흙 → 비료
        { "green",    new Stn{ key="green",    x=126, y=-300, w=180, h=100 } }, // 온실: 비료(+물) → 식량 (모형은 150×84 설계를 1.2배, SettleStations.Grow)
    };
    public static readonly Dictionary<string, string> STN_ZONE = new Dictionary<string, string> { { "elec", "o2" }, { "purif", "soil" }, { "mixer", "soil" }, { "green", "farm" } };
    public static bool StnActive(string key) => key == "drill" ? drillInstalled : !STN_ZONE.TryGetValue(key, out var z) || openZones.Contains(z);
    public static bool drillInstalled;   // 드릴 발판 Lv1 이후 true (충돌·표시)
    // 러너 적재/하역점, 삐걱 자리 (§4.2)
    public static Vector2 RunnerLoad => new Vector2(STN["bottler"].x - 58, STN["bottler"].y + 8);
    public static Vector2 RunnerUnload => new Vector2(STN["counter"].x - 30, STN["counter"].y - 40);
    public static readonly Vector2 REG_SPOT = new Vector2(36, 4); // 계산대 기준 지폐 자리 오프셋

    public static readonly Vector2[] ICE_SPOTS =
    {
        new Vector2(60,240), new Vector2(116,268), new Vector2(178,232), new Vector2(84,312),
        new Vector2(152,318), new Vector2(44,366), new Vector2(190,350), new Vector2(112,380),
        // ice2 (Phase 2)
        new Vector2(150,86), new Vector2(204,118), new Vector2(148,158), new Vector2(206,172),
    };

    // 레골리스 더미(채굴장 구역 안)
    public static readonly Vector2[] REGO_SPOTS =
    {
        new Vector2(54,-150), new Vector2(118,-136), new Vector2(190,-150), new Vector2(76,-90),
        new Vector2(160,-84), new Vector2(206,-34), new Vector2(46,-30), new Vector2(100,-36),
    };

    public const float QUEUE_X = 216f, QUEUE_Y0 = 584f, QUEUE_GAP = 46f;
    public static readonly Vector2 ENTRY = new Vector2(206, 1000);
    public static readonly Vector2 PLAYER_START = new Vector2(200, 450);
    public static Vector2 CashierSpot => new Vector2(STN["register"].x + 2, STN["register"].y - 34);

    // open=false인 구역은 pad(발판 id) Lv≥1일 때 열린다
    public struct Zone { public string key; public float x0, y0, x1, y1; public Color floor; public string sides; public bool open; public string pad; }
    // 바닥색은 모두 같은 모래·주황 계열(얼음밭만 조금 희게): 2026-10-25 배경 채도 정리(예전 초록 돔·파랑 산소동·연두 온실 → 같은 계열)
    public static readonly Zone[] ZONES =
    {
        new Zone{ key="ice",    x0=16,  y0=196, x1=236, y1=404, floor=GameConfig.Hex("e3d6c8"), sides="NESW", open=true },
        new Zone{ key="ice2",   x0=16,  y0=46,  x1=236, y1=196, floor=GameConfig.Hex("e3d6c8"), sides="NEW",  open=false, pad="ice2" },
        new Zone{ key="melt",   x0=256, y0=46,  x1=380, y1=224, floor=GameConfig.Hex("dcc1a8"), sides="NESW", open=true },
        new Zone{ key="bottle", x0=256, y0=224, x1=380, y1=404, floor=GameConfig.Hex("dcc1a8"), sides="ESW",  open=true },
        new Zone{ key="shop",   x0=16,  y0=420, x1=380, y1=560, floor=GameConfig.Hex("e8cfb3"), sides="EW",   open=true },
        new Zone{ key="dome",   x0=22,  y0=740, x1=196, y1=934, floor=GameConfig.Hex("dccab2"), sides="NESW", open=false, pad="dome" },
        new Zone{ key="pen",    x0=276, y0=740, x1=378, y1=934, floor=GameConfig.Hex("d9bb9c"), sides="NESW", open=false, pad="pen" },
        // 생존 기지(북쪽): 잠겨 있으면 단단한 땅(통행 불가, NorthBlocked)
        new Zone{ key="mine",   x0=16,  y0=-180, x1=236, y1=10,   floor=GameConfig.Hex("d6bca2"), sides="NESW", open=false, pad="mine" },
        new Zone{ key="o2",     x0=256, y0=-180, x1=380, y1=10,   floor=GameConfig.Hex("ddcbb8"), sides="NESW", open=false, pad="o2" },
        new Zone{ key="farm",   x0=16,  y0=-380, x1=236, y1=-210, floor=GameConfig.Hex("d8c8a8"), sides="NESW", open=false, pad="farm" },
        new Zone{ key="soil",   x0=256, y0=-380, x1=380, y1=-210, floor=GameConfig.Hex("d4b496"), sides="NESW", open=false, pad="soil" },
    };
    // 게이트(울타리 틈): 구역키, 변, 위치, 폭
    public struct Gate { public string zone; public char side; public float at, w; }
    public static readonly Gate[] GATES =
    {
        new Gate{ zone="ice", side='N', at=126, w=70 }, new Gate{ zone="ice", side='S', at=126, w=70 },
        new Gate{ zone="melt", side='W', at=150, w=70 },
        new Gate{ zone="bottle", side='W', at=330, w=70 }, new Gate{ zone="bottle", side='S', at=322, w=70 },
        new Gate{ zone="dome", side='N', at=106, w=60 }, new Gate{ zone="pen", side='N', at=330, w=60 },
        // 울타리가 실제로 막으므로 얼음밭 → 가운데 통로(x 236~256) → 해빙기 W(y150)·병입기 W(y330)로 바로 이어지는 E변 게이트
        new Gate{ zone="ice", side='E', at=250, w=60 }, new Gate{ zone="ice2", side='E', at=120, w=60 },
        // 생존 기지: ice2 북쪽 → 북쪽 길(y 10~46) → 채굴장·산소동, 가운데 길(y -210~-180) → 온실·토양 공장
        new Gate{ zone="ice2", side='N', at=126, w=70 },
        new Gate{ zone="mine", side='S', at=126, w=70 }, new Gate{ zone="mine", side='N', at=126, w=70 }, new Gate{ zone="mine", side='E', at=-85, w=60 },
        new Gate{ zone="o2", side='S', at=318, w=60 }, new Gate{ zone="o2", side='N', at=300, w=60 }, new Gate{ zone="o2", side='W', at=-85, w=60 },
        new Gate{ zone="farm", side='S', at=126, w=70 }, new Gate{ zone="farm", side='E', at=-295, w=60 },
        new Gate{ zone="soil", side='S', at=318, w=60 }, new Gate{ zone="soil", side='W', at=-296, w=36 },
    };
    // 북쪽(y<10)은 열린 구역 안·열린 통로만 통행. 통로(x 236~256)와 가운데 길은 옆 구역이 하나라도 열려야 한다
    public const float NORTH_Y = 10f, MID_Y0 = -210f, MID_Y1 = -180f;
    static bool In(Zone z, float x, float y, float m) => x >= z.x0 - m && x <= z.x1 + m && y >= z.y0 - m && y <= z.y1 + m;
    static bool NorthBlocked(float x, float y)
    {
        if (y >= NORTH_Y) return false;
        foreach (var z in ZONES) if (z.y1 <= NORTH_Y && openZones.Contains(z.key) && In(z, x, y, 6)) return false;
        bool near = openZones.Contains("mine") || openZones.Contains("o2"), far = openZones.Contains("farm") || openZones.Contains("soil");
        bool aisle = x > 236 && x < 256;
        if (aisle && y >= MID_Y1) return !near;
        if (y >= MID_Y0 && y < MID_Y1) return !far;
        if (aisle && y < MID_Y0) return !far;
        return true;
    }
    // 열린 구역(울타리가 보이는 구역)만 울타리 충돌. ZoneManager가 채운다. fenceVer는 봇 격자 재생성 신호
    public static readonly HashSet<string> openZones = new HashSet<string>();
    public static int fenceVer;
    public static void SetZoneOpen(string key, bool open)
    {
        if (open ? openZones.Add(key) : openZones.Remove(key)) fenceVer++;
    }
    public static void ClearZones() { openZones.Clear(); fenceVer++; }
    const float FENCE_HW = 5f;      // 울타리 충돌 반폭(논리). 8 격자에서도 두 칸이 막히고 가운데 통로(236~256)는 한 칸 남는다
    const float GATE_POST = 4f;     // 게이트 굵은 기둥(7) 몫만큼 틈을 좁힌다

    // 열린 구역의 울타리 변(게이트 틈 제외) 선분 충돌. 줄 난간(qL/qR)은 충돌 없음
    static bool FenceBlocked(float x, float y)
    {
        foreach (var z in ZONES)
        {
            if (!openZones.Contains(z.key)) continue;
            if (x < z.x0 - FENCE_HW || x > z.x1 + FENCE_HW || y < z.y0 - FENCE_HW || y > z.y1 + FENCE_HW) continue;
            foreach (char side in z.sides)
            {
                bool horiz = side == 'N' || side == 'S';
                float fixedV = side == 'N' ? z.y0 : side == 'S' ? z.y1 : side == 'W' ? z.x0 : z.x1;
                float across = horiz ? y : x, along = horiz ? x : y;
                if (Mathf.Abs(across - fixedV) > FENCE_HW) continue;
                bool inGate = false;
                foreach (var g in GATES)
                    if (g.zone == z.key && g.side == side && Mathf.Abs(along - g.at) < g.w / 2 - GATE_POST) { inGate = true; break; }
                if (!inGate) return true;
            }
        }
        return false;
    }

    // §4.2 휴게 돔·토끼 우리·착륙선
    public static readonly Vector2 DOME = new Vector2(108, 838);
    public const float DOME_R = 84f;
    public static readonly Vector2[] SEATS = { new Vector2(74, 786), new Vector2(142, 786), new Vector2(74, 838), new Vector2(142, 838), new Vector2(74, 890), new Vector2(142, 890) };
    public const float SEAT_R = 34f;                                  // 좌석 팁 수거 반경
    public static Vector2 FOUNTAIN => new Vector2(DOME.x, DOME.y + 30);
    public static Vector2[] BEDS => new[] { new Vector2(DOME.x - 58, DOME.y + 22), new Vector2(DOME.x + 58, DOME.y + 22) };   // 화단 2곳 ±20
    public static Vector2 AIRLOCK => new Vector2(DOME.x - 55, DOME.y - DOME_R - 4);                                             // 충돌 반폭 ±18·±11
    public const float PEN_X0 = 276, PEN_Y0 = 740, PEN_X1 = 378, PEN_Y1 = 934;
    public static readonly Vector2 PEN_TIP = new Vector2(327, 766);
    public static readonly Vector2 LANDER = new Vector2(84, 988);
    public static bool domeOpen;   // 휴게 돔 발판 Lv1 이후 true (돔 시설 충돌)
    public const float POST_GAP = 40f;

    // 논리 → Unity (z 반전으로 왼손 좌표 보정)
    public static Vector3 W2U(float x, float y, float h = 0f)
    {
        float k = GameConfig.K;
        return new Vector3((x - 200f) * k, h * k, -(y - 520f) * k);
    }
    public static Vector3 W2U(Vector2 p, float h = 0f) => W2U(p.x, p.y, h);
    public static Vector2 U2W(Vector3 u) => new Vector2(u.x / GameConfig.K + 200f, -u.z / GameConfig.K + 520f);

    // §1.1 스테이션 근접: AABB 확장
    public static bool NearStn(Vector2 p, string key, float r)
    {
        var s = STN[key];
        return Mathf.Abs(p.x - s.x) < s.w / 2 + r && Mathf.Abs(p.y - s.y) < s.h / 2 + r;
    }

    // §2 blocked(): 경계 + STN AABB(+10 x / +8 y) + 열린 구역 울타리(게이트 틈만 통과, jake 피드백 "울타리를 왜 넘어가냐"). 드릴은 설치 후만.
    public static bool Blocked(float x, float y)
    {
        if (x < 14 || x > 386 || y < GameConfig.Y_MIN + 6 || y > 1024) return true;
        if (NorthBlocked(x, y)) return true;
        foreach (var s in STN.Values)
        {
            if (!StnActive(s.key)) continue;
            if (Mathf.Abs(x - s.x) < s.w / 2 + 10 && Mathf.Abs(y - s.y) < s.h / 2 + 8) return true;
        }
        var p = new Vector2(x, y);
        if (domeOpen)
        {
            foreach (var s in SEATS) if ((p - s).sqrMagnitude < 16 * 16) return true;
            if ((p - FOUNTAIN).sqrMagnitude < 17 * 17) return true;
            foreach (var b in BEDS) if (Mathf.Abs(x - b.x) < 20 && Mathf.Abs(y - b.y) < 20) return true;
            var a = AIRLOCK; if (Mathf.Abs(x - a.x) < 18 && Mathf.Abs(y - a.y) < 11) return true;   // 반폭 ±18·±11 (원본 947행)
        }
        if (x > 338 && y > 358 && y < 404) return true;                 // 병입 구역 드럼통
        if (y > 566 && y < 680 && (Mathf.Abs(x - 186) < 5 || Mathf.Abs(x - 246) < 5)) return true;   // 줄 난간 qL·qR(울타리처럼 보이니 통과 금지, jake 10/25)
        if ((p - LANDER).sqrMagnitude < 34 * 34) return true;            // 착륙선
        if (FenceBlocked(x, y)) return true;
        return false;
    }

    public static Vector2 QueuePos(int qi) => new Vector2(QUEUE_X, QUEUE_Y0 + qi * QUEUE_GAP);
}

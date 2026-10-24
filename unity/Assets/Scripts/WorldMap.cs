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
    };
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

    public const float QUEUE_X = 216f, QUEUE_Y0 = 584f, QUEUE_GAP = 46f;
    public static readonly Vector2 ENTRY = new Vector2(206, 1000);
    public static readonly Vector2 PLAYER_START = new Vector2(200, 450);
    public static Vector2 CashierSpot => new Vector2(STN["register"].x + 2, STN["register"].y - 34);

    // open=false인 구역은 pad(발판 id) Lv≥1일 때 열린다
    public struct Zone { public string key; public float x0, y0, x1, y1; public Color floor; public string sides; public bool open; public string pad; }
    public static readonly Zone[] ZONES =
    {
        new Zone{ key="ice",    x0=16,  y0=196, x1=236, y1=404, floor=GameConfig.Hex("e6d8c6"), sides="NESW", open=true },
        new Zone{ key="ice2",   x0=16,  y0=46,  x1=236, y1=196, floor=GameConfig.Hex("e6d8c6"), sides="NEW",  open=false, pad="ice2" },
        new Zone{ key="melt",   x0=256, y0=46,  x1=380, y1=224, floor=GameConfig.Hex("d9b298"), sides="NESW", open=true },
        new Zone{ key="bottle", x0=256, y0=224, x1=380, y1=404, floor=GameConfig.Hex("d9b298"), sides="ESW",  open=true },
        new Zone{ key="shop",   x0=16,  y0=420, x1=380, y1=560, floor=GameConfig.Hex("efcfa9"), sides="EW",   open=true },
        new Zone{ key="dome",   x0=22,  y0=740, x1=196, y1=934, floor=GameConfig.Hex("d3dbbd"), sides="NESW", open=false, pad="dome" },
        new Zone{ key="pen",    x0=276, y0=740, x1=378, y1=934, floor=GameConfig.Hex("dcb085"), sides="NESW", open=false, pad="pen" },
    };
    // 게이트(울타리 틈): 구역키, 변, 위치, 폭
    public struct Gate { public string zone; public char side; public float at, w; }
    public static readonly Gate[] GATES =
    {
        new Gate{ zone="ice", side='N', at=126, w=70 }, new Gate{ zone="ice", side='S', at=126, w=70 },
        new Gate{ zone="melt", side='W', at=150, w=70 },
        new Gate{ zone="bottle", side='W', at=330, w=70 }, new Gate{ zone="bottle", side='S', at=322, w=70 },
        new Gate{ zone="dome", side='N', at=106, w=60 }, new Gate{ zone="pen", side='N', at=330, w=60 },
    };

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

    // §2 blocked(): 경계 + STN AABB(+10 x / +8 y). 울타리는 충돌 없음(넘어다님). 드릴은 설치 후만.
    public static bool Blocked(float x, float y)
    {
        if (x < 14 || x > 386 || y < 30 || y > 1024) return true;
        foreach (var s in STN.Values)
        {
            if (s.key == "drill" && !drillInstalled) continue;
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
        if ((p - LANDER).sqrMagnitude < 34 * 34) return true;            // 착륙선
        return false;
    }

    public static Vector2 QueuePos(int qi) => new Vector2(QUEUE_X, QUEUE_Y0 + qi * QUEUE_GAP);
}

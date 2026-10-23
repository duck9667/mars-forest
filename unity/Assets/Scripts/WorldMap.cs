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
    };
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

    public struct Zone { public string key; public float x0, y0, x1, y1; public Color floor; public string sides; public bool open; }
    public static readonly Zone[] ZONES =
    {
        new Zone{ key="ice",    x0=16,  y0=196, x1=236, y1=404, floor=GameConfig.Hex("e6d8c6"), sides="NESW", open=true },
        new Zone{ key="melt",   x0=256, y0=46,  x1=380, y1=224, floor=GameConfig.Hex("d9b298"), sides="NESW", open=true },
        new Zone{ key="bottle", x0=256, y0=224, x1=380, y1=404, floor=GameConfig.Hex("d9b298"), sides="ESW",  open=true },
        new Zone{ key="shop",   x0=16,  y0=420, x1=380, y1=560, floor=GameConfig.Hex("efcfa9"), sides="EW",   open=true },
    };
    // 게이트(울타리 틈): 구역키, 변, 위치, 폭
    public struct Gate { public string zone; public char side; public float at, w; }
    public static readonly Gate[] GATES =
    {
        new Gate{ zone="ice", side='N', at=126, w=70 }, new Gate{ zone="ice", side='S', at=126, w=70 },
        new Gate{ zone="melt", side='W', at=150, w=70 },
        new Gate{ zone="bottle", side='W', at=330, w=70 }, new Gate{ zone="bottle", side='S', at=322, w=70 },
    };
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

    // §2 blocked(): 경계 + STN AABB(+10 x / +8 y). Phase 1은 울타리 충돌 없음.
    public static bool Blocked(float x, float y)
    {
        if (x < 14 || x > 386 || y < 30 || y > 1024) return true;
        foreach (var s in STN.Values)
            if (Mathf.Abs(x - s.x) < s.w / 2 + 10 && Mathf.Abs(y - s.y) < s.h / 2 + 8) return true;
        return false;
    }

    public static Vector2 QueuePos(int qi) => new Vector2(QUEUE_X, QUEUE_Y0 + qi * QUEUE_GAP);
}

using System;
using System.Collections.Generic;
using UnityEngine;

// 명세 §1~6 상수. 수치는 app/index.html 원본 그대로(논리 단위). Phase 1은 SO 대신 static 상수로 두고, Phase 2에서 SO로 옮긴다.
public static class GameConfig
{
    public const float K = 0.05f;              // 논리 20 = 1m
    public const float W = 400f, H = 1040f;
    public const float DT_MAX = 0.05f;

    public static readonly int[] CARRY = { 20, 24, 28, 32, 36, 40, 48 };   // jake 요청(10/24): 처음부터 20개
    public const int BOTTLE_CAP = 24;
    public const int MELT_VIS_MAX = 10;        // 제빙 큐 시각화 상한(2열)
    public const int BILL_MAX = 24;
    public const int STACK_MAX = 60;

    // 근접 반경·쿨다운 (§1.1)
    public const float R_ICE = 40f, CD_ICE = 0.09f;
    public const float R_MELT = 40f, CD_MELT = 0.06f;
    public const float R_BOTTLE = 40f, CD_BOTTLE = 0.13f;
    public const float R_SHELF = 48f, CD_SHELF = 0.11f;
    public const float R_CASH = 54f, CD_CASH = 0.25f;
    public const float R_PAY = 70f;            // 플레이어가 계산대 근처면 결제
    public const float R_PAD = 30f;
    public const float PAY_TIME = 0.35f;

    // 얼음 더미
    public const int BASE_SPOTS = 8;
    public static float SpotRespawn() => 6f + UnityEngine.Random.value * 3f;

    // 플레이어 (§2)
    public const float BASE_SPEED = 165f, SPEED_PER_LV = 18f;
    public const float JOY_BASE = 120f, JOY_KNOB = 50f, JOY_MAX = 45f, JOY_DEAD = 6f;

    // 손님 (§5.2)
    public const float FIRST_SPAWN = 3f, TUT_GAP = 12f;
    public const int QUEUE_MAX = 6;
    public const float CUST_WALK = 110f, CUST_LEAVE = 120f;
    public const float CUST_WARN = 25f, CUST_PATIENCE = 40f;

    // 뽀삐·드릴 (§5.1)
    public const float RUNNER_BASE_SPEED = 95f, RUNNER_SPEED_PER_LV = 25f;
    public const int RUNNER_CAP = 8;
    public const float RUNNER_LOAD_T = 0.18f, RUNNER_UNLOAD_T = 0.15f, RUNNER_DEPART_T = 1.5f;
    public const float DRILL_PERIOD = 2.0f, DRILL_GAP = 0.15f, DRILL_FLY_T = 0.9f, DRILL_LIFT = 140f;
    public const int MELT_CAP = 40;   // 드릴에만 적용

    // 발판 납입 (§3.2)
    public const float PAD_MIN_TAKE = 3f, PAD_RATE = 0.6f, PAD_COIN_GAP = 0.05f;
    public const float VIS_REFRESH = 0.3f;
    public const int VIS_MAX = 2;

    // 손맛
    public const float FLY_T = 0.3f, FLY_LIFT = 45f;
    public const float SQUASH_T = 0.32f, POP_T = 0.55f;

    // 튜토리얼 말풍선 (§6.2)
    public static readonly string[] TUT = { "얼음을 주워요", "해빙기에 넣어요", "카운터에 올려요" };

    // 색
    public static readonly Color BG = Hex("c9552b");
    public static readonly Color GROUND = Color.white;
    public static readonly Color OUTLINE = Hex("1b1a2e");
    public static readonly Color NAVY = Hex("22305a");
    public static readonly Color GOLD = Hex("f2c14e");
    public static readonly Color ICE = Hex("cfeeff");
    public static readonly Color BOTTLE = Hex("6fc3f0");
    public static readonly Color BILL = Hex("7ccf7a");
    public static readonly Color[] CUST_COLS = { Hex("f06a6a"), Hex("f0a24a"), Hex("f2d35b"), Hex("7fcf7a"), Hex("6fb6f0"), Hex("b48cf0"), Hex("f08cc8") };

    public static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        return c;
    }
}

// §3 발판 정의. cost(l) = 현재 레벨 l에서 다음 레벨 비용.
public class PadDef
{
    public string id, name, icon;
    public Vector2 pos;
    public Func<int, float> cost;
    public int max;
    public Func<GameState, bool> req;
    public string reqText;
    public bool menu;      // 시트 전용(월드 발판 없음)
    public bool zone;      // 구역 발판
    public bool active;    // 이번 Phase에 포함

    public float Cost(int lv) => Mathf.Floor(cost(lv) + 0.5f);   // JS Math.round와 같은 반올림(은행가 반올림 아님: hand Lv2 = 123)
}

public static class Pads
{
    public static readonly List<PadDef> ALL = new List<PadDef>
    {
        new PadDef{ id="hand",    name="손 크기",      icon="✋", pos=new Vector2(60,452),  cost=l=>40f*Mathf.Pow(1.75f,l),  max=6, req=g=>true, reqText="", menu=true, active=true },
        new PadDef{ id="spd",     name="이동 속도",    icon="👟", pos=new Vector2(200,452), cost=l=>60f*Mathf.Pow(1.8f,l),   max=5, req=g=>true, reqText="", menu=true, active=true },
        new PadDef{ id="price",   name="프리미엄 라벨", icon="🏷", pos=new Vector2(134,598), cost=l=>200f*Mathf.Pow(1.9f,l),  max=5, req=g=>g.S.sold>=10, reqText="판매 10", menu=true, active=true },
        new PadDef{ id="cashier", name="삐걱 고용",    icon="🤖", pos=new Vector2(360,598), cost=l=>150f,                   max=1, req=g=>g.S.sold>=3, reqText="판매 3", active=true },
        new PadDef{ id="bottler", name="병입기 속도",  icon="🧴", pos=new Vector2(290,372), cost=l=>80f*Mathf.Pow(1.7f,l),   max=6, req=g=>g.S.sold>=3, reqText="판매 3", active=true },
        new PadDef{ id="melter",  name="해빙기 속도",  icon="❄", pos=new Vector2(290,192), cost=l=>70f*Mathf.Pow(1.7f,l),   max=6, req=g=>g.PadLv("ice2")>=1, reqText="얼음밭 확장", active=true },
        new PadDef{ id="shelf",   name="선반 칸",      icon="🧊", pos=new Vector2(134,452), cost=l=>120f*Mathf.Pow(1.8f,l),  max=4, req=g=>g.PadLv("ice2")>=1, reqText="얼음밭 확장", active=true },
        // ---- Phase 2 ----
        new PadDef{ id="ice2",    name="얼음밭 확장",  icon="🧊", pos=new Vector2(126,220), cost=l=>120f,  max=1, req=g=>g.PadLv("bottler")>=1, reqText="병입기 Lv.1", zone=true, active=true },
        new PadDef{ id="runner",  name="뽀삐 고용",    icon="🛒", pos=new Vector2(282,598), cost=l=>350f,  max=1, req=g=>g.S.sold>=20, reqText="판매 20", active=true },
        new PadDef{ id="ads",     name="간판",         icon="📣", pos=new Vector2(50,598),  cost=l=>150f*Mathf.Pow(1.8f,l), max=5, req=g=>g.PadLv("runner")>=1, reqText="뽀삐", active=true },
        new PadDef{ id="drill",   name="얼음 드릴",    icon="⛏", pos=new Vector2(62,166),  cost=l=>600f,  max=1, req=g=>g.PadLv("runner")>=1 && g.PadLv("ice2")>=1, reqText="뽀삐·얼음밭", active=true },
        new PadDef{ id="runspd",  name="뽀삐 속도",    icon="💨", pos=new Vector2(282,662), cost=l=>250f*Mathf.Pow(1.8f,l), max=4, req=g=>g.PadLv("runner")>=1, reqText="뽀삐", active=true },
        new PadDef{ id="runner2", name="뽀삐 한 명 더", icon="🛒", pos=new Vector2(360,662), cost=l=>1200f, max=1, req=g=>g.PadLv("drill")>=1, reqText="드릴", active=true },
        // ---- Phase 3 ----
        new PadDef{ id="pen",     name="먼지 토끼 우리", icon="🐰", pos=new Vector2(330,720), cost=l=>400f, max=1, req=g=>g.PadLv("drill")>=1, reqText="드릴", zone=true, active=true },
        new PadDef{ id="dome",    name="휴게 돔",      icon="🏠", pos=new Vector2(106,720), cost=l=>700f,  max=1, req=g=>g.PadLv("pen")>=1, reqText="우리", zone=true, active=true },
    };

    // §3.1 노출 순서. n번째 등장 = 그 id의 Lv n 차례.
    public static readonly string[] ORDER =
    {
        "cashier","bottler","ice2","melter","shelf","runner","ads","bottler","melter","ads","drill","pen","shelf","dome",
        "runspd","bottler","melter","runner2","ads","shelf","runspd","bottler","melter","runspd","ads","bottler","melter","runspd","shelf"
    };

    static Dictionary<string, PadDef> _byId;
    public static PadDef Get(string id)
    {
        if (_byId == null) { _byId = new Dictionary<string, PadDef>(); foreach (var p in ALL) _byId[p.id] = p; }
        return _byId.TryGetValue(id, out var d) ? d : null;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// 이어지는 세계(docs/chapter3-design.md): 기지(x 0~400) 동쪽에 지역을 하나씩 붙인다. 지역 = 표 한 줄(이름·장·사각형·들어가는 길·발판 둘).
//  · 길: 서쪽(앞 지역 끝 roadX0)에서 지역 서쪽 변(x0)까지 y ROAD_Y0~ROAD_Y1(상점 북쪽 띠를 동쪽으로 이은 크림 타일). 길 발판(roadPad)을 사면 깔린다.
//  · 지역: 지역 발판(zonePad)을 사면 바닥이 열리고 시설이 선다. 잠긴 동안은 측량 점선만.
//  · 관문 앞 바닥(길 시작 STUB): 그 지역 차례(openStage)가 되면 걸을 수 있다(길 발판이 여기 선다).
// 걷기: WorldMap.Blocked가 x > EAST_X이면 EastBlocked로 넘긴다. 열림 상태는 저장 복원(SaveSystem.Migrate)·발판 이벤트(RegionWorld)가 채운다.
// 시설 발자국(충돌)은 각 시스템이 Solid(키, 사각형, 켜짐)로 등록한다(재조립 때 같은 키로 덮어씀).
public static class Regions
{
    public class Def
    {
        public string key, name, icon;
        public int chapter;                 // 0부터(우주항 2 = 3장)
        public float x0, y0, x1, y1;        // 지역 사각형(논리, 걷는 곳)
        public float roadX0;                // 길 시작 x(길은 roadX0 ~ x0)
        public float gateX;                 // 관문(기둥·차단봉) x
        public string roadPad, zonePad;
        public int openStage;               // 길 발판 차례(관문 앞 바닥도 이때부터 걸을 수 있다)
        public Color floor;
        public float W => x1 - x0;
        public float H => y1 - y0;
        public Vector2 Center => new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f);
    }

    public const float ROAD_Y0 = 396f, ROAD_Y1 = 436f, ROAD_Y = 416f;   // 걷는 폭 40
    public const float DRAW_Y0 = 392f, DRAW_Y1 = 440f;                  // 그림 폭 48(상점 북쪽 띠 GroundPainter.BAND_Y0~Y1과 같은 높이라 이어진다)
    public const float EAST_X = 386f;                                   // 기지 놀이 경계(여기까지는 기지 규칙)
    public const float STUB = 58f;                                      // 관문 앞 바닥 길이
    public const float GROUND_X1 = 1720f;                               // 동쪽 지면 텍스처 끝(카메라는 이 안쪽만 본다)

    // 지역 표. 다음 지역은 여기에 한 줄 더(길은 앞 지역 x1에서 시작). 바닥색은 그 지역 맨땅
    public static readonly Def[] ALL =
    {
        new Def{ key="port",   name="우주항",   icon="ship", chapter=2, x0=640,  y0=200, x1=1080, y1=660, roadX0=EAST_X, gateX=448,  roadPad="road1", zonePad="port",   openStage=10, floor=GameConfig.Hex("d4916a") },
        new Def{ key="canyon", name="얼음 협곡", icon="ice",  chapter=3, x0=1240, y0=180, x1=1680, y1=680, roadX0=1080,   gateX=1142, roadPad="road2", zonePad="canyon", openStage=15, floor=GameConfig.Hex("cc9478") },
    };
    public static Def Get(string key) { foreach (var r in ALL) if (r.key == key) return r; return null; }

    // ---- 열림 상태(저장 복원·발판 이벤트가 채움). GameState가 아직 없는 저장 복원 중에도 쓰므로 정적 집합으로 둔다 ----
    static readonly HashSet<string> open = new HashSet<string>();
    public static int stage;
    public static bool IsOpen(string pad) => open.Contains(pad);
    public static void SetOpen(string pad, bool on) { if (on ? open.Add(pad) : open.Remove(pad)) WorldMap.fenceVer++; }
    public static void Sync(SaveData d)
    {
        open.Clear();
        foreach (var e in d.pads) if (e.lv >= 1) foreach (var r in ALL) if (e.id == r.roadPad || e.id == r.zonePad) open.Add(e.id);
        stage = d.stage; WorldMap.fenceVer++;
    }
    public static bool RoadOpen(Def r) => open.Contains(r.roadPad);
    public static bool ZoneOpen(Def r) => open.Contains(r.zonePad);
    /// 동쪽 지역을 알게 됐나(2장 끝: 지도 지역 탭·카메라)
    public static bool Known => stage >= ALL[0].openStage || open.Count > 0;

    // ---- 시설 발자국(충돌) ----
    class Solid { public Rect r; public Func<bool> on; public bool circle; }
    static readonly Dictionary<string, Solid> solids = new Dictionary<string, Solid>();
    /// 사각형(논리 x0,y0,w,h) 발자국 등록. on이 false면 통과
    public static void SetSolid(string key, Rect r, Func<bool> on, bool circle = false) { solids[key] = new Solid { r = r, on = on, circle = circle }; WorldMap.fenceVer++; }
    static bool HitSolid(float x, float y)
    {
        if (GameState.I == null || GameState.I.S == null) return false;   // 저장 복원 중(재조립 직후)엔 시설 상태를 모른다
        foreach (var s in solids.Values)
        {
            if (!s.r.Contains(new Vector2(x, y))) continue;
            if (s.on != null && !s.on()) continue;
            if (s.circle)
            {
                var c = s.r.center; float rx = s.r.width / 2f, ry = s.r.height / 2f;
                float dx = (x - c.x) / rx, dy = (y - c.y) / ry; if (dx * dx + dy * dy > 1f) continue;
            }
            return true;
        }
        return false;
    }

    /// 기지 동쪽(x > EAST_X) 통행: 열린 길·관문 앞 바닥·열린 지역 안(시설 발자국 밖)만
    public static bool EastBlocked(float x, float y)
    {
        if (x > GROUND_X1 - 40f) return true;
        for (int i = 0; i < ALL.Length; i++)
        {
            var r = ALL[i];
            bool prevOk = i == 0 || ZoneOpen(ALL[i - 1]);
            // 길(지역 서쪽 변 안쪽 12까지 이어서 지역 바닥과 틈이 없게)
            if (y >= ROAD_Y0 && y <= ROAD_Y1 && x >= r.roadX0 - 12f && x <= r.x0 + 12f)
            {
                if (RoadOpen(r)) return HitSolid(x, y);
                if (prevOk && stage >= r.openStage && x <= r.roadX0 + STUB) return false;
            }
            if (ZoneOpen(r) && x >= r.x0 + 6f && x <= r.x1 - 6f && y >= r.y0 + 6f && y <= r.y1 - 6f) return HitSolid(x, y);
        }
        return true;
    }

    /// x가 속한 지역(길 포함). 기지면 null
    public static Def At(float x)
    {
        if (x <= EAST_X) return null;
        foreach (var r in ALL) if (x >= r.roadX0 && x <= r.x1 + 20f) return r;
        return ALL[ALL.Length - 1];
    }

    // ---- 카메라: 동쪽에선 x를 지역 끝까지 따라가고 y는 지역 띠에 붙인다 ----
    public static float BandY0 { get { float v = float.MaxValue; foreach (var r in ALL) v = Mathf.Min(v, r.y0); return v; } }
    public static float BandY1 { get { float v = float.MinValue; foreach (var r in ALL) v = Mathf.Max(v, r.y1); return v; } }
    public const float CAM_MAX_X = GROUND_X1 - 30f;

    // ---- 밝은 땅(지면 명암·비네트): 기지 + 길 + 지역 사각형. GroundPainter 두 장(기지·동쪽)이 같은 목록으로 칠해 경계에서 이어진다 ----
    public static List<Rect> LitRects()
    {
        var l = new List<Rect> { Rect.MinMaxRect(16f, GameConfig.Y_MIN + 4f, 384f, 1010f) };
        foreach (var r in ALL)
        {
            l.Add(Rect.MinMaxRect(r.roadX0, DRAW_Y0, r.x0, DRAW_Y1));
            l.Add(Rect.MinMaxRect(r.x0, r.y0, r.x1, r.y1));
        }
        return l;
    }
}

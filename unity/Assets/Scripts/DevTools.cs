using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 검증용: -shot <dir> 스크린샷 시퀀스 후 종료, -autoplay 힌트 타깃을 따라가는 봇, F5 저장 초기화. 원본 tools/qa의 자동 플레이 봇에 대응.
public class DevTools : MonoBehaviour
{
    GameState G => GameState.I;
    // 스크린샷 타이머·진행과 검증 인자 적용 여부는 Bootstrap.Restart(F5·프레스티지) 뒤에도 이어지도록 정적으로 둔다
    string shotDir; bool autoplay; static float t;
    float[] SHOTS = { 2f, 10f, 25f, 45f, 70f, 100f };
    static int shotIdx; static bool argsApplied;

    void Start()
    {
        shotDir = DevArgs.Get("-shot");
        autoplay = DevArgs.Has("-autoplay");
        // -walk dx,dy: 봇 대신 한 방향으로 계속 밀기(울타리 충돌 확인용). 스크린샷 로그에 위치가 찍힌다
        var wk = DevArgs.Get("-walk");
        if (!string.IsNullOrEmpty(wk))
        {
            var w = wk.Split(',');
            if (w.Length == 2 && float.TryParse(w[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wx) && float.TryParse(w[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wy))
            { autoplay = false; FloatingJoystick.Override = new Vector2(wx, wy).normalized; }
        }
        if (!string.IsNullOrEmpty(shotDir)) Directory.CreateDirectory(shotDir);
        // -shots 2,10,25,...  스크린샷 시점(초) 목록
        var list = DevArgs.Get("-shots");
        if (!string.IsNullOrEmpty(list))
        {
            var parts = list.Split(','); var arr = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++) float.TryParse(parts[i], out arr[i]);
            SHOTS = arr;
        }
        if (argsApplied) return;   // 아래 검증 인자는 앱 실행당 1회만(재조립 때 다시 적용하면 이주 결과를 덮는다)
        argsApplied = true;
        // -rich: 검증용 시작 자금 2000, -bells N: 지정 금액
        if (DevArgs.Has("-rich")) G.AddBells(2000);
        var bells = DevArgs.Get("-bells"); if (!string.IsNullOrEmpty(bells) && float.TryParse(bells, out var bv)) G.AddBells(bv);
        // -pads id=lv,id=lv: 발판 레벨 바로 지정(구역 해제 연출·시스템 반응까지 이벤트로 돈다), -sold N, -tut N, -shelf N(선반 병)
        var pads = DevArgs.Get("-pads");
        if (!string.IsNullOrEmpty(pads))
            foreach (var kv in pads.Split(',')) { var a = kv.Split('='); if (a.Length == 2 && Pads.Get(a[0]) != null && int.TryParse(a[1], out var lv)) G.SetPadLv(a[0], lv); }
        if (int.TryParse(DevArgs.Get("-sold"), out var soldV)) G.S.sold = soldV;
        if (int.TryParse(DevArgs.Get("-tut"), out var tutV)) { G.S.tut = tutV; if (tutV >= 3) G.S.handShown = 1; }
        if (int.TryParse(DevArgs.Get("-shelf"), out var shelfV)) G.S.shelf = Mathf.Min(shelfV, G.ShelfCap);
        // -shelfo2 N·-shelffood N: 카운터 산소·식량 칸 채우기(정착민 세 종류 요구·결제 검증용)
        if (int.TryParse(DevArgs.Get("-shelfo2"), out var shelfO2V)) G.S.shelfO2 = Mathf.Min(shelfO2V, G.ShelfCap);
        if (int.TryParse(DevArgs.Get("-shelffood"), out var shelfFoodV)) G.S.shelfFood = Mathf.Min(shelfFoodV, G.ShelfCap);
        // -start x,y: 플레이어 시작 위치(스크린샷용). Player.Start 전에 저장값을 바꿔 둔다
        var st = DevArgs.Get("-start");
        if (!string.IsNullOrEmpty(st)) { var xy = st.Split(','); if (xy.Length == 2 && float.TryParse(xy[0], out var sx) && float.TryParse(xy[1], out var sy)) { G.S.p.x = sx; G.S.p.y = sy; if (Player.I != null) Player.I.pos = new Vector2(sx, sy); } }
    }

    int frames; float fpsT, diagT;
    void Update()
    {
        t += Time.deltaTime;
        // 5초마다 진단 로그(자동 플레이 중): FPS·위치·타깃·입력·캐리 — 봇이 멈출 때 원인 추적용
        frames++; fpsT += Time.unscaledDeltaTime; diagT += Time.unscaledDeltaTime;
        if (autoplay && diagT >= 5f)
        {
            var pl = Player.I; var h = HintSystem.I;
            Debug.Log("diag t=" + t.ToString("0") + " fps=" + (frames / fpsT).ToString("0.0") + " pos=" + (pl ? pl.pos.ToString("0") : "-") + " carry=" + G.Carry + "/" + G.CarryMax + " pend=" + G.pend + G.pendK
                + " target=" + (h ? h.targetKey + h.target.ToString("0") : "-") + " dist=" + (pl && h ? Vector2.Distance(pl.pos, h.target).ToString("0") : "-")
                + " ovr=" + (FloatingJoystick.Override.HasValue ? FloatingJoystick.Override.Value.ToString("0.00") : "null") + " sup=" + (FloatingJoystick.I ? FloatingJoystick.I.suppressed : false)
                + " walk=" + (pl && pl.walking) + " esc=" + escapeT.ToString("0.0") + " stuck=" + stuckT.ToString("0.0") + " actT=" + (pl ? pl.actT.ToString("0.00") : "-")
                + " bells=" + G.S.bells.ToString("0") + " sold=" + G.S.sold + " meltQ=" + G.S.meltQ + " water=" + G.S.water + " bottles=" + G.S.bottles + " shelf=" + G.S.shelf
                + " | elec=" + G.S.elecQ + "/" + G.S.elecOut + " purif=" + G.S.purQ + "/" + G.S.purOut + " mixer=" + G.S.mixQ + "/" + G.S.mixOut + " green=" + G.S.grnQ + "/" + G.S.grnOut
                + " fertMade=" + G.S.fertMade + " top=" + G.CarryTop + " dumped=" + dumped);
            frames = 0; fpsT = 0; diagT = 0;
        }
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SaveSystem.Wipe(); G.S = new SaveData();
            Bootstrap.Restart();
        }
        if (Input.GetKeyDown(KeyCode.F6)) { G.AddBells(500); }
        if (autoplay) Bot();
        if (!string.IsNullOrEmpty(shotDir) && shotIdx < SHOTS.Length && t >= SHOTS[shotIdx])
        {
            float st = SHOTS[shotIdx]; string p = Path.Combine(shotDir, "shot_" + (Mathf.Approximately(st, Mathf.Round(st)) ? Mathf.RoundToInt(st).ToString("000") : Mathf.RoundToInt(st * 100).ToString("00000")) + ".png");   // 소수 시점은 1/100초 단위 이름
            ScreenCapture.CaptureScreenshot(p);
            Debug.Log("screenshot " + p + " pos=" + (Player.I ? Player.I.pos.ToString("0") : "-") + " bells=" + G.S.bells + " sold=" + G.S.sold + " tut=" + G.S.tut + " shelf=" + G.S.shelf + " bottles=" + G.S.bottles + " meltQ=" + G.S.meltQ
                + " shelfO2=" + G.S.shelfO2 + " shelfFood=" + G.S.shelfFood + " supW=" + G.S.supW + " supO=" + G.S.supO + " supF=" + G.S.supF + " custs=" + G.S.custs + " lost=" + G.S.lost);
            shotIdx++;
            if (shotIdx >= SHOTS.Length) StartCoroutine(QuitSoon());
        }
    }

    IEnumerator QuitSoon()
    {
        yield return new WaitForSeconds(1.5f);
        G.Save();
        Debug.Log("QA done: bells=" + G.S.bells + " sold=" + G.S.sold + " custs=" + G.S.custs + " lost=" + G.S.lost + " pads=" + JsonUtility.ToJson(G.S));
        Application.Quit();
    }

    // 힌트 타깃으로 걷고, 발판이면 멈춰서 납입, 아니면 근접 반경 안에서 잠깐 머문다.
    // 카운터+계산대처럼 가로로 긴 장애물은 직진으로 못 지나므로 Blocked() 위 격자 BFS로 경로를 잡는다(8단위 격자, 0.4초마다 재계산).
    // 봇 정책: 빈손이고 가장 가까운 노출 발판을 바로 살 수 있으면 힌트보다 발판을 우선한다(QA에서 발판·구역을 밟기 위해).
    float holdT, stuckT, escapeT; Vector2 lastPos, escapeDir;
    const float CELL = 8f, GY0 = GameConfig.Y_MIN; const int GW = 51, GH = (int)((GameConfig.H - GameConfig.Y_MIN) / CELL) + 1;   // x 0..400, y Y_MIN(−640)..1040 (생존 기지·2장 개척지 포함)
    readonly List<Vector2> path = new List<Vector2>(); float pathT; Vector2 pathTarget;
    void Bot()
    {
        var h = HintSystem.I; var pl = Player.I; if (h == null || pl == null) return;
        Vector2 target = h.target; string key = h.targetKey;
        // 현재 목표가 시트 스탯(hand·spd·price)이면 살 수 있을 때 시트 구매로 처리(사람은 시트에서 탭)
        var q = QuestManager.I != null ? QuestManager.I.Current : null;
        if (q != null && Pads.Get(q.key) != null && Pads.Get(q.key).menu && G.PadReq(q.key) && !G.PadMaxed(q.key) && G.S.bells >= G.PadRemain(q.key)) PadManager.I.Buy(q.key);
        // 돔 좌석 팁이 병값×3 이상 쌓였으면 빈손일 때 들러 수거(좌석 r34 안, 좌석 충돌 r16 밖)
        int seatTip = 0; int seatI = DomeSystem.I != null && G.Carry == 0 && G.pend == 0 ? DomeSystem.I.RichestSeat(out seatTip) : -1;
        if (seatI >= 0 && seatTip >= G.BottlePrice * 3)
        {
            // 돔 울타리가 막은 뒤로 뒷줄 좌석(y 890) 뒤쪽은 못 들어가므로 화단·분수 사이 앞쪽 안쪽 자리(좌석에서 r34 안)에서 걷는다
            var st = WorldMap.SEATS[seatI];
            target = st.y > WorldMap.DOME.y + 20 ? st + new Vector2(st.x < WorldMap.DOME.x ? 14 : -14, -18) : st + new Vector2(0, 26);
            key = "seat:" + seatI;
        }
        // 병을 든 채 선반이 가득이면(힌트는 카운터에서 기다리라고 함) 그 사이 계산대 돈을 걷는다 — 사람은 이렇게 하므로 봇도.
        // 힌트가 물통을 전기분해기로 보내는 중이면 끼어들지 않는다(돈이 쌓일 때마다 계산대와 북쪽 사이를 오가며 멈춤)
        else if (G.CarryTop == "bottle" && key == "counter" && G.S.shelf + G.shelfPend >= G.ShelfCap && G.S.cash > 0 && !G.collectingCash)
        { var r = WorldMap.STN["register"]; target = new Vector2(r.x, r.y - r.h / 2 - 14); key = "register"; }
        // 정화기·배합기 틈에서 넣어 둔 묶음(흙 → 비료)을 기다리는 중이면 발판 쇼핑으로 끊지 않는다
        else if (G.Carry == 0 && G.pend == 0 && PadManager.I != null && !((key == "purif" || key == "mixer") && SettleStations.I != null && SettleStations.I.Near(SettleStations.Index(key), pl.pos)))
        {
            // ORDER 순서(visOrder)에서 첫 번째로 살 수 있는 발판 → 진행 순서대로 밟는다
            foreach (var id in PadManager.I.visOrder)
                if (G.S.bells >= G.PadRemain(id)) { target = Pads.Get(id).pos; key = "pad:" + id; break; }
        }
        Vector2 d = target - pl.pos; float dist = d.magnitude;
        float stop = key.StartsWith("pad:") ? 12f : key.StartsWith("ice:") || key.StartsWith("rego:") ? 18f : 12f;
        JamGuard(key, dist, stop);
        if (escapeT > 0) { escapeT -= Time.deltaTime; FloatingJoystick.Override = escapeDir; lastPos = pl.pos; return; }
        if (dist > stop)
        {
            // 경로: 타깃이 바뀌었거나 0.4초 지났으면 재계산
            pathT += Time.deltaTime;
            if (path.Count == 0 || (pathTarget - target).sqrMagnitude > 1f || pathT > 0.4f) { pathT = 0; pathTarget = target; FindPath(pl.pos, target); }
            Vector2 next = target;
            while (path.Count > 0 && Vector2.Distance(pl.pos, path[0]) < CELL * 1.2f) path.RemoveAt(0);
            if (path.Count > 0) next = path.Count > 1 ? path[1] : path[0];
            FloatingJoystick.Override = (next - pl.pos).normalized; holdT = 0;
            if (Vector2.Distance(pl.pos, lastPos) < 0.5f) { stuckT += Time.deltaTime; } else stuckT = 0;
            lastPos = pl.pos;
            if (stuckT > 0.3f)
            {
                stuckT = 0; escapeT = 0.4f; path.Clear();
                Vector2 n = new Vector2(-d.y, d.x).normalized; escapeDir = Random.value < 0.5f ? n : -n;
            }
        }
        else
        {
            FloatingJoystick.Override = Vector2.zero; holdT += Time.deltaTime;
            // 발판 납입은 돈이 떨어질 때까지, 그 외는 2.5초 머물면 작은 흔들림으로 타깃 재평가
            if (!h.targetKey.StartsWith("pad:") && holdT > 2.5f) { FloatingJoystick.Override = Random.insideUnitCircle.normalized; holdT = 0; }
        }
    }

    // 격자 BFS (8방향). 타깃 셀이 막혀 있으면 가장 가까운 열린 셀로.
    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 }, DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
    int[] prev = new int[GW * GH]; bool[] blockedCell = new bool[GW * GH]; bool gridBuilt; bool gridDrill, gridDome; int gridFence = -1;
    void BuildGrid()
    {
        // 칸 중심만 보면 게이트 가장자리 칸(예: 온실 북쪽 게이트 x 96)이 열린 칸으로 잡혀, 점 하나인 플레이어가 울타리 끝에 걸려 맴돈다(개척 길에서 확인).
        // 중심 ±2를 같이 봐서 게이트 가장자리 칸은 막고(가운데 통로 x 248 칸은 그대로 열림) 경로가 틈 가운데로 지나가게 한다
        static bool B(float x, float y) => WorldMap.Blocked(x, y) || WorldMap.Blocked(x - 2, y) || WorldMap.Blocked(x + 2, y) || WorldMap.Blocked(x, y - 2) || WorldMap.Blocked(x, y + 2);
        for (int y = 0; y < GH; y++) for (int x = 0; x < GW; x++) blockedCell[y * GW + x] = B(x * CELL, GY0 + y * CELL);
        gridBuilt = true; gridDrill = WorldMap.drillInstalled; gridDome = WorldMap.domeOpen; gridFence = WorldMap.fenceVer;
    }
    void FindPath(Vector2 from, Vector2 to)
    {
        if (!gridBuilt || gridDrill != WorldMap.drillInstalled || gridDome != WorldMap.domeOpen || gridFence != WorldMap.fenceVer) BuildGrid();   // 구역이 열려 울타리가 생기면 격자도 다시
        path.Clear();
        int sx = Mathf.Clamp(Mathf.RoundToInt(from.x / CELL), 0, GW - 1), sy = Mathf.Clamp(Mathf.RoundToInt((from.y - GY0) / CELL), 0, GH - 1);
        int tx = Mathf.Clamp(Mathf.RoundToInt(to.x / CELL), 0, GW - 1), ty = Mathf.Clamp(Mathf.RoundToInt((to.y - GY0) / CELL), 0, GH - 1);
        int start = sy * GW + sx, goal = ty * GW + tx;
        for (int i = 0; i < prev.Length; i++) prev[i] = -1;
        var q = new Queue<int>(); q.Enqueue(start); prev[start] = start;
        int best = start; float bestD = float.MaxValue;
        while (q.Count > 0)
        {
            int c = q.Dequeue(); int cx = c % GW, cy = c / GW;
            float dd = (cx - tx) * (cx - tx) + (cy - ty) * (cy - ty);
            if (dd < bestD) { bestD = dd; best = c; }
            if (c == goal) break;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + DX[k], ny = cy + DY[k];
                if (nx < 0 || ny < 0 || nx >= GW || ny >= GH) continue;
                int n = ny * GW + nx;
                if (prev[n] != -1 || blockedCell[n]) continue;
                if (k >= 4 && (blockedCell[cy * GW + nx] || blockedCell[ny * GW + cx])) continue; // 대각선은 모서리 끼임 방지
                prev[n] = c; q.Enqueue(n);
            }
        }
        var rev = new List<Vector2>();
        for (int c = best; c != start && c != -1; c = prev[c]) rev.Add(new Vector2((c % GW) * CELL, GY0 + (c / GW) * CELL));
        rev.Reverse(); path.AddRange(rev);
    }

    // 산소통·식량을 카운터에 올리는 단계(Player 4번)는 정착민 작업 몫이다. 그게 없으면 카운터에서 등짐이 줄지 않아 봇이 무한 대기하므로
    // 카운터 앞에서 8초 동안 개수가 그대로면 등짐을 비우고 로그를 남긴다(봇 전용, 실제 플레이엔 영향 없음)
    float jamT; int jamN = -1, dumped;
    void JamGuard(string key, float dist, float stop)
    {
        string top = G.CarryTop;
        if ((top != "o2" && top != "food") || key != "counter" || dist > stop + 20f) { jamT = 0; jamN = -1; return; }
        if (G.Carry != jamN) { jamN = G.Carry; jamT = 0; return; }
        jamT += Time.deltaTime;
        if (jamT < 8f || G.pend > 0) return;
        Debug.Log("bot: 카운터가 " + GameState.KindName(top) + " ×" + G.Carry + "을 받지 않아 등짐을 비움");
        dumped += G.Carry; G.S.p.carry.Clear(); jamT = 0; jamN = -1;
    }

    void OnDestroy() { FloatingJoystick.Override = null; }
}

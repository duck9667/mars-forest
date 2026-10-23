using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 검증용: -shot <dir> 스크린샷 시퀀스 후 종료, -autoplay 힌트 타깃을 따라가는 봇, F5 저장 초기화. 원본 tools/qa의 자동 플레이 봇에 대응.
public class DevTools : MonoBehaviour
{
    GameState G => GameState.I;
    string shotDir; bool autoplay; float t;
    float[] SHOTS = { 2f, 10f, 25f, 45f, 70f, 100f };
    int shotIdx;

    void Start()
    {
        shotDir = DevArgs.Get("-shot");
        autoplay = DevArgs.Has("-autoplay");
        if (!string.IsNullOrEmpty(shotDir)) Directory.CreateDirectory(shotDir);
        // -shots 2,10,25,...  스크린샷 시점(초) 목록
        var list = DevArgs.Get("-shots");
        if (!string.IsNullOrEmpty(list))
        {
            var parts = list.Split(','); var arr = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++) float.TryParse(parts[i], out arr[i]);
            SHOTS = arr;
        }
        // -rich: 검증용 시작 자금
        if (DevArgs.Has("-rich")) G.AddBells(2000);
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
                + " bells=" + G.S.bells.ToString("0") + " sold=" + G.S.sold + " meltQ=" + G.S.meltQ + " water=" + G.S.water + " bottles=" + G.S.bottles + " shelf=" + G.S.shelf);
            frames = 0; fpsT = 0; diagT = 0;
        }
        if (Input.GetKeyDown(KeyCode.F5)) { SaveSystem.Wipe(); G.S = new SaveData(); UnityEngine.SceneManagement.SceneManager.LoadScene(0); }
        if (Input.GetKeyDown(KeyCode.F6)) { G.AddBells(500); }
        if (autoplay) Bot();
        if (!string.IsNullOrEmpty(shotDir) && shotIdx < SHOTS.Length && t >= SHOTS[shotIdx])
        {
            string p = Path.Combine(shotDir, "shot_" + Mathf.RoundToInt(SHOTS[shotIdx]).ToString("000") + ".png");
            ScreenCapture.CaptureScreenshot(p);
            Debug.Log("screenshot " + p + " bells=" + G.S.bells + " sold=" + G.S.sold + " tut=" + G.S.tut + " shelf=" + G.S.shelf + " bottles=" + G.S.bottles + " meltQ=" + G.S.meltQ);
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
    const float CELL = 8f; const int GW = 51, GH = 131;   // x 0..400, y 0..1040
    readonly List<Vector2> path = new List<Vector2>(); float pathT; Vector2 pathTarget;
    void Bot()
    {
        var h = HintSystem.I; var pl = Player.I; if (h == null || pl == null) return;
        Vector2 target = h.target; string key = h.targetKey;
        // 현재 목표가 시트 스탯(hand·spd·price)이면 살 수 있을 때 시트 구매로 처리(사람은 시트에서 탭)
        var q = QuestManager.I != null ? QuestManager.I.Current : null;
        if (q != null && Pads.Get(q.key) != null && Pads.Get(q.key).menu && G.PadReq(q.key) && !G.PadMaxed(q.key) && G.S.bells >= G.PadRemain(q.key)) PadManager.I.Buy(q.key);
        if (G.Carry == 0 && G.pend == 0 && PadManager.I != null)
        {
            // ORDER 순서(visOrder)에서 첫 번째로 살 수 있는 발판 → 진행 순서대로 밟는다
            foreach (var id in PadManager.I.visOrder)
                if (G.S.bells >= G.PadRemain(id)) { target = Pads.Get(id).pos; key = "pad:" + id; break; }
        }
        Vector2 d = target - pl.pos; float dist = d.magnitude;
        float stop = key.StartsWith("pad:") ? 12f : key.StartsWith("ice:") ? 18f : 12f;
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
    int[] prev = new int[GW * GH]; bool[] blockedCell = new bool[GW * GH]; bool gridBuilt; bool gridDrill;
    void BuildGrid()
    {
        for (int y = 0; y < GH; y++) for (int x = 0; x < GW; x++) blockedCell[y * GW + x] = WorldMap.Blocked(x * CELL, y * CELL);
        gridBuilt = true; gridDrill = WorldMap.drillInstalled;
    }
    void FindPath(Vector2 from, Vector2 to)
    {
        if (!gridBuilt || gridDrill != WorldMap.drillInstalled) BuildGrid();
        path.Clear();
        int sx = Mathf.Clamp(Mathf.RoundToInt(from.x / CELL), 0, GW - 1), sy = Mathf.Clamp(Mathf.RoundToInt(from.y / CELL), 0, GH - 1);
        int tx = Mathf.Clamp(Mathf.RoundToInt(to.x / CELL), 0, GW - 1), ty = Mathf.Clamp(Mathf.RoundToInt(to.y / CELL), 0, GH - 1);
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
        for (int c = best; c != start && c != -1; c = prev[c]) rev.Add(new Vector2((c % GW) * CELL, (c / GW) * CELL));
        rev.Reverse(); path.AddRange(rev);
    }

    void OnDestroy() { FloatingJoystick.Override = null; }
}

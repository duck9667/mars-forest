using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 명세 §8. 필드명은 웹 저장(mars-bottle-v1)과 맞춘다. JsonUtility가 Dictionary를 못 다루므로 pads는 목록으로 둔다.
[Serializable] public class PadEntry { public string id; public int lv; public float prog; }
// §3.4 장식 6종(0/1, 프레스티지 후에도 유지)
[Serializable] public class DecoSave { public int awning, neon, plant, lights, flag, tile; }
[Serializable] public class PlayerSave { public float x = 200, y = 450; public float dx = 0, dy = 1; public List<string> carry = new List<string>(); }

[Serializable]
public class SaveData
{
    public int v = 1;
    public float bells = 0;
    public int sold = 0, custs = 0;
    public List<PadEntry> pads = new List<PadEntry>();
    public PlayerSave p = new PlayerSave();
    public int water = 0, bottles = 0, shelf = 0, cash = 0;
    public int meltQ = 0; public float meltT = 0, botT = 0;
    public float[] spots = new float[12];
    public long savedAt = 0;
    public bool muted = false;
    public bool vibrate = true;   // 설정: 진동(햅틱) 켜기
    public float zoom = 1f;       // 카메라 확대 배율(1=기본, 클수록 가까이)
    public float spawnT = GameConfig.FIRST_SPAWN;
    public float clock = 0; public int day = 1;
    public float rushT = 0, drillT = 0;
    public int penTip = 0; public float penT = 0;   // §5.1 먼지 토끼가 모은 벨·타이머
    public DecoSave deco = new DecoSave();
    public int terraKeep = 0;                       // §6.3 프레스티지로 남긴 초록 타일 수
    public int quest = 0, prestige = 0, custN = 0, lost = 0;
    public int tut = 0, handShown = 0, qv = 3;   // qv 3: Phase 3(우리·돔 목표 활성)
    public List<int> qskip = new List<int>();    // 보상 없이 넘길 목표 번호(옛 저장 마이그레이션)
    // ---- 생존 기지 (docs/settlement-design.md) ----
    public int shelfO2 = 0, shelfFood = 0;               // 카운터 산소·식량 칸 (물은 shelf)
    public float[] rspots = new float[GameConfig.REGO_SPOTS];   // 레골리스 더미 리스폰 타이머
    public int elecQ, elecOut, purQ, purOut, mixQ, mixOut, grnQ, grnOut;   // 시설 입력 큐·출력 더미
    public float elecT, purT, mixT, grnT;
    public int supW, supO, supF, fertMade;               // 공급 누계(물·산소·식량)·비료 생산 누계
    public int stage = 0;                                // 정착 목표 단계(0~5, 5=정착 완료)
}

public static class SaveSystem
{
    public const float AUTOSAVE = 3f;
    // -save <이름> 으로 저장 파일을 분리할 수 있다(QA 앱 여러 개가 같은 persistentDataPath를 쓰므로)
    static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save" + (string.IsNullOrEmpty(DevArgs.Get("-save")) ? "" : "-" + DevArgs.Get("-save")) + ".json");

    public static SaveData Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
                if (d != null) { Migrate(d); return d; }
            }
        }
        catch (Exception e) { Debug.LogWarning("저장 읽기 실패: " + e.Message); }
        return new SaveData();
    }

    public static void Save(SaveData d)
    {
        try
        {
            d.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            File.WriteAllText(Path, JsonUtility.ToJson(d));
        }
        catch (Exception e) { Debug.LogWarning("저장 실패: " + e.Message); }
    }

    public static void Wipe() { try { if (File.Exists(Path)) File.Delete(Path); } catch { } }

    // §8 migrate(): 누락 필드 채움, sold>=1이면 튜토리얼 완료 처리, 막힌 위치면 시작 위치로.
    static void Migrate(SaveData d)
    {
        if (d.pads == null) d.pads = new List<PadEntry>();
        if (d.p == null) d.p = new PlayerSave();
        if (d.p.carry == null) d.p.carry = new List<string>();
        if (d.spots == null || d.spots.Length < 12) d.spots = new float[12];
        if (d.rspots == null || d.rspots.Length < GameConfig.REGO_SPOTS) d.rspots = new float[GameConfig.REGO_SPOTS];
        if (d.sold >= 1) { d.tut = 3; d.handShown = 1; }
        // 드릴 충돌은 저장된 레벨로 먼저 켜고 나서 위치를 검사해야 드릴 안에 갇힌 저장본을 구제할 수 있다
        if (d.deco == null) d.deco = new DecoSave();
        if (d.qskip == null) d.qskip = new List<int>();
        // Phase 2 저장(qv<3)은 우리·돔 목표(9·10번)를 보상 없이 건너뛰었다. 아직 그 발판이 없으면 9번으로 되돌리고,
        // 그 뒤 이미 보상받은 목표(11번 물병 300개 등)는 qskip에 넣어 다시 보상하지 않는다
        if (d.qv < 3)
        {
            bool pen = false, dome = false; foreach (var e in d.pads) { if (e.id == "pen" && e.lv >= 1) pen = true; if (e.id == "dome" && e.lv >= 1) dome = true; }
            int back = !pen ? 9 : !dome ? 10 : -1;
            if (back >= 0 && d.quest > back) { for (int q = back; q < d.quest; q++) if (q >= 11) d.qskip.Add(q); d.quest = back; }
            d.qv = 3;
        }
        foreach (var e in d.pads) { if (e.id == "drill") WorldMap.drillInstalled = e.lv >= 1; if (e.id == "dome") WorldMap.domeOpen = e.lv >= 1; }
        if (WorldMap.Blocked(d.p.x, d.p.y)) { d.p.x = WorldMap.PLAYER_START.x; d.p.y = WorldMap.PLAYER_START.y; }
        // 비활성·요구조건 미충족 발판의 부분 납입금 환불
        for (int i = d.pads.Count - 1; i >= 0; i--)
        {
            var e = d.pads[i]; var def = Pads.Get(e.id);
            if (def == null) { d.pads.RemoveAt(i); continue; }
            if (def.menu && e.prog > 0) { d.bells += e.prog; e.prog = 0; }
            if (e.lv >= def.max && e.prog > 0) { d.bells += e.prog; e.prog = 0; }
        }
    }
}

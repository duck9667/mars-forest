using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 명세 §8. 필드명은 웹 저장(mars-bottle-v1)과 맞춘다. JsonUtility가 Dictionary를 못 다루므로 pads는 목록으로 둔다.
[Serializable] public class PadEntry { public string id; public int lv; public float prog; }
// §3.4 장식 6종(0/1, 프레스티지 후에도 유지)
[Serializable] public class DecoSave { public int awning, neon, plant, lights, flag, tile; }
[Serializable] public class MissionSave { public string id; public int goal, prog, reward; public bool done, claimed; }
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
    public int stage = 0;                                // 정착 목표 단계(0~10: 1~5 1장 생존, 6~10 2장 마을, 10=화성 마을 선포). 옛 저장의 5는 6단계부터 이어짐
    // ---- 2장 정착지 확장 (docs/chapter2-design.md) ----
    public int pop = 0;                                  // 거주 구역에 사는 정착민 수
    public List<string> story = new List<string>();      // 이미 본 서사 메시지 id
    public List<MissionSave> missions = new List<MissionSave>(); public int missionDay = 0;   // 하루 미션 3개·발급한 날
    public float powerRun = 0;                           // 2장 6단계: 태양광 전력으로 기계가 일한 초(SettlementManager, 2장부터 셈)
    public int missionAll = 0;                           // 2장 9단계: 8단계 뒤 끝낸 하루 미션 누계(SettlementManager.MISSION_GOAL, 예전 0/1 저장은 1개로 이어짐)
    public float evNext = 0;                             // 화성 이벤트(MarsEvents): 다음 판정까지 남은 초(0이면 새로 정함)
    // ---- 3·4장 이어지는 세계 (docs/chapter3-design.md) ----
    public int fundBack = 0;                             // 옛 이주 준비금(fund)을 벨로 돌려줬음(1)
    public List<ShipSave> ships = new List<ShipSave>();  // 착륙장마다 화물선 상태·주문
    public int orders, launches, arrivals;               // 채운 주문·수출 발사·화물선이 데려온 정착민 누계
    public int fuelQ, fuelOut, fuelMade; public float fuelT;   // 연료 공장: 물통 큐·연료통 출력·생산 누계·타이머
    public int rocketFuel; public float rocketT;         // 발사대: 실은 연료, 새 로켓 조립까지 남은 초
    public int refQ, refOut, refMade, veinPicked, roverHauled; public float refT;   // 얼음 협곡: 정제소 얼음 큐·연료 출력·정제 누계·광맥에서 주운 얼음·로버가 나른 얼음
    public float[] vspots = new float[8];                // 광맥 얼음 자리 리스폰 타이머
}
// 착륙장 하나의 화물선: state 0 빈 착륙장(t = 다음 도착까지), 1 내려오는 중, 2 내려앉아 주문 대기, 3 정착민 내림, 4 이륙
[Serializable] public class ShipSave { public int state; public float t; public int needW, needO, needF, gotW, gotO, gotF, reward, seed; }

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
        if (d.story == null) d.story = new List<string>();
        if (d.missions == null) d.missions = new List<MissionSave>();
        if (d.ships == null) d.ships = new List<ShipSave>();
        if (d.vspots == null || d.vspots.Length < 8) d.vspots = new float[8];
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
        // 구역 열림(울타리·북쪽 통행)도 위치 검사 전에 채운다. 비워 두면 북쪽 구역에서 저장한 위치가 막힌 땅으로 보여 시작점으로 튄다(ZoneManager가 다시 채움)
        foreach (var z in WorldMap.ZONES) { bool open = z.open; foreach (var e in d.pads) if (z.pad != null && e.id == z.pad && e.lv >= 1) open = true; WorldMap.SetZoneOpen(z.key, open); }
        Regions.Sync(d);   // 동쪽 길·지역 열림도 위치 검사 전에(우주항·협곡에서 저장한 위치가 막힌 땅으로 보이지 않게)
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

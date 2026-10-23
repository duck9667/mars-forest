using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 명세 §8. 필드명은 웹 저장(mars-bottle-v1)과 맞춘다. JsonUtility가 Dictionary를 못 다루므로 pads는 목록으로 둔다.
[Serializable] public class PadEntry { public string id; public int lv; public float prog; }
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
    public float spawnT = GameConfig.FIRST_SPAWN;
    public float clock = 0; public int day = 1;
    public int quest = 0, prestige = 0, custN = 0, lost = 0;
    public int tut = 0, handShown = 0, qv = 2;
}

public static class SaveSystem
{
    public const float AUTOSAVE = 3f;
    static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

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
        if (d.sold >= 1) { d.tut = 3; d.handShown = 1; }
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

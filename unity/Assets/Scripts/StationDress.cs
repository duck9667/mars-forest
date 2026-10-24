using UnityEngine;

// §9 Kenney Space Kit 모델로 스테이션을 입힌다. 원본 index.html bMelter(585행)·bBottler(596행)·bDrill(623행)의 put 호출과 같은 배치.
// 규약: 각 view의 플레이스홀더 자식 "body"/"top"을 숨기고, 스테이션 중심 W2U(stn.x, stn.y, 0)에 둔 그룹 "models" 아래에 모델을 넣는다
// (view 자체는 월드 원점에 있고 JuiceFX가 view를 스쿼시한다).
public static class StationDress
{
    public static void Dress(Stations st)
    {
        // 제빙기: 바닥판 96×5×64 + 소형 격납고·물탱크(파란 띠)·파이프·굴뚝
        var m = WorldMap.STN["melter"];
        var gm = Group(st.melterView, m, 96, 64);
        var reM = ModelLib.SK(ModelLib.COL.orange);
        ModelLib.Put(gm, "hangar", 36, 44, 26, -8, 5, -2, 0f, reM);                              // 본체: 소형 격납고 (72×44×52)
        ModelLib.Put(gm, "tankBig", 40, 52, 40, 36, 5, -12, 0f, ModelLib.SK(ModelLib.COL.blue)); // 물탱크
        ModelLib.Put(gm, "pipe", 14, 14, 22, 20, 22, -12, Mathf.PI / 2, reM);
        ModelLib.Put(gm, "chimney", 16, 24, 16, -40, 5, -22, 0f, reM);
        // 병입기: 바닥판 + 육각 격납고·펌프 발전기
        var b = WorldMap.STN["bottler"];
        var gb = Group(st.bottlerView, b, 96, 64);
        ModelLib.Put(gb, "hangarRound", 20, 26, 16, -10, 5, -6, 0f, ModelLib.SK(ModelLib.COL.blue, ModelLib.COL.steelD)); // 본체: 육각 격납고 (65×39×45)
        ModelLib.Put(gb, "gen", 30, 30, 30, 38, 5, -6, -Mathf.PI / 2, ModelLib.SK(ModelLib.COL.orange));                   // 펌프 발전기
        DressDrill(st.drillView);
    }

    // 얼음 드릴: 바닥판 80×6×60 + 철골 데릭 2단·발전기·드럼통 묶음 (설치 전엔 view가 꺼져 있어 같이 숨는다)
    public static void DressDrill(Transform drillView)
    {
        if (drillView == null || drillView.Find("models") != null) return;
        var d = WorldMap.STN["drill"];
        var g = Group(drillView, d, 80, 60, 6);
        var re = ModelLib.SK(ModelLib.COL.orange);
        ModelLib.Put(g, "tower", 36, 44, 36, 0, 6, 0, 0f, re);
        ModelLib.Put(g, "tower", 30, 40, 30, 0, 50, 0, Mathf.PI / 4, re); // 철골 데릭 2단 (구멍 사이로 드릴이 보인다)
        ModelLib.Put(g, "gen", 34, 34, 34, -26, 6, 18, 0f, re);
        ModelLib.Put(g, "barrels", 34, 34, 34, 28, 6, -16, 0.4f, ModelLib.SK(ModelLib.COL.blue));
    }

    // 플레이스홀더 숨기고 스테이션 중심 그룹 + 원본의 steelD 바닥판(w×plateH×h)
    static Transform Group(Transform view, WorldMap.Stn s, float w, float h, float plateH = 5f)
    {
        foreach (Transform c in view) if (c.name == "body" || c.name == "top") c.gameObject.SetActive(false);
        var g = new GameObject("models").transform;
        g.SetParent(view, false);
        g.position = WorldMap.W2U(s.x, s.y, 0f);   // 월드 기준(view 피벗이 스테이션 중심)
        Prim.Box(g, "plate", new Vector3(0f, plateH / 2 * GameConfig.K, 0f), new Vector3(w, plateH, h), ModelLib.C(ModelLib.COL.steelD));
        return g;
    }
}

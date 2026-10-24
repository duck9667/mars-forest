using UnityEngine;

// §3.4 장식 DECOS(1회 구매, 프레스티지 후 유지): 차양·네온·선인장·줄전구·깃발·바닥 타일. 시트의 장식 탭(sold≥60)에서 산다.
// — 스텁(Phase 3 구현에서 시각물·구매 처리를 채움)
public class DecoDef { public string id, name, icon; public int price; }

public class DecoManager : MonoBehaviour
{
    public static DecoManager I;
    GameState G => GameState.I;
    public static readonly DecoDef[] DECOS =
    {
        new DecoDef{ id="awning", name="파란 차양",   icon="home",  price=150 },
        new DecoDef{ id="neon",   name="네온 간판",   icon="sign",  price=250 },
        new DecoDef{ id="plant",  name="선인장 화분", icon="drop",  price=200 },
        new DecoDef{ id="lights", name="줄전구",      icon="bolt",  price=300 },
        new DecoDef{ id="flag",   name="보라 깃발",   icon="flag",  price=120 },
        new DecoDef{ id="tile",   name="체크무늬 바닥", icon="shelf", price=350 },
    };
    public const int UNLOCK_SOLD = 60;   // 시트 장식 탭이 열리는 판매 수
    void Awake() { I = this; }
    public bool Has(string id)
    {
        var d = G.S.deco; if (d == null) return false;
        switch (id) { case "awning": return d.awning > 0; case "neon": return d.neon > 0; case "plant": return d.plant > 0; case "lights": return d.lights > 0; case "flag": return d.flag > 0; case "tile": return d.tile > 0; }
        return false;
    }
    /// 벨을 내고 장식을 산다. 성공하면 true
    public bool Buy(string id) => false;
}

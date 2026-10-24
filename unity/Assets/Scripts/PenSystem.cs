using UnityEngine;

// §5.1 먼지 토끼 우리(pen, Phase 3): 우리 해제 후 토끼 3마리가 우리 안을 깡충거리고, penT 22초마다 penTip이 쌓인다.
// 플레이어가 PEN_TIP(327,766) r40에서 수거(§1.1 6번째 행동). — 스텁: 비주얼 패스 이후 Phase 3 구현에서 채운다
public class PenSystem : MonoBehaviour
{
    public static PenSystem I;
    void Awake() { I = this; }
    /// 플레이어 등짐 칩·벨 비행의 기준 위치(우리 팁 자리)
    public Vector3 TipWorld => WorldMap.W2U(WorldMap.PEN_TIP, 6f);
}

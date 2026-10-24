using System.Collections.Generic;
using UnityEngine;

// §4.2·§5.2 휴게 돔(dome, Phase 3): 돔 시설(유리 반구·분수·화단·에어록·좌석 6), 좌석 예약·경로·팁.
// Customers가 결제 후 빈 좌석이 있으면 seatPath로 걸어가 앉았다 팁을 남기고 leavePath로 퇴장한다. — 스텁(Phase 3 구현에서 채움)
public class DomeSystem : MonoBehaviour
{
    public static DomeSystem I;
    GameState G => GameState.I;
    void Awake() { I = this; }
    public bool Open => G != null && G.PadLv("dome") >= 1;
    /// 빈 좌석을 예약한다. 없거나 돔이 닫혀 있으면 false
    public bool TryReserveSeat(out int seat) { seat = -1; return false; }
    public void ReleaseSeat(int seat) { }
    /// 현재 위치에서 좌석까지 경유점(돔 게이트·에어록 경유)
    public List<Vector2> SeatPath(int seat, Vector2 from) => new List<Vector2> { WorldMap.SEATS[Mathf.Clamp(seat, 0, 5)] };
    /// 좌석에서 입구까지 경유점
    public List<Vector2> LeavePath(int seat) => new List<Vector2> { WorldMap.ENTRY };
    /// 좌석에 팁을 남긴다(플레이어가 좌석 r34 안에 들어오면 수거)
    public void LeaveTip(int seat, int amount) { }
}

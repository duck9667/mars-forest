using UnityEngine;

// §5.4 부재 중 수익: savedAt 기준 경과 120s 이상 & 삐걱 있음이면 min(경과, 3h) × rate, 시작·앱 복귀 때 모달 + 동전 14개.
// — 스텁(Phase 3 구현에서 채움)
public class OfflineEarnings : MonoBehaviour
{
    public static OfflineEarnings I;
    void Awake() { I = this; }
}

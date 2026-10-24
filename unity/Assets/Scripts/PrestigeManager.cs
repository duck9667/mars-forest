using UnityEngine;

// §6.3 프레스티지 "새 기지로 이주": 조건(ice2·pen·dome 해금 && sold≥300 && terra≥0.6), 초기화·유지·영구 보너스.
// 시트 최상단 카드(sold≥60 또는 prestige>0부터 노출)가 CanPrestige/Prestige를 부른다. — 스텁(Phase 3 구현에서 채움)
public class PrestigeManager : MonoBehaviour
{
    public static PrestigeManager I;
    void Awake() { I = this; }
    /// 시트 카드 노출 여부
    public bool ShowCard => GameState.I != null && (GameState.I.S.sold >= 60 || GameState.I.S.prestige > 0);
    public bool CanPrestige() => false;
    /// 조건 진행 한 줄(예: "판매 120/300 · 테라 42% · 돔")
    public string ProgressText() => "";
    /// 이주 실행: 저장을 초기화·보너스 반영 후 Bootstrap.Restart()
    public void Prestige() { }
}

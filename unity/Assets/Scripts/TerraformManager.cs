using UnityEngine;

// §6.4 테라포밍: terra() = clamp(0.5*(ice2+pen+dome)/3 + 0.5*(Σ발판 레벨)/48), 초록 타일(TERRA_SLOTS 45) 점등, 땅색 lerp.
// — 스텁(Phase 3 구현에서 채움)
public class TerraformManager : MonoBehaviour
{
    public static TerraformManager I;
    void Awake() { I = this; }
    public float Terra() => 0f;
}

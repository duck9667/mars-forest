using UnityEngine;

// 마지막 안전망: 리스너(최종 믹스)에 붙는 피크 리미터. 예산(AudioManager.Mix)을 넘는 순간이 와도 출력이 T를 넘지 않는다.
// 즉시 어택(넘는 표본에서 바로 눌러 0 오버슈트) · 느린 회복(약 80ms). 오프라인 덤프 믹스도 같은 Lim을 쓴다.
public class AudioLimiter : MonoBehaviour
{
    public readonly Lim lim = new Lim();
    void Awake() { lim.SetRate(AudioSettings.outputSampleRate); }
    void OnAudioFilterRead(float[] data, int channels) { lim.Process(data, channels); }

    public class Lim
    {
        public const float T = 0.9f;   // 출력 상한(-0.9dBFS)
        float g = 1f, rel = 0.0003f;
        public float minGain = 1f;     // 점검용: 가장 깊이 눌렀던 배율
        public void SetRate(int sr) { rel = 1f - Mathf.Exp(-1f / (0.08f * Mathf.Max(8000, sr))); }
        public void Process(float[] data, int ch)
        {
            for (int i = 0; i + ch <= data.Length; i += ch)
            {
                float pk = 0f;
                for (int c = 0; c < ch; c++) { float a = data[i + c]; if (a < 0) a = -a; if (a > pk) pk = a; }
                float tg = pk > T ? T / pk : 1f;
                if (tg < g) g = tg; else g += (1f - g) * rel;
                if (g < minGain) minGain = g;
                for (int c = 0; c < ch; c++) data[i + c] *= g;
            }
        }
    }
}

using System;
using System.Collections.Generic;

// 소리 합성기: 효과음·배경 루프를 전부 코드로 만든다(외부 음원·유료 라이브러리 없음).
// 결과는 모노 float[](피크 PEAK로 정규화)이고, AudioClip 변환·재생·믹스는 AudioManager가 맡는다.
// 난수는 고정 시드라 실행마다 같은 소리가 나온다(-dumpaudio 점검을 재현할 수 있게).
// 고정 주파수는 회전 페이저(곱셈 4번)로 돌려 Math.Sin 호출을 줄인다: 시작 때 한 번만 합성하지만 폰에서도 가볍게.
public static class SfxSynth
{
    public const float PEAK = 0.7f;   // 정규화 피크(-3dBFS). 겹침 여유는 AudioManager 예산·리미터가 맡는다
    public const int SR = 44100;      // 효과음(동전·딸깍의 고음이 살게)
    public const int SR_MID = 22050;  // 지글(고음 잡음이 필요한 루프)
    public const int SR_LOW = 11025;  // 바람·앰비언트·모터 웅웅(저음뿐인 루프)
    const double TAU = Math.PI * 2;

    public class Snd
    {
        public string name; public float[] d; public int sr; public bool loop;
        public float Len => d.Length / (float)sr;
    }

    [ThreadStatic] static Random rng;   // 루프는 백그라운드 스레드에서 합성하므로 스레드마다 따로
    static float N() => (float)(rng.NextDouble() * 2 - 1);
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // 효과음 13개: 첫 소리가 바로 나야 하니 AudioManager.Awake에서 동기로 한 번
    public static List<Snd> BuildSfx()
    {
        rng = new Random(9667);
        return new List<Snd> { Pick(), Put(), Coin(), Sell(), Ding(), Tick(), Pad(), Up(), No(), Pop(), Whoosh(), Clack(), Drip() };
    }

    // 배경 루프 5개: 길어서 백그라운드 스레드에서 한 번(메인 스레드 멈춤 없음). 클립 변환은 메인 스레드가 한다
    public static List<Snd> BuildLoops()
    {
        rng = new Random(4242);
        return new List<Snd> { Wind(), PadDay(), PadNight(), Sizzle(), Hum() };
    }

    // ───────── 버퍼 도구 ─────────
    class B
    {
        public readonly float[] d; public readonly int sr;
        public B(float len, int sr) { this.sr = sr; d = new float[(int)Math.Ceiling(len * sr)]; }
        int Idx(float t) => (int)(t * sr);

        // 고정 주파수 부분음: 지수 감쇠(tau 초)·짧은 선형 어택
        public void Tone(float at, float f, float amp, float tau, float atk = 0.0015f)
        {
            if (f * 2f >= sr) return;
            int i0 = Idx(at), na = Math.Max(1, (int)(atk * sr));
            double w = TAU * f / sr, c = Math.Cos(w), s = Math.Sin(w), re = 1, im = 0;
            double dec = Math.Exp(-1.0 / (tau * sr)), env = 1;
            for (int i = i0, k = 0; i < d.Length; i++, k++)
            {
                float a = k < na ? k / (float)na : 1f;
                d[i] += (float)(im * env) * amp * a;
                double r2 = re * c - im * s; im = re * s + im * c; re = r2;
                env *= dec; if (env < 1e-4) break;
            }
        }

        // 주파수가 움직이는 음(위상 누적): f(t), 지수 감쇠 tau, h2 = 2배음 섞는 양
        public void Sweep(float at, float len, Func<float, float> f, float amp, float tau, float atk = 0.0015f, float h2 = 0f)
        {
            int i0 = Idx(at), n = (int)(len * sr), na = Math.Max(1, (int)(atk * sr));
            double ph = 0, env = 1, dec = Math.Exp(-1.0 / (tau * sr));
            for (int k = 0; k < n && i0 + k < d.Length; k++)
            {
                float t = k / (float)sr;
                ph += TAU * f(t) / sr;
                float a = k < na ? k / (float)na : 1f;
                d[i0 + k] += (float)((Math.Sin(ph) + h2 * Math.Sin(2 * ph)) * env) * amp * a;
                env *= dec;
            }
        }

        // 잡음 터짐: 한 극 저역·고역 통과(lp·hp Hz), tau 감쇠
        public void Noise(float at, float len, float amp, float tau, float lp, float hp)
        {
            int i0 = Idx(at), n = (int)(len * sr);
            float al = Coef(lp, sr), ah = Coef(hp, sr), yl = 0, yh = 0;
            double env = 1, dec = Math.Exp(-1.0 / (tau * sr));
            for (int k = 0; k < n && i0 + k < d.Length; k++)
            {
                yl += al * (N() - yl);       // 저역 통과
                yh += ah * (yl - yh);        // 고역 통과 = 입력 - 저역
                float ra = k < 13 * sr / 44100 + 1 ? k / (13f * sr / 44100 + 1) : 1f;   // 0.3ms 어택: 첫 표본부터 확 튀는 딸깍을 둥글게
                d[i0 + k] += (yl - yh) * (float)env * amp * ra;
                env *= dec;
            }
        }

        // 끝 처리: 직류 제거 → 피크 정규화 → 꼬리 4ms 페이드(잘린 끝 딸깍 방지)
        public float[] Done(float peak = PEAK, float fadeMs = 4f)
        {
            double m = 0; for (int i = 0; i < d.Length; i++) m += d[i]; m /= Math.Max(1, d.Length);
            float pk = 0; for (int i = 0; i < d.Length; i++) { d[i] -= (float)m; pk = Math.Max(pk, Math.Abs(d[i])); }
            if (pk > 1e-6f) { float g = peak / pk; for (int i = 0; i < d.Length; i++) d[i] *= g; }
            int nf = Math.Min(d.Length, (int)(fadeMs * 0.001f * sr));
            for (int i = 0; i < nf; i++) d[d.Length - 1 - i] *= i / (float)nf;
            return d;
        }
    }

    static float Coef(float fc, int sr) => 1f - (float)Math.Exp(-TAU * Math.Max(1f, fc) / sr);
    static float Ex(float t, float tau) => (float)Math.Exp(-t / tau);
    static Snd S(string n, float[] d, int sr, bool loop = false) => new Snd { name = n, d = d, sr = sr, loop = loop };

    // ───────── 효과음 ─────────

    // 줍기(기준 520Hz): 통통 튀는 짧은 팝. 아래로 꺾이는 톡 + 45ms 뒤 살짝 높은 되튐. 연속 줍기 음 높이 상승은 호출 쪽 PlayHz가 준다
    static Snd Pick()
    {
        var b = new B(0.14f, SR);
        b.Sweep(0f, 0.11f, t => 520f * (1f + 0.75f * Ex(t, 0.010f)), 1f, 0.032f, 0.0015f, 0.18f);
        b.Sweep(0.045f, 0.08f, t => 660f * (1f + 0.5f * Ex(t, 0.008f)), 0.32f, 0.022f, 0.0015f, 0.1f);
        b.Noise(0f, 0.008f, 0.22f, 0.0015f, 6500f, 1500f);
        return S("pick", b.Done(), SR);
    }

    // 내려놓기: 둔탁한 탁. 낮게 떨어지는 몸통 + 짧은 저역 잡음 + 상자 울림 한 점
    static Snd Put()
    {
        var b = new B(0.15f, SR);
        b.Sweep(0f, 0.14f, t => 140f * (1f + 1.4f * Ex(t, 0.007f)), 1f, 0.028f, 0.001f, 0.12f);
        b.Noise(0f, 0.05f, 0.9f, 0.010f, 1100f, 80f);
        b.Tone(0f, 410f, 0.16f, 0.016f, 0.001f);
        return S("put", b.Done(), SR);
    }

    // 금속 한 번 치기(비조화 부분음: 동전·종 계열)
    static void Strike(B b, float at, float f, float amp, float tl = 1f)
    {
        b.Tone(at, f, 1.00f * amp, 0.110f * tl, 0.0008f);
        b.Tone(at, f * 2.76f, 0.50f * amp, 0.070f * tl, 0.0008f);
        b.Tone(at, f * 5.40f, 0.26f * amp, 0.040f * tl, 0.0008f);
        b.Tone(at, f * 8.93f, 0.10f * amp, 0.020f * tl, 0.0008f);
        b.Noise(at, 0.004f, 0.25f * amp, 0.0012f, 9000f, 4000f);
    }

    // 동전(기준 1100Hz): 짤랑. 두 번 부딪힘(두 번째는 4도 위)
    static Snd Coin()
    {
        var b = new B(0.30f, SR);
        Strike(b, 0f, 1100f, 1f);
        Strike(b, 0.05f, 1100f * 1.335f, 0.62f);
        return S("coin", b.Done(), SR);
    }

    // 판매: 계산대 착(짧은 고역 잡음) + 동전 두 번 + 은은한 종 꼬리
    static Snd Sell()
    {
        var b = new B(0.45f, SR);
        b.Noise(0f, 0.04f, 0.45f, 0.012f, 7000f, 2500f);
        Strike(b, 0f, 1320f, 0.7f);
        Strike(b, 0.075f, 1760f, 1f, 1.4f);
        b.Tone(0.075f, 2640f, 0.15f, 0.25f, 0.002f);
        return S("sell", b.Done(), SR);
    }

    // 종(기준 1320Hz): 부분음 5개 + 살짝 어긋난 쌍(맥놀이 반짝임)
    static void Bell(B b, float at, float f, float amp, float tl = 1f)
    {
        float[] r = { 1f, 2f, 3.01f, 4.16f, 5.43f }, a = { 1f, 0.4f, 0.2f, 0.1f, 0.05f }, tau = { 0.55f, 0.30f, 0.18f, 0.10f, 0.06f };
        for (int i = 0; i < r.Length; i++) b.Tone(at, f * r[i], a[i] * amp, tau[i] * tl, 0.0015f);
        b.Tone(at, f * 1.004f, 0.35f * amp, 0.5f * tl, 0.0015f);
    }

    static Snd Ding()
    {
        var b = new B(0.85f, SR);
        Bell(b, 0f, 1320f, 1f);
        return S("ding", b.Done(), SR);
    }

    // 틱(기준 480Hz): 발판 결제의 코인 틱. 아주 짧은 금속 딸깍
    static Snd Tick()
    {
        var b = new B(0.05f, SR);
        b.Tone(0f, 960f, 1f, 0.010f, 0.0005f);
        b.Tone(0f, 2640f, 0.5f, 0.006f, 0.0005f);
        b.Tone(0f, 4300f, 0.25f, 0.004f, 0.0005f);
        b.Noise(0f, 0.003f, 0.3f, 0.001f, 9000f, 3000f);
        return S("tick", b.Done(PEAK, 2f), SR);
    }

    // 발판 완료 딩: 묵직한 툭 + 도·솔 종 두 개(70ms 차) + 반짝 고음
    static Snd Pad()
    {
        var b = new B(0.95f, SR);
        b.Sweep(0f, 0.2f, t => 120f * (1f + 1f * Ex(t, 0.01f)), 0.5f, 0.05f, 0.001f);
        Bell(b, 0f, 1046.5f, 0.8f, 1.1f);
        Bell(b, 0.07f, 1568f, 0.7f, 1.1f);
        b.Tone(0.12f, 3136f, 0.12f, 0.12f, 0.003f);
        return S("pad", b.Done(), SR);
    }

    // 업그레이드: 밝은 상승 아르페지오(도·미·솔·도)
    static Snd Up()
    {
        var b = new B(0.62f, SR);
        float[] f = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < f.Length; i++)
        {
            float at = i * 0.065f, tl = i == f.Length - 1 ? 0.32f : 0.14f;
            b.Tone(at, f[i], 0.8f, tl, 0.003f);
            b.Tone(at, f[i] * 2f, 0.25f, tl * 0.55f, 0.003f);
            b.Tone(at, f[i] * 3f, 0.10f, tl * 0.35f, 0.003f);
        }
        b.Tone(0.2f, 2093f, 0.12f, 0.15f, 0.004f);
        return S("up", b.Done(), SR);
    }

    // 실패: 부드러운 낮은 두 음(라 → 파), 느린 어택이라 거슬리지 않게
    static Snd No()
    {
        var b = new B(0.36f, SR);
        b.Tone(0f, 220f, 1f, 0.07f, 0.010f); b.Tone(0f, 440f, 0.12f, 0.04f, 0.010f);
        b.Tone(0.11f, 175f, 1f, 0.10f, 0.012f); b.Tone(0.11f, 350f, 0.12f, 0.05f, 0.012f);
        var d = b.d; float y = 0, a = Coef(900f, SR);   // 한 번 더 저역 통과: 둥글게
        for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        return S("no", b.Done(PEAK, 20f), SR);
    }

    // 팝(UI·건설): 위로 올라가는 거품 톡
    static Snd Pop()
    {
        var b = new B(0.09f, SR);
        b.Sweep(0f, 0.09f, t => 300f + 520f * (1f - Ex(t, 0.018f)), 1f, 0.022f, 0.001f, 0.1f);
        b.Noise(0f, 0.003f, 0.15f, 0.001f, 8000f, 2000f);
        return S("pop", b.Done(), SR);
    }

    // 휙: 중심이 오르내리는 대역 통과 잡음(상태 변수 필터) + 저역 몸통
    static Snd Whoosh()
    {
        float len = 0.38f; var b = new B(len, SR); var d = b.d;
        float low = 0, band = 0, yl = 0, al = Coef(400f, SR);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)SR, u = t / len, sh = (float)Math.Sin(Math.PI * u);
            float fc = 350f + 1500f * (float)Math.Pow(sh, 1.5), F = 2f * (float)Math.Sin(Math.PI * fc / SR), q = 1f / 0.9f;
            float x = N();
            low += F * band; float high = x - low - q * band; band += F * high;
            yl += al * (x - yl);
            d[i] = (band + 0.4f * yl) * (float)Math.Pow(sh, 1.4);
        }
        return S("whoosh", b.Done(PEAK, 10f), SR);
    }

    // 병입기 딸깍: 두 번 맞물리는 딸깍 + 병 뚜껑 팅 + 짧은 몸통
    static Snd Clack()
    {
        var b = new B(0.15f, SR);
        b.Noise(0f, 0.006f, 1f, 0.0015f, 6000f, 1500f);
        b.Tone(0f, 260f, 0.3f, 0.012f, 0.0005f);
        b.Noise(0.055f, 0.006f, 0.8f, 0.0015f, 6000f, 1500f);
        b.Tone(0.055f, 1850f, 0.35f, 0.03f, 0.0008f);
        return S("clack", b.Done(), SR);
    }

    // 해빙 한 덩이 끝: 물방울 똑(빠르게 올라가는 사인)
    static Snd Drip()
    {
        var b = new B(0.08f, SR);
        b.Sweep(0f, 0.08f, t => 800f + 900f * (1f - Ex(t, 0.012f)), 1f, 0.022f, 0.001f);
        return S("drip", b.Done(), SR);
    }

    // ───────── 배경 루프 ─────────

    // 잡음 루프 이음매: 길이 L + X로 만든 뒤 꼬리 X를 머리에 등전력 교차(끝 → 처음이 원래 이어지던 표본이 된다)
    static float[] LoopXfade(float[] w, int Ln, int Xn)
    {
        var o = new float[Ln];
        for (int i = 0; i < Ln; i++)
        {
            if (i < Xn) { float k = i / (float)Xn; o[i] = w[i] * (float)Math.Sqrt(k) + w[Ln + i] * (float)Math.Sqrt(1 - k); }
            else o[i] = w[i];
        }
        return o;
    }

    static float[] Norm(float[] d, float peak = PEAK)
    {
        double m = 0; foreach (var v in d) m += v; m /= Math.Max(1, d.Length);
        float pk = 0; for (int i = 0; i < d.Length; i++) { d[i] -= (float)m; pk = Math.Max(pk, Math.Abs(d[i])); }
        if (pk > 1e-6f) { float g = peak / pk; for (int i = 0; i < d.Length; i++) d[i] *= g; }
        return d;
    }

    // 화성 바람: 갈색 잡음 → 천천히 움직이는 저역 통과(돌풍) + 아주 옅은 휘파람(좁은 대역)
    static Snd Wind()
    {
        int sr = SR_LOW; float Ls = 12f, Xs = 1.5f; int Ln = (int)(Ls * sr), Xn = (int)(Xs * sr);
        var w = new float[Ln + Xn];
        float br = 0, y = 0, hp = 0, low = 0, band = 0, ahp = Coef(30f, sr);
        float ay = 0, g = 0, F = 0, wa = 0; const float q = 1f / 10f;
        for (int i = 0; i < w.Length; i++)
        {
            if ((i & 31) == 0)   // 느린 변조값은 32표본마다(제어 속도)만 다시 계산
            {
                float t = i / (float)sr;
                float fc = 260f + 220f * (0.5f + 0.5f * (float)Math.Sin(TAU * t / 6.0)) + 120f * (float)Math.Sin(TAU * t / 2.7 + 1);
                ay = Coef(Math.Max(120f, fc), sr);
                g = 0.55f + 0.25f * (float)Math.Sin(TAU * t / 6.0 + 0.5) + 0.2f * (float)Math.Sin(TAU * t / 3.7);
                float wc = 540f + 110f * (float)Math.Sin(TAU * t / 7.1);
                F = 2f * (float)Math.Sin(Math.PI * wc / sr);
                wa = 0.05f * (0.5f + 0.5f * (float)Math.Sin(TAU * t / 4.3));
            }
            br = br * 0.996f + N() * 0.05f;
            y += ay * (br - y);
            hp += ahp * (y - hp);
            low += F * band; float high = N() - low - q * band; band += F * high;
            w[i] = (y - hp) * g + band * wa;
        }
        Norm(w);
        return S("wind", LoopXfade(w, Ln, Xn), sr, true);
    }

    // 회전 페이저: im = sin(w·n + ph). Step은 곱셈 4번, Fix는 반올림 누적으로 커지는 반지름을 1로 되돌린다
    struct Ph
    {
        double re, c, s; public double im;
        public Ph(double w, double ph) { c = Math.Cos(w); s = Math.Sin(w); re = Math.Cos(ph); im = Math.Sin(ph); }
        public void Step() { double r2 = re * c - im * s; im = re * s + im * c; re = r2; }
        public void Fix() { double m = Math.Sqrt(re * re + im * im); re /= m; im /= m; }
    }

    // 고정 주파수 사인 합 루프(주파수를 1/L Hz(8초 → 0.125Hz) 배수로 골라 정수 주기 → 이음매 없음). tones: (주파수, 크기, 숨쉬기 LFO 주기 수, 위상)
    static float[] Drone(int sr, float Ls, (float f, float a, int k, float ph)[] tones, float h2)
    {
        int Ln = (int)(Ls * sr); var d = new float[Ln];
        foreach (var tn in tones)
        {
            // 본음·1/L Hz 어긋난 쌍(루프 한 바퀴에 한 번 맥놀이)·2배음을 페이저로
            var o1 = new Ph(TAU * tn.f / sr, 0); var o2 = new Ph(TAU * (tn.f + 1f / Ls) / sr, 0);
            var o3 = new Ph(TAU * tn.f * 2 / sr, 0); var ol = new Ph(TAU * tn.k / Ln, tn.ph);
            for (int i = 0; i < Ln; i++)
            {
                float lfo = 0.55f + 0.45f * (float)ol.im;
                d[i] += (float)(o1.im + 0.6 * o2.im + h2 * o3.im) * tn.a * lfo;
                o1.Step(); o2.Step(); o3.Step(); ol.Step();
                if ((i & 4095) == 4095) { o1.Fix(); o2.Fix(); o3.Fix(); ol.Fix(); }
            }
        }
        return d;
    }

    // 낮 앰비언트: 라 열린 화음(A2·E3·B3·E4), 아주 느린 숨쉬기
    static Snd PadDay() => S("pad_day", Norm(Drone(SR_LOW, 8f, new[] { (110f, 1f, 1, 0f), (165f, 0.7f, 2, 1.3f), (247.5f, 0.45f, 3, 2.1f), (330f, 0.3f, 1, 4f) }, 0.1f)), SR_LOW, true);

    // 밤 앰비언트: 한 옥타브 가까이 낮은 레 5도(D2·A2·D3·A3), 배음 적게(더 어둡고 조용하게)
    static Snd PadNight() => S("pad_night", Norm(Drone(SR_LOW, 8f, new[] { (73.375f, 1f, 1, 0.4f), (110f, 0.6f, 2, 2.2f), (146.875f, 0.35f, 1, 3.3f), (220f, 0.15f, 3, 5f) }, 0.04f)), SR_LOW, true);

    // 해빙기 지글: 고역 쉿 + 무작위 탁탁 튀는 소리(초당 ~55번)
    static Snd Sizzle()
    {
        int sr = SR_MID; float Ls = 2.5f, Xs = 0.3f; int Ln = (int)(Ls * sr), Xn = (int)(Xs * sr);
        var w = new float[Ln + Xn];
        float l1 = 0, a1 = Coef(2500f, sr), l2 = 0, a2 = Coef(1500f, sr), cr = 0, crDec = 0;
        for (int i = 0; i < w.Length; i++)
        {
            float t = i / (float)sr, x = N();
            l1 += a1 * (x - l1); float hiss = (x - l1) * 0.18f * (0.7f + 0.3f * (float)Math.Sin(TAU * t * 1.9));
            if (rng.NextDouble() < 55.0 / sr) { cr = R(0.4f, 1f); crDec = (float)Math.Exp(-1.0 / (R(0.6f, 2.5f) * 0.001f * sr)); }
            float c = cr * N(); cr *= crDec;
            l2 += a2 * (c - l2);
            w[i] = hiss + (c - l2);
        }
        Norm(w);
        return S("sizzle", LoopXfade(w, Ln, Xn), sr, true);
    }

    // 병입기 모터 웅웅: 50·100·200·300Hz, 4Hz로 살짝 흔들림(2초 정수 주기)
    static Snd Hum()
    {
        int sr = SR_LOW, Ln = 2 * sr; var d = new float[Ln];
        for (int i = 0; i < Ln; i++)
        {
            double t = i / (double)sr;
            float am = 0.85f + 0.15f * (float)Math.Sin(TAU * 4 * t);
            d[i] = (float)(0.25 * Math.Sin(TAU * 50 * t) + Math.Sin(TAU * 100 * t) + 0.35 * Math.Sin(TAU * 200 * t) + 0.15 * Math.Sin(TAU * 300 * t)) * am;
        }
        return S("hum", Norm(d), sr, true);
    }
}

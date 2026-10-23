using System.Collections.Generic;
using UnityEngine;

// 효과음: 원본은 WebAudio 신스. 에셋 없이 재현하려고 클립을 런타임에 합성한다(피치 파라미터 유지).
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    AudioSource[] srcs; int rr;
    const int SR = 22050;

    void Awake()
    {
        I = this;
        srcs = new AudioSource[8];
        for (int i = 0; i < srcs.Length; i++) { srcs[i] = gameObject.AddComponent<AudioSource>(); srcs[i].playOnAwake = false; }
        clips["pick"] = Tone("pick", 0.08f, t => Sine(520, t), Decay(0.08f));
        clips["put"] = Tone("put", 0.07f, t => Sine(300, t) * 0.7f + Noise() * 0.3f, Decay(0.07f));
        clips["coin"] = Tone("coin", 0.12f, t => Sine(1100, t) * 0.7f + Sine(2200, t) * 0.3f, Decay(0.12f));
        clips["sell"] = Tone("sell", 0.2f, t => t < 0.09f ? Sine(660, t) : Sine(990, t), Decay(0.2f));
        clips["ding"] = Tone("ding", 0.45f, t => Sine(1320, t) * 0.6f + Sine(2640, t) * 0.2f, Decay(0.45f));
        clips["tick"] = Tone("tick", 0.03f, t => Sine(480, t), Decay(0.03f));
        clips["pad"] = Tone("pad", 0.28f, t => Sine(400 + 400 * (t / 0.28f), t), Decay(0.28f));
        clips["up"] = Tone("up", 0.36f, t => Sine(t < 0.12f ? 523 : t < 0.24f ? 659 : 784, t), Decay(0.36f, 0.5f));
        clips["no"] = Tone("no", 0.15f, t => Mathf.Sign(Sine(180, t)) * 0.4f, Decay(0.15f));
        clips["pop"] = Tone("pop", 0.1f, t => Sine(220 + 500 * (1 - t / 0.1f), t), Decay(0.1f));
        clips["whoosh"] = Tone("whoosh", 0.18f, t => Noise() * 0.5f, t => Mathf.Sin(Mathf.PI * t / 0.18f));
    }

    static float Sine(float f, float t) => Mathf.Sin(2 * Mathf.PI * f * t);
    static float Noise() => Random.Range(-1f, 1f);
    static System.Func<float, float> Decay(float len, float pow = 2f) => t => Mathf.Pow(1 - Mathf.Clamp01(t / len), pow);

    AudioClip Tone(string name, float len, System.Func<float, float> wave, System.Func<float, float> env)
    {
        int n = Mathf.CeilToInt(len * SR);
        var d = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)SR; d[i] = wave(t) * env(t) * 0.5f; }
        var c = AudioClip.Create(name, n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    public void Play(string name, float pitch = 1f, float vol = 1f)
    {
        if (GameState.I != null && GameState.I.S.muted) return;
        if (!clips.TryGetValue(name, out var c)) return;
        var s = srcs[rr = (rr + 1) % srcs.Length];
        s.pitch = pitch; s.volume = vol; s.clip = c; s.Play();
    }
    // 주파수 목표를 피치로 환산(기준 주파수 대비)
    public void PlayHz(string name, float hz, float baseHz, float vol = 1f) => Play(name, hz / baseHz, vol);
}

using System.Collections.Generic;
using UnityEngine;

// 효과음·배경 소리. 클립은 SfxSynth가 시작 때 한 번 합성해 캐시한다(외부 음원 없음, 폰 성능).
// 이름·Play/PlayHz 시그니처는 예전 그대로(다른 파일의 호출을 바꾸지 않는다. 새 이름이 필요하면 ALIAS로 잇는다).
// 믹스 규칙: 마스터 낮게 · 같은 소리 동시 재생 수 제한 · 진폭 합 예산(넘치면 울리는 소리를 눌러 자리 마련) · 리스너 리미터.
// 배경: 아주 조용한 화성 바람 루프 + 낮/밤 앰비언트(밤엔 낮고 조용하게) + 기계 근처에서만 들리는 해빙기 지글·병입기 웅웅(거리 감쇠).
// 소리 끄기(S.muted)는 효과음을 막고 배경을 서서히 내린다. QA(-shot·-mute)는 Bootstrap이 AudioListener.volume=0으로 끈다.
// 점검: -dumpaudio <폴더> → 클립 원본과 시나리오 믹스(실제와 같은 규칙·리미터)를 WAV로 저장(자동 플레이 인자가 없으면 저장 뒤 종료).
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;

    // ───────── 믹스 기준 ─────────
    public const float MASTER = 0.5f;     // 효과음 마스터(QA 때 소리를 싫어했다 → 게임 중에도 과하지 않게 낮게)
    public const float BUDGET = 0.85f;    // 동시에 울리는 효과음 진폭(피크) 합 상한. 배경 몫(~0.06)을 더해도 리미터 T(0.9) 아래
    const int VOICES = 12, SAME_CAP = 3;  // 전체 동시 재생 · 같은 소리 동시 재생
    const float SAME_GAP = 0.03f;         // 같은 소리가 이 간격 안에 또 오면 하나로 합친다(한 프레임 몰림 방지)
    // 소리별 기본 크기(피크 0.7 정규화 기준. 짧은 딸깍과 긴 종소리의 체감 크기를 맞춘다)
    static readonly Dictionary<string, float> TRIM = new Dictionary<string, float>
    {
        { "pick", 0.8f }, { "put", 0.75f }, { "coin", 0.55f }, { "sell", 0.6f }, { "ding", 0.5f }, { "tick", 0.7f }, { "pad", 0.6f },
        { "up", 0.55f }, { "no", 0.45f }, { "pop", 0.6f }, { "whoosh", 0.45f }, { "clack", 0.6f }, { "drip", 0.45f },
    };
    // 별칭: 의미 이름 → 합성 클립
    static readonly Dictionary<string, string> ALIAS = new Dictionary<string, string>
    {
        { "hop", "pick" }, { "thud", "put" }, { "clink", "coin" }, { "fail", "no" }, { "done", "pad" }, { "click", "tick" },
    };
    // 배경 음량(AudioSource.volume, 클립 피크 0.7 기준 → 실제 진폭은 ×0.7)
    const float WIND_DAY = 0.06f, WIND_NIGHT = 0.045f, WIND_STORM = 0.11f;
    const float PAD_DAY = 0.035f, PAD_NIGHT = 0.022f;
    const float SIZZLE = 0.10f, HUM = 0.03f, CLACK = 0.5f, DRIP = 0.4f;
    const float NEAR = 2.5f, FAR = 8f;    // 기계 소리 거리 감쇠(유닛, 논리 20 = 1m): NEAR 안은 그대로, FAR 밖은 0

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, SfxSynth.Snd> snds = new Dictionary<string, SfxSynth.Snd>();
    AudioSource[] srcs;
    readonly Mix mix = new Mix(VOICES);
    AudioSource wind, padDay, padNight, sizzle, hum;
    int lastBottles = -1, lastMeltQ = -1, lastWater = -1;
    float synthMs;
    // 소리 전용 난수(UnityEngine.Random을 건드리면 게임·자동 플레이의 난수 순서가 바뀐다)
    static readonly System.Random rnd = new System.Random(7);

    void Awake()
    {
        I = this;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (sfxData == null) sfxData = SfxSynth.BuildSfx();   // 앱 실행당 한 번(Restart 재조립 때는 캐시 재사용)
        foreach (var s in sfxData) AddClip(s);
        synthMs = (float)sw.Elapsed.TotalMilliseconds;
        srcs = new AudioSource[VOICES];
        for (int i = 0; i < srcs.Length; i++) { srcs[i] = gameObject.AddComponent<AudioSource>(); srcs[i].playOnAwake = false; srcs[i].priority = 64; }
        // 배경 루프는 길어서 백그라운드 스레드에서 합성(-dumpaudio는 바로 써야 하니 동기)
        if (loopData == null && !string.IsNullOrEmpty(DevArgs.Get("-dumpaudio"))) loopData = SfxSynth.BuildLoops();
        if (loopData == null && loopTask == null) loopTask = System.Threading.Tasks.Task.Run(() => SfxSynth.BuildLoops());
        TakeLoops();
    }

    void AddClip(SfxSynth.Snd s)
    {
        snds[s.name] = s;
        if (!clipCache.TryGetValue(s.name, out var c) || c == null)   // 씬 다시 읽기에서 클립이 내려갔으면 데이터 캐시로 다시 만든다
        {
            c = AudioClip.Create(s.name, s.d.Length, 1, s.sr, false);
            c.SetData(s.d, 0);
            clipCache[s.name] = c;
        }
        clips[s.name] = c;
    }
    // 합성 결과·클립 캐시(정적: 프레스티지·F5 Restart 뒤에도 다시 합성하지 않는다)
    static List<SfxSynth.Snd> sfxData, loopData;
    static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    // 배경 루프 합성이 끝났으면 클립·소스를 만든다(Update에서 매 프레임 확인, 한 번만)
    static System.Threading.Tasks.Task<List<SfxSynth.Snd>> loopTask; bool loopsOn;
    void TakeLoops()
    {
        if (loopsOn) return;
        if (loopData == null && loopTask != null && loopTask.IsCompleted)
        {
            if (loopTask.IsFaulted) { Debug.LogWarning("audio loops failed: " + loopTask.Exception); loopTask = null; loopsOn = true; return; }   // 배경 없이 효과음만
            loopData = loopTask.Result; loopTask = null;
        }
        if (loopData == null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var s in loopData) AddClip(s);
        wind = Loop("wind"); padDay = Loop("pad_day"); padNight = Loop("pad_night"); sizzle = Loop("sizzle"); hum = Loop("hum");
        loopsOn = true;
        Debug.Log("audio synth sfx_ms=" + synthMs.ToString("0.0") + " loop_clip_ms=" + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " clips=" + clips.Count);
    }

    AudioSource Loop(string n)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false; s.loop = true; s.clip = clips[n]; s.volume = 0f; s.priority = 200;
        return s;
    }

    void Start()
    {
        var dir = DevArgs.Get("-dumpaudio");
        if (!string.IsNullOrEmpty(dir)) Dump(dir);
    }

    // ───────── 효과음 ─────────
    public void Play(string name, float pitch = 1f, float vol = 1f)
    {
        if (GameState.I != null && GameState.I.S.muted) return;
        if (ALIAS.TryGetValue(name, out var al)) name = al;
        if (!clips.TryGetValue(name, out var c)) return;
        float want = vol * Trim(name) * MASTER;
        int slot = mix.Admit(name, c.length / Mathf.Max(0.05f, pitch), want, Time.unscaledTime, out bool ducked);
        if (slot < 0) return;
        var s = srcs[slot];
        s.Stop(); s.clip = c; s.pitch = pitch; s.volume = mix.v[slot].gain; s.Play();
        if (ducked) for (int i = 0; i < srcs.Length; i++) if (i != slot && mix.v[i].on) srcs[i].volume = mix.v[i].gain;
    }
    // 주파수 목표를 피치로 환산(기준 주파수 대비)
    public void PlayHz(string name, float hz, float baseHz, float vol = 1f) => Play(name, hz / baseHz, vol);

    static float Trim(string n) => TRIM.TryGetValue(n, out var t) ? t : 0.5f;

    // 보이스 정책(실제 재생·오프라인 덤프 공용). 진폭은 '볼륨 × 클립 피크'로 어림하고 시간에 따라 선형으로 줄어든다고 본다
    public class Mix
    {
        public struct V { public string name; public float start, len, gain; public bool on; }
        public readonly V[] v;
        public Mix(int n) { v = new V[n]; }

        float Level(int i, float now)
        {
            if (!v[i].on) return 0f;
            float e = (now - v[i].start) / v[i].len;
            if (e >= 1f) { v[i].on = false; return 0f; }
            return v[i].gain * SfxSynth.PEAK * (1f - e);
        }

        // 새 소리 자리 찾기: 반환 = 슬롯(-1이면 합침/생략). ducked = 예산 때문에 울리는 소리들을 눌렀는지
        public int Admit(string name, float len, float want, float now, out bool ducked)
        {
            ducked = false;
            int same = 0, oldestSame = -1, free = -1, oldest = -1; float lastSame = -1e9f, sum = 0f;
            for (int i = 0; i < v.Length; i++)
            {
                float l = Level(i, now);
                if (!v[i].on) { if (free < 0) free = i; continue; }
                sum += l;
                if (oldest < 0 || v[i].start < v[oldest].start) oldest = i;
                if (v[i].name == name)
                {
                    same++; if (v[i].start > lastSame) lastSame = v[i].start;
                    if (oldestSame < 0 || v[i].start < v[oldestSame].start) oldestSame = i;
                }
            }
            if (same > 0 && now - lastSame < SAME_GAP) return -1;
            want /= Mathf.Sqrt(1f + 0.6f * Mathf.Min(same, SAME_CAP));   // 같은 소리가 쌓일수록 하나하나는 조금씩 작게
            int slot = same >= SAME_CAP ? oldestSame : free >= 0 ? free : oldest;
            if (v[slot].on) { sum -= Level(slot, now); v[slot].on = false; }
            float need = want * SfxSynth.PEAK;
            if (sum + need > BUDGET)
            {
                // 울리는 소리를 최대 0.35배까지 눌러 새 소리 몫을 만든다(새 소리가 손맛이라 우선). 그래도 넘치면 새 소리를 줄인다
                float k = sum > 1e-4f ? Mathf.Clamp((BUDGET - need) / sum, 0.35f, 1f) : 1f;
                if (k < 1f) { ducked = true; for (int i = 0; i < v.Length; i++) if (v[i].on) v[i].gain *= k; sum *= k; }
                if (sum + need > BUDGET) need = Mathf.Max(0f, BUDGET - sum);
                want = need / SfxSynth.PEAK;
                if (want < 0.01f) return -1;
            }
            v[slot] = new V { name = name, start = now, len = Mathf.Max(0.02f, len), gain = want, on = true };
            return slot;
        }
    }

    // ───────── 배경·기계 ─────────
    void Update()
    {
        if (limiter == null) AttachLimiter();
        if (!loopsOn) TakeLoops();
        if (wind == null) return;   // 배경 루프가 아직(또는 실패)
        var G = GameState.I; if (G == null) return;
        var S = G.S;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        bool off = S.muted;
        float night = DayNight.I != null ? DayNight.I.night : 0f;
        float storm = MarsEvents.StormK;
        Fade(wind, off ? 0f : Mathf.Lerp(Mathf.Lerp(WIND_DAY, WIND_NIGHT, night), WIND_STORM, storm), dt, 0.6f);
        Fade(padDay, off ? 0f : PAD_DAY * (1f - night) * (1f - 0.6f * storm), dt, 0.5f);
        Fade(padNight, off ? 0f : PAD_NIGHT * night * (1f - 0.6f * storm), dt, 0.5f);

        // 기계: 플레이어와의 거리로만 감쇠(2D 소스 + 손으로 감쇠: 카메라 높이와 무관하게 '근처'가 분명하게)
        var st = Stations.I; Vector3 me = Player.I != null && Player.I.view != null ? Player.I.view.position : Vector3.zero;
        float aMelt = st != null && st.melterView != null && Player.I != null ? Att(me, st.melterView.position) : 0f;
        float aBot = st != null && st.bottlerView != null && Player.I != null ? Att(me, st.bottlerView.position) : 0f;
        Fade(sizzle, off || S.meltQ <= 0 ? 0f : SIZZLE * aMelt, dt, 1.5f);
        Fade(hum, off || S.botT <= 0f ? 0f : HUM * aBot, dt, 1.5f);
        // 병입 한 병 = 딸깍, 해빙 한 덩이 = 똑(상태 변화만 읽는다: 다른 파일 수정 없음)
        if (lastBottles >= 0 && S.bottles > lastBottles && aBot > 0.02f) Play("clack", 0.94f + 0.12f * (float)rnd.NextDouble(), CLACK * aBot);
        if (lastMeltQ >= 0 && S.meltQ < lastMeltQ && S.water > lastWater && aMelt > 0.02f) Play("drip", 0.9f + 0.25f * (float)rnd.NextDouble(), DRIP * aMelt);
        lastBottles = S.bottles; lastMeltQ = S.meltQ; lastWater = S.water;
    }

    static float Att(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z, d = Mathf.Sqrt(dx * dx + dz * dz);
        float k = 1f - Mathf.Clamp01((d - NEAR) / (FAR - NEAR));
        return k * k;
    }

    // 루프 음량을 목표로 천천히 옮기고, 0이면 멈춰 둔다(폰에서 안 들리는 루프를 돌리지 않게)
    static void Fade(AudioSource s, float target, float dt, float speed)
    {
        float v = Mathf.MoveTowards(s.volume, target, dt * speed * Mathf.Max(0.02f, Mathf.Max(target, s.volume)));
        if (Mathf.Abs(v - target) < 0.0005f) v = target;
        s.volume = v;
        if (v <= 0f) { if (s.isPlaying) s.Pause(); }
        else if (!s.isPlaying)
        {
            if (s.time > 0f) s.UnPause();
            else { s.timeSamples = rnd.Next(s.clip.samples); s.Play(); }   // 첫 시작은 루프 아무 데서나(낮·밤 앰비언트가 같은 박자로 맞물리지 않게)
        }
    }

    AudioLimiter limiter; float limTry;
    void AttachLimiter()
    {
        if (Time.unscaledTime < limTry) return;
        limTry = Time.unscaledTime + 0.5f;
        var l = FindAnyObjectByType<AudioListener>();
        if (l == null) return;
        limiter = l.GetComponent<AudioLimiter>();
        if (limiter == null) limiter = l.gameObject.AddComponent<AudioLimiter>();
    }

    // ───────── 점검: -dumpaudio ─────────
    struct Ev { public float t, p, v; public string n; public Ev(float t, string n, float p = 1f, float v = 1f) { this.t = t; this.n = n; this.p = p; this.v = v; } }
    struct Bed { public string n; public float v, p; public Bed(string n, float v, float p = 1f) { this.n = n; this.v = v; this.p = p; } }
    const int OUT_SR = 44100;

    void Dump(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var log = new System.Text.StringBuilder();
        log.Append("audio dump synth_ms=").Append(synthMs.ToString("0.0")).Append('\n');
        foreach (var s in snds.Values) { WriteWav(System.IO.Path.Combine(dir, "clip_" + s.name + ".wav"), s.d, s.sr); }

        // 1) 연속 줍기 15개(가득 차면 딩) — Player.cs 줍기와 같은 피치 공식
        var e = new List<Ev>();
        for (int i = 1; i <= 15; i++) e.Add(new Ev(0.1f + i * 0.11f, "pick", (520 + 380 * (i / 15f)) / 520f));
        e.Add(new Ev(0.1f + 15 * 0.11f, "ding", 1f, 0.7f));
        Scene(dir, "mix_pick_run", 2.6f, e, null, log);
        // 2) 판매 동전 쏟아짐 24개(0.035초 간격) + 판매 3번 + 콤보
        e = new List<Ev>();
        for (int i = 0; i < 24; i++) e.Add(new Ev(0.05f + i * 0.035f, "coin", Mathf.Pow(1.06f, Mathf.Min(12, i)), 0.5f));
        for (int i = 0; i < 3; i++) e.Add(new Ev(0.1f + i * 0.3f, "sell"));
        e.Add(new Ev(0.5f, "coin", 900 * Mathf.Pow(1.12f, 3) / 1100f, 0.6f));
        Scene(dir, "mix_coin_burst", 1.8f, e, null, log);
        // 3) 발판 결제: 틱 40개(오르는 피치) → 완료 딩 + 상승음(PadManager와 같은 호출)
        e = new List<Ev>();
        for (int i = 0; i < 40; i++) e.Add(new Ev(0.05f + i * 0.05f, "tick", (480 + 700 * i / 40f) / 480f, 0.6f));
        e.Add(new Ev(2.1f, "pad")); e.Add(new Ev(2.1f, "up", 1f, 0.6f));
        Scene(dir, "mix_pad_pay", 3.2f, e, null, log);
        // 4) 최악: 한순간에 다 터짐(업그레이드·딩·완료·팝 7·동전 10·탁 5·휙)
        e = new List<Ev> { new Ev(0.05f, "up"), new Ev(0.05f, "ding", 1.2f, 0.8f), new Ev(0.05f, "pad"), new Ev(0.06f, "whoosh", 0.75f, 0.8f) };
        for (int i = 0; i < 7; i++) e.Add(new Ev(0.05f + i * 0.075f, "pop", 1f + i * 0.12f, 0.45f));
        for (int i = 0; i < 10; i++) e.Add(new Ev(0.05f + i * 0.02f, "coin", 1f + i * 0.05f, 0.7f));
        for (int i = 0; i < 5; i++) e.Add(new Ev(0.05f + i * 0.01f, "put", 1f + i * 0.04f));
        Scene(dir, "mix_chaos", 1.6f, e, null, log);
        // 5) 배경: 낮(해빙기·병입기 바로 옆, 딸깍 2.4초마다·똑 3초마다), 밤(기계 멀리), 폭풍
        e = new List<Ev>();
        for (float t = 0.5f; t < 20f; t += 2.4f) e.Add(new Ev(t, "clack", 1f, CLACK));
        for (float t = 1.2f; t < 20f; t += 3f) e.Add(new Ev(t, "drip", 1f, DRIP));
        Scene(dir, "amb_day_machines", 20f, e, new[] { new Bed("wind", WIND_DAY), new Bed("pad_day", PAD_DAY), new Bed("sizzle", SIZZLE), new Bed("hum", HUM) }, log);
        Scene(dir, "amb_night", 20f, new List<Ev>(), new[] { new Bed("wind", WIND_NIGHT), new Bed("pad_night", PAD_NIGHT) }, log);
        Scene(dir, "amb_storm", 12f, new List<Ev>(), new[] { new Bed("wind", WIND_STORM), new Bed("pad_day", PAD_DAY * 0.4f) }, log);
        // 6) 평소 플레이 같은 20초: 낮 배경 + 줍기·내려놓기·판매가 섞여 흐름
        e = new List<Ev>();
        for (int r = 0; r < 4; r++)
        {
            float b0 = 0.5f + r * 4.8f;
            for (int i = 1; i <= 8; i++) e.Add(new Ev(b0 + i * 0.12f, "pick", (520 + 380 * (i / 8f)) / 520f));
            for (int i = 0; i < 8; i++) e.Add(new Ev(b0 + 1.8f + i * 0.09f, "put", 1f + (i % 3 - 1) * 0.04f));
            for (int i = 0; i < 6; i++) e.Add(new Ev(b0 + 3f + i * 0.06f, "coin", Mathf.Pow(1.06f, i), 0.5f));
            e.Add(new Ev(b0 + 3.2f, "sell")); e.Add(new Ev(b0 + 4f, "pop", 1.3f, 0.4f));
        }
        Scene(dir, "mix_play_20s", 20f, e, new[] { new Bed("wind", WIND_DAY), new Bed("pad_day", PAD_DAY), new Bed("hum", HUM * 0.3f) }, log);

        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "dump.txt"), log.ToString());
        Debug.Log(log.ToString());
        if (!DevArgs.Has("-autoplay") && !DevArgs.Has("-shot")) Application.Quit();
    }

    // 시나리오 한 장면을 실제와 같은 규칙(Mix 예산·같은 소리 제한·리미터)으로 그려 저장한다. 출력 = 리스너가 듣는 최종 믹스
    void Scene(string dir, string name, float len, List<Ev> evs, Bed[] beds, System.Text.StringBuilder log)
    {
        evs.Sort((a, b) => a.t.CompareTo(b.t));
        int n = (int)(len * OUT_SR); var o = new float[n];
        var m = new Mix(VOICES);
        var vd = new float[VOICES][]; var vpos = new double[VOICES]; var vstep = new double[VOICES];
        int ei = 0, admitted = 0, merged = 0, ducks = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)OUT_SR;
            while (ei < evs.Count && evs[ei].t <= t)
            {
                var ev = evs[ei++]; var sn = snds[ev.n];
                int slot = m.Admit(ev.n, sn.Len / ev.p, ev.v * Trim(ev.n) * MASTER, t, out bool dk);
                if (slot < 0) { merged++; continue; }
                admitted++; if (dk) ducks++;
                vd[slot] = sn.d; vpos[slot] = 0; vstep[slot] = sn.sr * ev.p / OUT_SR;
            }
            float acc = 0f;
            for (int s = 0; s < VOICES; s++)
            {
                if (vd[s] == null) continue;
                // Mix가 끝났다고 어림한 뒤에도 실제 클립은 끝까지 울린다(실제 AudioSource와 같게)
                int p0 = (int)vpos[s]; if (p0 + 1 >= vd[s].Length) { vd[s] = null; continue; }
                float fr = (float)(vpos[s] - p0);
                acc += (vd[s][p0] * (1 - fr) + vd[s][p0 + 1] * fr) * m.v[s].gain;
                vpos[s] += vstep[s];
            }
            if (beds != null) foreach (var bd in beds) { var sn = snds[bd.n]; double pp = (t * sn.sr * bd.p) % sn.d.Length; int q = (int)pp; float f = (float)(pp - q); acc += (sn.d[q] * (1 - f) + sn.d[(q + 1) % sn.d.Length] * f) * bd.v; }
            o[i] = acc;
        }
        float pre = 0; foreach (var x in o) pre = Mathf.Max(pre, Mathf.Abs(x));
        var lim = new AudioLimiter.Lim(); lim.SetRate(OUT_SR); lim.Process(o, 1);
        WriteWav(System.IO.Path.Combine(dir, name + ".wav"), o, OUT_SR);
        log.Append(name).Append(" len=").Append(len.ToString("0.0")).Append(" prePeak=").Append(pre.ToString("0.000"))
           .Append(" limMin=").Append(lim.minGain.ToString("0.000")).Append(" voices=").Append(admitted).Append(" merged=").Append(merged).Append(" ducks=").Append(ducks).Append('\n');
    }

    static void WriteWav(string path, float[] d, int sr)
    {
        using (var f = new System.IO.BinaryWriter(System.IO.File.Create(path)))
        {
            int bytes = d.Length * 2;
            f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); f.Write(36 + bytes); f.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            f.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); f.Write(16); f.Write((short)1); f.Write((short)1); f.Write(sr); f.Write(sr * 2); f.Write((short)2); f.Write((short)16);
            f.Write(System.Text.Encoding.ASCII.GetBytes("data")); f.Write(bytes);
            foreach (var x in d) f.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * 32767f), -32768, 32767));
        }
    }
}

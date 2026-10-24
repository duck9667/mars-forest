using UnityEngine;

// 씬 파일 없이 런타임에 전부 조립한다(원본 단일 파일 구조와 같은 감각). Main.unity는 빈 씬이면 된다.
public static class Bootstrap
{
    static bool wiped;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Init()
    {
        if (GameState.I != null) return;
        Application.targetFrameRate = 60;
        QualitySettings.pixelLightCount = 6;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        if (DevArgs.Has("-wipe") && !wiped) SaveSystem.Wipe();   // 앱 실행당 1회(Restart 재조립 때 다시 지우지 않게)
        wiped = true;
        // QA(스크린샷 실행)·-mute면 소리를 끈다(사용자 작업 중 숨김 실행되는 QA가 시끄럽지 않게)
        if (DevArgs.Has("-shot") || DevArgs.Has("-mute")) AudioListener.volume = 0f;

        var cam = Camera.main; if (cam != null) Object.Destroy(cam.gameObject);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.Destroy(l.gameObject);

        var root = new GameObject("Game");
        root.AddComponent<GameState>();
        root.AddComponent<AudioManager>();
        new GameObject("WorldLabels").AddComponent<WorldLabels>();
        new GameObject("Joystick").AddComponent<FloatingJoystick>();
        new GameObject("HUD").AddComponent<HUD>();
        new GameObject("Sheet").AddComponent<UpgradeSheet>();
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; camGo.AddComponent<CameraRig>();
        new GameObject("FX").AddComponent<JuiceFX>();
        new GameObject("Flyer").AddComponent<ArcFlyer>();
        new GameObject("World").AddComponent<World>();
        new GameObject("Stations").AddComponent<Stations>();
        new GameObject("Settle").AddComponent<SettleStations>();
        new GameObject("Frontier").AddComponent<Frontier>();               // 2장 개척지: 거주 구역·태양광 단지
        new GameObject("Zones").AddComponent<ZoneManager>();
        new GameObject("Player").AddComponent<Player>();
        new GameObject("Pads").AddComponent<PadManager>();
        new GameObject("Customers").AddComponent<CustomerSystem>();
        new GameObject("Cashier").AddComponent<CashierBot>();
        new GameObject("Runners").AddComponent<RunnerSystem>();
        new GameObject("Drill").AddComponent<DrillStation>();
        new GameObject("AdsSign").AddComponent<AdsSign>();
        new GameObject("Pen").AddComponent<PenSystem>();
        new GameObject("Dome").AddComponent<DomeSystem>();
        new GameObject("Deco").AddComponent<DecoManager>();
        new GameObject("Terraform").AddComponent<TerraformManager>();
        new GameObject("Prestige").AddComponent<PrestigeManager>();
        new GameObject("Offline").AddComponent<OfflineEarnings>();
        new GameObject("DayNight").AddComponent<DayNight>();
        new GameObject("Quests").AddComponent<QuestManager>();
        new GameObject("Missions").AddComponent<MissionBoard>();          // 하루 미션 3개(시트 목표 탭)
        new GameObject("Settlement").AddComponent<SettlementManager>();   // 정착 목표 5단계
        new GameObject("Map").AddComponent<MapScreen>();                  // 지도 화면(HUD 정착 알약)
        new GameObject("Story").AddComponent<StoryManager>();             // 관제사 하나의 무전(인트로·사건 카드)
        new GameObject("Events").AddComponent<MarsEvents>();              // 화성 이벤트(먼지 폭풍·보급선·운석)
        new GameObject("Tutorial").AddComponent<TutorialManager>();
        new GameObject("Hint").AddComponent<HintSystem>();
        new GameObject("Dev").AddComponent<DevTools>();
    }

    // 저장을 디스크에 쓴 뒤 씬을 다시 불러 전부 새로 조립한다(F5 초기화·프레스티지 "새 기지로 이주").
    // RuntimeInitializeOnLoadMethod는 앱당 1회라 sceneLoaded에서 Init을 직접 다시 부른다.
    public static void Restart()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += Reboot;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
    static void Reboot(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Reboot;
        Init();
    }
}

public static class DevArgs
{
    static string[] args;
    static string[] Args => args ?? (args = System.Environment.GetCommandLineArgs());
    public static bool Has(string flag) { foreach (var a in Args) if (a == flag) return true; return false; }
    public static string Get(string flag)
    {
        for (int i = 0; i < Args.Length - 1; i++) if (Args[i] == flag) return Args[i + 1];
        return null;
    }
}

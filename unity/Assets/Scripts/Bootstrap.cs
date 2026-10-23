using UnityEngine;

// 씬 파일 없이 런타임에 전부 조립한다(원본 단일 파일 구조와 같은 감각). Main.unity는 빈 씬이면 된다.
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (GameState.I != null) return;
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        if (DevArgs.Has("-wipe")) SaveSystem.Wipe();

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
        new GameObject("Zones").AddComponent<ZoneManager>();
        new GameObject("Player").AddComponent<Player>();
        new GameObject("Pads").AddComponent<PadManager>();
        new GameObject("Customers").AddComponent<CustomerSystem>();
        new GameObject("Cashier").AddComponent<CashierBot>();
        new GameObject("Runners").AddComponent<RunnerSystem>();
        new GameObject("Drill").AddComponent<DrillStation>();
        new GameObject("AdsSign").AddComponent<AdsSign>();
        new GameObject("DayNight").AddComponent<DayNight>();
        new GameObject("Quests").AddComponent<QuestManager>();
        new GameObject("Tutorial").AddComponent<TutorialManager>();
        new GameObject("Hint").AddComponent<HintSystem>();
        new GameObject("Dev").AddComponent<DevTools>();
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

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// §6.3 프레스티지 "새 기지로 이주": 조건(ice2·pen·dome 해금 && sold≥300 && terra≥0.6), 초기화·유지·영구 보너스.
// 시트 최상단 카드(sold≥60 또는 prestige>0부터 노출)가 CanPrestige/Prestige를 부른다. 원본 index.html 545행(canPrestige)·898행(prestige).
//  - 초기화: pads(padProg 포함)·bells·shelf·bottles·water·cash·meltQ/T·botT·penTip·spots·플레이어(시작 위치·빈 등짐). 손님·좌석은 재조립으로 사라짐
//    생존 기지: 카운터 산소·식량 칸, 레골리스 리스폰, 시설 4개 큐·출력·타이머, 공급·비료 누계, 정착 단계(stage 0부터), 정착민 수(custs: 정착지마다 새로 센다)
//  - 유지: sold·quest·deco·clock/day·custN·lost·tut. 영구 보너스: prestige++(병값 ×1.5^p·등짐 +3은 GameState 파생값), terraKeep += 8
//  - 연출(원본: 플래시 0.8·흔들림 8·팡파르): 플레이어가 작은 로켓에 올라 이륙 → 화면이 크림색으로 덮이면 저장 후 Bootstrap.Restart()
//    → 새 기지에서 덮개가 걷히며 "새 기지 n번째" 배너, 남겨 둔 초록 타일이 팝으로 번진다(TerraformManager가 Arrived를 본다)
//  - 검증 인자: -prestigeAt <초> (그 시점에 조건 무시하고 이주, 앱 실행당 1회). -shot이 있으면 직전 화면을 shot_pre_prestige.png로 남긴다
public class PrestigeManager : MonoBehaviour
{
    public static PrestigeManager I;
    GameState G => GameState.I;

    /// 이번 씬이 이주 직후 다시 조립된 새 기지인가(시작 연출용)
    public static bool Arrived;
    static bool arrivedPending, devFired;
    static GameObject veil;          // 씬 재조립을 가리는 화면 덮개(DontDestroyOnLoad)
    static Image veilImg;
    static readonly Color VEIL = new Color(1f, 0.97f, 0.88f, 1f);   // HUD 플래시와 같은 크림색

    public const int SOLD_GOAL = 300;
    public const float TERRA_GOAL = 0.6f;
    public const int KEEP_PER_PRESTIGE = 8;
    bool busy;
    string devShot;   // 검증용 이륙 스크린샷 폴더(-prestigeAt로 부른 경우만)

    void Awake()
    {
        I = this;
        Arrived = arrivedPending; arrivedPending = false;
    }

    /// 시트 카드 노출 여부
    public bool ShowCard => GameState.I != null && (GameState.I.S.sold >= 60 || GameState.I.S.prestige > 0);
    /// 이주 중(연출 진행)
    public bool Busy => busy;
    /// 지금 병값 배율·이주 후 배율 (시트 카드 "새 기지 ×n" 표시용, 원본 prestigeMul()*1.5)
    public float Multiplier => Mathf.Pow(1.5f, G.S.prestige);
    public float NextMultiplier => Mathf.Pow(1.5f, G.S.prestige + 1);
    int ZonesOpen => (G.PadLv("ice2") > 0 ? 1 : 0) + (G.PadLv("pen") > 0 ? 1 : 0) + (G.PadLv("dome") > 0 ? 1 : 0);
    float TerraNow => TerraformManager.I != null ? TerraformManager.I.Terra() : 0f;

    // §6.3 canPrestige(): 구역 3개 + 판매 300 + 테라포밍 60%
    public bool CanPrestige()
    {
        if (G == null || busy) return false;
        return G.PadLv("dome") > 0 && G.PadLv("pen") > 0 && G.PadLv("ice2") > 0 && G.S.sold >= SOLD_GOAL && TerraNow >= TERRA_GOAL;
    }

    /// 정착 목표 5단계를 다 채웠는가(지도·시트에서 "새 정착지로" 안내)
    public bool Settled => SettlementManager.I != null && SettlementManager.I.Settled;

    /// 조건 진행 한 줄(예: "판매 120/300 · 테라 42%/60% · 구역 2/3"). 정착 완료면 앞에 "정착 완료 · "
    public string ProgressText()
    {
        if (G == null) return "";
        int tp = Mathf.FloorToInt(TerraNow * 100f + 0.5f);
        return (Settled ? "정착 완료 · " : "") + "판매 " + Mathf.Min(G.S.sold, SOLD_GOAL) + "/" + SOLD_GOAL + " · 테라 " + tp + "%/" + Mathf.RoundToInt(TERRA_GOAL * 100f) + "% · 구역 " + ZonesOpen + "/3";
    }

    /// 이주 실행: 연출 → 저장 초기화·보너스 반영 → Bootstrap.Restart()
    public void Prestige()
    {
        if (busy || G == null) return;
        busy = true;
        StartCoroutine(CoPrestige());
    }

    void Start()
    {
        if (Arrived) StartCoroutine(CoArrive());
        else if (veil != null) Destroy(veil);   // 혹시 남은 덮개(이주 중 다른 경로로 재시작된 경우)
    }

    void Update()
    {
        // 검증용 -prestigeAt <초>: 조건 무시하고 이주(앱 실행당 1회, 재조립 뒤엔 다시 안 함)
        if (!devFired && !busy && float.TryParse(DevArgs.Get("-prestigeAt"), out var at) && Time.timeSinceLevelLoad >= at)
        {
            devFired = true;
            StartCoroutine(CoDevPrestige());
        }
    }

    IEnumerator CoDevPrestige()
    {
        var dir = DevArgs.Get("-shot");
        if (!string.IsNullOrEmpty(dir)) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "shot_pre_prestige.png")); yield return null; yield return null; }
        Debug.Log("prestige dev: can=" + CanPrestige() + " " + ProgressText());
        devShot = string.IsNullOrEmpty(dir) ? null : dir;
        Prestige();
    }

    // ---- 이주 연출 ----
    IEnumerator CoPrestige()
    {
        var S = G.S;
        if (UpgradeSheet.I != null) UpgradeSheet.I.Close();
        Debug.Log("prestige before: prestige=" + S.prestige + " terraKeep=" + S.terraKeep + " terra=" + TerraNow.ToString("0.00") + " sold=" + S.sold + " bells=" + S.bells.ToString("0") + " pads=" + S.pads.Count);

        // 원본 prestige(): 플래시 0.8 · 흔들림 8 · 팡파르. + 색종이·링, 진동
        var pl = Player.I;
        Vector3 at = pl != null ? pl.view.position : WorldMap.W2U(WorldMap.PLAYER_START);
        JuiceFX.I.Flash(0.8f); JuiceFX.I.Shake(8); JuiceFX.I.Vibrate(60);
        AudioManager.I.Play("up", 1f); AudioManager.I.Play("ding", 1.2f, 0.8f);
        JuiceFX.I.Confetti(at + Vector3.up * 1.5f, 36);
        JuiceFX.I.Ring(at, Color.white, 60); JuiceFX.I.Ring(at, GameConfig.GOLD, 30);

        // 작은 로켓(Kenney 로켓 파츠 4단, 착륙선과 같은 팔레트)이 플레이어 자리에 팝 → 플레이어 탑승(축소) → 덜덜 → 이륙
        float k = GameConfig.K; const float S_R = 20f;
        var rocket = new GameObject("prestigeRocket").transform; rocket.position = at;
        var body = new GameObject("body").transform; body.SetParent(rocket, false);
        var re = ModelLib.SK(ModelLib.COL.orange, ModelLib.COL.dark);
        ModelLib.Put(body, "rFins", S_R, S_R, S_R, 0, 0, 0, Mathf.PI / 4, re);
        ModelLib.Put(body, "rFuel", S_R, S_R, S_R, 0, .7f * S_R, 0, 0f, re);
        ModelLib.Put(body, "rSides", S_R, S_R * .9f, S_R, 0, 1.2f * S_R, 0, 0f, re);
        ModelLib.Put(body, "rTop", S_R, S_R, S_R, 0, 2.1f * S_R, 0, 0f, re);
        var flame = Prim.Sphere(rocket, "flame", new Vector3(0, -3f * k, 0), 12, GameConfig.Hex("ffc94a")).transform; Prim.NoShadow(flame.gameObject);
        var core = Prim.Sphere(flame, "core", Vector3.zero, 1f, GameConfig.Hex("fff6d0")).transform; Prim.NoShadow(core.gameObject);
        core.localScale = Vector3.one * 0.6f;
        flame.gameObject.SetActive(false);
        JuiceFX.I.Pop(rocket, 0.001f, 0.25f);
        AudioManager.I.Play("pop", 0.8f);
        Vector3 plScale = pl != null ? pl.view.localScale : Vector3.one;

        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
        VeilShow(0f);
        float el = 0f, dustT = 0f; const float BOARD = 0.2f, RUMBLE = 0.4f, END = 1.0f;
        float h = 0f, v = 0f; bool lift = false;
        while (el < END)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, GameConfig.DT_MAX); el += dt;
            if (FloatingJoystick.I) FloatingJoystick.I.suppressed = true;
            // 탑승: 플레이어가 로켓 안으로 쏙
            if (pl != null) pl.view.localScale = plScale * Mathf.Max(0.001f, 1f - Mathf.Clamp01(el / BOARD));
            // 덜덜 + 배기 먼지
            dustT += dt;
            if (el > BOARD && dustT > 0.05f) { dustT = 0f; JuiceFX.I.Dust(at + Vector3.up * 0.1f, 2); }
            if (el > BOARD && el < RUMBLE) body.localPosition = new Vector3(Random.Range(-0.7f, 0.7f), 0, Random.Range(-0.7f, 0.7f)) * k;
            // 이륙: 가속 상승, 불꽃 깜빡, 바람 소리
            if (el >= RUMBLE)
            {
                if (!lift) { lift = true; flame.gameObject.SetActive(true); body.localPosition = Vector3.zero; AudioManager.I.Play("whoosh", 0.7f, 0.9f); JuiceFX.I.Shake(4); JuiceFX.I.Dust(at, 8); JuiceFX.I.Ring(at, GameConfig.Hex("ffc94a"), 40); }
                v += 900f * dt; h += v * dt;
                rocket.position = at + Vector3.up * h * k;
                float f = 1f + Mathf.Sin(el * 60f) * 0.25f;
                flame.localScale = new Vector3(12f, 12f * (1.4f + f), 12f) * k;
            }
            // 검증(-prestigeAt + -shot): 이륙 장면 한 장
            if (devShot != null && el >= 0.6f) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(devShot, "shot_prestige_launch.png")); devShot = null; }
            // 덮개: 이륙과 함께 크림색으로 덮인다
            VeilShow(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((el - 0.6f) / (END - 0.6f))));
            yield return null;
        }
        VeilShow(1f);

        // ---- 저장 반영: 초기화·유지·영구 보너스 ----
        S.prestige++;
        S.pads.Clear();                       // 발판 레벨·부분 납입(padProg) 모두
        S.bells = 0; S.shelf = 0; S.bottles = 0; S.water = 0; S.cash = 0;
        S.meltQ = 0; S.meltT = 0; S.botT = 0; S.penTip = 0;
        S.spots = new float[S.spots != null && S.spots.Length > 12 ? S.spots.Length : 12];
        S.p = new PlayerSave();                // 시작 위치(200,450)·방향(0,1)·빈 등짐
        // 생존 기지 필드: 새 정착지는 0단계부터
        S.shelfO2 = 0; S.shelfFood = 0;
        S.rspots = new float[GameConfig.REGO_SPOTS];
        S.elecQ = S.elecOut = S.purQ = S.purOut = S.mixQ = S.mixOut = S.grnQ = S.grnOut = 0;
        S.elecT = S.purT = S.mixT = S.grnT = 0;
        S.supW = S.supO = S.supF = S.fertMade = 0;
        S.stage = 0; S.custs = 0;
        S.terraKeep += KEEP_PER_PRESTIGE;
        // 정적 충돌 플래그는 저장 복원(Migrate)이 pads에 없는 항목을 되돌리지 않으므로 여기서 끈다
        WorldMap.drillInstalled = false; WorldMap.domeOpen = false;
        if (FloatingJoystick.I) FloatingJoystick.I.suppressed = false;
        G.Save();
        Debug.Log("prestige after: " + JsonUtility.ToJson(S));
        arrivedPending = true;
        Destroy(rocket.gameObject);
        Bootstrap.Restart();
    }

    // ---- 새 기지 도착: 덮개 걷기 + 배너 ----
    IEnumerator CoArrive()
    {
        var S = G.S;
        Debug.Log("prestige arrive: prestige=" + S.prestige + " terraKeep=" + S.terraKeep + " bells=" + S.bells + " sold=" + S.sold + " quest=" + S.quest + " stage=" + S.stage
            + " pads=" + S.pads.Count + " p=(" + S.p.x + "," + S.p.y + ") carry=" + S.p.carry.Count + " lit=" + (TerraformManager.I != null ? TerraformManager.I.TargetCount : -1) + " bottlePrice=" + G.BottlePrice + " carryMax=" + G.CarryMax);
        // 첫 프레임(지면 텍스처 그리기 등)이 지나간 뒤 걷는다
        yield return null; yield return null;
        float el = 0f;
        while (el < 0.6f) { el += Mathf.Min(Time.unscaledDeltaTime, GameConfig.DT_MAX); VeilShow(1f - Mathf.SmoothStep(0f, 1f, el / 0.6f)); yield return null; }
        if (veil != null) Destroy(veil);
        veil = null; veilImg = null;
        AudioManager.I.Play("up", 1.15f); AudioManager.I.Play("ding", 1f, 0.6f);
        JuiceFX.I.Shake(3); JuiceFX.I.Vibrate(30);
        if (Player.I != null) { JuiceFX.I.Confetti(Player.I.view.position + Vector3.up * 1.5f, 24); JuiceFX.I.Squash(Player.I.view, 1.5f); }
        yield return CoBanner(S.prestige + 1);
        Arrived = false;
    }

    // 배너: 금색 판 [깃발] "새 정착지 n번째" + 아래 작은 줄 [병] ×병값 배율. 팝 0.45s → 1.8s 유지 → 위로 떠오르며 사라짐
    IEnumerator CoBanner(int n)
    {
        if (HUD.I == null) yield break;
        var box = UIKit.Panel(HUD.I.transform, "baseBanner", UIKit.SignOk);
        var brt = box.rectTransform; UIKit.Anchor(brt, new Vector2(0.5f, 0.5f), new Vector2(0, 210), new Vector2(330, 118));
        var row = UIKit.Row(box.transform, "row", 8); UIKit.Anchor(row.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(310, 54));
        UIKit.IconImg(row.transform, "flag", 40);
        var t1 = UIKit.Text(row.transform, "새 정착지 " + n + "번째", 36, UIKit.SignOk.text); UIKit.Stroke(t1, Color.white, 1.2f);
        var row2 = UIKit.Row(box.transform, "row2", 6); UIKit.Anchor(row2.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(310, 36));
        UIKit.IconImg(row2.transform, "bottle", 28);
        UIKit.Text(row2.transform, "×" + Multiplier.ToString("0.##"), 26, UIKit.GOLD_INK);
        var cg = box.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
        float el = 0f;
        while (el < 0.45f) { el += Time.unscaledDeltaTime; brt.localScale = Vector3.one * Mathf.Max(0.001f, JuiceFX.EaseOutBack(Mathf.Clamp01(el / 0.45f))); yield return null; }
        brt.localScale = Vector3.one;
        el = 0f;
        while (el < 1.8f) { el += Time.unscaledDeltaTime; brt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(el * 5f) * 1.5f * (1f - el / 1.8f)); yield return null; }
        el = 0f; var p0 = brt.anchoredPosition;
        while (el < 0.4f) { el += Time.unscaledDeltaTime; float e = el / 0.4f; brt.anchoredPosition = p0 + new Vector2(0, 40f * e); cg.alpha = 1f - e; yield return null; }
        Destroy(box.gameObject);
    }

    // 덮개(씬 재조립에도 살아남는 최상단 캔버스)
    static void VeilShow(float a)
    {
        if (veil == null)
        {
            if (a <= 0f) return;
            veil = new GameObject("prestigeVeil"); DontDestroyOnLoad(veil);
            var cv = veil.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 200;
            veilImg = UIKit.RT(veil.transform, "veil").gameObject.AddComponent<Image>(); veilImg.raycastTarget = true;
            var rt = veilImg.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        if (veilImg != null) { var c = VEIL; c.a = Mathf.Clamp01(a); veilImg.color = c; }
    }
}

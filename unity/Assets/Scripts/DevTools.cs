using System.Collections;
using System.IO;
using UnityEngine;

// 검증용: -shot <dir> 스크린샷 시퀀스 후 종료, -autoplay 힌트 타깃을 따라가는 봇, F5 저장 초기화. 원본 tools/qa의 자동 플레이 봇에 대응.
public class DevTools : MonoBehaviour
{
    GameState G => GameState.I;
    string shotDir; bool autoplay; float t;
    static readonly float[] SHOTS = { 2f, 10f, 25f, 45f, 70f, 100f };
    int shotIdx;

    void Start()
    {
        shotDir = DevArgs.Get("-shot");
        autoplay = DevArgs.Has("-autoplay");
        if (!string.IsNullOrEmpty(shotDir)) Directory.CreateDirectory(shotDir);
    }

    void Update()
    {
        t += Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.F5)) { SaveSystem.Wipe(); G.S = new SaveData(); UnityEngine.SceneManagement.SceneManager.LoadScene(0); }
        if (Input.GetKeyDown(KeyCode.F6)) { G.AddBells(500); }
        if (autoplay) Bot();
        if (!string.IsNullOrEmpty(shotDir) && shotIdx < SHOTS.Length && t >= SHOTS[shotIdx])
        {
            string p = Path.Combine(shotDir, "shot_" + Mathf.RoundToInt(SHOTS[shotIdx]).ToString("000") + ".png");
            ScreenCapture.CaptureScreenshot(p);
            Debug.Log("screenshot " + p + " bells=" + G.S.bells + " sold=" + G.S.sold + " tut=" + G.S.tut + " shelf=" + G.S.shelf + " bottles=" + G.S.bottles + " meltQ=" + G.S.meltQ);
            shotIdx++;
            if (shotIdx >= SHOTS.Length) StartCoroutine(QuitSoon());
        }
    }

    IEnumerator QuitSoon()
    {
        yield return new WaitForSeconds(1.5f);
        G.Save();
        Debug.Log("QA done: bells=" + G.S.bells + " sold=" + G.S.sold + " custs=" + G.S.custs + " lost=" + G.S.lost + " pads=" + JsonUtility.ToJson(G.S));
        Application.Quit();
    }

    // 힌트 타깃으로 걷고, 발판이면 멈춰서 납입, 아니면 근접 반경 안에서 잠깐 머문다
    float holdT;
    void Bot()
    {
        var h = HintSystem.I; var pl = Player.I; if (h == null || pl == null) return;
        Vector2 d = h.target - pl.pos; float dist = d.magnitude;
        float stop = h.targetKey.StartsWith("pad:") ? 12f : h.targetKey.StartsWith("ice:") ? 18f : 24f;
        if (dist > stop) { FloatingJoystick.Override = d.normalized; holdT = 0; }
        else
        {
            FloatingJoystick.Override = Vector2.zero; holdT += Time.deltaTime;
            // 발판 납입은 돈이 떨어질 때까지, 그 외는 2.5초 머물면 작은 흔들림으로 타깃 재평가
            if (!h.targetKey.StartsWith("pad:") && holdT > 2.5f) { FloatingJoystick.Override = Random.insideUnitCircle.normalized; holdT = 0; }
        }
    }

    void OnDestroy() { FloatingJoystick.Override = null; }
}

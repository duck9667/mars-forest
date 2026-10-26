using UnityEngine;
using static KayKitDress;

// KayKit 통일(r7): 개척지 모형. 집·패널·배터리·변환기의 동작(창 불·문 앞 바닥빛·패널 기울기·배터리 막대·환풍기)은 예전 그대로이고 외형만 바꾼다.
//  거주 돔 = KayKit basemodule_E(문이 남쪽): 돔은 흰 외피로, 아래 띠는 집마다 강조색(파랑·산호·청록·보라), 남동쪽 둥근 창 하나
//  태양광 = KayKit solarpanel 의 파란 판만 떼어 눕힌 것(짙은 회색 테 위) → 짧은 기둥 위에서 해 쪽으로 기운다(Animate)
//  배터리 = KayKit structure_low 받침 위 흰 원통 + 초록 막대 5칸, 변환기 = KayKit 회색 화물 상자(cargo_B) + 환풍기
//  예전 모형은 -kk 0(Frontier.BuildHouse 등).
public partial class Frontier
{
    static Material KkMat => ToyMesh.Mat(0.18f);

    // 집 몸통: 강조색별 메시(같은 색 집끼리 공유)
    static Mesh KkHouseMesh(int ai) => ToyMesh.Cached("kkHouse" + ai, m =>
    {
        var acc = ACCENT[ai];
        m.AtM(0, 0, 0).Cyl(HOUSE_R + 2.4f, HOUSE_R + 2f, 2.4f, 28, PAD, MachineKit.PAD_TOP);
        m.AtM(0, 0.8f, 2.4f).KkP("basemodule_E", new Vector3(17, 12.5f, 17), (p, c) =>
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            if (s < 0.3f) return KK.Warm(c);
            if (p.y > 1.05f) return Color.Lerp(KW_D, Color.Lerp(KW, Color.white, 0.4f), Mathf.Clamp01((v - 0.55f) / 0.4f));   // 돔: 흰 외피(이글루)
            Color.RGBToHSV(acc, out float ah, out float asat, out float av);
            return Color.HSVToRGB(ah, asat, Mathf.Clamp01(av * v / 0.85f));                                                       // 띠: 집 강조색
        });
        return m;
    });

    void KkHouse(int i)
    {
        var p = HOUSES[i]; const float R = HOUSE_R;
        var v = new GameObject("house" + i).transform; v.SetParent(habRoot, false); v.position = WorldMap.W2U(p);
        houseViews.Add(v);
        ToyMesh.Put(v, "body", KkHouseMesh(i % ACCENT.Length), KkMat, Vector3.zero);
        // 문 위 작은 등(입주하면 켜짐)
        var lamp = Prim.Sphere(v, "porchLamp", L(0, R + 1.4f, 15.6f), 3.2f, SHELL_D); Prim.NoShadow(lamp); porch.Add(lamp.GetComponent<Renderer>());
        // 둥근 창 1개: 남동쪽 비스듬한 면(띠 위)
        float a = 48f * Mathf.Deg2Rad, r = 18.4f, h = 9.5f;
        float dx = Mathf.Sin(a) * r, dy = Mathf.Cos(a) * r;
        var n = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a));
        var rot = Quaternion.FromToRotation(Vector3.up, n);
        var frame = Prim.Cyl(v, "winFrame", L(dx, dy, h) + n * 0.02f, 9.4f, 1.2f, KD); frame.transform.localRotation = rot;
        var w = Prim.Cyl(v, "win", L(dx, dy, h) + n * 0.06f, 7f, 1.2f, WIN_OFF); w.transform.localRotation = rot; Prim.NoShadow(w);
        windows.Add(w.GetComponent<Renderer>());
        // 문 앞 바닥빛(밤에 입주한 집만)
        var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(pool.GetComponent<Collider>()); pool.name = "pool";
        pool.transform.SetParent(v, false); pool.transform.localPosition = L(0, R + 8, 0.25f); pool.transform.localScale = new Vector3(34, 0.2f, 22) * GameConfig.K;
        var pr = pool.GetComponent<Renderer>(); pr.sharedMaterial = poolMat; Prim.NoShadow(pool); pools.Add(pr);
    }

    // 패널 판: KayKit solarpanel 의 파란 판만(정점 색이 파란 쪽) 떼어 가운데로 옮기고 45° 세워진 것을 눕힌다 + 짙은 회색 테
    static bool PanelBlue(Vector3 p, Color c) => c.b > c.r + 0.15f;
    static Mesh KkPanelMesh(float mw, float d) => ToyMesh.Cached("kkPanel" + Mathf.RoundToInt(mw) + "_" + Mathf.RoundToInt(d), m =>
    {
        m.At(new Vector3(0, -0.9f, 0)).RBox(new Vector3(mw + 1.8f, 1.6f, d + 1.8f), 1f, KD, KG);
        float sz = d / 0.45f;
        m.At(Vector3.zero, new Vector3(45, 0, 0)).KkP("solarpanel", new Vector3(mw / 0.9f, sz, sz), null, PanelBlue, true);
        return m;
    });

    void KkRow(Vector3 r)
    {
        float len = r.z; const float D = 21f, H = 8f, GAP = 5f;
        var g = new GameObject("row").transform; g.SetParent(solarRoot, false); g.position = WorldMap.W2U(r.x, r.y);
        solarParts.Add(g);
        int n = Mathf.Max(1, Mathf.RoundToInt((len + GAP) / 35f)); float mw = (len - GAP * (n - 1)) / n;
        var post = ToyMesh.Cached("kkPanelPost", m =>
        {
            m.AtM(0, 0, 0).Cyl(4.4f, 4f, 1.6f, 14, KD, KG);
            m.AtM(0, 0, 1.6f).Cyl(1.8f, 1.8f, H - 1.6f, 12, KW_D, KW);
            m.AtM(0, 0, H).RBox(new Vector3(6, 2, 3), 0.8f, KD, KG);
            return m;
        });
        for (int j = 0; j < n; j++)
        {
            float x = -len / 2 + mw / 2 + j * (mw + GAP);
            ToyMesh.Put(g, "post", post, KkMat, L(x, 0, 0));
            var t = new GameObject("tilt").transform; t.SetParent(g, false); t.localPosition = L(x, 0, H + 1.6f);
            tilts.Add(t);
            ToyMesh.Put(t, "panel", KkPanelMesh(mw, D), ToyMesh.Mat(0.45f), Vector3.zero);
        }
    }

    // 배터리 탑: KayKit 철골 받침(structure_low) 위 통통한 흰 원통 + 둥근 뚜껑, 남쪽 면에 초록 막대 5칸(충전량), 꼭대기 등
    void KkBattery()
    {
        var g = new GameObject("battery").transform; g.SetParent(solarRoot, false); g.position = WorldMap.W2U(BATTERY);
        solarParts.Add(g);
        const float B0 = 12f, BH = 30f, BR = 11f;
        ToyMesh.Put(g, "body", ToyMesh.Cached("kkBattery", m =>
        {
            m.AtM(0, 0, 0).Kk("structure_low", new Vector3(16, B0, 16));
            m.AtM(0, 0, B0).Cyl(BR, BR, BH, 22, KW_D, KW, true, KW);
            m.AtM(0, 0, B0 + BH).Dome(BR, BR, 5f, KW, Color.Lerp(KW, Color.white, 0.5f), 22, 4);
            m.AtM(0, 0, B0 + 1f).Band(BR + 0.3f, 3.6f, 22, Color.Lerp(BAR_ON, KD, 0.45f));
            m.AtM(0, 0, B0 + BH - 2.2f).Band(BR + 0.3f, 2f, 22, KD);
            m.AtM(0, BR - 0.4f, B0 + 15f).RBox(new Vector3(9, 28, 2.4f), 1f, KD_D, KD);
            return m;
        }), KkMat, Vector3.zero);
        for (int j = 0; j < 5; j++)
        {
            var b = Prim.Box(g, "bar", L(0, BR + 1.1f, B0 + 4f + j * 5.2f), new Vector3(6.4f, 3.6f, 1f), BAR_OFF); Prim.NoShadow(b);
            bars.Add(b.GetComponent<Renderer>());
        }
        var lamp = Prim.Sphere(g, "lamp", L(0, 0, B0 + BH + 6.5f), 5, BAR_OFF); Prim.NoShadow(lamp); battLamp = lamp.GetComponent<Renderer>();
    }

    // 변환기: KayKit 회색 화물 상자 + 남쪽 면 환풍기(전력이 흐르면 돈다) + 초록 표시등
    void KkInverter()
    {
        var g = new GameObject("inverter").transform; g.SetParent(solarRoot, false); g.position = WorldMap.W2U(INVERTER);
        solarParts.Add(g);
        ToyMesh.Put(g, "body", ToyMesh.Cached("kkInverter", m =>
        {
            m.AtM(0, 0, 0).RBox(new Vector3(24, 2.4f, 17), 3f, PAD, MachineKit.PAD_TOP);
            m.AtM(0, 0, 1.2f).Kk("cargo_B", new Vector3(36, 28, 26));
            m.At(new Vector3(-4, 8.5f, -6.7f), new Vector3(-90, 0, 0)).Ring(3.4f, 4.8f, 0.9f, 16, KD, KG);
            return m;
        }), KkMat, Vector3.zero);
        fan = new GameObject("fan").transform; fan.SetParent(g, false); fan.localPosition = L(-4, 7.4f, 8.5f);
        Prim.Box(fan, "a", Vector3.zero, new Vector3(6, 1.2f, 0.6f), KD);
        Prim.Box(fan, "b", Vector3.zero, new Vector3(1.2f, 6, 0.6f), KD);
        Prim.Box(g, "led", L(5, 6.9f, 10), new Vector3(3, 3, 0.6f), BAR_ON);
    }
}

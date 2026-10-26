using UnityEngine;
using static KayKitDress;

// KayKit 통일(r7): 본부 Lv1~5 를 KayKit 기지 부품(Space Base Bits)으로 단계마다 쌓아 올린다. 솟아오름(CoRise)·발자국(FOOT)·창 불빛·표시등은 예전 그대로.
//  Lv1 지휘 모듈 = basemodule_C(문이 남쪽) → Lv2 통신탑 = structure_tall 철골 위 레이돔 + 모듈 지붕에 화물 지붕(roofmodule_cargo_A)
//  → Lv3 유리 돔 모듈 = basemodule_E(돔은 옅은 하늘빛 유리색, 창 불빛) → Lv4 태양 돛 = roofmodule_solarpanels + 깃발
//  → Lv5 관제탑 = 키 큰 structure_tall 위 유리 관제실 + 지붕 + 표시등.  예전 모형은 -kk 0(HQSystem.BuildModule 등).
public partial class HQSystem
{
    static Material KkMat => ToyMesh.Mat(0.2f);
    GameObject KkPut(Transform p, string key, System.Func<ToyMesh, ToyMesh> build) => ToyMesh.Put(p, key, ToyMesh.Cached(key, build), KkMat, Vector3.zero);

    void KkBuild()
    {
        // Lv1: 지휘 모듈(발자국 −33~17 × −7~23 안)
        var p1 = Part(1, "module", -8, 6);
        KkPut(p1, "kkHqModule", m => m.AtM(0, 1.5f, SLAB_H).Kk("basemodule_C", new Vector3(22, 20, 13)));
        foreach (var x in new[] { -13f, 13f })
        {
            var w = Prim.Box(p1, "win", L(x, 15.6f, SLAB_H + 11f), new Vector3(5.5f, 4.2f, 1.2f), GLASS_D); Win(w); Prim.NoShadow(w);
        }

        // Lv2: 통신탑(안테나 받침 발자국 12×12) + 모듈 지붕 위 화물 지붕
        var p2 = Part(2, "antenna", -26, -14);
        KkPut(p2, "kkHqAntenna", m =>
        {
            m.AtM(0, 0, SLAB_H).Kk("structure_tall", 7f);
            m.AtM(0, 0, SLAB_H + 13.6f).Cyl(2.6f, 2.6f, 7f, 14, KW_D, KW);
            m.AtM(0, 0, SLAB_H + 20.6f).Ring(2.4f, 5.2f, 2.2f, 16, KD, KG);
            m.AtM(0, 0, SLAB_H + 29f).Ball(8.2f, KW, Color.Lerp(KW, Color.white, 0.6f), 18, 5);
            m.AtM(0, 0, SLAB_H + 27.4f).Band(8.25f, 2.2f, 18, KO);
            return m;
        });
        Beacon(p2, L(0, 0, SLAB_H + 38.6f), 4.2f);
        var p2b = Part(2, "roofCargo", -8, 7);
        KkPut(p2b, "kkHqRoofCargo", m => m.AtM(0, 0, SLAB_H + 19.6f + 1.3f).Kk("roofmodule_cargo_A", 13f));

        // Lv3: 유리 돔 모듈(돔 발자국 33×33). KayKit 금빛 돔은 금색 규칙(돈·발판)과 섞여 옅은 하늘빛 유리색으로
        var p3 = Part(3, "dome", 20, -6);
        KkPut(p3, "kkHqDome", m => m.AtM(0, 0, SLAB_H).KkP("basemodule_E", new Vector3(14.5f, 13f, 14.5f), (p, c) =>
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            return p.y > 1.05f && s > 0.3f ? Color.HSVToRGB(0.56f, 0.28f, Mathf.Clamp01(0.62f + 0.5f * v)) : KK.Warm(c);
        }));
        foreach (var x in new[] { -6.5f, 6.5f })
        {
            var w = Prim.Box(p3, "win", L(x, 16.4f, SLAB_H + 7.5f), new Vector3(4.6f, 3.6f, 1.2f), GLASS_D); Win(w); Prim.NoShadow(w);
        }

        // Lv4: 태양 돛(앞 오른쪽, 발자국 8×8) + 깃발(앞 왼쪽)
        var s4 = Part(4, "sail", 28, 20);
        KkPut(s4, "kkHqSail", m => m.AtM(0, 0, SLAB_H + 1.1f).Kk("roofmodule_solarpanels", 11f));
        var f = Part(4, "flag", -30, 22);
        KkPut(f, "kkHqFlagPole", m =>
        {
            m.AtM(0, 0, SLAB_H).Cyl(3.2f, 2.8f, 2.4f, 12, KD, KG);
            m.AtM(0, 0, SLAB_H + 2.4f).Cyl(1f, 1f, 38f, 10, KW_D, KW);
            m.AtM(0, 0, SLAB_H + 41f).Ball(1.8f, KD, KG);
            return m;
        });
        flagCloth = new GameObject("cloth").transform; flagCloth.SetParent(f, false); flagCloth.localPosition = L(0, 0, SLAB_H + 35);
        KkPut(flagCloth, "kkHqFlagCloth", m =>
        {
            m.At(7.5f, 0, 0).RBox(new Vector3(14, 9, 1f), 0.5f, KO, KO);
            m.At(7.5f, 0, 0).RBox(new Vector3(14.2f, 2.6f, 1.3f), 0.4f, KW, KW);
            return m;
        });

        // Lv5: 관제탑(발자국 20×20): 키 큰 철골 + 갑판 + 유리 관제실(창 불빛) + 둥근 지붕 + 안테나 끝 표시등
        var p5 = Part(5, "tower", 2, -18);
        const float TH = 34f;
        float hc = SLAB_H + TH;
        KkPut(p5, "kkHqTower", m =>
        {
            m.AtM(0, 0, SLAB_H).Kk("structure_tall", new Vector3(11, TH / 2f, 11));
            m.AtM(0, 0, hc).Cyl(12.5f, 12.5f, 2.6f, 20, KD, KG);
            m.AtM(0, 0, hc + 13.4f).Cyl(12.8f, 12.8f, 2.2f, 20, KD, KW_D);
            m.AtM(0, 0, hc + 15.6f).Dome(12.8f, 12.8f, 5.4f, KW_D, KW, 20, 4);
            m.AtM(0, 0, hc + 13.4f).Band(12.9f, 1.4f, 20, KO);
            m.AtM(0, 0, hc + 20f).Cyl(0.9f, 0.9f, 9f, 8, KD, KG);
            foreach (var a in new[] { 0f, 60f, 120f })
            {
                float rad = a * Mathf.Deg2Rad;
                foreach (var sg in new[] { -1f, 1f }) m.AtM(Mathf.Sin(rad) * 11.6f * sg, Mathf.Cos(rad) * 11.6f * sg, hc + 2.6f).Cyl(0.9f, 0.9f, 10.8f, 8, KW_D, KW);
            }
            return m;
        });
        var cab = Prim.Cyl(p5, "cab", L(0, 0, hc + 2.6f + 5.4f), 22, 10.8f, GLASS_D); Win(cab);
        Beacon(p5, L(0, 0, hc + 30.5f), 4.5f);
    }
}

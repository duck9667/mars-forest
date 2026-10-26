using UnityEngine;
using static KayKitDress;

// KayKit 통일(r7): 토끼 우리 소품. 자리·토끼가 피하는 원(OB_R)·들르는 자리는 예전 그대로.
//  건초 더미 → Restaurant 상추 상자(crate_lettuce), 당근 밭 → 당근 상자(crate_carrots), 물그릇 → KayKit 그릇(bowl) + 물,
//  토끼집 → 작은 KayKit 기지 모듈(basemodule_B, 문이 남쪽) + 문등·지붕 표시등(예전 발광 재질). 예전 소품은 -kk 0.
public partial class PenSystem
{
    static Material KkMat => ToyMesh.Mat(0.15f);

    void KkHay()
    {
        var g = Prop("hay", P_HAY);
        ToyMesh.Put(g, "crate", ToyMesh.Cached("kkPenLettuce", m => m.Kk("crate_lettuce", new Vector3(11, 9, 9))), KkMat, Vector3.zero).transform.localRotation = Quaternion.Euler(0, 8, 0);
    }
    void KkCarrots()
    {
        var g = Prop("carrots", P_CARROT);
        ToyMesh.Put(g, "crate", ToyMesh.Cached("kkPenCarrots", m => m.Kk("crate_carrots", new Vector3(9, 8, 8))), KkMat, Vector3.zero).transform.localRotation = Quaternion.Euler(0, -10, 0);
    }
    void KkBowl()
    {
        var g = Prop("bowl", P_BOWL);
        ToyMesh.Put(g, "bowl", ToyMesh.Cached("kkPenBowl", m =>
        {
            m.Kk("bowl", new Vector3(18, 15, 18));
            m.At(0, 3.2f, 0).Cyl(7f, 7f, 0.8f, 20, BLUE, GameConfig.Hex("8fd3f5"));
            return m;
        }), ToyMesh.Mat(0.35f), Vector3.zero);
    }
    void KkHutch()
    {
        var g = Prop("hutch", P_HUTCH);
        ToyMesh.Put(g, "hutch", ToyMesh.Cached("kkPenHutch", m =>
        {
            m.Kk("basemodule_B", new Vector3(12, 15, 10));
            m.At(5, 14.6f, 4).Cyl(0.7f, 0.7f, 8f, 8, KD, KG);
            return m;
        }), KkMat, Vector3.zero);
        var lamp = SphS(g, "lamp", 0, 12.5f, 12.6f, 2.8f, GameConfig.Hex("ffe9a0")); Prim.NoShadow(lamp);
        lampMat = Emis(GameConfig.Hex("ffe9a0")); lamp.GetComponent<Renderer>().sharedMaterial = lampMat;
        var bc = SphS(g, "beacon", 5, 23.6f, -4, 3f, GameConfig.Hex("ff5a4a")); Prim.NoShadow(bc);
        beaconMat = Emis(GameConfig.Hex("ff5a4a")); bc.GetComponent<Renderer>().sharedMaterial = beaconMat;
    }
}

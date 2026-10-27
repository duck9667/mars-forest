using UnityEngine;
using static KayKitDress;

// KayKit 통일(r7): 휴게 돔 안 소품. 좌석 자리·충돌·앉는 높이(좌판 11)·분수 물줄기·물방울 높이는 예전 그대로.
//  좌석 = Restaurant 의자(chair_A, 서쪽 줄 청록·동쪽 줄 산호) + 아래 두 줄은 바깥쪽에 작은 둥근 탁자(table_round_A_small)
//  화단 = Restaurant 나무 상자(crate) 화분 + 테라포밍과 같은 통통한 나무, 분수 = KayKit 그릇(bowl) 수반 둘 + 가운데 기둥
//  에어록 = KayKit 통로(tunnel_straight_A)를 남북으로 돌려 돔 벽을 지나게. 예전 모형은 -kk 0(Kenney 나무·분수·에어록).
public partial class DomeSystem
{
    static Material KkMat => ToyMesh.Mat(0.18f);

    void KkSeat(Transform b, int i, bool west)
    {
        int ci = west ? 0 : 1;
        ToyMesh.Put(b, "chair", ToyMesh.Cached("kkDomeChair" + ci, m => m.Kk("chair_A", 22f, KK.Hue(0, 45, CUSHION[ci], 0.72f))), KkMat, Vector3.zero);
        if (i < 2) return;   // 맨 위 줄은 탁자가 유리 벽에 닿아 뺐다
        // 팁 동전 더미(좌석 오른쪽 앞 +14, +8)와 겹치지 않게 바깥쪽 조금 뒤에
        ToyMesh.Put(b, "table", ToyMesh.Cached("kkDomeTable", m => m.Kk("table_round_A_small", new Vector3(9, 10, 9))), KkMat, L(west ? -18 : 20, -6, 0));
    }

    void KkBed(Transform bed, bool west)
    {
        ToyMesh.Put(bed, "planter", ToyMesh.Cached("kkDomePlanter", m =>
        {
            m.Kk("crate", new Vector3(19, 8f, 19));
            m.At(0, 5.4f, 0).RBox(new Vector3(33, 1f, 33), 2f, GameConfig.SOIL, GameConfig.Hex("6b4a33"));
            return m;
        }), KkMat, Vector3.zero);
        var tr = ToyMesh.Put(bed, "trees", TerraformManager.TreeMesh(west, west ? 1 : 0), ToyMesh.Mat(0.1f), L(0, 0, 5.9f));
        tr.transform.localScale = Vector3.one * 1.3f;
        tr.transform.localRotation = Quaternion.Euler(0, west ? 20 : -40, 0);
    }

    // 분수: 큰 그릇 수반(수면 5.6) + 가운데 기둥 + 작은 그릇(꼭대기 16, 물줄기가 여기서 솟는다)
    void KkFountain(Transform fo)
    {
        ToyMesh.Put(fo, "basin", ToyMesh.Cached("kkFountain", m =>
        {
            m.Kk("bowl", new Vector3(26, 20, 26));
            m.At(0, 4.6f, 0).Cyl(10.6f, 10.6f, 1f, 24, Items.ICE_TOP, GameConfig.Hex("8fd3f5"));
            m.At(0, 0, 0).Cyl(2.2f, 1.8f, 13.4f, 12, KW_D, KW);
            m.At(0, 13.2f, 0).Kk("bowl", 9f);
            m.At(0, 14.6f, 0).Cyl(3.4f, 3.4f, 0.8f, 14, Items.ICE_TOP, GameConfig.Hex("8fd3f5"));
            return m;
        }), ToyMesh.Mat(0.35f), Vector3.zero);
    }

    void KkAirlock(Transform al)
    {
        ToyMesh.Put(al, "tube", ToyMesh.Cached("kkAirlock", m =>
        {
            m.At(new Vector3(0, -0.2f * 15f, 0), new Vector3(0, 90, 0)).Kk("tunnel_straight_A", 15f);
            m.At(0, 0, 0).RBox(new Vector3(18, 1.6f, 32), 2f, KD, KG);
            return m;
        }), KkMat, Vector3.zero);
    }
}

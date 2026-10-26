using System.Collections.Generic;
using UnityEngine;
using static KayKitDress;

// KayKit 통일(r7, 2026-10-26): 북쪽 생존 기지 4시설을 KayKit 기지 모듈(Space Base Bits) 위에 시설마다 고유 투입부를 얹은 모형으로.
//  전기분해기 = basemodule_A + 지붕 위 전해조(물·전극·거품) + 양옆 기체 탱크(산소 파랑 띠·수소 주황 띠)
//  토양 정화기 = basemodule_D + 서쪽 깔때기 호퍼(붉은 레골리스) + 지붕 위 남북 축 세척 드럼(앞면 둥근 창 안에서 흙덩이가 구른다)
//  비료 배합기 = basemodule_A + 지붕 위 깔때기(흙 더미) 위로 도는 배합 날개 + 서쪽 초록 비료 통 묶음(containers_A)
//  온실 = 남북 유리 터널(KayKit 톤 아치 살·문턱) + 동쪽 KayKit 통로(tunnel_straight_A) → 물탱크, 안에는 KayKit 상추(food_ingredient_lettuce)
// 받침판·투입/산출 쟁반·앵커(SettleStations.View)·충돌 발자국은 예전 그대로. 키를 늘리던 1.25배(Grow)는 모듈 비율이 늘어나 쓰지 않고 모형 자체를 키웠다.
// 레벨 3단계(시설 속도 발판): 본체는 테 색만 바뀌고(주황 → 금속 → 샴페인 금), 단계 부품은 KayKit 부품을 더해 솟아오른다(LevelLook·TierLook).
// 예전 모형은 -kk 0(SettleStations.BuildElec 등).
public partial class SettleStations
{
    static Color Hx(string h) => GameConfig.Hex(h);
    static Color TrimOf(int t) => t == 0 ? KO : t == 1 ? METAL_L : TRIM;
    static Color TrimLOf(int t) => t == 0 ? KO_L : t == 1 ? Hx("e6ebf0") : TRIM_L;
    static readonly Color KWATER = Hx("6fc3f0"), KWATER_L = Hx("9ad7f6");
    // 모듈 지붕 높이(받침판 PLATE 위, KayKit 지붕 윗면 0.97)
    const float MH = 22f, KROOF = PLATE + 0.97f * MH;

    void KkBuild()
    {
        KkElec(); KkPurif(); KkMixer(); KkGreen();
        for (int i = 0; i < 3; i++) Grow(i, Vector3.one);
        Grow(3, new Vector3(1.2f, 1.2f, 1.2f));
        for (int i = 0; i < 4; i++) KkLook(i);
    }

    // 둥근 탱크(받침 고리 + 원통 + 둥근 뚜껑 + 색 띠 + 테)
    static void KTank(ToyMesh m, float x, float y, float r, float h, Color band, Color trim, Color trimL)
    {
        m.AtM(x, y, PLATE).Cyl(r + 1.6f, r + 1.4f, 2.4f, 20, KD, KG);
        m.AtM(x, y, PLATE + 2.4f).Cyl(r, r, h, 20, KW_D, KW, true, KW);
        m.AtM(x, y, PLATE + 2.4f + h).Dome(r, r, r * 0.55f, KW, Color.Lerp(KW, Color.white, 0.5f), 20, 4);
        m.AtM(x, y, PLATE + 2.4f + h * 0.5f).Band(r + 0.35f, 4.2f, 20, band);
        m.AtM(x, y, PLATE + 2.4f + h * 0.12f).Ring(r - 0.5f, r + 0.6f, 2f, 20, trim, trimL);
        m.AtM(x, y, PLATE + 2.4f + h + r * 0.55f - 0.6f).Cyl(1.6f, 1.6f, 2.4f, 10, KD, KG);
    }

    // ═════════════ 전기분해기(물통 → 산소통) ═════════════
    const float E_MY = -13f, E_TUBR = 11f;
    static Mesh KkElecBody(int t) => ToyMesh.Cached("kkElecBody" + t, m =>
    {
        Color tr = TrimOf(t), trL = TrimLOf(t);
        m.AtM(1, E_MY, PLATE).Kk("basemodule_A", new Vector3(18, MH, 15));
        // 지붕 위 전해조: 두툼한 고리 벽 + 짙은 바닥 + 물(윗면 밝게) + 테
        m.AtM(0, E_MY, KROOF).Cyl(E_TUBR + 1.8f, E_TUBR + 1.8f, 1.2f, 24, KD, KD);
        m.AtM(0, E_MY, KROOF + 1.2f).Cyl(E_TUBR, E_TUBR, 4.4f, 24, KWATER, KWATER_L, true, KWATER);
        m.AtM(0, E_MY, KROOF + 1.2f).Ring(E_TUBR, E_TUBR + 2f, 6.6f, 24, KW_D, KW, KW_D);
        m.AtM(0, E_MY, KROOF + 7.8f).Ring(E_TUBR - 0.2f, E_TUBR + 2.4f, 1.4f, 24, tr, trL);
        // 전극 둘(＋빨강·－검정): 물에 꽂힌 회색 막대 + 머리 + 뒤로 내려가는 전선
        foreach (var (x, cap) in new[] { (-5f, Hx("e04848")), (5f, KD_D) })
        {
            m.AtM(x, E_MY, KROOF + 2f).Cyl(1.5f, 1.5f, 11f, 10, KG, KG);
            m.AtM(x, E_MY, KROOF + 13f).RBox(new Vector3(4.4f, 2.6f, 4.4f), 1f, cap);
            m.PipeM(x, E_MY - 1.5f, KROOF + 13.6f, x, E_MY - 14f, KROOF + 13.6f, 0.8f, cap, false);
            m.PipeM(x, E_MY - 14f, KROOF + 13.6f, x, E_MY - 14f, KROOF - 1f, 0.8f, cap, false);
        }
        // 기체 탱크: 서쪽 수소(주황 띠) · 동쪽 산소(파랑 띠), 모듈로 짧은 관
        KTank(m, -37, -14, 8.5f, 28, KO, tr, trL);
        KTank(m, 37, -14, 8.5f, 28, PIPE, tr, trL);
        m.PipeM(-28.5f, -14, PLATE + 9, -21, -14, PLATE + 9, 1.7f, KG);
        m.PipeM(21, -14, PLATE + 9, 28.5f, -14, PLATE + 9, 1.7f, PIPE);
        return m;
    });
    static Mesh KkElecParts(int t) => t <= 0 ? null : ToyMesh.Cached("kkElecParts" + t, m =>
    {
        Color tr = TrimOf(t), trL = TrimLOf(t);
        // 개량: 두 탱크를 잇는 관 아치(가운데 밸브 공) + 탱크 금속 띠 + 산소 쪽 화물 상자(cargo_B)
        foreach (float x in new[] { -37f, 37f })
        {
            m.AtM(x, -14, PLATE + 26f).Band(8.9f, 2.2f, 20, tr);
            m.PipeM(x, -14, PLATE + 37, x, -14, PLATE + 47, 1.6f, t >= 2 ? KW : METAL_L, false);
            m.AtM(x, -14, PLATE + 47).Ball(2.4f, tr, trL);
        }
        m.PipeM(-37, -14, PLATE + 47, 37, -14, PLATE + 47, 1.6f, t >= 2 ? KW : METAL_L, false);
        m.AtM(0, -14, PLATE + 47).Ball(3.2f, tr, trL);
        m.AtM(26, -27, PLATE).Kk("cargo_B", 15);
        // 고급: 수소 쪽 뒤 태양판
        if (t >= 2) m.AtM(-26, -26, PLATE, 0).Kk("solarpanel", 12);
        return m;
    });

    void KkElec()
    {
        var v = View(0, new Vector2(-24, 20), new Vector2(24, 20));
        // 거품: Animate 가 로컬 높이 10~19 로 올리므로 부모를 전해조 물 높이에 맞춰 둔다
        var bubRoot = new GameObject("bubRoot").transform; bubRoot.SetParent(v, false); bubRoot.localPosition = new Vector3(0, (KROOF + 3f - 10f) * GameConfig.K, 0);
        for (int j = 0; j < 6; j++)
        {
            var b = Prim.Sphere(bubRoot, "bub", L((j % 2 == 0 ? -5 : 5) + (j % 3 - 1) * 2.2f, E_MY + (j % 3 - 1) * 2.6f, 10), 2.5f, Color.white);
            Prim.NoShadow(b); bubbles.Add(b.transform);
        }
    }

    // ═════════════ 토양 정화기(레골리스 → 흙) ═════════════
    const float P_MX = 10f, P_MY = -12f;
    static Mesh KkPurifBody(int t) => ToyMesh.Cached("kkPurifBody" + t, m =>
    {
        Color tr = TrimOf(t), trL = TrimLOf(t);
        m.AtM(P_MX, P_MY, PLATE).Kk("basemodule_D", new Vector3(15, MH, 13));
        // 서쪽 깔때기 호퍼(다리 둘 + 위가 넓은 원뿔대 + 테 + 붉은 레골리스)
        foreach (float lx in new[] { -37f, -25f }) m.AtM(lx, P_MY, PLATE).Cyl(1.6f, 1.6f, 16f, 10, KD, KD);
        m.AtM(-31, P_MY, PLATE + 13f).Cyl(5f, 13f, 16f, 22, KW_D, KW, true, KW);
        m.AtM(-31, P_MY, PLATE + 28.4f).Ring(11.8f, 14f, 1.8f, 22, tr, trL);
        m.AtM(-31, P_MY, PLATE + 27.6f).Cyl(12f, 12f, 1f, 22, Items.REGO_M, GameConfig.REGO);
        m.At(new Vector3(-31, PLATE + 29f, -P_MY)).Blob(new Vector3(9f, 3.4f, 7.5f), 1, 61, 0.22f, Items.REGO_T, Items.REGO_M, Items.REGO_D, Items.REGO_A, 0.4f);
        m.PipeM(-20, P_MY, PLATE + 18, -8, P_MY, PLATE + 12, 2.6f, KD);
        // 드럼 요람(지붕 위 짙은 회색 받침 둘)
        foreach (float dy in new[] { P_MY - 7f, P_MY + 6f }) m.AtM(P_MX, dy, KROOF + 1.6f).RBox(new Vector3(15, 3.2f, 3.6f), 1.2f, KD, KG);
        // 물 관 아치: 동쪽 뒤에서 솟아 드럼 뒤로
        m.PipeM(38, P_MY - 10, PLATE, 38, P_MY - 10, KROOF + 26, 2.4f, PIPE);
        m.PipeM(38, P_MY - 10, KROOF + 26, P_MX, P_MY - 10, KROOF + 26, 2.4f, PIPE);
        m.PipeM(P_MX, P_MY - 10, KROOF + 26, P_MX, P_MY - 10, KROOF + 20.5f, 2.4f, PIPE);
        return m;
    });
    // 세척 드럼(축 z = 남북, 원점 = 드럼 중심). Animate 가 로컬 z 축으로 돌린다. 앞(남, −z)면 둥근 창 안에 흙덩이 셋
    static Mesh KkDrum(int t) => ToyMesh.Cached("kkDrum" + t, m =>
    {
        Color tr = TrimOf(t), trL = TrimLOf(t);
        const float R = 10f, LEN = 18f;
        var q = new Vector3(-90, 0, 0);   // y 축 원기둥 → z 축(아래 끝이 남쪽 −z)
        m.At(new Vector3(0, 0, LEN / 2), q).Cyl(R, R, LEN, 24, KW_D, KW, true, KW, true);
        foreach (float z in new[] { LEN / 2 - 1.2f, -LEN / 2 + 3.6f }) m.At(new Vector3(0, 0, z), q).Ring(R - 0.4f, R + 1f, 2.4f, 24, tr, trL);
        m.At(new Vector3(0, 0, -LEN / 2 - 0.2f), q).Cyl(6.6f, 6.6f, 0.6f, 20, Hx("7a4430"), Hx("7a4430"));
        m.At(new Vector3(0, 0, -LEN / 2 + 0.3f), q).Ring(6.4f, 7.8f, 1.4f, 20, KD, KG);
        for (int j = 0; j < 3; j++)
        {
            float a = j * 120f * Mathf.Deg2Rad;
            m.At(new Vector3(Mathf.Cos(a) * 3.2f, Mathf.Sin(a) * 3.2f, -LEN / 2 - 0.8f), new Vector3(0, 0, j * 37f)).Blob(new Vector3(2.4f, 2.4f, 1.2f), 0, 70 + j, 0.2f, Items.REGO_T, j == 1 ? Hx("8f3d20") : Items.REGO_M, Items.REGO_D);
        }
        return m;
    });
    MeshFilter drumMf;
    void KkPurif()
    {
        var v = View(1, new Vector2(-26, 16), new Vector2(26, 16));
        drum = new GameObject("drum").transform; drum.SetParent(v, false); drum.localPosition = L(P_MX, P_MY - 1, KROOF + 13.2f);
        drumMf = LevelLook.Holder(drum, "drumMesh");
    }

    // ═════════════ 비료 배합기(흙 → 비료) ═════════════
    const float X_MX = 14f, X_MY = -12f, FUN_H = 12f;
    static Mesh KkMixBody(int t) => ToyMesh.Cached("kkMixBody" + t, m =>
    {
        Color tr = TrimOf(t), trL = TrimLOf(t);
        m.AtM(X_MX + 1, X_MY, PLATE).Kk("basemodule_A", new Vector3(15, MH, 13));
        // 지붕 위 깔때기(흙이 찬 윗면 + 솟은 흙 더미)
        m.AtM(X_MX, X_MY, KROOF).Cyl(8f, 15.5f, FUN_H, 24, KW_D, KW, true, KW);
        m.AtM(X_MX, X_MY, KROOF + FUN_H - 0.4f).Ring(14.6f, 17f, 2f, 24, tr, trL);
        m.AtM(X_MX, X_MY, KROOF + FUN_H - 0.6f).Cyl(15f, 15f, 0.8f, 24, GameConfig.SOIL, GameConfig.SOIL);
        m.At(new Vector3(X_MX, KROOF + FUN_H + 0.2f, -X_MY)).Blob(new Vector3(11f, 3.6f, 8.5f), 1, 41, 0.18f, Hx("86613f"), GameConfig.SOIL, Hx("553a27"), Hx("7a5638"), 0.4f);
        // 서쪽 비료 통 묶음(KayKit 초록 컨테이너) + 퇴비 관
        m.AtM(-31, -13, PLATE).Kk("containers_A", new Vector3(44, 80, 44));
        m.AtM(-31, -13, PLATE + 16).Kk("containers_A", new Vector3(40, 60, 40));
        m.PipeM(-19, -13, PLATE + 8, -3, -13, PLATE + 8, 1.8f, Hx("8fae4a"));
        return m;
    });
    static Mesh KkPaddle() => ToyMesh.Cached("kkPaddle", m =>
    {
        m.At(0, 0, 0).RBox(new Vector3(20, 1.6f, 2.6f), 0.8f, KD, KG);
        m.At(0, 0, 0).RBox(new Vector3(2.6f, 1.6f, 20), 0.8f, KD, KG);
        m.At(0, 0, 0).Cyl(1.4f, 1.4f, 9f, 10, KD, KG);
        m.At(0, 9f, 0).Ball(2.2f, KO, KO_L);
        return m;
    });
    static Mesh KkMixParts(int t) => t <= 0 ? null : ToyMesh.Cached("kkMixParts" + t, m =>
    {
        Color tr = TrimOf(t), trL = TrimLOf(t);
        // 개량: 동쪽 모터 상자(KayKit cargo_B) + 배합 통으로 굴대 + 깔때기 허리 띠 / 고급: 모터 위 태양판
        m.AtM(41, -14, PLATE).Kk("cargo_B", 16);
        m.PipeM(37, -14, PLATE + 6, 31, -14, PLATE + 6, 1.5f, METAL_D);
        m.AtM(X_MX, X_MY, KROOF + 4f).Band(12.1f, 2f, 24, tr);
        if (t >= 2) m.AtM(41, -15, PLATE + 8.3f).Kk("solarpanel", 11);
        return m;
    });
    void KkMixer()
    {
        var v = View(2, new Vector2(-26, 16), new Vector2(26, 16));
        paddle = new GameObject("paddle").transform; paddle.SetParent(v, false); paddle.localPosition = L(X_MX, X_MY, KROOF + FUN_H + 4.2f);
        ToyMesh.Put(paddle, "blades", KkPaddle(), ToyMesh.Mat(0.2f), Vector3.zero);
    }

    // ═════════════ 온실(비료 + 물 → 식량) ═════════════
    const float G_R = 36f, G_LEN = 60f, G_CX = -20f, G_CY = -14f;
    // 아치 살 하나(반원, 두께 있는 관 10토막)
    static void Arch(ToyMesh m, float ay, float r, float th, Color c)
    {
        const int SEG = 12;
        for (int j = 0; j < SEG; j++)
        {
            float a0 = Mathf.PI * j / SEG, a1 = Mathf.PI * (j + 1) / SEG;
            m.PipeM(G_CX - Mathf.Cos(a0) * r, ay, PLATE + Mathf.Sin(a0) * r, G_CX - Mathf.Cos(a1) * r, ay, PLATE + Mathf.Sin(a1) * r, th, c, false);
        }
    }
    static Mesh KkGreenBody(int t) => ToyMesh.Cached("kkGreenBody" + t, m =>
    {
        Color rib = t == 0 ? KW : t == 1 ? METAL_L : TRIM, ribD = t == 0 ? KD : t == 1 ? METAL : TRIM;
        // 흙 두둑 3줄
        foreach (float bx in new[] { G_CX - 16, G_CX, G_CX + 16 }) m.AtM(bx, G_CY, PLATE + 2).RBox(new Vector3(10, 4, G_LEN - 8), 2f, GameConfig.SOIL, Hx("6b4a33"));
        // 아치 살 셋(앞·가운데·뒤) + 문턱(짙은 회색). 앞 아치에 주황 띠를 겹쳤더니 큰 주황 호로 튀어 뺐다
        foreach (float ay in new[] { G_CY + G_LEN / 2, G_CY, G_CY - G_LEN / 2 }) Arch(m, ay, G_R, 1.4f, rib);
        foreach (float sx in new[] { G_CX - G_R, G_CX + G_R }) m.AtM(sx, G_CY, PLATE + 1.6f).RBox(new Vector3(4, 3.2f, G_LEN + 4), 1.2f, ribD, KG);
        // 앞 문(유리문 + 짙은 문틀)
        float fy = G_CY + G_LEN / 2 + 1;
        m.AtM(G_CX, fy, PLATE + 11).RBox(new Vector3(14, 22, 1.2f), 0.4f, Hx("9cc4da"), Hx("b8d8ea"));
        m.AtM(G_CX - 8, fy + 0.3f, PLATE + 11).RBox(new Vector3(2.2f, 22, 1.8f), 0.6f, KD, KG);
        m.AtM(G_CX + 8, fy + 0.3f, PLATE + 11).RBox(new Vector3(2.2f, 22, 1.8f), 0.6f, KD, KG);
        m.AtM(G_CX, fy + 0.3f, PLATE + 23).RBox(new Vector3(18.4f, 2.4f, 1.8f), 0.6f, KD, KG);
        // 동쪽: KayKit 통로(tunnel_straight_A) → 물탱크
        m.AtM(G_CX + G_R + 10.5f, -26, PLATE - 0.2f * 10f).Kk("tunnel_straight_A", 10.5f);
        KTank(m, 44, -26, 8f, 22, PIPE, TrimOf(t), TrimLOf(t));
        return m;
    });
    static Mesh KkGreenParts(int t) => t <= 0 ? null : ToyMesh.Cached("kkGreenParts" + t, m =>
    {
        // 개량: 둘째 물탱크 + 관, 터널 꼭대기 살수관(살수 머리 셋) / 고급: 금 살수관 + 탱크 사이 태양판
        bool hi = t >= 2;
        KTank(m, 63, -24, 7f, 18, PIPE, TrimOf(t), TrimLOf(t));
        m.PipeM(52, -25, PLATE + 6, 56, -25, PLATE + 6, 1.6f, PIPE);
        Color rp = hi ? TRIM : PIPE;
        m.PipeM(G_CX, G_CY - 31, PLATE + G_R + 1.6f, G_CX, G_CY + 31, PLATE + G_R + 1.6f, 1.3f, rp);
        foreach (float y in new[] { G_CY - 18, G_CY, G_CY + 18 }) m.AtM(G_CX, y, PLATE + G_R + 2.8f).Ball(1.9f, hi ? TRIM_L : METAL_L);
        if (hi) m.AtM(54, -6, PLATE).Kk("solarpanel", 13);
        return m;
    });
    void KkGreen()
    {
        var v = View(3, new Vector2(-38, 28), new Vector2(38, 28));
        // 작물: KayKit 상추(진행도만큼 자라고 살랑, Animate)
        var lettuce = ToyMesh.Cached("kkLettuce", m => m.Kk("food_ingredient_lettuce", 8.5f));
        for (int r = 0; r < 3; r++)
            for (int j = 0; j < 4; j++)
            {
                var c = new GameObject("crop").transform; c.SetParent(v, false); c.localPosition = L(G_CX - 16 + r * 16, G_CY - 22 + j * 14.5f, PLATE + 4);
                var leaf = ToyMesh.Put(c, "lettuce", lettuce, ToyMesh.Mat(0.12f), Vector3.zero);
                leaf.transform.localRotation = Quaternion.Euler(0, (r * 4 + j) * 47f, 0);
                crops.Add(c);
            }
        // 유리(투명 반원통): 깊이를 안 써서 외곽선이 생기지 않고 안의 작물이 비친다
        var film = new GameObject("film"); film.transform.SetParent(v, false); film.transform.localPosition = L(G_CX, G_CY, PLATE);
        film.transform.localScale = Vector3.one * GameConfig.K; film.transform.localRotation = Quaternion.Euler(0, 90, 0);
        film.AddComponent<MeshFilter>().sharedMesh = TunnelMesh(G_R, G_LEN, 20);
        var mr = film.AddComponent<MeshRenderer>(); var fc = Hx("eef8ff"); fc.a = 0.26f; mr.sharedMaterial = Mats.TransShared(fc);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    // ═════════════ 레벨 외형(KayKit) ═════════════
    void KkLook(int i)
    {
        var v = views[i];
        var look = new TierLook { squashRoot = v }; looks[i] = look;
        var body = LevelLook.Holder(v, "kkBody"); var parts = LevelLook.Holder(v, "tierParts");
        switch (i)
        {
            case 0:
                look.Swap(body, KkElecBody).Swap(parts, KkElecParts, null, true);
                look.Part(Glass(v, "tubGlass", ToyMesh.Cached("kkElecGlass", m => m.Dome(E_TUBR + 2.2f, E_TUBR + 2.2f, 12f, Color.white, Color.white, 24, 6)), L(0, E_MY, KROOF + 9.2f)), 2);
                break;
            case 1:
                look.Swap(body, KkPurifBody).Swap(drumMf, KkDrum);
                break;
            case 2:
                look.Swap(body, KkMixBody).Swap(parts, KkMixParts, null, true);
                break;
            case 3:
                look.Swap(body, KkGreenBody).Swap(parts, KkGreenParts, null, true);
                var lamp = ToyMesh.Put(v, "ridgeLamp", ToyMesh.Cached("kkGreenLamp", m => m.AtM(G_CX, G_CY, PLATE + G_R - 3.4f).RBox(new Vector3(2.6f, 1.2f, 54), 0.5f, Color.white)),
                    MachineKit.GlowMat(Hx("ffe2a8"), 0.8f), Vector3.zero);
                Prim.NoShadow(lamp); look.Part(lamp, 2);
                break;
        }
        if (i == 1) look.Swap(parts, KkPurifParts, null, true);
        look.Apply(LevelLook.TierOf(Pads.FAC_SPD[i]), false);
    }
    static Mesh KkPurifParts(int t) => t <= 0 ? null : ToyMesh.Cached("kkPurifParts" + t, m =>
    {
        // 개량: 동쪽 뒤 작은 물탱크 → 드럼으로 짧은 관 / 고급: 지붕 앞 동쪽 태양판
        KTank(m, 40, -20, 5.6f, 20, PIPE, TrimOf(t), TrimLOf(t));
        m.PipeM(40, -20, PLATE + 18, 34, -16, PLATE + 18, 1.4f, PIPE);
        if (t >= 2) m.AtM(P_MX + 14, P_MY + 7, KROOF).Kk("solarpanel", 9);
        return m;
    });
}

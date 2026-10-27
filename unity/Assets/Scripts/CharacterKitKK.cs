using System.Collections.Generic;
using UnityEngine;

// 캐릭터 KayKit 톤(r7, 10/26): 건물·기계를 KayKit(각진 저폴리·정점 색 그라데이션)으로 통일하자 둥근 프리미티브 캐릭터가
// "매끈한 장난감"처럼 따로 놀아서, 같은 언어로 다시 만든 것. 부위마다 ToyMesh 한 메시(정점 색, Shaders/MarsVColor)로 합친다.
//  - 저폴리 공(Gem, 면 평면 셰이딩)·모서리 깎은 상자(Chamfer)·각진 원뿔대(Cyl smooth=false)만 쓴다. 매끈한 구·얇은 판 없음.
//  - 색은 KayKit 팔레트: 밝은 외피 e9e8e1·c9ccc7, 짙은 회색 545b5f·3f4549, 중간 회색 8e9597, 주황 띠 f07b36. 위가 밝고 아래가 어둡다.
//  - 애니메이션 골격(팔·다리 피벗, 머리 그룹, 바퀴, 토끼 귀·코·몸 스케일)은 예전과 같은 자리·이름이라 Animate·Swing·TickSit 그대로.
//  - 렌더러: 플레이어·정착민 20개 남짓 → 6개(몸·머리·팔 둘·다리 둘), 로봇 10개 → 2~3개, 토끼 9개 → 5개. 같은 메시는 인스턴싱.
// 스위치: 검증 인자 -chars 0 이면 예전 프리미티브 캐릭터(비교용). 기본은 KayKit 톤.
public static partial class CharacterKit
{
    static int kkOn = -1;
    public static bool Kk { get { if (kkOn < 0) kkOn = DevArgs.Get("-chars") == "0" ? 0 : 1; return kkOn == 1; } }

    // KayKit 톤 팔레트(캐릭터)
    static readonly Color K_WHITE = GameConfig.Hex("f6f3ec"), K_WHITE_D = GameConfig.Hex("d6d3ca");     // 우주복 위·아래
    static readonly Color K_LIGHT = GameConfig.Hex("e9e8e1"), K_LIGHT_D = GameConfig.Hex("c9ccc7");     // 로봇 머리·수레
    static readonly Color K_MID = GameConfig.Hex("8e9597"), K_MID_D = GameConfig.Hex("6f777a");
    static readonly Color K_DARK = GameConfig.Hex("545b5f"), K_DARK_D = GameConfig.Hex("3f4549"), K_INK = GameConfig.Hex("2c3135");
    static readonly Color K_ORANGE = GameConfig.Hex("f07b36"), K_ORANGE_D = GameConfig.Hex("d4612a");
    static readonly Color K_VISOR = GameConfig.Hex("1f2b40"), K_VISOR_L = GameConfig.Hex("3a5680");
    static readonly Color K_SCREEN = GameConfig.Hex("3d8fd0"), K_SCREEN_L = GameConfig.Hex("6cc2f2");
    static readonly Color K_GLINT = GameConfig.Hex("f4fbff");
    static readonly Color K_BROWN = GameConfig.Hex("8a5636"), K_BROWN_L = GameConfig.Hex("a8714a");

    // 캐릭터 재질: 기계와 같은 정점 색 셰이더, 밤 채도 덜기만 0.3 몫(기계·물건은 1). 밤에도 플레이어·정착민 옷색이 살아 있게
    static Material bodyMat, helmMat;
    static Material CharMat(float gloss) { var m = new Material(ToyMesh.Mat(gloss)) { name = "charVColor" }; m.SetFloat("_NightSat", 0.3f); return m; }
    static Material BodyMat => bodyMat != null ? bodyMat : (bodyMat = CharMat(0.15f));
    static Material HelmMat => helmMat != null ? helmMat : (helmMat = CharMat(0.35f));   // 헬멧·바이저만 조금 매끈하게(해 반짝임)
    static Color Dk(Color c, float t) => Color.Lerp(c, K_INK, t);
    static Color Lt(Color c, float t) => Color.Lerp(c, Color.white, t);
    static string Hx(Color c) => ColorUtility.ToHtmlStringRGB(c);
    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static GameObject PutL(Transform p, string n, Mesh m, Material mat, float x, float y, float z) => ToyMesh.Put(p, n, m, mat, new Vector3(x, y, z) * K);
    // 피벗(빈 Transform, 논리 위치)
    static Transform Pivot(Transform p, string n, float x, float y, float z) => Group(p, n, x, y, z);

    // 저폴리 헬멧(16×8 칸, 가운데가 원점) + 같은 칸을 따라 띄운 남색 바이저(정면 위아래 3칸 × 좌우 3~5칸, 모서리 깎은 타원꼴) + 반짝임 하나.
    //  바이저는 헬멧 면 모서리를 그대로 따라가 테두리가 각지고 깔끔하다(KayKit 처럼 면 단위로 칠한 느낌), lift 만큼 도드라져 외곽선이 둘린다
    static ToyMesh Helmet(this ToyMesh m, Vector3 r, float lift, Vector3 glint)
    {
        const int S = 16, R = 8;   // 정면 칸 i = 4, 위도 칸 22.5°(k 3: −22.5~0, 4: 0~22.5, 5: 22.5~45)
        m.At(Vector3.zero).Gem(r, S, R, K_WHITE_D, Lt(K_WHITE, 0.4f));
        m.At(Vector3.zero).GemShell(r, S, R, (i, k) => (k == 3 && i >= 3 && i <= 5) || ((k == 4 || k == 5) && i >= 2 && i <= 6),
            lift, K_VISOR, K_VISOR_L, Dk(K_VISOR, 0.2f));
        // 반짝임: 바이저 왼쪽 위 칸(i 5 = 112.5°, 위도 34°) 면 위에, 면을 보게 눕혀서
        float a = 112.5f * Mathf.Deg2Rad, l = 34f * Mathf.Deg2Rad;
        var ro = r + Vector3.one * lift;
        var p = new Vector3(Mathf.Cos(a) * Mathf.Cos(l) * ro.x, Mathf.Sin(l) * ro.y, Mathf.Sin(a) * Mathf.Cos(l) * ro.z);
        var nrm = new Vector3(p.x / (ro.x * ro.x), p.y / (ro.y * ro.y), p.z / (ro.z * ro.z)).normalized;
        return m.At(p - nrm * glint.z * 0.3f, Quaternion.LookRotation(nrm, Vector3.up)).Gem(glint, 6, 3, K_GLINT, K_GLINT, 0.6f);
    }

    // ───────── 플레이어: 흰 우주복 + 주황 포인트, 큰 저폴리 헬멧 + 남색 바이저 ─────────
    // 피벗·높이는 예전과 같다: 다리 (±6,13) · 팔 (±15,31) · 머리 (0,49), 헬멧 꼭대기 ≈ 63.5, 등짐 앵커 (0,34,−25)
    static void BuildPlayerKK(Transform body, out Transform head, out Transform armL, out Transform armR, out Transform legL, out Transform legR)
    {
        var legM = ToyMesh.Cached("chPlLeg", m => m
            .At(0, -9.5f, 0).Cyl(4.3f, 4.9f, 10.5f, 8, K_WHITE_D, K_WHITE, false, Lt(K_WHITE_D, 0.4f))
            .At(V(0, -9.8f, 1.6f)).Chamfer(V(10.6f, 6f, 13.4f), 2f, K_DARK_D, K_DARK, Lt(K_DARK, 0.08f)));
        var armM = ToyMesh.Cached("chPlArm", m => m
            .At(0, -10.5f, 0).Cyl(3.5f, 4.2f, 11f, 8, K_WHITE_D, K_WHITE, false, Lt(K_WHITE_D, 0.4f))
            .At(V(0, -12.5f, 0)).Gem(V(4.8f, 4.6f, 4.8f), 8, 4, K_ORANGE_D, K_ORANGE));
        var torsoM = ToyMesh.Cached("chPlTorso", m => m
            .At(V(0, 25.5f, 0)).Chamfer(V(27f, 22f, 22f), 6f, K_WHITE_D, K_WHITE)
            .At(V(0, 16.5f, 0)).Chamfer(V(28.5f, 4.6f, 23.4f), 2f, K_ORANGE_D, K_ORANGE)                 // 벨트
            .At(V(0, 27f, 10.6f)).Chamfer(V(11f, 7.5f, 3.2f), 1.2f, K_SCREEN, K_SCREEN_L)                 // 가슴 패널(파란 화면)
            .At(V(0, 27f, -13.2f)).Chamfer(V(16f, 15f, 7.5f), 2.5f, K_MID_D, K_LIGHT_D, K_LIGHT)          // 등 배낭
            .At(V(0, 31.5f, -16.9f)).Chamfer(V(16.4f, 2.6f, 1.2f), 0.4f, K_ORANGE_D, K_ORANGE)            // 배낭 윗띠(배낭에 박힌 띠)
            .At(-4.4f, 19.5f, -17.4f).Cyl(2.9f, 2.9f, 15f, 8, K_WHITE_D, K_LIGHT, false, K_WHITE)        // 산소통 둘
            .At(4.4f, 19.5f, -17.4f).Cyl(2.9f, 2.9f, 15f, 8, K_WHITE_D, K_LIGHT, false, K_WHITE)
            .At(-4.4f, 34.5f, -17.4f).Cyl(1.6f, 1.2f, 1.8f, 6, K_ORANGE, K_ORANGE, false)
            .At(4.4f, 34.5f, -17.4f).Cyl(1.6f, 1.2f, 1.8f, 6, K_ORANGE, K_ORANGE, false));
        var headM = ToyMesh.Cached("chPlHead", m => m
            .Helmet(V(15.6f, 14.6f, 15.2f), 1.7f, V(3.2f, 1.5f, 0.8f))
            .At(0, -13.6f, 0).Ring(8f, 12.6f, 3.6f, 12, K_ORANGE_D, K_ORANGE)                              // 목 링
            .At(10.2f, 8.6f, -4f).Cyl(0.9f, 0.8f, 9.5f, 6, K_DARK, K_DARK, false)                          // 안테나
            .At(V(10.2f, 19f, -4f)).Gem(V(2.3f, 2.3f, 2.3f), 6, 3, Dk(TIP, 0.15f), TIP));

        legL = Pivot(body, "legL", -6, 13, 0); PutL(legL, "m", legM, BodyMat, 0, 0, 0);
        legR = Pivot(body, "legR", 6, 13, 0); PutL(legR, "m", legM, BodyMat, 0, 0, 0);
        PutL(body, "torso", torsoM, BodyMat, 0, 0, 0);
        armL = Pivot(body, "armL", -15.5f, 31, 0); PutL(armL, "m", armM, BodyMat, 0, 0, 0);
        armR = Pivot(body, "armR", 15.5f, 31, 0); PutL(armR, "m", armM, BodyMat, 0, 0, 0);
        head = Pivot(body, "head", 0, 49, 0); PutL(head, "helmet", headM, HelmMat, 0, 0, 0);
    }

    // ───────── 정착민: 옷 4색 × 액세서리 0~5, VIP 금색 몸 + 왕관 ─────────
    // 피벗·높이는 예전과 같다: 다리 (±5,11) · 팔 (±13,27) · 머리 (0,42), 헬멧 꼭대기 54(모자 ≈ 58, 왕관 ≈ 61)
    static void BuildCustomerKK(Transform body, Color col, int acc, bool vip, out Transform head)
    {
        if (vip) col = GOLD;
        var rig = body.gameObject.AddComponent<CharRig>();
        rig.ph = Random.Range(0f, 7f);
        Color lo = Dk(col, 0.2f), hi = Lt(col, 0.14f), packC = Dk(col, 0.3f);
        string ck = Hx(col);

        var legM = ToyMesh.Cached("chCuLeg", m => m
            .At(0, -10f, 0).Cyl(3.7f, 4.3f, 10f, 8, K_DARK_D, K_DARK, false)
            .At(V(0, -9.3f, 1.2f)).Chamfer(V(8f, 4.2f, 10.6f), 1.5f, K_INK, K_DARK_D, Lt(K_DARK_D, 0.06f)));
        var armM = ToyMesh.Cached("chCuArm" + ck, m => m
            .At(0, -9f, 0).Cyl(3.0f, 3.6f, 9.5f, 8, lo, hi, false)
            .At(V(0, -10.5f, 0)).Gem(V(3.7f, 3.6f, 3.7f), 8, 4, K_WHITE_D, K_WHITE));
        var torsoM = ToyMesh.Cached("chCuTorso" + ck + "_" + acc, m =>
        {
            m.At(V(0, 21f, 0)).Chamfer(V(22.5f, 19.5f, 18.5f), 5f, lo, hi)
             .At(V(0, 13.2f, 0)).Chamfer(V(23.6f, 3.8f, 19.6f), 1.5f, K_DARK_D, K_DARK)                    // 벨트
             .At(V(0, 24.5f, -11.6f)).Chamfer(V(12.5f, 12.5f, 7f), 2f, Dk(packC, 0.1f), packC)            // 작은 배낭
             .At(V(8f, 8.5f, -13.5f), V(0, 0, 90)).Cyl(4.2f, 4.2f, 16f, 8, K_BROWN, K_BROWN_L, false, K_BROWN_L, true);   // 침낭
            if (acc == 1) m.At(V(0, 27f, -15f)).Chamfer(V(14.5f, 16.5f, 9f), 2.5f, Dk(packC, 0.1f), packC);   // 큰 배낭
            if (acc == 5) m.At(0, 29.2f, 0).Ring(9.4f, 12.6f, 4.2f, 10, K_WHITE_D, K_WHITE);                     // 목도리(크림)
            return m;
        });
        Color hc = Color.Lerp(col, K_INK, acc == 3 ? 0.2f : 0.45f);   // 모자도 옷색 계열
        var headM = ToyMesh.Cached("chCuHead" + (vip ? "v" : "") + acc + (acc == 0 || acc == 3 ? Hx(hc) : ""), m =>
        {
            m.Helmet(V(12.4f, 12f, 12.2f), 1.4f, V(2.4f, 1.1f, 0.6f))
             .At(0, -11.6f, 0).Ring(6.4f, 9.8f, 2.8f, 10, K_WHITE_D, K_WHITE);                               // 목 링(옷과 헬멧 사이)
            if (acc == 0 || acc == 3)   // 모자: 두툼한 챙 + 윗부분
                m.At(0, 8.4f, 0).Cyl(9.8f, 9.8f, 2.8f, 10, Dk(hc, 0.1f), hc, false, null, true)
                 .At(0, 10.8f, 0).Cyl(7f, 6.2f, 6f, 10, Dk(hc, 0.05f), Lt(hc, 0.1f), false, hc);
            if (acc == 4)   // 안테나
                m.At(7f, 8f, -3f).Cyl(0.8f, 0.7f, 9f, 6, K_DARK, K_DARK, false)
                 .At(V(7f, 17.6f, -3f)).Gem(V(2f, 2f, 2f), 6, 3, K_WHITE_D, K_WHITE);
            if (vip)    // 왕관: 두툼한 금 띠 + 네모 뿔 셋(가운데 큼)
                m.At(0, 8.6f, 0).Ring(6.6f, 8.8f, 3f, 10, GOLD_D, GOLD)
                 .At(-5.2f, 11.4f, 0, 45).Cyl(2.6f, 0f, 5.6f, 4, GOLD_D, GOLD, false, GOLD)
                 .At(0f, 11.4f, 0, 45).Cyl(3f, 0f, 8.2f, 4, GOLD_D, GOLD, false, GOLD)
                 .At(5.2f, 11.4f, 0, 45).Cyl(2.6f, 0f, 5.6f, 4, GOLD_D, GOLD, false, GOLD);
            return m;
        });

        rig.legL = Pivot(body, "legL", -5, 11, 0); PutL(rig.legL, "m", legM, BodyMat, 0, 0, 0);
        rig.legR = Pivot(body, "legR", 5, 11, 0); PutL(rig.legR, "m", legM, BodyMat, 0, 0, 0);
        PutL(body, "torso", torsoM, BodyMat, 0, 0, 0);
        rig.armL = Pivot(body, "armL", -13.2f, 27, 0); PutL(rig.armL, "m", armM, BodyMat, 0, 0, 0);
        rig.armR = Pivot(body, "armR", 13.2f, 27, 0); PutL(rig.armR, "m", armM, BodyMat, 0, 0, 0);
        head = Pivot(body, "head", 0, 42, 0); rig.head = head;
        PutL(head, "helmet", headM, HelmMat, 0, 0, 0);
    }

    // ───────── 로봇(삐걱·뽀삐): KayKit 기계처럼 깎은 상자 몸 + 주황 띠, 밝은 회색 머리 + 짙은 화면 + 민트 눈 ─────────
    // head 그룹 (0,38,0) 은 예전 자리(삐걱이 위아래·좌우로 까딱). 비콘 꼭대기 ≈ 60
    static Transform RobotCoreKK(Transform parent, Color col, string key)
    {
        var bodyM = ToyMesh.Cached("chBotBody" + key, m => m
            .At(V(-5.2f, 5.2f, 0)).Chamfer(V(6.6f, 10.4f, 8f), 1.6f, K_INK, K_DARK)                       // 다리 둘
            .At(V(5.2f, 5.2f, 0)).Chamfer(V(6.6f, 10.4f, 8f), 1.6f, K_INK, K_DARK)
            .At(V(0, 19.5f, 0)).Chamfer(V(23f, 18f, 17f), 3.5f, Dk(col, 0.22f), Lt(col, 0.1f))           // 몸
            .At(V(0, 13.6f, 0)).Chamfer(V(23.8f, 3.2f, 17.8f), 1.2f, K_ORANGE_D, K_ORANGE)               // 주황 띠(KayKit 기계와 같은 강조)
            .At(V(0, 21.5f, 8.2f)).Chamfer(V(10f, 6.4f, 2.4f), 1f, K_INK, K_DARK_D)                       // 가슴 패널
            .At(0, 28f, 0).Cyl(3.4f, 3f, 3.6f, 8, K_DARK_D, K_DARK, false));                                // 목
        PutL(parent, "botBody", bodyM, BodyMat, 0, 0, 0);
        var headM = ToyMesh.Cached("chBotHead", m => m
            .At(V(0, 0, 0)).Chamfer(V(24f, 16f, 18f), 3.5f, K_LIGHT_D, Lt(K_LIGHT, 0.3f))
            .At(V(0, 0.6f, 8.6f)).Chamfer(V(17.5f, 8.4f, 2.8f), 1.2f, K_INK, K_DARK_D)                    // 얼굴 화면
            .At(V(-4f, 1f, 10.1f)).Chamfer(V(3.4f, 2.8f, 1.2f), 0.5f, EYE, Lt(EYE, 0.3f))               // 민트 눈
            .At(V(4f, 1f, 10.1f)).Chamfer(V(3.4f, 2.8f, 1.2f), 0.5f, EYE, Lt(EYE, 0.3f))
            .At(V(12.2f, 0, 0), V(0, 0, 90)).Cyl(3.2f, 2.8f, 2.4f, 8, K_MID_D, K_MID, false)             // 귀 볼트
            .At(V(-12.2f, 0, 0), V(0, 0, -90)).Cyl(3.2f, 2.8f, 2.4f, 8, K_MID_D, K_MID, false)
            .At(0, 7.5f, 0).Cyl(0.9f, 0.8f, 9.5f, 6, K_DARK, K_DARK, false)                                 // 안테나
            .At(V(0, 19f, 0)).Gem(V(3.2f, 3.2f, 3.2f), 8, 4, GOLD_D, GOLD));                               // 금 비콘
        var head = Pivot(parent, "head", 0, 38, 0);
        PutL(head, "botHead", headM, HelmMat, 0, 0, 0);
        return head;
    }

    static void BuildRunnerKK(Transform body, out Transform head)
    {
        head = RobotCoreKK(body, BOT_PINK, "pink");
        // 뒤로 뻗은 팔 2개: 어깨(y 29, z 0)에서 손(y 22, z -12)으로 내려가 손수레 손잡이 꼭대기를 잡는다
        var armM = ToyMesh.Cached("chBotArms", m => m
            .At(V(-12f, 25.5f, -6f), V(-30, 0, 0)).Chamfer(V(4.2f, 4.2f, 14f), 1.2f, K_MID_D, K_MID)
            .At(V(12f, 25.5f, -6f), V(-30, 0, 0)).Chamfer(V(4.2f, 4.2f, 14f), 1.2f, K_MID_D, K_MID));
        PutL(body, "arms", armM, BodyMat, 0, 0, 0);
    }

    // 손수레: KayKit 화물칸 톤(밝은 회색 짐칸 + 주황 띠 + 짙은 회색 받침), 짐칸 윗면 y 19.5. 바퀴는 따로 돈다
    static Transform BuildCartKK(Transform view, out Transform stack)
    {
        var cart = Group(view, "cart", 0, 0, -26);
        var rig = cart.gameObject.AddComponent<CharRig>();
        var bedM = ToyMesh.Cached("chCartBed", m =>
        {
            m.At(V(0, 5.2f, 0)).Chamfer(V(30f, 4f, 26f), 1.5f, K_INK, K_DARK_D)                            // 받침
             .At(V(0, 12.75f, 0)).Chamfer(V(28f, 13.5f, 24f), 2.5f, K_LIGHT_D, K_LIGHT, K_MID)             // 짐칸(윗면은 짐 바닥이라 중간 회색)
             .At(V(0, 14.2f, 0)).Chamfer(V(28.8f, 3f, 24.8f), 1.2f, K_ORANGE_D, K_ORANGE);                // 주황 띠
            // 손잡이: 짐칸 앞 모서리에서 로봇 손(view 기준 y 23, z -11)까지 살짝 기운 막대 둘
            var q = Quaternion.Euler(10, 0, 0);
            foreach (float x in new[] { -10f, 10f })
            {
                var c0 = V(x, 14f, 13.5f);
                m.Pipe(c0 - q * Vector3.up * 9f, c0 + q * Vector3.up * 9f, 1.25f, K_MID, false, 6);
            }
            return m;
        });
        PutL(cart, "bed", bedM, BodyMat, 0, 0, 0);
        var wheelM = ToyMesh.Cached("chCartWheel", m => m
            .At(0, -1.7f, 0).Cyl(6f, 6f, 3.4f, 8, K_INK, K_DARK_D, false, null, true)
            .At(0, -2.1f, 0).Cyl(2.6f, 2.6f, 4.2f, 6, K_LIGHT_D, K_LIGHT, false, null, true));
        var wheels = new List<Transform>();
        foreach (float x in new[] { -16f, 16f })
        {
            var w = PutL(cart, "wheel", wheelM, BodyMat, x, 6, 6).transform;
            w.localRotation = Quaternion.Euler(0, 0, 90);   // 축(로컬 y)이 동서로: SpinWheels 가 로컬 y 로 굴린다
            wheels.Add(w);
        }
        rig.wheels = wheels.ToArray();
        stack = Group(cart, "stack", 0, 19.5f, 0);
        return cart;
    }

    // ───────── 먼지 토끼: 저폴리 공 몸(스케일로 숨쉬기·깡충 늘어남) + 머리·발·꼬리 한 메시 + 귀 둘·코 ─────────
    // 자리·크기는 예전(PenSystem.MakeHopper)과 같다. body·nose 는 지름 1(논리 1/K) 메시라 호출처가 localScale 로 크기를 준다
    static readonly Color B_TAN = GameConfig.Hex("d9a46c"), B_TAN_L = GameConfig.Hex("e9bd86"), B_TAN_D = GameConfig.Hex("b0804e"), B_TAN_DD = GameConfig.Hex("94683c");
    static readonly Color B_EAR_IN = GameConfig.Hex("f2b8a0"), B_NOSE = GameConfig.Hex("ffb3a0"), B_EYE = GameConfig.Hex("2b2420"), B_TAIL = GameConfig.Hex("f8f6f2");
    public static void BuildBunny(Transform root, out Transform body, out Transform earL, out Transform earR, out Transform nose)
    {
        float u = 0.5f / K;   // 지름 1(유니티) 공의 반지름(논리)
        var bodyM = ToyMesh.Cached("chBunBody", m => m.Gem(V(u, u, u), 10, 6, B_TAN_D, B_TAN_L));
        body = PutL(root, "body", bodyM, BodyMat, 0, 10, 0).transform;
        body.localScale = V(18f, 16.2f, 21.6f) * K;
        var headM = ToyMesh.Cached("chBunHead", m => m
            .At(V(0, 17f, 8f)).Gem(V(7.2f, 7f, 7.2f), 10, 6, B_TAN_D, B_TAN_L)
            .At(V(-3f, 18f, 14.2f)).Gem(V(1.4f, 1.5f, 1.2f), 6, 3, B_EYE, B_EYE)
            .At(V(3f, 18f, 14.2f)).Gem(V(1.4f, 1.5f, 1.2f), 6, 3, B_EYE, B_EYE)
            .At(V(-5f, 2.5f, 4f)).Gem(V(3.5f, 1.9f, 5.6f), 8, 4, B_TAN_DD, B_TAN_D)                       // 발
            .At(V(5f, 2.5f, 4f)).Gem(V(3.5f, 1.9f, 5.6f), 8, 4, B_TAN_DD, B_TAN_D)
            .At(V(0, 10.5f, -11f)).Gem(V(3.2f, 3.2f, 3.2f), 6, 4, Dk(B_TAIL, 0.08f), B_TAIL));             // 꼬리
        PutL(root, "head", headM, BodyMat, 0, 0, 0);
        var earM = ToyMesh.Cached("chBunEar", m => m
            .At(V(0, 4.8f, 0)).Gem(V(1.6f, 5.4f, 1.25f), 6, 4, B_TAN_D, B_TAN_L)
            .At(V(0, 4.8f, 0.8f)).Gem(V(0.8f, 3.4f, 0.6f), 6, 3, B_EAR_IN, B_EAR_IN));
        earL = Pivot(root, "earL", -3, 21.5f, 6); PutL(earL, "m", earM, BodyMat, 0, 0, 0);
        earR = Pivot(root, "earR", 3, 21.5f, 6); PutL(earR, "m", earM, BodyMat, 0, 0, 0);
        var noseM = ToyMesh.Cached("chBunNose", m => m.Gem(V(u, u, u), 6, 3, Dk(B_NOSE, 0.1f), B_NOSE));
        nose = PutL(root, "nose", noseM, BodyMat, 0, 15.5f, 15f).transform;
        nose.localScale = V(2.8f, 2.6f, 2.6f) * K;
    }
}

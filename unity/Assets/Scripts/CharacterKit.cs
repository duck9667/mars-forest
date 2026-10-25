using System.Collections.Generic;
using UnityEngine;

// 캐릭터 조립 킷: 원본 app/index.html 의 bPerson(플레이어)·bTraveler(손님)·bRobot(삐걱·뽀삐)을
// 프리미티브(+원뿔·토러스 절차 메시)로 옮긴 것. 치수는 논리 단위(×GameConfig.K), 로컬 +z 가 정면.
// 콜라이더 없음, 단색 재질(Mats.Solid). 외곽선은 포스트 프로세스 담당(여기선 신경 안 씀).
// 팔다리는 피벗이 위쪽 끝인 빈 Transform 이라 호출처가 localRotation 으로 흔든다.

// 조립 결과 중 애니메이션이 만지는 부품 모음(body 에 붙음). 손님·로봇처럼 필드가 없는 호출처가 쓴다.
public class CharRig : MonoBehaviour
{
    public Transform armL, armR, legL, legR, head;
    public Transform[] wheels = new Transform[0];
    public float ph;   // 걷기 위상(개체마다 다르게 해 줄이 한 박자로 흔들리지 않게)
}

public static class CharacterKit
{
    const float K = GameConfig.K;

    // 원본 COL 팔레트
    public static readonly Color SUIT = GameConfig.Hex("f7f3ec");
    public static readonly Color WHITE = GameConfig.Hex("f8f6f2");
    public static readonly Color DARK = GameConfig.Hex("2b2420");
    public static readonly Color NAVY = GameConfig.Hex("22304a");
    public static readonly Color BLUE = GameConfig.Hex("4fb0f0");
    public static readonly Color ORANGE = GameConfig.Hex("f07a3a");
    public static readonly Color GOLD = GameConfig.Hex("ffc83d");
    public static readonly Color GOLD_D = GameConfig.Hex("c98a00");
    public static readonly Color GREEN = GameConfig.Hex("5cc45e");
    public static readonly Color WOOD_D = GameConfig.Hex("6e3f26");
    public static readonly Color PLANK = GameConfig.Hex("a8653f");
    public static readonly Color STEEL = GameConfig.Hex("c9d1d8");
    public static readonly Color STEEL_D = GameConfig.Hex("8c98a4");
    public static readonly Color PURPLE = GameConfig.Hex("8a5cd6");
    public static readonly Color TEAL = GameConfig.Hex("5ed6c8");
    public static readonly Color CORAL = GameConfig.Hex("ff8c69");
    public static readonly Color RUST = GameConfig.Hex("d4612f");
    public static readonly Color EYE = GameConfig.Hex("6cf0d0");
    public static readonly Color TIP = GameConfig.Hex("ff6b5a");
    public static readonly Color BOT_GREY = GameConfig.Hex("9aa7b2");   // 삐걱 몸 (§5.1)
    public static readonly Color BOT_PINK = GameConfig.Hex("f29bc8");   // 뽀삐 몸

    // ───────── 플레이어 (bPerson): 흰 우주복, 둥근 헬멧·남색 바이저, 등 배낭+산소통, 주황 벨트·장갑·목 링, 안테나 ─────────
    // 키 ≈ 64(헬멧 꼭대기)+안테나 2. 머리가 전체의 절반 가까운 치비 비율.
    public static void BuildPlayer(Transform body, out Transform head, out Transform armL, out Transform armR, out Transform legL, out Transform legR)
    {
        Color suit = SUIT, acc = ORANGE;
        legL = Limb(body, "legL", -6, 13, 0, 5f, 8f, suit);
        legR = Limb(body, "legR", 6, 13, 0, 5f, 8f, suit);
        Ell(legL, "boot", 0, -9.5f, 1.5f, 12.1f, 6.6f, 15.4f, DARK);
        Ell(legR, "boot", 0, -9.5f, 1.5f, 12.1f, 6.6f, 15.4f, DARK);
        Ell(body, "torso", 0, 25, 0, 32.5f, 27.3f, 26f, suit);
        Box(body, "chest", 0, 27, 12.5f, 10, 6, 2, BLUE);          // 가슴 패널
        Box(body, "belt", 0, 16, 0, 28, 4, 22, acc);                // 벨트
        armL = Limb(body, "armL", -15, 31, 0, 4.2f, 11f, suit);
        armR = Limb(body, "armR", 15, 31, 0, 4.2f, 11f, suit);
        Sph(armL, "glove", 0, -12, 0, 9, acc);
        Sph(armR, "glove", 0, -12, 0, 9, acc);
        Box(body, "pack", 0, 28, -12, 14, 14, 7, DARK);             // 등 배낭
        Cyl(body, "tankL", -4, 28, -15, 6, 14, WHITE);              // 산소통 2개
        Cyl(body, "tankR", 4, 28, -15, 6, 14, WHITE);
        head = Group(body, "head", 0, 49, 0);
        Sph(head, "helmet", 0, 0, 0, 30, suit);
        // 바이저: 원본 수치(z 6.5, 깊이 16.1)는 헬멧(r15) 안에 묻혀 안 보여서 앞으로 빼 표면보다 1.5 돌출시킴
        Ell(head, "visor", 0, 1, 9, 24f, 19f, 15f, NAVY);
        Ell(head, "glint", -4, 5, 16f, 6.5f, 3f, 1.6f, WHITE);      // 바이저 하이라이트
        Torus(head, "collar", 0, -8, 0, 15.3f, 1.6f, acc);          // 헬멧 목 링
        Cyl(head, "ant", 11, 12, -4, 1.6f, 9, DARK);
        Sph(head, "antTip", 11, 17, -4, 4, TIP);
    }

    // ───────── 손님 (bTraveler): 몸색 col × 액세서리 acc 0~5, VIP 는 금색 몸+왕관 ─────────
    // acc: 0 남색 모자 · 1 큰 초록 배낭 · 2 보라 바이저 · 3 녹슨색 모자 · 4 안테나 · 5 산호색 목도리(+바이저)
    // 키 ≈ 54(머리 꼭대기), 모자 58. 머리 위 병 스택은 호출처가 y 56(VIP 66)에 둔다.
    public static void BuildCustomer(Transform body, Color col, int acc, bool vip, out Transform head)
    {
        if (vip) col = GOLD;
        var rig = body.gameObject.AddComponent<CharRig>();
        rig.ph = Random.Range(0f, 7f);
        rig.legL = Limb(body, "legL", -5, 11, 0, 4.5f, 7f, DARK);
        rig.legR = Limb(body, "legR", 5, 11, 0, 4.5f, 7f, DARK);
        Ell(body, "torso", 0, 21, 0, 26.4f, 23.1f, 20.9f, col);
        Box(body, "belt", 0, 13, 0, 24, 3.5f, 18, DARK);
        rig.armL = Limb(body, "armL", -13, 27, 0, 3.8f, 10f, col);
        rig.armR = Limb(body, "armR", 13, 27, 0, 3.8f, 10f, col);
        head = Group(body, "head", 0, 42, 0);
        rig.head = head;
        Sph(head, "skull", 0, 0, 0, 24, WHITE);
        // 바이저: 원본(z 5.5, 깊이 11.9)은 머리(r12) 안에 묻혀서 앞으로 빼 표면보다 1 돌출
        Ell(head, "visor", 0, 1, 7.5f, 17f, 13f, 11f, NAVY);   // 바이저는 모두 남색(색 바이저는 줄을 어수선하게 했다)
        Box(body, "pack", 0, 25, -11, 12, 12, 7, Color.Lerp(col, DARK, 0.25f));   // 작은 배낭(옷색 한 톤 어둡게)
        Box(body, "roll", 0, 8, -13, 16, 8, 8, WOOD_D);                     // 배낭 아래 침낭
        if (acc == 0 || acc == 3)   // 모자: 챙 + 윗부분
        {
            Color hc = Color.Lerp(col, DARK, acc == 3 ? 0.2f : 0.45f);   // 모자도 옷색 계열
            Cyl(head, "brim", 0, 10, 0, 18, 2, hc);
            Cyl(head, "crownTop", 0, 13, 0, 13, 6, hc);
        }
        if (acc == 1) Box(body, "bigPack", 0, 27, -14, 14, 16, 9, Color.Lerp(col, DARK, 0.25f));   // 큰 배낭
        if (acc == 4)   // 안테나
        {
            Cyl(head, "ant", 8, 16, -3, 1.6f, 10, DARK);
            Sph(head, "antTip", 8, 21, -3, 3.6f, WHITE);
        }
        if (acc == 5) Torus(body, "scarf", 0, 32, 0, 11f, 2.2f, SUIT);     // 목도리(크림)
        if (vip)   // 왕관: 금 띠 + 뾰족 3개(가운데 큼)
        {
            Cyl(head, "crownBand", 0, 10, 0, 16, 2, GOLD_D);        // 띠 y 9~11
            Cone(head, "spikeL", -6, 14, 0, 6, 6, GOLD);            // 밑면이 띠 윗면(11)에 닿게
            Cone(head, "spikeM", 0, 15.5f, 0, 6, 9, GOLD);
            Cone(head, "spikeR", 6, 14, 0, 6, 6, GOLD);
        }
    }

    // ───────── 삐걱 (bRobot 회색): 계산대 옆 고정. 상자 다리·몸·강철 머리, 남색 바이저+민트 눈, 안테나·금 비콘 ─────────
    // 키 ≈ 46(머리 꼭대기), 비콘 60. head 는 (0,38,0) 그룹이라 호출처가 까딱 애니를 건다.
    public static void BuildCashier(Transform view, out Transform head)
    {
        head = RobotCore(view, BOT_GREY);
    }

    // ───────── 뽀삐 (bRobot 분홍): 몸은 삐걱과 같은 골격, 손수레(BuildCart)를 뒤에 끌고 다닌다 ─────────
    public static void BuildRunner(Transform body, out Transform head)
    {
        head = RobotCore(body, BOT_PINK);
        // 뒤로 뻗은 팔 2개(긴 축 z): 어깨(y 29, z 0)에서 손(y 22, z -12)으로 내려가 손수레 손잡이 꼭대기를 잡는다
        Box(body, "armL", -11, 25.5f, -6, 4, 4, 14, STEEL_D).transform.localRotation = Quaternion.Euler(-30, 0, 0);
        Box(body, "armR", 11, 25.5f, -6, 4, 4, 14, STEEL_D).transform.localRotation = Quaternion.Euler(-30, 0, 0);
    }

    // 손수레: view 자식(걷기 bob 에 안 흔들림), 로봇 뒤 z -26. 반환 = cart, stack = 짐칸 윗면(y 19.5).
    // 원본은 앞(z +26)에서 밀지만 적재 자리가 병입기 모델과 겹쳐 뒤로 끄는 형태로 바꿈.
    public static Transform BuildCart(Transform view, out Transform stack)
    {
        var cart = Group(view, "cart", 0, 0, -26);
        var rig = cart.gameObject.AddComponent<CharRig>();
        Box(cart, "bed", 0, 12, 0, 28, 12, 24, PLANK);
        Box(cart, "top", 0, 18.5f, 0, 26, 2, 22, WOOD_D);
        Box(cart, "base", 0, 6, 0, 30, 3, 26, WOOD_D);
        var wheels = new List<Transform>();
        foreach (float x in new[] { -16f, 16f })
        {
            // 바퀴·허브는 형제(스케일된 바퀴의 자식으로 두면 부모 스케일이 곱해져 크기가 틀어짐)
            var w = Cyl(cart, "wheel", x, 6, 6, 12, 3, DARK); w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            Cyl(cart, "hub", x, 6, 6, 4, 3.5f, STEEL).transform.localRotation = Quaternion.Euler(0, 0, 90);
            wheels.Add(w.transform);
        }
        rig.wheels = wheels.ToArray();
        // 손잡이: 짐칸 앞 모서리(z 12, y 5)에서 로봇 손(view 기준 y 23, z -11)까지 살짝 기운 막대 2개
        foreach (float x in new[] { -10f, 10f })
            Cyl(cart, "handle", x, 14, 13.5f, 2.4f, 18, STEEL_D).transform.localRotation = Quaternion.Euler(10, 0, 0);
        stack = Group(cart, "stack", 0, 19.5f, 0);
        return cart;
    }

    // 로봇 공통 골격. head 그룹 (0,38,0) 반환
    static Transform RobotCore(Transform parent, Color col)
    {
        Box(parent, "legL", -5, 5, 0, 6, 10, 6, DARK);
        Box(parent, "legR", 5, 5, 0, 6, 10, 6, DARK);
        Box(parent, "torso", 0, 19, 0, 22, 18, 16, col);
        var head = Group(parent, "head", 0, 38, 0);
        Box(head, "skull", 0, 0, 0, 24, 16, 18, STEEL);
        Box(head, "visor", 0, 1, 9.5f, 16, 6, 3, NAVY);
        Box(head, "eyeL", -4, 1, 11.2f, 3, 2, 1, EYE);
        Box(head, "eyeR", 4, 1, 11.2f, 3, 2, 1, EYE);
        Cyl(head, "ant", 0, 13, 0, 1.6f, 10, DARK);
        Sph(head, "beacon", 0, 19, 0, 6, GOLD);
        return head;
    }

    // ───────── 걷기 스윙: 다리 ±s, 팔 ∓0.7s (각도 amp°), 목표치로 dt*10 보간 ─────────
    public static void Swing(CharRig rig, float s, float amp, float dt)
    {
        if (rig == null || rig.legL == null) return;
        float a = dt * 10f;
        Rot(rig.legL, -s * amp, a); Rot(rig.legR, s * amp, a);
        Rot(rig.armL, s * 0.7f * amp, a); Rot(rig.armR, -s * 0.7f * amp, a);
    }
    static void Rot(Transform t, float xDeg, float a) { if (t) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(xDeg, 0, 0), a); }

    // 바퀴 굴리기: 이동 속도(논리/초)에 맞춰 바퀴 지름 12 기준 각속도
    public static void SpinWheels(CharRig rig, float speedL, float dt)
    {
        if (rig == null) return;
        float deg = speedL / (Mathf.PI * 12f) * 360f * dt;
        foreach (var w in rig.wheels) if (w) w.Rotate(0, deg, 0, Space.Self);
    }

    // ───────── 프리미티브 래퍼: 위치·크기 모두 논리 단위 ─────────
    static Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * K;
    static Transform Group(Transform p, string n, float x, float y, float z)
    {
        var t = new GameObject(n).transform; t.SetParent(p, false); t.localPosition = P(x, y, z); return t;
    }
    static GameObject Box(Transform p, string n, float x, float y, float z, float w, float h, float d, Color c) => Prim.Box(p, n, P(x, y, z), new Vector3(w, h, d), c);
    static GameObject Cyl(Transform p, string n, float x, float y, float z, float dia, float h, Color c) => Prim.Cyl(p, n, P(x, y, z), dia, h, c);
    static GameObject Sph(Transform p, string n, float x, float y, float z, float dia, Color c) => Prim.Sphere(p, n, P(x, y, z), dia, c);
    // 타원체: 세 축 지름
    static GameObject Ell(Transform p, string n, float x, float y, float z, float dx, float dy, float dz, Color c)
    {
        var g = Prim.Sphere(p, n, P(x, y, z), 1, c); g.transform.localScale = new Vector3(dx, dy, dz) * K; return g;
    }
    // 팔다리: 피벗(위쪽 끝) 그룹 + 반지름 r·반높이 h 타원체(원본 limbOf: 구 r 을 y 로 h/r 배)
    static Transform Limb(Transform p, string n, float x, float y, float z, float r, float h, Color c)
    {
        var piv = Group(p, n, x, y, z);
        Ell(piv, "m", 0, -h * 0.5f, 0, r * 2, h * 2, r * 2, c);
        return piv;
    }

    // ───────── 절차 메시: 원뿔·토러스(프리미티브에 없음). 면마다 정점을 따로 둬 평면 셰이딩 ─────────
    static GameObject MeshObj(Transform p, string n, Vector3 pos, Mesh m, Color c)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false); go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Solid(c);
        return go;
    }
    // 원뿔: 밑면 지름 dia, 높이 h, 중심이 pos (밑면 -h/2, 꼭짓점 +h/2)
    static GameObject Cone(Transform p, string n, float x, float y, float z, float dia, float h, Color c)
    {
        var go = MeshObj(p, n, P(x, y, z), ConeMesh(), c);
        go.transform.localScale = new Vector3(dia, h, dia) * K;
        return go;
    }
    // 토러스: 고리 반지름 R, 관 반지름 r, 축은 y (바닥에 눕힌 링)
    static GameObject Torus(Transform p, string n, float x, float y, float z, float R, float r, Color c) => MeshObj(p, n, P(x, y, z), TorusMesh(R * K, r * K), c);

    static Mesh _cone;
    static readonly Dictionary<string, Mesh> torusCache = new Dictionary<string, Mesh>();

    static Mesh ConeMesh()
    {
        if (_cone != null) return _cone;
        const int N = 12; var v = new List<Vector3>(); var t = new List<int>();
        Vector3 apex = new Vector3(0, 0.5f, 0), bc = new Vector3(0, -0.5f, 0);
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N;
            var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
            var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
            var mid = (p0 + p1) * 0.5f;
            Tri(v, t, apex, p0, p1, new Vector3(mid.x, 0.3f, mid.z));   // 옆면: 바깥쪽+약간 위
            Tri(v, t, bc, p0, p1, Vector3.down);                         // 밑면
        }
        _cone = Build(v, t); return _cone;
    }

    static Mesh TorusMesh(float R, float r)
    {
        string key = R.ToString("0.####") + "/" + r.ToString("0.####");
        if (torusCache.TryGetValue(key, out var m)) return m;
        const int N = 20, M = 8; var v = new List<Vector3>(); var t = new List<int>();
        Vector3 P2(float a, float b) => new Vector3((R + r * Mathf.Cos(b)) * Mathf.Cos(a), r * Mathf.Sin(b), (R + r * Mathf.Cos(b)) * Mathf.Sin(a));
        for (int i = 0; i < N; i++)
        {
            float a0 = i * Mathf.PI * 2 / N, a1 = (i + 1) * Mathf.PI * 2 / N, am = (a0 + a1) * 0.5f;
            var ring = new Vector3(R * Mathf.Cos(am), 0, R * Mathf.Sin(am));   // 이 조각의 관 중심
            for (int j = 0; j < M; j++)
            {
                float b0 = j * Mathf.PI * 2 / M, b1 = (j + 1) * Mathf.PI * 2 / M;
                Vector3 p00 = P2(a0, b0), p10 = P2(a1, b0), p11 = P2(a1, b1), p01 = P2(a0, b1);
                var outward = (p00 + p10 + p11 + p01) * 0.25f - ring;
                Tri(v, t, p00, p10, p11, outward); Tri(v, t, p00, p11, p01, outward);
            }
        }
        m = Build(v, t); torusCache[key] = m; return m;
    }

    // 삼각형 추가: 법선이 outward 를 향하도록 감는 방향 맞춤(Unity 는 시계방향이 앞면)
    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { var tmp = b; b = c; c = tmp; }
        int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }
    static Mesh Build(List<Vector3> v, List<int> t)
    {
        var m = new Mesh { vertices = v.ToArray(), triangles = t.ToArray() };
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
}

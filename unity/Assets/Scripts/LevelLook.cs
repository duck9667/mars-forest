using System;
using System.Collections.Generic;
using UnityEngine;

// 시설 레벨 외형 3단계(jake 10/25 "기지도 레벨이 있고 돈을 줘서 레벨을 높임에 따라 기지도 좀 더 세련되고 멋있어지고 고도화되는 게 필요해").
//  기본(Lv0~1) → 개량(Lv2~3) → 고급(Lv4~최대). 대상과 레벨을 읽는 발판:
//   해빙기 melter · 병입기 bottler · 카운터 shelf · 계산대 price(프리미엄 라벨) · 전기분해기 elecspd · 토양 정화기 purspd · 비료 배합기 mixspd · 온실 grnspd · 본부 hq(Lv0~1·2~3·4~5)
//  문법(아트 바이블 "모형 만들기"): 얇은 판을 붙이지 않고 두툼한 덩어리를 ToyMesh 한 메시로. 단계가 오를수록
//   개량 = 금속 테·부품 하나 더(굴뚝·탱크·화면), 고급 = 매끈한 흰 외피 + 샴페인 금 테 + 유리 덮개·은은한 빛. 글자·말풍선은 없다.
//  모형은 시설마다 본체 메시 + 단계 부품 메시(단계마다 메시를 바꿔 끼움)로 그려 드로우콜이 늘지 않게 한다.
//  저장된 레벨에서 Start 때 바로 그 단계로 짓고(앱 재시작·이주 뒤에도 맞다), 레벨이 올라 단계가 바뀌는 순간만 바뀐 부품이 솟아오른다(JuiceFX.Rise, 화면 번쩍임 없음).
public static class LevelLook
{
    public static int Tier(int lv) => lv >= 4 ? 2 : lv >= 2 ? 1 : 0;
    public static int TierOf(string pad) => GameState.I == null ? 0 : Tier(GameState.I.PadLv(pad));
    // 외형이 레벨을 따르는 발판(본부는 레벨마다 조각이 솟아 따로 표시하지 않는다)
    static readonly HashSet<string> PADS = new HashSet<string> { "melter", "bottler", "shelf", "price", "elecspd", "purspd", "mixspd", "grnspd" };
    public static bool Has(string pad) => PADS.Contains(pad);
    // 업그레이드 시트 한 줄: 다음에 외형이 바뀌는 레벨("Lv4: 외형 변화"), 더 없으면 빈 문자열
    public static string NextText(string pad, int lv, int max)
    {
        if (!Has(pad)) return "";
        int n = lv < 2 ? 2 : lv < 4 ? 4 : -1;
        return n < 0 || n > max ? "" : "Lv" + n + ": 외형 변화";
    }

    // 단계 색: 고급의 금 테는 발판·산출 받침대의 금색(f2c14e)과 섞이지 않게 한 톤 차분한 샴페인 금
    public static readonly Color TRIM = GameConfig.Hex("d9b25c"), TRIM_L = GameConfig.Hex("f0d58e"),
        METAL = GameConfig.Hex("9aa4b0"), METAL_L = GameConfig.Hex("c6cdd5"), METAL_D = GameConfig.Hex("6c7581"),
        GLOSS = GameConfig.Hex("f8f5f0"), GLOSS_D = GameConfig.Hex("dcd5ca");
    // 유리 덮개(휴게 돔과 같은 MarsGlass: 가운데는 비치고 가장자리 테만)
    static Material _glass;
    public static Material GlassMat => _glass != null ? _glass : (_glass = Mats.Glass(new Color(0.94f, 0.98f, 1f, 0.05f), new Color(0.8f, 0.9f, 0.98f, 0.6f), 3f, 0.7f, 160f));
    // 하이라이트를 메시에 구운 유리(정점 알파 = 테·반사 줄 양, MarsGlass _VHi). 위에서 거의 안 보이던 해빙기 유리 돔(10/26)
    static Material _glassHi;
    public static Material GlassHiMat
    {
        get
        {
            if (_glassHi != null) return _glassHi;
            // 밝은 호퍼 위에선 흰 유리가 묻혀 몸통을 옅은 하늘빛으로(가운데 알파 0.05 → 0.26), 흰 테·반사 줄이 그 위에서 읽힌다
            _glassHi = Mats.Glass(new Color(0.66f, 0.85f, 1f, 0.26f), new Color(0.74f, 0.89f, 1f, 0.7f), 2.4f, 0.7f, 160f);
            _glassHi.SetFloat("_VHi", 1f); _glassHi.SetColor("_HiColor", new Color(1f, 1f, 1f, 0.92f));
            _glassHi.SetFloat("_RimAdd", 0.75f); _glassHi.SetFloat("_RimPow", 1f);   // 가장자리 밝힘(r6): 돔 둘레가 흰 하늘빛 테로 읽힌다
            return _glassHi;
        }
    }
    // 단계별 재질: 고급은 조금 더 매끈하게(해 반짝임이 맺힘)
    public static Material Mat(int tier) => ToyMesh.Mat(tier >= 2 ? 0.55f : tier == 1 ? 0.3f : 0.12f);

    // 메시 하나를 시설 그룹 아래 둔다(그림자 포함). mesh 가 null 이면 꺼 둔다
    public static MeshFilter Holder(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>().sharedMaterial = ToyMesh.Mat();
        return mf;
    }
}

// 단계별 부품 묶음: 메시 교체(본체는 살짝 눌리고 단계 부품은 솟아오름) + 단계 전용 오브젝트 켜고 끄기 + 단계 콜백(앵커·색·재질)
public class TierLook
{
    public int tier = -1;
    public Transform squashRoot;   // 단계가 바뀔 때 살짝 눌리는 뷰(시설 전체)
    public float squashAmp = 0.6f;  // 눌림 세기(긴 카운터는 작게: 폭 220이라 조금만 눌려도 크게 출렁인다)
    public Action<int, bool> onTier;   // (단계, 이번이 레벨 업 순간인지)
    readonly List<(MeshFilter mf, Func<int, Mesh> mesh, Func<int, Material> mat, bool rise)> swaps = new List<(MeshFilter, Func<int, Mesh>, Func<int, Material>, bool)>();
    readonly List<(GameObject go, int min, int max)> parts = new List<(GameObject, int, int)>();

    // mesh(t) 가 null 이면 그 단계엔 꺼 둔다. rise: 단계가 바뀌면 솟아오름(아니면 제자리 교체)
    public TierLook Swap(MeshFilter mf, Func<int, Mesh> mesh, Func<int, Material> mat = null, bool rise = false) { swaps.Add((mf, mesh, mat ?? LevelLook.Mat, rise)); return this; }
    // 단계 min~max 에서만 보이는 오브젝트(유리 덮개·두 번째 탱크 액체 등). 켜지는 순간 솟아오른다
    public TierLook Part(GameObject go, int min, int max = 2) { parts.Add((go, min, max)); return this; }

    public void Apply(int t, bool anim)
    {
        t = Mathf.Clamp(t, 0, 2);
        if (t == tier) return;
        bool changed = tier >= 0; tier = t;
        anim &= changed && JuiceFX.I != null;
        int n = 0;
        foreach (var s in swaps)
        {
            var m = s.mesh(t); var go = s.mf.gameObject;
            bool was = go.activeSelf; var old = s.mf.sharedMesh;
            if (m == null) { if (JuiceFX.I != null) JuiceFX.I.StopRise(go.transform); go.SetActive(false); continue; }
            s.mf.sharedMesh = m; go.GetComponent<MeshRenderer>().sharedMaterial = s.mat(t);
            go.SetActive(true);
            if (anim && s.rise && (old != m || !was)) JuiceFX.I.Rise(go.transform, 0.12f * n++);
        }
        foreach (var p in parts)
        {
            bool on = t >= p.min && t <= p.max, was = p.go.activeSelf;
            if (!on && JuiceFX.I != null) JuiceFX.I.StopRise(p.go.transform);
            p.go.SetActive(on);
            if (anim && on && !was) JuiceFX.I.Rise(p.go.transform, 0.12f * n++);
        }
        onTier?.Invoke(t, anim);
        if (anim && squashRoot != null) JuiceFX.I.Squash(squashRoot, squashAmp);
    }
}

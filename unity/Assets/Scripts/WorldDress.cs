using UnityEngine;

// Kenney 모델로 환경을 입히는 자리 (§9): 가장자리 바위·큰 바위, 착륙선 4단, 로버, 위성 안테나, 드럼통.
// 모델 파이프라인(ModelLib)이 붙기 전까지는 아무것도 하지 않는다. World.Start가 프리미티브 바위를 만든 뒤 호출한다.
public static class WorldDress
{
    // rockRoot: 프리미티브 바위들의 부모. 모델로 교체할 때 이 아래를 비우고 다시 채운다.
    public static void Dress(Transform worldRoot, Transform rockRoot) { }
}

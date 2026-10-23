using UnityEngine;

// Kenney Space Kit 모델로 스테이션을 입히는 자리 (§9). 모델 파이프라인(ModelLib)이 붙기 전까지는 아무것도 하지 않는다.
// 규약: 각 view의 플레이스홀더 자식은 이름이 "body"/"top"이다. 모델을 붙일 때 그것들을 숨긴다.
public static class StationDress
{
    public static void Dress(Stations st) { }
    public static void DressDrill(Transform drillView) { }
}

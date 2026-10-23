using UnityEngine;

// §9 위성 안테나 접시 회전. 원본(index.html 1046행) dishG.rotation.y = t/1800 (t는 ms) → 1/1.8 rad/s.
// 월드가 z 거울이라 Unity에선 부호를 뒤집어 같은 방향으로 돈다.
public class DishSpin : MonoBehaviour
{
    public float radPerSec = -1f / 1.8f;
    void Update() { transform.Rotate(0f, radPerSec * Mathf.Rad2Deg * Time.deltaTime, 0f, Space.Self); }
}

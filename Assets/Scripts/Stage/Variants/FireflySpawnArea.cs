using UnityEngine;

/// <summary>
/// 반딧불이 나타나는 영역 (씬에 배치, 여러 개 가능). 이 오브젝트 위치/Y 회전 기준 size 사각형 안에 반딧불이 생기고 다시 나타남.
/// </summary>
public class FireflySpawnArea : MonoBehaviour
{
    [Tooltip("영역 크기 (X, Z)")] public Vector2 size = new Vector2(100f, 100f);
    [Tooltip("이 반경 안(중앙 시작 지점)에는 만들지 않음")] public float clearRadius = 6f;
    [Tooltip("여러 영역일 때 상대 비율")] public float weight = 1f;

    public Vector3 RandomPoint()
    {
        for (int i = 0; i < 12; i++)
        {
            var p = transform.position + transform.rotation * new Vector3(Random.Range(-0.5f, 0.5f) * size.x, 0f, Random.Range(-0.5f, 0.5f) * size.y);
            if (new Vector2(p.x, p.z).magnitude >= clearRadius) return p;
        }
        return transform.position;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.75f, 1f, 0.35f, 0.7f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 2f, size.y));
    }
}

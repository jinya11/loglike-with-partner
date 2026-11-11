using UnityEngine;

/// <summary>
/// 반딧불 숲의 등불 (씬에 배치, 프리팹). 위치는 씬에서 자유롭게 옮김. 켜짐/꺼짐 연출에 쓰는 부품 참조만 가짐 — 규칙은 ForestFireflyVariant.
/// 에디터에서 선택하면 안전지대 반경을 원으로 표시.
/// </summary>
public class VariantLantern : MonoBehaviour
{
    public Light lanternLight;
    public Renderer glass;
    public LineRenderer safeRing;
    [Tooltip("미리보기용 안전지대 반경 (실제 값은 VariantConfig.firefly.safeRadius)")] public float previewRadius = 9f;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.45f, 0.6f);
        const int n = 40;
        Vector3 c = transform.position + Vector3.up * 0.1f, prev = c + new Vector3(previewRadius, 0f, 0f);
        for (int i = 1; i <= n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * previewRadius;
            Gizmos.DrawLine(prev, p); prev = p;
        }
    }
}


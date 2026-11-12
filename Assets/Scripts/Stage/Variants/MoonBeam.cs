using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 달빛 묘역의 달빛 구역 하나 (씬 배치, 프리팹 MoonBeam): 하늘에서 내리비추는 빛 + 바닥의 둥근 달빛 웅덩이.
/// 천천히 옮겨 다니는 규칙과 회복은 MoonlitGraveyardVariant. 씬에 놓인 자리에서 출발.
/// </summary>
public class MoonBeam : MonoBehaviour
{
    public static readonly List<MoonBeam> All = new List<MoonBeam>();

    [Tooltip("바닥 달빛 웅덩이 (반경에 맞춰 크기 조절)")] public Transform pool;
    [Tooltip("하늘에서 비추는 빛")] public Light beam;
    [Tooltip("빛 기둥 (반투명, 하늘까지)")] public Transform shaft;
    [Tooltip("바닥 경계 고리 (지형을 따라 그림)")] public LineRenderer ring;

    [System.NonSerialized] public Vector3 target;
    [System.NonSerialized] public float wait;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>반경에 맞춰 웅덩이/빛 크기.</summary>
    public void Fit(float radius)
    {
        ringRadius = radius;
        if (pool) pool.localScale = new Vector3(radius * 2f, pool.localScale.y, radius * 2f);
        if (shaft) shaft.localScale = new Vector3(radius * 1.2f, shaft.localScale.y, radius * 1.2f);
        if (beam && beam.type == LightType.Spot)
        {
            float h = Mathf.Max(1f, beam.transform.localPosition.y);
            beam.spotAngle = Mathf.Clamp(2f * Mathf.Atan(radius * 1.15f / h) * Mathf.Rad2Deg, 5f, 170f);
            beam.range = h * 2.2f;
        }
    }

    /// <summary>지면 높이를 따라 놓음 (웅덩이가 언덕에 묻히지 않게 살짝 띄움).</summary>
    public void PlaceAt(Vector3 p)
    {
        p.y = Ground.HeightAt(p);
        transform.position = p;
        DrawRing();
    }

    float ringRadius = 7f;
    readonly Vector3[] pts = new Vector3[48];
    void DrawRing()
    {
        if (!ring) return;
        ring.useWorldSpace = true; ring.loop = true; ring.positionCount = pts.Length;
        Vector3 c = transform.position;
        for (int i = 0; i < pts.Length; i++)
        {
            float a = i * Mathf.PI * 2f / pts.Length;
            float x = c.x + Mathf.Cos(a) * ringRadius, z = c.z + Mathf.Sin(a) * ringRadius;
            pts[i] = new Vector3(x, Ground.HeightAt(x, z) + 0.08f, z);
        }
        ring.SetPositions(pts);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>원형 아레나 내부에 프리셋을 StageLayout 규칙대로 랜덤 배치. 바닥/벽은 씬의 ProBuilder 메시.</summary>
public class StageGenerator : MonoBehaviour
{
    [Tooltip("게임플레이 반경 (ProBuilder 바닥/벽 안쪽 반경과 맞출 것)")]
    [SerializeField] float radius = 45f;
    [SerializeField] StageLayout layout;
    public float Radius => radius;
    readonly List<Vector4> placed = new List<Vector4>(64); // xyz=위치, w=간격

    public void Build()
    {
        if (layout == null || layout.entries == null) return;

        var rng = layout.seed != 0 ? new System.Random(layout.seed) : new System.Random();
        placed.Clear();
        float safeSq = layout.centerSafeRadius * layout.centerSafeRadius;

        foreach (var e in layout.entries)
        {
            if (e.prefab == null) continue;
            int count = rng.Next(e.minCount, Mathf.Max(e.minCount, e.maxCount) + 1);
            float r0 = e.minRadius * radius, r1 = e.maxRadius * radius;
            for (int k = 0; k < count; k++)
            {
                for (int a = 0; a < layout.maxAttempts; a++)
                {
                    // 면적 균등 분포
                    float r = Mathf.Sqrt(Mathf.Lerp(r0 * r0, r1 * r1, (float)rng.NextDouble()));
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    var p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    if (p.sqrMagnitude < safeSq || Overlaps(p, e.spacing)) continue;

                    var rot = e.randomRotation ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
                    var go = Instantiate(e.prefab, p, rot, transform);
                    go.transform.localScale *= Mathf.Lerp(e.scaleRange.x, e.scaleRange.y, (float)rng.NextDouble());
                    placed.Add(new Vector4(p.x, 0f, p.z, e.spacing));
                    break;
                }
            }
        }
    }

    bool Overlaps(Vector3 p, float spacing)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            var o = placed[i];
            float s = Mathf.Max(spacing, o.w);
            float dx = p.x - o.x, dz = p.z - o.z;
            if (dx * dx + dz * dz < s * s) return true;
        }
        return false;
    }
}

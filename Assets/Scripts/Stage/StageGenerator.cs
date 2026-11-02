using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아레나 내부에 프리셋을 StageLayout 규칙대로 랜덤 배치. 바닥/벽은 씬의 ProBuilder 메시.
/// border가 있으면 열린 구역마다 그 구역 안에만 배치 (minRadius/maxRadius = 구역 중심~구역 경계 사이 사각형 고리 비율).
/// 보더를 열어 새 구역이 생기면 BuildArea로 그 구역에도 같은 규칙으로 배치.
/// </summary>
public class StageGenerator : MonoBehaviour
{
    [Tooltip("게임플레이 반경 (원형 스테이지: ProBuilder 바닥/벽 안쪽 반경과 맞출 것)")]
    [SerializeField] float radius = 45f;
    [Tooltip("보더 (지역 스테이지). 있으면 radius 대신 열린 구역들 사용")]
    [SerializeField] ArenaBorder border;
    [Tooltip("보더 안쪽으로 비워 둘 거리")]
    [SerializeField] float borderMargin = 2.5f;
    [SerializeField] StageLayout layout;
    public float Radius => border ? border.HalfSize : radius;
    public ArenaBorder Border => border;
    readonly List<Vector4> placed = new List<Vector4>(64); // xyz=위치, w=간격
    System.Random rng;

    public void Build()
    {
        if (layout == null || layout.entries == null) return;
        rng = layout.seed != 0 ? new System.Random(layout.seed) : new System.Random();
        placed.Clear();
        if (!border) { Place(Vector3.zero, new Vector2(radius, radius), false, true, 1f); return; }
        for (int i = 0; i < border.Areas.Count; i++) BuildArea(border.Areas[i], i == 0);
    }

    /// <summary>구역 하나에 배치. 수량은 중앙 정사각형 대비 넓이에 비례 (중앙만 시작 지점을 비움).</summary>
    public void BuildArea(Rect r, bool isCenter)
    {
        if (layout == null || layout.entries == null || !border) return;
        rng ??= new System.Random();
        float baseArea = border.HalfSize * border.HalfSize * 4f;
        Place(new Vector3(r.center.x, 0f, r.center.y), new Vector2(r.width * 0.5f - borderMargin, r.height * 0.5f - borderMargin), true, isCenter, r.width * r.height / baseArea);
    }

    /// <summary>size = 반 변 (x, z). countMul = 수량 배율.</summary>
    void Place(Vector3 center, Vector2 size, bool square, bool keepCenterSafe, float countMul)
    {
        float safeSq = keepCenterSafe ? layout.centerSafeRadius * layout.centerSafeRadius : 0f;
        foreach (var e in layout.entries)
        {
            if (e.prefab == null) continue;
            int count = Mathf.RoundToInt(rng.Next(e.minCount, Mathf.Max(e.minCount, e.maxCount) + 1) * countMul);
            float r0 = e.minRadius, r1 = e.maxRadius; // 반 변 대비 비율
            for (int k = 0; k < count; k++)
            {
                for (int a = 0; a < layout.maxAttempts; a++)
                {
                    var off = square ? SquarePoint(rng, r0, r1) : CirclePoint(rng, r0, r1);
                    off.x *= size.x; off.z *= size.y;
                    var p = center + off;
                    if (off.sqrMagnitude < safeSq || Overlaps(p, e.spacing)) continue;

                    var rot = e.randomRotation ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) : Quaternion.identity;
                    var go = Instantiate(e.prefab, p, rot, transform);
                    go.transform.localScale *= Mathf.Lerp(e.scaleRange.x, e.scaleRange.y, (float)rng.NextDouble());
                    placed.Add(new Vector4(p.x, 0f, p.z, e.spacing));
                    break;
                }
            }
        }
    }

    /// <summary>면적 균등 분포 (원형 고리).</summary>
    static Vector3 CirclePoint(System.Random rng, float r0, float r1)
    {
        float r = Mathf.Sqrt(Mathf.Lerp(r0 * r0, r1 * r1, (float)rng.NextDouble()));
        float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        return new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
    }

    /// <summary>면적 균등 분포 (정사각형 고리: max(|x|,|z|)가 r0~r1).</summary>
    static Vector3 SquarePoint(System.Random rng, float r0, float r1)
    {
        for (int i = 0; i < 16; i++)
        {
            float x = Mathf.Lerp(-r1, r1, (float)rng.NextDouble()), z = Mathf.Lerp(-r1, r1, (float)rng.NextDouble());
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) >= r0) return new Vector3(x, 0f, z);
        }
        return new Vector3(r1, 0f, 0f);
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

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 스테이지 지면 높이 (고저차). 언덕(원형 고원 + 완만한 경사로)들의 합으로 정의되는 해석적 높이장.
/// 콜라이더 없이 높이를 계산하므로 적 수백 마리가 매 프레임 조회해도 저렴 (레이캐스트 불필요).
/// 플레이어(조준점)·작은 생명체·적·투사체·보석은 이동할 때 HeightAt으로 Y를 보정.
/// 언덕은 StageTerrain이 스테이지 시작 시 만들고, 스테이지가 끝나면(StageDirector 파괴) 비움.
/// </summary>
public static class Ground
{
    struct Hill
    {
        public float x, z, outer, inner, height;
    }

    static readonly List<Hill> hills = new List<Hill>(32);

    /// <summary>언덕이 하나라도 있는지 (평지면 모든 높이 = 0).</summary>
    public static bool HasHills => hills.Count > 0;
    public static int HillCount => hills.Count;

    public static void Clear() => hills.Clear();

    /// <summary>
    /// 언덕 추가. radius = 바깥 반경(경사 끝), plateau = 꼭대기 평평한 부분 비율(0~1), height = 높이.
    /// 겹치는 언덕은 더 높은 쪽(max)을 따름 → 작은 언덕을 큰 언덕 위에 올리면 계단식 단차.
    /// </summary>
    public static void Add(Vector2 center, float radius, float plateau, float height)
    {
        float inner = Mathf.Clamp01(plateau) * radius;
        hills.Add(new Hill { x = center.x, z = center.y, outer = radius, inner = Mathf.Min(inner, radius - 0.5f), height = height });
    }

    public static float HeightAt(Vector3 p) => HeightAt(p.x, p.z);

    public static float HeightAt(float x, float z)
    {
        float h = 0f;
        for (int i = 0; i < hills.Count; i++)
        {
            var hl = hills[i];
            float dx = x - hl.x, dz = z - hl.z, sq = dx * dx + dz * dz;
            if (sq >= hl.outer * hl.outer) continue;
            float d = Mathf.Sqrt(sq);
            float t = d <= hl.inner ? 1f : 1f - (d - hl.inner) / (hl.outer - hl.inner);
            float v = hl.height * t * t * (3f - 2f * t); // smoothstep 경사
            if (v > h) h = v;
        }
        return h;
    }

    /// <summary>XZ는 그대로, Y만 지면 높이 + offset.</summary>
    public static Vector3 Snap(Vector3 p, float offset = 0f) { p.y = HeightAt(p.x, p.z) + offset; return p; }

    /// <summary>경사 (높이 변화율, 0 = 평지). 길찾기 비용용.</summary>
    public static float Slope(float x, float z)
    {
        if (hills.Count == 0) return 0f;
        const float e = 0.5f;
        float gx = HeightAt(x + e, z) - HeightAt(x - e, z), gz = HeightAt(x, z + e) - HeightAt(x, z - e);
        return Mathf.Sqrt(gx * gx + gz * gz) / (2f * e);
    }

    /// <summary>XZ 원(center, radius)이 어느 언덕 바깥 반경과 겹치는지 (배치 판정용).</summary>
    public static bool Overlaps(Vector2 center, float radius)
    {
        for (int i = 0; i < hills.Count; i++)
        {
            float dx = center.x - hills[i].x, dz = center.y - hills[i].z, r = radius + hills[i].outer;
            if (dx * dx + dz * dz < r * r) return true;
        }
        return false;
    }
}

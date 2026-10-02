using UnityEngine;

/// <summary>현재 씬의 원형 경계. 로비/스테이지 디렉터가 Radius를 설정.</summary>
public static class Arena
{
    public static float Radius = 30f;

    /// <summary>XZ만 원 안으로 제한 (y 유지).</summary>
    public static Vector3 Clamp(Vector3 p)
    {
        float r = Radius - 0.6f;
        float sq = p.x * p.x + p.z * p.z;
        if (sq <= r * r) return p;
        float k = r / Mathf.Sqrt(sq);
        p.x *= k; p.z *= k;
        return p;
    }
}

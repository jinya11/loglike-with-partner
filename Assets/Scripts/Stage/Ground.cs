using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 스테이지 지면 높이 (고저차). 평평한 원형 고원 + 수직 바위 절벽 + 고원마다 1~2개의 경사로.
/// 고원 위에 더 작은 고원을 올리면 계단식 단차 (위 단의 경사로는 아래 고원 위로 내려옴).
/// 콜라이더 없이 해석적으로 높이를 계산 → 적 수백 마리가 매 프레임 조회해도 저렴 (레이캐스트 불필요).
/// 절벽은 오를 수 없음 (StepBlocked / ClampMove): 적과 작은 생명체는 경사로로만 올라감. 내려가는 건 가능.
/// 감시자(형태 없는 조준점)는 절벽을 무시하고 높이만 따라감.
/// StageTerrain이 스테이지 시작 시 만들고, 스테이지가 끝나면(StageDirector 파괴) 비움.
/// </summary>
public static class Ground
{
    /// <summary>고원 하나. 경사로: (방향 x, 방향 z, 길이, 폭). 경사로는 반경 끝에서 바깥으로 baseHeight까지 내려감.</summary>
    public sealed class Plateau
    {
        public Vector2 center;
        public float radius, height, baseHeight;
        public readonly List<Vector4> ramps = new List<Vector4>(2);
        /// <summary>경사로까지 포함한 바깥 반경 (배치 겹침 판정용).</summary>
        public float Reach { get { float m = 0f; foreach (var r in ramps) m = Mathf.Max(m, r.z); return radius + m; } }
    }

    static readonly List<Plateau> plateaus = new List<Plateau>(32);

    public static bool HasHills => plateaus.Count > 0;
    public static int HillCount => plateaus.Count;
    public static IReadOnlyList<Plateau> Plateaus => plateaus;
    /// <summary>한 번에 오를 수 있는 최대 높이 차 (경사로는 통과, 절벽은 막힘). StageTerrain이 설정.</summary>
    public static float MaxStep = 0.5f;

    public static void Clear() => plateaus.Clear();
    public static void Remove(Plateau p) => plateaus.Remove(p);

    public static Plateau Add(Vector2 center, float radius, float height, float baseHeight = 0f)
    {
        var p = new Plateau { center = center, radius = radius, height = height, baseHeight = baseHeight };
        plateaus.Add(p);
        return p;
    }

    public static float HeightAt(Vector3 p) => HeightAt(p.x, p.z);

    public static float HeightAt(float x, float z)
    {
        float h = 0f;
        for (int i = 0; i < plateaus.Count; i++)
        {
            var pl = plateaus[i];
            if (pl.height <= h) continue;
            float dx = x - pl.center.x, dz = z - pl.center.y, sq = dx * dx + dz * dz;
            if (sq <= pl.radius * pl.radius) { h = pl.height; continue; }
            for (int k = 0; k < pl.ramps.Count; k++)
            {
                var r = pl.ramps[k];
                float along = dx * r.x + dz * r.y;
                if (along <= 0f || along >= pl.radius + r.z) continue;
                float perp = Mathf.Abs(dx * r.y - dz * r.x);
                if (perp > r.w * 0.5f) continue;
                float t = Mathf.Clamp01((pl.radius + r.z - along) / r.z);
                float v = pl.baseHeight + (pl.height - pl.baseHeight) * t;
                if (v > h) h = v;
            }
        }
        return h;
    }

    /// <summary>XZ는 그대로, Y만 지면 높이 + offset.</summary>
    public static Vector3 Snap(Vector3 p, float offset = 0f) { p.y = HeightAt(p.x, p.z) + offset; return p; }

    /// <summary>경사 (높이 변화율, 0 = 평지, 절벽은 매우 큼). 길찾기 비용용.</summary>
    public static float Slope(float x, float z)
    {
        if (plateaus.Count == 0) return 0f;
        const float e = 0.3f;
        float gx = HeightAt(x + e, z) - HeightAt(x - e, z), gz = HeightAt(x, z + e) - HeightAt(x, z - e);
        return Mathf.Sqrt(gx * gx + gz * gz) / (2f * e);
    }

    /// <summary>from → to로 가면 절벽을 오르게 되는지 (내려가는 건 허용).</summary>
    public static bool StepBlocked(Vector3 from, Vector3 to)
        => plateaus.Count > 0 && HeightAt(to.x, to.z) - HeightAt(from.x, from.z) > MaxStep;

    /// <summary>
    /// 수평 이동량을 절벽에 막히게 조정: 그대로 못 가면 x만/z만(벽 따라 미끄러짐), 그것도 안 되면 0.
    /// </summary>
    public static Vector3 ClampMove(Vector3 pos, Vector3 delta)
    {
        if (plateaus.Count == 0) return delta;
        if (!StepBlocked(pos, pos + delta)) return delta;
        var dx = new Vector3(delta.x, 0f, 0f);
        var dz = new Vector3(0f, 0f, delta.z);
        bool okX = !StepBlocked(pos, pos + dx), okZ = !StepBlocked(pos, pos + dz);
        if (okX && okZ) return Mathf.Abs(delta.x) > Mathf.Abs(delta.z) ? dx : dz;
        if (okX) return dx;
        if (okZ) return dz;
        return Vector3.zero;
    }

    /// <summary>
    /// 이 원(중심 x,z / 반경 r) 안에 절벽(오를 수 없는 단차)이 있는지. 길찾기 격자의 벽 판정용.
    /// 0.4 간격 표본의 이웃 높이 차로 판정 (경사로는 표본 간 차이가 작아 통과).
    /// </summary>
    public static bool HasCliff(float x, float z, float r)
    {
        if (plateaus.Count == 0) return false;
        const float step = 0.4f;
        int n = Mathf.Max(1, Mathf.CeilToInt(r * 2f / step));
        float s = r * 2f / n, x0 = x - r, z0 = z - r;
        for (int j = 0; j <= n; j++)
        {
            float prev = HeightAt(x0, z0 + j * s);
            for (int i = 1; i <= n; i++)
            {
                float h = HeightAt(x0 + i * s, z0 + j * s);
                if (Mathf.Abs(h - prev) > MaxStep) return true;
                prev = h;
            }
        }
        for (int i = 0; i <= n; i++)
        {
            float prev = HeightAt(x0 + i * s, z0);
            for (int j = 1; j <= n; j++)
            {
                float h = HeightAt(x0 + i * s, z0 + j * s);
                if (Mathf.Abs(h - prev) > MaxStep) return true;
                prev = h;
            }
        }
        return false;
    }

    /// <summary>XZ 원(center, radius)이 어느 고원(경사로 포함)과 겹치는지 (배치 판정용).</summary>
    public static bool Overlaps(Vector2 center, float radius)
    {
        for (int i = 0; i < plateaus.Count; i++)
        {
            float r = radius + plateaus[i].Reach;
            if ((center - plateaus[i].center).sqrMagnitude < r * r) return true;
        }
        return false;
    }

    /// <summary>절벽 띠 또는 경사로 위인지 (장애물을 치울 자리). margin = 여유 거리.</summary>
    public static bool OnFeature(Vector3 p, float margin)
    {
        for (int i = 0; i < plateaus.Count; i++)
        {
            var pl = plateaus[i];
            float dx = p.x - pl.center.x, dz = p.z - pl.center.y, d = Mathf.Sqrt(dx * dx + dz * dz);
            if (Mathf.Abs(d - pl.radius) < margin) return true;
            foreach (var r in pl.ramps)
            {
                float along = dx * r.x + dz * r.y, perp = Mathf.Abs(dx * r.y - dz * r.x);
                if (along > pl.radius - margin && along < pl.radius + r.z + margin && perp < r.w * 0.5f + margin) return true;
            }
        }
        return false;
    }
}

using UnityEngine;

/// <summary>
/// 전투 스테이지 지면 높이 = 씬의 Unity Terrain (심연 외 지역 씬에 배치, Terrain 툴로 직접 올리고 내려서 편집).
/// Terrain이 없는 씬(심연/보너스/로비)은 평지(높이 0).
/// 플레이어(조준점)·작은 생명체·적·투사체·보석·포털은 HeightAt(Terrain.SampleHeight)으로 Y를 맞춤.
/// 너무 가파른 경사(maxWalkSlope 초과)는 오르지 못함 (StepBlocked/ClampMove) — 감시자는 예외.
/// 지형 충돌은 쓰지 않음 (TerrainCollider는 런타임에 끔: 적 Rigidbody가 경사에 걸리지 않게).
/// </summary>
public static class Ground
{
    static Terrain terrain;
    static TerrainData data;
    static Vector3 origin, size;
    static float maxSlopeTan = 0.84f; // tan(40도)

    /// <summary>높이를 가진 지형이 있는지.</summary>
    public static bool HasTerrain => terrain != null;
    public static Terrain Terrain => terrain;
    /// <summary>오를 수 있는 최대 경사 (도).</summary>
    public static float MaxSlope { get; private set; } = 40f;

    public static void SetTerrain(Terrain t, float maxSlopeDeg)
    {
        terrain = t; runtimeCopy = false;
        data = t ? t.terrainData : null;
        if (!data) { terrain = null; return; }
        origin = t.transform.position;
        size = data.size;
        MaxSlope = Mathf.Clamp(maxSlopeDeg, 5f, 85f);
        maxSlopeTan = Mathf.Tan(MaxSlope * Mathf.Deg2Rad);
    }

    public static void Clear() { terrain = null; data = null; runtimeCopy = false; }

    static bool runtimeCopy;
    /// <summary>이미 런타임 복제본(TerrainData)을 쓰고 있다고 표시 (원본 에셋을 바꾸지 않도록).</summary>
    public static void MarkRuntimeCopy() => runtimeCopy = true;

    /// <summary>
    /// 상자 영역(중심 p, 회전 rot, 반 변 half) 아래 지형을 바닥 높이로 평평하게 깎음 + 가장자리 margin만큼 완만하게.
    /// 긴 강줄기처럼 피할 수 없는 장판용. 원본 TerrainData는 건드리지 않고 런타임 복제본에서만.
    /// </summary>
    public static void FlattenBox(Vector3 p, Quaternion rot, Vector2 half, float margin)
    {
        if (!terrain) return;
        if (!runtimeCopy)
        {
            data = Object.Instantiate(data);
            terrain.terrainData = data;
            if (terrain.TryGetComponent(out TerrainCollider tc)) tc.terrainData = data;
            runtimeCopy = true;
        }
        int res = data.heightmapResolution;
        var h = data.GetHeights(0, 0, res, res);
        var inv = Quaternion.Inverse(rot);
        float target = Mathf.Clamp01((p.y - origin.y) / size.y);
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                Vector3 w = new Vector3(origin.x + x / (float)(res - 1) * size.x, 0f, origin.z + z / (float)(res - 1) * size.z);
                Vector3 l = inv * (w - new Vector3(p.x, 0f, p.z));
                float d = Mathf.Max(Mathf.Abs(l.x) - half.x, Mathf.Abs(l.z) - half.y, 0f);
                if (d >= margin) continue;
                float k = d / margin; k = k * k * (3f - 2f * k);
                h[z, x] = Mathf.Lerp(target, h[z, x], k);
            }
        data.SetHeights(0, 0, h);
    }

    public static float HeightAt(Vector3 p) => HeightAt(p.x, p.z);

    public static float HeightAt(float x, float z)
    {
        if (!terrain) return 0f;
        float nx = (x - origin.x) / size.x, nz = (z - origin.z) / size.z;
        if (nx < 0f || nz < 0f || nx > 1f || nz > 1f) return 0f;
        return data.GetInterpolatedHeight(nx, nz) + origin.y;
    }

    /// <summary>XZ는 그대로, Y만 지면 높이 + offset.</summary>
    public static Vector3 Snap(Vector3 p, float offset = 0f) { p.y = HeightAt(p.x, p.z) + offset; return p; }

    /// <summary>경사 (도). 지형 밖/평지는 0.</summary>
    public static float SteepnessAt(float x, float z)
    {
        if (!terrain) return 0f;
        float nx = (x - origin.x) / size.x, nz = (z - origin.z) / size.z;
        if (nx < 0f || nz < 0f || nx > 1f || nz > 1f) return 0f;
        return data.GetSteepness(nx, nz);
    }

    /// <summary>
    /// 원(중심 p, 반경 r) 안이 평평한지: 높이 편차 ≤ tolerance, 경사 ≤ maxSlope(도). 독 웅덩이/용암 같은 바닥 장판 배치용.
    /// 지형이 없으면 항상 평평.
    /// </summary>
    public static bool IsFlat(Vector3 p, float r, float tolerance, float maxSlope)
    {
        if (!terrain) return true;
        float min = float.MaxValue, max = float.MinValue;
        for (int ring = 0; ring <= 2; ring++)
        {
            float rr = r * ring * 0.5f;
            int n = ring == 0 ? 1 : 8 * ring;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float x = p.x + Mathf.Cos(a) * rr, z = p.z + Mathf.Sin(a) * rr;
                float h = HeightAt(x, z);
                if (h < min) min = h;
                if (h > max) max = h;
                if (max - min > tolerance || SteepnessAt(x, z) > maxSlope) return false;
            }
        }
        return true;
    }

    /// <summary>오를 수 없을 만큼 가파른지 (길찾기 격자 벽 판정).</summary>
    public static bool TooSteep(float x, float z) => SteepnessAt(x, z) > MaxSlope;

    /// <summary>from → to로 가면 너무 가파른 오르막인지 (내려가는 건 허용).</summary>
    public static bool StepBlocked(Vector3 from, Vector3 to)
    {
        if (!terrain) return false;
        float dx = to.x - from.x, dz = to.z - from.z, d = Mathf.Sqrt(dx * dx + dz * dz);
        if (d < 0.0001f) return false;
        return (HeightAt(to.x, to.z) - HeightAt(from.x, from.z)) / d > maxSlopeTan;
    }

    /// <summary>수평 이동량을 가파른 오르막에 막히게 조정: x만/z만(비탈을 따라 미끄러짐), 그것도 안 되면 0.</summary>
    public static Vector3 ClampMove(Vector3 pos, Vector3 delta)
    {
        if (!terrain) return delta;
        // 한 프레임 이동은 짧으므로 0.4m 앞을 보고 판정
        float m = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
        if (m < 0.0001f) return delta;
        float k = Mathf.Max(1f, 0.4f / m);
        if (!StepBlocked(pos, pos + delta * k)) return delta;
        var dx = new Vector3(delta.x, 0f, 0f);
        var dz = new Vector3(0f, 0f, delta.z);
        bool okX = Mathf.Abs(delta.x) > 0.0001f && !StepBlocked(pos, pos + dx * k);
        bool okZ = Mathf.Abs(delta.z) > 0.0001f && !StepBlocked(pos, pos + dz * k);
        if (okX && okZ) return Mathf.Abs(delta.x) > Mathf.Abs(delta.z) ? dx : dz;
        if (okX) return dx;
        if (okZ) return dz;
        return Vector3.zero;
    }
}

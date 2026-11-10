using UnityEngine;

/// <summary>
/// Unity Terrain 하이트맵에 완만한 언덕/구릉을 채우는 생성기.
/// 에디터 메뉴(Roguelike/Setup Region Terrains)가 지역 씬의 기본 지형을 만들 때 사용하고,
/// StageConfig.ground.randomizeAtRuntime을 켜면 런타임에 TerrainData 복제본에 매번 새로 생성 (원본 에셋/손으로 고친 지형은 그대로).
/// </summary>
[System.Serializable]
public class TerrainHillSettings
{
    [Tooltip("언덕이 차지하는 비율 (0~1, 클수록 언덕이 많음)")] [Range(0, 1)] public float coverage = 0.5f;
    [Tooltip("언덕 최대 높이 (m)")] public float hillHeight = 5f;
    [Tooltip("언덕 크기 (노이즈 주기, 작을수록 큰 언덕)")] public float scale = 0.022f;
    [Tooltip("언덕 위 자잘한 기복 (m)")] public float detail = 0.6f;
    [Tooltip("중앙(시작 지점) 평지 반경")] public float centerFlatRadius = 14f;
    [Tooltip("경사가 이 각도 이상인 곳은 바위 레이어로 칠함")] public float rockSlope = 24f;
}

public static class TerrainHills
{
    /// <summary>td의 하이트맵을 언덕으로 채우고 경사에 따라 풀/바위 레이어를 칠함. center = 월드 기준 중앙 (평지로 비움).</summary>
    public static void Generate(TerrainData td, Vector3 terrainPos, TerrainHillSettings s, int seed)
    {
        int res = td.heightmapResolution;
        var h = new float[res, res];
        var rng = new System.Random(seed);
        float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;
        Vector3 size = td.size;
        float thr = 1f - Mathf.Clamp01(s.coverage);
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = terrainPos.x + x / (float)(res - 1) * size.x, wz = terrainPos.z + z / (float)(res - 1) * size.z;
                // 큰 언덕 (fbm) → 임계값 위만 솟아오르게 (나머지는 평지/골짜기)
                float n = 0.65f * Mathf.PerlinNoise(ox + wx * s.scale, oz + wz * s.scale)
                        + 0.35f * Mathf.PerlinNoise(ox * 2f + wx * s.scale * 2.3f, oz * 2f + wz * s.scale * 2.3f);
                float t = Mathf.Clamp01((n - thr * 0.75f) / 0.3f);
                float hill = t * t * (3f - 2f * t) * s.hillHeight;
                float det = (Mathf.PerlinNoise(ox + wx * 0.12f, oz + wz * 0.12f) - 0.5f) * s.detail * Mathf.Clamp01(hill);
                // 중앙 평지
                float dc = Mathf.Sqrt(wx * wx + wz * wz);
                float flat = Mathf.Clamp01((dc - s.centerFlatRadius) / 10f);
                flat = flat * flat * (3f - 2f * flat);
                h[z, x] = Mathf.Clamp01(Mathf.Max(0f, hill + det) * flat / size.y);
            }
        td.SetHeights(0, 0, h);
        PaintLayers(td, s.rockSlope);
    }

    /// <summary>레이어 0 = 풀, 레이어 1 = 바위 (경사 기준). 레이어가 2개 미만이면 건너뜀.</summary>
    public static void PaintLayers(TerrainData td, float rockSlope)
    {
        if (td.terrainLayers == null || td.terrainLayers.Length < 2) return;
        int ar = td.alphamapResolution, layers = td.alphamapLayers;
        var a = new float[ar, ar, layers];
        for (int z = 0; z < ar; z++)
            for (int x = 0; x < ar; x++)
            {
                float st = td.GetSteepness(x / (float)(ar - 1), z / (float)(ar - 1));
                float rock = Mathf.Clamp01((st - rockSlope) / 10f);
                a[z, x, 0] = 1f - rock;
                a[z, x, 1] = rock;
            }
        td.SetAlphamaps(0, 0, a);
    }
}

/// <summary>지면(Terrain) 관련 설정 (StageConfig.ground).</summary>
[System.Serializable]
public class GroundSettings
{
    [Tooltip("이보다 가파른 오르막은 작은 생명체/적이 못 오름 (도). 감시자는 예외")] [Range(5, 85)] public float maxWalkSlope = 40f;
    [Tooltip("켜면 매 스테이지 시작 시 Terrain 복제본에 새 언덕을 랜덤 생성 (원본 에셋/손으로 고친 지형은 건드리지 않음). 끄면 씬에 만든 지형 그대로")]
    public bool randomizeAtRuntime = false;
    [Header("바닥 장판 (독 웅덩이/용암/물/진흙 등)")]
    [Tooltip("장판은 평지에만: 장판 범위 안 높이 편차가 이 값(m) 이하")] public float zoneFlatTolerance = 0.2f;
    [Tooltip("장판은 평지에만: 경사가 이 각도(도) 이하")] [Range(0, 45)] public float zoneMaxSlope = 6f;
    [Tooltip("언덕 생성 설정 (에디터 메뉴 기본 지형 + 런타임 랜덤 생성)")] public TerrainHillSettings hills = new TerrainHillSettings();
}

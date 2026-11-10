using System.Collections.Generic;
using UnityEngine;

/// <summary>고저차 지형 생성 설정 (StageConfig.terrain). 심연 외 전투 스테이지에서 사용.</summary>
[System.Serializable]
public class TerrainSettings
{
    [Tooltip("고저차 지형 사용 (끄면 평지)")] public bool enabled = true;
    [Tooltip("언덕 수 (최소, 최대). 넓이 120x120 기준")] public Vector2Int hillCount = new Vector2Int(5, 8);
    [Tooltip("언덕 바깥 반경 (최소, 최대)")] public Vector2 hillRadius = new Vector2(7f, 13f);
    [Tooltip("꼭대기 평평한 부분 비율 (최소, 최대)")] public Vector2 plateau = new Vector2(0.35f, 0.6f);
    [Tooltip("언덕 높이 (최소, 최대)")] public Vector2 hillHeight = new Vector2(1.5f, 3.5f);
    [Tooltip("언덕 위에 한 단 더 쌓아 계단식 단차를 만들 확률")] [Range(0, 1)] public float terraceChance = 0.35f;
    [Tooltip("위 단의 추가 높이 (최소, 최대)")] public Vector2 terraceHeight = new Vector2(1f, 2f);
    [Tooltip("경사로 길이 ≥ 높이 × 이 값 (클수록 완만, 2.4 ≈ 최대 경사 32도)")] public float rampRatio = 2.4f;
    [Tooltip("시작 지점(중앙) 주변 평지 반경")] public float centerSafeRadius = 12f;
    [Tooltip("보더 안쪽 여백")] public float edgeMargin = 4f;
    [Tooltip("지형 메시 격자 크기")] public float cellSize = 1f;
    [Tooltip("지형 색 (바닥 재질 색에 곱함)")] public Color tint = new Color(1.12f, 1.15f, 1.05f);
}

/// <summary>
/// 언덕/단차 지형을 만들고(Ground에 등록) 보이는 메시를 생성. 콜라이더는 없음 — 높이는 Ground.HeightAt으로 계산.
/// 평지 부분 메시는 바닥 아래로 내려 숨김 (z-fighting 방지).
/// </summary>
public static class StageTerrain
{
    static readonly List<Vector4> zoneBuf = new List<Vector4>(32);

    /// <summary>언덕 배치 + 메시 생성. countMul/heightMul: 스테이지 특징(고지대)용 배율.</summary>
    public static void Build(TerrainSettings s, Transform parent, float countMul = 1f, float heightMul = 1f)
    {
        Ground.Clear();
        if (s == null || !s.enabled) return;

        Rect b = Arena.Square ? Arena.Bounds : new Rect(-Arena.Radius, -Arena.Radius, Arena.Radius * 2f, Arena.Radius * 2f);
        float areaMul = b.width * b.height / (120f * 120f);
        int count = Mathf.RoundToInt(Random.Range(s.hillCount.x, s.hillCount.y + 1) * areaMul * countMul);

        // 바닥 구역(물/용암 등)은 피함
        zoneBuf.Clear();
        foreach (var z in StageZone.All)
        {
            var sc = z.transform.lossyScale;
            zoneBuf.Add(new Vector4(z.transform.position.x, z.transform.position.z, Mathf.Max(sc.x, sc.z) * 0.5f + 1.5f, 0f));
        }

        int placed = 0;
        for (int attempt = 0; attempt < count * 25 && placed < count; attempt++)
        {
            float h = Random.Range(s.hillHeight.x, s.hillHeight.y) * heightMul;
            float r = Random.Range(s.hillRadius.x, s.hillRadius.y);
            float pl = Random.Range(s.plateau.x, s.plateau.y);
            // 경사가 너무 가파르지 않게: 경사로 길이 = r × (1 - plateau) ≥ h × rampRatio
            float minRamp = h * s.rampRatio;
            if (r * (1f - pl) < minRamp) { r = Mathf.Max(r, minRamp / Mathf.Max(0.1f, 1f - pl)); }
            float m = s.edgeMargin + r;
            if (b.width < m * 2f || b.height < m * 2f) continue;
            var c = new Vector2(Random.Range(b.xMin + m, b.xMax - m), Random.Range(b.yMin + m, b.yMax - m));
            if (!Arena.Contains(new Vector3(c.x, 0f, c.y), m)) continue;
            if (c.magnitude < s.centerSafeRadius + r) continue;
            if (Ground.Overlaps(c, r * 0.35f)) continue; // 살짝 겹치는 건 허용 (능선)
            bool hitZone = false;
            foreach (var z in zoneBuf) if ((c - new Vector2(z.x, z.y)).sqrMagnitude < (z.z + r) * (z.z + r)) { hitZone = true; break; }
            if (hitZone) continue;

            Ground.Add(c, r, pl, h);
            placed++;
            // 계단식 단차: 꼭대기 평지 안에 한 단 더
            if (Random.value < s.terraceChance && pl > 0.3f)
            {
                float inner = r * pl;
                float h2 = Random.Range(s.terraceHeight.x, s.terraceHeight.y) * heightMul;
                float r2 = inner * Random.Range(0.75f, 0.95f);
                float pl2 = Mathf.Clamp(1f - (h + h2) * s.rampRatio * 0.6f / Mathf.Max(0.1f, r2), 0.15f, 0.6f);
                Vector2 off = Random.insideUnitCircle * Mathf.Max(0f, inner - r2);
                Ground.Add(c + off, r2, pl2, h + h2);
            }
        }
        if (Ground.HasHills) BuildMesh(s, b, parent);
    }

    static void BuildMesh(TerrainSettings s, Rect b, Transform parent)
    {
        float cell = Mathf.Max(0.5f, s.cellSize);
        int nx = Mathf.CeilToInt(b.width / cell) + 1, nz = Mathf.CeilToInt(b.height / cell) + 1;
        var verts = new Vector3[nx * nz];
        var uvs = new Vector2[nx * nz];
        var raised = new bool[nx * nz];
        for (int z = 0; z < nz; z++)
            for (int x = 0; x < nx; x++)
            {
                int i = z * nx + x;
                float wx = b.xMin + x * cell, wz = b.yMin + z * cell;
                float h = Ground.HeightAt(wx, wz);
                raised[i] = h > 0.001f;
                verts[i] = new Vector3(wx, raised[i] ? h : -0.05f, wz); // 평지는 바닥 아래로
                uvs[i] = new Vector2(wx * 0.1f, wz * 0.1f);
            }
        var tris = new List<int>(nx * nz * 2);
        for (int z = 0; z < nz - 1; z++)
            for (int x = 0; x < nx - 1; x++)
            {
                int a = z * nx + x, bb = a + 1, c = a + nx, d = c + 1;
                if (!raised[a] && !raised[bb] && !raised[c] && !raised[d]) continue;
                tris.Add(a); tris.Add(c); tris.Add(bb);
                tris.Add(bb); tris.Add(c); tris.Add(d);
            }
        var mesh = new Mesh { name = "StageTerrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts; mesh.uv = uvs; mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        var go = new GameObject("Terrain_Hills", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        var floor = GameObject.Find("Arena/Floor");
        Material src = floor && floor.TryGetComponent(out Renderer fr) ? fr.sharedMaterial : null;
        if (src)
        {
            var mat = new Material(src) { name = "TerrainHills" };
            if (mat.HasColor("_BaseColor")) mat.SetColor("_BaseColor", mat.GetColor("_BaseColor") * s.tint);
            mr.sharedMaterial = mat;
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
    }

    /// <summary>배치된 장애물/장식을 지면 높이로 올림 (언덕 위에 묻히지 않게).</summary>
    public static void LiftChildren(Transform root)
    {
        if (!Ground.HasHills || !root) return;
        foreach (Transform t in root)
        {
            if (t.GetComponent<StageZone>()) continue;
            var p = t.position;
            p.y += Ground.HeightAt(p.x, p.z);
            t.position = p;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>고저차 지형 생성 설정 (StageConfig.terrain). 심연 외 전투 스테이지에서 사용.</summary>
[System.Serializable]
public class TerrainSettings
{
    [Tooltip("고저차 지형 사용 (끄면 평지)")] public bool enabled = true;
    [Tooltip("고원 수 (최소, 최대). 넓이 120x120 기준")] public Vector2Int plateauCount = new Vector2Int(5, 7);
    [Tooltip("고원 반경 (최소, 최대)")] public Vector2 plateauRadius = new Vector2(6f, 11f);
    [Tooltip("고원 높이 = 절벽 높이 (최소, 최대)")] public Vector2 plateauHeight = new Vector2(1.6f, 3.2f);
    [Tooltip("고원마다 경사로 수 (최소, 최대)")] public Vector2Int rampCount = new Vector2Int(1, 2);
    [Tooltip("경사로 폭")] public float rampWidth = 4f;
    [Tooltip("경사로 길이 = 높이 × 이 값 (2.6 ≈ 21도)")] public float rampRatio = 2.6f;
    [Tooltip("고원 위에 한 단 더 쌓을 확률 (계단식 단차)")] [Range(0, 1)] public float terraceChance = 0.35f;
    [Tooltip("위 단의 추가 높이 (최소, 최대)")] public Vector2 terraceHeight = new Vector2(1.2f, 2f);
    [Tooltip("한 번에 오를 수 있는 높이 차. 이보다 높은 단차(절벽)는 적/생명체가 못 오름 (경사로로만)")] public float maxStep = 0.5f;
    [Tooltip("시작 지점(중앙) 주변 평지 반경")] public float centerSafeRadius = 12f;
    [Tooltip("보더 안쪽 여백")] public float edgeMargin = 4f;
    [Tooltip("고원끼리 최소 간격 (경사로 끝 포함)")] public float spacing = 4f;
    [Header("색 (어두운 지역에서도 구분되게)")]
    [Tooltip("고원 윗면 색: 바닥 색을 이만큼 밝게 (배율)")] public float topBrighten = 1.35f;
    [Tooltip("고원 윗면 최소 밝기 (HSV V). 어두운 지역 대비")] [Range(0, 1)] public float topMinBrightness = 0.34f;
    [Tooltip("바위 절벽 색")] public Color rockColor = new Color(0.46f, 0.39f, 0.32f);
    [Tooltip("절벽 윗선 강조 색 (고원 가장자리 띠)")] public Color rimColor = new Color(0.85f, 0.8f, 0.6f);
    [Tooltip("절벽 울퉁불퉁함 (보이는 모양만)")] public float cliffJitter = 0.3f;
}

/// <summary>
/// 고원 + 바위 절벽 + 경사로 지형을 만들고(Ground에 등록) 보이는 메시를 생성. 콜라이더는 없음 — 높이/절벽은 Ground가 계산.
/// 메시: 고원 윗면/경사로(풀) + 절벽/경사로 옆면(바위) + 가장자리 띠(밝은 색, 어두운 지역 구분용).
/// </summary>
public static class StageTerrain
{
    const int Seg = 56;
    static readonly List<Vector4> zoneBuf = new List<Vector4>(32);

    /// <summary>고원 배치 + 메시 생성. countMul/heightMul: 스테이지 특징(고지대)용 배율.</summary>
    public static void Build(TerrainSettings s, Transform parent, float countMul = 1f, float heightMul = 1f)
    {
        Ground.Clear();
        if (s == null || !s.enabled) return;
        Ground.MaxStep = Mathf.Max(0.2f, s.maxStep);

        Rect b = Arena.Square ? Arena.Bounds : new Rect(-Arena.Radius, -Arena.Radius, Arena.Radius * 2f, Arena.Radius * 2f);
        float areaMul = b.width * b.height / (120f * 120f);
        int count = Mathf.RoundToInt(Random.Range(s.plateauCount.x, s.plateauCount.y + 1) * areaMul * countMul);

        // 바닥 구역(물/용암/특징 장판)은 피함
        zoneBuf.Clear();
        foreach (var z in StageZone.All)
        {
            var sc = z.transform.lossyScale;
            zoneBuf.Add(new Vector4(z.transform.position.x, z.transform.position.z, Mathf.Max(sc.x, sc.z) * 0.5f + 1.5f, 0f));
        }

        int placed = 0;
        for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
        {
            float h = Random.Range(s.plateauHeight.x, s.plateauHeight.y) * heightMul;
            float r = Random.Range(s.plateauRadius.x, s.plateauRadius.y);
            float L = h * s.rampRatio, reach = r + L;
            var c = new Vector2(Random.Range(b.xMin + r, b.xMax - r), Random.Range(b.yMin + r, b.yMax - r));
            if (!Arena.Contains(new Vector3(c.x, 0f, c.y), r + s.edgeMargin)) continue;
            if (c.magnitude < s.centerSafeRadius + reach) continue;
            if (Ground.Overlaps(c, reach + s.spacing)) continue;
            bool hitZone = false;
            foreach (var z in zoneBuf) if ((c - new Vector2(z.x, z.y)).sqrMagnitude < (z.z + reach) * (z.z + reach)) { hitZone = true; break; }
            if (hitZone) continue;

            var pl = Ground.Add(c, r, h);
            int ramps = Random.Range(s.rampCount.x, s.rampCount.y + 1);
            AddRamps(pl, Mathf.Max(1, ramps), L, s);
            if (pl.ramps.Count == 0) { Ground.Remove(pl); continue; } // 경사로를 못 놓으면 버림
            placed++;

            // 계단식 단차: 고원 위에 작은 고원 (경사로는 아래 고원 윗면으로 내려옴)
            if (Random.value < s.terraceChance)
            {
                float h2 = Random.Range(s.terraceHeight.x, s.terraceHeight.y) * heightMul;
                float r2 = r * Random.Range(0.38f, 0.5f), L2 = h2 * s.rampRatio;
                if (r2 + L2 < r - 1f && r2 > 2.5f)
                {
                    var t = Ground.Add(c, r2, h + h2, h);
                    float a = Random.value * Mathf.PI * 2f;
                    t.ramps.Add(new Vector4(Mathf.Cos(a), Mathf.Sin(a), L2, Mathf.Min(s.rampWidth, r2 * 1.2f)));
                }
            }
        }
        if (Ground.HasHills) BuildMesh(s, parent);
    }


    /// <summary>경사로: 서로 90도 이상 떨어진 방향, 끝이 경계 안이고 다른 고원과 겹치지 않는 곳.</summary>
    static void AddRamps(Ground.Plateau pl, int n, float L, TerrainSettings s)
    {
        for (int k = 0; k < n; k++)
            for (int tries = 0; tries < 10; tries++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool close = false;
                foreach (var r in pl.ramps) if (Vector2.Dot(dir, new Vector2(r.x, r.y)) > 0f) { close = true; break; }
                if (close) continue;
                var end = pl.center + dir * (pl.radius + L + 1.5f);
                if (!Arena.Contains(new Vector3(end.x, 0f, end.y), 2f)) continue;
                if (end.magnitude < s.centerSafeRadius * 0.6f) continue;
                pl.ramps.Add(new Vector4(dir.x, dir.y, L, s.rampWidth));
                break;
            }
    }

    // =====================================================================
    // 메시
    // =====================================================================
    static readonly List<Vector3> verts = new List<Vector3>(8192);
    static readonly List<Vector2> uvs = new List<Vector2>(8192);
    static readonly List<int> grass = new List<int>(8192), rock = new List<int>(8192), rim = new List<int>(2048);

    static void BuildMesh(TerrainSettings s, Transform parent)
    {
        verts.Clear(); uvs.Clear(); grass.Clear(); rock.Clear(); rim.Clear();
        var ring = new Vector3[Seg];
        foreach (var pl in Ground.Plateaus)
        {
            float bottom = pl.baseHeight - 0.06f;
            // 바위 모양: 반경을 각도에 따라 살짝 들쭉날쭉 (보이는 모양만)
            float ph1 = pl.center.x * 0.37f, ph2 = pl.center.y * 0.53f;
            for (int i = 0; i < Seg; i++)
            {
                float a = i * Mathf.PI * 2f / Seg;
                float j = s.cliffJitter * (0.6f * Mathf.Sin(a * 5f + ph1) + 0.4f * Mathf.Sin(a * 11f + ph2));
                float rr = pl.radius + j * 0.5f;
                ring[i] = new Vector3(pl.center.x + Mathf.Cos(a) * rr, pl.height, pl.center.y + Mathf.Sin(a) * rr);
            }
            // 윗면 (부채꼴)
            int c0 = V(new Vector3(pl.center.x, pl.height, pl.center.y));
            int r0 = verts.Count;
            for (int i = 0; i < Seg; i++) V(ring[i]);
            for (int i = 0; i < Seg; i++) Tri(grass, c0, r0 + (i + 1) % Seg, r0 + i, Vector3.up);
            // 가장자리 띠 (밝은 테두리: 어두운 지역에서도 고원 윤곽이 보이게)
            int e0 = verts.Count;
            for (int i = 0; i < Seg; i++) V(ring[i] + Vector3.up * 0.015f);
            int e1 = verts.Count;
            for (int i = 0; i < Seg; i++) { var p = ring[i]; var d = new Vector3(p.x - pl.center.x, 0f, p.z - pl.center.y).normalized; V(p - d * 0.35f + Vector3.up * 0.015f); }
            for (int i = 0; i < Seg; i++) { int n = (i + 1) % Seg; Quad(rim, e0 + i, e0 + n, e1 + n, e1 + i, Vector3.up); }
            // 절벽 (위/중간/아래 3단, 중간 단을 들쭉날쭉하게 해서 바위 결)
            int w0 = verts.Count;
            float mid = Mathf.Lerp(bottom, pl.height, 0.5f);
            for (int i = 0; i < Seg; i++) V(ring[i]);
            for (int i = 0; i < Seg; i++)
            {
                var p = ring[i]; var d = new Vector3(p.x - pl.center.x, 0f, p.z - pl.center.y).normalized;
                float bump = s.cliffJitter * Mathf.Sin(i * 2.3f + ph2);
                V(new Vector3(p.x, mid + bump * 0.3f, p.z) + d * (bump * 0.5f + 0.1f));
            }
            for (int i = 0; i < Seg; i++) { var p = ring[i]; var d = new Vector3(p.x - pl.center.x, 0f, p.z - pl.center.y).normalized; V(new Vector3(p.x, bottom, p.z) + d * 0.2f); }
            for (int band = 0; band < 2; band++)
                for (int i = 0; i < Seg; i++)
                {
                    int n = (i + 1) % Seg, top = w0 + band * Seg, bot = top + Seg;
                    var outward = new Vector3(ring[i].x - pl.center.x, 0f, ring[i].z - pl.center.y);
                    Quad(rock, top + i, top + n, bot + n, bot + i, outward);
                }
            // 경사로
            foreach (var r in pl.ramps) Ramp(pl, r, bottom);
        }

        var mesh = new Mesh { name = "StageTerrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(grass, 0); mesh.SetTriangles(rock, 1); mesh.SetTriangles(rim, 2);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        var go = new GameObject("Terrain_Plateaus", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterials = Materials(s);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
    }

    /// <summary>경사로: 윗면(풀) + 양옆 벽(바위). along = 고원 중심에서 방향 축 거리.</summary>
    static void Ramp(Ground.Plateau pl, Vector4 r, float bottom)
    {
        var c = new Vector3(pl.center.x, 0f, pl.center.y);
        var dir = new Vector3(r.x, 0f, r.y);
        var side = new Vector3(r.y, 0f, -r.x);
        float hw = r.w * 0.5f;
        float start = Mathf.Sqrt(Mathf.Max(0f, pl.radius * pl.radius - hw * hw)) - 0.5f; // 고원 안쪽에서 시작 (틈 없게)
        float edge = pl.radius, end = pl.radius + r.z;
        float top = pl.height + 0.02f, low = pl.baseHeight + 0.005f;
        Vector3 P(float along, float perp, float y) { var p = c + dir * along + side * perp; p.y = y; return p; }
        // 윗면: 고원 안쪽 평평한 부분 + 경사
        Quad(grass, V(P(start, -hw, top)), V(P(start, hw, top)), V(P(edge, hw, top)), V(P(edge, -hw, top)), Vector3.up);
        Quad(grass, V(P(edge, -hw, top)), V(P(edge, hw, top)), V(P(end, hw, low)), V(P(end, -hw, low)), Vector3.up);
        // 양옆 벽
        foreach (float sgn in new[] { -1f, 1f })
        {
            float pp = hw * sgn;
            int a = V(P(edge, pp, top)), bb = V(P(end, pp, low)), cc = V(P(end, pp, bottom)), d = V(P(edge, pp, bottom));
            Quad(rock, a, bb, cc, d, side * sgn);
        }
    }

    static int V(Vector3 p) { verts.Add(p); uvs.Add(new Vector2(p.x * 0.15f + p.y * 0.1f, p.z * 0.15f + p.y * 0.1f)); return verts.Count - 1; }

    /// <summary>삼각형 추가: 법선이 hint 쪽을 향하도록 감는 방향 자동 결정.</summary>
    static void Tri(List<int> list, int a, int b, int c, Vector3 hint)
    {
        Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (Vector3.Dot(n, hint) < 0f) { int t = b; b = c; c = t; }
        list.Add(a); list.Add(b); list.Add(c);
    }

    static void Quad(List<int> list, int a, int b, int c, int d, Vector3 hint)
    {
        Tri(list, a, b, c, hint);
        Tri(list, a, c, d, hint);
    }

    static Material[] Materials(TerrainSettings s)
    {
        var floor = GameObject.Find("Arena/Floor");
        Material src = floor && floor.TryGetComponent(out Renderer fr) ? fr.sharedMaterial : null;
        if (!src) { var sh = Shader.Find("Universal Render Pipeline/Lit"); src = sh ? new Material(sh) : null; }
        Color baseC = src && src.HasColor("_BaseColor") ? src.GetColor("_BaseColor") : new Color(0.3f, 0.45f, 0.25f);
        Color.RGBToHSV(baseC, out float hh, out float ss, out float vv);
        var top = Color.HSVToRGB(hh, Mathf.Min(1f, ss * 1.05f), Mathf.Max(vv * s.topBrighten, s.topMinBrightness));
        // 바위: 지정 색에 지역 색을 살짝 섞음 (지역 분위기 유지)
        var rockC = Color.Lerp(s.rockColor, baseC, 0.2f);
        return new[] { Mat(src, top, "TerrainTop"), Mat(src, rockC, "TerrainRock"), Mat(src, s.rimColor, "TerrainRim") };
    }

    static Material Mat(Material src, Color c, string name)
    {
        var m = src ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = name;
        m.mainTexture = null;
        if (m.HasTexture("_BaseMap")) m.SetTexture("_BaseMap", null);
        if (m.HasColor("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasFloat("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        return m;
    }

    /// <summary>
    /// 배치된 장애물/장식을 지면 높이로 올림. 절벽 띠/경사로 위에 걸친 것은 치움 (경사로가 막히거나 절벽에 박히지 않게).
    /// </summary>
    public static void LiftChildren(Transform root)
    {
        if (!Ground.HasHills || !root) return;
        foreach (Transform t in root)
        {
            if (t.GetComponent<StageZone>()) continue;
            var p = t.position;
            if (Ground.OnFeature(p, 2f)) { t.gameObject.SetActive(false); continue; }
            p.y += Ground.HeightAt(p.x, p.z);
            t.position = p;
        }
    }
}

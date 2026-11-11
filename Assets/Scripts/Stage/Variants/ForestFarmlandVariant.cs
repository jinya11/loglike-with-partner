using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숲 파생형 "자연 농지": 벼 농사가 이루어진 곳. 평평해진 땅 전체가 빽빽한 벼 논(Meshy 벼 덤불 모델, GPU 인스턴싱)으로 덮이고,
/// 화면 전체에 벼가루가 은은하게 휘날림. 벼는 허리 높이라 적은 아래쪽이 벼에 묻혀 윗부분만 보임 (실제 깊이 가림).
/// 고유 규칙: 폭격하면 그 범위의 벼가 베여 적이 전부 드러남.
/// 기본형과 다른 점: 지형(평평한 논, 높이 ×terrainHeightMul) / 적 구성(감속형 증가) / 규칙(벼에 반쯤 가림 + 폭격 벼 베기).
/// StageDirector가 파생형일 때 붙임. 수치/모델은 StageConfig.farmland.
/// </summary>
public class ForestFarmlandVariant : MonoBehaviour
{
    const int Chunk = 1023;

    StageDirector stage;
    FarmlandSettings s;
    Mesh riceMesh;
    bool ownMesh;
    Vector3 meshScale = Vector3.one, meshPivot; // 모델을 clumpSize×높이 1로 맞추는 배율, 바닥 중심 보정
    Quaternion meshFix = Quaternion.identity;   // 모델 축 보정 (Z-up → Y-up)
    // 벼 덤불: 격자 칸마다 하나 (살아 있으면 그림)
    Vector3[] pos;
    float[] heights, yaws, sizes;
    bool[] alive, gold;
    int gridW, gridH;
    float cellX, cellZ; // 줄 안 포기 간격 / 줄 간격

    static float Hash01(int n) { unchecked { n = (n << 13) ^ n; return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f; } }
    Vector2 gridOrigin;
    // 재질별 인스턴스 묶음 (0 = 황금, 1 = 연두)
    readonly RenderParams[] rps = new RenderParams[2];
    ParticleSystem pollen;
    int cutCount;

    public void Init(StageDirector st, FarmlandSettings settings)
    {
        stage = st; s = settings;
        Material goldMat = s.riceMaterialGold, greenMat = s.riceMaterialGreen;
        if (s.riceMesh)
        {
            riceMesh = s.riceMesh;
            // 모델 크기 정규화: 가로 clumpSize, 높이 1 (인스턴스마다 높이 배율), 바닥 중심이 원점.
            // Meshy FBX는 위쪽이 Z축으로 들어옴(riceMeshUpIsZ) → X축 -90도로 세운 뒤 정규화
            var b = riceMesh.bounds;
            const float eps = 1e-6f;
            if (s.riceMeshUpIsZ)
            {
                meshFix = Quaternion.Euler(-90f, 0f, 0f);
                meshScale = new Vector3(s.clumpSize / Mathf.Max(eps, b.size.x), 1f / Mathf.Max(eps, b.size.z), s.clumpSize / Mathf.Max(eps, b.size.y));
                meshPivot = new Vector3(-b.center.x, -b.center.y, -b.min.z);
            }
            else
            {
                float w = Mathf.Max(eps, Mathf.Max(b.size.x, b.size.z));
                meshScale = new Vector3(s.clumpSize / w, 1f / Mathf.Max(eps, b.size.y), s.clumpSize / w);
                meshPivot = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            }
        }
        else { riceMesh = BuildRiceMesh(); ownMesh = true; meshScale = new Vector3(s.clumpSize / 0.6f, 1f, s.clumpSize / 0.6f); }
        if (!goldMat) goldMat = BuildRiceMaterial();
        if (!greenMat) greenMat = goldMat;
        foreach (var m in new[] { goldMat, greenMat }) m.enableInstancing = true;
        var bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
        rps[0] = new RenderParams(goldMat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true, worldBounds = bounds };
        rps[1] = new RenderParams(greenMat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true, worldBounds = bounds };
        PaintPaddyGround();
        PlantRice();
        RebuildChunks();
        MakePollen();
        CompanionBase.Bombarded += OnBombard;
        if (HudExtras.I) HudExtras.I.SetStatus("벼에 가려 적은 윗부분만 보입니다 — 폭격(R)으로 벼를 베면 드러납니다");
    }

    void OnDestroy()
    {
        CompanionBase.Bombarded -= OnBombard;
        if (HudExtras.I) HudExtras.I.SetStatus(null);
        if (ownMesh && riceMesh) Destroy(riceMesh);
    }

    /// <summary>
    /// 논 바닥: 줄 사이로 보이는 땅을 물 댄 논(짙은 물빛, 약한 반사)으로 칠함. 런타임 지형 복제본의 레이어만 바꿈
    /// (자연 농지는 지형을 평평하게 만들 때 이미 복제본을 씀 → 원본 TerrainData/레이어 에셋은 그대로).
    /// </summary>
    void PaintPaddyGround()
    {
        var t = Ground.Terrain;
        if (!t || !t.terrainData) return;
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "PaddyGround", wrapMode = TextureWrapMode.Repeat };
        var c = s.paddyGround;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float n = (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) - 0.5f) * 0.2f; // 물빛이라 얼룩은 약하게
                tex.SetPixel(x, y, new Color(c.r * (1f + n), c.g * (1f + n), c.b * (1f + n), 1f));
            }
        tex.Apply(true);
        var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(3f, 3f), smoothness = s.paddyWaterSmoothness }; // 물 댄 논: 약한 반사
        var td = t.terrainData;
        int count = Mathf.Max(1, td.alphamapLayers);
        var layers = new TerrainLayer[count];
        for (int i = 0; i < count; i++) layers[i] = layer; // 풀/바위 모두 논 바닥색으로
        td.terrainLayers = layers;
    }

    // ---- 벼 심기 (모내기 논처럼 곧은 줄로 가지런히) ----
    void PlantRice()
    {
        Rect b = Arena.Bounds;
        cellX = Mathf.Max(0.2f, s.plantSpacing);  // 줄 안 포기 간격 (줄은 X 방향)
        cellZ = Mathf.Max(0.3f, s.rowSpacing);    // 줄 간격 (줄 사이로 물 댄 논바닥이 보임)
        gridOrigin = new Vector2(b.xMin, b.yMin);
        gridW = Mathf.CeilToInt(b.width / cellX); gridH = Mathf.CeilToInt(b.height / cellZ);
        int n = gridW * gridH;
        pos = new Vector3[n]; heights = new float[n]; yaws = new float[n]; sizes = new float[n]; alive = new bool[n]; gold = new bool[n];
        var zones = StageZone.All;
        int seed = Random.Range(0, 100000);
        for (int z = 0; z < gridH; z++)
            for (int x = 0; x < gridW; x++)
            {
                int i = z * gridW + x;
                float wx = gridOrigin.x + (x + 0.5f) * cellX + Random.Range(-1f, 1f) * s.jitter;
                float wz = gridOrigin.y + (z + 0.5f) * cellZ + Random.Range(-1f, 1f) * s.jitter;
                var p = new Vector3(wx, 0f, wz);
                // 논두렁(길), 경계 밖, 시작 지점, 장애물 칸, 바닥 장판(물 등)은 비움
                float px = Mathf.Repeat(wx, s.pathEvery), pz = Mathf.Repeat(wz, s.pathEvery);
                if (px < s.pathWidth || pz < s.pathWidth) continue;
                if (!Arena.Contains(p, 0.8f) || wx * wx + wz * wz < 25f) continue;
                if (stage.Nav != null && !stage.Nav.IsFree(p)) continue;
                bool inZone = false;
                for (int k = 0; k < zones.Count; k++) if (zones[k].Contains(p)) { inZone = true; break; }
                if (inZone) continue;
                p.y = Ground.HeightAt(wx, wz) - 0.03f;
                pos[i] = p;
                // 논 구획(논두렁으로 나뉜 칸)마다 높이와 색을 하나로 → 구획 안은 가지런함
                int paddy = Mathf.FloorToInt(wx / s.pathEvery) * 7919 + Mathf.FloorToInt(wz / s.pathEvery) * 104729 + seed;
                float pr = Hash01(paddy), pr2 = Hash01(paddy * 31 + 17);
                heights[i] = Mathf.Lerp(s.height.x, s.height.y, pr2) * (1f + Random.Range(-1f, 1f) * s.sizeJitter);
                yaws[i] = Random.Range(-1f, 1f) * s.yawJitter;
                sizes[i] = 1f + Random.Range(-1f, 1f) * s.sizeJitter;
                gold[i] = pr < s.goldRatio;
                alive[i] = true;
            }
    }

    /// <summary>인스턴스 묶음 하나: 공간 타일(tileSize m) × 재질. 타일마다 경계가 있어 화면 밖 타일은 그리지 않음(절두체 컬링).</summary>
    class Batch { public Matrix4x4[] m; public int count; public int mat; public Bounds bounds; }
    readonly List<Batch> batches = new List<Batch>(256);
    const float TileSize = 12f;

    void RebuildChunks()
    {
        batches.Clear();
        int tilesX = Mathf.Max(1, Mathf.CeilToInt(gridW * cellX / TileSize));
        var open = new Dictionary<int, Batch>(512);
        for (int i = 0; i < alive.Length; i++)
        {
            if (!alive[i]) continue;
            int m = gold[i] ? 0 : 1;
            int tx = (int)((pos[i].x - gridOrigin.x) / TileSize), tz = (int)((pos[i].z - gridOrigin.y) / TileSize);
            int key = ((tz * tilesX + tx) << 1) | m;
            if (!open.TryGetValue(key, out var b) || b.count == Chunk)
            {
                b = new Batch { m = new Matrix4x4[Chunk], mat = m, bounds = new Bounds(pos[i], Vector3.one) };
                open[key] = b; batches.Add(b);
            }
            var scale = new Vector3(meshScale.x * sizes[i], meshScale.y * heights[i], meshScale.z * sizes[i]);
            b.m[b.count++] = Matrix4x4.TRS(pos[i], Quaternion.Euler(0f, yaws[i], 0f), scale) * Matrix4x4.Rotate(meshFix) * Matrix4x4.Translate(meshPivot);
            b.bounds.Encapsulate(new Bounds(pos[i] + Vector3.up * heights[i] * 0.5f, new Vector3(s.clumpSize * 1.3f, heights[i], s.clumpSize * 1.3f)));
        }
    }

    /// <summary>지금 그리는 벼 포기 수 (디버그/확인용).</summary>
    public int AliveCount { get { int n = 0; foreach (var b in batches) n += b.count; return n; } }

    /// <summary>벼 포기 하나: 엇갈린 잎 3장 (아래 초록 → 위 황금빛, 텍스처 그라데이션).</summary>
    static Mesh BuildRiceMesh()
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int k = 0; k < 3; k++)
        {
            float a = k * Mathf.PI / 3f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.22f;
            var lean = new Vector3(Mathf.Cos(a + 1.3f), 0f, Mathf.Sin(a + 1.3f)) * 0.12f; // 이삭이 살짝 휨
            int o = v.Count;
            v.Add(-d); v.Add(d); v.Add(-d * 0.3f + Vector3.up + lean); v.Add(d * 0.3f + Vector3.up + lean);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f));
            t.AddRange(new[] { o, o + 2, o + 1, o + 1, o + 2, o + 3 });
        }
        var m = new Mesh { name = "RiceStalk" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    Material BuildRiceMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh) { name = "Rice", enableInstancing = true };
        var tex = new Texture2D(4, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RiceGradient" };
        var bottom = new Color(s.riceColor.r * 0.55f, s.riceColor.g * 0.75f, s.riceColor.b * 0.4f);
        for (int y = 0; y < 32; y++)
        {
            float k = y / 31f;
            var c = k < 0.6f ? Color.Lerp(bottom, s.riceColor, k / 0.6f) : Color.Lerp(s.riceColor, s.riceTipColor, (k - 0.6f) / 0.4f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        if (m.HasTexture("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasColor("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasFloat("_Cull")) m.SetFloat("_Cull", 0f); // 양면
        if (m.HasFloat("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        m.doubleSidedGI = true;
        return m;
    }

    // ---- 벼가루 (화면 전체에 은은하게) ----
    void MakePollen()
    {
        var go = new GameObject("RicePollen");
        go.transform.SetParent(transform, false);
        pollen = go.AddComponent<ParticleSystem>();
        pollen.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = pollen.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(s.pollenSize.x, s.pollenSize.y);
        main.startColor = new Color(1f, 0.95f, 0.7f, s.pollenAlpha);
        main.maxParticles = 1500;
        main.gravityModifier = -0.005f;
        var em = pollen.emission; em.rateOverTime = s.pollenRate;
        var sh = pollen.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60f, 6f, 60f);
        var vel = pollen.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.1f); vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        var noise = pollen.noise; noise.enabled = true; noise.strength = 0.3f; noise.frequency = 0.4f;
        var col = pollen.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = PollenMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pollen.Play();
    }

    static Material PollenMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var m = new Material(sh ? sh : Shader.Find("Sprites/Default")) { name = "RicePollen" };
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "SoftDot" };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        if (m.HasTexture("_BaseMap")) m.SetTexture("_BaseMap", tex); else m.mainTexture = tex;
        // 반투명 (URP 파티클)
        if (m.HasFloat("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasFloat("_Blend")) m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // ---- 폭격으로 벼 베기 ----
    void OnBombard(Vector3 center, float radius)
    {
        float r = radius * s.cutRadiusMul, r2 = r * r;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - r - gridOrigin.x) / cellX)), x1 = Mathf.Min(gridW - 1, Mathf.CeilToInt((center.x + r - gridOrigin.x) / cellX));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((center.z - r - gridOrigin.y) / cellZ)), z1 = Mathf.Min(gridH - 1, Mathf.CeilToInt((center.z + r - gridOrigin.y) / cellZ));
        int cut = 0;
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                int i = z * gridW + x;
                if (!alive[i]) continue;
                float dx = pos[i].x - center.x, dz = pos[i].z - center.z;
                if (dx * dx + dz * dz > r2) continue;
                alive[i] = false; cut++;
            }
        if (cut == 0) return;
        cutCount += cut;
        RebuildChunks();
        Vfx.Pickup(center + Vector3.up, s.riceTipColor, 20);
    }

    void Update()
    {
        foreach (var b in batches)
        {
            var rp = rps[b.mat];
            rp.worldBounds = b.bounds; // 타일 경계로 컬링
            Graphics.RenderMeshInstanced(rp, riceMesh, 0, b.m, b.count);
        }

        var pc = PlayerController.I;
        if (pollen && pc) pollen.transform.position = pc.Position + Vector3.up * 3f;
    }
}

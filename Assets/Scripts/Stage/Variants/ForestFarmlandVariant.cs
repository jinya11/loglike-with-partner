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
    float cell;
    Vector2 gridOrigin;
    // 재질별 인스턴스 묶음 (0 = 황금, 1 = 연두)
    readonly List<Matrix4x4[]>[] chunks = { new List<Matrix4x4[]>(32), new List<Matrix4x4[]>(32) };
    readonly List<int>[] chunkCounts = { new List<int>(32), new List<int>(32) };
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
    /// 논 바닥: 벼 사이로 보이는 땅을 벼 색(어두운 황금빛)으로 칠함. 런타임 지형 복제본의 레이어만 바꿈
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
                float n = (Mathf.PerlinNoise(x * 0.2f, y * 0.2f) - 0.5f) * 0.35f + (Random.value - 0.5f) * 0.15f;
                tex.SetPixel(x, y, new Color(c.r * (1f + n), c.g * (1f + n), c.b * (1f + n), 1f));
            }
        tex.Apply(true);
        var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(3f, 3f), smoothness = 0.05f };
        var td = t.terrainData;
        int count = Mathf.Max(1, td.alphamapLayers);
        var layers = new TerrainLayer[count];
        for (int i = 0; i < count; i++) layers[i] = layer; // 풀/바위 모두 논 바닥색으로
        td.terrainLayers = layers;
    }

    // ---- 벼 심기 (빈틈 없이 촘촘하게) ----
    void PlantRice()
    {
        Rect b = Arena.Bounds;
        cell = Mathf.Max(0.3f, s.clumpSize * s.overlap);
        gridOrigin = new Vector2(b.xMin, b.yMin);
        gridW = Mathf.CeilToInt(b.width / cell); gridH = Mathf.CeilToInt(b.height / cell);
        int n = gridW * gridH;
        pos = new Vector3[n]; heights = new float[n]; yaws = new float[n]; sizes = new float[n]; alive = new bool[n]; gold = new bool[n];
        var zones = StageZone.All;
        float seedX = Random.value * 100f, seedZ = Random.value * 100f;
        for (int z = 0; z < gridH; z++)
            for (int x = 0; x < gridW; x++)
            {
                int i = z * gridW + x;
                float wx = gridOrigin.x + (x + 0.5f) * cell + Random.Range(-0.25f, 0.25f) * cell;
                float wz = gridOrigin.y + (z + 0.5f) * cell + Random.Range(-0.25f, 0.25f) * cell;
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
                heights[i] = Random.Range(s.height.x, s.height.y);
                yaws[i] = Random.Range(0, 4) * 90f + Random.Range(-20f, 20f);
                sizes[i] = Random.Range(0.9f, 1.15f);
                // 색 변주: 논 단위로 뭉쳐서 황금/연두 (사진처럼 층층이)
                gold[i] = Mathf.PerlinNoise(seedX + wx * 0.05f, seedZ + wz * 0.05f) < s.goldRatio + 0.15f * (Random.value - 0.5f);
                alive[i] = true;
            }
    }

    void RebuildChunks()
    {
        for (int m = 0; m < 2; m++) { chunks[m].Clear(); chunkCounts[m].Clear(); }
        var cur = new Matrix4x4[2][]; var c = new int[2];
        for (int i = 0; i < alive.Length; i++)
        {
            if (!alive[i]) continue;
            int m = gold[i] ? 0 : 1;
            if (cur[m] == null || c[m] == Chunk) { if (cur[m] != null) { chunks[m].Add(cur[m]); chunkCounts[m].Add(c[m]); } cur[m] = new Matrix4x4[Chunk]; c[m] = 0; }
            var scale = new Vector3(meshScale.x * sizes[i], meshScale.y * heights[i], meshScale.z * sizes[i]);
            cur[m][c[m]++] = Matrix4x4.TRS(pos[i], Quaternion.Euler(0f, yaws[i], 0f), scale) * Matrix4x4.Rotate(meshFix) * Matrix4x4.Translate(meshPivot);
        }
        for (int m = 0; m < 2; m++) if (cur[m] != null && c[m] > 0) { chunks[m].Add(cur[m]); chunkCounts[m].Add(c[m]); }
    }

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
        int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - r - gridOrigin.x) / cell)), x1 = Mathf.Min(gridW - 1, Mathf.CeilToInt((center.x + r - gridOrigin.x) / cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((center.z - r - gridOrigin.y) / cell)), z1 = Mathf.Min(gridH - 1, Mathf.CeilToInt((center.z + r - gridOrigin.y) / cell));
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
        for (int m = 0; m < 2; m++)
            for (int i = 0; i < chunks[m].Count; i++)
                Graphics.RenderMeshInstanced(rps[m], riceMesh, 0, chunks[m][i], chunkCounts[m][i]);

        var pc = PlayerController.I;
        if (pollen && pc) pollen.transform.position = pc.Position + Vector3.up * 3f;
    }
}

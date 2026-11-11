using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 숲 파생형 "자연 농지": 벼 농사가 이루어진 곳. 평평해진 땅 전체가 벼 논(GPU 인스턴싱)으로 둘러싸이고, 화면 전체에 벼가루가 은은하게 휘날림.
/// 고유 규칙: 키 큰 벼 속의 적은 모습이 가려짐 (생명체/감시자 가까이 오면 보임). 폭격하면 그 범위의 벼가 베여 시야가 트임.
/// 기본형과 다른 점: 지형(평평한 논, 높이 ×terrainHeightMul) / 적 구성(감속형 증가) / 규칙(벼 은폐 + 폭격 벼 베기).
/// StageDirector가 파생형일 때 붙임. 수치는 StageConfig.farmland.
/// </summary>
public class ForestFarmlandVariant : MonoBehaviour
{
    const int Chunk = 1023;

    StageDirector stage;
    FarmlandSettings s;
    Mesh riceMesh;
    Material riceMat;
    // 벼 포기: 격자 칸마다 하나 (살아 있으면 그림)
    Vector3[] pos;
    float[] heights, yaws;
    bool[] alive;
    int gridW, gridH;
    Vector2 gridOrigin;
    readonly List<Matrix4x4[]> chunks = new List<Matrix4x4[]>(64);
    readonly List<int> chunkCounts = new List<int>(64);
    RenderParams rp;
    ParticleSystem pollen;
    float hideTick;
    int cutCount;

    public void Init(StageDirector st, FarmlandSettings settings)
    {
        stage = st; s = settings;
        riceMesh = BuildRiceMesh();
        riceMat = BuildRiceMaterial();
        rp = new RenderParams(riceMat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true, worldBounds = new Bounds(Vector3.zero, Vector3.one * 2000f) };
        PlantRice();
        RebuildChunks();
        MakePollen();
        CompanionBase.Bombarded += OnBombard;
        if (HudExtras.I) HudExtras.I.SetStatus("벼 속의 적은 보이지 않습니다 — 폭격(R)으로 벼를 베어 시야를 확보하세요");
    }

    void OnDestroy()
    {
        CompanionBase.Bombarded -= OnBombard;
        if (HudExtras.I) HudExtras.I.SetStatus(null);
        if (riceMesh) Destroy(riceMesh);
    }

    // ---- 벼 심기 ----
    void PlantRice()
    {
        Rect b = Arena.Bounds;
        float sp = Mathf.Max(0.4f, s.spacing);
        gridOrigin = new Vector2(b.xMin, b.yMin);
        gridW = Mathf.CeilToInt(b.width / sp); gridH = Mathf.CeilToInt(b.height / sp);
        int n = gridW * gridH;
        pos = new Vector3[n]; heights = new float[n]; yaws = new float[n]; alive = new bool[n];
        var zones = StageZone.All;
        for (int z = 0; z < gridH; z++)
            for (int x = 0; x < gridW; x++)
            {
                int i = z * gridW + x;
                float wx = gridOrigin.x + (x + 0.5f) * sp + Random.Range(-0.2f, 0.2f) * sp;
                float wz = gridOrigin.y + (z + 0.5f) * sp + Random.Range(-0.2f, 0.2f) * sp;
                var p = new Vector3(wx, 0f, wz);
                // 논두렁(길), 경계 밖, 시작 지점, 장애물 칸, 바닥 장판(물 등)은 비움
                float px = Mathf.Repeat(wx, s.pathEvery), pz = Mathf.Repeat(wz, s.pathEvery);
                if (px < s.pathWidth || pz < s.pathWidth) continue;
                if (!Arena.Contains(p, 0.8f) || wx * wx + wz * wz < 25f) continue;
                if (stage.Nav != null && !stage.Nav.IsFree(p)) continue;
                bool inZone = false;
                for (int k = 0; k < zones.Count; k++) if (zones[k].Contains(p)) { inZone = true; break; }
                if (inZone) continue;
                p.y = Ground.HeightAt(wx, wz) - 0.05f;
                pos[i] = p;
                heights[i] = Random.Range(s.height.x, s.height.y);
                yaws[i] = Random.value * 360f;
                alive[i] = true;
            }
    }

    void RebuildChunks()
    {
        chunks.Clear(); chunkCounts.Clear();
        Matrix4x4[] cur = null; int c = 0;
        for (int i = 0; i < alive.Length; i++)
        {
            if (!alive[i]) continue;
            if (cur == null || c == Chunk) { if (cur != null) { chunks.Add(cur); chunkCounts.Add(c); } cur = new Matrix4x4[Chunk]; c = 0; }
            cur[c++] = Matrix4x4.TRS(pos[i], Quaternion.Euler(0f, yaws[i], 0f), new Vector3(1f, heights[i], 1f));
        }
        if (cur != null && c > 0) { chunks.Add(cur); chunkCounts.Add(c); }
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
        float sp = Mathf.Max(0.4f, s.spacing);
        int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - r - gridOrigin.x) / sp)), x1 = Mathf.Min(gridW - 1, Mathf.CeilToInt((center.x + r - gridOrigin.x) / sp));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((center.z - r - gridOrigin.y) / sp)), z1 = Mathf.Min(gridH - 1, Mathf.CeilToInt((center.z + r - gridOrigin.y) / sp));
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

    /// <summary>이 위치가 (베이지 않은) 벼 속인지.</summary>
    bool InRice(Vector3 p)
    {
        float sp = Mathf.Max(0.4f, s.spacing);
        int x = Mathf.FloorToInt((p.x - gridOrigin.x) / sp), z = Mathf.FloorToInt((p.z - gridOrigin.y) / sp);
        int hits = 0;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx, zz = z + dz;
                if (xx < 0 || zz < 0 || xx >= gridW || zz >= gridH) continue;
                if (alive[zz * gridW + xx]) hits++;
            }
        return hits >= 4; // 주변 대부분이 벼
    }

    void Update()
    {
        for (int i = 0; i < chunks.Count; i++)
            Graphics.RenderMeshInstanced(rp, riceMesh, 0, chunks[i], chunkCounts[i]);

        var pc = PlayerController.I;
        if (pollen && pc) pollen.transform.position = pc.Position + Vector3.up * 3f;

        // 벼 속의 적 숨기기 (0.15초마다)
        if ((hideTick -= Time.deltaTime) > 0f) return;
        hideTick = 0.15f;
        Vector3 w = Ward.I != null ? Ward.I.Position : (pc ? pc.Position : Vector3.zero);
        Vector3 p = pc ? pc.Position : w;
        float rr = s.revealRadius * s.revealRadius;
        foreach (var e in Enemy.Active)
        {
            Vector3 ep = e.transform.position;
            Vector3 dw = ep - w, dp = ep - p; dw.y = 0f; dp.y = 0f;
            bool near = dw.sqrMagnitude < rr || dp.sqrMagnitude < rr;
            e.SetHidden(!near && InRice(ep));
        }
    }
}

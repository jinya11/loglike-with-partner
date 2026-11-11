using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>사막 파생 씬: 모래폭풍 사막 (동굴 바위 배치 + 폭풍 모래 입자 + 유물 프리팹).</summary>
public static partial class PrototypeBuilder
{
    /// <summary>무광 파스텔 점토 재질 (URP Lit, metallic 0 / smoothness 0.12).</summary>
    static Material ClayMat(string name, Color c)
    {
        var m = VMat(name, "Universal Render Pipeline/Lit", c);
        m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.12f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildSandstorm(GameObject root, SandstormSettings s)
    {
        var rule = root.AddComponent<DesertSandstormVariant>();
        rule.windYaw = s.windYaw;
        rule.relicPrefab = RelicPrefab();
        Vector3 wind = Quaternion.Euler(0f, s.windYaw, 0f) * Vector3.forward;

        // 동굴 바위: 입구(로컬 +Z)가 바람이 불어가는 쪽. 평평한 곳에, 서로 떨어지게
        var cave = CaveRockPrefab();
        var rocks = new GameObject("CaveRocks").transform;
        rocks.SetParent(root.transform, false);
        var t = Terrain.activeTerrain;
        var rng = new System.Random(11);
        var used = new List<Vector3>();
        for (int tries = 0; used.Count < s.caveCount && tries < 800; tries++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * RegionHalf * 0.75f, z = ((float)rng.NextDouble() * 2f - 1f) * RegionHalf * 0.75f;
            var p = new Vector3(x, 0f, z);
            if (p.magnitude < 9f) continue; // 시작 지점 비움
            if (!FlatEnough(t, x, z, 4f, tries < 500 ? 1.0f : 1.8f)) continue;
            bool near = false;
            foreach (var u in used) if ((u - p).sqrMagnitude < 20f * 20f) { near = true; break; }
            if (near && tries < 600) continue;
            p.y = LowestGround(t, x, z, 3.5f) - 0.15f; // 발밑 가장 낮은 곳 기준 (뜨지 않게, 살짝 묻힘)
            used.Add(p);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cave, rocks);
            go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(wind) * Quaternion.Euler(0f, (float)(rng.NextDouble() * 16 - 8), 0f));
        }

        // 폭풍 모래 입자 (아레나 전체, 평소엔 방출 0 — 규칙이 경보/폭풍 때 켬)
        var go2 = new GameObject("SandstormParticles");
        go2.transform.SetParent(root.transform, false);
        go2.transform.position = new Vector3(0f, 2.5f, 0f);
        var ps = go2.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
        main.startColor = new Color(s.sandColor.r, s.sandColor.g, s.sandColor.b, 0.55f);
        main.maxParticles = 9000; main.playOnAwake = true;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(RegionHalf * 2.2f, 5f, RegionHalf * 2.2f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(wind.x * 13f, wind.x * 19f); vel.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f); vel.z = new ParticleSystem.MinMaxCurve(wind.z * 13f, wind.z * 19f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 1.2f; noise.frequency = 0.3f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go2.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = PollenMat(); // 부드러운 점 (색은 입자 색)
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 2f; // 바람결 줄무늬
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rule.sand = ps;
    }

    /// <summary>footprint 반경 안 지면 높이 차가 maxDiff 이하인지.</summary>
    static bool FlatEnough(Terrain t, float x, float z, float r, float maxDiff)
    {
        if (!t) return true;
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = -1; i <= 1; i++)
            for (int k = -1; k <= 1; k++)
            {
                float h = SampleGround(t, x + i * r, z + k * r);
                lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
            }
        return hi - lo <= maxDiff;
    }

    static float LowestGround(Terrain t, float x, float z, float r)
    {
        float lo = float.MaxValue;
        for (int i = -1; i <= 1; i++)
            for (int k = -1; k <= 1; k++) lo = Mathf.Min(lo, SampleGround(t, x + i * r, z + k * r));
        return lo;
    }

    /// <summary>
    /// 동굴 바위 (기본 도형 조합, 무광 사암 점토). 바람 쪽(로컬 -Z) 뒷벽 + 양옆 벽 + 뒷벽 위 처마 → 입구(+Z)로 들어가 숨음.
    /// 충돌은 상자 콜라이더(벽 3개) — 생명체/적이 통과 못 하고 길찾기 격자도 막힘으로 봄.
    /// </summary>
    static GameObject CaveRockPrefab()
    {
        string path = $"{VariantPrefabDir}/CaveRock.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var light = ClayMat("Sandstone_Light", new Color(0.92f, 0.74f, 0.55f));
        var dark = ClayMat("Sandstone_Dark", new Color(0.8f, 0.58f, 0.42f));
        var root = new GameObject("CaveRock");
        var sr = root.AddComponent<ShelterRock>();
        sr.halfWidth = 3.4f; sr.depth = 2.6f;
        // 뒷벽 (큰 바위 셋)
        LPart(PrimitiveType.Sphere, "Back_L", root.transform, new Vector3(-2f, 1.2f, -2.1f), new Vector3(3.2f, 2.8f, 2f), light);
        LPart(PrimitiveType.Sphere, "Back_C", root.transform, new Vector3(0f, 1.5f, -2.5f), new Vector3(3.6f, 3.4f, 2.2f), dark);
        LPart(PrimitiveType.Sphere, "Back_R", root.transform, new Vector3(2.1f, 1.1f, -2.0f), new Vector3(3f, 2.6f, 2f), light);
        // 옆벽
        LPart(PrimitiveType.Sphere, "Side_L", root.transform, new Vector3(-3.3f, 0.9f, 0f), new Vector3(1.8f, 2.1f, 3.2f), dark);
        LPart(PrimitiveType.Sphere, "Side_R", root.transform, new Vector3(3.3f, 0.8f, 0.1f), new Vector3(1.7f, 1.9f, 3f), light);
        // 처마 (뒷벽 위로 조금 튀어나옴 — 동굴 느낌, 안쪽 생명체는 보이게 짧게)
        LPart(PrimitiveType.Sphere, "Overhang", root.transform, new Vector3(0f, 2.9f, -1.6f), new Vector3(6.4f, 0.9f, 2.6f), light);
        // 바닥 모래 언덕 (입구 쪽으로 쌓인 모래)
        LPart(PrimitiveType.Sphere, "Drift", root.transform, new Vector3(0f, -0.05f, -0.6f), new Vector3(7.5f, 0.35f, 4.5f), ClayMat("Sand_Drift", new Color(0.96f, 0.85f, 0.64f)));
        foreach (var c in new[] { (new Vector3(0f, 1.4f, -2.3f), new Vector3(7.4f, 2.8f, 1.8f)), (new Vector3(-3.3f, 1f, 0f), new Vector3(1.4f, 2f, 3f)), (new Vector3(3.3f, 1f, 0.1f), new Vector3(1.4f, 2f, 2.8f)) })
        {
            var bc = root.AddComponent<BoxCollider>(); bc.center = c.Item1; bc.size = c.Item2;
        }
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    /// <summary>유물: 모래 둔덕 + 금빛 유물 조각(자식 'Relic', 규칙이 돌림).</summary>
    static GameObject RelicPrefab()
    {
        string path = $"{VariantPrefabDir}/SandRelic.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var sand = ClayMat("Sand_Drift", new Color(0.96f, 0.85f, 0.64f));
        var gold = ClayMat("Relic_Gold", new Color(1f, 0.8f, 0.35f));
        var teal = ClayMat("Relic_Teal", new Color(0.45f, 0.85f, 0.8f));
        var root = new GameObject("SandRelic");
        LPart(PrimitiveType.Sphere, "Mound", root.transform, new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.45f, 1.6f), sand);
        var relic = new GameObject("Relic").transform;
        relic.SetParent(root.transform, false); relic.localPosition = new Vector3(0f, 0.55f, 0f);
        LPart(PrimitiveType.Cylinder, "Urn", relic, new Vector3(0f, 0f, 0f), new Vector3(0.45f, 0.3f, 0.45f), gold);
        LPart(PrimitiveType.Sphere, "Gem", relic, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.3f, teal);
        var l = new GameObject("Glow").AddComponent<Light>();
        l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        l.type = LightType.Point; l.range = 3.5f; l.intensity = 1.5f; l.color = new Color(1f, 0.85f, 0.5f); l.shadows = LightShadows.None;
        foreach (var rr in root.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>버섯 숲 파생 씬: 발광 균사 동굴 (기본형 버섯 숲 지형 + 어두운 동굴 조명 + 빛나는 균사 덩어리(실제 빛 + 포자 파티클) + 회수할 포자 결정).</summary>
public static partial class PrototypeBuilder
{
    static void BuildMyceliumCave(GameObject root, MyceliumCaveSettings s)
    {
        root.AddComponent<MyceliumCaveVariant>();
        // 동굴: 해/주변광/안개/배경을 어둡게 (씬 조명이라 이후 직접 고치면 됨)
        foreach (var l in Object.FindObjectsByType<Light>()) if (l.type == LightType.Directional) { l.intensity *= s.sunMul; l.color = new Color(0.5f, 0.55f, 0.85f); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = s.ambient;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = s.fogColor; RenderSettings.fogStartDistance = 18f; RenderSettings.fogEndDistance = 70f;
        RenderSettings.reflectionIntensity = 0.1f;
        var cam = Camera.main;
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = s.fogColor; }
        FollowLightObj("WardLight", FollowLight.Target.Ward, 9f, 1.8f, new Color(0.7f, 0.95f, 1f), new Vector3(0f, 2.6f, 0f)); // 생명체 주변만 희미하게

        var t = Terrain.activeTerrain;
        // 균사 덩어리 (고르게 흩어짐)
        var prefab = MyceliumPatchPrefab(s);
        var parent = new GameObject("Mycelium").transform;
        parent.SetParent(root.transform, false);
        var rng = new System.Random(57);
        var used = new List<Vector3>();
        float lim = RegionHalf * 0.85f;
        for (int tries = 0; used.Count < s.patchCount && tries < 2000; tries++)
        {
            var p = new Vector3(((float)rng.NextDouble() * 2f - 1f) * lim, 0f, ((float)rng.NextDouble() * 2f - 1f) * lim);
            if (p.magnitude < 5f) continue; // 시작 지점 바로 밑은 비움
            bool near = false;
            foreach (var u in used) if ((u - p).sqrMagnitude < 11f * 11f) { near = true; break; }
            if (near && tries < 1600) continue;
            used.Add(p);
            p.y = SampleGround(t, p.x, p.z);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Mycelium_{used.Count:00}";
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
        }

        // 포자 결정 (회수 물품): 맵 곳곳에 멀리 떨어뜨려 놓음
        var crystal = SporeCrystalPrefab(s);
        var cparent = new GameObject("SporeCrystals").transform;
        cparent.SetParent(root.transform, false);
        var cused = new List<Vector3>();
        float clim = RegionHalf * 0.82f;
        for (int tries = 0; cused.Count < s.crystalCount && tries < 2000; tries++)
        {
            var p = new Vector3(((float)rng.NextDouble() * 2f - 1f) * clim, 0f, ((float)rng.NextDouble() * 2f - 1f) * clim);
            if (p.magnitude < 18f) continue; // 시작 지점에서 멀리
            bool near = false;
            foreach (var u in cused) if ((u - p).sqrMagnitude < 30f * 30f) { near = true; break; }
            if (near && tries < 1800) continue;
            cused.Add(p);
            p.y = SampleGround(t, p.x, p.z);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(crystal, cparent);
            go.name = $"SporeCrystal_{cused.Count:00}";
            go.transform.position = p;
        }
    }

    /// <summary>균사 덩어리: 가운데에서 뻗어 나간 빛나는 가닥 + 매듭 + 실제 빛(Point Light) + 떠오르는 포자 + 밟았을 때 퍼지는 포자 고리. 충돌 없음.</summary>
    static GameObject MyceliumPatchPrefab(MyceliumCaveSettings s)
    {
        string path = $"{VariantPrefabDir}/MyceliumPatch.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var glow = VMat("MyceliumGlow", "Universal Render Pipeline/Unlit", s.glowColor * 1.4f);
        var dim = VMat("MyceliumDim", "Universal Render Pipeline/Unlit", s.dimColor * 0.6f);
        var root = new GameObject("MyceliumPatch");
        var patch = root.AddComponent<MyceliumPatch>();
        var rs = new List<Renderer>();
        var rng = new System.Random(9);
        float R = s.patchRadius;
        rs.Add(LPart(PrimitiveType.Sphere, "Core", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.12f, 0.7f), glow));
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.PI * 2f / 9f + (float)rng.NextDouble() * 0.4f, len = R * Mathf.Lerp(0.6f, 1f, (float)rng.NextDouble());
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var strand = LPart(PrimitiveType.Cube, "Strand", root.transform, dir * len * 0.5f + Vector3.up * 0.03f, new Vector3(0.07f, 0.04f, len), glow);
            strand.transform.localRotation = Quaternion.LookRotation(dir);
            rs.Add(strand);
            var tip = dir * len;
            var side = Quaternion.Euler(0f, rng.Next(2) == 0 ? 35f : -35f, 0f) * dir;
            var twig = LPart(PrimitiveType.Cube, "Twig", root.transform, tip + side * 0.35f + Vector3.up * 0.03f, new Vector3(0.05f, 0.03f, 0.7f), glow);
            twig.transform.localRotation = Quaternion.LookRotation(side);
            rs.Add(twig);
            rs.Add(LPart(PrimitiveType.Sphere, "Knot", root.transform, tip + Vector3.up * 0.04f, Vector3.one * 0.18f, glow));
        }
        foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        patch.strands = rs.ToArray();
        patch.glowMaterial = glow; patch.dimMaterial = dim;

        // 실제 빛
        var l = new GameObject("GlowLight").AddComponent<Light>();
        l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        l.type = LightType.Point; l.color = s.glowColor; l.intensity = s.idleLight; l.range = s.idleRange; l.shadows = LightShadows.None;
        patch.glowLight = l;
        // 떠오르는 포자 (평소)
        patch.spores = SporeParticles(root.transform, "Spores", s.glowColor, false, R);
        // 밟았을 때 바깥으로 퍼지는 포자 고리
        patch.burst = SporeParticles(root.transform, "Burst", s.glowColor, true, R);

        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    static ParticleSystem SporeParticles(Transform parent, string name, Color c, bool burst, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(c.r, c.g, c.b, 0.9f);
        main.maxParticles = burst ? 200 : 60;
        var em = ps.emission;
        var sh = ps.shape;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        if (burst)
        {
            main.playOnAwake = false; main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = 1.2f; main.startSpeed = 8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 140) });
            sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.4f; sh.radiusThickness = 0f; sh.arc = 360f;
            sh.rotation = new Vector3(90f, 0f, 0f); // 바닥과 나란한 원 → 바깥으로 퍼짐
        }
        else
        {
            main.playOnAwake = true; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.gravityModifier = -0.02f;
            em.rateOverTime = 6f;
            sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius * 0.8f;
            sh.rotation = new Vector3(90f, 0f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.5f;
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = PollenMat();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!burst) ps.Play();
        return ps;
    }

    /// <summary>포자 결정: 빛나는 결정 몇 개가 모인 덩어리 + 받침 + 작은 빛. 충돌 없음.</summary>
    static GameObject SporeCrystalPrefab(MyceliumCaveSettings s)
    {
        string path = $"{VariantPrefabDir}/SporeCrystal.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var cry = VMat("SporeCrystal", "Universal Render Pipeline/Unlit", new Color(1f, 0.85f, 0.45f) * 1.6f);
        var basem = ClayMat("SporeCrystalBase", new Color(0.32f, 0.3f, 0.36f));
        var root = new GameObject("SporeCrystal");
        var sc = root.AddComponent<SporeCrystal>();
        LPart(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(1f, 0.08f, 1f), basem);
        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false); body.localPosition = new Vector3(0f, 0.9f, 0f);
        LPart(PrimitiveType.Cube, "Shard", body, Vector3.zero, new Vector3(0.35f, 0.9f, 0.35f), cry).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        LPart(PrimitiveType.Cube, "ShardL", body, new Vector3(-0.28f, -0.2f, 0f), new Vector3(0.22f, 0.55f, 0.22f), cry).transform.localRotation = Quaternion.Euler(0f, 30f, 22f);
        LPart(PrimitiveType.Cube, "ShardR", body, new Vector3(0.27f, -0.25f, 0.05f), new Vector3(0.2f, 0.45f, 0.2f), cry).transform.localRotation = Quaternion.Euler(10f, 60f, -25f);
        sc.body = body;
        var l = new GameObject("CrystalLight").AddComponent<Light>();
        l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.5f); l.intensity = 2.5f; l.range = 6f; l.shadows = LightShadows.None;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}

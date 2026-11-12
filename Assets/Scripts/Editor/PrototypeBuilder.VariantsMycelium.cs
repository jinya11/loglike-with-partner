using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>버섯 숲 파생 씬: 발광 균사 동굴 (기본형 버섯 숲 지형 + 어두운 동굴 조명 + 땅에 뻗은 빛나는 균사 덩어리).</summary>
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

        var prefab = MyceliumPatchPrefab(s);
        var parent = new GameObject("Mycelium").transform;
        parent.SetParent(root.transform, false);
        var t = Terrain.activeTerrain;
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
    }

    /// <summary>균사 덩어리: 가운데에서 뻗어 나간 빛나는 가닥 + 작은 매듭들. 충돌 없음.</summary>
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
            // 끝에서 갈라지는 잔가지 + 매듭
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
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}

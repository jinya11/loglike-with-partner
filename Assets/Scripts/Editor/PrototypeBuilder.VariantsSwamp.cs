using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>늪 파생 씬: 도깨비불 늪 (기본형 늪 지형 그대로 + 떠다니는 도깨비불).</summary>
public static partial class PrototypeBuilder
{
    static void BuildWispSwamp(GameObject root, WispSwampSettings s)
    {
        root.AddComponent<SwampWispVariant>();
        // 해 질 녘: 해/주변광을 낮추고 푸른 안개 — 도깨비불 빛이 잘 보이게 (씬 조명이라 이후 직접 고치면 됨)
        foreach (var l in Object.FindObjectsByType<Light>()) if (l.type == LightType.Directional) { l.intensity *= s.sunMul; l.color = new Color(0.6f, 0.7f, 0.85f); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = s.ambient;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = s.fogColor; RenderSettings.fogStartDistance = 25f; RenderSettings.fogEndDistance = 95f;
        var cam = Camera.main;
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = s.fogColor; }
        var prefab = SwampWispPrefab(s);
        var parent = new GameObject("Wisps").transform;
        parent.SetParent(root.transform, false);
        var t = Terrain.activeTerrain;
        var rng = new System.Random(23);
        var used = new List<Vector3>();
        float lim = RegionHalf * 0.75f;
        for (int tries = 0; used.Count < s.wispCount && tries < 800; tries++)
        {
            var p = new Vector3(((float)rng.NextDouble() * 2f - 1f) * lim, 0f, ((float)rng.NextDouble() * 2f - 1f) * lim);
            if (p.magnitude < 10f) continue; // 시작 지점 바로 옆은 비움
            bool near = false;
            foreach (var u in used) if ((u - p).sqrMagnitude < 16f * 16f) { near = true; break; }
            if (near && tries < 600) continue;
            used.Add(p);
            p.y = SampleGround(t, p.x, p.z) + s.hoverHeight;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Wisp_{used.Count:00}";
            go.transform.position = p;
        }
    }

    /// <summary>도깨비불: 빛나는 구 2겹(속 밝게) + 주변을 비추는 점광원. 충돌 없음 (닿음 판정은 거리).</summary>
    static GameObject SwampWispPrefab(WispSwampSettings s)
    {
        string path = $"{VariantPrefabDir}/SwampWisp.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var outer = VMat("WispGlow", "Universal Render Pipeline/Unlit", s.wispColor * 1.3f);
        var core = VMat("WispCore", "Universal Render Pipeline/Unlit", Color.Lerp(s.wispColor, Color.white, 0.6f) * 1.8f);
        var root = new GameObject("SwampWisp");
        var wisp = root.AddComponent<SwampWisp>();
        var glow = new GameObject("Glow").transform;
        glow.SetParent(root.transform, false);
        LPart(PrimitiveType.Sphere, "Flame", glow, Vector3.zero, new Vector3(1f, 1.25f, 1f), outer);
        LPart(PrimitiveType.Sphere, "Core", glow, new Vector3(0f, -0.08f, 0f), Vector3.one * 0.55f, core);
        LPart(PrimitiveType.Sphere, "Tip", glow, new Vector3(0f, 0.7f, 0f), new Vector3(0.4f, 0.6f, 0.4f), outer);
        foreach (var r in glow.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wisp.glow = glow;
        var l = new GameObject("WispLight").AddComponent<Light>();
        l.transform.SetParent(root.transform, false);
        l.type = LightType.Point; l.range = s.lightRange; l.intensity = s.lightIntensity; l.color = s.wispColor; l.shadows = LightShadows.None;
        wisp.lightSource = l;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }
}

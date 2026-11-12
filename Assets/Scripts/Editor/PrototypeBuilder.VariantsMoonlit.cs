using UnityEditor;
using UnityEngine;

/// <summary>묘지 파생 씬: 달빛 묘역 (기본형 묘지 지형 + 밤 조명 + 천천히 옮겨 다니는 달빛 구역).</summary>
public static partial class PrototypeBuilder
{
    static void BuildMoonlitGraveyard(GameObject root, MoonlitGraveyardSettings s)
    {
        root.AddComponent<MoonlitGraveyardVariant>();
        // 밤: 해/주변광/안개/배경 (씬 조명이라 이후 직접 고치면 됨)
        foreach (var l in Object.FindObjectsByType<Light>()) if (l.type == LightType.Directional) { l.intensity *= s.sunMul; l.color = new Color(0.55f, 0.62f, 0.9f); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = s.ambient;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = s.fogColor; RenderSettings.fogStartDistance = 22f; RenderSettings.fogEndDistance = 90f;
        RenderSettings.reflectionIntensity = 0.15f;
        var cam = Camera.main;
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = s.fogColor; }

        var prefab = MoonBeamPrefab(s);
        var parent = new GameObject("MoonBeams").transform;
        parent.SetParent(root.transform, false);
        var t = Terrain.activeTerrain;
        for (int i = 0; i < Mathf.Max(1, s.beamCount); i++)
        {
            float a = i * Mathf.PI * 2f / Mathf.Max(1, s.beamCount) + Mathf.PI * 0.5f;
            var p = i == 0 ? new Vector3(0f, 0f, 6f) : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RegionHalf * 0.4f; // 첫 달빛은 시작 지점 근처
            p.y = SampleGround(t, p.x, p.z);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"MoonBeam_{i + 1:00}";
            go.transform.position = p;
            go.GetComponent<MoonBeam>().Fit(s.radius);
        }
    }

    /// <summary>달빛 구역: 위에서 내리비추는 스폿 빛 + 옅은 빛 기둥 + 지형을 따라 그리는 바닥 고리. 충돌 없음.</summary>
    static GameObject MoonBeamPrefab(MoonlitGraveyardSettings s)
    {
        string path = $"{VariantPrefabDir}/MoonBeam.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var shaftMat = TransparentUnlit("MoonShaft", new Color(s.moonColor.r, s.moonColor.g, s.moonColor.b, 0.035f));
        var ringMat = VMat("MoonRing", "Sprites/Default", Color.white);
        var root = new GameObject("MoonBeam");
        var mb = root.AddComponent<MoonBeam>();
        var l = new GameObject("MoonLight").AddComponent<Light>();
        l.transform.SetParent(root.transform, false);
        l.transform.localPosition = new Vector3(0f, 18f, 0f);
        l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        l.type = LightType.Spot; l.color = s.moonColor; l.intensity = s.moonIntensity; l.shadows = LightShadows.None;
        l.innerSpotAngle = 1f;
        mb.beam = l;
        var shaft = LPart(PrimitiveType.Cylinder, "Shaft", root.transform, new Vector3(0f, 10f, 0f), new Vector3(1f, 10f, 1f), shaftMat);
        shaft.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mb.shaft = shaft.transform;
        var ring = new GameObject("Ring").AddComponent<LineRenderer>();
        ring.transform.SetParent(root.transform, false);
        ring.sharedMaterial = ringMat; ring.widthMultiplier = 0.14f; ring.positionCount = 0; ring.loop = true; ring.useWorldSpace = true;
        ring.startColor = ring.endColor = new Color(s.moonColor.r, s.moonColor.g, s.moonColor.b, 0.7f);
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mb.ring = ring;
        mb.Fit(s.radius);
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    /// <summary>반투명 무광(Unlit) 재질.</summary>
    static Material TransparentUnlit(string name, Color c)
    {
        string path = $"{VariantMatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }
}

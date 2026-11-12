using UnityEditor;
using UnityEngine;

/// <summary>버섯 숲 파생 씬: 거대 버섯 숲 (기본형 버섯 숲 지형 + 고리 모양으로 겹친 거대 버섯 갓 = 위층, 줄기 옆 덩굴 사다리 = 오르내리는 곳).</summary>
public static partial class PrototypeBuilder
{
    static void BuildGiantMushroom(GameObject root, GiantMushroomSettings s)
    {
        var rule = root.AddComponent<GiantMushroomVariant>();
        var t = Terrain.activeTerrain;
        // 지형: 기본형 지형을 복사한 전용 TerrainData — 버섯 고리 자리를 평평하게 (갓이 지면에서 일정한 높이에 오도록)
        if (t && t.terrainData)
        {
            string src = AssetDatabase.GetAssetPath(t.terrainData);
            string dst = $"{TerrainDir}/Region_Mushroom_Giant_Terrain.asset";
            AssetDatabase.DeleteAsset(dst);
            AssetDatabase.CopyAsset(src, dst);
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dst);
            FlattenLake(td, t.transform.position, s.ringRadius + s.capRadius + 2f, 12f);
            EditorUtility.SetDirty(td);
            t.terrainData = td;
            if (t.TryGetComponent(out TerrainCollider tc)) tc.terrainData = td;
        }
        var mushPrefab = GiantMushroomPrefab(s);
        var spotPrefab = ClimbSpotPrefab(s);
        var parent = new GameObject("GiantMushrooms").transform;
        parent.SetParent(root.transform, false);
        var spots = new GameObject("ClimbSpots").transform;
        spots.SetParent(root.transform, false);

        int n = Mathf.Max(1, s.mushroomCount);
        float top = 0f;
        var pos = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n + Mathf.PI / n;
            pos[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * s.ringRadius;
            pos[i].y = SampleGround(t, pos[i].x, pos[i].z);
            // 갓 아래 전체에서 가장 높은 지면
            for (int k = 0; k < 12; k++)
            {
                float b = k * Mathf.PI * 2f / 12f;
                top = Mathf.Max(top, SampleGround(t, pos[i].x + Mathf.Cos(b) * s.capRadius, pos[i].z + Mathf.Sin(b) * s.capRadius));
            }
            top = Mathf.Max(top, pos[i].y);
        }
        rule.capY = top + s.capHeight;

        for (int i = 0; i < n; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(mushPrefab, parent);
            go.name = $"GiantMushroom_{i + 1:00}";
            go.transform.SetPositionAndRotation(pos[i], Quaternion.Euler(0f, i * 47f, 0f));
            var m = go.GetComponent<GiantMushroom>();
            m.capRadius = s.capRadius;
            m.FitHeight(rule.capY);
            // 오르내리는 곳: 줄기에서 고리 중심 쪽으로
            Vector3 inward = (-new Vector3(pos[i].x, 0f, pos[i].z)).normalized;
            Vector3 sp = pos[i] + inward * (m.stemRadius + 1.3f);
            sp.y = SampleGround(t, sp.x, sp.z);
            var sgo = (GameObject)PrefabUtility.InstantiatePrefab(spotPrefab, spots);
            sgo.name = $"ClimbSpot_{i + 1:00}";
            sgo.transform.SetPositionAndRotation(sp, Quaternion.LookRotation(-inward));
            sgo.GetComponent<ClimbSpot>().FitHeight(rule.capY);
        }
    }

    /// <summary>거대 버섯: 줄기(원기둥) + 갓(평평한 윗면 + 둥근 테두리 + 흰 점 + 주름). 갓 재질은 불투명/반투명 두 벌.</summary>
    static GameObject GiantMushroomPrefab(GiantMushroomSettings s)
    {
        string path = $"{VariantPrefabDir}/GiantMushroom.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var stemMat = ClayMat("MushroomStem", s.stemColor);
        var capMat = ClayMat("MushroomCap", s.capColor);
        var capFaded = TransparentLit("MushroomCapFaded", new Color(s.capColor.r, s.capColor.g, s.capColor.b, s.capFadeAlpha));
        var spotMat = ClayMat("MushroomSpot", s.spotColor);
        var gillMat = ClayMat("MushroomGill", Color.Lerp(s.stemColor, s.capColor, 0.25f) * 0.85f);
        float R = s.capRadius;

        var root = new GameObject("GiantMushroom");
        var m = root.AddComponent<GiantMushroom>();
        m.capRadius = R;
        var stem = LPart(PrimitiveType.Cylinder, "Stem", root.transform, new Vector3(0f, 2.5f, 0f), new Vector3(m.stemRadius * 2f, 2.5f, m.stemRadius * 2f), stemMat);
        m.stem = stem.transform;
        var cap = new GameObject("Cap").transform;
        cap.SetParent(root.transform, false);
        cap.localPosition = new Vector3(0f, 5f, 0f);
        m.cap = cap;
        var rs = new System.Collections.Generic.List<Renderer>();
        rs.Add(LPart(PrimitiveType.Cylinder, "CapTop", cap, new Vector3(0f, -0.2f, 0f), new Vector3(R * 2f, 0.2f, R * 2f), capMat));
        rs.Add(LPart(PrimitiveType.Sphere, "CapRim", cap, new Vector3(0f, -0.55f, 0f), new Vector3(R * 2.06f, 1.5f, R * 2.06f), capMat));
        rs.Add(LPart(PrimitiveType.Cylinder, "Gills", cap, new Vector3(0f, -1.1f, 0f), new Vector3(R * 1.7f, 0.05f, R * 1.7f), gillMat));
        var rng = new System.Random(3);
        for (int i = 0; i < 9; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Lerp(0.25f, 0.82f, (float)rng.NextDouble()) * R, sz = Mathf.Lerp(0.9f, 1.8f, (float)rng.NextDouble());
            rs.Add(LPart(PrimitiveType.Sphere, "Spot", cap, new Vector3(Mathf.Cos(a) * d, 0.01f, Mathf.Sin(a) * d), new Vector3(sz, 0.12f, sz), spotMat));
        }
        m.capRenderers = rs.ToArray();
        m.capOpaque = capMat; m.capFaded = capFaded;
        // 반투명으로 바뀌는 갓은 그림자를 끄지 않음 (불투명일 때 아래층 그늘)
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    /// <summary>오르내리는 곳: 아래층 빛나는 고리 + 덩굴 사다리 + 위층 고리.</summary>
    static GameObject ClimbSpotPrefab(GiantMushroomSettings s)
    {
        string path = $"{VariantPrefabDir}/ClimbSpot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var glow = VMat("ClimbGlow", "Universal Render Pipeline/Unlit", s.climbColor);
        var vineMat = ClayMat("ClimbVine", new Color(0.45f, 0.65f, 0.4f));
        var root = new GameObject("ClimbSpot");
        var c = root.AddComponent<ClimbSpot>();
        LPart(PrimitiveType.Cylinder, "LowerMark", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(1.8f, 0.02f, 1.8f), glow).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var vine = new GameObject("Vine").transform;
        vine.SetParent(root.transform, false);
        LPart(PrimitiveType.Cylinder, "Stalk", vine, Vector3.zero, new Vector3(0.22f, 1f, 0.22f), vineMat);
        for (int i = 0; i < 6; i++) LPart(PrimitiveType.Sphere, "Leaf", vine, new Vector3((i % 2 == 0 ? 0.18f : -0.18f), -0.8f + i * 0.32f, 0f), new Vector3(0.35f, 0.08f, 0.22f), vineMat);
        c.vine = vine;
        var up = LPart(PrimitiveType.Cylinder, "UpperMark", root.transform, new Vector3(0f, 5f, 0f), new Vector3(1.8f, 0.02f, 1.8f), glow);
        up.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        c.upperMark = up.transform;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    /// <summary>반투명 무광 클레이(Lit) 재질.</summary>
    static Material TransparentLit(string name, Color c)
    {
        string path = $"{VariantMatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.12f);
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

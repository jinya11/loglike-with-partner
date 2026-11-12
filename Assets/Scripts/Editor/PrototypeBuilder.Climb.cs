using UnityEditor;
using UnityEngine;

/// <summary>밧줄(덩굴 사다리) 타기 지점 프리팹 (독립 기능, 다른 스테이지에서 재사용). 프리팹: Prefabs/Presets/Variants/ClimbSpot.prefab.</summary>
public static partial class PrototypeBuilder
{
    /// <summary>아래쪽 빛나는 고리 + 덩굴 사다리 + 위쪽 고리. 이미 있으면 그대로 씀.</summary>
    static GameObject ClimbSpotPrefab(Color markColor)
    {
        string path = $"{VariantPrefabDir}/ClimbSpot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var glow = VMat("ClimbGlow", "Universal Render Pipeline/Unlit", markColor);
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
}

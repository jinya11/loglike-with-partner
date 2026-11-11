using UnityEditor;
using UnityEngine;

/// <summary>
/// 자연 농지 벼 모델 연결: Meshy로 만든 벼 덤불(Assets/Prototype/Models/Rice/RicePatch.fbx + basecolor)을 임포트 설정하고
/// 황금/연두 재질 2종(인스턴싱, 양면)을 만들어 StageConfig.farmland에 연결.
/// 메뉴: Roguelike/Setup Farmland Rice Model
/// </summary>
public static partial class PrototypeBuilder
{
    const string RiceDir = Root + "/Models/Rice";

    [MenuItem("Roguelike/Setup Farmland Rice Model")]
    public static void SetupFarmlandRice()
    {
        string fbx = RiceDir + "/RicePatch.fbx", tex = RiceDir + "/RicePatch_basecolor.png";
        var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (imp == null) { Debug.LogError("[PrototypeBuilder] 벼 모델이 없습니다: " + fbx); return; }
        imp.bakeAxisConversion = true;               // Meshy FBX는 Z-up
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.importAnimation = false; imp.importCameras = false; imp.importLights = false;
        imp.isReadable = false;
        imp.SaveAndReimport();

        Mesh mesh = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx)) if (o is Mesh m) { mesh = m; break; }
        var basecolor = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
        if (!mesh) { Debug.LogError("[PrototypeBuilder] 벼 모델에서 메시를 찾지 못했습니다"); return; }

        var gold = RiceMat("Rice_Gold", basecolor, new Color(1.05f, 0.92f, 0.55f));
        var green = RiceMat("Rice_Green", basecolor, new Color(0.82f, 1f, 0.55f));

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var f = cfg.stage.farmland;
        f.riceMesh = mesh; f.riceMaterialGold = gold; f.riceMaterialGreen = green;
        EditorUtility.SetDirty(cfg.stage);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PrototypeBuilder] 벼 모델 연결: {mesh.name} (삼각형 {mesh.triangles.Length / 3})");
    }

    static Material RiceMat(string name, Texture2D tex, Color tint)
    {
        string path = $"{RiceDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        if (tex) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Cull", 0f);          // 양면 (얇은 잎)
        m.SetFloat("_Smoothness", 0.12f);
        m.enableInstancing = true;
        m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        return m;
    }
}

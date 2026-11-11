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
        // 벼 한 포기 (사용자 참고 이미지 → Meshy image-to-3d, 스마트 토폴로지 ~1000 삼각형). 이전 벼 패치(RicePatch)는 남겨 둠
        string fbx = RiceDir + "/RicePlant.fbx", tex = RiceDir + "/RicePlant_base_color.png";
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

        // 아트 스타일: 다른 Meshy 모델(토끼/당근/엘리/유나)과 같은 URP Lit, 금속 0, 매끄러움 0.12의 무광 파스텔 클레이 + Meshy 기본색 텍스처
        var gold = RiceMat("Rice_Gold", basecolor, Color.white);
        var green = RiceMat("Rice_Green", basecolor, new Color(0.9f, 1f, 0.82f)); // 덜 익은 벼 (살짝 연두)

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var f = cfg.stage.farmland;
        f.riceMesh = mesh; f.riceMaterialGold = gold; f.riceMaterialGreen = green;
        f.riceMeshUpIsZ = mesh.bounds.size.z > mesh.bounds.size.y * 1.2f; // 축 자동 판정 (식물은 위로 긴 모양)
        // 한 포기 기준 배치 수치 (포기 폭 0.6m, 촘촘히 겹쳐 빈틈 없이, 높이는 적 키 0.5~0.9m보다 낮게)
        f.clumpSize = 0.65f; f.overlap = 0.62f; f.height = new Vector2(0.55f, 0.72f);
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
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        return m;
    }
}

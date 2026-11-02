using UnityEditor;
using UnityEngine;

/// <summary>
/// 생명체 고유 공격 에셋 생성/연결. 토끼: 당근 던지기 (Meshy 당근 모델 → Carrot 프리팹 → ThrowAttack).
/// 메뉴: Roguelike/Setup Creature Attacks (재실행해도 안전, 있는 것은 갱신)
/// </summary>
public static partial class PrototypeBuilder
{
    const string CarrotDir = "Assets/Prototype/Creatures/Rabbit/Carrot";

    [MenuItem("Roguelike/Setup Creature Attacks")]
    public static void SetupCreatureAttacks()
    {
        var carrot = BuildCarrotPrefab();
        string atkPath = "Assets/Prototype/Creatures/Rabbit/Rabbit_CarrotThrow.asset";
        var atk = AssetDatabase.LoadAssetAtPath<ThrowAttack>(atkPath);
        if (!atk)
        {
            atk = ScriptableObject.CreateInstance<ThrowAttack>();
            atk.displayName = "당근 던지기";
            atk.description = "가장 가까운 적에게 당근을 포물선으로 던져 떨어진 자리에 작은 범위 피해";
            AssetDatabase.CreateAsset(atk, atkPath);
        }
        atk.projectile = carrot;
        EditorUtility.SetDirty(atk);

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var list = cfg.ward.creatures;
        if (list != null)
            foreach (var c in list)
                if (c != null && (c.name == "토끼" || (c.model && c.model.name.Contains("Rabbit")))) c.attack = atk;
        EditorUtility.SetDirty(cfg.ward);
        AssetDatabase.SaveAssets();
        Debug.Log("[PrototypeBuilder] 생명체 공격 설정 완료 (토끼: 당근 던지기)");
    }

    /// <summary>Meshy 당근(FBX, Z-up, 외부 텍스처)을 매트한 머티리얼로 임포트하고 던지기용 프리팹으로 감쌈.</summary>
    static ThrownProjectile BuildCarrotPrefab()
    {
        string fbx = $"{CarrotDir}/carrot.fbx", tex = $"{CarrotDir}/carrot_basecolor.png";
        var texImp = AssetImporter.GetAtPath(tex) as TextureImporter;
        if (texImp != null && texImp.maxTextureSize != 512) { texImp.maxTextureSize = 512; texImp.SaveAndReimport(); }

        string matPath = $"{CarrotDir}/M_Carrot.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true }; AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.12f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(mat);

        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        imp.bakeAxisConversion = true;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.animationType = ModelImporterAnimationType.None;
        imp.importAnimation = false;
        imp.SaveAndReimport();

        var root = new GameObject("Carrot");
        var spinner = new GameObject("Spinner").transform;
        spinner.SetParent(root.transform, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        model.transform.SetParent(spinner, false);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // 크기: 가장 긴 변 1.1m (멀리서도 보이게), 중심을 회전축(Spinner 원점)에 맞춤
        var b = new Bounds(model.transform.position, Vector3.zero);
        foreach (var r in model.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        float s = 1.1f / Mathf.Max(0.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        model.transform.localScale *= s;
        model.transform.localPosition = -(b.center - model.transform.position) * s;

        var tp = root.AddComponent<ThrownProjectile>();
        Set(tp, "spinner", spinner);
        var p = PrefabUtility.SaveAsPrefabAsset(root, $"{CarrotDir}/Carrot.prefab");
        Object.DestroyImmediate(root);
        return p.GetComponent<ThrownProjectile>();
    }
}

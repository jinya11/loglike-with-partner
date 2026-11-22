using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 교환소 (거점 재화 → 성장의 씨앗). 메뉴: Roguelike/Setup Exchange Stand (재실행해도 안전).
/// 만드는 것: 과일 노점 모델 머티리얼/프리팹(ExchangeStand), HUD_Exchange 프리팹, MetaConfig 교환 비율 기본값(비어 있을 때만),
/// 열려 있는 로비 씬에 교환소가 없으면 배치 (씬 저장은 하지 않음).
/// 모델 파일: Assets/LobbyIsland/Models/exchangestand.fbx + exchangestand_basecolor.png
/// </summary>
public static partial class PrototypeBuilder
{
    const string ExchangeModelDir = "Assets/LobbyIsland/Models";

    [MenuItem("Roguelike/Setup Exchange Stand")]
    static void SetupExchangeStand()
    {
        var sp = BuildGrowthSprites();
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var meta = cfg.meta;
        if (meta.exchangeRates == null || meta.exchangeRates.Length == 0)
            meta.exchangeRates = new[]
            {
                new ExchangeRate { from = ResourceType.AncientStone, amount = 3 },
                new ExchangeRate { from = ResourceType.FrostCrystal, amount = 3 },
                new ExchangeRate { from = ResourceType.RiverPebble, amount = 3 },
                new ExchangeRate { from = ResourceType.MagmaStone, amount = 4 },
                new ExchangeRate { from = ResourceType.SugarCrystal, amount = 2 },
            };
        meta.exchangePrefab = BuildExchangeUI(sp).GetComponent<ExchangeUI>();
        EditorUtility.SetDirty(meta);
        var stand = BuildExchangeStandPrefab();
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.GetActiveScene();
        if (stand && scene.name == "Lobby" && Object.FindAnyObjectByType<ExchangeStation>() == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(stand, scene);
            var lobby = GameObject.Find("Lobby");
            if (lobby) go.transform.SetParent(lobby.transform, true);
            go.transform.position = new Vector3(6f, -0.07f, -2f); // 출발 포털 동쪽 빈 자리 (씬에서 옮겨도 됨)
            go.transform.rotation = Quaternion.LookRotation(new Vector3(-6f, 0f, 2f)); // 광장 가운데를 보게
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
            Debug.Log("[PrototypeBuilder] 로비에 교환소 배치 (위치 확인 후 씬 저장)");
        }
        Debug.Log("[PrototypeBuilder] 교환소 설정 완료");
    }

    static GameObject BuildExchangeStandPrefab()
    {
        string fbx = $"{ExchangeModelDir}/exchangestand.fbx", tex = $"{ExchangeModelDir}/exchangestand_basecolor.png";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (!model) { Debug.LogWarning("교환소 모델 없음: " + fbx); return null; }
        var texImp = AssetImporter.GetAtPath(tex) as TextureImporter;
        if (texImp != null && texImp.maxTextureSize != 1024) { texImp.maxTextureSize = 1024; texImp.SaveAndReimport(); }
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (!imp.bakeAxisConversion || imp.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            imp.bakeAxisConversion = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.SaveAndReimport();
        }
        string matPath = $"{ExchangeModelDir}/M_ExchangeStand.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true }; AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.1f); mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_EnvironmentReflections", 0f); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(mat);

        string path = Root + "/Prefabs/ExchangeStand.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing; // 직접 다듬었을 수 있으니 덮어쓰지 않음

        var root = new GameObject("ExchangeStand");
        var m = (GameObject)PrefabUtility.InstantiatePrefab(model);
        m.transform.SetParent(root.transform, false);
        foreach (var r in m.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        // 높이 2.6m, 바닥(y=0)에 맞춤, 가운데 정렬
        var b = new Bounds(m.transform.position, Vector3.zero);
        foreach (var r in m.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        float s = 2.6f / Mathf.Max(0.001f, b.size.y);
        m.transform.localScale *= s;
        m.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1.3f, 0f); col.size = new Vector3(b.size.x * s * 0.85f, 2.6f, b.size.z * s * 0.85f);
        root.AddComponent<ExchangeStation>();
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    static GameObject BuildExchangeUI(GrowthSprites sp)
    {
        GrowthFonts();
        var canvas = NewCanvas("HUD_Exchange");
        canvas.GetComponent<Canvas>().sortingOrder = 60;
        var panel = Stretch("Panel", canvas.transform);
        Img(panel, Color.white).sprite = sp.bg; // 성장 노드 창과 같은 파스텔 그라데이션
        Deco(panel, sp.glow, new Vector2(-560, 300), 1000, new Color(1f, 0.72f, 0.84f, 0.55f));
        Deco(panel, sp.glow, new Vector2(520, -280), 1100, new Color(0.8f, 0.74f, 1f, 0.5f));
        Deco(panel, sp.glow, new Vector2(-120, -40), 900, new Color(1f, 0.95f, 0.75f, 0.45f));

        // 창 (둥근 크림 카드)
        var win = UI("Window", panel, C, C, new Vector2(0, -30), new Vector2(780, 900));
        var winImg = Img(win, new Color(1f, 0.985f, 0.97f, 0.96f)); winImg.sprite = sp.round; winImg.type = Image.Type.Sliced;
        SoftShadow(win);

        // 제목 패널 (창 위쪽에 걸침)
        var plate = UI("TitlePlate", win, TC, TC, new Vector2(0, 40), new Vector2(340, 92));
        var plateImg = Img(plate, PinkBtn); plateImg.sprite = sp.round; plateImg.type = Image.Type.Sliced;
        SoftShadow(plate);
        Txt("Title", plate, "교환소", 46, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 닫기 X
        var close = UI("CloseButton", win, Vector2.one, Vector2.one, new Vector2(-22, -22), new Vector2(64, 64), Vector2.one);
        var closeImg = Img(close, new Color(1f, 0.9f, 0.9f)); closeImg.sprite = sp.dot;
        var closeBtn = close.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = closeImg;
        Txt("Label", close, "X", 30, TextAlignmentOptions.Center, Brown, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 보유 재화 바
        var bar = UI("WalletBar", win, TC, TC, new Vector2(0, -100), new Vector2(700, 80), new Vector2(0.5f, 1f));
        var barImg = Img(bar, new Color(1f, 0.92f, 0.92f)); barImg.sprite = sp.round; barImg.type = Image.Type.Sliced;
        var wallet = Txt("Text", bar, "고대 석재 <b>0</b>    성장의 씨앗 <b>0</b>", 20, TextAlignmentOptions.Center, Brown, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40, -12), null, false);

        // 카드 목록 (세로 1열, 스크롤)
        var scroll = UI("Scroll", win, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(720, 680), new Vector2(0.5f, 0f));
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        var vp = Stretch("Viewport", scroll);
        Img(vp, new Color(1, 1, 1, 0f));
        vp.gameObject.AddComponent<RectMask2D>();
        var content = UI("Content", vp, TC, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 1f));
        content.anchorMin = new Vector2(0f, 1f);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16; vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30f;

        var row = UI("RowTemplate", content, TL, TL, Vector2.zero, new Vector2(700, 120));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
        var rowImg = Img(row, Color.white); rowImg.sprite = sp.round; rowImg.type = Image.Type.Sliced;
        SoftShadow(row);
        var label = Txt("Label", row, "{0} {1}개  →  성장의 씨앗 1개", 28, TextAlignmentOptions.Left, Brown, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 16), new Vector2(470, 44), new Vector2(0, 0.5f));
        var have = Txt("Have", row, "보유 {0}", 22, TextAlignmentOptions.Left, SoftBrown, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, -24), new Vector2(470, 34), new Vector2(0, 0.5f), false);
        var btnRt = UI("TradeButton", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(150, 66), new Vector2(1, 0.5f));
        var btnImg = Img(btnRt, PinkBtn); btnImg.sprite = sp.round; btnImg.type = Image.Type.Sliced;
        var btn = btnRt.gameObject.AddComponent<Button>(); btn.targetGraphic = btnImg;
        var cb = btn.colors; cb.disabledColor = new Color(0.8f, 0.78f, 0.82f, 0.85f); btn.colors = cb;
        Txt("Label", btnRt, "교환", 28, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var er = row.gameObject.AddComponent<ExchangeRow>();
        er.label = label; er.have = have; er.button = btn;
        row.gameObject.SetActive(false);

        var ui = canvas.AddComponent<ExchangeUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("rowTemplate").objectReferenceValue = er;
        so.FindProperty("walletText").objectReferenceValue = wallet;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false); // 편집 시 Hierarchy에서 켜서 보기
        return Save(canvas, "UI/HUD_Exchange");
    }
}

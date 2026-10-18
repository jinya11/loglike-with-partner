using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.UI;

/// <summary>
/// 메타 성장 업데이트: 맵별 자원, 자원 드롭, 거점 성장(로비 거점 핵 + HUD_BaseUpgrade), 감시자 해금 비용, 자원 획득 알림.
/// 기존 편집은 유지 (없는 것만 추가). 메뉴: Roguelike/Apply Meta Progression Update
/// </summary>
public static partial class PrototypeBuilder
{
    static readonly (string scene, ResourceType res)[] SceneResources =
    {
        ("Stage_Ruins", ResourceType.AncientStone), ("Stage_Snow", ResourceType.FrostCrystal), ("Stage_River", ResourceType.RiverPebble),
        ("Stage_Volcano", ResourceType.MagmaStone), ("Bonus_Candy", ResourceType.SugarCrystal),
    };

    [MenuItem("Roguelike/Apply Meta Progression Update")]
    public static void ApplyMetaUpdate()
    {
        EnsureFonts();
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");

        if (cfg.resources == null || cfg.resources.Length == 0)
            cfg.resources = new[]
            {
                new ResourceDef { type = ResourceType.AncientStone, name = "고대 석재", color = new Color(0.8f, 0.75f, 0.6f) },
                new ResourceDef { type = ResourceType.FrostCrystal, name = "서리 결정", color = new Color(0.6f, 0.9f, 1f) },
                new ResourceDef { type = ResourceType.RiverPebble, name = "강의 조약돌", color = new Color(0.3f, 0.8f, 0.75f) },
                new ResourceDef { type = ResourceType.MagmaStone, name = "용암석", color = new Color(1f, 0.4f, 0.1f) },
                new ResourceDef { type = ResourceType.SugarCrystal, name = "설탕 결정", color = new Color(1f, 0.6f, 0.85f) },
            };
        if (cfg.baseUpgrades == null || cfg.baseUpgrades.Length == 0)
            cfg.baseUpgrades = new[]
            {
                BU("ward_hp", "생명의 샘", "생명체 최대 체력 +10", StatType.WardMaxHp, 10f, ResourceType.AncientStone, 3),
                BU("ward_regen", "서리 방벽", "생명체 초당 회복 +0.15", StatType.WardRegen, 0.15f, ResourceType.FrostCrystal, 3),
                BU("pickup", "강의 축복", "획득 범위 +0.5", StatType.Pickup, 0.5f, ResourceType.RiverPebble, 3),
                BU("damage", "용암 단련", "공격력 +1", StatType.Damage, 1f, ResourceType.MagmaStone, 3),
                BU("companion", "달콤한 공물", "감시자 피해 +3", StatType.CompanionDamage, 3f, ResourceType.SugarCrystal, 2),
            };
        foreach (var c in cfg.companions)
            if (c.name == "풍신" && (c.unlockCost == null || c.unlockCost.Length == 0))
                c.unlockCost = new[] { new ResourceCost { type = ResourceType.RiverPebble, amount = 5 }, new ResourceCost { type = ResourceType.FrostCrystal, amount = 5 } };
        cfg.resourcePickupPrefab = BuildResourcePickup();
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        AddResourceToast();
        var upgradeHud = BuildBaseUpgradeHud(cfg);

        // 씬: 스테이지별 자원 지정 + 로비에 거점 핵. 열린 씬에 저장 안 된 편집이 있으면 씬 작업은 건너뜀.
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) { Debug.LogWarning("[Meta] 열린 씬에 저장 안 된 변경이 있어 씬 작업을 건너뜀. 저장 후 다시 실행하세요."); return; }
        string back = active.path;
        foreach (var (scene, res) in SceneResources)
        {
            var s = EditorSceneManager.OpenScene($"Assets/Scenes/{scene}.unity", OpenSceneMode.Single);
            var theme = Object.FindAnyObjectByType<StageTheme>();
            if (theme && theme.resource != res) { theme.resource = res; EditorUtility.SetDirty(theme); EditorSceneManager.SaveScene(s); }
        }
        var lobby = EditorSceneManager.OpenScene($"Assets/Scenes/{cfg.lobbyScene}.unity", OpenSceneMode.Single);
        if (Object.FindAnyObjectByType<BaseUpgradeStation>() == null) { AddBaseCore(upgradeHud); EditorSceneManager.SaveScene(lobby); }
        if (!string.IsNullOrEmpty(back) && back != lobby.path) EditorSceneManager.OpenScene(back);
        Debug.Log("[PrototypeBuilder] 메타 성장 업데이트 완료");
    }

    static BaseUpgradeDef BU(string id, string n, string d, StatType s, float v, ResourceType r, int cost) =>
        new BaseUpgradeDef { id = id, name = n, desc = d, stat = s, valuePerLevel = v, maxLevel = 5, cost = new ResourceCost { type = r, amount = cost }, costGrowth = 2 };

    static ResourcePickup BuildResourcePickup()
    {
        var go = new GameObject("ResourcePickup");
        var m = Mat("ResourceCrystal", Color.white);
        var a = Prim(PrimitiveType.Cube, "Crystal", go.transform, new Vector3(0, 0.55f, 0), new Vector3(0.4f, 0.4f, 0.4f), m);
        a.transform.localRotation = Quaternion.Euler(45, 0, 45);
        var b = Prim(PrimitiveType.Cube, "Shard", go.transform, new Vector3(0, 0.55f, 0), new Vector3(0.18f, 0.7f, 0.18f), m);
        var p = go.AddComponent<ResourcePickup>();
        var so = new SerializedObject(p);
        SetArray(so.FindProperty("tint"), new Object[] { a.GetComponent<Renderer>(), b.GetComponent<Renderer>() });
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(go, "ResourcePickup").GetComponent<ResourcePickup>();
    }

    /// <summary>HUD_Stage에 "자원 이름 획득" 알림 (화면 중앙에서 살짝 오른쪽).</summary>
    static void AddResourceToast()
    {
        string path = Root + "/Prefabs/UI/HUD_Stage.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so = new SerializedObject(root.GetComponent<Hud>());
            if (so.FindProperty("resourceToast").objectReferenceValue == null)
            {
                var t = Txt("ResourceToast", root.transform, "{0} 획득", 34, TextAlignmentOptions.Left, new Color(1f, 0.9f, 0.55f), C, C, new Vector2(140, 60), new Vector2(600, 60), new Vector2(0f, 0.5f));
                var g = t.gameObject.AddComponent<CanvasGroup>();
                so.FindProperty("resourceToast").objectReferenceValue = t;
                so.FindProperty("resourceToastGroup").objectReferenceValue = g;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>거점 성장 화면 (항목/자원 줄은 프리팹 안에 개별 배치 → 직접 편집 가능).</summary>
    static GameObject BuildBaseUpgradeHud(GameConfig cfg)
    {
        string path = Root + "/Prefabs/UI/HUD_BaseUpgrade.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing; // 이미 있으면 사용자 편집 유지

        var canvas = NewCanvas("HUD_BaseUpgrade");
        canvas.GetComponent<Canvas>().sortingOrder = 10;
        var panel = Stretch("Panel", canvas.transform); Img(panel, new Color(0.06f, 0.055f, 0.08f, 0.96f));
        var line = UI("TopLine", panel, TC, TC, new Vector2(0, -50), new Vector2(1500, 3)); Img(line, Gold);
        Txt("Title", panel, "거점 성장", 60, TextAlignmentOptions.Center, Gold, TC, TC, new Vector2(0, -110), new Vector2(1200, 80));
        Txt("Subtitle", panel, "맵에서 모은 자원으로 거점을 키우면 모든 런에 영구히 적용됩니다.", 22, TextAlignmentOptions.Center, Soft, TC, TC, new Vector2(0, -168), new Vector2(1400, 40));

        // 보유 자원
        for (int i = 0; i < cfg.resources.Length; i++)
        {
            var r = cfg.resources[i];
            var t = Txt($"Res_{r.name}", panel, "{0}  {1}", 24, TextAlignmentOptions.Center, r.color, TC, TC, new Vector2((i - (cfg.resources.Length - 1) * 0.5f) * 260f, -220), new Vector2(250, 36));
            var rl = t.gameObject.AddComponent<ResourceLabel>();
            var rso = new SerializedObject(rl);
            rso.FindProperty("type").enumValueIndex = (int)r.type;
            rso.FindProperty("text").objectReferenceValue = t;
            rso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 성장 항목
        for (int i = 0; i < cfg.baseUpgrades.Length; i++)
        {
            var b = cfg.baseUpgrades[i];
            var row = UI($"Row_{b.name}", panel, TC, TC, new Vector2(0, -300 - i * 118), new Vector2(1300, 104));
            Img(row, new Color(0.15f, 0.14f, 0.18f));
            Txt("Name", row, b.name, 30, TextAlignmentOptions.Left, Color.white, TL, TL, new Vector2(30, -14), new Vector2(420, 40), TL);
            Txt("Desc", row, b.desc, 20, TextAlignmentOptions.Left, Soft, TL, TL, new Vector2(30, -58), new Vector2(520, 32), TL, false);
            var lv = Txt("Level", row, "Lv {0} / {1}", 26, TextAlignmentOptions.Center, Gold, C, C, new Vector2(-20, 0), new Vector2(220, 40));
            var cost = Txt("Cost", row, "{0} {1}개 (보유 {2})", 22, TextAlignmentOptions.Right, Soft, C, C, new Vector2(270, 0), new Vector2(340, 40));
            var btnRt = UI("BuyButton", row, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(150, 64));
            var btnImg = Img(btnRt, new Color(0.35f, 0.3f, 0.15f));
            var btn = btnRt.gameObject.AddComponent<Button>(); btn.targetGraphic = btnImg;
            Txt("Label", btnRt, "성장", 26, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var br = row.gameObject.AddComponent<BaseUpgradeRow>();
            var bso = new SerializedObject(br);
            bso.FindProperty("upgradeId").stringValue = b.id;
            bso.FindProperty("levelText").objectReferenceValue = lv;
            bso.FindProperty("costText").objectReferenceValue = cost;
            bso.FindProperty("buyButton").objectReferenceValue = btn;
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        var close = UI("CloseButton", panel, BC, BC, new Vector2(0, 60), new Vector2(300, 64));
        var closeImg = Img(close, new Color(0.25f, 0.23f, 0.28f));
        close.gameObject.AddComponent<Button>().targetGraphic = closeImg;
        Txt("Label", close, "닫기 (F / Esc)", 24, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        panel.gameObject.SetActive(false);
        return Save(canvas, "UI/HUD_BaseUpgrade");
    }

    /// <summary>로비 서쪽(구 무기 자리)에 거점 핵.</summary>
    static void AddBaseCore(GameObject hudPrefab)
    {
        var lobbyRoot = GameObject.Find("Lobby");
        var root = new GameObject("BaseCore").transform;
        if (lobbyRoot) root.SetParent(lobbyRoot.transform, false);
        root.localPosition = new Vector3(-11.5f, 0f, 0.5f);
        var stone = Mat("LobbyStone", new Color(0.33f, 0.31f, 0.36f));
        var gold = Mat("LobbyGold", new Color(0.9f, 0.72f, 0.35f));
        var core = Mat("BaseCoreCrystal", new Color(1f, 0.85f, 0.45f));
        PB(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 16, 1.6f, 0.8f, 0), "Pedestal", root, new Vector3(0, 0.4f, 0), stone, true);
        PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, 1.7f, 0.05f, 0.2f, 24, 0), "Ring", root, new Vector3(0, 0.83f, 0), gold, false);
        Prim(PrimitiveType.Cube, "Crystal", root, new Vector3(0, 2.1f, 0), new Vector3(0.9f, 0.9f, 0.9f), core).transform.localRotation = Quaternion.Euler(45, 0, 45);
        Prim(PrimitiveType.Cube, "CrystalTop", root, new Vector3(0, 2.1f, 0), new Vector3(0.35f, 1.8f, 0.35f), core);
        var l = new GameObject("CoreLight").AddComponent<Light>();
        l.type = LightType.Point; l.range = 6f; l.intensity = 4f; l.color = new Color(1f, 0.85f, 0.5f);
        l.transform.SetParent(root, false); l.transform.localPosition = new Vector3(0, 2.2f, 0);

        var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
        var station = root.gameObject.AddComponent<BaseUpgradeStation>();
        var so = new SerializedObject(station);
        var panel = hud.transform.Find("Panel");
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("closeButton").objectReferenceValue = panel.Find("CloseButton").GetComponent<Button>();
        var lobbyHud = GameObject.Find("HUD_Lobby");
        if (lobbyHud) SetArray(so.FindProperty("hideCanvases"), new Object[] { lobbyHud.GetComponent<Canvas>() });
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

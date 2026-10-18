using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

/// <summary>기본 도형 + ProBuilder로 프로토타입 에셋/프리팹/씬을 생성. 메뉴: Roguelike/Build Prototype (재실행 시 씬 덮어씀)</summary>
public static partial class PrototypeBuilder
{
    const string Root = "Assets/Prototype";
    const float StageRadius = 45f, WallThickness = 1.5f, WallHeight = 1.2f;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static int enemyLayer, playerLayer;
    static GameObject stageHud, lobbyHud;

    [MenuItem("Roguelike/Build Prototype")]
    public static void Build()
    {
        foreach (var f in new[] { Root, Root + "/Materials", Root + "/Prefabs", Root + "/Prefabs/Presets", Root + "/Prefabs/UI", Root + "/Data", Root + "/Resources" })
            if (!AssetDatabase.IsValidFolder(f))
                AssetDatabase.CreateFolder(f.Substring(0, f.LastIndexOf('/')), f.Substring(f.LastIndexOf('/') + 1));
        enemyLayer = EnsureLayer("Enemy");
        playerLayer = EnsureLayer("Player");
        foreach (var old in new[] { "Assets/Scenes/Stage_01.unity", "Assets/Scenes/Stage_02.unity", Root + "/Data/Layout_A.asset", Root + "/Data/Layout_B.asset" })
            AssetDatabase.DeleteAsset(old);

        // ---- 전투 프리팹 ----
        var enemyGo = new GameObject("Enemy") { layer = enemyLayer };
        var col = enemyGo.AddComponent<CapsuleCollider>(); col.radius = 0.45f; col.height = 1f; col.center = new Vector3(0, 0.5f, 0);
        col.sharedMaterial = Frictionless();
        var rb = enemyGo.AddComponent<Rigidbody>();
        rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        var visual = new GameObject("Visual") { layer = enemyLayer }.transform; visual.SetParent(enemyGo.transform, false);
        Prim(PrimitiveType.Cube, "Body", visual, new Vector3(0, 0.45f, 0), Vector3.one * 0.9f, Mat("Enemy", new Color(0.85f, 0.2f, 0.25f)));
        Prim(PrimitiveType.Cube, "Eye", visual, new Vector3(0, 0.65f, 0.45f), new Vector3(0.5f, 0.15f, 0.05f), Mat("EnemyEye", Color.yellow));
        Set(enemyGo.AddComponent<Enemy>(), "visual", visual);
        var enemy = Save(enemyGo, "Enemy").GetComponent<Enemy>();

        var projGo = new GameObject("Projectile");
        projGo.AddComponent<Projectile>();
        Prim(PrimitiveType.Sphere, "Ball", projGo.transform, Vector3.zero, Vector3.one * 0.35f, Mat("Projectile", new Color(1f, 0.9f, 0.3f)));
        var proj = Save(projGo, "Projectile").GetComponent<Projectile>();

        var gemGo = new GameObject("XpGem");
        gemGo.AddComponent<XpGem>();
        Prim(PrimitiveType.Cube, "Gem", gemGo.transform, new Vector3(0, 0.3f, 0), Vector3.one * 0.3f, Mat("Gem", new Color(0.3f, 1f, 0.7f)));
        var gem = Save(gemGo, "XpGem").GetComponent<XpGem>();

        var fxGo = Prim(PrimitiveType.Cylinder, "Fx", null, Vector3.zero, Vector3.one, Mat("Fx", Color.white));
        fxGo.AddComponent<Fx>();
        var fx = Save(fxGo, "Fx").GetComponent<Fx>();

        var portalGo = new GameObject("Portal");
        Prim(PrimitiveType.Cylinder, "Ring", portalGo.transform, new Vector3(0, 0.05f, 0), new Vector3(3f, 0.05f, 3f), Mat("PortalRing", new Color(0.5f, 0.2f, 0.9f)));
        Prim(PrimitiveType.Cylinder, "Core", portalGo.transform, new Vector3(0, 1.5f, 0), new Vector3(1.6f, 1.5f, 1.6f), Mat("PortalCore", new Color(0.75f, 0.5f, 1f)));
        var portal = Save(portalGo, "Portal");

        // ---- 데이터 ----
        var cfg = Asset<GameConfig>(Root + "/Resources/GameConfig.asset");
        cfg.enemyPrefab = enemy; cfg.projectilePrefab = proj; cfg.gemPrefab = gem; cfg.fxPrefab = fx; cfg.portalPrefab = portal;
        cfg.stageScenes = new[] { "Stage_Ruins", "Stage_Snow", "Stage_River", "Stage_Volcano" };
        cfg.bonusScenes = new[] { "Bonus_Candy" };
        if (cfg.upgrades == null || cfg.upgrades.Length == 0) cfg.upgrades = DefaultUpgrades();
        foreach (var u in cfg.upgrades) if (u.weight <= 0f) u.weight = 1f;
        if (System.Array.FindIndex(cfg.upgrades, u => u.stat == StatType.Jump) < 0)
            ArrayUtility.Add(ref cfg.upgrades, new UpgradeDef { name = "도약", desc = "점프 해금 (Space)\n공중에서는 접촉 피해와 장판을 피함", stat = StatType.Jump, value = 1f, weight = 0.05f });
        BuildLobbyData(cfg);
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();

        stageHud = BuildStageHud();
        cfgForUI = cfg;
        lobbyHud = BuildLobbyHud();

        // ---- 씬 ----
        string lobby = $"Assets/Scenes/{cfg.lobbyScene}.unity";
        BuildLobby(lobby, cfg);
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(lobby, true) };
        foreach (var t in Themes())
        {
            string path = $"Assets/Scenes/{t.scene}.unity";
            BuildStage(path, t);
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.OpenScene(lobby);
        Debug.Log("[PrototypeBuilder] 완료");
    }

    // =====================================================================
    // 스테이지 컨셉
    // =====================================================================
    class Theme
    {
        public string scene, name;
        public Color floor, wall;
        public float regen, killMul = 1f, centerSafe = 6f;
        public PresetEntry[] entries;
    }

    static PresetEntry E(GameObject p, int min, int max, float r0, float r1, float spacing, float s0 = 1f, float s1 = 1f, bool rot = true)
        => new PresetEntry { prefab = p, minCount = min, maxCount = max, minRadius = r0, maxRadius = r1, spacing = spacing, scaleRange = new Vector2(s0, s1), randomRotation = rot };

    static List<Theme> Themes()
    {
        var rock = Mat("Rock", new Color(0.45f, 0.42f, 0.4f));
        var list = new List<Theme>();

        // 폐허: 기본
        var pillar = Preset("Ruins_Pillar", t => Prim(PrimitiveType.Cylinder, "Pillar", t, new Vector3(0, 2f, 0), new Vector3(1.4f, 2f, 1.4f), rock, true));
        var cluster = Preset("Ruins_Rocks", t =>
        {
            Prim(PrimitiveType.Cube, "A", t, new Vector3(0, 0.6f, 0), new Vector3(1.6f, 1.2f, 1.4f), rock, true).transform.localRotation = Quaternion.Euler(0, 20, 0);
            Prim(PrimitiveType.Cube, "B", t, new Vector3(1.3f, 0.4f, 0.6f), new Vector3(1f, 0.8f, 1f), rock, true).transform.localRotation = Quaternion.Euler(0, 50, 0);
            Prim(PrimitiveType.Cube, "C", t, new Vector3(-0.9f, 0.35f, 1f), new Vector3(0.8f, 0.7f, 0.9f), rock, true);
        });
        var wall = Preset("Ruins_Wall", t => Prim(PrimitiveType.Cube, "Wall", t, new Vector3(0, 0.9f, 0), new Vector3(6f, 1.8f, 1f), Mat("RuinWall", new Color(0.38f, 0.35f, 0.4f)), true));
        list.Add(new Theme { scene = "Stage_Ruins", name = "폐허", floor = new Color(0.22f, 0.24f, 0.28f), wall = new Color(0.3f, 0.29f, 0.33f),
            entries = new[] { E(pillar, 8, 12, 0.2f, 0.9f, 5f, 0.8f, 1.3f), E(cluster, 6, 10, 0.15f, 0.95f, 4f, 0.8f, 1.3f), E(wall, 5, 8, 0.3f, 0.85f, 7f) } });

        // 설원: 이동속도 감소 눈밭
        var snow = Zone("Snow_Drift", ZoneType.Slow, 0.5f, false, new Color(0.92f, 0.95f, 1f), new Vector2(9f, 9f));
        var pine = Preset("Snow_Pine", t =>
        {
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.75f, 0.5f), Mat("Trunk", new Color(0.35f, 0.25f, 0.18f)), true);
            var leaf = Mat("Pine", new Color(0.2f, 0.4f, 0.32f));
            Prim(PrimitiveType.Cube, "Low", t, new Vector3(0, 2f, 0), new Vector3(2.2f, 1.2f, 2.2f), leaf).transform.localRotation = Quaternion.Euler(0, 45, 0);
            Prim(PrimitiveType.Cube, "Mid", t, new Vector3(0, 3f, 0), new Vector3(1.6f, 1f, 1.6f), leaf);
            Prim(PrimitiveType.Cube, "Top", t, new Vector3(0, 3.8f, 0), new Vector3(0.9f, 0.8f, 0.9f), leaf).transform.localRotation = Quaternion.Euler(0, 45, 0);
            Prim(PrimitiveType.Cube, "SnowCap", t, new Vector3(0, 4.25f, 0), new Vector3(0.6f, 0.2f, 0.6f), Mat("SnowWhite", Color.white));
        });
        var ice = Preset("Snow_Ice", t =>
        {
            var m = Mat("Ice", new Color(0.65f, 0.85f, 0.95f));
            Prim(PrimitiveType.Cube, "A", t, new Vector3(0, 0.9f, 0), new Vector3(1f, 1.8f, 1f), m, true).transform.localRotation = Quaternion.Euler(10, 30, 8);
            Prim(PrimitiveType.Cube, "B", t, new Vector3(0.8f, 0.6f, 0.4f), new Vector3(0.7f, 1.2f, 0.7f), m, true).transform.localRotation = Quaternion.Euler(-12, 60, 0);
        });
        list.Add(new Theme { scene = "Stage_Snow", name = "설원", floor = new Color(0.55f, 0.62f, 0.72f), wall = new Color(0.6f, 0.68f, 0.78f),
            entries = new[] { E(snow, 5, 8, 0.15f, 0.85f, 10f, 0.8f, 1.5f), E(pine, 10, 16, 0.2f, 0.95f, 4f, 0.8f, 1.4f), E(ice, 5, 9, 0.2f, 0.95f, 4f) } });

        // 강가: 강줄기와 웅덩이 (닿으면 물보라)
        var water = new Color(0.2f, 0.5f, 0.85f);
        var river = Zone("River_Stream", ZoneType.Water, 0.75f, true, water, new Vector2(7f, StageRadius * 2f));
        var pond = Zone("River_Pond", ZoneType.Water, 0.75f, false, water, new Vector2(7f, 7f));
        var reeds = Preset("River_Reeds", t =>
        {
            var m = Mat("Reed", new Color(0.45f, 0.6f, 0.3f));
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cylinder, "Reed" + i, t, new Vector3(Mathf.Cos(i * 1.3f) * 0.6f, 0.8f, Mathf.Sin(i * 1.3f) * 0.6f), new Vector3(0.12f, 0.8f + i * 0.08f, 0.12f), m);
        });
        list.Add(new Theme { scene = "Stage_River", name = "강가", floor = new Color(0.3f, 0.42f, 0.28f), wall = new Color(0.35f, 0.33f, 0.3f), centerSafe = 0f,
            entries = new[] { E(river, 1, 1, 0f, 0.01f, 0f), E(pond, 3, 5, 0.3f, 0.85f, 9f, 0.8f, 1.4f), E(reeds, 10, 16, 0.15f, 0.95f, 3f), E(cluster, 4, 7, 0.2f, 0.95f, 5f) } });

        // 화산: 용암 웅덩이 지속 피해
        var lava = Zone("Volcano_Lava", ZoneType.Damage, 10f, false, new Color(1f, 0.35f, 0.05f), new Vector2(6f, 6f));
        var obsidian = Preset("Volcano_Obsidian", t =>
        {
            var m = Mat("Obsidian", new Color(0.08f, 0.07f, 0.09f));
            Prim(PrimitiveType.Cube, "A", t, new Vector3(0, 1.2f, 0), new Vector3(1.2f, 2.4f, 1.2f), m, true).transform.localRotation = Quaternion.Euler(8, 25, -6);
            Prim(PrimitiveType.Cube, "B", t, new Vector3(1f, 0.7f, -0.5f), new Vector3(0.9f, 1.4f, 0.9f), m, true).transform.localRotation = Quaternion.Euler(-10, 70, 5);
        });
        var vent = Preset("Volcano_Vent", t =>
        {
            Prim(PrimitiveType.Cylinder, "Cone", t, new Vector3(0, 0.6f, 0), new Vector3(2.4f, 0.6f, 2.4f), Mat("VentRock", new Color(0.25f, 0.15f, 0.12f)), true);
            Prim(PrimitiveType.Sphere, "Glow", t, new Vector3(0, 1.2f, 0), new Vector3(1.2f, 0.3f, 1.2f), Mat("Magma", new Color(1f, 0.5f, 0.1f)));
        });
        list.Add(new Theme { scene = "Stage_Volcano", name = "화산", floor = new Color(0.22f, 0.13f, 0.11f), wall = new Color(0.15f, 0.1f, 0.1f),
            entries = new[] { E(lava, 6, 10, 0.2f, 0.9f, 9f, 0.7f, 1.5f), E(obsidian, 8, 12, 0.15f, 0.95f, 4f, 0.8f, 1.4f), E(vent, 4, 6, 0.25f, 0.9f, 6f) } });

        // 보너스: 과자의 집 (상시 회복)
        var cane = Preset("Candy_Cane", t =>
        {
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Cylinder, "Seg" + i, t, new Vector3(0, 0.25f + i * 0.5f, 0), new Vector3(0.6f, 0.25f, 0.6f), i % 2 == 0 ? Mat("CandyRed", new Color(0.95f, 0.2f, 0.3f)) : Mat("CandyWhite", Color.white), i == 0);
        });
        var gumdrop = Preset("Candy_Gumdrop", t =>
        {
            Color[] c = { new Color(0.4f, 0.9f, 0.5f), new Color(1f, 0.6f, 0.2f), new Color(0.6f, 0.4f, 1f) };
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Sphere, "Drop" + i, t, new Vector3(i * 1.1f - 1.1f, 0.4f, (i % 2) * 0.8f), new Vector3(1f, 0.8f, 1f), Mat("Gum" + i, c[i]), true);
        });
        var house = Preset("Candy_House", t =>
        {
            Prim(PrimitiveType.Cube, "Walls", t, new Vector3(0, 1.2f, 0), new Vector3(3.5f, 2.4f, 3f), Mat("Cookie", new Color(0.72f, 0.48f, 0.26f)), true);
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0, 2.9f, 0), new Vector3(2.6f, 2.6f, 3.2f), Mat("Frosting", new Color(1f, 0.75f, 0.85f))).transform.localRotation = Quaternion.Euler(0, 0, 45);
            Prim(PrimitiveType.Cube, "Door", t, new Vector3(0, 0.6f, -1.51f), new Vector3(0.8f, 1.2f, 0.05f), Mat("Choco", new Color(0.35f, 0.2f, 0.12f)));
        });
        list.Add(new Theme { scene = "Bonus_Candy", name = "과자의 집", floor = new Color(0.98f, 0.8f, 0.86f), wall = new Color(0.6f, 0.38f, 0.25f), regen = 4f, killMul = 0.5f,
            entries = new[] { E(house, 4, 6, 0.3f, 0.85f, 9f), E(cane, 10, 16, 0.15f, 0.95f, 3f, 0.8f, 1.5f), E(gumdrop, 8, 12, 0.15f, 0.95f, 4f) } });
        return list;
    }

    static void BuildStage(string path, Theme t)
    {
        var scene = BuildBase();
        var arena = new GameObject("Arena").transform;
        float outer = StageRadius + WallThickness;
        // ProBuilder 원형 바닥과 둘러싼 벽 (ProBuilder로 직접 편집 가능)
        PB(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 64, outer, 0.5f, 0), "Floor", arena, new Vector3(0, -0.25f, 0), Mat("Floor_" + t.scene, t.floor), false);
        PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, outer, WallHeight, WallThickness, 64, 0), "Wall", arena, new Vector3(0, WallHeight * 0.5f, 0), Mat("Wall_" + t.scene, t.wall), true);

        var layout = Asset<StageLayout>($"{Root}/Data/Layout_{t.scene}.asset");
        layout.entries = t.entries;
        layout.centerSafeRadius = t.centerSafe;
        EditorUtility.SetDirty(layout);

        var stage = new GameObject("Stage");
        var gen = stage.AddComponent<StageGenerator>();
        var so = new SerializedObject(gen);
        so.FindProperty("radius").floatValue = StageRadius;
        so.FindProperty("layout").objectReferenceValue = layout;
        so.ApplyModifiedPropertiesWithoutUndo();
        var theme = stage.AddComponent<StageTheme>();
        theme.displayName = t.name; theme.ambientRegen = t.regen; theme.killTargetMul = t.killMul;
        Set(stage.AddComponent<StageDirector>(), "generator", gen);
        AddUI(stageHud);
        EditorSceneManager.SaveScene(scene, path);
    }

    // =====================================================================
    // 로비
    // =====================================================================
    static void BuildLobbyData(GameConfig cfg)
    {
        var giant = BuildCompanion();
        if (cfg.companions == null || cfg.companions.Length == 0)
            cfg.companions = new[] { new CompanionDef { name = "거신", desc = "스테이지를 내려다보며 낙뢰로 지원" } };
        cfg.companions[0].prefab = giant;

        if (cfg.modifiers == null || cfg.modifiers.Length == 0)
            cfg.modifiers = new[]
            {
                new ModifierDef { name = "분노의 석판", desc = "몬스터 피해 +50%", type = ModifierType.EnemyDamage, value = 0.5f },
                new ModifierDef { name = "강철의 석판", desc = "몬스터 체력 +50%", type = ModifierType.EnemyHp, value = 0.5f },
                new ModifierDef { name = "질주의 석판", desc = "몬스터 속도 +25%", type = ModifierType.EnemySpeed, value = 0.25f },
                new ModifierDef { name = "군세의 석판", desc = "스폰 수 +2", type = ModifierType.MoreEnemies, value = 2f },
                new ModifierDef { name = "고갈의 석판", desc = "체력 회복 불가", type = ModifierType.NoHealing },
                new ModifierDef { name = "재앙의 석판", desc = "맵에 위험 장판 패턴 추가", type = ModifierType.Hazards },
            };
    }

    /// <summary>로비: 중앙 출발 포털, 북쪽 난이도의 석판(제단 위), 동쪽 동반자 제단. ProBuilder 지형.</summary>
    static void BuildLobby(string path, GameConfig cfg)
    {
        var scene = BuildBase();
        const float R = 18f;
        var root = new GameObject("Lobby").transform;
        AddUI(lobbyHud);
        var stone = Mat("LobbyStone", new Color(0.33f, 0.31f, 0.36f));
        var dark = Mat("LobbyDark", new Color(0.18f, 0.17f, 0.21f));
        var gold = Mat("LobbyGold", new Color(0.9f, 0.72f, 0.35f));
        var rune = Mat("Rune", Color.white);
        var fire = Mat("Fire", new Color(1f, 0.55f, 0.15f));

        // 지형
        PB(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 48, R + 1.5f, 0.6f, 0), "Floor", root, new Vector3(0, -0.3f, 0), Mat("LobbyFloor", new Color(0.22f, 0.21f, 0.25f)), false);
        PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, R + 1.5f, 1.4f, 1.5f, 48, 0), "OuterWall", root, new Vector3(0, 0.7f, 0), stone, true);
        PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, 6f, 0.04f, 0.35f, 48, 0), "GoldInlay", root, new Vector3(0, 0.02f, 0), gold, false);
        PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, 12.5f, 0.04f, 0.25f, 48, 0), "GoldInlay2", root, new Vector3(0, 0.02f, 0), gold, false);

        // 기둥 (카메라 쪽 남쪽은 시야 확보를 위해 생략)
        for (int a = 0; a < 360; a += 30)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(a, 180)) < 50) continue;
            float rad = a * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(rad) * (R - 1f), 0, Mathf.Cos(rad) * (R - 1f));
            PB(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 12, 0.7f, 7f, 0), "Pillar", root, p + Vector3.up * 3.5f, stone, true);
            PB(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2f, 0.5f, 2f)), "Cap", root, p + Vector3.up * 7.25f, dark, false);
            PB(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(1.8f, 0.4f, 1.8f)), "Base", root, p + Vector3.up * 0.2f, dark, false);
        }

        // 화로
        foreach (var a in new[] { 45f, 135f, 225f, 315f })
        {
            float rad = a * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(rad) * 12.5f, 0, Mathf.Cos(rad) * 12.5f);
            PB(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 10, 0.5f, 1.2f, 0), "Brazier", root, p + Vector3.up * 0.6f, dark, true);
            Prim(PrimitiveType.Sphere, "Flame", root, p + Vector3.up * 1.4f, new Vector3(0.8f, 0.6f, 0.8f), fire);
        }

        // 북쪽: 난이도의 석판 (계단 + 제단 + 거대 석판 하나, 룬이 상시 발광)
        const float tz = 10f;
        var tabletStone = Mat("TabletStone", new Color(0.5f, 0.48f, 0.55f));
        PB(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(8f, 1f, 4f)), "Dais", root, new Vector3(0, 0.5f, tz), stone, true);
        PB(ShapeGenerator.GenerateStair(PivotLocation.Center, new Vector3(4f, 1f, 2.5f), 4, true), "Stairs", root, new Vector3(0, 0.5f, tz - 3.25f), dark, true);
        PB(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(5f, 0.5f, 1.6f)), "TabletBase", root, new Vector3(0, 1.25f, tz + 0.5f), dark, false);
        PB(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(4.4f, 7f, 0.9f)), "GreatTablet", root, new Vector3(0, 5f, tz + 0.5f), tabletStone, true);
        PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, 3.4f, 0.3f, 0.35f, 32, 0), "Halo", root, new Vector3(0, 8.6f, tz + 0.8f), gold, false).transform.rotation = Quaternion.Euler(90, 0, 0);
        var tabletRune = Prim(PrimitiveType.Cube, "Rune", root, new Vector3(0, 5.2f, tz - 0.01f), new Vector3(1.2f, 4.6f, 0.05f), rune);
        for (int i = 0; i < 6; i++) // 석판 테두리 룬 장식
            Prim(PrimitiveType.Cube, "Glyph" + i, root, new Vector3(i < 3 ? -1.6f : 1.6f, 2.8f + (i % 3) * 2f, tz - 0.01f), new Vector3(0.5f, 0.5f, 0.04f), gold).transform.rotation = Quaternion.Euler(0, 0, 45);
        var tl = new GameObject("TabletLight").AddComponent<Light>();
        tl.type = LightType.Point; tl.range = 9f; tl.intensity = 6f; tl.color = new Color(0.6f, 0.85f, 1f);
        tl.transform.SetParent(root, false); tl.transform.localPosition = new Vector3(0, 4f, tz - 3f);
        var tablet = new GameObject("DifficultyTablet").transform;
        tablet.SetParent(root, false); tablet.localPosition = new Vector3(0, 0, tz - 4.8f);
        Interactable(tablet.gameObject, LobbyInteractable.Kind.Modifier, 0, tabletRune.GetComponent<Renderer>(), new Color(1f, 0.3f, 0.2f), new Color(0.35f, 0.8f, 1f));
        // 동쪽: 동반자 제단 + 아치
        var cm = Mat("Companion", new Color(0.18f, 0.16f, 0.25f));
        for (int i = 0; i < cfg.companions.Length; i++)
        {
            var t = new GameObject("Companion_" + cfg.companions[i].name).transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(11.5f, 0, 1f - i * 6f);
            PB(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 24, 2.6f, 0.25f, 0), "Altar", t, new Vector3(0, 0.125f, 0), stone, true);
            var top = PB(ShapeGenerator.GeneratePipe(PivotLocation.Center, 2.6f, 0.04f, 0.2f, 24, 0), "Ring", t, new Vector3(0, 0.27f, 0), rune, false);
            var arch = PB(ShapeGenerator.GenerateArch(PivotLocation.Center, 180f, 3.2f, 0.6f, 0.8f, 12, true, true, true, true, true), "Arch", t, new Vector3(1.8f, 1.6f, 0), stone, false);
            arch.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Prim(PrimitiveType.Cylinder, "MiniTorso", t, new Vector3(0.8f, 1.2f, 0), new Vector3(1f, 0.8f, 0.7f), cm);
            Prim(PrimitiveType.Sphere, "MiniHead", t, new Vector3(0.8f, 2.4f, 0), new Vector3(0.8f, 0.85f, 0.8f), cm);
            Prim(PrimitiveType.Sphere, "MiniEyeL", t, new Vector3(0.5f, 2.45f, -0.18f), new Vector3(0.12f, 0.1f, 0.1f), Mat("CompanionEye", Color.white));
            Prim(PrimitiveType.Sphere, "MiniEyeR", t, new Vector3(0.5f, 2.45f, 0.18f), new Vector3(0.12f, 0.1f, 0.1f), Mat("CompanionEye", Color.white));
            Interactable(t.gameObject, LobbyInteractable.Kind.Companion, i, top.GetComponent<Renderer>(), new Color(0.75f, 0.6f, 1f));
        }

        // 중앙: 출발 포털 + 아치
        var portal = (GameObject)PrefabUtility.InstantiatePrefab(cfg.portalPrefab, root);
        portal.name = "StartPortal";
        portal.transform.localPosition = Vector3.zero;
        PB(ShapeGenerator.GenerateArch(PivotLocation.Center, 180f, 2.8f, 0.5f, 0.8f, 12, true, true, true, true, true), "PortalArch", root, new Vector3(0, 1.4f, 0.8f), gold, false);
        Interactable(portal, LobbyInteractable.Kind.Start, 0, null, Color.white);

        GameObject.Find("Player").transform.position = new Vector3(0, 0, -6f);
        EditorSceneManager.SaveScene(scene, path);
    }

    // =====================================================================
    // 공통
    // =====================================================================
    /// <summary>조명, 카메라, 플레이어.</summary>
    static Scene BuildBase()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional; light.shadows = LightShadows.Soft; light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var cam = new GameObject("Main Camera") { tag = "MainCamera" };
        var c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.05f, 0.05f, 0.08f); c.fieldOfView = 60f; c.farClipPlane = 300f;
        cam.AddComponent<AudioListener>();
        cam.AddComponent<CameraFollow>();
        cam.transform.SetPositionAndRotation(new Vector3(0, 11, -17), Quaternion.LookRotation(new Vector3(0, -11, 17)));

        var player = new GameObject("Player") { layer = playerLayer };
        var cc = player.AddComponent<CharacterController>();
        cc.center = new Vector3(0, 1f, 0); cc.height = 2f; cc.radius = 0.45f;
        var body = new GameObject("Body") { layer = playerLayer }.transform; body.SetParent(player.transform, false);
        Prim(PrimitiveType.Capsule, "Capsule", body, new Vector3(0, 1f, 0), new Vector3(0.9f, 1f, 0.9f), Mat("Player", new Color(0.25f, 0.55f, 1f)));
        Prim(PrimitiveType.Cube, "Visor", body, new Vector3(0, 1.5f, 0.4f), new Vector3(0.6f, 0.2f, 0.2f), Mat("Visor", Color.white));
        var pc = player.AddComponent<PlayerController>();
        Set(pc, "body", body);
        return scene;
    }

    /// <summary>거대한 동반자 프리팹: 아레나 벽 너머 북쪽에서 스테이지를 내려다봄.</summary>
    static GameObject BuildCompanion()
    {
        var comp = new GameObject("Companion_Giant");
        var cm = Mat("Companion", new Color(0.18f, 0.16f, 0.25f));
        Prim(PrimitiveType.Cylinder, "Torso", comp.transform, new Vector3(0, 9f, 0), new Vector3(10f, 9f, 6f), cm);
        Prim(PrimitiveType.Sphere, "ShoulderL", comp.transform, new Vector3(-6f, 16f, 0), Vector3.one * 5f, cm);
        Prim(PrimitiveType.Sphere, "ShoulderR", comp.transform, new Vector3(6f, 16f, 0), Vector3.one * 5f, cm);
        var head = new GameObject("Head").transform; head.SetParent(comp.transform, false); head.localPosition = new Vector3(0, 21f, -1f);
        Prim(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(7f, 7.5f, 7f), cm);
        var eyeMat = Mat("CompanionEye", Color.white);
        var eyeL = Prim(PrimitiveType.Sphere, "EyeL", head, new Vector3(-1.4f, 0.5f, 3.1f), new Vector3(1.4f, 0.9f, 0.8f), eyeMat).GetComponent<Renderer>();
        var eyeR = Prim(PrimitiveType.Sphere, "EyeR", head, new Vector3(1.4f, 0.5f, 3.1f), new Vector3(1.4f, 0.9f, 0.8f), eyeMat).GetComponent<Renderer>();
        var compC = comp.AddComponent<Companion>();
        Set(compC, "head", head);
        var so = new SerializedObject(compC);
        var eyes = so.FindProperty("eyes"); eyes.arraySize = 2;
        eyes.GetArrayElementAtIndex(0).objectReferenceValue = eyeL; eyes.GetArrayElementAtIndex(1).objectReferenceValue = eyeR;
        so.FindProperty("distanceFromEdge").floatValue = 14f;
        so.FindProperty("height").floatValue = -43f;
        so.ApplyModifiedPropertiesWithoutUndo();
        comp.transform.rotation = Quaternion.Euler(0, 180f, 0);
        comp.transform.localScale = Vector3.one * 2.2f;
        return Save(comp, "Companion_Giant");
    }

    static GameObject Preset(string name, System.Action<Transform> build)
    {
        var go = new GameObject(name);
        build(go.transform);
        var p = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Presets/{name}.prefab");
        Object.DestroyImmediate(go);
        return p;
    }

    /// <summary>바닥 구역 프리셋. size = (가로, 세로) 월드 크기.</summary>
    static GameObject Zone(string name, ZoneType type, float value, bool box, Color c, Vector2 size)
        => Preset(name, t =>
        {
            var z = Prim(box ? PrimitiveType.Cube : PrimitiveType.Cylinder, "Zone", t, new Vector3(0, 0.02f, 0), new Vector3(size.x, 0.01f, size.y), Mat(name, c));
            var sz = z.AddComponent<StageZone>();
            sz.type = type; sz.value = value; sz.box = box;
        });

    static ProBuilderMesh PB(ProBuilderMesh m, string name, Transform parent, Vector3 pos, Material mat, bool collider)
    {
        m.gameObject.name = name;
        m.transform.SetParent(parent, false);
        m.transform.localPosition = pos;
        m.SetMaterial(m.faces, mat);
        m.ToMesh();
        m.Refresh();
        if (collider) m.gameObject.AddComponent<MeshCollider>();
        return m;
    }

    static UpgradeDef[] DefaultUpgrades() => new[]
    {
        U("날카로움", "공격력 +2", StatType.Damage, 2f, false),
        U("신속", "이동 속도 +0.8", StatType.MoveSpeed, 0.8f, false),
        U("연사", "공격 간격 -12%", StatType.AttackSpeed, 0.12f, false),
        U("강인함", "최대 체력 +20", StatType.MaxHp, 20f, false),
        U("자력", "획득 범위 +1.5", StatType.Pickup, 1.5f, false),
        U("집중", "스킬 쿨타임 -10%", StatType.Cooldown, 0.1f, false),
        U("회복", "체력 40 회복", StatType.Heal, 40f, false),
        U("다중 사격", "투사체 +1", StatType.ProjectileCount, 1f, true),
        U("광역화", "범위 +25%", StatType.Area, 0.25f, true),
        U("거신의 분노", "동반자 낙뢰 피해 +15", StatType.CompanionDamage, 15f, true),
        U("거신의 주시", "동반자 지원 주기 -20%", StatType.CompanionRate, 0.2f, true),
        U("거신의 연격", "동반자 낙뢰 +1회", StatType.CompanionStrikes, 1f, true),
    };

    static UpgradeDef U(string n, string d, StatType s, float v, bool sp) => new UpgradeDef { name = n, desc = d, stat = s, value = v, special = sp };

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (parent) { go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); }
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static Material Mat(string name, Color c)
    {
        if (mats.TryGetValue(name, out var m) && m) return m;
        string path = $"{Root}/Materials/{name}.mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true }; AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return mats[name] = m;
    }

    static PhysicsMaterial Frictionless()
    {
        string path = Root + "/Materials/Frictionless.physicMaterial";
        var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (pm) return pm;
        pm = new PhysicsMaterial("Frictionless") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
        AssetDatabase.CreateAsset(pm, path);
        return pm;
    }

    static GameObject Save(GameObject go, string name)
    {
        var p = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
        Object.DestroyImmediate(go);
        return p;
    }

    static T Asset<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Interactable(GameObject go, LobbyInteractable.Kind kind, int index, Renderer indicator, Color on, Color? off = null)
    {
        var li = go.AddComponent<LobbyInteractable>();
        li.kind = kind; li.index = index;
        var so = new SerializedObject(li);
        so.FindProperty("indicator").objectReferenceValue = indicator;
        so.FindProperty("onColor").colorValue = on;
        if (off.HasValue) so.FindProperty("offColor").colorValue = off.Value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static int EnsureLayer(string name)
    {
        int l = LayerMask.NameToLayer(name);
        if (l >= 0) return l;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(p.stringValue)) continue;
            p.stringValue = name;
            tm.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }
        throw new System.Exception("빈 레이어 없음");
    }
}

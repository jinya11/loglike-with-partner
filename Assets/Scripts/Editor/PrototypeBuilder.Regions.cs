using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 지역 스테이지: 넓은 정사각형 필드 + 셰이더 에너지 보더(ArenaBorder) + 로비 3x3 지역 지도(HUD_RegionSelect).
/// 1번(중앙) = 하늘섬 바로 밑 심연(칠흑 같은 어둠), 2~9번 = 지역 후보 10종에서 로테이션.
/// 기존 Stage_* 씬은 건드리지 않고 Assets/Scenes/Regions/Region_*.unity를 새로 만듦 (재실행 시 Region 씬만 덮어씀).
/// 메뉴: Roguelike/Build Region Stages
/// </summary>
public static partial class PrototypeBuilder
{
    const float RegionHalf = 60f, RegionMaxHalf = 90f, RegionGround = 150f; // RegionMaxHalf: 바깥 풍경/강줄기 길이 기준 (보더는 칸 단위로 열림)
    const string RegionDir = "Assets/Scenes/Regions";
    const string RegionBaseScene = RegionDir + "/RegionBase.unity"; // 템플릿: 카메라/조명/플레이어/EventSystem만 (직접 편집하면 다음 생성부터 반영)

    class RegionTheme
    {
        public string id, name, desc;
        public Color map, floor, light = Color.white, ambient = new Color(0.45f, 0.45f, 0.5f), fog = new Color(0.1f, 0.1f, 0.12f), border = new Color(0.45f, 0.75f, 1.6f);
        public float lightIntensity = 1.2f, fogStart = 45f, fogEnd = 140f, killMul = 1f, centerSafe = 7f;
        public ResourceType resource;
        public bool dark;
        public PresetEntry[] entries;
        public GameObject[] outside; // 보더 밖 풍경 (충돌 없음, 게임플레이 영역 아님)
    }

    [MenuItem("Roguelike/Build Region Stages")]
    public static void BuildRegions()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty && !EditorUtility.DisplayDialog("지역 스테이지 생성", $"'{active.name}' 씬에 저장하지 않은 변경이 있습니다. 씬을 닫고 계속할까요? (변경은 저장되지 않습니다)", "계속", "취소"))
            return;
        string returnTo = active.path;

        EnsureFonts();
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        if (!AssetDatabase.IsValidFolder(RegionDir)) AssetDatabase.CreateFolder("Assets/Scenes", "Regions");
        if (!AssetDatabase.IsValidFolder(Root + "/Shaders")) AssetDatabase.CreateFolder(Root, "Shaders");

        var themes = RegionThemes();
        var center = themes[0];
        var defs = new List<RegionDef>();
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var t in themes)
        {
            string path = $"{RegionDir}/Region_{t.id}.unity";
            BuildRegionScene(path, t);
            if (!buildScenes.Exists(s => s.path == path)) buildScenes.Add(new EditorBuildSettingsScene(path, true));
            defs.Add(new RegionDef { name = t.name, scene = "Region_" + t.id, desc = t.desc, mapColor = t.map });
        }
        // 기존 원형 스테이지(Stage_*)는 빌드 목록에서 제외 → 전투는 지역 씬에서만
        buildScenes.RemoveAll(s => s.path.StartsWith("Assets/Scenes/Stage_"));
        EditorBuildSettings.scenes = buildScenes.ToArray();

        // 씬을 여러 번 열면서 설정 에셋이 언로드될 수 있어 다시 불러옴
        cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var st = cfg.stage;
        st.centerRegion = defs[0];
        st.regionPool = defs.GetRange(1, defs.Count - 1).ToArray();
        st.stageScenes = System.Array.ConvertAll(st.regionPool, r => r.scene); // 지역을 고르지 않고 출발할 때의 대체 목록
        st.regionSelectPrefab = BuildRegionSelectHud();
        EditorUtility.SetDirty(st);
        AssetDatabase.SaveAssetIfDirty(st);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo);
        Debug.Log($"[PrototypeBuilder] 지역 스테이지 {themes.Count}개 생성 (중앙: {center.name})");
    }

    // =====================================================================
    // 지역 컨셉
    // =====================================================================
    static GameObject LoadPreset(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Presets/{name}.prefab");

    static Material Glow(string name, Color c, float intensity)
    {
        var m = Mat(name, c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>보더 밖 풍경용 언덕 (납작한 구, 충돌 없음).</summary>
    static GameObject Hill(string name, Color c) => Preset(name, t =>
    {
        var m = Mat(name, c);
        Prim(PrimitiveType.Sphere, "A", t, new Vector3(0, 0f, 0), new Vector3(16f, 6f, 13f), m);
        Prim(PrimitiveType.Sphere, "B", t, new Vector3(7f, -0.5f, 3f), new Vector3(11f, 4.5f, 10f), m);
    });

    static List<RegionTheme> RegionThemes()
    {
        var list = new List<RegionTheme>();
        float streamLen = RegionMaxHalf * 2f + 20f;

        // ---- 1번: 심연 (하늘섬 바로 밑, 칠흑 같은 어둠) ----
        var spire = Preset("Abyss_Spire", t =>
        {
            var m = Mat("AbyssRock", new Color(0.035f, 0.03f, 0.045f));
            var tip = Glow("AbyssGlow", new Color(0.45f, 0.25f, 1f), 3f);
            Prim(PrimitiveType.Cube, "A", t, new Vector3(0, 1.8f, 0), new Vector3(1.1f, 3.6f, 1.1f), m, true).transform.localRotation = Quaternion.Euler(6, 20, -4);
            Prim(PrimitiveType.Cube, "B", t, new Vector3(0.9f, 1f, 0.4f), new Vector3(0.7f, 2f, 0.7f), m, true).transform.localRotation = Quaternion.Euler(-10, 55, 8);
            Prim(PrimitiveType.Cube, "Vein", t, new Vector3(0.02f, 2.2f, 0.56f), new Vector3(0.12f, 2.4f, 0.05f), tip).transform.localRotation = Quaternion.Euler(6, 20, -4);
        });
        var shroom = Preset("Abyss_Glowshroom", t =>
        {
            var stem = Mat("AbyssStem", new Color(0.12f, 0.12f, 0.16f));
            var cap = Glow("AbyssShroom", new Color(0.2f, 0.9f, 0.85f), 2.5f);
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(Mathf.Cos(i * 2.1f) * 0.6f, 0f, Mathf.Sin(i * 2.1f) * 0.6f);
                float h = 0.4f + i * 0.2f;
                Prim(PrimitiveType.Cylinder, "Stem" + i, t, p + Vector3.up * h * 0.5f, new Vector3(0.12f, h * 0.5f, 0.12f), stem);
                Prim(PrimitiveType.Sphere, "Cap" + i, t, p + Vector3.up * h, new Vector3(0.5f, 0.2f, 0.5f), cap);
            }
        });
        var lantern = Preset("Abyss_Lantern", t =>
        {
            Prim(PrimitiveType.Cylinder, "Post", t, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 1.2f, 0.6f), Mat("AbyssRock", new Color(0.035f, 0.03f, 0.045f)), true);
            Prim(PrimitiveType.Sphere, "Orb", t, new Vector3(0, 2.7f, 0), Vector3.one * 0.6f, Glow("AbyssOrb", new Color(0.55f, 0.6f, 1f), 4f));
            var l = new GameObject("Light").AddComponent<Light>();
            l.type = LightType.Point; l.range = 9f; l.intensity = 2.5f; l.color = new Color(0.5f, 0.55f, 1f); l.shadows = LightShadows.None;
            l.transform.SetParent(t, false); l.transform.localPosition = new Vector3(0, 2.7f, 0);
        });
        var rift = Zone("Abyss_Rift", ZoneType.Damage, 6f, false, new Color(0.25f, 0.05f, 0.35f), new Vector2(5f, 5f));
        list.Add(new RegionTheme
        {
            id = "Abyss", name = "심연", desc = "하늘섬 바로 밑. 칠흑 같은 어둠", dark = true,
            map = new Color(0.02f, 0.02f, 0.03f), floor = new Color(0.02f, 0.02f, 0.025f),
            light = new Color(0.35f, 0.35f, 0.6f), lightIntensity = 0.05f, ambient = new Color(0.015f, 0.015f, 0.025f),
            fog = new Color(0f, 0f, 0f), fogStart = 18f, fogEnd = 75f, border = new Color(0.5f, 0.2f, 1.4f), killMul = 1f,
            entries = new[] { E(spire, 18, 26, 0.12f, 0.98f, 5f, 0.8f, 1.6f), E(shroom, 30, 45, 0.08f, 0.98f, 3f, 0.8f, 1.5f), E(lantern, 10, 14, 0.15f, 0.95f, 14f), E(rift, 8, 12, 0.2f, 0.95f, 8f, 0.8f, 1.4f) },
        });

        // ---- 2~9번 로테이션 후보 ----
        var rock = LoadPreset("Ruins_Rocks");
        list.Add(new RegionTheme
        {
            id = "Ruins", name = "폐허", desc = "무너진 기둥과 성벽", resource = ResourceType.AncientStone,
            map = new Color(0.32f, 0.31f, 0.36f), floor = new Color(0.22f, 0.24f, 0.28f), fog = new Color(0.12f, 0.12f, 0.15f),
            entries = new[] { E(LoadPreset("Ruins_Pillar"), 16, 24, 0.15f, 0.95f, 5f, 0.8f, 1.3f), E(rock, 12, 18, 0.12f, 0.97f, 4f, 0.8f, 1.3f), E(LoadPreset("Ruins_Wall"), 10, 15, 0.25f, 0.92f, 7f) },
            outside = new[] { Hill("Hill_Ruins", new Color(0.26f, 0.27f, 0.31f)) },
        });
        list.Add(new RegionTheme
        {
            id = "Snow", name = "설원", desc = "발이 묶이는 눈밭", resource = ResourceType.FrostCrystal,
            map = new Color(0.7f, 0.78f, 0.88f), floor = new Color(0.55f, 0.62f, 0.72f), light = new Color(0.9f, 0.95f, 1f), ambient = new Color(0.55f, 0.6f, 0.7f), fog = new Color(0.62f, 0.68f, 0.78f), border = new Color(0.6f, 0.9f, 1.6f),
            entries = new[] { E(LoadPreset("Snow_Drift"), 10, 15, 0.12f, 0.9f, 10f, 0.8f, 1.5f), E(LoadPreset("Snow_Pine"), 22, 32, 0.15f, 0.97f, 4f, 0.8f, 1.4f), E(LoadPreset("Snow_Ice"), 10, 16, 0.15f, 0.97f, 4f) },
            outside = new[] { Hill("Hill_Snow", new Color(0.75f, 0.8f, 0.88f)) },
        });
        var stream = Zone("Region_River_Stream", ZoneType.Water, 0.75f, true, new Color(0.2f, 0.5f, 0.85f), new Vector2(8f, streamLen));
        list.Add(new RegionTheme
        {
            id = "River", name = "강가", desc = "필드를 가로지르는 강줄기", resource = ResourceType.RiverPebble, centerSafe = 0f,
            map = new Color(0.28f, 0.45f, 0.4f), floor = new Color(0.3f, 0.42f, 0.28f), fog = new Color(0.25f, 0.33f, 0.3f),
            entries = new[] { E(stream, 1, 1, 0f, 0.01f, 0f), E(LoadPreset("River_Pond"), 6, 10, 0.25f, 0.9f, 9f, 0.8f, 1.4f), E(LoadPreset("River_Reeds"), 22, 32, 0.12f, 0.97f, 3f), E(rock, 8, 14, 0.15f, 0.97f, 5f) },
            outside = new[] { Hill("Hill_River", new Color(0.26f, 0.38f, 0.24f)) },
        });
        list.Add(new RegionTheme
        {
            id = "Volcano", name = "화산", desc = "닿으면 타는 용암 웅덩이", resource = ResourceType.MagmaStone,
            map = new Color(0.5f, 0.18f, 0.1f), floor = new Color(0.22f, 0.13f, 0.11f), light = new Color(1f, 0.75f, 0.6f), ambient = new Color(0.4f, 0.25f, 0.22f), fog = new Color(0.2f, 0.08f, 0.05f), border = new Color(1.6f, 0.5f, 0.2f),
            entries = new[] { E(LoadPreset("Volcano_Lava"), 12, 18, 0.15f, 0.92f, 9f, 0.7f, 1.5f), E(LoadPreset("Volcano_Obsidian"), 16, 24, 0.12f, 0.97f, 4f, 0.8f, 1.4f), E(LoadPreset("Volcano_Vent"), 8, 12, 0.2f, 0.92f, 6f) },
            outside = new[] { Hill("Hill_Volcano", new Color(0.15f, 0.09f, 0.08f)) },
        });

        // 사막: 모래 수렁(감속)
        var sand = new Color(0.78f, 0.64f, 0.42f);
        var cactus = Preset("Desert_Cactus", t =>
        {
            var m = Mat("Cactus", new Color(0.3f, 0.55f, 0.3f));
            Prim(PrimitiveType.Capsule, "Trunk", t, new Vector3(0, 1.5f, 0), new Vector3(0.7f, 1.5f, 0.7f), m, true);
            Prim(PrimitiveType.Capsule, "ArmL", t, new Vector3(-0.6f, 1.8f, 0), new Vector3(0.4f, 0.6f, 0.4f), m);
            Prim(PrimitiveType.Capsule, "ArmR", t, new Vector3(0.6f, 2.2f, 0), new Vector3(0.4f, 0.5f, 0.4f), m);
        });
        var mesa = Preset("Desert_Mesa", t =>
        {
            var m = Mat("Sandstone", new Color(0.72f, 0.48f, 0.32f));
            Prim(PrimitiveType.Cube, "Base", t, new Vector3(0, 0.9f, 0), new Vector3(3.2f, 1.8f, 2.6f), m, true).transform.localRotation = Quaternion.Euler(0, 15, 0);
            Prim(PrimitiveType.Cube, "Top", t, new Vector3(0.3f, 2.3f, 0.1f), new Vector3(2.2f, 1f, 1.8f), Mat("SandstoneLight", new Color(0.82f, 0.6f, 0.42f)), true).transform.localRotation = Quaternion.Euler(0, 30, 0);
        });
        var quicksand = Zone("Desert_Quicksand", ZoneType.Slow, 0.55f, false, new Color(0.62f, 0.5f, 0.32f), new Vector2(8f, 8f));
        list.Add(new RegionTheme
        {
            id = "Desert", name = "사막", desc = "모래 수렁과 붉은 바위", resource = ResourceType.AncientStone,
            map = sand, floor = sand, light = new Color(1f, 0.92f, 0.78f), lightIntensity = 1.4f, ambient = new Color(0.62f, 0.55f, 0.45f), fog = new Color(0.85f, 0.72f, 0.55f), border = new Color(1.6f, 1.1f, 0.4f),
            entries = new[] { E(quicksand, 8, 12, 0.15f, 0.9f, 10f, 0.8f, 1.5f), E(cactus, 22, 30, 0.12f, 0.97f, 4f, 0.8f, 1.4f), E(mesa, 10, 14, 0.2f, 0.95f, 7f, 0.8f, 1.5f) },
            outside = new[] { Hill("Hill_Desert", new Color(0.8f, 0.66f, 0.45f)) },
        });

        // 숲: 회복의 샘
        var tree = Preset("Forest_Tree", t =>
        {
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 1.2f, 0.6f), Mat("Trunk", new Color(0.35f, 0.25f, 0.18f)), true);
            var leaf = Mat("ForestLeaf", new Color(0.22f, 0.48f, 0.25f));
            Prim(PrimitiveType.Sphere, "Canopy", t, new Vector3(0, 3.2f, 0), new Vector3(3f, 2.6f, 3f), leaf);
            Prim(PrimitiveType.Sphere, "Canopy2", t, new Vector3(0.7f, 3.9f, 0.3f), new Vector3(1.8f, 1.6f, 1.8f), Mat("ForestLeafLight", new Color(0.3f, 0.58f, 0.3f)));
        });
        var bush = Preset("Forest_Bush", t =>
        {
            var m = Mat("ForestBush", new Color(0.2f, 0.42f, 0.22f));
            Prim(PrimitiveType.Sphere, "A", t, new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1f, 1.4f), m, true);
            Prim(PrimitiveType.Sphere, "B", t, new Vector3(0.8f, 0.4f, 0.3f), new Vector3(1f, 0.8f, 1f), m);
        });
        var spring = Zone("Forest_Spring", ZoneType.Heal, 3f, false, new Color(0.45f, 0.95f, 0.75f), new Vector2(4.5f, 4.5f));
        list.Add(new RegionTheme
        {
            id = "Forest", name = "숲", desc = "울창한 나무와 회복의 샘", resource = ResourceType.RiverPebble,
            map = new Color(0.22f, 0.45f, 0.24f), floor = new Color(0.24f, 0.36f, 0.2f), light = new Color(1f, 0.97f, 0.85f), ambient = new Color(0.38f, 0.45f, 0.36f), fog = new Color(0.2f, 0.3f, 0.22f), border = new Color(0.6f, 1.6f, 0.7f),
            entries = new[] { E(tree, 28, 38, 0.12f, 0.97f, 4.5f, 0.8f, 1.4f), E(bush, 16, 24, 0.1f, 0.97f, 3f, 0.8f, 1.3f), E(spring, 3, 5, 0.25f, 0.85f, 14f) },
            outside = new[] { tree, Hill("Hill_Forest", new Color(0.2f, 0.32f, 0.18f)) },
        });

        // 늪: 물(감속) + 독 웅덩이(피해)
        var deadTree = Preset("Swamp_DeadTree", t =>
        {
            var m = Mat("DeadWood", new Color(0.25f, 0.22f, 0.18f));
            Prim(PrimitiveType.Cylinder, "Trunk", t, new Vector3(0, 1.4f, 0), new Vector3(0.5f, 1.4f, 0.5f), m, true).transform.localRotation = Quaternion.Euler(6, 0, 4);
            Prim(PrimitiveType.Cylinder, "Branch", t, new Vector3(0.5f, 2.3f, 0), new Vector3(0.18f, 0.7f, 0.18f), m).transform.localRotation = Quaternion.Euler(0, 0, -50);
            Prim(PrimitiveType.Cylinder, "Branch2", t, new Vector3(-0.4f, 2f, 0.2f), new Vector3(0.15f, 0.5f, 0.15f), m).transform.localRotation = Quaternion.Euler(20, 0, 45);
        });
        var bog = Zone("Swamp_Bog", ZoneType.Water, 0.65f, false, new Color(0.22f, 0.32f, 0.2f), new Vector2(9f, 9f));
        var poison = Zone("Swamp_Poison", ZoneType.Damage, 5f, false, new Color(0.45f, 0.75f, 0.15f), new Vector2(4.5f, 4.5f));
        list.Add(new RegionTheme
        {
            id = "Swamp", name = "늪", desc = "발이 빠지는 물웅덩이와 독", resource = ResourceType.RiverPebble,
            map = new Color(0.3f, 0.36f, 0.22f), floor = new Color(0.18f, 0.22f, 0.15f), light = new Color(0.85f, 0.95f, 0.75f), lightIntensity = 0.9f, ambient = new Color(0.3f, 0.35f, 0.28f), fog = new Color(0.22f, 0.27f, 0.18f), fogStart = 25f, fogEnd = 95f, border = new Color(0.8f, 1.5f, 0.4f),
            entries = new[] { E(bog, 10, 14, 0.12f, 0.92f, 10f, 0.8f, 1.4f), E(poison, 8, 12, 0.2f, 0.95f, 7f, 0.8f, 1.3f), E(deadTree, 20, 28, 0.12f, 0.97f, 4f, 0.8f, 1.4f), E(LoadPreset("River_Reeds"), 16, 24, 0.1f, 0.97f, 3f) },
            outside = new[] { deadTree },
        });

        // 수정 동굴: 빛나는 수정
        var crystal = Preset("Crystal_Cluster", t =>
        {
            var m = Glow("CrystalBlue", new Color(0.35f, 0.65f, 1f), 1.5f);
            Prim(PrimitiveType.Cube, "A", t, new Vector3(0, 1.3f, 0), new Vector3(0.8f, 2.6f, 0.8f), m, true).transform.localRotation = Quaternion.Euler(8, 45, 5);
            Prim(PrimitiveType.Cube, "B", t, new Vector3(0.7f, 0.8f, 0.3f), new Vector3(0.5f, 1.6f, 0.5f), m, true).transform.localRotation = Quaternion.Euler(-15, 20, -18);
            Prim(PrimitiveType.Cube, "C", t, new Vector3(-0.5f, 0.6f, -0.4f), new Vector3(0.45f, 1.2f, 0.45f), Glow("CrystalPink", new Color(0.9f, 0.45f, 1f), 1.5f)).transform.localRotation = Quaternion.Euler(18, 10, 15);
        });
        var stalagmite = Preset("Crystal_Stalagmite", t =>
        {
            var m = Mat("CaveRock", new Color(0.2f, 0.2f, 0.27f));
            Prim(PrimitiveType.Cylinder, "Base", t, new Vector3(0, 0.8f, 0), new Vector3(1.4f, 0.8f, 1.4f), m, true);
            Prim(PrimitiveType.Cylinder, "Mid", t, new Vector3(0, 2f, 0), new Vector3(0.8f, 0.6f, 0.8f), m);
            Prim(PrimitiveType.Cylinder, "Tip", t, new Vector3(0, 2.9f, 0), new Vector3(0.35f, 0.4f, 0.35f), m);
        });
        list.Add(new RegionTheme
        {
            id = "Crystal", name = "수정 동굴", desc = "어둑한 동굴에 빛나는 수정", resource = ResourceType.FrostCrystal,
            map = new Color(0.25f, 0.3f, 0.55f), floor = new Color(0.12f, 0.13f, 0.2f), light = new Color(0.6f, 0.7f, 1f), lightIntensity = 0.45f, ambient = new Color(0.18f, 0.2f, 0.32f), fog = new Color(0.05f, 0.06f, 0.12f), fogStart = 25f, fogEnd = 90f, border = new Color(0.7f, 0.5f, 1.8f),
            entries = new[] { E(crystal, 22, 30, 0.12f, 0.97f, 4.5f, 0.8f, 1.6f), E(stalagmite, 18, 24, 0.12f, 0.97f, 4f, 0.8f, 1.5f) },
            outside = new[] { stalagmite },
        });

        // 버섯 군락: 포자(감속)
        var giantShroom = Preset("Mushroom_Giant", t =>
        {
            Prim(PrimitiveType.Cylinder, "Stem", t, new Vector3(0, 1.3f, 0), new Vector3(0.8f, 1.3f, 0.8f), Mat("ShroomStem", new Color(0.9f, 0.85f, 0.75f)), true);
            Prim(PrimitiveType.Sphere, "Cap", t, new Vector3(0, 2.8f, 0), new Vector3(3.6f, 1.4f, 3.6f), Mat("ShroomCapRed", new Color(0.85f, 0.25f, 0.3f)));
            var dot = Mat("ShroomDot", new Color(1f, 0.95f, 0.9f));
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Sphere, "Dot" + i, t, new Vector3(Mathf.Cos(i * 1.6f) * 1f, 3.35f, Mathf.Sin(i * 1.6f) * 1f), new Vector3(0.5f, 0.2f, 0.5f), dot);
        });
        var smallShroom = Preset("Mushroom_Small", t =>
        {
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(Mathf.Cos(i * 2.2f) * 0.7f, 0f, Mathf.Sin(i * 2.2f) * 0.7f);
                Prim(PrimitiveType.Cylinder, "Stem" + i, t, p + Vector3.up * 0.35f, new Vector3(0.25f, 0.35f, 0.25f), Mat("ShroomStem", new Color(0.9f, 0.85f, 0.75f)), i == 0);
                Prim(PrimitiveType.Sphere, "Cap" + i, t, p + Vector3.up * 0.75f, new Vector3(0.8f, 0.35f, 0.8f), Glow("ShroomCapGlow", new Color(0.7f, 0.4f, 1f), 1.2f));
            }
        });
        var spores = Zone("Mushroom_Spores", ZoneType.Slow, 0.6f, false, new Color(0.7f, 0.55f, 0.85f), new Vector2(7f, 7f));
        list.Add(new RegionTheme
        {
            id = "Mushroom", name = "버섯 군락", desc = "거대 버섯과 끈적한 포자", resource = ResourceType.SugarCrystal,
            map = new Color(0.5f, 0.32f, 0.55f), floor = new Color(0.3f, 0.24f, 0.32f), light = new Color(1f, 0.85f, 1f), lightIntensity = 0.9f, ambient = new Color(0.4f, 0.32f, 0.45f), fog = new Color(0.25f, 0.18f, 0.3f), border = new Color(1.4f, 0.6f, 1.6f),
            entries = new[] { E(spores, 8, 12, 0.15f, 0.9f, 9f, 0.8f, 1.4f), E(giantShroom, 16, 22, 0.15f, 0.97f, 6f, 0.8f, 1.5f), E(smallShroom, 22, 32, 0.1f, 0.97f, 3f, 0.8f, 1.4f) },
            outside = new[] { giantShroom },
        });

        // 묘지: 비석 + 무덤 벽
        var tomb = Preset("Graveyard_Tomb", t =>
        {
            var m = Mat("Tombstone", new Color(0.5f, 0.5f, 0.52f));
            Prim(PrimitiveType.Cube, "Stone", t, new Vector3(0, 0.6f, 0), new Vector3(0.9f, 1.2f, 0.25f), m, true).transform.localRotation = Quaternion.Euler(0, 0, 4);
            Prim(PrimitiveType.Cylinder, "Top", t, new Vector3(0, 1.2f, 0), new Vector3(0.9f, 0.125f, 0.9f), m).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cube, "Mound", t, new Vector3(0, 0.1f, -0.9f), new Vector3(0.9f, 0.2f, 1.6f), Mat("GraveDirt", new Color(0.22f, 0.2f, 0.18f)));
        });
        var crypt = Preset("Graveyard_Crypt", t =>
        {
            var m = Mat("CryptStone", new Color(0.34f, 0.34f, 0.38f));
            Prim(PrimitiveType.Cube, "Body", t, new Vector3(0, 1.1f, 0), new Vector3(3f, 2.2f, 2.4f), m, true);
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0, 2.5f, 0), new Vector3(3.4f, 0.4f, 2.8f), Mat("CryptRoof", new Color(0.24f, 0.24f, 0.28f)));
            Prim(PrimitiveType.Cube, "Door", t, new Vector3(0, 0.8f, -1.21f), new Vector3(1f, 1.6f, 0.05f), Mat("CryptDoor", new Color(0.1f, 0.1f, 0.12f)));
        });
        var candle = Preset("Graveyard_Candle", t =>
        {
            Prim(PrimitiveType.Cylinder, "Wax", t, new Vector3(0, 0.25f, 0), new Vector3(0.18f, 0.25f, 0.18f), Mat("CandleWax", new Color(0.9f, 0.88f, 0.8f)));
            Prim(PrimitiveType.Sphere, "Flame", t, new Vector3(0, 0.6f, 0), new Vector3(0.14f, 0.2f, 0.14f), Glow("CandleFlame", new Color(0.4f, 1f, 0.6f), 4f));
        });
        list.Add(new RegionTheme
        {
            id = "Graveyard", name = "묘지", desc = "안개 낀 비석과 무덤", resource = ResourceType.AncientStone,
            map = new Color(0.32f, 0.38f, 0.36f), floor = new Color(0.2f, 0.24f, 0.22f), light = new Color(0.7f, 0.8f, 0.9f), lightIntensity = 0.6f, ambient = new Color(0.25f, 0.3f, 0.32f), fog = new Color(0.28f, 0.33f, 0.35f), fogStart = 18f, fogEnd = 80f, border = new Color(0.5f, 1.5f, 1f),
            entries = new[] { E(tomb, 34, 46, 0.1f, 0.97f, 3f, 0.9f, 1.2f), E(crypt, 6, 9, 0.25f, 0.92f, 9f), E(candle, 16, 24, 0.1f, 0.97f, 2.5f) },
            outside = new[] { Hill("Hill_Graveyard", new Color(0.18f, 0.22f, 0.2f)) },
        });
        return list;
    }

    // =====================================================================
    // 지역 씬
    // =====================================================================
    static void BuildRegionScene(string path, RegionTheme t)
    {
        // 기존 스테이지 씬을 바탕으로 (카메라/조명/플레이어 설정 유지) 지형과 Stage만 새로 만듦
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(RegionBaseScene, path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Arena" || root.name == "Stage") Object.DestroyImmediate(root);

        // 지형: 보더보다 훨씬 넓은 평지 (보더 밖은 게임플레이 영역 아님)
        var arena = new GameObject("Arena").transform;
        PB(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(RegionGround * 2f, 0.5f, RegionGround * 2f)), "Floor", arena, new Vector3(0, -0.25f, 0), Mat("RegionFloor_" + t.id, t.floor), false);

        var borderGo = new GameObject("Border");
        borderGo.transform.SetParent(arena, false);
        var border = borderGo.AddComponent<ArenaBorder>();
        var bso = new SerializedObject(border);
        bso.FindProperty("halfSize").floatValue = RegionHalf;
        bso.FindProperty("material").objectReferenceValue = BorderMat(t);
        bso.ApplyModifiedPropertiesWithoutUndo();
        border.Apply();

        // 보더 밖 풍경 (최대로 넓혀도 닿지 않는 곳)
        if (t.outside != null && t.outside.Length > 0)
        {
            var deco = new GameObject("Outside").transform;
            deco.SetParent(arena, false);
            var rng = new System.Random(t.id.GetHashCode());
            for (int i = 0; i < 40; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float r = Mathf.Lerp(RegionMaxHalf * 1.25f, RegionGround * 0.95f, (float)rng.NextDouble());
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(t.outside[i % t.outside.Length], deco);
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale *= Mathf.Lerp(1.2f, 2.5f, (float)rng.NextDouble());
            }
        }

        var layout = Asset<StageLayout>($"{Root}/Data/Layout_Region_{t.id}.asset");
        layout.entries = t.entries;
        layout.centerSafeRadius = t.centerSafe;
        EditorUtility.SetDirty(layout);

        var stage = new GameObject("Stage");
        var gen = stage.AddComponent<StageGenerator>();
        var so = new SerializedObject(gen);
        so.FindProperty("radius").floatValue = RegionHalf;
        so.FindProperty("border").objectReferenceValue = border;
        so.FindProperty("layout").objectReferenceValue = layout;
        so.ApplyModifiedPropertiesWithoutUndo();
        var theme = stage.AddComponent<StageTheme>();
        theme.displayName = t.name; theme.killTargetMul = t.killMul; theme.resource = t.resource;
        Set(stage.AddComponent<StageDirector>(), "generator", gen);

        // 조명 / 안개 / 배경
        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { l.color = t.light; l.intensity = t.lightIntensity; }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = t.ambient;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = t.fog;
        RenderSettings.fogStartDistance = t.fogStart;
        RenderSettings.fogEndDistance = t.fogEnd;
        // 하늘 반사가 바닥을 밝히지 않게 (어두운 지역은 반사 끔)
        RenderSettings.defaultReflectionMode = t.dark ? DefaultReflectionMode.Custom : DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = t.dark ? 0f : 0.35f;
        var cam = Camera.main;
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = t.fog; }

        if (t.dark)
        {
            // 칠흑 같은 어둠: 플레이어와 작은 생명체 주변만 희미하게 보임
            FollowLightObj("PlayerLight", FollowLight.Target.Player, 16f, 3f, new Color(0.75f, 0.8f, 1f), new Vector3(0f, 5f, 0f));
            FollowLightObj("WardLight", FollowLight.Target.Ward, 8f, 2f, new Color(0.7f, 1f, 0.75f), new Vector3(0f, 2.5f, 0f));
        }
        EditorSceneManager.SaveScene(scene, path);
    }

    static void FollowLightObj(string name, FollowLight.Target target, float range, float intensity, Color c, Vector3 offset)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = c; l.shadows = LightShadows.None;
        var f = l.gameObject.AddComponent<FollowLight>();
        var so = new SerializedObject(f);
        so.FindProperty("target").enumValueIndex = (int)target;
        so.FindProperty("offset").vector3Value = offset;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material BorderMat(RegionTheme t)
    {
        string path = $"{Root}/Materials/Border_{t.id}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Roguelike/ArenaBorder")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", t.border);
        m.SetColor("_EdgeColor", t.border * 1.6f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // =====================================================================
    // 로비 지역 지도 UI (3x3)
    // =====================================================================
    static GameObject BuildRegionSelectHud()
    {
        var canvas = NewCanvas("HUD_RegionSelect");
        canvas.GetComponent<Canvas>().sortingOrder = 10;
        var panel = Stretch("RegionPanel", canvas.transform); Img(panel, Panel);
        var line = UI("TopLine", panel, TC, TC, new Vector2(0, -50), new Vector2(1500, 3)); Img(line, Gold);
        Txt("Title", panel, "지역 지도", 62, TextAlignmentOptions.Center, Gold, TC, TC, new Vector2(0, -105), new Vector2(1200, 80));
        Txt("Subtitle", panel, "텔레포트할 지역을 고르세요. 중앙은 하늘섬 바로 밑, 바깥 지역은 로비로 돌아올 때마다 바뀝니다.", 22, TextAlignmentOptions.Center, Soft, TC, TC, new Vector2(0, -160), new Vector2(1500, 40));

        var grid = UI("Grid", panel, C, C, new Vector2(0, -10), new Vector2(900, 600));
        int n = RegionSelectUI.GridNumbers.Length;
        var cells = new Button[n]; var names = new TMP_Text[n]; var descs = new TMP_Text[n];
        var bgs = new Image[n]; var borders = new Image[n];
        for (int i = 0; i < n; i++)
        {
            int num = RegionSelectUI.GridNumbers[i];
            var pos = new Vector2(((i % 3) - 1) * 300f, 195f - (i / 3) * 195f);
            var cell = UI($"Cell_{num}", grid, C, C, pos, new Vector2(284, 180));
            borders[i] = Img(cell, new Color(0.3f, 0.29f, 0.34f));
            var bgRt = Stretch("Background", cell); bgRt.offsetMin = new Vector2(4, 4); bgRt.offsetMax = new Vector2(-4, -4);
            bgs[i] = Img(bgRt, new Color(0.16f, 0.15f, 0.18f));
            var shade = Stretch("Shade", bgRt); shade.offsetMax = new Vector2(0, -90); Img(shade, new Color(0, 0, 0, 0.55f)).raycastTarget = false;
            names[i] = Txt("Name", cell, $"{num}. 지역", 30, TextAlignmentOptions.TopLeft, Color.white, TL, TL, new Vector2(18, -16), new Vector2(250, 44), TL);
            descs[i] = Txt("Desc", cell, "", 19, TextAlignmentOptions.BottomLeft, Soft, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-32, -24), null, false);
            cells[i] = cell.gameObject.AddComponent<Button>(); cells[i].targetGraphic = bgs[i];
            var cb = cells[i].colors; cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f); cb.disabledColor = new Color(0.4f, 0.4f, 0.4f); cells[i].colors = cb;
        }

        var selected = Txt("SelectedText", panel, "지역을 고르세요", 28, TextAlignmentOptions.Center, Gold, BC, BC, new Vector2(0, 160), new Vector2(1000, 44));
        var start = UI("StartButton", panel, BC, BC, new Vector2(-170, 80), new Vector2(300, 64));
        var startBtn = start.gameObject.AddComponent<Button>(); startBtn.targetGraphic = Img(start, new Color(0.55f, 0.42f, 0.2f));
        Txt("Label", start, "출발 (F / Enter)", 24, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var close = UI("CloseButton", panel, BC, BC, new Vector2(170, 80), new Vector2(300, 64));
        var closeBtn = close.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = Img(close, new Color(0.25f, 0.23f, 0.28f));
        Txt("Label", close, "닫기 (Esc)", 24, TextAlignmentOptions.Center, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Txt("KeyHint", panel, "숫자 키 1~9로도 고를 수 있어요", 18, TextAlignmentOptions.Center, Soft, BC, BC, new Vector2(0, 30), new Vector2(800, 30), null, false);

        var ui = canvas.AddComponent<RegionSelectUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        SetArray(so.FindProperty("cells"), cells);
        SetArray(so.FindProperty("cellNames"), names);
        SetArray(so.FindProperty("cellDescs"), descs);
        SetArray(so.FindProperty("cellBackgrounds"), bgs);
        SetArray(so.FindProperty("cellBorders"), borders);
        so.FindProperty("selectedText").objectReferenceValue = selected;
        so.FindProperty("startButton").objectReferenceValue = startBtn;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(canvas, "UI/HUD_RegionSelect");
    }
}

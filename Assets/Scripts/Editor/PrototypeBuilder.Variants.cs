using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 파생 스테이지 전용 씬 만들기 (사용자 방침: 파생 스테이지는 코드 생성이 아니라 씬으로 — QA/손편집용).
/// - Roguelike/Build Variant Scenes: VariantConfig.stageVariants 중 씬 파일이 아직 없는 것만 만듦 (손으로 고친 씬은 절대 덮어쓰지 않음).
/// - Roguelike/Build Variant Scenes (다시 만들기): 확인 후 모두 새로 만듦 (손편집 사라짐).
/// - Roguelike/QA/...: 파생 씬 바로 열기 (열고 Play하면 그 파생형으로 바로 시작).
/// 씬은 지역 기본형 씬을 복사한 뒤: 자동 배치 소품(StageGenerator 배치 규칙)과 보더 밖 풍경 제거 + 파생 고유 오브젝트를 실제로 배치 + 규칙 컴포넌트(VariantStage 오브젝트) 추가.
/// 이후 씬에서 등불/반딧불 영역/논 구획/조명 등을 직접 옮기고 고치면 됨.
/// </summary>
public static partial class PrototypeBuilder
{
    const string VariantSceneDir = "Assets/Scenes/Regions/Variants";
    // 사용자가 폴더를 정리하면서 옮긴 위치(Prefabs/Presets/...)를 우선, 없으면 예전 위치
    static string VariantMatDir => AssetDatabase.IsValidFolder(Root + "/Prefabs/Presets/Materials/Variants") ? Root + "/Prefabs/Presets/Materials/Variants" : Root + "/Materials/Variants";
    static string VariantPrefabDir => AssetDatabase.IsValidFolder(Root + "/Prefabs/Presets/Variants") ? Root + "/Prefabs/Presets/Variants" : Root + "/Prefabs/Variants";

    [MenuItem("Roguelike/Build Variant Scenes")]
    public static void BuildVariantScenes() => BuildVariants(false);

    [MenuItem("Roguelike/Build Variant Scenes (다시 만들기)")]
    public static void RebuildVariantScenes()
    {
        if (!EditorUtility.DisplayDialog("파생 씬 다시 만들기", "모든 파생 스테이지 씬을 새로 만듭니다. 씬에서 손으로 고친 내용은 사라집니다. 계속할까요?", "다시 만들기", "취소")) return;
        BuildVariants(true);
    }

    [MenuItem("Roguelike/QA/파생 씬: 반딧불 숲 열기")] static void OpenFirefly() => OpenVariantScene("Region_Forest_Firefly");
    [MenuItem("Roguelike/QA/파생 씬: 자연 농지 열기")] static void OpenFarmland() => OpenVariantScene("Region_Forest_Farmland");
    [MenuItem("Roguelike/QA/파생 씬: 모래폭풍 사막 열기")] static void OpenSandstorm() => OpenVariantScene("Region_Desert_Sandstorm");
    [MenuItem("Roguelike/QA/파생 씬: 얼어붙은 호수 열기")] static void OpenFrozenLake() => OpenVariantScene("Region_Snow_FrozenLake");
    [MenuItem("Roguelike/QA/파생 씬: 칼데라 열기")] static void OpenCaldera() => OpenVariantScene("Region_Volcano_Caldera");
    [MenuItem("Roguelike/QA/파생 씬: 도깨비불 늪 열기")] static void OpenWisp() => OpenVariantScene("Region_Swamp_Wisp");
    [MenuItem("Roguelike/QA/파생 씬: 깨어난 묘지 열기")] static void OpenAwakened() => OpenVariantScene("Region_Graveyard_Awakened");

    static void OpenVariantScene(string name)
    {
        string path = $"{VariantSceneDir}/{name}.unity";
        if (!System.IO.File.Exists(path)) { EditorUtility.DisplayDialog("파생 씬", $"{path} 이(가) 없습니다. Roguelike/Build Variant Scenes를 먼저 실행하세요.", "확인"); return; }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
    }

    static void BuildVariants(bool overwrite)
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty && !EditorUtility.DisplayDialog("파생 씬", $"'{active.name}' 씬에 저장하지 않은 변경이 있습니다. 씬을 닫고 계속할까요? (변경은 저장되지 않습니다)", "계속", "취소")) return;
        string returnTo = active.path;
        EnsureFolder("Assets/Scenes/Regions", "Variants");
        if (!AssetDatabase.IsValidFolder(VariantMatDir)) { EnsureFolder(Root, "Materials"); EnsureFolder(Root + "/Materials", "Variants"); }
        if (!AssetDatabase.IsValidFolder(VariantPrefabDir)) EnsureFolder(Root + "/Prefabs", "Variants");

        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Root + "/Resources/GameConfig.asset");
        var vc = cfg.variant;
        var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int made = 0;
        foreach (var v in vc.stageVariants)
        {
            if (v == null || string.IsNullOrEmpty(v.scene)) continue;
            string path = $"{VariantSceneDir}/{v.scene}.unity";
            if (!build.Exists(s => s.path == path)) build.Add(new EditorBuildSettingsScene(path, true));
            if (System.IO.File.Exists(path) && !overwrite) continue; // 손편집 보존
            string basePath = $"{RegionDir}/{v.regionScene}.unity";
            if (!System.IO.File.Exists(basePath)) { Debug.LogWarning($"[PrototypeBuilder] 기본형 씬이 없습니다: {basePath}"); continue; }
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(basePath, path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            StripAutoProps();
            var root = new GameObject("VariantStage");
            root.AddComponent<VariantStage>().variantId = v.id;
            switch (v.kind)
            {
                case StageVariantKind.ForestFirefly: BuildFirefly(root, vc.firefly); break;
                case StageVariantKind.ForestFarmland: BuildFarmland(root, vc.farmland, v.scene); break;
                case StageVariantKind.DesertSandstorm: BuildSandstorm(root, vc.sandstorm); break;
                case StageVariantKind.SnowFrozenLake: BuildFrozenLake(root, vc.frozenLake, v.scene); break;
                case StageVariantKind.VolcanoCaldera: BuildCaldera(root, vc.caldera); break;
                case StageVariantKind.SwampWisp: BuildWispSwamp(root, vc.wisp); break;
                case StageVariantKind.GraveyardAwakened: BuildAwakenedGraveyard(root, vc.awakened); break;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            made++;
        }
        EditorBuildSettings.scenes = build.ToArray();
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo);
        Debug.Log($"[PrototypeBuilder] 파생 씬 {made}개 생성 ({VariantSceneDir}) — 이미 있는 씬은 건드리지 않음");
    }

    static void EnsureFolder(string parent, string name) { if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name); }

    /// <summary>자동 생성 모델 제거: 배치 규칙(StageLayout) 비우기 + 보더 밖 풍경 삭제.</summary>
    static void StripAutoProps()
    {
        var gen = Object.FindAnyObjectByType<StageGenerator>();
        if (gen) { var so = new SerializedObject(gen); so.FindProperty("layout").objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }
        var outside = GameObject.Find("Arena/Outside");
        if (outside) Object.DestroyImmediate(outside);
    }

    static Material VMat(string name, string shader, Color c)
    {
        string path = $"{VariantMatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        if (m.HasColor("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
        EditorUtility.SetDirty(m);
        return m;
    }

    static float SampleGround(Terrain t, float x, float z) => t ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 0f;

    // =====================================================================
    // 반딧불 숲
    // =====================================================================
    static void BuildFirefly(GameObject root, FireflyForestSettings s)
    {
        var rule = root.AddComponent<ForestFireflyVariant>();
        rule.fireflyMaterial = VMat("FireflyGlow", "Universal Render Pipeline/Unlit", new Color(0.75f, 1f, 0.35f) * 2.2f);
        rule.lanternOnMaterial = VMat("LanternOn", "Universal Render Pipeline/Unlit", new Color(1f, 0.85f, 0.45f) * 2f);
        rule.lanternOffMaterial = VMat("LanternOff", "Universal Render Pipeline/Unlit", new Color(0.25f, 0.22f, 0.15f));

        // 밤: 해/주변광/안개/배경
        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { l.intensity *= s.sunMul; l.color = new Color(0.55f, 0.65f, 1f); }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = s.ambient;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.06f);
        RenderSettings.fogStartDistance = 18f; RenderSettings.fogEndDistance = 75f;
        RenderSettings.reflectionIntensity = 0.1f;
        var cam = Camera.main;
        if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor; }
        // 감시자/생명체 주변만 희미하게
        FollowLightObj("WatcherLight", FollowLight.Target.Player, 11f, 2.2f, new Color(0.65f, 0.75f, 1f), new Vector3(0f, 4f, 0f));
        FollowLightObj("WardLight", FollowLight.Target.Ward, 6f, 1.6f, new Color(0.75f, 1f, 0.75f), new Vector3(0f, 2.2f, 0f));

        // 반딧불 영역 (아레나 전체, 손으로 늘리거나 여러 개로 나눠도 됨)
        var area = new GameObject("FireflySpawnArea").AddComponent<FireflySpawnArea>();
        area.transform.SetParent(root.transform, false);
        area.size = new Vector2(RegionHalf * 1.7f, RegionHalf * 1.7f);

        // 등불 (프리팹, 씬에서 옮기면 됨)
        var prefab = LanternPrefab(rule, s);
        var lanterns = new GameObject("Lanterns").transform;
        lanterns.SetParent(root.transform, false);
        var t = Terrain.activeTerrain;
        var rng = new System.Random(42);
        var used = new List<Vector3>();
        for (int tries = 0; used.Count < s.lanternCount && tries < 600; tries++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * RegionHalf * 0.8f, z = ((float)rng.NextDouble() * 2f - 1f) * RegionHalf * 0.8f;
            var p = new Vector3(x, 0f, z);
            if (p.magnitude < 10f) continue;
            if (t)
            {
                var td = t.terrainData; var tp = t.transform.position;
                float st = td.GetSteepness((x - tp.x) / td.size.x, (z - tp.z) / td.size.z);
                if (st > 18f) continue;
            }
            bool near = false;
            foreach (var u in used) if ((u - p).sqrMagnitude < 22f * 22f) { near = true; break; }
            if (near && tries < 450) continue;
            p.y = SampleGround(t, x, z);
            used.Add(p);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lanterns);
            go.transform.position = p;
        }
    }

    static GameObject LanternPrefab(ForestFireflyVariant rule, FireflyForestSettings s)
    {
        string path = $"{VariantPrefabDir}/Lantern.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing;
        var post = VMat("LanternPost", "Universal Render Pipeline/Lit", new Color(0.12f, 0.1f, 0.09f));
        var ringMat = VMat("LanternRing", "Sprites/Default", Color.white);
        var root = new GameObject("Lantern");
        var lan = root.AddComponent<VariantLantern>();
        LPart(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 1.1f, 0f), new Vector3(0.25f, 1.1f, 0.25f), post);
        LPart(PrimitiveType.Cube, "Cap", root.transform, new Vector3(0f, 2.3f, 0f), new Vector3(0.75f, 0.12f, 0.75f), post);
        lan.glass = LPart(PrimitiveType.Sphere, "Glass", root.transform, new Vector3(0f, 2.75f, 0f), Vector3.one * 0.65f, rule.lanternOffMaterial);
        var l = new GameObject("LanternLight").AddComponent<Light>();
        l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(0f, 3f, 0f);
        l.type = LightType.Point; l.range = s.safeRadius * 1.6f; l.intensity = 0f; l.color = new Color(1f, 0.85f, 0.45f); l.shadows = LightShadows.None;
        lan.lanternLight = l;
        var ring = new GameObject("SafeRing").AddComponent<LineRenderer>();
        ring.transform.SetParent(root.transform, false);
        ring.sharedMaterial = ringMat; ring.widthMultiplier = 0.12f; ring.positionCount = 0; ring.loop = true; ring.useWorldSpace = true;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lan.safeRing = ring;
        lan.previewRadius = s.safeRadius;
        var p = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return p;
    }

    static Renderer LPart(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = m;
        return r;
    }


    // =====================================================================
    // 자연 농지
    // =====================================================================
    static void BuildFarmland(GameObject root, FarmlandSettings s, string sceneName)
    {
        root.AddComponent<ForestFarmlandVariant>();

        // 지형: 기본형 지형 에셋을 복사한 전용 TerrainData (평평한 논 + 논두렁/물 댄 논바닥 레이어). 원본 숲 지형은 그대로
        var t = Terrain.activeTerrain;
        if (t && t.terrainData)
        {
            string src = AssetDatabase.GetAssetPath(t.terrainData);
            string dst = $"{TerrainDir}/{sceneName}_Terrain.asset";
            AssetDatabase.DeleteAsset(dst);
            AssetDatabase.CopyAsset(src, dst);
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dst);
            var sz = td.size; sz.y *= s.terrainHeightMul; td.size = sz;
            var levee = td.terrainLayers != null && td.terrainLayers.Length > 0 ? td.terrainLayers[0] : null;
            var water = PaddyWaterLayer(s);
            td.terrainLayers = levee ? new[] { water, levee } : new[] { water };
            PaintPaddies(td, t.transform.position, s);
            EditorUtility.SetDirty(td);
            t.terrainData = td;
            if (t.TryGetComponent(out TerrainCollider tc)) tc.terrainData = td;
        }

        // 논 구획 (논두렁 사이 칸마다 RicePaddy 하나) — 씬에서 옮기거나 크기/색/줄 방향을 바꾸면 벼가 따라옴
        var paddies = new GameObject("Paddies").transform;
        paddies.SetParent(root.transform, false);
        var rng = new System.Random(7);
        float step = s.pathEvery, inner = s.pathEvery - s.pathWidth;
        int n = Mathf.CeilToInt(RegionHalf / step) + 1, idx = 0;
        for (int iz = -n; iz < n; iz++)
            for (int ix = -n; ix < n; ix++)
            {
                float cx = ix * step + s.pathWidth + inner * 0.5f, cz = iz * step + s.pathWidth + inner * 0.5f;
                float half = inner * 0.5f, lim = RegionHalf - 1f;
                if (cx - half < -lim || cx + half > lim || cz - half < -lim || cz + half > lim) continue; // 보더 밖 제외
                var go = new GameObject($"Paddy_{++idx:00}");
                go.transform.SetParent(paddies, false);
                go.transform.position = new Vector3(cx, 0f, cz);
                var p = go.AddComponent<RicePaddy>();
                p.size = new Vector2(inner, inner);
                p.rowSpacing = s.rowSpacing; p.plantSpacing = s.plantSpacing;
                p.jitter = s.jitter; p.yawJitter = s.yawJitter; p.sizeJitter = s.sizeJitter;
                p.height = Mathf.Lerp(s.height.x, s.height.y, (float)rng.NextDouble());
                p.golden = rng.NextDouble() < s.goldRatio;
                p.seed = idx;
                p.riceMesh = s.riceMesh; p.goldMaterial = s.riceMaterialGold; p.greenMaterial = s.riceMaterialGreen;
                p.plantWidth = s.clumpSize; p.meshUpIsZ = s.riceMeshUpIsZ;
            }

        // 벼가루 (화면 전체에 은은하게) — 아레나 전체를 덮는 파티클
        Pollen(root.transform, s);
    }

    static TerrainLayer PaddyWaterLayer(FarmlandSettings s)
    {
        string texPath = $"{TerrainDir}/PaddyWater_Tex.asset", layerPath = $"{TerrainDir}/PaddyWater.terrainlayer";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (!tex)
        {
            tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "PaddyWater", wrapMode = TextureWrapMode.Repeat };
            var c = s.paddyGround;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float k = (Mathf.PerlinNoise(x * 0.12f, y * 0.12f) - 0.5f) * 0.2f;
                    tex.SetPixel(x, y, new Color(c.r * (1f + k), c.g * (1f + k), c.b * (1f + k), 1f));
                }
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, texPath);
        }
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
        layer.diffuseTexture = tex; layer.tileSize = new Vector2(3f, 3f); layer.smoothness = s.paddyWaterSmoothness; layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    /// <summary>레이어 0 = 물 댄 논바닥, 레이어 1 = 논두렁(풀): pathEvery마다 pathWidth 폭의 줄.</summary>
    static void PaintPaddies(TerrainData td, Vector3 tpos, FarmlandSettings s)
    {
        int res = td.alphamapResolution, layers = td.alphamapLayers;
        var a = new float[res, res, layers];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = tpos.x + x / (float)(res - 1) * td.size.x, wz = tpos.z + z / (float)(res - 1) * td.size.z;
                bool levee = Mathf.Repeat(wx, s.pathEvery) < s.pathWidth || Mathf.Repeat(wz, s.pathEvery) < s.pathWidth || Mathf.Abs(wx) > RegionHalf || Mathf.Abs(wz) > RegionHalf;
                if (layers > 1) { a[z, x, 0] = levee ? 0f : 1f; a[z, x, 1] = levee ? 1f : 0f; }
                else a[z, x, 0] = 1f;
            }
        td.SetAlphamaps(0, 0, a);
    }

    static void Pollen(Transform parent, FarmlandSettings s)
    {
        var go = new GameObject("RicePollen");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, 3f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(s.pollenSize.x, s.pollenSize.y);
        main.startColor = new Color(1f, 0.95f, 0.7f, s.pollenAlpha);
        main.maxParticles = 6000; main.prewarm = true;
        main.gravityModifier = -0.005f;
        var em = ps.emission; em.rateOverTime = s.pollenRate * 4f; // 아레나 전체를 덮으니 넓이만큼
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(RegionHalf * 2f, 6f, RegionHalf * 2f);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.1f); vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.3f; noise.frequency = 0.4f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = PollenMat();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Material PollenMat()
    {
        string path = $"{VariantMatDir}/RicePollen.mat", texPath = $"{VariantMatDir}/SoftDot.asset";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "SoftDot" };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        AssetDatabase.CreateAsset(tex, texPath);
        m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "RicePollen" };
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}

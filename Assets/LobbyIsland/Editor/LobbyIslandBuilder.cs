using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 로비 "노을 하늘의 떠 있는 섬" 씬을 처음부터 다시 만든다.
// 열려 있는 다른 씬은 건드리지 않도록 추가(Additive) 씬으로 만들고 저장 후 닫는다.
public static partial class LobbyIslandBuilder
{
    const string Root = "Assets/LobbyIsland";
    const string ModelDir = Root + "/Models";
    const string MatDir = Root + "/Materials";
    const string ScenePath = "Assets/Scenes/LobbyIsland.unity";
    const string ProfilePath = Root + "/LobbyIslandVolume.asset";

    static readonly Vector3 SunDir = new Vector3(-0.22f, 0.06f, 0.97f).normalized;
    static readonly Color Fog = new Color(0.93f, 0.8f, 0.88f);

    public static float CottageYaw = 290f;
    const int FenceCount = 20;

    static System.Random rng;
    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    [MenuItem("Tools/Lobby Island/Rebuild Scene")]
    public static void BuildMenu() => Build(null);

    public static string Build(string capturePath)
    {
        rng = new System.Random(7);
        EnsureFolder(MatDir);
        var prevActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        // 같은 경로의 씬이 열려 있으면 저장이 막히므로 새 씬을 연 뒤에 닫는다
        var existing = SceneManager.GetSceneByPath(ScenePath);
        if (existing.IsValid() && existing.isLoaded) EditorSceneManager.CloseScene(existing, true);

        var root = new GameObject("LobbyIsland");
        var mats = new Mats();

        // ---- 주 섬 ----
        var island = Place("island", root.transform, Vector3.zero, 22f, Axis.Width, 0f);
        island.name = "MainIsland";
        var islandBounds = Bounds(island);
        // 섬을 윗면이 y=0 근처에 오도록 내린다(윗면 = 중앙 레이캐스트 높이)
        AddColliders(island);
        float topY = GroundY(new Vector3(0, 0, 0), islandBounds.max.y + 5);
        island.transform.position -= Vector3.up * topY;
        Physics.SyncTransforms();
        islandBounds = Bounds(island);
        var islandPivot = new GameObject("IslandBob").transform;
        islandPivot.SetParent(root.transform);
        island.transform.SetParent(islandPivot, true);
        float rim = Mathf.Min(islandBounds.extents.x, islandBounds.extents.z);

        // ---- 오두막, 벚나무, 가로등, 울타리 ----
        var cottage = Place("cottage2", islandPivot, GroundMin(new Vector3(-0.4f, 0, 0.6f), 3f), 6.4f, Axis.Height, CottageYaw);
        var tree = Place("sakura", islandPivot, GroundMin(new Vector3(3.6f, 0, 4.2f), 1f), 9.5f, Axis.Height, 30f);
        Place("sakura", islandPivot, GroundMin(new Vector3(-4.8f, 0, 3.6f), 1f), 5.5f, Axis.Height, 120f);
        Place("lamppost", islandPivot, Ground(new Vector3(4.2f, 0, -3.4f)), 3.2f, Axis.Height, 160f);
        float fenceR = rim * 0.62f;
        for (int i = 0; i < FenceCount; i++)
        {
            float a = i / (float)FenceCount * Mathf.PI * 2f;
            if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 270f)) < 22f) continue; // 앞쪽 계단/길 자리
            var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * fenceR;
            Place("fence", islandPivot, Ground(p), 1.9f, Axis.Width, -a * Mathf.Rad2Deg + 90f);
        }
        RemoveColliders(island);

        // 창문/랜턴의 따뜻한 빛
        var cb = Bounds(cottage);
        PointLight(islandPivot, cb.center + new Vector3(0, -cb.extents.y * 0.25f, -cb.extents.z - 0.6f), new Color(1f, 0.62f, 0.3f), 3.5f, 9f);
        PointLight(islandPivot, Ground(new Vector3(4.2f, 0, -3.4f)) + Vector3.up * 3f, new Color(1f, 0.7f, 0.35f), 2.5f, 6f);

        // ---- 폭포 (앞왼쪽 2줄, 오른쪽 1줄) ----
        var wfMat = mats.Get("Waterfall", "LobbyIsland/Waterfall");
        wfMat.SetColor("_Color", new Color(0.8f, 0.92f, 1.15f, 0.95f));
        Waterfall(islandPivot, wfMat, -150f, rim * 0.92f, 1.6f, 34f);
        Waterfall(islandPivot, wfMat, -132f, rim * 0.92f, 1.1f, 30f);
        Waterfall(islandPivot, wfMat, -20f, rim * 0.92f, 1.3f, 32f);

        // ---- 파티클: 벚꽃잎, 굴뚝 연기, 폭포 물안개 ----
        Petals(islandPivot, Bounds(tree).center + Vector3.up * 1f, mats);
        Smoke(islandPivot, new Vector3(cb.center.x + cb.extents.x * 0.45f, cb.max.y - 0.3f, cb.center.z + cb.extents.z * 0.2f), mats);

        islandPivot.gameObject.AddComponent<FloatBob>().amplitude = 0.25f;

        // ---- 주변의 작은 섬들 ----
        var islets = new GameObject("Islets").transform;
        islets.SetParent(root.transform);
        Islet(islets, "islet", new Vector3(-17f, -5f, 6f), 5.5f);
        Islet(islets, "islet", new Vector3(19f, 2f, 14f), 6.5f);
        Islet(islets, "islet", new Vector3(-30f, 6f, 40f), 7f);
        Islet(islets, "islet", new Vector3(13f, -10f, -6f), 2.2f);
        Islet(islets, "islet", new Vector3(-9f, -13f, -9f), 1.6f);
        Islet(islets, "islet", new Vector3(7f, -17f, 2f), 1.2f);
        Islet(islets, "castleislet", new Vector3(-62f, 14f, 120f), 22f);
        Islet(islets, "castleislet", new Vector3(70f, 22f, 150f), 26f);
        Islet(islets, "castleislet", new Vector3(36f, 4f, 85f), 12f);
        Islet(islets, "islet", new Vector3(-95f, 30f, 190f), 18f);

        // ---- 구름 바다 ----
        Clouds(root.transform, mats);

        // ---- 조명/환경 ----
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.transform.SetParent(root.transform);
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.8f, 0.74f);
        sun.intensity = 1.5f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.55f;
        // 노을 태양이 낮으므로 그림자가 너무 길지 않게 실제보다 조금 높은 각도로 비춘다
        // 화면 속 태양은 정면 왼쪽 수평선에 있지만, 섬 정면이 어둡지 않게 실제 빛은 왼쪽 앞 위에서 비춘다
        sun.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.75f, 0.75f, -0.35f).normalized);

        ApplySkyAndFog(mats, sun, 75f, 340f);

        // ---- 카메라 + 포스트프로세싱 ----
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(root.transform);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 34f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 600f;
        camGo.transform.position = new Vector3(1.5f, 9f, -62f);
        var lookTarget = new GameObject("CameraTarget").transform;
        lookTarget.SetParent(root.transform);
        lookTarget.position = new Vector3(-1f, -3f, 0f);
        camGo.transform.LookAt(lookTarget);
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var drift = camGo.AddComponent<CameraDrift>();
        drift.target = lookTarget;

        var volGo = new GameObject("Global Volume");
        volGo.transform.SetParent(root.transform);
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = BuildProfile();

        if (!EditorSceneManager.SaveScene(scene, ScenePath)) Debug.LogError("[LobbyIsland] 씬 저장 실패: " + ScenePath);

        string result = "saved " + ScenePath;
        if (!string.IsNullOrEmpty(capturePath)) result += "; " + Capture(cam, capturePath);

        if (prevActive.IsValid() && prevActive.isLoaded && prevActive != scene)
        {
            SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(scene, true);
        }
        return result;
    }

    static void ApplySkyAndFog(Mats mats, Light sun, float fogStart, float fogEnd, bool configureSky = true)
    {
        var sky = mats.Get("Sky", "LobbyIsland/GradientSky");
        if (configureSky)
        {
            sky.SetVector("_SunDir", SunDir);
            sky.SetColor("_TopColor", new Color(0.42f, 0.66f, 1f));
            sky.SetColor("_MidColor", new Color(0.72f, 0.78f, 1f));
            sky.SetColor("_HorizonColor", new Color(1f, 0.74f, 0.8f));
            sky.SetFloat("_MidHeight", 0.2f);
            sky.SetFloat("_HorizonSharpness", 5f);
            sky.SetColor("_SunGlowColor", new Color(0.9f, 0.5f, 0.35f));
            sky.SetFloat("_SunGlowPower", 60f);
            sky.SetColor("_SunColor", new Color(8f, 5f, 3.2f));
            sky.SetFloat("_SunSize", 0.0012f);
        }
        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.8f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.98f, 0.8f, 0.86f);
        RenderSettings.ambientGroundColor = new Color(0.62f, 0.56f, 0.75f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Fog;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }

    // ---------------- 포스트프로세싱 ----------------
    static VolumeProfile BuildProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        foreach (var c in profile.components.ToArray()) { profile.Remove(c.GetType()); Object.DestroyImmediate(c, true); }

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.Neutral;

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.value = 1.2f;
        bloom.intensity.value = 0.8f;
        bloom.scatter.value = 0.7f;
        bloom.tint.value = new Color(1f, 0.9f, 0.88f);
        bloom.highQualityFiltering.value = true;

        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.value = 0f;
        ca.contrast.value = 12f;
        ca.saturation.value = 18f;
        ca.colorFilter.value = Color.white;
        ca.hueShift.value = 0f;

        var wb = profile.Add<WhiteBalance>(true);
        wb.temperature.value = 2f;
        wb.tint.value = 0f;

        var split = profile.Add<SplitToning>(true);
        split.shadows.value = new Color(0.62f, 0.7f, 1f);  // 그림자는 푸른 라벤더
        split.highlights.value = new Color(1f, 0.9f, 0.9f);  // 하이라이트는 분홍
        split.balance.value = -15f;

        var smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.value = new Vector4(0.96f, 0.96f, 1.06f, 0.03f); // 그림자를 들어올려 파스텔 톤
        smh.midtones.value = new Vector4(1f, 1f, 1f, 0f);
        smh.highlights.value = new Vector4(1.02f, 1f, 1f, 0f);

        var vig = profile.Add<Vignette>(true);
        vig.color.value = new Color(0.45f, 0.3f, 0.5f);
        vig.intensity.value = 0.22f;
        vig.smoothness.value = 0.5f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // ---------------- 모델 임포트 ----------------
    // Meshy FBX는 Z-up + 외부 텍스처라서, 축 변환을 굽고 basecolor PNG로 만든 매트한 URP Lit 머티리얼을 연결한다.
    [MenuItem("Tools/Lobby Island/Setup Meshy Models")]
    public static void SetupModels()
    {
        EnsureFolder(MatDir);
        foreach (var fbx in Directory.GetFiles(ModelDir, "*.fbx"))
        {
            var path = fbx.Replace('\\', '/');
            var name = Path.GetFileNameWithoutExtension(path);
            var texPath = $"{ModelDir}/{name}_basecolor.png";
            var texImp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (texImp != null && texImp.maxTextureSize != 2048)
            {
                texImp.maxTextureSize = 2048;
                texImp.SaveAndReimport();
            }
            var matPath = $"{MatDir}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);

            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.bakeAxisConversion = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var src in imp.GetExternalObjectMap())
                if (src.Key.type == typeof(Material)) imp.RemoveRemap(src.Key);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material.001"), mat);
            imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
    }

    // ---------------- 모델 배치 ----------------
    enum Axis { Height, Width }

    static GameObject Place(string model, Transform parent, Vector3 pos, float size, Axis axis, float yaw)
    {
        var path = $"{ModelDir}/{model}.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("[LobbyIsland] 모델 없음: " + path); return new GameObject(model + " (missing)"); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0) * prefab.transform.localRotation;
        go.transform.localScale = Vector3.one;
        var b = Bounds(go);
        float cur = axis == Axis.Height ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        if (cur > 0.0001f) go.transform.localScale = Vector3.one * (size / cur);
        b = Bounds(go);
        // 바닥 중심을 pos에 맞춘다
        go.transform.position += pos - new Vector3(b.center.x, b.min.y, b.center.z);
        return go;
    }

    static void Islet(Transform parent, string model, Vector3 pos, float width)
    {
        var pivot = new GameObject(model + "_float").transform;
        pivot.SetParent(parent);
        pivot.position = pos;
        var go = Place(model, pivot, pos, width, Axis.Width, R(0, 360));
        var b = Bounds(go);
        go.transform.position += pos - b.center + Vector3.up * b.extents.y * 0.3f;
        var bob = pivot.gameObject.AddComponent<FloatBob>();
        bob.amplitude = Mathf.Clamp(width * 0.05f, 0.15f, 0.8f);
        bob.period = R(5f, 9f);
        bob.tiltDegrees = 1.2f;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static readonly List<MeshCollider> groundColliders = new List<MeshCollider>();

    static void AddColliders(GameObject go)
    {
        groundColliders.Clear();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            groundColliders.Add(mc);
        }
    }

    static void RemoveColliders(GameObject go)
    {
        foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) Object.DestroyImmediate(mc);
        groundColliders.Clear();
    }

    // 다른 열린 씬의 콜라이더와 섞이지 않도록 섬 콜라이더에만 레이캐스트
    static float GroundY(Vector3 xz, float fromY)
    {
        Physics.SyncTransforms();
        var ray = new Ray(new Vector3(xz.x, fromY, xz.z), Vector3.down);
        float best = float.NegativeInfinity;
        foreach (var c in groundColliders)
            if (c.Raycast(ray, out var hit, 500f)) best = Mathf.Max(best, hit.point.y);
        return float.IsNegativeInfinity(best) ? 0f : best;
    }

    static Vector3 Ground(Vector3 xz) => new Vector3(xz.x, GroundY(xz, 40f) - 0.05f, xz.z);

    // 발자국 둘레의 가장 낮은 지면(섬 위의 작은 장식 나무에 걸려 떠 보이지 않게)
    static Vector3 GroundMin(Vector3 xz, float radius)
    {
        float y = GroundY(xz, 40f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            y = Mathf.Min(y, GroundY(xz + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius, 40f));
        }
        return new Vector3(xz.x, y - 0.1f, xz.z);
    }

    static void PointLight(Transform parent, Vector3 pos, Color c, float intensity, float range)
    {
        var l = new GameObject("WarmLight").AddComponent<Light>();
        l.transform.SetParent(parent);
        l.transform.position = pos;
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
    }

    // ---------------- 폭포 ----------------
    static void Waterfall(Transform parent, Material mat, float angleDeg, float radius, float width, float length, Vector3 center = default)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        var top = center + dir * radius;
        top.y = GroundY(center + dir * (radius - 1.2f), 60f) - 0.1f;
        var go = new GameObject("Waterfall");
        go.transform.SetParent(parent);
        go.transform.position = top;
        go.transform.rotation = Quaternion.LookRotation(dir);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = WaterfallMesh(width, length);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        // 아래쪽 물안개
        var mist = new GameObject("Mist").AddComponent<ParticleSystem>();
        mist.transform.SetParent(go.transform, false);
        mist.transform.localPosition = new Vector3(0, -length * 0.85f, length * 0.12f);
        var m = mist.main;
        m.startLifetime = 4f;
        m.startSpeed = 0.6f;
        m.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
        m.startColor = new Color(1f, 0.95f, 1f, 0.35f);
        m.maxParticles = 60;
        var e = mist.emission; e.rateOverTime = 10f;
        var s = mist.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 1.5f;
        Fade(mist);
        mist.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftParticleMat();
    }

    // 섬 가장자리에서 살짝 앞으로 휘어 떨어지는 띠. v=1이 위.
    static Mesh WaterfallMesh(float width, float length)
    {
        const int seg = 24;
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            float y = -t * length;
            float z = Mathf.Sqrt(t) * length * 0.12f + 0.3f;
            float w = width * (1f + t * 0.8f);
            verts.Add(new Vector3(-w * 0.5f, y, z)); uvs.Add(new Vector2(0, 1 - t));
            verts.Add(new Vector3(w * 0.5f, y, z)); uvs.Add(new Vector2(1, 1 - t));
            if (i < seg)
            {
                int k = i * 2;
                tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            }
        }
        var mesh = new Mesh { name = "Waterfall" };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var path = $"{MatDir}/Waterfall_{width:0.0}_{length:0}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------------- 구름 ----------------
    static void Clouds(Transform parent, Mats mats, float scale = 1f, float yOffset = 0f)
    {
        var cloudsRoot = new GameObject("CloudSea").transform;
        cloudsRoot.SetParent(parent);
        var mat = mats.Get(yOffset == 0f ? "Cloud" : "Cloud_Lobby", "LobbyIsland/SoftCloud");
        mat.SetFloat("_HeightMin", -40f + yOffset);
        mat.SetFloat("_HeightMax", -18f + yOffset);
        mat.SetColor("_LitColor", new Color(1f, 0.86f, 0.9f));
        mat.SetColor("_ShadeColor", new Color(0.66f, 0.6f, 0.86f));
        mat.SetColor("_RimColor", new Color(1.3f, 0.75f, 0.6f));
        var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        // 아래 구름 바다: 큰 덩어리를 넓게 흩뿌림
        for (int i = 0; i < 260; i++)
        {
            float ang = R(0, Mathf.PI * 2f);
            float dist = Mathf.Sqrt(R(0.0f, 1f)) * 260f * scale;
            var p = new Vector3(Mathf.Cos(ang) * dist, R(-34f, -24f) + yOffset, Mathf.Sin(ang) * dist + 60f * scale);
            float s = R(10f, 26f) * (0.7f + dist / (260f * scale)) * Mathf.Sqrt(scale);
            Puff(cloudsRoot, sphere, mat, p, new Vector3(s * R(1.2f, 1.8f), s * R(0.45f, 0.7f), s));
        }
        // 섬 근처를 감싸는 낮은 뭉게구름
        for (int i = 0; i < 18; i++)
        {
            float ang = R(0, Mathf.PI * 2f);
            var p = new Vector3(Mathf.Cos(ang) * R(14f, 30f) * scale, R(-22f, -14f) + yOffset, Mathf.Sin(ang) * R(14f, 30f) * scale);
            float s = R(5f, 9f) * scale;
            Puff(cloudsRoot, sphere, mat, p, new Vector3(s * 1.5f, s * 0.8f, s));
        }
    }

    static void Puff(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject("Puff");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    // ---------------- 파티클 ----------------
    static void Petals(Transform parent, Vector3 pos, Mats mats)
    {
        var ps = new GameObject("SakuraPetals").AddComponent<ParticleSystem>();
        ps.transform.SetParent(parent);
        ps.transform.position = pos;
        var m = ps.main;
        m.startLifetime = 9f;
        m.startSpeed = 0.4f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.85f), new Color(1f, 0.9f, 0.95f));
        m.gravityModifier = 0.03f;
        m.maxParticles = 300;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.prewarm = true;
        var e = ps.emission; e.rateOverTime = 22f;
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = 4f;
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(-1.4f, -0.6f); v.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.2f); v.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.2f);
        var n = ps.noise; n.enabled = true; n.strength = 0.6f; n.frequency = 0.3f;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        Fade(ps);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = PetalMat();
    }

    static void Smoke(Transform parent, Vector3 pos, Mats mats)
    {
        var ps = new GameObject("ChimneySmoke").AddComponent<ParticleSystem>();
        ps.transform.SetParent(parent);
        ps.transform.position = pos;
        ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
        var m = ps.main;
        m.startLifetime = 6f;
        m.startSpeed = 0.9f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        m.startColor = new Color(1f, 0.96f, 1f, 0.5f);
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.prewarm = true;
        var e = ps.emission; e.rateOverTime = 4f;
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 8f; s.radius = 0.15f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 3f));
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World; v.x = new ParticleSystem.MinMaxCurve(-0.5f, -0.3f); v.y = new ParticleSystem.MinMaxCurve(0f, 0f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        Fade(ps);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftParticleMat();
    }

    static void Fade(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
        col.color = g;
    }

    static Material SoftParticleMat() => ParticleMat("SoftParticle", SoftTexture(), true);
    static Material PetalMat() => ParticleMat("Petal", PetalTexture(), false);

    static Material ParticleMat(string name, Texture2D tex, bool soft)
    {
        var path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        else return mat; // 이미 있으면 사용자가 바꾼 값을 그대로 둔다
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", soft ? Color.white : new Color(1.3f, 1.1f, 1.2f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D SoftTexture() => MakeTexture("SoftDot", (x, y) =>
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(0.5f, 0.5f)) * 2f;
        return Mathf.Pow(Mathf.Clamp01(1 - d), 2f);
    });

    // 끝이 살짝 갈라진 벚꽃잎 모양
    static Texture2D PetalTexture() => MakeTexture("Petal", (x, y) =>
    {
        float px = (x - 0.5f) * 2f, py = (y - 0.5f) * 2f;
        float shape = (px * px) / 0.35f + (py * py);
        float notch = py > 0.6f && Mathf.Abs(px) < 0.12f ? 1f : 0f;
        return Mathf.Clamp01((1f - shape) * 6f) * (1 - notch);
    });

    static Texture2D MakeTexture(string name, System.Func<float, float, float> alpha)
    {
        var path = $"{MatDir}/{name}.png";
        if (!File.Exists(path))
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    t.SetPixel(x, y, new Color(1, 1, 1, alpha((x + 0.5f) / n, (y + 0.5f) / n)));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------- 유틸 ----------------
    class Mats
    {
        public Material Get(string name, string shader)
        {
            var path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }
    }

    static string Capture(Camera cam, string path)
    {
        const int size = 1024;
        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        var prevAspect = cam.aspect;
        cam.aspect = 1f;
        cam.targetTexture = rt;
        // 함께 열린 다른 씬은 렌더하는 동안만 잠시 숨겼다가 원래대로 돌린다
        var hidden = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s == cam.gameObject.scene || !s.isLoaded) continue;
            foreach (var go in s.GetRootGameObjects())
                if (go.activeSelf) { go.SetActive(false); hidden.Add(go); }
        }
        // 비동기 셰이더 컴파일 중이면 메시가 빠진 채 찍히므로 캡처 동안은 동기 컴파일
        bool asyncCompile = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try { cam.Render(); cam.Render(); }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncCompile;
            foreach (var go in hidden) go.SetActive(true);
        }
        cam.targetTexture = null;
        cam.aspect = prevAspect;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        return "captured " + path;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

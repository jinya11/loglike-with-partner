using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 기존 Lobby.unity의 기능(포털, 난이도 석판, 기지 업그레이드, 동반자 플랫폼, UI)은 그대로 두고
// 겉모습만 "노을 하늘의 떠 있는 섬"으로 바꾼다.
// - 발판: ProBuilder로 만든 넓고 평평한 타원형 잔디 대지(두 광장 + 오두막 자리를 모두 덮음)
// - 그 밑: Meshy 섬 모델(윗면을 깎아 발판 밑면에 맞춤)이 절벽과 아랫부분을 담당
// - 분위기(하늘·구름·폭포·꽃잎·포스트프로세싱)는 "Atmosphere"에 처음 한 번만 만들고, 다시 실행해도 건드리지 않는다
// PrototypeBuilder가 로비를 처음부터 다시 만들면 이 메뉴를 한 번 더 실행하면 된다.
public static partial class LobbyIslandBuilder
{
    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    const string EnvName = "LobbyIslandEnv";
    const string AtmosphereName = "Atmosphere";
    const float CompanionX = 36f; // 동반자 플랫폼 중심(PrototypeBuilder 로비 배치와 동일)

    // 발판 타원: 메인 광장(0,0)과 동반자 광장(36,0)(각 반지름 ~19.5), 북쪽 오두막 자리까지 덮는다
    static readonly Vector3 PlatformCenter = new Vector3(CompanionX * 0.5f, 0f, 6f);
    const float PlatformA = 46f, PlatformB = 42f, PlatformThickness = 2.5f;

    [MenuItem("Tools/Lobby Island/Apply To Lobby Scene")]
    public static void ApplyToLobbyMenu() => ApplyToLobby(null);

    public static string ApplyToLobby(string capturePath)
    {
        rng = new System.Random(11);
        EnsureFolder(MatDir);
        var prevActive = SceneManager.GetActiveScene();
        var lobby = SceneManager.GetSceneByPath(LobbyPath);
        bool openedHere = false;
        if (!lobby.isLoaded)
        {
            lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            openedHere = true;
        }
        SceneManager.SetActiveScene(lobby);

        // 분위기 오브젝트는 사용자가 손볼 수 있으니 보존하고, 나머지(발판·섬·소품)만 다시 만든다
        Transform keptAtmosphere = null;
        foreach (var go in lobby.GetRootGameObjects())
        {
            if (go.name != EnvName) continue;
            var atm = go.transform.Find(AtmosphereName);
            if (atm != null && keptAtmosphere == null) { atm.SetParent(null, true); keptAtmosphere = atm; }
            Object.DestroyImmediate(go);
        }
        var root = new GameObject(EnvName).transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, lobby);
        var mats = new Mats();

        // ---- 발판(ProBuilder) ----
        var grass = Lit("Lobby_Grass", new Color(0.7f, 0.84f, 0.6f));
        var rock = Lit("Lobby_Rock", new Color(0.76f, 0.72f, 0.86f));
        var platform = BuildPlatform(root, grass, rock);

        // ---- 발판 밑 Meshy 섬: 발판 크기에 맞춰 늘이고, 윗면을 발판 밑면 높이로 깎는다 ----
        float bottomY = -PlatformThickness + 0.05f;
        var island = Place("island", root, PlatformCenter, 1f, Axis.Width, 0f);
        island.name = "IslandBase";
        var ib = Bounds(island);
        island.transform.localScale = Vector3.Scale(island.transform.localScale,
            new Vector3(PlatformA * 2.12f / ib.size.x, 0.9f * PlatformA * 2f / ib.size.x, PlatformB * 2.12f / ib.size.z));
        ib = Bounds(island);
        island.transform.position += PlatformCenter - new Vector3(ib.center.x, ib.max.y, ib.center.z);
        AddColliders(island);
        SinkToEllipseRim(island, bottomY);
        FlattenAbove(island, bottomY, "Island_Lobby", new[] { (PlatformCenter, 500f) });
        RemoveColliders(island);

        // ---- 소품: 발판 윗면(y=0)에 바로 세운다 ----
        var cottagePos = new Vector3(-6f, 0f, 27f);
        var treePos = new Vector3(13f, 0f, 26f);
        var cottage = Place("cottage2", root, cottagePos, 10f, Axis.Height, CottageYaw);
        cottage.name = "Cottage";
        var tree = Place("sakura", root, treePos, 14f, Axis.Height, 30f);
        tree.name = "SakuraBig";
        Place("sakura", root, new Vector3(-26f, 0f, 14f), 9f, Axis.Height, 120f);
        Place("sakura", root, new Vector3(18f, 0f, -26f), 8f, Axis.Height, 250f);
        Place("sakura", root, new Vector3(58f, 0f, 20f), 9f, Axis.Height, 60f);
        var lampPos = cottagePos + new Vector3(7f, 0f, -6f);
        Place("lamppost", root, lampPos, 4.2f, Axis.Height, 180f);
        for (int i = 0; i < 5; i++)
        {
            float a = (195f + i * 18f) * Mathf.Deg2Rad;
            var p = cottagePos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 9f;
            Place("fence", root, p, 2.6f, Axis.Width, -a * Mathf.Rad2Deg + 90f);
        }
        var cb = Bounds(cottage);
        PointLight(root, cb.center + new Vector3(0, -cb.extents.y * 0.2f, -cb.extents.z - 0.8f), new Color(1f, 0.62f, 0.3f), 4f, 12f);
        PointLight(root, lampPos + Vector3.up * 3.9f, new Color(1f, 0.7f, 0.35f), 3f, 8f);

        // ---- 광장 겉모습: 기존 오브젝트는 그대로 두고 파스텔 머티리얼만 바꿔 끼운다 ----
        Reskin(lobby);

        // ---- 분위기: 이미 있으면 그대로 두고, 없을 때만 처음 한 번 만든다 ----
        Camera mainCam = null;
        foreach (var go in lobby.GetRootGameObjects())
            if (go.CompareTag("MainCamera")) mainCam = go.GetComponent<Camera>();
        if (keptAtmosphere != null) keptAtmosphere.SetParent(root, true);
        else BuildAtmosphere(lobby, root, mats, platform, tree, cb, mainCam);

        EditorSceneManager.MarkSceneDirty(lobby);
        if (!EditorSceneManager.SaveScene(lobby)) Debug.LogError("[LobbyIsland] 로비 씬 저장 실패");

        string result = "lobby updated" + (keptAtmosphere != null ? " (atmosphere kept)" : " (atmosphere created)");
        if (!string.IsNullOrEmpty(capturePath) && mainCam != null)
        {
            result += "; " + Capture(mainCam, capturePath);
            // 전체 구도를 보는 와이드 샷도 함께
            var wide = new GameObject("WideShot").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(wide.gameObject, lobby);
            wide.fieldOfView = 45f;
            wide.farClipPlane = 700f;
            wide.transform.position = new Vector3(18f, 30f, -115f);
            wide.transform.LookAt(new Vector3(18f, -10f, 6f));
            wide.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            result += "; " + Capture(wide, capturePath.Replace(".png", "_wide.png"));
            Object.DestroyImmediate(wide.gameObject);
        }

        if (openedHere)
        {
            if (prevActive.IsValid() && prevActive.isLoaded) SceneManager.SetActiveScene(prevActive);
            EditorSceneManager.CloseScene(lobby, true);
        }
        else if (prevActive.IsValid() && prevActive.isLoaded) SceneManager.SetActiveScene(prevActive);
        return result;
    }

    // 윗면 y=0, 두께 PlatformThickness의 타원형 대지. 가장자리는 살짝 울퉁불퉁하게, 옆면은 바위 머티리얼.
    static ProBuilderMesh BuildPlatform(Transform parent, Material top, Material side)
    {
        var pb = ShapeGenerator.GenerateCylinder(PivotLocation.Center, 72, 1f, PlatformThickness, 0);
        pb.gameObject.name = "IslandPlatform";
        pb.transform.SetParent(parent, false);
        // 윗면을 광장 바닥(y=0)보다 살짝 낮춰 겹침 깜빡임을 피한다
        pb.transform.position = PlatformCenter + Vector3.down * (PlatformThickness * 0.5f + 0.06f);
        var pos = pb.positions.ToArray();
        for (int i = 0; i < pos.Length; i++)
        {
            var p = pos[i];
            float ang = Mathf.Atan2(p.z, p.x);
            float wobble = 1f + 0.035f * Mathf.Sin(ang * 5f) + 0.025f * Mathf.Sin(ang * 11f + 1.3f);
            // 아래쪽 테두리는 조금 안으로 들여 절벽이 섬 쪽으로 기울게
            float taper = p.y < 0f ? 0.96f : 1f;
            pos[i] = new Vector3(p.x * PlatformA * wobble * taper, p.y, p.z * PlatformB * wobble * taper);
        }
        pb.positions = pos;
        pb.SetMaterial(pb.faces, side);
        pb.SetMaterial(pb.faces.Where(f => UnityEngine.ProBuilder.Math.Normal(pb, f).y > 0.5f), top);
        pb.ToMesh();
        pb.Refresh();
        pb.gameObject.AddComponent<MeshCollider>();
        return pb;
    }

    static Vector3 EllipsePoint(float angleDeg, float scale = 1f)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        return PlatformCenter + new Vector3(Mathf.Cos(a) * PlatformA, 0f, Mathf.Sin(a) * PlatformB) * scale;
    }

    // 발판 둘레(조금 안쪽) 중 가장 낮은 섬 지면이 topY가 되도록 섬을 옮긴다 → 발판 가장자리 밑이 비지 않음
    static void SinkToEllipseRim(GameObject island, float topY)
    {
        Physics.SyncTransforms();
        float from = Bounds(island).max.y + 5f;
        float min = float.PositiveInfinity;
        for (int i = 0; i < 72; i++)
        {
            var p = EllipsePoint(i * 5f, 0.93f);
            var ray = new Ray(new Vector3(p.x, from, p.z), Vector3.down);
            foreach (var col in groundColliders)
                if (col.Raycast(ray, out var hit, 1000f)) min = Mathf.Min(min, hit.point.y);
        }
        if (float.IsPositiveInfinity(min)) return;
        island.transform.position += Vector3.up * (topY - min);
        Physics.SyncTransforms();
    }

    // 영역 안에서 topY 위로 솟은 정점을 topY로 눌러 평평하게 한다(메시 사본을 에셋으로 저장)
    static void FlattenAbove(GameObject island, float topY, string assetName, (Vector3 c, float r)[] areas)
    {
        foreach (var mf in island.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = assetName;
            var verts = mesh.vertices;
            var l2w = mf.transform.localToWorldMatrix;
            var w2l = mf.transform.worldToLocalMatrix;
            for (int i = 0; i < verts.Length; i++)
            {
                var w = l2w.MultiplyPoint3x4(verts[i]);
                if (w.y <= topY) continue;
                foreach (var (c, r) in areas)
                {
                    float dx = w.x - c.x, dz = w.z - c.z;
                    if (dx * dx + dz * dz > r * r) continue;
                    w.y = topY;
                    verts[i] = w2l.MultiplyPoint3x4(w);
                    break;
                }
            }
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            var path = $"{MatDir}/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            mf.sharedMesh = mesh;
            var mc = mf.GetComponent<MeshCollider>();
            if (mc) mc.sharedMesh = mesh;
        }
        Physics.SyncTransforms();
    }

    // 하늘·안개·조명·포스트프로세싱·구름·폭포·꽃잎·주변 섬 (처음 한 번만)
    static void BuildAtmosphere(Scene lobby, Transform root, Mats mats, ProBuilderMesh platform, GameObject tree, Bounds cottageBounds, Camera mainCam)
    {
        var atm = new GameObject(AtmosphereName).transform;
        atm.SetParent(root);

        // 폭포는 발판 가장자리에서 떨어지게(발판 콜라이더로 높이를 잰다)
        groundColliders.Clear();
        groundColliders.Add(platform.GetComponent<MeshCollider>());
        var wfMat = mats.Get("Waterfall", "LobbyIsland/Waterfall");
        foreach (var (ang, width, len) in new[] { (-140f, 3.2f, 70f), (-122f, 2.2f, 62f), (-35f, 2.6f, 66f) })
        {
            float a = ang * Mathf.Deg2Rad;
            float r = 1f / Mathf.Sqrt(Mathf.Pow(Mathf.Cos(a) / PlatformA, 2) + Mathf.Pow(Mathf.Sin(a) / PlatformB, 2));
            Waterfall(atm, wfMat, ang, r * 0.97f, width, len, PlatformCenter);
        }
        groundColliders.Clear();

        Petals(atm, Bounds(tree).center + Vector3.up * 2f, mats);
        Smoke(atm, new Vector3(cottageBounds.center.x + cottageBounds.extents.x * 0.45f, cottageBounds.max.y - 0.3f, cottageBounds.center.z + cottageBounds.extents.z * 0.2f), mats);

        var islets = new GameObject("Islets").transform;
        islets.SetParent(atm);
        Islet(islets, "islet", new Vector3(-85f, -10f, 30f), 16f);
        Islet(islets, "islet", new Vector3(-75f, -20f, -30f), 10f);
        Islet(islets, "islet", new Vector3(115f, -6f, 40f), 14f);
        Islet(islets, "islet", new Vector3(-40f, -32f, -62f), 5f);
        Islet(islets, "islet", new Vector3(25f, -38f, -68f), 4f);
        Islet(islets, "islet", new Vector3(85f, -30f, -55f), 4.5f);
        Islet(islets, "castleislet", new Vector3(-80f, 18f, 150f), 34f);
        Islet(islets, "castleislet", new Vector3(95f, 26f, 175f), 40f);
        Islet(islets, "castleislet", new Vector3(20f, 8f, 120f), 20f);
        Islet(islets, "islet", new Vector3(-140f, 34f, 230f), 30f);

        Clouds(atm, mats, 2f, -40f);

        Light sun = null;
        foreach (var go in lobby.GetRootGameObjects())
            if (go.name == "Directional Light") sun = go.GetComponent<Light>();
        if (sun == null)
        {
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(atm);
            sun.type = LightType.Directional;
        }
        sun.color = new Color(1f, 0.82f, 0.76f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.5f;
        sun.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.6f, 0.8f, -0.25f).normalized);
        // 하늘 머티리얼은 데모 씬과 공유하므로 값은 건드리지 않고 연결만 한다
        ApplySkyAndFog(mats, sun, 90f, 420f, configureSky: false);

        if (mainCam != null)
        {
            var data = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            mainCam.farClipPlane = Mathf.Max(mainCam.farClipPlane, 700f);
            mainCam.clearFlags = CameraClearFlags.Skybox;
        }
        var volGo = new GameObject("Global Volume");
        volGo.transform.SetParent(atm);
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) ?? BuildProfile();
    }

    // 로비 광장 오브젝트를 이름으로 찾아 파스텔 머티리얼로 교체(메시·콜라이더·스크립트는 그대로)
    static void Reskin(Scene lobby)
    {
        var plaza = Lit("Lobby_Plaza", new Color(0.86f, 0.83f, 0.8f));      // 연한 돌바닥
        var wall = Lit("Lobby_Wall", new Color(0.86f, 0.82f, 0.93f));       // 연보라 석재
        var pillar = Lit("Lobby_Pillar", new Color(0.97f, 0.95f, 0.97f));   // 흰 기둥
        var cap = Lit("Lobby_Cap", new Color(0.62f, 0.66f, 0.82f));         // 청회색(오두막 지붕 톤)
        var path = Lit("Lobby_Path", new Color(0.98f, 0.93f, 0.84f));       // 크림색 돌길
        var tablet = Lit("Lobby_Tablet", new Color(0.8f, 0.78f, 0.9f));
        foreach (var rootGo in lobby.GetRootGameObjects())
        {
            if (rootGo.name == EnvName) continue;
            foreach (var r in rootGo.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material m = r.name switch
                {
                    "Floor" or "Floor (1)" => plaza,
                    "OuterWall" or "OuterWall (1)" => wall,
                    "Pillar" => pillar,
                    "Cap" or "Base" or "Brazier" or "Stairs" or "TabletBase" => cap,
                    "Dais" => wall,
                    "GoldInlay" or "GoldInlay2" or "Halo" => path,
                    "GreatTablet" => tablet,
                    _ => null,
                };
                if (m == null) continue;
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
            }
        }
    }

    static Material Lit(string name, Color c)
    {
        var path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", c);
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}

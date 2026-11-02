using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 로비 구름 바다 틈의 주기적인 빛기둥(LightPillars)을 Atmosphere 밑에 추가한다. 다시 실행하면 그 오브젝트만 새로 만든다.
public static partial class LobbyIslandBuilder
{
    [MenuItem("Tools/Lobby Island/Add Light Pillars")]
    public static void AddLightPillarsMenu() => AddLightPillars(null);

    public static string AddLightPillars(string capturePath)
    {
        var prevActive = SceneManager.GetActiveScene();
        var lobby = SceneManager.GetSceneByPath(LobbyPath);
        bool openedHere = false;
        if (!lobby.isLoaded)
        {
            lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            openedHere = true;
        }

        // 사용자가 계층을 옮겼을 수 있으니 이름으로 씬 전체에서 찾는다
        Transform atm = null, clouds = null;
        Camera mainCam = null;
        foreach (var go in lobby.GetRootGameObjects())
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (atm == null && t.name == AtmosphereName) atm = t;
                if (clouds == null && t.name == "CloudSea") clouds = t;
            }
            if (go.CompareTag("MainCamera")) mainCam = go.GetComponent<Camera>();
        }
        foreach (var old in Object.FindObjectsByType<LightPillars>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (old.gameObject.scene == lobby) Object.DestroyImmediate(old.gameObject);

        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/LightPillar.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("LobbyIsland/LightPillar"));
            AssetDatabase.CreateAsset(mat, $"{MatDir}/LightPillar.mat");
        }

        var go2 = new GameObject("LightPillars");
        if (atm != null) go2.transform.SetParent(atm, false);
        else SceneManager.MoveGameObjectToScene(go2, lobby);
        var lp = go2.AddComponent<LightPillars>();
        lp.material = mat;
        lp.cloudRoot = clouds;
        lp.avoidCenter = PlatformCenter;

        EditorSceneManager.MarkSceneDirty(lobby);
        // 사용자가 편집 중인(저장 안 된) 로비면 저장은 사용자에게 맡긴다
        if (openedHere) EditorSceneManager.SaveScene(lobby);

        string result = "light pillars added";
        if (!string.IsNullOrEmpty(capturePath) && mainCam != null)
        {
            SceneManager.SetActiveScene(lobby);
            var a = lp.PreviewAt(new Vector3(-70f, lp.baseY, 170f), mainCam.transform.position, lp.glowColors[0]);
            var b = lp.PreviewAt(new Vector3(95f, lp.baseY, 230f), mainCam.transform.position, lp.glowColors[2]);
            result += "; " + Capture(mainCam, capturePath);
            var wide = new GameObject("WideShot").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(wide.gameObject, lobby);
            wide.fieldOfView = 45f;
            wide.farClipPlane = 700f;
            wide.transform.position = new Vector3(18f, 30f, -115f);
            wide.transform.LookAt(new Vector3(18f, -10f, 6f));
            wide.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            a = lp.PreviewAt(new Vector3(-60f, lp.baseY, 120f), wide.transform.position, lp.glowColors[0]);
            b = lp.PreviewAt(new Vector3(110f, lp.baseY, 90f), wide.transform.position, lp.glowColors[2]);
            result += "; " + Capture(wide, capturePath.Replace(".png", "_wide.png"));
            Object.DestroyImmediate(wide.gameObject);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        if (prevActive.IsValid() && prevActive.isLoaded) SceneManager.SetActiveScene(prevActive);
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
        return result;
    }
}

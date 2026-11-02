using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 로비 섬 씬을 잠시 추가로 열어 캡처만 하고 닫는 도구(씬은 저장하지 않음).
public static class LobbyIslandDebug
{
    public static string Capture(string path, bool fog, bool volume, float pitchOffset)
    {
        var prev = SceneManager.GetActiveScene();
        var s = EditorSceneManager.OpenScene("Assets/Scenes/LobbyIsland.unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(s);
        RenderSettings.fog = fog;
        Camera cam = null;
        foreach (var r in s.GetRootGameObjects())
        {
            if (cam == null) cam = r.GetComponentInChildren<Camera>();
            var v = r.transform.Find("Global Volume");
            if (v) v.gameObject.SetActive(volume);
        }
        cam.transform.Rotate(pitchOffset, 0, 0, Space.Self);
        var method = typeof(LobbyIslandBuilder).GetMethod("Capture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var res = (string)method.Invoke(null, new object[] { cam, path });
        SceneManager.SetActiveScene(prev);
        EditorSceneManager.CloseScene(s, true);
        return res;
    }
}

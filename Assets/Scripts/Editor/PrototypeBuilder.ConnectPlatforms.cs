using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 동반자 선택 플랫폼을 메인 로비 플랫폼에 붙여 로비 씬에 저장한다 (실제 처리는 LobbyPlatformConnector).
/// 메뉴: Roguelike/Connect Companion Platform
/// 에디터가 준비되면(컴파일/임포트/Play 끝난 뒤) 자동으로 한 번 확인해 적용·저장한다.
/// </summary>
public static partial class PrototypeBuilder
{
    const string LobbyScenePath = "Assets/Scenes/Lobby.unity";

    [MenuItem("Roguelike/Connect Companion Platform")]
    public static void ConnectCompanionPlatform()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PrototypeBuilder] Play를 멈춘 뒤 실행하세요.");
            return;
        }
        ApplyToLobbyScene(true);
    }

    /// <summary>로비 씬(열려 있지 않으면 잠깐 열어서)에 연결을 적용하고 저장.</summary>
    static void ApplyToLobbyScene(bool logIfNothing)
    {
        var lobby = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(LobbyScenePath);
        bool openedHere = false;
        if (!lobby.isLoaded)
        {
            if (!System.IO.File.Exists(LobbyScenePath)) return;
            lobby = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        string result = LobbyPlatformConnector.Connect();
        if (result != null) Debug.Log($"[PrototypeBuilder] {result}");
        bool altars = ArrangeCompanionAltars();
        if (result != null || altars)
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            SceneView.RepaintAll();
            Debug.Log("[PrototypeBuilder] 로비 씬 저장 완료");
        }
        else if (logIfNothing)
        {
            Debug.Log("[PrototypeBuilder] 동반자 플랫폼은 이미 메인 플랫폼에 붙어 있습니다.");
        }

        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
    }

    /// <summary>에디터가 한가해질 때까지 기다렸다가 한 번 자동 적용.</summary>
    [InitializeOnLoadMethod]
    static void AutoConnectWhenIdle()
    {
        EditorApplication.update -= WaitAndConnect;
        EditorApplication.update += WaitAndConnect;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.update -= WaitAndConnect;
        EditorApplication.update += WaitAndConnect;
    }

    static void WaitAndConnect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return; // 다음 프레임에 다시 확인
        EditorApplication.update -= WaitAndConnect;
        try { ApplyToLobbyScene(false); }
        catch (System.Exception e) { Debug.LogException(e); }
    }
}

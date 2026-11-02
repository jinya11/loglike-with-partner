using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 개발자 모드 단축키 (DevConfig). 개발자 모드가 아니면 생성되지 않음.
/// - unlockAllKey(F2): 모든 감시자 잠금 해제 on/off (저장 데이터는 그대로)
/// </summary>
public class DevHotkeys : MonoBehaviour
{
    DevConfig cfg;
    string toast;
    float toastLeft;
    GUIStyle style;
    bool wasDown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!DevConfig.Enabled) return;
        var go = new GameObject("[Dev] Hotkeys");
        DontDestroyOnLoad(go);
        go.AddComponent<DevHotkeys>();
    }

    void Awake() => cfg = DevConfig.I;

    void Update()
    {
        if (toastLeft > 0f) toastLeft -= Time.unscaledDeltaTime;
        // wasPressedThisFrame는 에디터 포커스/프레임 처리에 따라 놓칠 수 있어 직접 눌림 변화를 추적
        var kb = Keyboard.current;
        bool down = kb != null && kb[cfg.unlockAllKey].isPressed;
        bool pressed = down && !wasDown;
        wasDown = down;
        if (!pressed) return;
        cfg.unlockAllCompanions = !cfg.unlockAllCompanions;
        LobbyInteractable.RefreshAll();
        Show(cfg.unlockAllCompanions ? "[개발자] 감시자 전체 해금: 켜짐" : "[개발자] 감시자 전체 해금: 꺼짐");
    }

    void Show(string msg) { toast = msg; toastLeft = 2f; }

    void OnGUI()
    {
        if (toastLeft <= 0f) return;
        style ??= new GUIStyle(GUI.skin.box) { fontSize = cfg.fontSize + 2, richText = true, padding = new RectOffset(10, 10, 6, 6) };
        var c = new GUIContent(toast);
        Vector2 s = style.CalcSize(c);
        GUI.Box(new Rect((Screen.width - s.x) * 0.5f, 60f, s.x, s.y), c, style);
    }
}

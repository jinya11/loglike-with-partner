using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ESC 일시정지 = 설정 창 (HUD_Settings). 로비/인게임: ESC 즉시 Time.timeScale = 0 + 입력 잠금 후 설정 창.
/// 설정 창 안의 귀환 버튼 → 확인 창 → 거점(인게임) / 타이틀(로비). 타이틀에서는 '설정' 버튼으로만 열림 (시간 정지 없음).
/// 다른 창이 열려 있으면 무시: 각 창이 여는 동안 GameManager.InputLocked를 켜므로 InputBlocked 하나로 판정
/// (닫힌 프레임도 InputBlocked라서 창을 닫은 ESC가 그대로 설정 창을 열지 않음).
/// ESC: 확인 창 → 닫기, 설정 창 → 닫기 (시간/입력 복구).
/// </summary>
[DefaultExecutionOrder(1000)] // 창들이 이번 프레임 ESC를 먼저 처리하도록
public class EscapeToBase : Singleton<EscapeToBase>
{
    protected override bool Persistent => true;

    enum SceneKind { Title, Lobby, Stage }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (I == null) new GameObject("EscapeToBase").AddComponent<EscapeToBase>();
    }

    MainMenuConfig cfg;
    SettingsConfig scfg;
    ConfirmPopup popup;
    SettingsUI settings;
    SceneKind kind;
    float savedTimeScale = 1f;
    bool paused;
    bool leaving; // 타이틀 비동기 로드 중

    public bool SettingsOpen => settings && settings.IsOpen;

    protected override void Awake()
    {
        base.Awake();
        if (I != this) return;
        cfg = MainMenuConfig.I;
        scfg = SettingsConfig.I;
        if (scfg.settingsPrefab)
        {
            settings = Instantiate(scfg.settingsPrefab, transform); // 작은 화면 테두리도 이 안에 있어서 항상 생성
            settings.ReturnClicked += AskReturn;
            settings.CloseClicked += CloseSettings;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    protected override void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (m != LoadSceneMode.Single) return;
        leaving = false;
        var gm = GameManager.I;
        kind = cfg && s.name == cfg.mainMenuScene ? SceneKind.Title
            : gm && s.name == gm.Config.stage.lobbyScene ? SceneKind.Lobby : SceneKind.Stage;
        if (popup && popup.IsOpen) popup.Hide();
        if (settings) settings.Hide();
        paused = false; // 시간/입력은 GameManager가 씬 로드 때 초기화
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame || cfg == null) return;
        if (popup && popup.IsOpen) { popup.Hide(); return; }
        if (SettingsOpen) { CloseSettings(); return; }
        if (kind == SceneKind.Title) return;

        var gm = GameManager.I;
        if (gm == null || gm.State != GameState.Playing || gm.InputBlocked || gm.Transitioning || leaving) return;
        if (kind == SceneKind.Lobby && !cfg.escInLobbyToTitle) return;
        OpenSettings();
    }

    /// <summary>설정 창 열기 (타이틀 '설정' 버튼도 이걸 호출). 로비/인게임이면 시간 정지.</summary>
    public void OpenSettings()
    {
        if (!settings || settings.IsOpen) return;
        var gm = GameManager.I;
        if (kind != SceneKind.Title && gm)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            gm.InputLocked = true;
            paused = true;
        }
        settings.Show(kind == SceneKind.Stage ? scfg.returnToBaseLabel : kind == SceneKind.Lobby ? scfg.returnToTitleLabel : null);
    }

    public void CloseSettings()
    {
        if (!settings) return;
        settings.Hide();
        if (!paused) return;
        paused = false;
        Time.timeScale = savedTimeScale;
        if (GameManager.I) GameManager.I.InputLocked = false;
    }

    void AskReturn()
    {
        if (!popup)
        {
            if (!cfg.confirmPrefab) return;
            popup = Instantiate(cfg.confirmPrefab, transform);
        }
        bool lobby = kind == SceneKind.Lobby;
        popup.Show(lobby ? cfg.returnToTitlePrompt : cfg.returnToBasePrompt, lobby ? (System.Action)GoTitle : GoBase);
    }

    // 사망/클리어 후 R과 같은 경로 (런 초기화 + 지역 로테이션 + 로비 로드)
    void GoBase()
    {
        settings.Hide(); paused = false;
        GameManager.I.GoLobby();
    }

    void GoTitle()
    {
        settings.Hide(); paused = false;
        leaving = true;
        Time.timeScale = 1f;
        if (GameManager.I) GameManager.I.InputLocked = false;
        SceneManager.LoadSceneAsync(cfg.mainMenuScene);
    }
}

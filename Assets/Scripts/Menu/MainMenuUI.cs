using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 (uGUI 프리팹 HUD_MainMenu, MainMenu 씬 전용). 버튼 연결 + 비동기 씬 전환.
/// BGM AudioSource는 이 씬의 오브젝트 (DontDestroyOnLoad 아님 → 씬이 바뀌면 같이 사라짐).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] Button continueButton, newGameButton, settingsButton, quitButton;
    [SerializeField] ConfirmPopup confirm;
    [SerializeField] CanvasGroup fade;
    [SerializeField] AudioSource bgm;

    MainMenuConfig cfg;
    bool busy;

    void Awake()
    {
        cfg = MainMenuConfig.I;
        continueButton.onClick.AddListener(Continue);
        newGameButton.onClick.AddListener(NewGame);
        quitButton.onClick.AddListener(Quit);
        if (settingsButton) settingsButton.onClick.AddListener(() => { if (!busy && EscapeToBase.I) EscapeToBase.I.OpenSettings(); });
        continueButton.interactable = SaveData.HasSave();
        fade.alpha = 0f; fade.blocksRaycasts = false;
        if (bgm) bgm.volume = cfg.bgmVolume;
        Time.timeScale = 1f;
    }

    void Continue()
    {
        if (busy || !SaveData.HasSave()) return;
        StartCoroutine(GoLobby());
    }

    void NewGame()
    {
        if (busy) return;
        if (SaveData.HasSave()) confirm.Show(cfg.newGamePrompt, StartNew);
        else StartNew();
    }

    void StartNew()
    {
        if (busy) return;
        SaveData.DeleteSave();
        GrowthProgress.ResetCache();
        GameManager.I.ResetRun(); // 거점 성장 효과 다시 적용 (빈 저장 기준)
        StartCoroutine(GoLobby());
    }

    void Quit()
    {
        if (busy) return;
        busy = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator GoLobby()
    {
        busy = true;
        fade.blocksRaycasts = true;
        var op = SceneManager.LoadSceneAsync(GameManager.I.Config.stage.lobbyScene);
        op.allowSceneActivation = false;
        float t = 0f, dur = Mathf.Max(0.01f, cfg.fadeTime), vol = bgm ? bgm.volume : 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            fade.alpha = k;
            if (bgm) bgm.volume = vol * (1f - k);
            yield return null;
        }
        op.allowSceneActivation = true;
    }
}

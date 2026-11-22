using UnityEngine;

/// <summary>타이틀(메인 메뉴) + 인게임 ESC 귀환 수치. Resources/Config/MainMenuConfig.asset.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/MainMenu")]
public class MainMenuConfig : ScriptableObject
{
    const string Path = "Config/MainMenuConfig";
    static MainMenuConfig cached;
    public static MainMenuConfig I => cached ? cached : (cached = Resources.Load<MainMenuConfig>(Path));

    [Header("씬")]
    [Tooltip("타이틀 씬 이름 (빌드 0번). 거점 씬은 StageConfig.lobbyScene")] public string mainMenuScene = "MainMenu";

    [Header("타이틀")]
    [Tooltip("씬 전환 페이드 시간(초, 실시간)")] public float fadeTime = 0.6f;
    [Range(0f, 1f)] public float bgmVolume = 0.6f;

    [Header("확인 창 문구")]
    public string newGamePrompt = "저장된 진행을 지우고 새로 시작할까요?";
    public string returnToBasePrompt = "거점으로 돌아가시겠습니까?";
    public string returnToTitlePrompt = "타이틀로 돌아가시겠습니까?";

    [Header("ESC 귀환")]
    [Tooltip("확인 창 프리팹 (HUD_Confirm)")] public ConfirmPopup confirmPrefab;
    [Tooltip("거점(로비)에서도 ESC로 설정 창(귀환 버튼 = 타이틀로)")] public bool escInLobbyToTitle = true;
}

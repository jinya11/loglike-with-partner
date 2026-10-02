using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 스테이지 HUD (uGUI). 레이아웃/문구는 HUD_Stage 프리팹에서 편집.
/// 코드는 텍스트 전체를 덮어쓰지 않고, 프리팹에 적힌 문구를 서식으로 읽어 {0},{1}.. 자리에 값만 채움.
/// 상태별 문구(레벨업/특수 강화, 승리/사망, 점프, 보너스)는 오브젝트를 켜고 끄기만 함.
/// </summary>
public class Hud : MonoBehaviour
{
    [Header("Bars (Fill의 anchorMax.x로 비율 표시)")]
    [SerializeField] RectTransform hpFill;
    [Tooltip("서식: {0}=현재 HP, {1}=최대 HP")]
    [SerializeField] TMP_Text hpText;
    [SerializeField] RectTransform xpFill;
    [Header("Info")]
    [Tooltip("서식: {0}=레벨, {1}=스테이지, {2}=총 스테이지, {3}=스테이지 이름, {4}=처치, {5}=목표")]
    [SerializeField] TMP_Text infoText;
    [SerializeField] GameObject bonusBadge;
    [Tooltip("서식: {0}=Q 쿨타임, {1}=E 쿨타임")]
    [SerializeField] TMP_Text skillText;
    [SerializeField] string readyText = "준비";
    [SerializeField] GameObject jumpHint;
    [SerializeField] GameObject portalMessage;
    [SerializeField] TMP_Text promptText;
    [Header("Level Up")]
    [SerializeField] GameObject levelUpPanel;
    [SerializeField] GameObject normalTitle, specialTitle;
    [SerializeField] Button[] choiceButtons;
    [Tooltip("서식: {0}=번호, {1}=강화 이름, {2}=설명")]
    [SerializeField] TMP_Text[] choiceTexts;
    [Header("Result")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject victoryTitle, defeatTitle;

    string hpFmt, infoFmt, skillFmt;
    string[] choiceFmt;
    Interactable shownFocus;
    int lastHp = -1, lastMaxHp = -1, lastLevel = -1, lastKills = -1, lastQ = -1, lastE = -1, shownJump = -1;
    float lastXp = -1f;
    GameState lastState = (GameState)(-1);

    void Awake()
    {
        hpFmt = hpText.text; infoFmt = infoText.text; skillFmt = skillText.text;
        choiceFmt = new string[choiceTexts.Length];
        for (int i = 0; i < choiceTexts.Length; i++) choiceFmt[i] = choiceTexts[i].text;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int idx = i;
            choiceButtons[i].onClick.AddListener(() => GameManager.I.Choose(idx));
        }
    }

    void OnEnable() { if (GameManager.I) GameManager.I.ChoicesChanged += RefreshChoices; }
    void OnDisable() { if (GameManager.I) GameManager.I.ChoicesChanged -= RefreshChoices; }

    void LateUpdate()
    {
        var gm = GameManager.I;
        var stage = StageDirector.I;
        var player = PlayerController.I;
        if (gm == null || stage == null || player == null) return;
        var s = gm.Stats;

        int hp = Mathf.CeilToInt(s.hp), maxHp = Mathf.RoundToInt(s.maxHp);
        if (hp != lastHp || maxHp != lastMaxHp)
        {
            lastHp = hp; lastMaxHp = maxHp;
            SetFill(hpFill, s.hp / s.maxHp);
            hpText.SetText(hpFmt, hp, maxHp); // 숫자 전용 SetText: 문자열 할당 없음
        }

        float xp = (float)s.xp / gm.XpToNext;
        if (!Mathf.Approximately(xp, lastXp)) { lastXp = xp; SetFill(xpFill, xp); }

        if (s.level != lastLevel || stage.Kills != lastKills)
        {
            lastLevel = s.level; lastKills = stage.Kills;
            infoText.text = string.Format(infoFmt, s.level, gm.StageIndex + 1, gm.Config.stageCount,
                stage.Theme ? stage.Theme.displayName : "", stage.Kills, stage.KillTarget);
            if (bonusBadge) bonusBadge.SetActive(gm.InBonus);
        }

        int q = Mathf.CeilToInt(player.QLeft * 10f), e = Mathf.CeilToInt(player.ELeft * 10f);
        if (q != lastQ || e != lastE)
        {
            lastQ = q; lastE = e;
            skillText.text = string.Format(skillFmt, Cd(q), Cd(e));
        }
        int jump = s.canJump ? 1 : 0;
        if (jump != shownJump) { shownJump = jump; if (jumpHint) jumpHint.SetActive(s.canJump); }

        var focus = gm.State == GameState.Playing ? player.Focus : null;
        if (focus != shownFocus) { shownFocus = focus; promptText.text = focus != null ? focus.Prompt : ""; }

        bool portal = stage.PortalOpen && gm.State == GameState.Playing;
        if (portalMessage.activeSelf != portal) portalMessage.SetActive(portal);

        if (gm.State != lastState)
        {
            lastState = gm.State;
            levelUpPanel.SetActive(gm.State == GameState.LevelUp);
            bool end = gm.State == GameState.GameOver || gm.State == GameState.Victory;
            resultPanel.SetActive(end);
            if (end) { victoryTitle.SetActive(gm.State == GameState.Victory); defeatTitle.SetActive(gm.State == GameState.GameOver); }
        }
    }

    /// <summary>GameManager가 선택지를 만들 때마다 호출 (연속 레벨업 포함).</summary>
    void RefreshChoices()
    {
        var gm = GameManager.I;
        normalTitle.SetActive(!gm.ChoiceIsSpecial);
        specialTitle.SetActive(gm.ChoiceIsSpecial);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool has = i < gm.Choices.Count;
            choiceButtons[i].gameObject.SetActive(has);
            if (has) choiceTexts[i].text = string.Format(choiceFmt[i], i + 1, gm.Choices[i].name, gm.Choices[i].desc);
        }
    }

    string Cd(int tenths) => tenths > 0 ? (tenths / 10f).ToString("0.0") : readyText;

    public static void SetFill(RectTransform fill, float t)
    {
        var max = fill.anchorMax;
        max.x = Mathf.Clamp01(t);
        fill.anchorMax = max;
    }
}

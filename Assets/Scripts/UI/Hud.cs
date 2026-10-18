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
    public static Hud I { get; private set; }
    [Header("Bars (Fill의 anchorMax.x로 비율 표시)")]
    [SerializeField] RectTransform hpFill;
    [Tooltip("서식: {0}=현재 HP, {1}=최대 HP")]
    [SerializeField] TMP_Text hpText;
    [SerializeField] RectTransform xpFill;
    [Header("Ward (작은 생명체)")]
    [SerializeField] RectTransform wardFill;
    [Tooltip("서식: {0}=생명체 현재 체력, {1}=최대 체력")]
    [SerializeField] TMP_Text wardText;
    [Header("Bombard (R)")]
    [Tooltip("서식: {0}=폭격 쿨타임 (준비되면 Ready Text)")]
    [SerializeField] TMP_Text bombardText;
    [Tooltip("폭격 조준 중에만 표시")]
    [SerializeField] GameObject bombardAimHint;
    [Header("Camera")]
    [Tooltip("서식: {0}=현재 시점 이름")]
    [SerializeField] TMP_Text cameraModeText;
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
    [Header("Resource (자원 획득 알림)")]
    [Tooltip("서식: {0}=자원 이름. 같은 자원을 연속 획득하면 {1}=개수")]
    [SerializeField] TMP_Text resourceToast;
    [SerializeField] CanvasGroup resourceToastGroup;
    [SerializeField] float toastTime = 1.8f, toastFade = 0.5f;
    [Header("Level Up")]
    [SerializeField] GameObject levelUpPanel;
    [Tooltip("탄환 / 동반자 증강 제목")]
    [SerializeField] GameObject normalTitle, specialTitle;
    [Tooltip("생명체 생존력 증강 제목")]
    [SerializeField] GameObject wardTitle;
    [SerializeField] Button[] choiceButtons;
    [Tooltip("서식: {0}=번호, {1}=강화 이름, {2}=설명")]
    [SerializeField] TMP_Text[] choiceTexts;
    [Header("Result")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject victoryTitle, defeatTitle;
    [Tooltip("생명체를 잃어서 패배했을 때 표시")]
    [SerializeField] GameObject wardLostTitle;

    string hpFmt, infoFmt, skillFmt, wardFmt, camFmt, bombardFmt, toastFmt, toastName;
    float toastLeft;
    int toastCount;
    int lastBombard = -1;
    int lastWard = -1, lastWardMax = -1;
    CameraFollow.Mode lastCam = (CameraFollow.Mode)(-1);
    string[] choiceFmt;
    Interactable shownFocus;
    int lastHp = -1, lastMaxHp = -1, lastLevel = -1, lastKills = -1, lastQ = -1, lastE = -1, shownJump = -1;
    float lastXp = -1f;
    GameState lastState = (GameState)(-1);

    void Awake()
    {
        I = this;
        if (resourceToast) { toastFmt = resourceToast.text; resourceToast.gameObject.SetActive(false); }
        hpFmt = hpText.text; infoFmt = infoText.text; skillFmt = skillText.text;
        if (wardText) wardFmt = wardText.text;
        if (cameraModeText) camFmt = cameraModeText.text;
        if (bombardText) bombardFmt = bombardText.text;
        choiceFmt = new string[choiceTexts.Length];
        for (int i = 0; i < choiceTexts.Length; i++) choiceFmt[i] = choiceTexts[i].text;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int idx = i;
            choiceButtons[i].onClick.AddListener(() => GameManager.I.Choose(idx));
        }
    }

    /// <summary>"자원 이름 획득" 알림. 같은 자원이 연속이면 개수를 올림.</summary>
    public void ShowResource(string name)
    {
        if (!resourceToast) return;
        toastCount = toastLeft > 0f && name == toastName ? toastCount + 1 : 1;
        toastName = name;
        toastLeft = toastTime;
        resourceToast.text = UiText.Format(toastFmt, name, toastCount);
        resourceToast.gameObject.SetActive(true);
        if (resourceToastGroup) resourceToastGroup.alpha = 1f;
    }

    void UpdateToast()
    {
        if (toastLeft <= 0f) return;
        toastLeft -= Time.unscaledDeltaTime;
        if (resourceToastGroup) resourceToastGroup.alpha = Mathf.Clamp01(toastLeft / toastFade);
        if (toastLeft <= 0f) resourceToast.gameObject.SetActive(false);
    }

    void OnEnable() { if (GameManager.I) GameManager.I.ChoicesChanged += RefreshChoices; }
    void OnDisable() { if (GameManager.I) GameManager.I.ChoicesChanged -= RefreshChoices; }

    void LateUpdate()
    {
        UpdateToast();
        var gm = GameManager.I;
        var stage = StageDirector.I;
        var player = PlayerController.I;
        if (gm == null || stage == null || player == null) return;
        var s = gm.Stats;

        // 전투에서 플레이어는 형태 없는 존재 → 플레이어 체력바 숨김
        bool showHp = !player.Formless;
        if (hpFill.parent.gameObject.activeSelf != showHp) hpFill.parent.gameObject.SetActive(showHp);

        int hp = Mathf.CeilToInt(s.hp), maxHp = Mathf.RoundToInt(s.maxHp);
        if (hp != lastHp || maxHp != lastMaxHp)
        {
            lastHp = hp; lastMaxHp = maxHp;
            SetFill(hpFill, s.hp / s.maxHp);
            hpText.SetText(hpFmt, hp, maxHp); // 숫자 전용 SetText: 문자열 할당 없음
        }

        int wh = Mathf.CeilToInt(s.wardHp), wm = Mathf.RoundToInt(s.wardMaxHp);
        if (wardFill && (wh != lastWard || wm != lastWardMax))
        {
            lastWard = wh; lastWardMax = wm;
            SetFill(wardFill, s.wardHp / s.wardMaxHp);
            if (wardText) wardText.SetText(wardFmt, wh, wm);
        }

        var bs = BombardSkill.I;
        if (bs)
        {
            int b = Mathf.CeilToInt(bs.CooldownLeft);
            if (bombardText && b != lastBombard) { lastBombard = b; bombardText.text = UiText.Format(bombardFmt, b > 0 ? b.ToString() : readyText); }
            if (bombardAimHint && bombardAimHint.activeSelf != bs.Aiming) bombardAimHint.SetActive(bs.Aiming);
        }

        var cf = CameraFollow.I;
        if (cameraModeText && cf && cf.Current != lastCam)
        {
            lastCam = cf.Current;
            cameraModeText.text = UiText.Format(camFmt, cf.CurrentName);
        }

        float xp = (float)s.xp / gm.XpToNext;
        if (!Mathf.Approximately(xp, lastXp)) { lastXp = xp; SetFill(xpFill, xp); }

        if (s.level != lastLevel || stage.Kills != lastKills)
        {
            lastLevel = s.level; lastKills = stage.Kills;
            infoText.text = UiText.Format(infoFmt, s.level, gm.StageIndex + 1, gm.Config.stageCount,
                stage.Theme ? stage.Theme.displayName : "", stage.Kills, stage.KillTarget);
            if (bonusBadge) bonusBadge.SetActive(gm.InBonus);
        }

        int q = Mathf.CeilToInt(player.QLeft * 10f), e = Mathf.CeilToInt(player.ELeft * 10f);
        if (q != lastQ || e != lastE)
        {
            lastQ = q; lastE = e;
            skillText.text = UiText.Format(skillFmt, Cd(q), Cd(e));
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
            if (end)
            {
                bool lost = gm.State == GameState.GameOver, wardLost = lost && gm.WardLost && wardLostTitle;
                victoryTitle.SetActive(gm.State == GameState.Victory);
                defeatTitle.SetActive(lost && !wardLost);
                if (wardLostTitle) wardLostTitle.SetActive(wardLost);
            }
        }
    }

    /// <summary>GameManager가 선택지를 만들 때마다 호출 (연속 레벨업 포함).</summary>
    void RefreshChoices()
    {
        var gm = GameManager.I;
        var cat = gm.ChoiceCategory;
        bool ward = cat == UpgradeCategory.Ward && wardTitle;
        normalTitle.SetActive(cat == UpgradeCategory.Projectile || (cat == UpgradeCategory.Ward && !ward));
        specialTitle.SetActive(cat == UpgradeCategory.Companion);
        if (wardTitle) wardTitle.SetActive(ward);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool has = i < gm.Choices.Count;
            choiceButtons[i].gameObject.SetActive(has);
            if (has) choiceTexts[i].text = UiText.Format(choiceFmt[i], i + 1, gm.Choices[i].name, gm.Choices[i].desc);
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

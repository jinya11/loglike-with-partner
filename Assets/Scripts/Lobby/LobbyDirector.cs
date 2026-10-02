using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

/// <summary>로비 씬 싱글톤: 상호작용 대상 탐색(F), 로비 HUD, 난이도의 석판 전체 화면 UI (uGUI, HUD_Lobby 프리팹).</summary>
public class LobbyDirector : Singleton<LobbyDirector>
{
    [Tooltip("켜면 반경 밖으로 못 나감. 끄면 벽/콜라이더로만 막힘 (플랫폼을 여러 개 둘 때)")]
    [SerializeField] bool clampToArena;
    [SerializeField] float radius = 18f;
    [Header("HUD")]
    [SerializeField] TMP_Text infoText;
    [SerializeField] TMP_Text promptText;
    [Header("Difficulty UI")]
    [SerializeField] GameObject difficultyPanel;
    [Tooltip("석판 카드 (GameConfig.modifiers 순서와 일치). 프리팹에서 개별 편집")]
    [SerializeField] ModifierCard[] cards;
    [SerializeField] TMP_Text activeCountText;
    [SerializeField] Button closeButton;

    string infoFmt, countFmt;
    Interactable shownFocus;
    int shownMask = -1, shownWeapon = -1, shownCompanion = -1, openedFrame = -1;

    protected override void Awake()
    {
        base.Awake();
        Arena.Radius = clampToArena ? radius : 100000f;
    }

    void Start()
    {
        var mods = GameManager.I.Config.modifiers;
        foreach (var c in cards) c.gameObject.SetActive(c.Index < mods.Length);
        infoFmt = infoText.text; countFmt = activeCountText.text;
        closeButton.onClick.AddListener(CloseDifficulty);
        difficultyPanel.SetActive(false);
        promptText.text = "";
    }

    public void OpenDifficulty()
    {
        difficultyPanel.SetActive(true);
        GameManager.I.InputLocked = true;
        openedFrame = Time.frameCount;
        foreach (var c in cards) c.Refresh();
    }

    void CloseDifficulty()
    {
        difficultyPanel.SetActive(false);
        GameManager.I.InputLocked = false;
        LobbyInteractable.RefreshAll();
    }

    void Update()
    {
        var gm = GameManager.I;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (difficultyPanel.activeSelf)
        {
            // 연 프레임의 F 입력으로 바로 닫히지 않도록
            if (Time.frameCount != openedFrame && (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) { CloseDifficulty(); return; }
            for (int i = 0; i < 9 && i < cards.Length && i < GameManager.I.Config.modifiers.Length; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) { gm.ToggleModifier(cards[i].Index); cards[i].Refresh(); }
        }
        RefreshHud(gm);
    }

    /// <summary>바뀐 경우에만 텍스트 갱신.</summary>
    void RefreshHud(GameManager gm)
    {
        var focus = gm.InputLocked ? null : PlayerController.I.Focus;
        if (focus != shownFocus || (focus != null && focus is LobbyInteractable li && li.kind != LobbyInteractable.Kind.Modifier && gm.ModifierMask != shownMask))
        {
            shownFocus = focus;
            promptText.text = focus != null ? focus.Prompt : "";
        }
        if (gm.ModifierMask == shownMask && gm.WeaponIndex == shownWeapon && gm.CompanionIndex == shownCompanion) return;
        shownMask = gm.ModifierMask; shownWeapon = gm.WeaponIndex; shownCompanion = gm.CompanionIndex;

        var mods = gm.Config.modifiers;
        int active = 0;
        for (int i = 0; i < mods.Length; i++) if (gm.IsModifierOn(i)) active++;
        // 프리팹 문구를 서식으로 사용: {0}=무기, {1}=동반자, {2}=각성 수, {3}=전체 석판 수
        infoText.text = string.Format(infoFmt, gm.Weapon?.name, gm.Companion?.name, active, mods.Length);
        activeCountText.SetText(countFmt, active, mods.Length);
    }
}

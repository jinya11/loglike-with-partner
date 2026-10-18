using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 로비의 거점 핵. F로 열면 거점 성장 화면 (HUD_BaseUpgrade): 모은 자원으로 영구 강화를 올림.
/// 강화 항목(BaseUpgradeRow)과 보유 자원(ResourceLabel)은 프리팹에서 직접 편집.
/// </summary>
public class BaseUpgradeStation : Interactable
{
    [SerializeField] string prompt = "[F] 거점 성장";
    [SerializeField] GameObject panel;
    [SerializeField] Button closeButton;
    [Tooltip("성장 화면 동안 숨길 다른 UI")] [SerializeField] Canvas[] hideCanvases;

    BaseUpgradeRow[] rows;
    ResourceLabel[] labels;
    int openedFrame = -1;
    bool open;

    public override string Prompt => prompt;

    void Awake()
    {
        rows = panel.GetComponentsInChildren<BaseUpgradeRow>(true);
        labels = panel.GetComponentsInChildren<ResourceLabel>(true);
        foreach (var r in rows) r.Bought += RefreshAll;
        if (closeButton) closeButton.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    public override void Interact()
    {
        open = true;
        openedFrame = Time.frameCount;
        GameManager.I.InputLocked = true;
        foreach (var c in hideCanvases) if (c) c.enabled = false;
        panel.SetActive(true);
        RefreshAll();
    }

    void Update()
    {
        if (!open || Time.frameCount == openedFrame) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) Close();
    }

    void RefreshAll()
    {
        foreach (var r in rows) r.Refresh();
        foreach (var l in labels) l.Refresh();
    }

    void Close()
    {
        open = false;
        panel.SetActive(false);
        foreach (var c in hideCanvases) if (c) c.enabled = true;
        GameManager.I.InputLocked = false;
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>예/아니오 확인 창 (uGUI 프리팹 HUD_Confirm). 문구와 '예' 동작만 바꿔서 재사용.</summary>
public class ConfirmPopup : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text message;
    [SerializeField] Button yesButton, noButton;

    Action onYes, onClose;
    public bool IsOpen => panel.activeSelf;

    void Awake()
    {
        yesButton.onClick.AddListener(Yes);
        noButton.onClick.AddListener(Hide);
        panel.SetActive(false);
    }

    /// <param name="closed">'예'/'아니오' 어느 쪽으로든 닫힐 때 ('예' 동작보다 먼저).</param>
    public void Show(string text, Action yes, Action closed = null)
    {
        message.text = text;
        onYes = yes; onClose = closed;
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (!panel.activeSelf) return;
        panel.SetActive(false);
        var c = onClose; onClose = null; onYes = null;
        c?.Invoke();
    }

    void Yes()
    {
        var y = onYes;
        Hide();
        y?.Invoke();
    }
}

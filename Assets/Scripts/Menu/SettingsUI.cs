using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창 (uGUI 프리팹 HUD_Settings, 씬 전환 없이 현재 화면 위에 반투명으로 겹침). EscapeToBase가 소유.
/// 루트 Canvas는 항상 켜져 있음: Panel만 열고 닫고, PipFrame(작은 화면 테두리)은 창과 무관하게 표시.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Slider master, sfx, ambient;
    [SerializeField] Toggle split;
    [SerializeField] GameObject splitOn; // 스위치 켜짐 모양
    [SerializeField] Button modeHalf, modePip, targetWatcher, targetCreature;
    [SerializeField] CanvasGroup modeRow, targetRow;
    [SerializeField] Button returnButton, closeButton;
    [SerializeField] TMP_Text returnLabel;
    [SerializeField] Color selectedColor = new Color(1f, 0.64f, 0.69f), idleColor = new Color(1f, 0.93f, 0.94f);
    [SerializeField] Color selectedText = Color.white, idleText = new Color(0.42f, 0.29f, 0.32f);
    [Header("작은 화면 테두리")]
    [SerializeField] RectTransform pipFrame;

    public bool IsOpen => panel.activeSelf;
    public event Action ReturnClicked, CloseClicked;

    bool refreshing, frameOn;
    float closeX;
    Rect frameRect;

    void Awake()
    {
        master.onValueChanged.AddListener(v => { if (!refreshing) GameSettings.Master = v; });
        sfx.onValueChanged.AddListener(v => { if (!refreshing) GameSettings.Sfx = v; });
        ambient.onValueChanged.AddListener(v => { if (!refreshing) GameSettings.Ambient = v; });
        split.onValueChanged.AddListener(v => { if (!refreshing) { GameSettings.SplitEnabled = v; Refresh(); } });
        modeHalf.onClick.AddListener(() => { GameSettings.Mode = SplitMode.Half; Refresh(); });
        modePip.onClick.AddListener(() => { GameSettings.Mode = SplitMode.Pip; Refresh(); });
        targetWatcher.onClick.AddListener(() => { GameSettings.Pip = PipTarget.Watcher; Refresh(); });
        targetCreature.onClick.AddListener(() => { GameSettings.Pip = PipTarget.Creature; Refresh(); });
        returnButton.onClick.AddListener(() => ReturnClicked?.Invoke());
        closeButton.onClick.AddListener(() => CloseClicked?.Invoke());
        closeX = ((RectTransform)closeButton.transform).anchoredPosition.x;
        panel.SetActive(false);
        pipFrame.gameObject.SetActive(false);
        var c = ScreenSplitConfig.I;
        foreach (var g in pipFrame.GetComponentsInChildren<Graphic>(true)) g.color = c.borderColor;
    }

    /// <param name="returnText">귀환 버튼 문구 (null이면 버튼 숨김 — 타이틀).</param>
    public void Show(string returnText)
    {
        returnButton.gameObject.SetActive(returnText != null);
        var cr = (RectTransform)closeButton.transform; // 귀환 버튼이 없으면 닫기를 가운데로
        cr.anchoredPosition = new Vector2(returnText != null ? closeX : 0f, cr.anchoredPosition.y);
        if (returnText != null) returnLabel.text = returnText;
        Refresh();
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (!panel.activeSelf) return;
        panel.SetActive(false);
        GameSettings.Flush();
    }

    void Refresh()
    {
        refreshing = true;
        master.SetValueWithoutNotify(GameSettings.Master);
        sfx.SetValueWithoutNotify(GameSettings.Sfx);
        ambient.SetValueWithoutNotify(GameSettings.Ambient);
        bool on = GameSettings.SplitEnabled, pip = GameSettings.Mode == SplitMode.Pip;
        split.SetIsOnWithoutNotify(on);
        splitOn.SetActive(on);
        Paint(modeHalf, !pip); Paint(modePip, pip);
        Paint(targetWatcher, GameSettings.Pip == PipTarget.Watcher); Paint(targetCreature, GameSettings.Pip == PipTarget.Creature);
        SetRow(modeRow, on); SetRow(targetRow, on && pip);
        refreshing = false;
    }

    void Paint(Button b, bool sel)
    {
        b.image.color = sel ? selectedColor : idleColor;
        var t = b.GetComponentInChildren<TMP_Text>();
        if (t) t.color = sel ? selectedText : idleText;
    }

    static void SetRow(CanvasGroup g, bool on) { g.alpha = on ? 1f : 0.4f; g.interactable = on; }

    // 작은 화면 테두리: CameraFollow가 지금 쓰는 viewport를 따라감 (바뀔 때만 갱신)
    void LateUpdate()
    {
        var cf = CameraFollow.I;
        bool on = cf && cf.PipActive;
        if (on != frameOn) { frameOn = on; pipFrame.gameObject.SetActive(on); }
        if (!on || cf.PipViewport == frameRect) return;
        frameRect = cf.PipViewport;
        pipFrame.anchorMin = frameRect.min; pipFrame.anchorMax = frameRect.max;
        pipFrame.offsetMin = pipFrame.offsetMax = Vector2.zero;
    }
}

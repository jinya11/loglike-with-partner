using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 생명체 명령 패널 (오른쪽 아래, 크게): 명령(고리/자율/정지/따라오기)과 자율일 때 조작 대상(둘 다/감시자만/생명체만)을
/// 버튼 클릭이나 키 한 번으로 바로 고름. 현재 상태는 버튼 색으로 표시. 듀얼 카메라일 때 가운데 구분선과 이름표.
/// 증강 선택/결과 화면에서는 패널을 숨김.
/// 프리팹 HUD_Command (StageConfig.commandHudPrefab) — StageDirector가 전투 스테이지마다 생성. 위치/색/크기는 프리팹에서 편집.
/// 버튼 글자는 프리팹 문구에서 {0} 자리에 키 이름이 들어감.
/// </summary>
public class CommandHud : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text title;
    [Tooltip("고리 / 자율 / 정지 / 따라오기 순서")]
    [SerializeField] Button[] orderButtons;
    [Tooltip("둘 다 / 감시자만 / 생명체만 순서")]
    [SerializeField] Button[] controlButtons;
    [Tooltip("자율이 아닐 때 흐리게")]
    [SerializeField] CanvasGroup controlGroup;
    [Header("듀얼 카메라")]
    [SerializeField] GameObject divider;
    [SerializeField] GameObject leftLabel, rightLabel;
    [Header("색")]
    [SerializeField] Color buttonOff = new Color(0.12f, 0.12f, 0.15f, 0.7f);
    [SerializeField] Color orderOn = new Color(0.3f, 0.65f, 0.35f, 1f), controlOn = new Color(0.85f, 0.62f, 0.2f, 1f);

    Ward.Command shownOrder = (Ward.Command)(-1);
    Ward.Control shownControl = (Ward.Control)(-1);
    bool shownSteer, shownSplit = true;

    void Start()
    {
        UiText.EnsureEventSystem();
        var c = GameManager.I.Config.ward;
        var orderKeys = new[] { c.linkKey, c.autoKey, c.stayKey, c.followKey };
        for (int i = 0; i < orderButtons.Length; i++)
        {
            int k = i;
            SetKeyLabel(orderButtons[i], i < orderKeys.Length ? orderKeys[i].ToString() : "");
            orderButtons[i].onClick.AddListener(() => OnOrder(k));
            orderButtons[i].onClick.AddListener(Deselect);
        }
        var controlKeys = new[] { c.bothKey, c.watcherOnlyKey, c.creatureOnlyKey };
        for (int i = 0; i < controlButtons.Length; i++)
        {
            var ctl = (Ward.Control)i;
            SetKeyLabel(controlButtons[i], i < controlKeys.Length ? controlKeys[i].ToString() : "");
            controlButtons[i].onClick.AddListener(() => { if (Ward.I) Ward.I.SetControl(ctl); });
            controlButtons[i].onClick.AddListener(Deselect);
        }
        SetSplit(false);
    }

    static void SetKeyLabel(Button b, string key)
    {
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t) t.text = UiText.Format(t.text, key);
    }

    static void Deselect() => EventSystem.current?.SetSelectedGameObject(null); // 선택 해제: Enter/Space가 버튼을 다시 누르지 않게

    void OnOrder(int i)
    {
        var w = Ward.I;
        if (!w) return;
        if (i == 0) { if (w.Order != Ward.Command.Linked) w.ToggleLink(); return; }
        w.SetOrder(i == 1 ? Ward.Command.Auto : i == 2 ? Ward.Command.Stay : Ward.Command.Follow);
    }

    // ---- 작은 화면(오른쪽 아래)이 떠 있으면 패널을 그 위로 올림 (배치가 바뀔 때만 계산) ----
    int shownLayout = -1;
    bool baseSet;
    Vector2 basePos;
    static readonly Vector3[] corners = new Vector3[4];

    void AvoidPip(CameraFollow cf, int layout)
    {
        shownLayout = layout;
        var rt = (RectTransform)panel.transform;
        if (!baseSet) { basePos = rt.anchoredPosition; baseSet = true; }
        rt.anchoredPosition = basePos;
        if (cf == null || !cf.PipActive) return;
        var canvas = rt.GetComponentInParent<Canvas>().rootCanvas;
        float scale = canvas.scaleFactor;
        rt.GetWorldCorners(corners); // Overlay 캔버스: 화면 픽셀
        float pipTop = cf.PipViewport.yMax * Screen.height + ScreenSplitConfig.I.hudGap * scale;
        if (pipTop > corners[0].y) rt.anchoredPosition = basePos + new Vector2(0f, (pipTop - corners[0].y) / scale);
    }

    void SetSplit(bool on)
    {
        shownSplit = on;
        if (divider) divider.SetActive(on);
        if (leftLabel) leftLabel.SetActive(on);
        if (rightLabel) rightLabel.SetActive(on);
    }

    void Update()
    {
        var w = Ward.I;
        bool splitNow = CameraFollow.I && CameraFollow.I.Split;
        if (splitNow != shownSplit) SetSplit(splitNow);
        int layout = CameraFollow.I ? CameraFollow.I.LayoutVersion : -1;
        if (layout != shownLayout) AvoidPip(CameraFollow.I, layout);
        bool playing = GameManager.I.State == GameState.Playing; // 증강 선택/결과 화면에서는 숨김
        if (panel.activeSelf != playing) panel.SetActive(playing);
        if (w == null || (w.Order == shownOrder && w.Controls == shownControl && w.Steering == shownSteer)) return;
        shownOrder = w.Order; shownControl = w.Controls; shownSteer = w.Steering;

        for (int i = 0; i < orderButtons.Length; i++) orderButtons[i].image.color = (int)w.Order == i ? orderOn : buttonOff;
        bool auto = w.Order == Ward.Command.Auto;
        controlGroup.alpha = auto ? 1f : 0.55f;
        for (int i = 0; i < controlButtons.Length; i++) controlButtons[i].image.color = auto && (int)w.Controls == i ? controlOn : buttonOff;

        string state = w.Order != Ward.Command.Auto ? Ward.CommandName(w.Order)
            : w.Controls == Ward.Control.Both ? "WASD 감시자 · 화살표 생명체"
            : w.Controls == Ward.Control.WatcherOnly ? "WASD 감시자 · 생명체 자동"
            : "WASD/화살표 생명체 · 감시자 자동";
        title.text = $"생명체 명령   <size=22><color=#ffffff>{state}</color>{(w.Steering ? "   <color=#ffb070>직접 조작 중</color>" : "")}</size>";
    }
}

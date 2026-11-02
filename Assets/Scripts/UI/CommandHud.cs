using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 생명체 명령 패널 (오른쪽 아래, 크게): 명령(고리/자율/정지/따라오기)과 자율일 때 조작 대상(둘 다/감시자만/생명체만)을
/// 버튼 클릭이나 키 한 번으로 바로 고름. 현재 상태는 버튼 색으로 표시. 듀얼 카메라일 때 가운데 구분선과 이름표.
/// 밝은 지역에서도 HUD 글자가 보이도록 스테이지 HUD 글자에 외곽선을 덧붙임. 증강 선택/결과 화면에서는 패널을 숨김.
/// StageDirector가 전투 스테이지마다 생성 (코드로 만든 uGUI).
/// </summary>
public class CommandHud : MonoBehaviour
{
    static readonly Color BtnOff = new Color(0.12f, 0.12f, 0.15f, 0.7f),
        OrderOn = new Color(0.3f, 0.65f, 0.35f, 1f), ControlOn = new Color(0.85f, 0.62f, 0.2f, 1f), Gold = new Color(0.95f, 0.82f, 0.5f);

    Image[] orderBtns, controlBtns;
    CanvasGroup controlGroup;
    TMP_Text title, leftLabel, rightLabel;
    GameObject panelGo;
    GameObject divider;
    Ward.Command shownOrder = (Ward.Command)(-1);
    Ward.Control shownControl = (Ward.Control)(-1);
    bool shownSteer, shownSplit;

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var cs = gameObject.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var c = GameManager.I.Config.ward;
        // ---- 명령 패널 ----
        // 배경 판/구역 이름 없이 버튼 두 줄만 (윗줄 명령, 아랫줄 조작 대상)
        var panel = Rect("CommandPanel", transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 24), new Vector2(660, 220), new Vector2(1, 0));
        panelGo = panel.gameObject;
        title = Text("Title", panel, "생명체 명령", 30, TextAlignmentOptions.TopLeft, Gold, new Vector2(0, 1), new Vector2(22, 0), new Vector2(640, 44));

        orderBtns = new Image[4];
        string[] orderNames = { "고리", "자율", "정지", "따라오기" };
        var orderKeys = new[] { c.linkKey, c.autoKey, c.stayKey, c.followKey };
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            orderBtns[i] = Button(panel, $"Order_{orderNames[i]}", new Vector2(22 + i * 156, -52), new Vector2(146, 72), $"{orderNames[i]}\n<size=18>[{orderKeys[i]}]</size>", () => OnOrder(k));
        }

        var row2 = Rect("ControlRow", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -134), new Vector2(660, 80), new Vector2(0, 1));
        controlGroup = row2.gameObject.AddComponent<CanvasGroup>();
        controlBtns = new Image[3];
        string[] controlNames = { "둘 다", "감시자만", "생명체만" };
        var controlKeys = new[] { c.bothKey, c.watcherOnlyKey, c.creatureOnlyKey };
        for (int i = 0; i < 3; i++)
        {
            var ctl = (Ward.Control)i;
            controlBtns[i] = Button(row2, $"Control_{controlNames[i]}", new Vector2(22 + i * 208, 0), new Vector2(198, 72), $"{controlNames[i]}\n<size=18>[{controlKeys[i]}]</size>", () => { if (Ward.I) Ward.I.SetControl(ctl); });
        }

        // ---- 듀얼 카메라 구분선 ----
        var line = Rect("Divider", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(6f, 0f), new Vector2(0.5f, 0.5f));
        var img = line.gameObject.AddComponent<Image>(); img.color = new Color(0.05f, 0.05f, 0.07f, 1f); img.raycastTarget = false;
        divider = line.gameObject;
        leftLabel = Text("LeftLabel", transform, "감시자", 28, TextAlignmentOptions.TopRight, Color.white, new Vector2(0.5f, 1f), new Vector2(-20, -24), new Vector2(400, 44), new Vector2(1, 1));
        rightLabel = Text("RightLabel", transform, "생명체", 28, TextAlignmentOptions.TopLeft, Color.white, new Vector2(0.5f, 1f), new Vector2(20, -24), new Vector2(400, 44), new Vector2(0, 1));
        SetSplit(false);

        ImproveStageHud();
    }

    void OnOrder(int i)
    {
        var w = Ward.I;
        if (!w) return;
        if (i == 0) { if (w.Order != Ward.Command.Linked) w.ToggleLink(); return; }
        w.SetOrder(i == 1 ? Ward.Command.Auto : i == 2 ? Ward.Command.Stay : Ward.Command.Follow);
    }

    // ---- uGUI 헬퍼 ----
    static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static TMP_Text Text(string name, Transform parent, string s, int size, TextAlignmentOptions align, Color col, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, Vector2? pivot = null)
    {
        var rt = Rect(name, parent, anchor, anchor, pos, sizeDelta, pivot ?? new Vector2(0, 1));
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = s; t.fontSize = size; t.alignment = align; t.color = col; t.raycastTarget = false;
        t.fontStyle = FontStyles.Bold;
        t.outlineWidth = 0.22f; t.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        return t;
    }

    static Image Button(Transform parent, string name, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
    {
        var rt = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), pos, size, new Vector2(0, 1));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = BtnOff;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); b.colors = cb;
        b.onClick.AddListener(onClick);
        b.onClick.AddListener(() => EventSystem.current?.SetSelectedGameObject(null)); // 선택 해제: Enter/Space가 버튼을 다시 누르지 않게
        var t = Text("Label", rt, label, 26, TextAlignmentOptions.Center, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
        t.lineSpacing = -10f;
        return img;
    }

    void SetSplit(bool on)
    {
        shownSplit = on;
        divider.SetActive(on); leftLabel.gameObject.SetActive(on); rightLabel.gameObject.SetActive(on);
    }

    void Update()
    {
        var w = Ward.I;
        bool splitNow = CameraFollow.I && CameraFollow.I.Split;
        if (splitNow != shownSplit) SetSplit(splitNow);
        bool playing = GameManager.I.State == GameState.Playing; // 증강 선택/결과 화면에서는 숨김
        if (panelGo.activeSelf != playing) panelGo.SetActive(playing);
        if (w == null || (w.Order == shownOrder && w.Controls == shownControl && w.Steering == shownSteer)) return;
        shownOrder = w.Order; shownControl = w.Controls; shownSteer = w.Steering;

        for (int i = 0; i < orderBtns.Length; i++) orderBtns[i].color = (int)w.Order == i ? OrderOn : BtnOff;
        bool auto = w.Order == Ward.Command.Auto;
        controlGroup.alpha = auto ? 1f : 0.55f;
        for (int i = 0; i < controlBtns.Length; i++) controlBtns[i].color = auto && (int)w.Controls == i ? ControlOn : BtnOff;

        string state = w.Order != Ward.Command.Auto ? Ward.CommandName(w.Order)
            : w.Controls == Ward.Control.Both ? "WASD 감시자 · 화살표 생명체"
            : w.Controls == Ward.Control.WatcherOnly ? "WASD 감시자 · 생명체 자동"
            : "WASD/화살표 생명체 · 감시자 자동";
        title.text = $"생명체 명령   <size=22><color=#ffffff>{state}</color>{(w.Steering ? "   <color=#ffb070>직접 조작 중</color>" : "")}</size>";
    }

    /// <summary>스테이지 HUD(다른 프리팹)의 글자에 외곽선을 덧붙임. 밝은 지역(설원/사막)에서도 읽히게.</summary>
    static void ImproveStageHud()
    {
        var hud = Hud.I;
        if (hud == null) return;
        // 꺼져 있는 글자(레벨업 창 등)는 아직 초기화 전이라 켜질 때 적용
        foreach (var t in hud.GetComponentsInChildren<TMP_Text>(true))
            if (!t.GetComponent<ReadableText>()) t.gameObject.AddComponent<ReadableText>();
    }
}

/// <summary>TMP 글자에 검은 외곽선 (밝은 배경에서도 읽히게). 켜질 때 한 번 적용.</summary>
public class ReadableText : MonoBehaviour
{
    bool applied;

    void OnEnable()
    {
        if (applied || !TryGetComponent(out TMP_Text t)) return;
        applied = true;
        t.outlineWidth = Mathf.Max(t.outlineWidth, 0.22f);
        t.outlineColor = new Color(0f, 0f, 0f, 0.9f);
    }
}

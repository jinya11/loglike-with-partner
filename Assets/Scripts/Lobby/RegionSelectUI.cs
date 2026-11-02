using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 출발 포털의 지역 지도 (3x3). 칸 번호 배치:  2 3 4 / 5 1 6 / 7 8 9  (1 = 중앙, 하늘섬 바로 밑 심연).
/// 숫자 키나 클릭으로 칸 선택 → 같은 칸 다시 클릭 / 출발 버튼 / F·Enter로 출발. Esc로 닫기.
/// 프리팹(HUD_RegionSelect, StageConfig.regionSelectPrefab)은 처음 열 때 로비에 생성.
/// </summary>
public class RegionSelectUI : Singleton<RegionSelectUI>
{
    /// <summary>지도 칸(왼쪽 위부터 행 순서) → 칸 번호.</summary>
    public static readonly int[] GridNumbers = { 2, 3, 4, 5, 1, 6, 7, 8, 9 };

    [SerializeField] GameObject panel;
    [Tooltip("GridNumbers 순서 (왼쪽 위부터 행 순서)")]
    [SerializeField] Button[] cells;
    [SerializeField] TMP_Text[] cellNames, cellDescs;
    [SerializeField] Image[] cellBackgrounds, cellBorders;
    [SerializeField] TMP_Text selectedText;
    [SerializeField] Button startButton, closeButton;
    [SerializeField] Color borderIdle = new Color(0.3f, 0.29f, 0.34f), borderSelected = new Color(0.95f, 0.8f, 0.5f);

    int selected = -1, openedFrame = -1, shownVersion = -1;

    /// <summary>지역 지도 열기 (프리팹이 없으면 바로 출발).</summary>
    public static void Open()
    {
        if (I == null)
        {
            var prefab = GameManager.I.Config.stage.regionSelectPrefab;
            if (!prefab) { GameManager.I.StartRun(); return; }
            Instantiate(prefab);
        }
        I.Show();
    }

    void Start()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            int k = i;
            cells[i].onClick.AddListener(() => Click(k));
        }
        startButton.onClick.AddListener(Depart);
        closeButton.onClick.AddListener(Close);
        if (openedFrame < 0) panel.SetActive(false);
    }

    void Show()
    {
        panel.SetActive(true);
        GameManager.I.InputLocked = true;
        openedFrame = Time.frameCount;
        if (selected < 0 || GameManager.I.Regions[GridNumbers[selected]] == null) selected = 4; // 기본: 중앙(1번)
        Refresh();
    }

    void Close()
    {
        panel.SetActive(false);
        GameManager.I.InputLocked = false;
        LobbyInteractable.RefreshAll();
    }

    void Click(int i)
    {
        if (GameManager.I.Regions[GridNumbers[i]] == null) return;
        if (selected == i) { Depart(); return; }
        selected = i;
        Refresh();
    }

    void Depart()
    {
        if (selected < 0) return;
        int slot = GridNumbers[selected];
        if (GameManager.I.Regions[slot] == null) return;
        panel.SetActive(false);
        GameManager.I.StartRun(slot);
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        if (shownVersion != GameManager.I.RegionVersion) Refresh();
        var kb = Keyboard.current;
        if (kb == null || Time.frameCount == openedFrame) return; // 연 프레임의 F 입력으로 바로 출발하지 않도록
        if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Depart(); return; }
        for (int n = 1; n <= 9; n++)
            if (kb[Key.Digit1 + n - 1].wasPressedThisFrame || kb[Key.Numpad1 + n - 1].wasPressedThisFrame)
            {
                int i = System.Array.IndexOf(GridNumbers, n);
                if (GameManager.I.Regions[n] != null) { selected = i; Refresh(); }
            }
    }

    void Refresh()
    {
        var gm = GameManager.I;
        shownVersion = gm.RegionVersion;
        for (int i = 0; i < cells.Length && i < GridNumbers.Length; i++)
        {
            var r = gm.Regions[GridNumbers[i]];
            cellNames[i].text = r != null ? $"{GridNumbers[i]}. {r.name}" : $"{GridNumbers[i]}. ???";
            cellDescs[i].text = r != null ? r.desc : "";
            cellBackgrounds[i].color = r != null ? r.mapColor : new Color(0.1f, 0.1f, 0.12f);
            cellBorders[i].color = i == selected ? borderSelected : borderIdle;
            cells[i].interactable = r != null;
        }
        var sel = selected >= 0 ? gm.Regions[GridNumbers[selected]] : null;
        selectedText.text = sel != null ? $"{GridNumbers[selected]}번 지역: {sel.name}" : "지역을 고르세요";
    }
}

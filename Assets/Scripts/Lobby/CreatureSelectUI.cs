using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 로비 생명체와 상호작용하면 뜨는 창: "이 생명체를 전투에 데려갈까요?" → 상호작용 키(F)/Enter/버튼으로 데려가기, Esc로 닫기.
/// 이미 함께 출발하는 생명체면 그렇다고 알려 줌.
/// 프리팹 HUD_CreatureSelect (StageConfig.creatureSelectPrefab) — 처음 열 때 생성. 문구 서식은 이 컴포넌트에서 편집.
/// </summary>
public class CreatureSelectUI : MonoBehaviour
{
    static CreatureSelectUI inst;

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text title, desc, current, confirmLabel;
    [SerializeField] Button confirmButton, closeButton;
    [Header("문구 (모든 생명체 공통, {0} = 생명체 이름 — 제목은 이름 없이 하나로 통일)")]
    [SerializeField] string titleAsk = "이 생명체를 전투에 데려갈까요?";
    [SerializeField] string titleAlready = "이미 함께 출발하는 생명체예요";
    [SerializeField] string currentFormat = "지금 함께 출발하는 생명체: {0}";
    [SerializeField] string confirmAsk = "데려가기 (F)", confirmAlready = "확인 (F)";
    [Header("잠긴 생명체 (확인 버튼 숨김, F/Enter/Esc = 닫기)")]
    [SerializeField] string titleLocked = "아직 만나지 못한 생명체예요";

    int index, openedFrame;
    bool locked;

    public static bool IsOpen => inst && inst.panel.activeSelf;

    public static void Open(int index)
    {
        if (!inst)
        {
            var prefab = GameManager.I.Config.stage.creatureSelectPrefab;
            if (!prefab) { Debug.LogWarning("StageConfig.creatureSelectPrefab 이 비어 있음"); return; }
            Instantiate(prefab);
        }
        inst.Show(index);
    }

    void Awake()
    {
        inst = this;
        UiText.EnsureEventSystem();
        confirmButton.onClick.AddListener(Confirm);
        closeButton.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    void OnDestroy() { if (inst == this) inst = null; }

    void Show(int i)
    {
        var gm = GameManager.I;
        var defs = gm.Config.ward.creatures;
        if (defs == null || i < 0 || i >= defs.Length) return;
        index = i;
        var d = defs[i];
        bool selected = gm.CreatureIndexEffective == i;
        locked = !SaveData.IsCreatureUnlocked(d);
        title.text = UiText.Format(locked ? titleLocked : selected ? titleAlready : titleAsk, d.name);
        desc.text = d.desc;
        confirmButton.gameObject.SetActive(!locked);
        var cur = defs[Mathf.Clamp(gm.CreatureIndexEffective, 0, defs.Length - 1)];
        current.text = UiText.Format(currentFormat, cur.name);
        confirmLabel.text = selected ? confirmAlready : confirmAsk;
        panel.SetActive(true);
        gm.InputLocked = true;
        openedFrame = Time.frameCount;
    }

    void Confirm()
    {
        var gm = GameManager.I;
        if (!locked && gm.CreatureIndexEffective != index)
        {
            gm.CreatureIndex = index;
            if (PlayerController.I) Vfx.Pillar(PlayerController.I.Position, new Color(0.6f, 1f, 0.6f));
        }
        Close();
    }

    void Close()
    {
        panel.SetActive(false);
        GameManager.I.InputLocked = false;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void Update()
    {
        if (!panel.activeSelf || Time.frameCount == openedFrame) return; // 연 프레임의 F로 바로 확정되지 않게
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Confirm();
    }
}

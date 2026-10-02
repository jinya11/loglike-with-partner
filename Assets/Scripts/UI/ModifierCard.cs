using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 난이도 UI의 석판 카드 하나. 이름/설명 텍스트는 프리팹에 적힌 그대로 사용 (코드가 덮어쓰지 않음).
/// 코드는 각성 여부에 따라 색과 상태 오브젝트(각성됨/봉인됨)만 바꿈.
/// </summary>
public class ModifierCard : MonoBehaviour
{
    [Tooltip("GameConfig.modifiers의 몇 번째 석판인지")]
    [SerializeField] int index;
    [SerializeField] Image border, background;
    [SerializeField] GameObject stateOn, stateOff;
    [SerializeField] Button button;
    [SerializeField] Color borderOff = new Color(0.35f, 0.33f, 0.38f), borderOn = new Color(1f, 0.45f, 0.3f);
    [SerializeField] Color bgOff = new Color(0.16f, 0.15f, 0.18f), bgOn = new Color(0.45f, 0.12f, 0.1f);

    public int Index => index;

    void Awake() => button.onClick.AddListener(() => { GameManager.I.ToggleModifier(index); Refresh(); });

    public void Refresh()
    {
        bool on = GameManager.I.IsModifierOn(index);
        border.color = on ? borderOn : borderOff;
        background.color = on ? bgOn : bgOff;
        stateOn.SetActive(on);
        stateOff.SetActive(!on);
    }
}

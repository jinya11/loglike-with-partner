using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 감시자 스킬 창의 스킬 카드 / 장착 칸 하나 (프리팹 HUD_SkillSelect 안 CardTemplate, Slot1·2).
/// SkillSelectUI가 카드는 스킬 수만큼 복제해 채움. 클릭 = 선택, 더블클릭 = 장착/해제.
/// </summary>
public class SkillSelectRow : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("테두리/바탕 (상태 색: 잠김·보통·장착)")] public Image background;
    public Button button;
    [Tooltip("스킬 이름")] public TMP_Text title;
    [Tooltip("보조 줄 (카드: 코스트, 칸: 비어 있을 때 문구)")] public TMP_Text line;
    [Tooltip("장착된 칸의 키 (비면 미장착)")] public TMP_Text badge;
    [Tooltip("아이콘 자리 (그림은 아직 없음 — 비워 둠)")] public Image icon;
    [Tooltip("추천 표시 (선택한 생명체의 성장 설정이 추천하는 분류일 때 켜짐)")] public GameObject recommend;
    [Tooltip("잠김 표시")] public GameObject lockMark;
    [Tooltip("선택 테두리")] public GameObject selectedMark;

    [HideInInspector] public WatcherSkillDef def;
    public System.Action onDoubleClick;

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && e.clickCount >= 2) onDoubleClick?.Invoke();
    }
}

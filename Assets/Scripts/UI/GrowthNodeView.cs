using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>성장 노드 점 하나 (프리팹 HUD_GrowthTree 안 NodeTemplate). GrowthTreeUI가 노드 수만큼 복제.</summary>
public class GrowthNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Button button;
    [Tooltip("점 (상태/종류 색)")] public Image dot;
    [Tooltip("겉테두리 (선택한 메인 노드)")] public GameObject ring;
    [Tooltip("연한 발광 (선택한 메인 노드)")] public Image glow;
    [Tooltip("종류별 점 크기: Main / Stat / Passive / Watcher")] public float mainSize = 66f, statSize = 34f, passiveSize = 50f, watcherSize = 56f;

    [HideInInspector] public GrowthNodeDef def;
    [HideInInspector] public GrowthTreeUI owner;

    public RectTransform Rt => (RectTransform)transform;

    public float SizeFor(GrowthNodeKind k) => k switch
    {
        GrowthNodeKind.Main => mainSize, GrowthNodeKind.Passive => passiveSize, GrowthNodeKind.Watcher => watcherSize, _ => statSize,
    };

    public void OnPointerEnter(PointerEventData e) { if (owner) owner.ShowTooltip(this); }
    public void OnPointerExit(PointerEventData e) { if (owner) owner.HideTooltip(this); }
}

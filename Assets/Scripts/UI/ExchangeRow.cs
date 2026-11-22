using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>교환 창의 카드 한 줄 (프리팹 HUD_Exchange 안 RowTemplate). ExchangeUI가 교환 비율 수만큼 복제해 채움.</summary>
public class ExchangeRow : MonoBehaviour
{
    [Tooltip("서식: {0}=재화 이름 {1}=필요 개수")] public TMP_Text label;
    [Tooltip("서식: {0}=보유 개수")] public TMP_Text have;
    public Button button;

    [HideInInspector] public ExchangeRate rate;
    [HideInInspector] public string haveFmt;
}

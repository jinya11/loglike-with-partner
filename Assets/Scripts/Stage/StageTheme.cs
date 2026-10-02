using UnityEngine;

/// <summary>스테이지 씬의 컨셉 정보 (이름, 상시 회복 등).</summary>
public class StageTheme : MonoBehaviour
{
    public string displayName = "폐허";
    [Tooltip("초당 상시 회복 (보너스 스테이지용, 고갈 석판 무시)")] public float ambientRegen;
    [Tooltip("처치 목표 배율")] public float killTargetMul = 1f;
}

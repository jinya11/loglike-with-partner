using UnityEngine;

/// <summary>작은 생명체 종류 하나 (모델 + 고유 능력 + 고유 공격). WardConfig.creatures에 등록.</summary>
[System.Serializable]
public class CreatureDef
{
    public string name;
    [TextArea(1, 3)] public string desc;
    [Tooltip("리깅된 모델 (Tools/Creatures/Auto Rig로 생성). 비우면 기본 도형 생명체")]
    public CreatureVisual model;
    [Tooltip("고유 능력 (CreatureAbility 에셋). 비우면 능력 없음")]
    public CreatureAbility ability;
    [Tooltip("기본 공격 (CreatureAttack 에셋, 생명체마다 고유). 비우면 기본 탄환")]
    public CreatureAttack attack;
    [Tooltip("평상시 색 (텍스처 모델은 흰색 = 원본 색). 디버프 색은 이 위에 곱해짐")]
    public Color normalTint = Color.white;
    [Tooltip("피격/디버프 색을 얼마나 진하게 입힐지 (텍스처 모델은 0.4~0.6 권장)")] [Range(0, 1)] public float tintStrength = 0.5f;
}

using UnityEngine;

/// <summary>마법사 성장 config: 공통(CreatureGrowthConfig) + 마법사 수치만. 메뉴 Roguelike/Config/Growth/Wizard.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Growth/Wizard")]
public class WizardGrowthConfig : CreatureGrowthConfig
{
    [Header("마법사 수치")]
    public WizardGrowthSettings wizard = new WizardGrowthSettings();
}

/// <summary>마법사 전용 수치. 메인 효과는 기본 평타(마법 탄)에 덧붙음.</summary>
[System.Serializable]
public class WizardGrowthSettings
{
    [Header("연쇄")]
    [Tooltip("번개가 옮겨 가는 최대 적 수")] public int chainCount = 4;
    [Tooltip("다음 적을 찾는 반경 / 피해 배율 (평타 피해 대비)")] public float chainRadius = 5f, chainDamageMul = 0.6f;
    public Color chainColor = new Color(0.75f, 0.85f, 1f);
    [Header("연소")]
    [Tooltip("지속 시간(초) / 총 피해")] public float burnDuration = 1.2f;
    [Tooltip("총 피해")] public float burnTotalDamage = 5f;
    [Tooltip("피해 간격(초)")] public float burnTick = 0.3f;
    public Color burnColor = new Color(1f, 0.6f, 0.4f);
    [Header("빙결")]
    [Range(0, 1)] public float freezeChance = 0.2f;
    public float freezeDuration = 1.5f;
    public Color freezeColor = new Color(0.65f, 0.9f, 1f);
    [Header("저주")]
    [Range(0, 1)] public float curseChance = 0.25f;
    [Tooltip("받는 피해 배율 (처치 전까지)")] public float curseDamageTaken = 1.1f;
    public Color curseColor = new Color(0.8f, 0.6f, 1f);
    [Header("감시자: 거대 마법 투사체 (유도 스킬 발동 시)")]
    [Tooltip("발사 수 / 발동 후 지연(초, 디코이가 적을 끌어모을 시간)")] public int orbCount = 3;
    public float orbDelay = 1.2f;
    [Tooltip("대상 탐색 반경 (스킬을 쓴 자리 기준)")] public float orbSearchRadius = 9f;
    [Tooltip("피해 배율 (평타 피해 대비) / 폭발 반경 / 크기 / 속도")] public float orbDamageMul = 3f, orbRadius = 2.5f, orbSize = 1.4f, orbSpeed = 14f;
    public Color orbColor = new Color(0.85f, 0.7f, 1f);
}

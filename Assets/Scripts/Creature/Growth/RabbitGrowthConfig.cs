using UnityEngine;

/// <summary>토끼 성장 config: 공통(CreatureGrowthConfig) + 토끼 수치만. 메뉴 Roguelike/Config/Growth/Rabbit.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Growth/Rabbit")]
public class RabbitGrowthConfig : CreatureGrowthConfig
{
    [Header("토끼 수치")]
    public RabbitGrowthSettings rabbit = new RabbitGrowthSettings();
}

/// <summary>토끼 전용 수치 (확률/피해/범위). 전부 여기서 조절.</summary>
[System.Serializable]
public class RabbitGrowthSettings
{
    [Header("공통: 직선 당근")]
    [Tooltip("직선 당근 프리팹 (CarrotShot)")] public CarrotShot shotPrefab;
    [Tooltip("직선 당근 비행 속도 = 탄속 × 이 값")] public float shotSpeedMul = 0.9f;
    [Tooltip("적과 닿는 반경")] public float shotHitRadius = 0.55f;

    [Header("메인1: 관통 당근")]
    [Tooltip("최대 관통 수 (맞히는 적 수)")] public int pierceCount = 3;
    [Tooltip("피해 배율 (기본 피해 대비)")] public float pierceDamageMul = 1.1f;
    [Tooltip("관통할 때마다 피해 × 이 값")] [Range(0, 1)] public float pierceFalloff = 0.7f;
    [Tooltip("적중 시 꼬침 확률")] [Range(0, 1)] public float pierceSkewerChance = 0.15f;

    [Header("메인2: 소형 당근")]
    [Tooltip("부채꼴 발사 수")] public int smallCount = 3;
    [Tooltip("부채꼴 각도 간격 (도)")] public float smallSpread = 14f;
    [Tooltip("피해 배율: 사거리 끝 / 바로 앞")] public float smallFarMul = 0.45f, smallNearMul = 1.1f;
    [Tooltip("적중 시 튕김 확률")] [Range(0, 1)] public float smallBounceChance = 0.15f;
    [Tooltip("'팅기는 당근' 패시브가 있으면 튕김 확률 +")] [Range(0, 1)] public float smallBounceBonus = 0.15f;

    [Header("패시브: 팅기는 당근")]
    [Tooltip("적중 시 튕김 확률")] [Range(0, 1)] public float bounceChance = 0.1f;
    [Tooltip("튕긴 당근 피해 배율")] public float bounceDamageMul = 0.6f;
    [Tooltip("튕길 대상 탐색 반경")] public float bounceRadius = 6f;

    [Header("패시브: 당근 꼬치")]
    [Tooltip("적중 시 꼬침 확률")] [Range(0, 1)] public float skewerChance = 0.08f;
    [Tooltip("꼬친 당근 유지 시간 (초)")] public float skewerDuration = 6f;
    [Tooltip("폭발 반경 / 피해 배율 (기본 피해 대비)")] public float skewerRadius = 2.2f, skewerDamageMul = 2f;
    public Color skewerColor = new Color(1f, 0.62f, 0.4f);

    [Header("패시브: 피흡")]
    [Tooltip("준 피해의 이 비율만큼 생명체 회복")] [Range(0, 1)] public float lifestealFrac = 0.04f;

    [Header("감시자: 당근 폭격")]
    [Tooltip("폭격 스킬 1회당 떨어지는 당근 수")] public int rainCount = 6;
    [Tooltip("떨어지기 시작하는 높이 / 낙하 시간 / 당근 사이 간격(초)")] public float rainHeight = 14f, rainFallTime = 0.55f, rainInterval = 0.12f;
    [Tooltip("피해 배율 (기본 피해 대비) / 착지 반경")] public float rainDamageMul = 1.5f, rainRadius = 1.8f;
    [Tooltip("맵 크기 대비 낙하 범위")] [Range(0.1f, 1f)] public float rainArea = 0.8f;
    public Color rainColor = new Color(1f, 0.7f, 0.45f);
}

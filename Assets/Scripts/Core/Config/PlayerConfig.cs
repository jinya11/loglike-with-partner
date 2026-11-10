using UnityEngine;

/// <summary>플레이어 기본 능력치 / 레벨업 / 강화 설정.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Player")]
public class PlayerConfig : ScriptableObject
{
    [Tooltip("플레이어 기본 능력치 (생명체/감시자 수치는 각 설정에서)")]
    public PlayerStats baseStats = new PlayerStats();

    [Header("생명체와의 거리 제한 (전투)")]
    [Tooltip("조준점(플레이어)이 생명체에서 이 거리 이상 멀어질 수 없음. 0이면 제한 없음")]
    public float maxWardDistance = 18f; // 기존 10 → 1.8배 (감시자가 폭격 마커 역할을 하도록 넓힘)
    [Tooltip("이 비율 이상 멀어지면 생명체 주변에 한계선 표시")] [Range(0, 1)] public float leashWarnRatio = 0.75f;
    public Color leashColor = new Color(0.7f, 0.85f, 1f, 0.8f);

    [Header("레벨업")]
    public int xpBase = 5;
    public float xpGrowth = 1.25f;
    [Tooltip("이 배수 레벨마다 특수 강화")] public int specialEvery = 3;
    public UpgradeDef[] upgrades;

    [Header("레벨업 방식")]
    [Tooltip("켜면 기존 증강 선택(3택 카드, upgrades). 끄면 레벨업마다 autoRewards에서 랜덤 효과를 자동 획득 (임시 비활성: 코드는 그대로 둠)")]
    public bool levelUpChoices = false;
    [Tooltip("레벨업 자동 보상 목록 (levelUpChoices가 꺼져 있을 때)")]
    public LevelRewardDef[] autoRewards =
    {
        new LevelRewardDef("공격력", "공격력 +1.5", StatType.Damage, 1.5f, 1f, new Color(1f, 0.4f, 0.35f)),
        new LevelRewardDef("공격 속도", "공격 속도 +6%", StatType.AttackSpeed, 0.06f, 1f, new Color(1f, 0.65f, 0.25f), 12),
        new LevelRewardDef("생명체 이동속도", "생명체 이동속도 +0.25", StatType.WardSpeed, 0.25f, 0.8f, new Color(0.5f, 1f, 0.5f), 8),
        new LevelRewardDef("감시자 이동속도", "감시자 이동속도 +0.4", StatType.MoveSpeed, 0.4f, 0.6f, new Color(0.55f, 0.85f, 1f), 8),
        new LevelRewardDef("생명체 체력", "생명체 최대 체력 +12", StatType.WardMaxHp, 12f, 1f, new Color(1f, 0.55f, 0.75f)),
        new LevelRewardDef("폭격 범위", "폭격 범위 +12%", StatType.BombardArea, 0.12f, 0.8f, new Color(0.8f, 0.55f, 1f), 8),
        new LevelRewardDef("사거리", "생명체 사거리 +1", StatType.AttackRange, 1f, 0.6f, new Color(1f, 0.9f, 0.4f), 6),
        new LevelRewardDef("감시자 위력", "감시자 낙뢰/폭격 피해 +3", StatType.CompanionDamage, 3f, 0.7f, new Color(0.7f, 0.6f, 1f)),
        new LevelRewardDef("생명체 회복", "생명체 초당 회복 +0.15", StatType.WardRegen, 0.15f, 0.5f, new Color(0.45f, 1f, 0.8f), 6),
    };

    [Header("프리팹")]
    public Projectile projectilePrefab;
    public XpGem gemPrefab;
}

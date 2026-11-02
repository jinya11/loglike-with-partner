using UnityEngine;

/// <summary>플레이어 기본 능력치 / 레벨업 / 강화 설정.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Player")]
public class PlayerConfig : ScriptableObject
{
    [Tooltip("플레이어 기본 능력치 (생명체/감시자 수치는 각 설정에서)")]
    public PlayerStats baseStats = new PlayerStats();

    [Header("생명체와의 거리 제한 (전투)")]
    [Tooltip("조준점(플레이어)이 생명체에서 이 거리 이상 멀어질 수 없음. 0이면 제한 없음")]
    public float maxWardDistance = 10f;
    [Tooltip("이 비율 이상 멀어지면 생명체 주변에 한계선 표시")] [Range(0, 1)] public float leashWarnRatio = 0.75f;
    public Color leashColor = new Color(0.7f, 0.85f, 1f, 0.8f);

    [Header("레벨업")]
    public int xpBase = 5;
    public float xpGrowth = 1.25f;
    [Tooltip("이 배수 레벨마다 특수 강화")] public int specialEvery = 3;
    public UpgradeDef[] upgrades;

    [Header("프리팹")]
    public Projectile projectilePrefab;
    public XpGem gemPrefab;
}

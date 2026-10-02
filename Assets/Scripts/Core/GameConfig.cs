using UnityEngine;

[CreateAssetMenu(menuName = "Roguelike/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Lobby")]
    public string lobbyScene = "Lobby";
    public WeaponDef[] weapons;
    public CompanionDef[] companions;
    public ModifierDef[] modifiers;
    [Tooltip("Hazards 디버프: 위험 장판 주기/반경/피해")]
    public float hazardInterval = 2.5f, hazardRadius = 2.5f, hazardDamage = 20f, hazardDelay = 1f;

    [Header("Stage")]
    [Min(1)] public int stageCount = 5;
    [Tooltip("일반 스테이지 씬 (매 스테이지 랜덤 선택, 직전 씬 제외)")] public string[] stageScenes = { "Stage_Ruins", "Stage_Snow", "Stage_River", "Stage_Volcano" };
    [Tooltip("보너스 스테이지 씬 (스테이지 수에 포함 안 됨)")] public string[] bonusScenes = { "Bonus_Candy" };
    [Range(0, 1)] public float bonusChance = 0.3f;
    public int baseKillTarget = 30, killTargetPerStage = 15;

    [Header("Spawn")]
    public float spawnInterval = 0.9f, spawnIntervalPerStage = -0.1f, minSpawnInterval = 0.25f;
    public int spawnBatch = 3, maxAlive = 150;
    public float spawnDistance = 16f;

    [Header("Enemy")]
    public float enemyHp = 10f, enemyHpPerStage = 6f;
    public float enemySpeed = 2.6f, enemySpeedPerStage = 0.2f, enemyDamage = 8f;

    [Header("Prefabs")]
    public Enemy enemyPrefab;
    public Projectile projectilePrefab;
    public XpGem gemPrefab;
    public Fx fxPrefab;
    public GameObject portalPrefab;

    [Header("Player")]
    public PlayerStats baseStats = new PlayerStats();
    public int xpBase = 5;
    public float xpGrowth = 1.25f;
    [Tooltip("이 배수 레벨마다 특수 강화")] public int specialEvery = 3;
    public UpgradeDef[] upgrades;
}

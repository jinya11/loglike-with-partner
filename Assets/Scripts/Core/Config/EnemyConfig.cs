using UnityEngine;

/// <summary>적 기본 능력치 / 종류 설정.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Enemy")]
public class EnemyConfig : ScriptableObject
{
    [Header("기본 능력치 (스테이지마다 증가)")]
    public float enemyHp = 10f, enemyHpPerStage = 6f;
    public float enemySpeed = 2.6f, enemySpeedPerStage = 0.2f, enemyDamage = 8f;
    [Tooltip("플레이어가 이 거리 안이면 적이 생명체 대신 플레이어를 노림 (몸으로 막기)")]
    public float enemyAggroRadius = 3.5f;

    [Header("종류")]
    public Enemy enemyPrefab;
    [Tooltip("적 종류 (비어 있으면 enemyPrefab만 사용)")] public EnemyDef[] enemyTypes;
}

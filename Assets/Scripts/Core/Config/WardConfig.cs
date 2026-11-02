using UnityEngine;

/// <summary>지켜야 할 작은 생명체: 기본 능력치 + AI(배회/도망/길찾기/포위 구제) 설정.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Ward")]
public class WardConfig : ScriptableObject
{
    public Ward wardPrefab;

    [Header("생명체 종류 (모델 + 고유 능력)")]
    public CreatureDef[] creatures;
    [Tooltip("기본으로 쓰는 생명체 (creatures 번호)")] public int defaultCreature;
    [Tooltip("생명체 공격 증강 데이터 테이블 (공통 + 생명체 전용)")] public AttackAugmentTable attackAugments;

    [Header("기본 능력치 (런 시작 시 PlayerStats로 복사, 강화로 성장)")]
    public float wardMaxHp = 60f, wardRegen = 0.4f, wardSpeed = 3.2f;
    [Tooltip("약한 저항: 주변 적에게 주기적으로 작은 피해와 넉백")]
    public float wardPulseDamage = 3f, wardPulseRadius = 2.2f, wardPulseInterval = 6f;

    [Header("배회 / 도망")]
    public float wanderSpeedMul = 0.5f, wanderRadius = 0.85f, wanderPauseMin = 0.5f, wanderPauseMax = 2.5f;
    [Tooltip("이 거리 안에 적이 있으면 도망")] public float fleeRadius = 5f;
    public float portalReach = 1.5f;

    [Header("길찾기 (벽/장애물 회피 + 적 회피 최단 경로)")]
    [Tooltip("도망칠 때 목표 지점까지 거리")] public float fleeDistance = 10f;
    [Tooltip("적 주변 위험 반경과 비용 (클수록 적을 더 크게 돌아감)")] public float dangerRadius = 4f, dangerWeight = 12f;
    public float replanInterval = 0.35f, waypointReach = 0.7f;

    [Header("포위 구제 (사방이 막히면 자동 발동)")]
    [Tooltip("이 반경 안의 적으로 포위 여부 판단")] public float surroundRadius = 1.8f;
    [Tooltip("포위 판정: 최소 적 수, 막힌 방향 수(4방향 중)")] public int surroundCount = 4, surroundSides = 3;
    [Tooltip("이 시간 동안 포위가 유지되면 발동")] public float surroundDelay = 0.6f;
    public float rescueCooldown = 12f, rescueRadius = 4.5f, rescuePush = 28f, rescueShield = 1.5f;

    [Header("명령 체계 (전투 중 플레이어가 생명체에게 지시)")]
    [Tooltip("고리 연결/해제 (연결 중엔 플레이어가 생명체 근처에 묶임)")] public UnityEngine.InputSystem.Key linkKey = UnityEngine.InputSystem.Key.Tab;
    [Tooltip("자율 / 정지 / 따라오기 (누르면 고리도 해제)")]
    public UnityEngine.InputSystem.Key autoKey = UnityEngine.InputSystem.Key.Z, stayKey = UnityEngine.InputSystem.Key.X, followKey = UnityEngine.InputSystem.Key.C;
    [Tooltip("자율일 때 조작 대상 바로 고르기: 둘 다 / 감시자만 / 생명체만 (다른 명령 중이면 자율로 바뀜)")]
    public UnityEngine.InputSystem.Key bothKey = UnityEngine.InputSystem.Key.B, watcherOnlyKey = UnityEngine.InputSystem.Key.N, creatureOnlyKey = UnityEngine.InputSystem.Key.M;
    [Tooltip("감시자 자동(생명체만 조작): 재화를 찾는 반경, 생명체에서 벗어나지 않는 거리")] public float autoPickupRadius = 14f, autoStayNearWard = 7f;
    [Tooltip("따라오기: 플레이어와 이 거리 안이면 멈춤")] public float followStopDistance = 3f;
    [Tooltip("고리를 풀었을 때 둘 사이가 이 거리 이상이면 화면을 둘로 나눔 (듀얼 카메라)")] public float splitDistance = 18f;
    [Tooltip("자율일 때 화살표 키로 직접 조작하는 속도 배율 (wardSpeed 기준)")] public float steerSpeedMul = 1f;

    public void ApplyTo(PlayerStats s)
    {
        s.wardMaxHp = wardMaxHp; s.wardRegen = wardRegen; s.wardSpeed = wardSpeed;
        s.wardPulseDamage = wardPulseDamage; s.wardPulseRadius = wardPulseRadius; s.wardPulseInterval = wardPulseInterval;
    }
}

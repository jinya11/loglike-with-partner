using UnityEngine;

/// <summary>새 소녀 성장 config: 공통(CreatureGrowthConfig) + 새 소녀 수치만. 메뉴 Roguelike/Config/Growth/Bird.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Growth/Bird")]
public class BirdGrowthConfig : CreatureGrowthConfig
{
    [Header("새 소녀 수치")]
    public BirdGrowthSettings bird = new BirdGrowthSettings();
}

/// <summary>새 소녀 전용 수치. 기본 평타 = 깃털(FeatherAttack, 항상), 메인/패시브/감시자 효과는 BirdGrowth.</summary>
[System.Serializable]
public class BirdGrowthSettings
{
    [Header("깃털 (평타 투사체 / 바닥 깃털 / 공속 스택 UI)")]
    [Tooltip("깃털 투사체 프리팹 (FeatherShot)")] public FeatherShot featherPrefab;
    [Tooltip("캐릭터 옆 공속 스택 표시 (월드 공간 Canvas 프리팹, BirdStackHud)")] public BirdStackHud stackHudPrefab;
    [Tooltip("적과 닿는 반경")] public float featherHitRadius = 0.7f;

    [Header("메인1: 날개짓")]
    [Tooltip("띄우는 시간(초, 행동·공격 불가) / 높이")] public float liftDuration = 0.9f;
    [Tooltip("띄우는 높이")] public float liftHeight = 1.6f;
    [Header("메인2: 돌진")]
    [Tooltip("피격 시 피해를 무시하고 돌진할 확률")] [Range(0, 1)] public float dashChance = 0.3f;
    [Tooltip("돌진 거리(m, 7~12 권장)")] [Range(3f, 15f)] public float dashDistance = 10f;
    [Tooltip("돌진 시간(초) / 판정 반경")] public float dashTime = 0.25f, dashRadius = 1.4f;
    [Tooltip("돌진 피해 배율 (평타 피해 대비) / 재사용 대기(초)")] public float dashDamageMul = 1.5f, dashCooldown = 1.5f;
    public Color windColor = new Color(0.85f, 0.95f, 1f);
    [Header("메인3: 거침없는 날개 (깃털 확정 드롭, 주우면 4방향)")]
    [Tooltip("퍼지는 깃털 피해 배율 / 날아가는 거리 / 옆으로 휘는 포물선 폭(지면 기준 수평) / 높이(낮게)")] public float scatterDamageMul = 1.2f;
    [Tooltip("날아가는 거리 / 옆으로 휘는 포물선 폭(지면 기준 수평) / 높이(낮게)")] public float scatterRange = 9f, scatterCurve = 1.5f, scatterHeight = 0.2f;

    [Header("패시브1: 공속 스택")]
    [Tooltip("맞지 않고 이 시간(초)마다 1스택")] public float stackInterval = 1.5f;
    [Tooltip("최대 스택 / 스택당 공격 속도 증가(비율)")] public int maxStacks = 10;
    public float stackAttackSpeed = 0.04f;
    [Header("패시브2: 깃털 드롭")]
    [Tooltip("명중 시 바닥에 깃털이 남을 확률")] [Range(0, 1)] public float dropChance = 0.12f;
    [Tooltip("바닥 깃털 유지 시간(초) / 줍는 반경 / 동시에 남는 최대 수")] public float pickupLife = 8f, pickupRadius = 1.3f;
    public int maxPickups = 8;
    [Tooltip("주우면: 지속(초) / 이동 속도 + / 공격 속도 +(비율)")] public float buffDuration = 4f, buffMoveSpeed = 0.8f, buffAttackSpeed = 0.2f;
    [Header("패시브3: 빠를수록 피해 감소")]
    [Tooltip("이동 속도 배율 1 증가당 / 공격 속도 배율 1 증가당 피해 감소, 최대 감소")] public float guardPerMove = 0.3f;
    [Tooltip("공격 속도 배율 1 증가당 피해 감소 / 최대 감소")] public float guardPerAttack = 0.3f, guardMax = 0.4f;

    [Header("감시자: 가로 돌풍 (폭격 범위 안에 새 소녀가 있을 때)")]
    [Tooltip("돌풍 길이 / 폭 / 넉백 속도 (피해 없음) / 한 칸씩 나아가는 간격(초)")] public float gustLength = 16f;
    [Tooltip("폭 / 넉백 속도 (피해 없음) / 한 칸씩 나아가는 간격(초)")] public float gustWidth = 2.4f, gustPush = 30f, gustStepTime = 0.04f;
}

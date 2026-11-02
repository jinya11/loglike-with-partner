using UnityEngine;

/// <summary>로비 지역 지도(3x3)에서 고를 수 있는 전투 지역 하나 = 지역 씬 하나.</summary>
[System.Serializable]
public class RegionDef
{
    public string name;
    [Tooltip("이 지역의 전투 씬 (Build Settings에 등록)")] public string scene;
    [TextArea] public string desc;
    [Tooltip("지도 칸 색")] public Color mapColor = new Color(0.3f, 0.3f, 0.35f);
}

/// <summary>스테이지 진행 / 몬스터 스폰 / 난이도 디버프 설정.</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Stage")]
public class StageConfig : ScriptableObject
{
    [Header("진행")]
    public string lobbyScene = "Lobby";
    [Min(1)] public int stageCount = 5;
    [Tooltip("일반 스테이지 씬 (지역을 고르지 않았을 때: 매 스테이지 랜덤 선택, 직전 씬 제외)")] public string[] stageScenes = { "Region_Ruins", "Region_Snow", "Region_River", "Region_Volcano" };

    [Header("지역 (로비 3x3 지도)")]
    [Tooltip("1번 (지도 중앙): 로비 하늘섬 바로 밑, 칠흑 같은 어둠. 로테이션 안 됨")] public RegionDef centerRegion;
    [Tooltip("2~9번 칸에 로테이션되는 지역 후보 (8개 이상이면 매번 일부가 바뀜)")] public RegionDef[] regionPool;
    [Tooltip("지역 선택 화면 프리팹 (출발 포털에서 F)")] public GameObject regionSelectPrefab;
    [Header("보너스 / 처치 목표")]
    [Tooltip("보너스 스테이지 씬 (스테이지 수에 포함 안 됨)")] public string[] bonusScenes = { "Bonus_Candy" };
    [Range(0, 1)] public float bonusChance = 0.3f;
    [Tooltip("다음 스테이지로 가기 위한 처치 수 = base + 스테이지 번호 × perStage")]
    public int baseKillTarget = 120, killTargetPerStage = 60;
    [Tooltip("보더를 열면 처치 목표 증가: 열린 구역 넓이가 중앙과 같아질 때마다 목표 × (1 + 이 값)")] public float killPerOpenedArea = 0.5f;

    [Header("스폰 (폴링)")]
    public float spawnInterval = 0.8f, spawnIntervalPerStage = -0.08f, minSpawnInterval = 0.3f;
    public int spawnBatch = 6, maxAlive = 250;
    [Tooltip("생명체로부터 이 거리에서 스폰")] public float spawnDistance = 16f;

    [Header("난이도 디버프")]
    public ModifierDef[] modifiers;
    [Tooltip("Hazards 디버프: 위험 장판 주기/반경/피해")]
    public float hazardInterval = 2.5f, hazardRadius = 2.5f, hazardDamage = 20f, hazardDelay = 1f;

    [Header("프리팹")]
    public GameObject portalPrefab;
    [Tooltip("도형 이펙트 (텔레그래프/광역)")] public Fx fxPrefab;
    [Tooltip("스테이지 HUD. 첫 스테이지에서 한 번만 생성되고 씬이 바뀌어도 유지 (싱글톤)")] public Hud hudPrefab;
}

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

    [Header("보더 확장 (심연 전용)")]
    [Tooltip("보더를 넓혀가는 구조를 쓰는 씬 (포털/다음 스테이지 없음). 나머지 지역은 보더가 중앙 칸 하나로 고정")]
    public string[] expandableBorderScenes = { "Region_Abyss" };
    public bool IsExpandable(string scene) => expandableBorderScenes != null && System.Array.IndexOf(expandableBorderScenes, scene) >= 0;

    [Header("지형 (Unity Terrain, 심연 외 지역 씬)")]
    [Tooltip("지역 씬의 Terrain으로 높이를 따름. 기본 지형은 메뉴 Roguelike/Setup Region Terrains로 만들고 Terrain 툴로 직접 편집")]
    public GroundSettings ground = new GroundSettings();

    [Header("포털")]
    [Tooltip("포털은 이동 가능한 지면 위 임의 위치에 생성: 생명체/플레이어에서 최소 이 거리")] public float portalMinDistance = 22f;
    [Tooltip("포털이 열리면 남은 적이 플레이어에게서 도망 (처치는 가능). 도망 속도 배율")] public float routSpeedMul = 1.25f;

    [Header("다음 스테이지 특징 (카드 선택, 심연 외)")]
    [Tooltip("포털로 이동하기 전에 다음 스테이지 특징을 카드로 고름")] public bool stageTraitsEnabled = true;
    [Tooltip("한 번에 보여 줄 카드 수")] [Range(1, 3)] public int traitChoiceCount = 3;
    [Tooltip("엘리트: 크기 배율, 피해 배율, 처치 시 경험치 보석 수")] public float eliteScale = 1.5f, eliteDamageMul = 1.5f;
    public int eliteGems = 6;
    [Tooltip("제한 시간 초과(광폭화) 시 적 이동속도 배율 / 스폰 배율")] public float enrageSpeedMul = 1.4f, enrageSpawnMul = 1.5f;
    [Tooltip("스테이지 증강 (누적형: 고른 것은 런 동안 계속 적용되고 다시 나오지 않음). 사건 = 스테이지에서 일어나는 일, 변이 = 적/땅/규칙이 바뀜")]
    public StageTraitDef[] stageTraits =
    {
        // ---- 사건 ----
        new StageTraitDef(StageTraitCategory.Event, "몰려드는 무리", "적이 60% 더 많이 몰려온다.\n경험치 +50%", StageTraitType.Horde, 0.6f, 0.5f, new Color(1f, 0.45f, 0.35f)),
        new StageTraitDef(StageTraitCategory.Event, "정예의 습격", "12% 확률로 엘리트(체력 5배, 거대) 출현.\n처치 시 경험치 보석 다수", StageTraitType.Elite, 0.12f, 5f, new Color(1f, 0.75f, 0.25f)),
        new StageTraitDef(StageTraitCategory.Event, "시간과의 싸움", "150초 제한. 시간 안에는 경험치 +50%,\n넘기면 적이 광폭화한다", StageTraitType.TimeLimit, 150f, 0.5f, new Color(0.85f, 0.6f, 1f)),
        new StageTraitDef(StageTraitCategory.Event, "유성우", "8초마다 유성이 떨어진다.\n적도 생명체도 피해(40)를 입는다", StageTraitType.MeteorShower, 8f, 40f, new Color(1f, 0.55f, 0.2f)),
        new StageTraitDef(StageTraitCategory.Event, "보급 상자", "25초마다 보급 상자가 떨어진다.\n경험치 보석 12개", StageTraitType.SupplyDrop, 25f, 12f, new Color(0.5f, 0.9f, 1f)),
        new StageTraitDef(StageTraitCategory.Event, "습격 파도", "30초마다 적 25마리가\n한꺼번에 몰려온다", StageTraitType.Waves, 30f, 25f, new Color(1f, 0.35f, 0.5f)),
        new StageTraitDef(StageTraitCategory.Event, "보물 운반자", "40초마다 도망치는 황금 적이 나타난다.\n잡으면 보석 15개 + 자원", StageTraitType.TreasureCarrier, 40f, 15f, new Color(1f, 0.85f, 0.2f)),
        new StageTraitDef(StageTraitCategory.Event, "성역", "회복의 샘(초당 회복 4) 3곳이 생긴다", StageTraitType.Sanctuary, 3f, 4f, new Color(0.5f, 1f, 0.75f), 0.8f),
        new StageTraitDef(StageTraitCategory.Event, "풍요의 땅", "지역 자원 드롭 확률 2배", StageTraitType.Riches, 2f, 0f, new Color(0.45f, 0.85f, 1f)),
        new StageTraitDef(StageTraitCategory.Event, "짙은 안개", "시야가 절반으로 줄어든다.\n경험치 +30%", StageTraitType.Fog, 0.45f, 0.3f, new Color(0.7f, 0.75f, 0.8f)),
        new StageTraitDef(StageTraitCategory.Event, "속전속결", "처치 목표 -30%.\n대신 적 체력 +40%", StageTraitType.Blitz, 0.7f, 0.4f, new Color(1f, 0.6f, 0.45f)),
        // ---- 변이 ----
        new StageTraitDef(StageTraitCategory.Mutation, "질주", "적 이동속도 +30%.\n경험치 +30%", StageTraitType.Swift, 0.3f, 0.3f, new Color(1f, 0.55f, 0.75f)),
        new StageTraitDef(StageTraitCategory.Mutation, "강철 피부", "적 체력 +50%.\n경험치 +40%", StageTraitType.IronSkin, 0.5f, 0.4f, new Color(0.7f, 0.75f, 0.85f)),
        new StageTraitDef(StageTraitCategory.Mutation, "분열", "적이 죽으면 35% 확률로\n작은 적 2마리로 갈라진다", StageTraitType.Split, 0.35f, 0.3f, new Color(0.55f, 1f, 0.5f)),
        new StageTraitDef(StageTraitCategory.Mutation, "자폭", "적이 죽을 때 폭발한다.\n가까운 생명체에게 피해 8", StageTraitType.Volatile, 2.2f, 8f, new Color(1f, 0.4f, 0.2f)),
        new StageTraitDef(StageTraitCategory.Mutation, "거대화", "적이 커지고 체력 1.6배, 조금 느려진다.\n경험치 +40%", StageTraitType.Giant, 1.6f, 0.4f, new Color(0.85f, 0.5f, 0.4f)),
        new StageTraitDef(StageTraitCategory.Mutation, "날카로운 이빨", "적 접촉 피해 +40%.\n경험치 +30%", StageTraitType.Sharp, 0.4f, 0.3f, new Color(0.95f, 0.3f, 0.35f)),
        new StageTraitDef(StageTraitCategory.Mutation, "재생", "적이 초당 최대 체력의 4%를 회복한다.\n경험치 +30%", StageTraitType.Regen, 0.04f, 0.3f, new Color(0.45f, 0.9f, 0.6f)),
        new StageTraitDef(StageTraitCategory.Mutation, "독의 땅", "평지에 독 웅덩이(초당 피해 8)가 생기고\n위험 장판이 떨어진다", StageTraitType.Toxic, 6f, 8f, new Color(0.6f, 0.9f, 0.3f)),
        new StageTraitDef(StageTraitCategory.Mutation, "고지대", "언덕이 1.6배 높고 가팔라진다", StageTraitType.Highlands, 1.6f, 0f, new Color(0.55f, 0.85f, 0.45f)),
        new StageTraitDef(StageTraitCategory.Mutation, "진흙탕", "평지에 진흙탕 6곳.\n생명체 이동속도 -40%", StageTraitType.Mire, 6f, 0.6f, new Color(0.6f, 0.45f, 0.3f)),
    };

    [Header("프리팹")]
    public GameObject portalPrefab;
    [Tooltip("도형 이펙트 (텔레그래프/광역)")] public Fx fxPrefab;
    [Tooltip("스테이지 HUD. 첫 스테이지에서 한 번만 생성되고 씬이 바뀌어도 유지 (싱글톤)")] public Hud hudPrefab;
}

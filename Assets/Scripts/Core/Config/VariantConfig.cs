using UnityEngine;

/// <summary>
/// 파생 스테이지 설정 (Assets/Prototype/Resources/Config/VariantConfig.asset, GameConfig.variant).
/// 지역마다 기본형 + 파생형. 파생형 목록(조건 증강, 종류, 색, 적 구성), 공통 규칙, 파생형별 수치를 여기서 편집.
/// 새 파생형 추가: stageVariants에 행 추가 + StageVariantKind에 종류 추가 + 규칙 스크립트(Stage/Variants) 하나.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Variant")]
public class VariantConfig : ScriptableObject
{
    [Header("파생 스테이지 목록")]
    [Tooltip("조건 증강을 보유하고 그 지역 포털이 열리면 파생 포털이 하나 더 열림 (같은 파생형은 런에 한 번)")]
    public StageVariantDef[] stageVariants =
    {
        new StageVariantDef("forest_firefly", "반딧불 숲", "밤의 숲. 흩어진 반딧불을 모아 등불을 켜면 그 구역이 밝아지고 안전지대가 된다.", "Region_Forest", "Region_Forest_Firefly", StageTraitType.Fog, StageVariantKind.ForestFirefly, new Color(0.65f, 1f, 0.45f), EnemyAbility.Fear, 3f),
        new StageVariantDef("forest_farmland", "자연 농지", "벼 농사가 이루어진 곳. 스테이지 전체가 벼로 둘러싸이고 벼가루가 휘날린다. 벼에 가려 적은 윗부분만 보인다 — 폭격으로 벼를 베면 드러난다.", "Region_Forest", "Region_Forest_Farmland", StageTraitType.Riches, StageVariantKind.ForestFarmland, new Color(1f, 0.85f, 0.35f), EnemyAbility.Slow, 3f),
        new StageVariantDef("desert_sandstorm", "모래폭풍 사막", "주기적으로 모래폭풍이 지나가며 모두를 한 방향으로 민다. 동굴 바위 뒤가 피난처, 폭풍 뒤에 묻힌 유물이 드러난다.", "Region_Desert", "Region_Desert_Sandstorm", StageTraitType.Swift, StageVariantKind.DesertSandstorm, new Color(1f, 0.78f, 0.45f), EnemyAbility.Charge, 2f),
        new StageVariantDef("snow_frozenlake", "얼어붙은 호수", "얼음판 위의 전장. 금 간 얼음은 깨지고, 물에 빠진 적은 즉사한다. 작은 생명체도 빠질 수 있다.", "Region_Snow", "Region_Snow_FrozenLake", StageTraitType.Mire, StageVariantKind.SnowFrozenLake, new Color(0.6f, 0.88f, 1f), EnemyAbility.Charge, 2f),
        new StageVariantDef("volcano_caldera", "칼데라", "용암 호수를 둘러싼 고리 모양 전장. 용암 수위가 오르내리며 고리의 폭이 바뀐다.", "Region_Volcano", "Region_Volcano_Caldera", StageTraitType.Volatile, StageVariantKind.VolcanoCaldera, new Color(1f, 0.5f, 0.25f), EnemyAbility.Charge, 2f),
        new StageVariantDef("swamp_wisp", "도깨비불 늪", "떠다니는 도깨비불에 닿으면 어딘가로 순간이동한다.", "Region_Swamp", "Region_Swamp_Wisp", StageTraitType.Fog, StageVariantKind.SwampWisp, new Color(0.55f, 1f, 0.85f), EnemyAbility.Slow, 2f),
        new StageVariantDef("graveyard_awakened", "깨어난 묘지", "무덤에서 적이 계속 일어난다. 무덤을 부숴야 한다.", "Region_Graveyard", "Region_Graveyard_Awakened", StageTraitType.Waves, StageVariantKind.GraveyardAwakened, new Color(0.75f, 0.7f, 0.95f), EnemyAbility.None, 1f) { alsoRequired = new[] { StageTraitType.Regen } },
        new StageVariantDef("graveyard_moonlit", "달빛 묘역", "달빛이 비치는 구역이 천천히 옮겨 다닌다. 달빛 안에서 작은 생명체는 회복한다.", "Region_Graveyard", "Region_Graveyard_Moonlit", StageTraitType.Sanctuary, StageVariantKind.GraveyardMoonlit, new Color(0.75f, 0.85f, 1f), EnemyAbility.None, 1f),
    };

    [Header("공통 규칙")]
    [Tooltip("파생 스테이지로 들어가면 스테이지 번호가 현재 번호에서 이만큼 줄어듦 (예: 4 → 3). 1 미만으로는 내려가지 않음. 번호 기반 스케일링(적 체력 등)과 HUD도 줄어든 번호를 따름")]
    [Min(0)] public int stageDecrease = 1;
    [Tooltip("일반 포털에서 파생 포털까지 최소 거리")] public float portalSpacing = 16f;

    [Header("숲: 반딧불 숲")]
    public FireflyForestSettings firefly = new FireflyForestSettings();
    [Header("숲: 자연 농지")]
    public FarmlandSettings farmland = new FarmlandSettings();
    [Header("사막: 모래폭풍 사막")]
    public SandstormSettings sandstorm = new SandstormSettings();
    [Header("설원: 얼어붙은 호수")]
    public FrozenLakeSettings frozenLake = new FrozenLakeSettings();
    [Header("화산: 칼데라")]
    public CalderaSettings caldera = new CalderaSettings();
    [Header("늪: 도깨비불 늪")]
    public WispSwampSettings wisp = new WispSwampSettings();
    [Header("묘지: 깨어난 묘지")]
    public AwakenedGraveyardSettings awakened = new AwakenedGraveyardSettings();
    [Header("묘지: 달빛 묘역")]
    public MoonlitGraveyardSettings moonlit = new MoonlitGraveyardSettings();
}

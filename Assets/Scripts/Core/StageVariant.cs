using UnityEngine;

/// <summary>파생 스테이지의 고유 규칙 종류 (지역마다 추가).</summary>
public enum StageVariantKind
{
    [Tooltip("숲: 밤의 숲. 반딧불을 모아 등불을 켜면 그 구역이 밝아지고 안전지대")] ForestFirefly,
    [Tooltip("숲: 자연 농지. 스테이지 전체가 벼 논, 벼 속의 적은 가려짐, 폭격으로 벼를 베면 시야 확보")] ForestFarmland,
}

/// <summary>
/// 파생 스테이지 하나 (StageConfig.stageVariants). 지역(씬)마다 기본형 + 파생형.
/// 조건 증강을 보유한 상태에서 그 지역의 포털이 열리면 일반 포털 옆에 파생 포털이 하나 더 열림 (플레이어가 고름).
/// 증강은 해금 조건일 뿐이고, 파생형의 내용은 증강 효과와 별개의 고유 규칙 (증강 수치 효과는 그대로 적용).
/// </summary>
[System.Serializable]
public class StageVariantDef
{
    [Tooltip("고유 id")] public string id;
    public string name;
    [TextArea(1, 3)] public string desc;
    [Tooltip("파생 포털이 열리는 지역(기본형) 씬")] public string regionScene;
    [Tooltip("파생 스테이지 전용 씬 (이 씬을 불러옴. Build Settings 등록 필요)")] public string scene;
    [Tooltip("해금 조건 증강 (이 증강을 보유하면 파생 포털이 열림)")] public StageTraitType requiredTrait;
    public StageVariantKind kind;
    [Tooltip("파생 포털/이름 색")] public Color color = Color.white;
    [Tooltip("켜면 지역 기본형의 자동 생성 모델(배치 소품: 나무·바위·장판, 보더 밖 풍경, 증강이 만드는 독 웅덩이·회복의 샘·진흙탕)을 그대로 씀. 끄면(기본) 파생 고유 오브젝트만")]
    public bool autoProps = false;
    [Header("적 구성 (기본형과 다르게)")]
    [Tooltip("이 능력을 가진 적의 등장 가중치 배율")] public EnemyAbility enemyBias;
    public float enemyBiasMul = 1f;

    public StageVariantDef() { }
    public StageVariantDef(string id, string name, string desc, string regionScene, string scene, StageTraitType trait, StageVariantKind kind, Color color, EnemyAbility bias, float biasMul)
    { this.id = id; this.name = name; this.desc = desc; this.regionScene = regionScene; this.scene = scene; requiredTrait = trait; this.kind = kind; this.color = color; enemyBias = bias; enemyBiasMul = biasMul; }
}

/// <summary>반딧불 숲 수치 (StageConfig.firefly).</summary>
[System.Serializable]
public class FireflyForestSettings
{
    [Tooltip("흩어진 반딧불 수")] public int fireflyCount = 45;
    [Tooltip("감시자(조준점)가 이 거리 안에 오면 반딧불을 모음")] public float collectRadius = 2.2f;
    [Tooltip("등불 수")] public int lanternCount = 5;
    [Tooltip("등불 하나를 켜는 데 필요한 반딧불 수")] public int firefliesPerLantern = 6;
    [Tooltip("감시자가 등불에 이 거리 안이면 모은 반딧불로 등불을 켬")] public float lightRadius = 3f;
    [Tooltip("켜진 등불의 안전지대 반경 (밝아짐)")] public float safeRadius = 9f;
    [Tooltip("안전지대에 들어온 적을 밀어내는 속도")] public float repelSpeed = 9f;
    [Tooltip("안전지대 안 적이 받는 초당 피해")] public float safeDps = 6f;
    [Tooltip("안전지대 안 생명체 초당 회복")] public float wardRegen = 2f;
    [Tooltip("켜진 등불 밝기")] public float lanternIntensity = 7f;
    [Tooltip("등불이 켜져 있는 시간(초). 지나면 꺼지고, 반딧불을 다시 모아 켤 수 있음")] public float lanternDuration = 5f;
    [Tooltip("반딧불이 다시 나타나는 간격(초) — 최대 fireflyCount까지")] public float fireflyRespawn = 2.5f;
    [Tooltip("밤: 해 밝기 배율 / 주변광")] public float sunMul = 0.12f;
    public Color ambient = new Color(0.05f, 0.06f, 0.1f);
}

/// <summary>자연 농지 수치 (StageConfig.farmland).</summary>
[System.Serializable]
public class FarmlandSettings
{
    [Tooltip("지형 높이 배율 (논은 평평하게)")] public float terrainHeightMul = 0.15f;
    [Tooltip("벼 모델 (Meshy로 만든 벼 포기 덤불). 비우면 코드로 만든 잎으로 대체")] public Mesh riceMesh;
    [Tooltip("벼 재질 2종 (황금 / 연두) — 섞어서 깔아 색 변주. 비우면 기본 재질")] public Material riceMaterialGold, riceMaterialGreen;
    [Tooltip("벼 모델의 위쪽이 Z축 (Meshy FBX). 켜면 세워서 씀")] public bool riceMeshUpIsZ = true;
    [Tooltip("벼 한 포기의 폭 (m) — 모델을 이 크기로 맞춤")] public float clumpSize = 0.65f;
    [Tooltip("줄 간격 (m) — 포기 폭보다 넓게 해서 줄 사이 물 댄 논바닥이 보이게")] public float rowSpacing = 0.8f;
    [Tooltip("줄 안 포기 간격 (m, 일정)")] public float plantSpacing = 0.45f;
    [Tooltip("위치 흔들림 (m) / 회전 흔들림 (도) / 크기 흔들림 (비율) — 작게 해야 가지런함")] public float jitter = 0.03f, yawJitter = 10f, sizeJitter = 0.05f;
    [Tooltip("벼 높이 (최소, 최대) — 논 구획마다 하나로 정해짐. 적 키가 0.5~0.9m라 아래쪽만 묻히고 윗부분이 보임")] public Vector2 height = new Vector2(0.55f, 0.72f);
    [Tooltip("황금색으로 익은 논 구획 비율 (구획 단위로 색이 정해짐, 나머지는 초록)")] [Range(0, 1)] public float goldRatio = 0.2f;
    [Tooltip("논두렁(길) 간격 / 폭")] public float pathEvery = 14f, pathWidth = 1.6f;
    public Color riceColor = new Color(0.78f, 0.72f, 0.32f), riceTipColor = new Color(0.9f, 0.82f, 0.4f);
    [Tooltip("논 바닥 색 (줄 사이 물 댄 논: 짙은 물빛)")] public Color paddyGround = new Color(0.27f, 0.33f, 0.27f);
    [Tooltip("논 바닥 매끄러움 (물 반사 느낌)")] [Range(0, 1)] public float paddyWaterSmoothness = 0.8f;
    [Tooltip("폭격이 벼를 베는 반경 배율 (폭격 반경 기준)")] public float cutRadiusMul = 1f;
    [Tooltip("베이지 않은 벼 속 적의 몸이 가려지는 비율 (0 = 다 보임, 1 = 다 가려짐)")] [Range(0, 1)] public float hiddenFraction = 0.5f;
    [Tooltip("가림 얼룩 크기 (클수록 잘게)")] public float maskTiling = 2f;
    [Tooltip("화면 전체 벼가루 파티클: 초당 수, 투명도")] public float pollenRate = 60f, pollenAlpha = 0.45f;
    [Tooltip("벼가루 크기 (최소, 최대)")] public Vector2 pollenSize = new Vector2(0.1f, 0.22f);
}

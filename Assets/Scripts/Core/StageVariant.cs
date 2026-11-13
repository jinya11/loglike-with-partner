using UnityEngine;

/// <summary>파생 스테이지의 고유 규칙 종류 (지역마다 추가).</summary>
public enum StageVariantKind
{
    [Tooltip("숲: 밤의 숲. 반딧불을 모아 등불을 켜면 그 구역이 밝아지고 안전지대")] ForestFirefly,
    [Tooltip("숲: 자연 농지. 스테이지 전체가 벼 논, 벼 속의 적은 가려짐, 폭격으로 벼를 베면 시야 확보")] ForestFarmland,
    [Tooltip("사막: 모래폭풍 사막. 주기적 모래폭풍이 한 방향으로 밀어냄, 동굴 바위 뒤가 피난처, 폭풍 뒤 묻힌 유물이 드러남")] DesertSandstorm,
    [Tooltip("설원: 얼어붙은 호수. 얼음판 전장, 금 간 얼음은 밟으면 깨져 구멍 — 적은 빠지면 즉사, 생명체는 빠지면 스페이스 연타로 탈출")] SnowFrozenLake,
    [Tooltip("화산: 칼데라. 용암 호수를 둘러싼 고리 모양 플랫폼 전장(지형/보더 없음). 용암 수위가 주기적으로 오르내리고, 그때 가장자리 플랫폼이 가라앉아 고리 폭이 바뀜")] VolcanoCaldera,
    [Tooltip("늪: 도깨비불 늪. 떠다니는 도깨비불에 닿으면 스테이지 안 무작위 안전 위치로 순간이동 (생명체/적)")] SwampWisp,
    [Tooltip("묘지: 깨어난 묘지. 무덤에서 적이 계속 일어남. 처치 목표 1.5배 + 무덤 N개를 부숴야 클리어")] GraveyardAwakened,
    [Tooltip("묘지: 달빛 묘역. 달빛이 비치는 구역이 천천히 옮겨 다님. 달빛 안에서 생명체 회복, 생명체는 적에게서 도망치며 달빛을 따라감")] GraveyardMoonlit,
    [Tooltip("버섯 숲: 발광 균사 동굴. 생명체 직접 조작, 감시자·폭격 없음. 적은 잠들어 있다가 균사를 밟아 번진 빛이 닿으면 깨어남. 생명체 공격은 발각됐을 때만")] MushroomMycelium,
    [Tooltip("강: 상류의 댐. 지형/보더 없는 세로로 긴 플랫폼 맵. 수문 장치 3개를 지키고, 수문을 열면 물살이 아래쪽 적을 쓸어 감. 감시자 조작이 메인")] RiverDam,
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
    [Tooltip("함께 보유해야 하는 증강 (모두 보유해야 열림). 비우면 requiredTrait 하나만")] public StageTraitType[] alsoRequired;
    public StageVariantKind kind;
    [Tooltip("파생 포털/이름 색")] public Color color = Color.white;
    [Tooltip("켜면 환경사(구멍/용암 등에 빠져 죽음)도 처치 수에 침. 기본은 끔 (보상은 어느 쪽이든 그대로)")] public bool environmentKillsCount = false;
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

/// <summary>모래폭풍 사막 수치 (VariantConfig.sandstorm).</summary>
[System.Serializable]
public class SandstormSettings
{
    [Header("폭풍 주기")]
    [Tooltip("스테이지 시작 후 첫 폭풍 경보까지(초)")] public float firstDelay = 10f;
    [Tooltip("폭풍이 끝나고 다음 경보까지 잔잔한 시간(초)")] public float calm = 18f;
    [Tooltip("경보 시간(초): 모래가 일기 시작하고 남은 시간이 표시됨")] public float warning = 5f;
    [Tooltip("폭풍 시작 몇 초 전부터 적이 움직이지 못함 (폭풍이 끝날 때까지). 경보 시간 이하")] public float freezeLead = 3f;
    [Tooltip("폭풍 지속(초)")] public float duration = 7f;
    [Header("바람")]
    [Tooltip("바람이 생명체를 미는 속도")] public float windSpeed = 6.5f;
    [Tooltip("적을 미는 속도 배율 (생명체 기준)")] public float enemyWindMul = 1.3f;
    [Tooltip("피난처 밖 생명체가 폭풍 중 받는 초당 피해")] public float stormWardDps = 3f;
    [Tooltip("바위 뒤(바람 반대쪽)로 바람이 막히는 길이(m)")] public float shelterLength = 7f;
    [Header("유물 (폭풍 뒤 모래에서 드러남)")]
    [Tooltip("폭풍 한 번에 드러나는 유물 수")] public int relicsPerStorm = 3;
    [Tooltip("감시자나 생명체가 이 거리 안에 오면 유물을 주움")] public float relicCollectRadius = 2.2f;
    [Tooltip("유물 하나의 경험치 보석 수")] public int relicGems = 5;
    [Tooltip("유물에서 이 지역 자원도 반드시 나옴")] public bool relicResource = true;
    [Tooltip("유물이 다시 묻히기까지(초)")] public float relicLifetime = 30f;
    [Header("연출")]
    [Tooltip("동굴 바위 수 (씬 빌더가 배치, 이후 씬에서 옮기면 됨)")] public int caveCount = 6;
    [Tooltip("바람이 불어가는 방향(도, 0 = +Z). 씬 빌더가 규칙 컴포넌트에 넣고 동굴 입구를 이쪽으로 돌림")] public float windYaw = 90f;
    public Color sandColor = new Color(0.93f, 0.78f, 0.52f);
    [Tooltip("폭풍 중 모래 입자 초당 수")] public float sandRate = 1600f;
    [Tooltip("폭풍 중 안개 끝 거리 (작을수록 시야가 좁음)")] public float stormFogEnd = 60f;
}

/// <summary>얼어붙은 호수 수치 (VariantConfig.frozenLake).</summary>
[System.Serializable]
public class FrozenLakeSettings
{
    [Header("호수 (씬 빌더가 얼음판을 깖 — 이후 씬에서 얼음판을 지우거나 금 간 상태를 바꿔도 됨)")]
    [Tooltip("호수 반경(m)")] public float lakeRadius = 36f;
    [Tooltip("얼음판 한 칸 크기(m)")] public float tileSize = 3f;
    [Tooltip("시작 시 금 간 얼음 비율")] [Range(0, 1)] public float crackedRatio = 0.15f;
    [Tooltip("시작 지점 주변 이 반경은 처음엔 금 가지 않음")] public float safeRadius = 7f;
    [Header("균열")]
    [Tooltip("새 균열이 생기는 간격(초)")] public float crackInterval = 3f;
    [Tooltip("금 간 얼음 최대 수 (구멍 제외)")] public int maxCracked = 45;
    [Tooltip("생명체가 금 간 얼음 위에 이만큼 있으면 깨짐(초)")] public float wardBreakTime = 0.9f;
    [Tooltip("적이 금 간 얼음 위에 이만큼 있으면 깨짐(초, 여럿이면 합산)")] public float enemyBreakTime = 1.2f;
    [Tooltip("깨지기 시작해서 구멍이 될 때까지(초) — 흔들리며 경고")] public float breakDelay = 0.4f;
    [Tooltip("구멍이 다시 얼기까지(초, 0 = 안 얼음)")] public float refreezeTime = 35f;
    [Header("빠짐 (생명체)")]
    [Tooltip("빠져나오는 데 필요한 스페이스 연타 수")] public int escapePresses = 12;
    [Tooltip("제한 시간(초): 넘기면 큰 피해를 입고 끌려 나옴")] public float escapeTime = 5f;
    [Tooltip("제한 시간을 넘기면 받는 피해 (최대 체력 비율)")] [Range(0, 1)] public float failDamageFrac = 0.25f;
    [Tooltip("빠진 동안 초당 피해 (차가운 물)")] public float inWaterDps = 2f;
    [Header("탈출 후 (생명체가 빠졌던 구멍)")]
    [Tooltip("생명체가 빠져나오면(연타 성공/시간 초과 모두) 그 구멍은 바로 멀쩡한 얼음으로 돌아오고, 균열이 다른 얼음판으로 옮겨 감")] public bool relocateCrackOnEscape = true;
    [Tooltip("옮겨 갈 균열: 생명체(탈출 지점)와 복구된 칸에서 최소 이만큼 떨어진 칸(m)")] public float relocateMinDist = 6f;
    [Tooltip("옮겨 갈 균열: 생명체에서 최대 이 거리 안의 칸(m, 0 = 제한 없음). 범위 안에 후보가 없으면 제한을 풀고 고름")] public float relocateMaxDist = 18f;
    [Header("색 (무광 파스텔)")]
    public Color iceColor = new Color(0.78f, 0.92f, 1f);
    public Color crackColor = new Color(0.32f, 0.5f, 0.72f);
    public Color waterColor = new Color(0.16f, 0.3f, 0.48f);
}

/// <summary>칼데라 수치 (VariantConfig.caldera).</summary>
[System.Serializable]
public class CalderaSettings
{
    public enum VanishAt { [Tooltip("수위가 낮아질 때 가라앉음 (오를 때 다시 떠오름)")] Low, [Tooltip("수위가 높아질 때 잠김 (내려갈 때 다시 드러남)")] High }

    [Header("고리 (씬 빌더가 플랫폼을 깖 — 이후 씬에서 플랫폼을 옮기거나 지우거나 '가라앉음'을 바꿔도 됨)")]
    [Tooltip("용암 호수(가운데) 반경 = 고리 안쪽 반경(m)")] public float innerRadius = 12f;
    [Tooltip("고리 바깥 반경(m). 그 밖도 용암 (보더 없음)")] public float outerRadius = 42f;
    [Tooltip("플랫폼 한 칸 크기(m)")] public float tileSize = 3f;
    [Tooltip("안쪽 가장자리에서 가라앉는 띠 폭(m)")] public float innerBand = 7f;
    [Tooltip("바깥 가장자리에서 가라앉는 띠 폭(m)")] public float outerBand = 8f;
    [Tooltip("가라앉는 띠 경계의 들쭉날쭉함(m)")] public float bandJitter = 2f;
    [Header("가라앉을 플랫폼 고르기")]
    [Tooltip("켜면(기본) 스테이지에 들어올 때와 플랫폼이 다시 올라올 때마다 가라앉을 플랫폼을 무작위로 다시 고름 (씬의 'vanishes' 지정은 무시). 끄면 씬 지정 그대로")] public bool randomVanish = true;
    [Tooltip("가라앉을 플랫폼 비율 (전체 칸 대비). 남는 칸이 서로 이어져야 해서 실제로는 조금 적을 수 있음")] [Range(0, 0.9f)] public float vanishRatio = 0.45f;
    [Tooltip("고리 가장자리(안쪽/바깥)일수록 더 잘 뽑히는 정도 (0 = 완전 무작위)")] [Min(0)] public float edgeBias = 2f;
    [Header("용암 수위 주기")]
    [Tooltip("어느 쪽 수위에서 가장자리 플랫폼이 사라지는지")] public VanishAt vanishAt = VanishAt.Low;
    [Tooltip("높은 수위 유지(초)")] public float highHold = 16f;
    [Tooltip("낮은 수위 유지(초)")] public float lowHold = 12f;
    [Tooltip("수위가 바뀌는 데 걸리는 시간(초)")] public float transition = 4f;
    [Tooltip("사라지기 전에 플랫폼이 흔들리며 빛나는 경고 시간(초, 수위 변화 시작부터)")] public float warning = 2f;
    [Tooltip("경고 중 화면 흔들림 세기(m, 미세하게). 플랫폼이 가라앉기 시작하면 멈춤. 0 = 없음")] public float warningShake = 0.05f;
    [Tooltip("높은 수위 / 낮은 수위 (용암 표면 높이, 플랫폼 윗면 = 0)")] public float highLevel = -0.35f, lowLevel = -3f;
    [Header("용암")]
    [Tooltip("생명체가 용암에 빠지면 받는 피해 (최대 체력 비율). 이후 가까운 플랫폼으로 돌아옴")] [Range(0, 1)] public float wardLavaDamageFrac = 0.2f;
    [Tooltip("생명체가 돌아온 뒤 보호막(초)")] public float wardShield = 1f;
    [Header("색 (무광 파스텔 + 용암만 발광)")]
    public Color rockColor = new Color(0.33f, 0.27f, 0.28f);
    [Tooltip("가라앉는 플랫폼 (식은 껍질)")] public Color crustColor = new Color(0.45f, 0.3f, 0.27f);
    public Color lavaColor = new Color(1f, 0.45f, 0.15f);
    [Tooltip("용암 발광 세기")] public float lavaGlow = 1.6f;
}

/// <summary>도깨비불 늪 수치 (VariantConfig.wisp).</summary>
[System.Serializable]
public class WispSwampSettings
{
    [Header("도깨비불 (씬 빌더가 배치 — 이후 씬에서 옮기거나 늘려도 됨)")]
    [Tooltip("씬 빌더가 놓는 도깨비불 수")] public int wispCount = 9;
    [Tooltip("자기 자리에서 떠다니는 반경(m)")] public float wanderRadius = 7f;
    [Tooltip("떠다니는 속도(m/초)")] public float driftSpeed = 1.6f;
    [Tooltip("땅에서 뜬 높이(m)")] public float hoverHeight = 1.1f;
    [Tooltip("닿았다고 보는 거리(m, 수평)")] public float touchRadius = 1.3f;
    [Header("순간이동")]
    [Tooltip("작은 생명체가 닿으면 순간이동")] public bool affectWard = true;
    [Tooltip("적이 닿으면 순간이동")] public bool affectEnemies = true;
    [Tooltip("한 번 닿은 도깨비불이 꺼져 있다가 다시 켜지기까지(초)")] public float wispCooldown = 4f;
    [Tooltip("순간이동한 대상이 다시 순간이동되지 않는 시간(초)")] public float targetCooldown = 2f;
    [Tooltip("생명체 도착 지점: 적에게서 최소 이만큼 떨어진 곳을 우선(m)")] public float wardSafeDistance = 7f;
    [Tooltip("도착 지점: 도깨비불에서 최소 이만큼 떨어진 곳(m)")] public float wispClearance = 4f;
    [Header("순간이동 후 지속 피해 (도깨비불의 대가)")]
    [Tooltip("지속 피해 총량")] public float teleportDotTotal = 9f;
    [Tooltip("지속 피해 시간(초)")] public float teleportDotDuration = 3f;
    [Tooltip("작은 생명체에게 지속 피해")] public bool dotWard = true;
    [Tooltip("지속 피해를 받는 동안 회복 불가 (기본 회복·회복 구역 등 모든 회복 차단). 생명체에게 적용")] public bool noHealDuringDot = true;
    [Tooltip("적에게도 지속 피해 (이 피해로 죽으면 환경사: 처치 수에 안 침)")] public bool dotEnemies = false;
    [Header("생명체는 도깨비불을 피해 다님")]
    [Tooltip("생명체가 도깨비불 둘레 이 거리 안으로는 걸어 들어가지 않음(m, 닿는 거리보다 크게)")] public float wardAvoidRadius = 2.2f;
    [Header("색 (무광 파스텔 + 불빛만 발광)")]
    public Color wispColor = new Color(0.55f, 1f, 0.85f);
    [Tooltip("도깨비불 빛 세기 / 범위")] public float lightIntensity = 3.5f, lightRange = 8f;
    [Header("해 질 녘 (씬 빌더가 조명에 적용)")]
    [Tooltip("해 밝기 배율")] public float sunMul = 0.35f;
    public Color ambient = new Color(0.16f, 0.2f, 0.22f), fogColor = new Color(0.12f, 0.17f, 0.18f);
}

/// <summary>깨어난 묘지 수치 (VariantConfig.awakened).</summary>
[System.Serializable]
public class AwakenedGraveyardSettings
{
    [Header("클리어 조건")]
    [Tooltip("처치 목표 배율 (기존 대비)")] public float killTargetMul = 1.5f;
    [Tooltip("부숴야 하는 무덤 수 (N). 씬의 무덤 수보다 많으면 씬의 무덤 수")] public int gravesToBreak = 5;
    [Header("무덤 (씬 빌더가 배치 — 이후 씬에서 옮기거나 늘려도 됨)")]
    [Tooltip("씬 빌더가 놓는 무덤 수")] public int graveCount = 8;
    [Tooltip("무덤 체력 = 그 스테이지 적 체력 × 이 값")] public float graveHpMul = 15f;
    [Header("일어나는 적")]
    [Tooltip("무덤 하나에서 적이 일어나는 간격(초)")] public float riseInterval = 6f;
    [Tooltip("한 번에 일어나는 적 수 (최소, 최대)")] public Vector2Int risePerWave = new Vector2Int(1, 2);
    [Tooltip("무덤에서 이 반경 안에 일어남(m)")] public float riseRadius = 2.5f;
    [Tooltip("살아 있는 적이 이 수 이상이면 무덤에서 일어나지 않음")] public int riseCap = 70;
    [Tooltip("첫 기상까지 무덤마다 흩어지는 시간(초, 0~이 값)")] public float firstRiseJitter = 4f;
    [Tooltip("땅에서 솟아오르는 시간(초). 그동안 움직이지도 공격하지도 않음 (맞을 수는 있음)")] public float riseDuration = 0.8f;
    [Tooltip("솟아오르기 시작하는 깊이(m)")] public float riseDepth = 1.3f;
    [Header("무덤 노리기")]
    [Tooltip("생명체 사거리 안에 무덤이 있으면, 적이 더 가까워도 이 확률로 무덤을 노림 (무덤이 더 가까우면 항상 무덤)")] [Range(0, 1)] public float graveTargetShare = 0.5f;
    [Header("색 (무광 파스텔)")]
    public Color stoneColor = new Color(0.62f, 0.62f, 0.66f);
    public Color moundColor = new Color(0.36f, 0.3f, 0.27f);
    [Tooltip("적이 일어날 때 흙먼지 색")] public Color dustColor = new Color(0.55f, 0.47f, 0.4f);
}

/// <summary>달빛 묘역 수치 (VariantConfig.moonlit).</summary>
[System.Serializable]
public class MoonlitGraveyardSettings
{
    [Header("달빛 구역 (씬 빌더가 배치 — 이후 씬에서 옮기거나 늘려도 됨)")]
    [Tooltip("씬 빌더가 놓는 달빛 구역 수")] public int beamCount = 1;
    [Tooltip("달빛 구역 반경(m)")] public float radius = 7f;
    [Tooltip("옮겨 다니는 속도(m/초, 천천히)")] public float moveSpeed = 1.3f;
    [Tooltip("다음 목적지는 지금 자리에서 이만큼 이상 떨어진 곳(m)")] public float minHop = 18f;
    [Tooltip("목적지에 닿으면 머무는 시간(초)")] public float pause = 2f;
    [Header("효과")]
    [Tooltip("달빛 안에서 작은 생명체 초당 회복")] public float healPerSec = 4f;
    [Tooltip("생명체가 달빛 중심에서 이 비율(반경 대비) 안이면 멈춤")] [Range(0, 1)] public float stopFrac = 0.35f;
    [Tooltip("도망칠 때 달빛 쪽을 선호하는 정도 (도망 목표 후보가 달빛에서 1m 멀어질 때 감점)")] public float fleeBias = 0.1f;
    [Header("밤 (씬 빌더가 조명에 적용)")]
    [Tooltip("해 밝기 배율")] public float sunMul = 0.18f;
    public Color ambient = new Color(0.08f, 0.09f, 0.14f), fogColor = new Color(0.05f, 0.06f, 0.1f);
    [Tooltip("달빛 색 / 세기")] public Color moonColor = new Color(0.75f, 0.85f, 1f);
    public float moonIntensity = 400f;
}

/// <summary>발광 균사 동굴 수치 (VariantConfig.mycelium).</summary>
[System.Serializable]
public class MyceliumCaveSettings
{
    [Header("균사 (씬 빌더가 배치 — 이후 씬에서 옮기거나 늘려도 됨)")]
    [Tooltip("씬 빌더가 놓는 균사 덩어리 수")] public int patchCount = 42;
    [Tooltip("균사 덩어리 반경(m) — 생명체가 이 안을 밟으면 빛이 번짐")] public float patchRadius = 2.6f;
    [Header("빛 번짐")]
    [Tooltip("번지는 빛의 최대 반경(m)")] public float spreadRadius = 11f;
    [Tooltip("빛이 최대 반경까지 번지는 시간(초)")] public float spreadTime = 1.2f;
    [Tooltip("한 번 번진 균사가 다시 번질 수 있기까지(초)")] public float patchCooldown = 6f;
    [Header("적")]
    [Tooltip("잠든 적에게 생명체가 이 거리까지 닿으면 깨어남(m)")] public float bumpWakeRadius = 1.4f;
    [Tooltip("깨어난 적이 이 반경 안의 잠든 적도 깨움(m, 0 = 안 깨움)")] public float chainWakeRadius = 0f;
    [Header("클리어: 포자 결정 회수 (처치 수와 상관없음)")]
    [Tooltip("씬 빌더가 맵 곳곳에 놓는 포자 결정 수")] public int crystalCount = 7;
    [Tooltip("이만큼 회수하면 클리어 (포털). 씬의 결정 수보다 많으면 씬의 결정 수")] public int crystalsToCollect = 5;
    [Tooltip("생명체가 이 거리 안에 오면 회수(m)")] public float pickupRadius = 1.6f;
    [Header("적 분포")]
    [Tooltip("동시에 있는 적 최대 수")] public int maxEnemies = 45;
    [Tooltip("적은 생명체에게서 최소 이만큼 떨어진 곳에 생김(m)")] public float spawnMinWardDistance = 16f;
    [Tooltip("적이 생길 자리 후보 수 — 그중 다른 적과 가장 먼 곳에 생김 (클수록 고르게)")] public int spawnCandidates = 10;
    [Header("균사 빛")]
    [Tooltip("균사 하나의 평소 빛 세기 / 범위")] public float idleLight = 0.9f, idleRange = 4.5f;
    [Tooltip("밟았을 때 번지는 빛 세기")] public float burstLight = 6f;
    [Header("동굴 (씬 빌더가 조명에 적용)")]
    [Tooltip("해 밝기 배율")] public float sunMul = 0.1f;
    public Color ambient = new Color(0.06f, 0.06f, 0.1f), fogColor = new Color(0.03f, 0.03f, 0.06f);
    [Tooltip("균사 빛 색")] public Color glowColor = new Color(0.45f, 0.95f, 1f);
    [Tooltip("잠든 균사 색 (어둡게)")] public Color dimColor = new Color(0.25f, 0.45f, 0.55f);
}

/// <summary>상류의 댐 수치 (VariantConfig.dam).</summary>
[System.Serializable]
public class RiverDamSettings
{
    [Header("맵 (씬 빌더가 깖 — 세로로 긴 강바닥 발판, 위쪽 끝에 댐과 수문 장치)")]
    [Tooltip("발판 폭(m, 가로)")] public float deckWidth = 28f;
    [Tooltip("발판 길이(m, 세로: 아래 = 하류, 위 = 댐)")] public float deckLength = 100f;
    [Tooltip("발판 한 장 크기(m)")] public float slabSize = 7f;
    [Tooltip("수문 장치 수")] public int gateCount = 3;
    [Tooltip("수문 장치 줄: 위쪽 끝에서 이만큼 아래(m)")] public float gateInset = 10f;
    [Header("수문 장치")]
    [Tooltip("장치 체력 = 그 스테이지 적 체력 × 이 값")] public float gateHpMul = 40f;
    [Tooltip("장치에 붙은 적 하나가 주는 초당 피해")] public float gateDpsPerEnemy = 4f;
    [Tooltip("적이 장치에 붙었다고 보는 거리(m)")] public float gateReach = 2.6f;
    [Tooltip("수문이 열 수 있게 차오르는 시간(초) — 차오르면 감시자가 가까이 가서 상호작용(F)으로 엶")] public float gateCharge = 22f;
    [Tooltip("물살이 수문에서 아래 끝까지 내려가는 시간(초)")] public float floodTime = 2.2f;
    [Tooltip("물살 폭: 열린 수문 중심에서 좌우로(m, 0이면 발판 전체 폭)")] public float floodHalfWidth = 0f;
    [Header("적")]
    [Tooltip("적이 나타나는 곳: 아래쪽 끝에서 이 길이 안(m)")] public float spawnDepth = 22f;
    [Tooltip("생명체가 이 거리 안이면 적이 장치 대신 생명체를 노림(m)")] public float wardAggro = 5f;
    [Header("클리어")]
    [Tooltip("이 시간(초) 동안 수문 장치를 하나라도 지키면 + 처치 목표를 채우면 포털")] public float defendTime = 120f;
    [Tooltip("처치 목표 배율 (물살에 쓸려 간 적은 처치 수에 안 침)")] public float killTargetMul = 0.5f;
    [Header("색 (무광 파스텔)")]
    public Color deckColor = new Color(0.62f, 0.64f, 0.66f);
    public Color waterColor = new Color(0.35f, 0.6f, 0.78f);
    public Color wallColor = new Color(0.55f, 0.55f, 0.6f);
    public Color gateColor = new Color(0.55f, 0.42f, 0.32f);
    [Tooltip("수문이 다 차올랐을 때 표시 색")] public Color readyColor = new Color(0.5f, 0.9f, 1f);
}

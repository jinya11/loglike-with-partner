using System;
using UnityEngine;

public enum StatType
{
    Damage, MoveSpeed, AttackSpeed, MaxHp, Pickup, Cooldown, Heal,
    ProjectileCount, Area, CompanionDamage, CompanionRate, CompanionStrikes, Jump,
    WardMaxHp, WardHeal, WardPulseDamage, WardPulseRate,
    AttackRange, ProjectileSpeed, WardRegen, WardSpeed,
    BombardArea // 폭격 범위 배율 +value (레벨업 자동 보상)
}

/// <summary>레벨업 순환: 1번째 탄환 → 2번째 생명체 생존력 → 3번째 동반자.</summary>
public enum UpgradeCategory { Projectile, Ward, Companion }

[Serializable]
public class UpgradeDef
{
    public string name;
    public string desc;
    public StatType stat;
    public float value;
    [Tooltip("레벨 순환 분류: 1번째 탄환, 2번째 생명체 생존력, 3번째 동반자")]
    public UpgradeCategory category;
    [HideInInspector] public bool special; // 구버전 호환 (category로 대체)
    [Tooltip("등장 가중치 (기본 1, 낮을수록 희귀)")] public float weight = 1f;
}

[Serializable]
public class PlayerStats
{
    public float maxHp = 100f, moveSpeed = 6f, damage = 5f, attackInterval = 0.6f;
    public float projectileSpeed = 18f, pickupRadius = 3f, cooldownMul = 1f, areaMul = 1f;
    public int projectileCount = 1;
    public float attackRange = 14f, spreadAngle = 12f, hpRegen = 0.5f;
    [Tooltip("점프 가능 여부 (희귀 강화로 획득)")] public bool canJump;
    // 생명체/감시자 수치: 기본값은 WardConfig / CompanionConfig에서 런 시작 시 복사 (여기서 편집하지 않음)
    [HideInInspector] public float wardMaxHp = 60f, wardRegen = 0.4f, wardSpeed = 3.2f;
    [HideInInspector] public float wardPulseDamage = 3f, wardPulseRadius = 2.2f, wardPulseInterval = 6f;
    [HideInInspector] public float companionDamage = 15f, companionInterval = 6f, companionRadius = 3f;
    [HideInInspector] public int companionStrikes = 1;
    [Tooltip("폭격(R) 범위 배율 (레벨업 자동 보상으로 성장)")] public float bombardAreaMul = 1f;

    [NonSerialized] public float hp, wardHp;
    [NonSerialized] public int level = 1, xp;

    public PlayerStats Clone()
    {
        var c = (PlayerStats)MemberwiseClone();
        c.hp = c.maxHp; c.wardHp = c.wardMaxHp; c.level = 1; c.xp = 0;
        return c;
    }

    public void Apply(UpgradeDef u)
    {
        float v = u.value;
        switch (u.stat)
        {
            case StatType.Damage: damage += v; break;
            case StatType.MoveSpeed: moveSpeed += v; break;
            case StatType.AttackSpeed: attackInterval *= 1f - v; break;
            case StatType.MaxHp: maxHp += v; hp += v; break;
            case StatType.Pickup: pickupRadius += v; break;
            case StatType.Cooldown: cooldownMul *= 1f - v; break;
            case StatType.Heal: hp = Mathf.Min(maxHp, hp + v); break;
            case StatType.ProjectileCount: projectileCount += (int)v; break;
            case StatType.Area: areaMul += v; break;
            case StatType.CompanionDamage: companionDamage += v; break;
            case StatType.CompanionRate: companionInterval *= 1f - v; break;
            case StatType.CompanionStrikes: companionStrikes += (int)v; break;
            case StatType.Jump: canJump = true; break;
            case StatType.WardMaxHp: wardMaxHp += v; wardHp += v; break;
            case StatType.WardHeal: wardHp = Mathf.Min(wardMaxHp, wardHp + v); break;
            case StatType.WardPulseDamage: wardPulseDamage += v; wardPulseRadius += 0.4f; break;
            case StatType.WardPulseRate: wardPulseInterval *= 1f - v; break;
            case StatType.AttackRange: attackRange += v; break;
            case StatType.ProjectileSpeed: projectileSpeed += v; break;
            case StatType.WardRegen: wardRegen += v; break;
            case StatType.WardSpeed: wardSpeed += v; break;
            case StatType.BombardArea: bombardAreaMul += v; break;
        }
    }
}

/// <summary>근접 디버프형 적의 고유 능력 (원거리형 없음).</summary>
public enum EnemyAbility
{
    None,
    [Tooltip("주기적으로 생명체에게 돌진, 부딪히면 크게 넉백")] Charge,
    [Tooltip("닿으면 생명체 이동속도 감소")] Slow,
    [Tooltip("가까이 오면 생명체가 겁에 질려 잠시 못 움직임")] Fear,
    [Tooltip("닿으면 출혈(지속 피해) + 그동안 회복 불가")] Bleed,
}

[Serializable]
public class EnemyDef
{
    public string name;
    public Enemy prefab;
    [Tooltip("스폰 가중치")] public float weight = 1f;
    [Tooltip("이 스테이지 번호(0부터)부터 등장")] public int minStage;
    public float hpMul = 1f, speedMul = 1f, damageMul = 1f;
    public EnemyAbility ability;
    [Tooltip("Charge: 넉백 속도 / Slow: 이동속도 배율 / Fear: 사용 안 함 / Bleed: 초당 피해")]
    public float abilityValue;
    [Tooltip("디버프 지속 시간 (Charge는 돌진 시간)")] public float abilityDuration = 2f;
    [Tooltip("능력 재사용 대기")] public float abilityCooldown = 4f;
    [Tooltip("Charge: 돌진 시작 거리 / Fear: 공포 반경")] public float abilityRange = 6f;
}

/// <summary>스테이지(맵)마다 나오는 자원. 런이 끝나도 유지되며 거점 성장/감시자 해금에 사용.</summary>
public enum ResourceType { None, AncientStone, FrostCrystal, RiverPebble, MagmaStone, SugarCrystal }

[Serializable]
public class ResourceDef
{
    public ResourceType type;
    public string name;
    public Color color = Color.white;
}

[Serializable]
public class ResourceCost
{
    public ResourceType type;
    public int amount;
}

/// <summary>거점 성장 (영구 강화). 레벨당 stat에 valuePerLevel 적용.</summary>
[Serializable]
public class BaseUpgradeDef
{
    public string id;
    public string name;
    public string desc;
    public StatType stat;
    public float valuePerLevel;
    public int maxLevel = 5;
    [Tooltip("1레벨 비용. 레벨이 오를수록 costGrowth만큼 증가")] public ResourceCost cost = new ResourceCost();
    public int costGrowth = 2;

    public int CostAt(int level) => cost.amount + costGrowth * level;
}

public enum ModifierType { EnemyDamage, EnemyHp, EnemySpeed, MoreEnemies, NoHealing, Hazards }

/// <summary>난이도 석판 하나 = 디버프 하나. 여러 개 중첩 가능.</summary>
[Serializable]
public class ModifierDef
{
    public string name;
    public string desc;
    public ModifierType type;
    public float value;
}

[Serializable]
public class CompanionDef
{
    public string name;
    [Tooltip("짧은 설명 (로비 안내 문구용)")] public string desc;
    public GameObject prefab;
    [Tooltip("동반자 선택 화면 미리보기 크기 (prefab 기준 배율)")] public float previewScale = 0.14f;
    [Tooltip("동반자 선택 화면에 표시할 능력 설명")]
    [TextArea(3, 8)] public string abilityText;
    [Header("잠금 해제")]
    [Tooltip("비어 있으면 처음부터 사용 가능")] public ResourceCost[] unlockCost;
    [Header("폭격 스킬 (R)")]
    [Tooltip("폭격 쿨타임(초). 너무 짧지 않게")] public float bombardCooldown = 45f;
    [Header("필살기 (추후 컷씬)")]
    public string ultimateName;
    [TextArea(2, 5)] public string ultimateDesc;
    [Tooltip("추후 연결할 필살기 컷씬 (Timeline 등)")] public GameObject ultimateCutscene;
}

[Serializable]
public class PresetEntry
{
    public GameObject prefab;
    [Min(0)] public int minCount = 2, maxCount = 4;
    [Tooltip("아레나 반지름 대비 배치 구간 (0=중심, 1=외곽)")]
    [Range(0, 1)] public float minRadius = 0.25f, maxRadius = 0.9f;
    [Tooltip("다른 배치물과의 최소 간격")] public float spacing = 3f;
    public Vector2 scaleRange = Vector2.one;
    public bool randomRotation = true;
}

/// <summary>레벨업 자동 보상 하나 (증강 선택 대신 레벨업마다 랜덤 획득). PlayerConfig.autoRewards.</summary>
[Serializable]
public class LevelRewardDef
{
    public string name;
    [Tooltip("획득 알림 문구 (예: 공격력 +1.5)")] public string desc;
    public StatType stat;
    public float value;
    [Tooltip("등장 가중치")] public float weight = 1f;
    [Tooltip("최대 획득 횟수 (0 = 무제한)")] public int maxStacks;
    [Tooltip("하단 아이콘 색")] public Color color = Color.white;

    public LevelRewardDef() { }
    public LevelRewardDef(string name, string desc, StatType stat, float value, float weight, Color color, int maxStacks = 0)
    { this.name = name; this.desc = desc; this.stat = stat; this.value = value; this.weight = weight; this.color = color; this.maxStacks = maxStacks; }
}

/// <summary>스테이지 증강 큰 분류. 사건 = 스테이지에서 일어나는 일, 변이 = 적/땅/규칙이 바뀜.</summary>
public enum StageTraitCategory { Event, Mutation }

/// <summary>
/// 스테이지 증강 종류 (심연 외 스테이지, 포털 진입 전 카드로 선택). 누적형: 고른 증강은 런 동안 계속 적용되고 다시 나오지 않음.
/// value/value2의 뜻은 종류마다 다름 (툴팁 참고).
/// </summary>
public enum StageTraitType
{
    // ---- 사건 ----
    [Tooltip("적 밀도 ×(1+value), 경험치 +value2")] Horde,
    [Tooltip("엘리트 출현 확률 value, 엘리트 체력 배율 value2")] Elite,
    [Tooltip("제한 시간 value초, 시간 안에는 경험치 +value2, 넘으면 적 광폭화")] TimeLimit,
    [Tooltip("value초마다 유성 낙하 (반경 3, 적과 생명체 모두 피해 value2)")] MeteorShower,
    [Tooltip("value초마다 보급 상자: 경험치 보석 value2개")] SupplyDrop,
    [Tooltip("value초마다 습격 파도: 적 value2마리가 한꺼번에")] Waves,
    [Tooltip("value초마다 보물 운반자(도망치는 황금 적) 출현, 처치 시 보석 value2개 + 자원")] TreasureCarrier,
    [Tooltip("회복의 샘 value개 (초당 회복 value2)")] Sanctuary,
    [Tooltip("자원 드롭 확률 ×value")] Riches,
    [Tooltip("안개: 시야 거리 ×value, 경험치 +value2")] Fog,
    [Tooltip("처치 목표 ×value, 적 체력 +value2")] Blitz,
    // ---- 변이 ----
    [Tooltip("적 이동속도 +value, 경험치 +value2")] Swift,
    [Tooltip("적 체력 +value, 경험치 +value2")] IronSkin,
    [Tooltip("적이 죽을 때 value 확률로 작은 적 2마리로 분열 (체력 배율 value2)")] Split,
    [Tooltip("적이 죽을 때 폭발 (반경 value, 생명체 피해 value2)")] Volatile,
    [Tooltip("적이 거대화: 체력 ×value, 크기 1.35배, 속도 -15%, 경험치 +value2")] Giant,
    [Tooltip("적 접촉 피해 +value, 경험치 +value2")] Sharp,
    [Tooltip("적이 초당 최대 체력의 value 비율만큼 재생, 경험치 +value2")] Regen,
    [Tooltip("독 웅덩이 value개 (초당 피해 value2) + 위험 장판")] Toxic,
    [Tooltip("지형(Terrain) 높이 ×value")] Highlands,
    [Tooltip("진흙탕 value개 (생명체 이동속도 ×value2)")] Mire,
}

/// <summary>스테이지 증강 하나 (카드). StageConfig.stageTraits.</summary>
[Serializable]
public class StageTraitDef
{
    public string name;
    [TextArea(1, 3)] public string desc;
    public StageTraitCategory category;
    public StageTraitType type;
    public float value, value2;
    [Tooltip("선택지 등장 가중치")] public float weight = 1f;
    public Color color = Color.white;

    public static string CategoryName(StageTraitCategory c) => c == StageTraitCategory.Event ? "사건" : "변이";
    public static Color CategoryColor(StageTraitCategory c) => c == StageTraitCategory.Event ? new Color(1f, 0.78f, 0.35f) : new Color(0.8f, 0.55f, 1f);

    public StageTraitDef() { }
    public StageTraitDef(StageTraitCategory category, string name, string desc, StageTraitType type, float value, float value2, Color color, float weight = 1f)
    { this.category = category; this.name = name; this.desc = desc; this.type = type; this.value = value; this.value2 = value2; this.color = color; this.weight = weight; }
}

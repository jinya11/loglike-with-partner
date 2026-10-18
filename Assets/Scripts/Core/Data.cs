using System;
using UnityEngine;

public enum StatType
{
    Damage, MoveSpeed, AttackSpeed, MaxHp, Pickup, Cooldown, Heal,
    ProjectileCount, Area, CompanionDamage, CompanionRate, CompanionStrikes, Jump,
    WardMaxHp, WardHeal, WardPulseDamage, WardPulseRate,
    AttackRange, ProjectileSpeed, WardRegen, WardSpeed
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
    [Header("Ward (지켜야 할 작은 생명체)")]
    public float wardMaxHp = 60f, wardRegen = 0.4f, wardSpeed = 3.2f;
    [Tooltip("약한 저항: 주변 적에게 주기적으로 작은 피해와 넉백")]
    public float wardPulseDamage = 3f, wardPulseRadius = 2.2f, wardPulseInterval = 6f;
    [Header("Companion")]
    public float companionDamage = 15f, companionInterval = 6f, companionRadius = 3f;
    public int companionStrikes = 1;

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

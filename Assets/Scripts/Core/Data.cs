using System;
using UnityEngine;

public enum StatType
{
    Damage, MoveSpeed, AttackSpeed, MaxHp, Pickup, Cooldown, Heal,
    ProjectileCount, Area, CompanionDamage, CompanionRate, CompanionStrikes, Jump
}

[Serializable]
public class UpgradeDef
{
    public string name;
    public string desc;
    public StatType stat;
    public float value;
    [Tooltip("true면 3배수 레벨 전용 특수 강화")] public bool special;
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
    [Header("Companion")]
    public float companionDamage = 15f, companionInterval = 6f, companionRadius = 3f;
    public int companionStrikes = 1;

    [NonSerialized] public float hp;
    [NonSerialized] public int level = 1, xp;

    public PlayerStats Clone()
    {
        var c = (PlayerStats)MemberwiseClone();
        c.hp = c.maxHp; c.level = 1; c.xp = 0;
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
        }
    }
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
public class WeaponDef
{
    public string name;
    public string desc;
    public GameObject model;
    public float damageMul = 1f, intervalMul = 1f, speedMul = 1f, rangeMul = 1f, spreadAngle = 12f;
    public int bonusProjectiles;
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

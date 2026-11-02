using UnityEngine;

/// <summary>공격 증강 효과 종류. 생명체 공격(CreatureAttack)이 자기에게 해당하는 효과를 읽어 적용.</summary>
public enum AugmentEffect
{
    // 공통 수치
    ProjectileCount, // 투사체 수 +value
    Damage,          // 피해 +value (비율, 0.15 = 15%)
    AttackSpeed,     // 공격 간격 -value (비율)
    Range,           // 사거리 +value
    ProjectileSpeed, // 탄속 +value (비율)
    Area,            // 범위 +value (비율)
    // 공격 방식 변화
    Chain,           // 연쇄: 맞은 적에서 가까운 적으로 value번 튕김
    Pierce,          // 관통: 적 value마리를 뚫고 지나감
    Split,           // 분열: 명중/착지 시 작은 투사체 value개로 갈라짐
    Homing,          // 유도: 적을 따라감 (value = 회전 세기)
    Echo,            // 메아리: value 확률로 한 번 더 시전
    Burn,            // 화상: 초당 value 피해 (3초)
    Slow,            // 감속: 이동 속도 ×(1 - value) (2초)
    Knockback,       // 넉백 세기 +value
    Zone,            // 장판: 착지/명중 지점에 value초 남는 장판 (피해 또는 감속)
    Mark,            // 표식: 맞은 적이 받는 피해 +value (비율, 4초)
    Summon,          // 소환체 추가 공격: 동료(새 등)가 value번 더 공격
}

/// <summary>공격 증강 하나 (레벨업 선택지).</summary>
[System.Serializable]
public class AttackAugmentDef
{
    [Tooltip("고유 id (저장/중복 확인)")] public string id;
    public string name;
    [TextArea(1, 3)] public string desc;
    public AugmentEffect effect;
    public float value = 1f;
    [Tooltip("최대 중첩 횟수 (0 = 무제한)")] public int maxStacks = 3;
    [Tooltip("선택지로 나올 가중치")] public float weight = 1f;
    [Tooltip("이 생명체만 얻을 수 있음 (CreatureDef.name). 비우면 모든 생명체 공통")] public string[] creatures;

    public bool AllowedFor(string creatureName)
    {
        if (creatures == null || creatures.Length == 0) return true;
        foreach (var c in creatures) if (c == creatureName) return true;
        return false;
    }
}

/// <summary>
/// 생명체 공격 증강 데이터 테이블 (공통 + 생명체 전용). 인스펙터에서 행을 추가/수정.
/// 기본값은 메뉴 Roguelike/Setup Attack Augment Table로 채움 (WardConfig.attackAugments에 연결).
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Attack Augment Table")]
public class AttackAugmentTable : ScriptableObject
{
    public AttackAugmentDef[] augments;

    /// <summary>해당 생명체가 얻을 수 있는 증강 (공통 + 전용).</summary>
    public void CollectFor(string creatureName, System.Collections.Generic.List<AttackAugmentDef> result)
    {
        result.Clear();
        if (augments == null) return;
        foreach (var a in augments) if (a != null && a.AllowedFor(creatureName)) result.Add(a);
    }
}

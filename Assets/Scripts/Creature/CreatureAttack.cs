using UnityEngine;

/// <summary>
/// 작은 생명체의 기본 공격 (생명체마다 고유). CreatureDef.attack에 연결하면 런 시작 시 복제되어 사용.
/// 사거리/공격 간격/피해/탄 수/퍼짐/탄속은 PlayerStats(레벨업 강화)를 그대로 사용.
/// 비우면 BasicShotAttack(기본 탄환).
/// </summary>
public abstract class CreatureAttack : ScriptableObject
{
    [Tooltip("공격 이름 (UI/디버그 표시)")] public string displayName;
    [TextArea(1, 3)] public string description;

    protected CreatureContext Ctx { get; private set; }
    float timer;

    public void Bind(CreatureContext ctx) => Ctx = ctx;

    /// <summary>매 프레임 (게임 진행 중, 살아 있을 때만). 사거리 안에 적이 있으면 간격마다 Fire.</summary>
    public void Tick(float dt)
    {
        if ((timer -= dt) > 0f) return;
        var s = Ctx.Stats;
        var target = Ctx.Stage.NearestEnemy(Ctx.Position, s.attackRange);
        if (target == null) return;
        timer = s.attackInterval;
        Fire(target, s);
    }

    protected abstract void Fire(Enemy target, PlayerStats s);

    /// <summary>탄 수만큼 퍼짐 각도로 나눈 방향들 (기준 dir 중심).</summary>
    protected static Vector3 SpreadDir(Vector3 dir, PlayerStats s, int i)
    {
        float start = -s.spreadAngle * (s.projectileCount - 1) * 0.5f;
        return Quaternion.Euler(0f, start + s.spreadAngle * i, 0f) * dir;
    }
}

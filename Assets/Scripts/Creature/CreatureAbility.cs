using UnityEngine;

/// <summary>
/// 작은 생명체의 고유 능력 기본형. 능력마다 이 클래스를 상속한 ScriptableObject를 만들고
/// CreatureDef.ability에 연결. 런 시작 시 복제(Instantiate)되므로 필드에 런 상태를 저장해도 됨.
/// 필요한 이벤트만 재정의하면 됨 (기본은 아무 것도 안 함).
/// </summary>
public abstract class CreatureAbility : ScriptableObject
{
    [Tooltip("능력 이름 (UI/디버그 표시)")] public string displayName;
    [TextArea(2, 5)] public string description;

    /// <summary>능력의 주인 (복제 후 Bind로 설정).</summary>
    protected CreatureContext Ctx { get; private set; }

    public void Bind(CreatureContext ctx) => Ctx = ctx;

    /// <summary>스테이지 시작 (생명체 생성 직후).</summary>
    public virtual void OnStart() { }
    /// <summary>매 프레임 (게임 진행 중, 살아 있을 때만).</summary>
    public virtual void Tick(float dt) { }
    /// <summary>피해를 받기 직전. 바뀐 피해량을 반환 (0이면 무효).</summary>
    public virtual float ModifyDamage(float damage) => damage;
    /// <summary>피해를 받은 뒤.</summary>
    public virtual void OnDamaged(float damage) { }
    /// <summary>적 디버프가 걸리기 직전. false면 막음.</summary>
    public virtual bool AllowDebuff(EnemyAbility kind) => true;
    /// <summary>저항 파동이 나갔을 때.</summary>
    public virtual void OnPulse(Vector3 center, float radius) { }
    /// <summary>포위 구제가 발동했을 때.</summary>
    public virtual void OnRescue() { }
    /// <summary>보호막이 켜졌을 때.</summary>
    public virtual void OnShield(float seconds) { }
    /// <summary>체력이 0이 됐을 때. true를 반환하면 사망을 막음 (부활 등).</summary>
    public virtual bool OnBeforeDeath() => false;
    /// <summary>스테이지 종료/생명체 파괴.</summary>
    public virtual void OnEnd() { }
    /// <summary>디버그 창에 표시할 상태 한 줄 (없으면 null).</summary>
    public virtual string DebugStatus() => null;
}

/// <summary>
/// 쿨타임형 능력 기본형: 조건(ShouldActivate)이 맞고 쿨타임이 끝나면 Activate 호출.
/// </summary>
public abstract class CooldownCreatureAbility : CreatureAbility
{
    [Tooltip("재사용 대기 (초)")] public float cooldown = 10f;
    [Tooltip("시작 후 처음 쓸 수 있을 때까지 (초)")] public float initialDelay = 3f;

    protected float CooldownLeft { get; set; }
    public float Ready01 => cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(CooldownLeft / cooldown);

    public override void OnStart() => CooldownLeft = initialDelay;

    public override void Tick(float dt)
    {
        if (CooldownLeft > 0f) { CooldownLeft -= dt; return; }
        if (!ShouldActivate()) return;
        Activate();
        CooldownLeft = cooldown * Ctx.Stats.cooldownMul;
    }

    /// <summary>발동 조건 (기본: 항상).</summary>
    protected virtual bool ShouldActivate() => true;
    protected abstract void Activate();

    public override string DebugStatus() => CooldownLeft > 0f ? $"{displayName} 대기 {CooldownLeft:F1}s" : $"{displayName} 준비";
}

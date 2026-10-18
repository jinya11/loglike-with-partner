using UnityEngine;

/// <summary>
/// 근접 적. Rigidbody 속도로 이동 → 장애물/벽/다른 적과 충돌. 목표는 작은 생명체(Ward).
/// 종류(EnemyDef)에 따라 디버프 능력: 돌진 넉백 / 감속 / 공포 경직 / 출혈. 원거리 공격 없음.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Enemy : Ticked<Enemy>
{
    const float ContactSq = 1f, WardContactSq = 0.75f, ChargeContactSq = 1.2f;
    /// <summary>StageDirector가 설정 (GameConfig.enemyAggroRadius 제곱).</summary>
    public static float AggroSq = 12.25f;
    [SerializeField] Transform visual;
    Rigidbody rb;
    EnemyDef def;
    float hp, speed, damage, abilityReadyAt, chargeLeft;
    Vector3 knock, chargeDir;

    public EnemyDef Def => def;

    void Awake() { rb = GetComponent<Rigidbody>(); }

    public void Init(float hp, float speed, float damage, EnemyDef def = null)
    {
        this.def = def;
        float hm = def != null ? def.hpMul : 1f, sm = def != null ? def.speedMul : 1f, dm = def != null ? def.damageMul : 1f;
        this.hp = hp * hm; this.speed = speed * sm * Random.Range(0.85f, 1.15f); this.damage = damage * dm;
        knock = Vector3.zero;
        chargeLeft = 0f;
        abilityReadyAt = Time.time + Random.Range(0.5f, 2f); // 동시에 몰려서 쓰지 않도록
        rb.linearVelocity = Vector3.zero;
    }

    public override void Tick(float dt)
    {
        var player = PlayerController.I;
        var ward = Ward.I;
        Vector3 pos = rb.position;
        Vector3 to = player.Position - pos;
        to.y = 0f;
        float sq = to.sqrMagnitude;
        if (!player.Formless && sq < ContactSq && !player.Airborne) player.TakeDamage(damage);

        float moveSpeed = speed;
        if (ward != null && ward.Alive)
        {
            Vector3 tw = ward.Position - pos; tw.y = 0f;
            float wsq = tw.sqrMagnitude;
            if (player.Formless || sq > AggroSq) { to = tw; sq = wsq; } // 생명체를 노림
            moveSpeed = UseAbility(ward, tw, wsq, dt);
            if (wsq < WardContactSq)
            {
                ward.TakeDamage(damage);
                OnTouchWard(ward);
            }
        }

        Vector3 v = knock;
        if (chargeLeft > 0f) v += chargeDir * moveSpeed;
        else if (sq > 0.0001f)
        {
            Vector3 dir = to / Mathf.Sqrt(sq);
            v += dir * moveSpeed;
            visual.rotation = Quaternion.LookRotation(dir);
        }
        rb.linearVelocity = v;
        knock = Vector3.MoveTowards(knock, Vector3.zero, 30f * dt);
    }

    /// <summary>종류별 능력. 이번 프레임 이동 속도를 반환.</summary>
    float UseAbility(Ward ward, Vector3 toWard, float wsq, float dt)
    {
        if (def == null) return speed;
        switch (def.ability)
        {
            case EnemyAbility.Charge:
                if (chargeLeft > 0f)
                {
                    chargeLeft -= dt;
                    if (wsq < ChargeContactSq) // 돌진 중 충돌: 생명체를 크게 넉백
                    {
                        ward.Knockback(chargeDir * def.abilityValue);
                        ward.TakeDamage(damage);
                        chargeLeft = 0f;
                    }
                    return speed * 3.5f;
                }
                if (Time.time >= abilityReadyAt && wsq < def.abilityRange * def.abilityRange)
                {
                    abilityReadyAt = Time.time + def.abilityCooldown;
                    chargeLeft = def.abilityDuration;
                    chargeDir = toWard.normalized;
                    visual.rotation = Quaternion.LookRotation(chargeDir);
                }
                return chargeLeft > 0f ? speed * 3.5f : speed;

            case EnemyAbility.Fear: // 근접 오라 (투사체 아님)
                if (Time.time >= abilityReadyAt && wsq < def.abilityRange * def.abilityRange)
                {
                    abilityReadyAt = Time.time + def.abilityCooldown;
                    ward.Fear(def.abilityDuration);
                    float d = def.abilityRange * 2f;
                    StageDirector.I.SpawnFx(rb.position + Vector3.up * 0.05f, new Vector3(0.5f, 0.02f, 0.5f), new Vector3(d, 0.02f, d), 0.3f, new Color(0.6f, 0.3f, 0.9f));
                }
                return speed;
        }
        return speed;
    }

    /// <summary>생명체에 닿았을 때 디버프.</summary>
    void OnTouchWard(Ward ward)
    {
        if (def == null) return;
        switch (def.ability)
        {
            case EnemyAbility.Slow: ward.Slow(def.abilityValue, def.abilityDuration); break;
            case EnemyAbility.Bleed: ward.Bleed(def.abilityValue, def.abilityDuration); break;
        }
    }

    /// <summary>외부 힘으로 밀기 (동반자 돌풍 등). 감쇠되며 사라짐.</summary>
    public void Push(Vector3 velocity) { knock = velocity; chargeLeft = 0f; }

    public void Hit(float dmg, Vector3 from)
    {
        hp -= dmg;
        Vector3 k = rb.position - from; k.y = 0f;
        knock = k.normalized * 8f;
        if (hp <= 0f)
        {
            StageDirector.I.OnEnemyKilled(rb.position);
            Despawn();
        }
    }
}

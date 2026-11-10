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
    /// <summary>포털이 열린 뒤: 남은 적은 플레이어에게서 도망치고 무적 (StageDirector가 설정, 스테이지마다 초기화).</summary>
    public static bool Routed;
    public static float RoutSpeedMul = 1.25f;
    /// <summary>전체 이동속도 배율 (제한 시간 초과 광폭화 등).</summary>
    public static float SpeedMul = 1f;
    static readonly Color ImmuneColor = new Color(0.75f, 0.75f, 0.8f);
    [SerializeField] Transform visual;
    Rigidbody rb;
    EnemyDef def;
    float hp, speed, damage, abilityReadyAt, chargeLeft, punch, scaleMul = 1f;
    Vector3 knock, chargeDir, visualScale;
    Color bodyColor = Color.white;
    bool elite;

    public EnemyDef Def => def;
    public bool Elite => elite;
    Vector3 HitPoint => rb.position + Vector3.up * 0.4f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        visualScale = visual.localScale;
        var r = visual.GetComponentInChildren<Renderer>();
        if (r && r.sharedMaterial && r.sharedMaterial.HasColor("_BaseColor")) bodyColor = r.sharedMaterial.GetColor("_BaseColor");
    }

    public void Init(float hp, float speed, float damage, EnemyDef def = null)
    {
        this.def = def;
        float hm = def != null ? def.hpMul : 1f, sm = def != null ? def.speedMul : 1f, dm = def != null ? def.damageMul : 1f;
        this.hp = hp * hm; this.speed = speed * sm * Random.Range(0.85f, 1.15f); this.damage = damage * dm;
        knock = Vector3.zero;
        chargeLeft = 0f; punch = 0f;
        elite = false; scaleMul = 1f;
        visual.localScale = visualScale;
        abilityReadyAt = Time.time + Random.Range(0.5f, 2f); // 동시에 몰려서 쓰지 않도록
        rb.linearVelocity = Vector3.zero;
    }

    /// <summary>엘리트 (스테이지 특징): 체력/피해 증가, 크게.</summary>
    public void MakeElite(float hpMul, float damageMul, float scale)
    {
        elite = true;
        hp *= hpMul; damage *= damageMul; scaleMul = scale;
        visual.localScale = visualScale * scale;
        Vfx.Pillar(rb.position, new Color(1f, 0.75f, 0.25f));
    }

    /// <summary>포털이 열려 도망치기 시작할 때.</summary>
    public void OnRouted() { chargeLeft = 0f; }

    /// <summary>지면 높이 따라가기 (고저차). Y 위치는 물리 대신 직접 맞춤 (Rigidbody Y 고정 유지).</summary>
    void FollowGround()
    {
        if (!Ground.HasHills) return;
        Vector3 p = rb.position;
        float gy = Ground.HeightAt(p.x, p.z);
        if (Mathf.Abs(gy - p.y) > 0.005f) { p.y = gy; rb.position = p; transform.position = p; }
    }

    /// <summary>절벽은 못 오름: 앞쪽(0.4m)이 절벽 위면 벽을 따라 미끄러지는 속도로 바꿈. 내려가는 건 가능.</summary>
    static Vector3 Climb(Vector3 pos, Vector3 v, float dt)
    {
        if (!Ground.HasHills) return v;
        v.y = 0f;
        float m = v.magnitude;
        if (m < 0.001f) return v;
        Vector3 probe = v * (0.4f / m);
        Vector3 ok = Ground.ClampMove(pos, probe);
        if (ok == probe) return v;
        return ok * (m / 0.4f);
    }

    /// <summary>도망: 플레이어(조준점)에게서 멀어짐. 접촉 피해/능력 없음.</summary>
    void TickRouted(float dt)
    {
        Vector3 pos = rb.position;
        Vector3 away = pos - PlayerController.I.Position; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
        away.y = 0f;
        // 경계에 몰리면 경계를 따라 미끄러지도록 중심 쪽 성분을 섞음
        if (Arena.EdgeDistance(pos) < 4f) { Vector3 c = Arena.Center - pos; c.y = 0f; away = away.normalized + c.normalized * 0.8f; }
        Vector3 dir = away.normalized;
        rb.linearVelocity = Climb(pos, knock + dir * speed * RoutSpeedMul, dt);
        visual.rotation = Quaternion.LookRotation(dir);
        knock = Vector3.MoveTowards(knock, Vector3.zero, 30f * dt);
        FollowGround();
    }

    public override void Tick(float dt)
    {
        if (Routed) { TickRouted(dt); return; }
        var player = PlayerController.I;
        var ward = Ward.I;
        Vector3 pos = rb.position;
        Vector3 to = player.Position - pos;
        to.y = 0f;
        float sq = to.sqrMagnitude;
        if (!player.Formless && sq < ContactSq && !player.Airborne) player.TakeDamage(damage);

        float moveSpeed = speed * SpeedMul;
        if (ward != null && ward.Alive)
        {
            Vector3 tw = ward.Position - pos; tw.y = 0f;
            float wsq = tw.sqrMagnitude;
            if (player.Formless || sq > AggroSq) { to = tw; sq = wsq; } // 생명체를 노림
            moveSpeed = UseAbility(ward, tw, wsq, dt) * SpeedMul;
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
            // 고저차: 절벽을 돌아 경사로로 오르도록 흐름장을 따라감 (가까우면 직진)
            if (Ground.HasHills && sq > 9f && StageDirector.I.Nav != null && StageDirector.I.Nav.FlowDir(pos, out var fd)) dir = fd;
            v += dir * moveSpeed;
            visual.rotation = Quaternion.LookRotation(dir);
        }
        rb.linearVelocity = Climb(pos, v, dt);
        knock = Vector3.MoveTowards(knock, Vector3.zero, 30f * dt);
        if (punch > 0f) // 피격 시 부풀었다 돌아옴
        {
            punch = Mathf.Max(0f, punch - dt);
            visual.localScale = visualScale * (scaleMul * (1f + punch * 2.5f));
        }
        FollowGround();
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
                        Vfx.Debuff(ward.Position, EnemyAbility.Charge);
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
                    Vfx.Hit(HitPoint, new Color(1f, 0.6f, 0.2f)); // 돌진 개시 신호
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
        if (Routed) { Vfx.Hit(HitPoint, ImmuneColor); return; } // 포털이 열린 뒤에는 처치 불가
        hp -= dmg;
        Vector3 k = rb.position - from; k.y = 0f;
        knock = k.normalized * 8f;
        punch = 0.12f;
        if (hp <= 0f)
        {
            Vfx.Death(rb.position, bodyColor);
            StageDirector.I.OnEnemyKilled(rb.position, elite);
            Despawn();
        }
        else Vfx.Hit(HitPoint, bodyColor);
    }
}

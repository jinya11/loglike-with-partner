using UnityEngine;

/// <summary>
/// 지켜야 할 작은 생명체. 스테이지를 자유롭게 돌아다니고(배회), 적이 가까우면 반대쪽으로 도망.
/// 포털이 열리면 포털로 걸어가며, 도착하면 다음 스테이지로. 바닥 구역(눈밭/물/용암/회복)의 영향을 받음.
/// 미약한 저항: 주기적으로 주변 적에게 작은 피해 + 넉백. 체력이 0이 되면 패배.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Ward : Singleton<Ward>
{
    [SerializeField] Transform body, eye;
    [SerializeField] Renderer[] tintRenderers;
    [Header("배회 / 도망")]
    [SerializeField] float wanderSpeedMul = 0.5f, wanderRadius = 0.85f, wanderPauseMin = 0.5f, wanderPauseMax = 2.5f;
    [Tooltip("이 거리 안에 적이 있으면 도망")]
    [SerializeField] float fleeRadius = 5f;
    [SerializeField] float portalReach = 1.5f;
    [Header("길찾기 (벽/장애물 회피 + 적 회피 최단 경로)")]
    [Tooltip("도망칠 때 목표 지점까지 거리")]
    [SerializeField] float fleeDistance = 10f;
    [Tooltip("적 주변 위험 반경과 비용 (클수록 적을 더 크게 돌아감)")]
    [SerializeField] float dangerRadius = 4f, dangerWeight = 12f;
    [SerializeField] float replanInterval = 0.35f, waypointReach = 0.7f;
    [Header("포위 구제 (사방이 막히면 자동 발동)")]
    [Tooltip("이 반경 안의 적으로 포위 여부 판단")]
    [SerializeField] float surroundRadius = 1.8f;
    [Tooltip("포위 판정: 최소 적 수, 막힌 방향 수(4방향 중)")]
    [SerializeField] int surroundCount = 4, surroundSides = 3;
    [Tooltip("이 시간 동안 포위가 유지되면 발동")]
    [SerializeField] float surroundDelay = 0.6f;
    [SerializeField] float rescueCooldown = 12f, rescueRadius = 4.5f, rescuePush = 28f, rescueShield = 1.5f;
    [Header("표현")]
    [SerializeField] Color normalColor = new Color(0.75f, 1f, 0.7f), hurtColor = new Color(1f, 0.35f, 0.35f), shieldColor = new Color(0.55f, 0.9f, 1f);
    [Tooltip("디버프 색: 공포 / 출혈 / 감속")]
    [SerializeField] Color fearColor = new Color(0.7f, 0.45f, 1f), bleedColor = new Color(0.75f, 0.15f, 0.2f), slowColor = new Color(0.45f, 0.65f, 1f);

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color PulseColor = new Color(0.6f, 1f, 0.6f), WaterColor = new Color(0.55f, 0.85f, 1f);
    const float HitIFrame = 0.5f;

    CharacterController cc;
    PlayerStats stats;
    MaterialPropertyBlock mpb;
    Vector3 wanderTarget, heading = Vector3.forward;
    float pulseTimer, iframe, hurtFlash, shieldLeft, pauseLeft, splashTimer, surroundedFor, rescueReadyAt;
    bool inWater, reachedPortal;
    readonly System.Collections.Generic.List<Vector3> path = new System.Collections.Generic.List<Vector3>(128);
    int pathIndex;
    float replanLeft;
    Vector3 lastGoal;
    // 디버프
    float slowMul = 1f, slowLeft, fearLeft, bleedDps, bleedLeft;
    Vector3 knockVel;
    Color currentTint;

    public Vector3 Position => transform.position;
    /// <summary>1인칭(생명체 시점) 카메라 위치.</summary>
    public Transform Eye => eye;
    /// <summary>생명체가 바라보는 방향 (1인칭 시점용).</summary>
    public Vector3 Heading => heading;
    public bool Alive => stats != null && stats.wardHp > 0f;
    public bool Shielded => shieldLeft > 0f;
    public bool Feared => fearLeft > 0f;
    public bool Slowed => slowLeft > 0f;
    public bool Bleeding => bleedLeft > 0f;

    protected override void Awake()
    {
        base.Awake();
        cc = GetComponent<CharacterController>();
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        stats = GameManager.I.Stats;
        if (stats.wardHp <= 0f) stats.wardHp = stats.wardMaxHp;
        wanderTarget = transform.position;
        SetTint(normalColor);
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing || !Alive) return; // 죽은 뒤에는 아무것도 하지 않음
        float dt = Time.deltaTime;
        var stage = StageDirector.I;
        Vector3 pos = transform.position;

        float zoneMul = ApplyZones(dt);

        // 목표 결정: 포털 귀환 > 도망 > 배회
        bool fleeing = false;
        Vector3 goal;
        float speedMul = 1f;
        if (stage.PortalOpen)
        {
            Vector3 tp = stage.PortalPosition - pos; tp.y = 0f;
            if (tp.sqrMagnitude < portalReach * portalReach)
            {
                if (!reachedPortal) { reachedPortal = true; GameManager.I.NextStage(); }
                return;
            }
            goal = stage.PortalPosition;
        }
        else if (Threat(pos, out Vector3 away))
        {
            fleeing = true;
            goal = Arena.Clamp(pos + away * fleeDistance);
        }
        else
        {
            goal = wanderTarget;
            speedMul = wanderSpeedMul;
            Vector3 tw = wanderTarget - pos; tw.y = 0f;
            if (tw.sqrMagnitude < 0.5f)
            {
                speedMul = 0f;
                if ((pauseLeft -= dt) <= 0f) PickWanderTarget(stage);
            }
        }

        // 경로 재계산: 적을 피하면서 가장 빠른 길 (주기적으로, 또는 목표가 크게 바뀌면)
        var nav = stage.Nav;
        if (nav != null && ((replanLeft -= dt) <= 0f || (goal - lastGoal).sqrMagnitude > 4f))
        {
            replanLeft = replanInterval;
            lastGoal = goal;
            nav.UpdateDanger(Enemy.Active, dangerRadius, dangerWeight);
            if (!nav.FindPath(pos, goal, path) && !fleeing && !stage.PortalOpen) PickWanderTarget(stage);
            pathIndex = 0;
        }

        if (fearLeft > 0f) speedMul = 0f;           // 공포: 제자리에서 굳음
        if (slowLeft > 0f) speedMul *= slowMul;     // 감속

        Vector3 dir = Vector3.zero;
        if (speedMul > 0f)
        {
            Vector3 wp = goal;
            if (nav != null)
            {
                while (pathIndex < path.Count - 1 && Flat(path[pathIndex] - pos).sqrMagnitude < waypointReach * waypointReach) pathIndex++;
                if (pathIndex < path.Count) wp = path[pathIndex];
            }
            dir = Flat(wp - pos);
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
        }

        bool knocked = knockVel.sqrMagnitude > 0.01f;
        if (dir.sqrMagnitude > 0f || knocked)
        {
            if (dir.sqrMagnitude > 0f) heading = dir;
            float speed = stats.wardSpeed * zoneMul * speedMul;
            Vector3 delta = Arena.Clamp(pos + (dir * speed + knockVel) * dt) - pos;
            delta.y = -pos.y;
            Vector3 before = transform.position;
            cc.Move(delta);
            // 막혀서 못 움직이면 바로 경로 재계산
            if (!knocked && (transform.position - before).sqrMagnitude < delta.sqrMagnitude * 0.1f) replanLeft = 0f;
            if (dir.sqrMagnitude > 0f) body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(dir), 540f * dt);
            if (knocked) { knockVel = Vector3.MoveTowards(knockVel, Vector3.zero, 25f * dt); replanLeft = 0f; }
        }
        TickDebuffs(dt);
        CheckSurrounded(dt, stage);
        Regen(dt, stage);
        if (iframe > 0f) iframe -= dt;
        if (shieldLeft > 0f) shieldLeft -= dt;
        if (hurtFlash > 0f) hurtFlash -= dt;
        UpdateTint();

        // 미약한 저항
        if ((pulseTimer += dt) >= stats.wardPulseInterval && stage.NearestEnemy(transform.position, stats.wardPulseRadius) != null)
        {
            pulseTimer = 0f;
            Vector3 c = transform.position;
            float r = stats.wardPulseRadius, d = r * 2f;
            stage.DamageArea(c, r, stats.wardPulseDamage);
            stage.SpawnFx(c + Vector3.up * 0.05f, new Vector3(0.4f, 0.02f, 0.4f), new Vector3(d, 0.02f, d), 0.3f, PulseColor);
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    /// <summary>
    /// 구제 조치: 사방(4방향 중 surroundSides 이상)이 적에게 막힌 상태가 surroundDelay 이상 지속되면
    /// 주변 적을 크게 날려버리고 잠시 보호막. 쿨타임 rescueCooldown.
    /// </summary>
    void CheckSurrounded(float dt, StageDirector stage)
    {
        Vector3 p = transform.position;
        float r2 = surroundRadius * surroundRadius;
        int count = 0, sides = 0;
        var list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 d = list[i].transform.position - p; d.y = 0f;
            if (d.sqrMagnitude > r2) continue;
            count++;
            sides |= Mathf.Abs(d.x) > Mathf.Abs(d.z) ? (d.x > 0f ? 1 : 2) : (d.z > 0f ? 4 : 8);
        }
        int sideCount = (sides & 1) + ((sides >> 1) & 1) + ((sides >> 2) & 1) + ((sides >> 3) & 1);
        bool surrounded = count >= surroundCount && sideCount >= surroundSides;
        surroundedFor = surrounded ? surroundedFor + dt : 0f;
        if (surroundedFor < surroundDelay || Time.time < rescueReadyAt) return;

        surroundedFor = 0f;
        rescueReadyAt = Time.time + rescueCooldown;
        stage.RadialForce(p, rescueRadius, rescuePush, stats.wardPulseDamage);
        Shield(rescueShield);
        float d2 = rescueRadius * 2f;
        stage.SpawnFx(p + Vector3.up * 0.05f, new Vector3(0.5f, 0.02f, 0.5f), new Vector3(d2, 0.02f, d2), 0.35f, PulseColor);
        replanLeft = 0f; // 열린 틈으로 바로 길찾기
    }

    void PickWanderTarget(StageDirector stage)
    {
        Vector2 r = Random.insideUnitCircle * (stage.ArenaRadius * wanderRadius);
        var p = new Vector3(r.x, 0f, r.y);
        wanderTarget = stage.Nav != null ? stage.Nav.NearestFree(p) : p;
        pauseLeft = Random.Range(wanderPauseMin, wanderPauseMax);
        replanLeft = 0f;
    }

    /// <summary>fleeRadius 안의 적들로부터 멀어지는 방향.</summary>
    bool Threat(Vector3 pos, out Vector3 away)
    {
        away = Vector3.zero;
        var list = Enemy.Active;
        float fr = fleeRadius * fleeRadius;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 d = pos - list[i].transform.position; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < fr && sq > 0.0001f) away += d / sq; // 가까울수록 강하게
        }
        if (away.sqrMagnitude < 0.0001f) return false;
        away.Normalize();
        // 벽 근처면 벽을 따라 미끄러지도록 안쪽 성분 보정
        Vector3 inward = -pos; inward.y = 0f;
        if (pos.sqrMagnitude > Arena.Radius * Arena.Radius * 0.7f) away = (away + inward.normalized * 0.6f).normalized;
        return true;
    }

    void Regen(float dt, StageDirector stage)
    {
        if (bleedLeft > 0f) return; // 출혈 중 회복 불가
        float regen = GameManager.I.Has(ModifierType.NoHealing) ? 0f : stats.wardRegen;
        if (stage.Theme) regen += stage.Theme.ambientRegen; // 보너스 스테이지 (고갈 석판 무시)
        if (regen > 0f) stats.wardHp = Mathf.Min(stats.wardMaxHp, stats.wardHp + regen * dt);
    }

    /// <summary>바닥 구역 효과 (눈밭/물 감속, 용암 피해, 회복). 이동속도 배율 반환.</summary>
    float ApplyZones(float dt)
    {
        float mul = 1f;
        bool water = false;
        Vector3 p = transform.position;
        var zones = StageZone.All;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (!z.Contains(p)) continue;
            switch (z.type)
            {
                case ZoneType.Slow: mul = Mathf.Min(mul, z.value); break;
                case ZoneType.Water: mul = Mathf.Min(mul, z.value); water = true; break;
                case ZoneType.Damage: Drain(z.value * dt); break;
                case ZoneType.Heal: stats.wardHp = Mathf.Min(stats.wardMaxHp, stats.wardHp + z.value * dt); break;
            }
        }
        if (water && (!inWater || (splashTimer -= dt) <= 0f))
        {
            splashTimer = 0.3f;
            StageDirector.I.SpawnFx(new Vector3(p.x, 0.05f, p.z), new Vector3(0.4f, 0.02f, 0.4f), new Vector3(inWater ? 1.4f : 2.6f, 0.02f, inWater ? 1.4f : 2.6f), 0.35f, WaterColor);
        }
        inWater = water;
        return mul;
    }

    void Drain(float amount)
    {
        if (Shielded || !Alive) return;
        stats.wardHp -= amount;
        if (stats.wardHp <= 0f) Die();
    }

    /// <summary>플레이어 E: 일정 시간 모든 피해 무시.</summary>
    public void Shield(float seconds)
    {
        shieldLeft = Mathf.Max(shieldLeft, seconds);
        fearLeft = 0f; slowLeft = 0f; bleedLeft = 0f; // 보호막은 디버프도 해제
        float d = 1.6f;
        StageDirector.I.SpawnFx(transform.position + Vector3.up * 0.4f, new Vector3(d, d, d) * 0.3f, new Vector3(d, d, d), 0.25f, shieldColor);
    }

    public void TakeDamage(float dmg)
    {
        if (iframe > 0f || Shielded || !Alive || GameManager.I.State != GameState.Playing) return;
        stats.wardHp -= dmg;
        iframe = HitIFrame;
        hurtFlash = 0.15f;
        if (stats.wardHp <= 0f) Die();
    }

    // ---- 디버프 (보호막 중에는 무시) ----
    public void Knockback(Vector3 velocity) { if (!Shielded) knockVel = velocity; }
    public void Slow(float mul, float seconds) { if (Shielded) return; slowMul = Mathf.Min(slowLeft > 0f ? slowMul : 1f, mul); slowLeft = Mathf.Max(slowLeft, seconds); }
    public void Fear(float seconds) { if (!Shielded) fearLeft = Mathf.Max(fearLeft, seconds); }
    public void Bleed(float dps, float seconds) { if (Shielded) return; bleedDps = Mathf.Max(bleedLeft > 0f ? bleedDps : 0f, dps); bleedLeft = Mathf.Max(bleedLeft, seconds); }

    void TickDebuffs(float dt)
    {
        if (slowLeft > 0f && (slowLeft -= dt) <= 0f) slowMul = 1f;
        if (fearLeft > 0f) fearLeft -= dt;
        if (bleedLeft > 0f) { bleedLeft -= dt; Drain(bleedDps * dt); }
    }

    void UpdateTint()
    {
        Color c = hurtFlash > 0f ? hurtColor : Shielded ? shieldColor : Feared ? fearColor : Bleeding ? bleedColor : Slowed ? slowColor : normalColor;
        if (c != currentTint) SetTint(c);
    }

    void Die()
    {
        stats.wardHp = 0f;
        GameManager.I.GameOver(true);
    }

    void SetTint(Color c)
    {
        if (tintRenderers == null) return;
        currentTint = c;
        mpb.SetColor(ColorId, c);
        foreach (var r in tintRenderers) r.SetPropertyBlock(mpb);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>WASD 이동, Space 점프, 자동 공격, Q=충격파, E=대시. 로비(StageDirector 없음)에서는 이동/점프/대시만.</summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : Singleton<PlayerController>
{
    [SerializeField] Transform body, hand;
    [SerializeField] float interactRange = 2.6f;
    [Header("Jump")]
    [SerializeField] float jumpSpeed = 10f, gravity = 30f;
    [Header("Q - 충격파")]
    [SerializeField] float qCooldown = 6f, qRadius = 4.5f, qDamageMul = 3f;
    [Header("E - 대시")]
    [SerializeField] float eCooldown = 3f, dashSpeed = 28f, dashTime = 0.18f;

    static readonly Color QColor = new Color(0.4f, 0.8f, 1f);

    CharacterController cc;
    PlayerStats stats;
    StageDirector stage;
    GameObject weaponModel;
    Vector3 facing = Vector3.forward, dashDir;
    float attackTimer, qReady, eReady, dashLeft, iframe, vy, splashTimer;
    bool inWater;
    static readonly Color WaterColor = new Color(0.55f, 0.85f, 1f);

    public Vector3 Position => transform.position;
    /// <summary>현재 상호작용 가능한 가장 가까운 대상 (없으면 null).</summary>
    public Interactable Focus { get; private set; }
    public bool Airborne => transform.position.y > 0.8f;
    public float QLeft => Mathf.Max(0f, qReady - Time.time);
    public float ELeft => Mathf.Max(0f, eReady - Time.time);

    protected override void Awake()
    {
        base.Awake();
        cc = GetComponent<CharacterController>();
    }

    void Start()
    {
        stats = GameManager.I.Stats;
        stage = StageDirector.I;
        RefreshWeapon();
    }

    /// <summary>선택된 무기 모델을 손에 장착 (로비에서 변경 시 호출).</summary>
    public void RefreshWeapon()
    {
        stats = GameManager.I.Stats;
        if (weaponModel) Destroy(weaponModel);
        var w = GameManager.I.Weapon;
        if (w != null && w.model) weaponModel = Instantiate(w.model, hand, false);
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        var kb = GameManager.I.InputBlocked ? null : Keyboard.current;

        Vector3 move = Vector3.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed) move.z += 1f;
            if (kb.sKey.isPressed) move.z -= 1f;
            if (kb.dKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed) move.x -= 1f;
        }
        if (move.sqrMagnitude > 0f) { move.Normalize(); facing = move; }

        Vector3 pos = transform.position;
        bool grounded = (pos.y <= 0.001f || cc.isGrounded) && vy <= 0f;
        if (grounded && stats.canJump && kb != null && kb.spaceKey.wasPressedThisFrame) vy = jumpSpeed;
        else if (grounded) vy = 0f;
        else vy -= gravity * dt;

        Vector3 vel;
        if (dashLeft > 0f) { dashLeft -= dt; vel = dashDir * dashSpeed; }
        else vel = move * (stats.moveSpeed * ApplyZones(dt, move.sqrMagnitude > 0f));

        Vector3 delta = Arena.Clamp(pos + vel * dt) - pos;
        delta.y = Mathf.Max(-pos.y, vy * dt);
        cc.Move(delta);
        body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(facing), 900f * dt);

        if (iframe > 0f) iframe -= dt;
        float regen = GameManager.I.Has(ModifierType.NoHealing) ? 0f : stats.hpRegen;
        if (stage != null && stage.Theme) regen += stage.Theme.ambientRegen;
        if (regen > 0f) stats.hp = Mathf.Min(stats.maxHp, stats.hp + regen * dt);

        if (kb != null && kb.eKey.wasPressedThisFrame && Time.time >= eReady) CastE();
        UpdateFocus();
        if (Focus != null && kb != null && kb.fKey.wasPressedThisFrame) Focus.Interact();
        if (stage == null) return; // 로비

        AutoAttack(dt);
        if (kb != null && kb.qKey.wasPressedThisFrame && Time.time >= qReady) CastQ();
    }

    /// <summary>바닥 구역 효과 적용, 이동속도 배율 반환.</summary>
    float ApplyZones(float dt, bool moving)
    {
        float mul = 1f;
        bool water = false;
        Vector3 p = transform.position;
        var zones = StageZone.All;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (Airborne || !z.Contains(p)) continue;
            switch (z.type)
            {
                case ZoneType.Slow: mul = Mathf.Min(mul, z.value); break;
                case ZoneType.Water: mul = Mathf.Min(mul, z.value); water = true; break;
                case ZoneType.Damage: Drain(z.value * dt); break;
                case ZoneType.Heal: stats.hp = Mathf.Min(stats.maxHp, stats.hp + z.value * dt); break;
            }
        }
        // 물에 닿는 순간 + 물속 이동 중 주기적으로 물보라
        if (water && stage != null && ((!inWater) || (moving && (splashTimer -= dt) <= 0f)))
        {
            splashTimer = 0.25f;
            stage.SpawnFx(new Vector3(p.x, 0.05f, p.z), new Vector3(0.6f, 0.02f, 0.6f), new Vector3(inWater ? 2.2f : 4f, 0.02f, inWater ? 2.2f : 4f), 0.35f, WaterColor);
        }
        inWater = water;
        return mul;
    }

    void Drain(float amount)
    {
        stats.hp -= amount;
        if (stats.hp <= 0f) { stats.hp = 0f; GameManager.I.GameOver(); }
    }

    void UpdateFocus()
    {
        Vector3 p = transform.position;
        float best = interactRange * interactRange;
        Interactable f = null;
        var all = Interactable.All;
        for (int i = 0; i < all.Count; i++)
        {
            Vector3 d = all[i].transform.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < best) { best = sq; f = all[i]; }
        }
        Focus = f;
    }

    void AutoAttack(float dt)
    {
        if ((attackTimer -= dt) > 0f) return;
        var target = stage.NearestEnemy(transform.position, stats.attackRange);
        if (target == null) return;
        attackTimer = stats.attackInterval;

        Vector3 origin = transform.position + Vector3.up * 0.6f;
        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0f; dir.Normalize();
        int n = stats.projectileCount;
        float spread = stats.spreadAngle, start = -spread * (n - 1) * 0.5f;
        for (int i = 0; i < n; i++)
            stage.FireProjectile(origin, Quaternion.Euler(0f, start + spread * i, 0f) * dir, stats.projectileSpeed, stats.damage);
    }

    void CastQ()
    {
        qReady = Time.time + qCooldown * stats.cooldownMul;
        float r = qRadius * stats.areaMul;
        Vector3 p = transform.position; p.y = 0f;
        stage.DamageArea(p, r, stats.damage * qDamageMul);
        stage.SpawnFx(p + Vector3.up * 0.05f, new Vector3(0.5f, 0.02f, 0.5f), new Vector3(r * 2f, 0.02f, r * 2f), 0.25f, QColor);
    }

    void CastE()
    {
        eReady = Time.time + eCooldown * stats.cooldownMul;
        dashDir = facing;
        dashLeft = dashTime;
        iframe = Mathf.Max(iframe, dashTime + 0.1f);
    }

    public void TakeDamage(float dmg)
    {
        if (iframe > 0f || GameManager.I.State != GameState.Playing) return;
        stats.hp -= dmg;
        iframe = 0.5f;
        if (stats.hp <= 0f) { stats.hp = 0f; GameManager.I.GameOver(); }
    }
}

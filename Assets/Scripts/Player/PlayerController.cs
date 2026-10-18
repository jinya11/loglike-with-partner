using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로비: 몸이 있는 캐릭터 (WASD 이동, 점프, E 대시, F 상호작용).
/// 전투 스테이지: 형태 없는 존재 (동반자와 같은 존재). 전장을 떠다니는 조준점으로만 표시되며 공격받지 않음.
///   WASD 조준점 이동(지형 무시), 자동 공격(조준점에서 생명체를 노리는 적에게), Q 조준점 충격파, E 생명체 보호막, F 상호작용.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : Singleton<PlayerController>
{
    [SerializeField] Transform body;
    [SerializeField] float interactRange = 2.6f;
    [Header("Jump (로비)")]
    [SerializeField] float jumpSpeed = 10f, gravity = 30f;
    [Header("E - 대시 (로비)")]
    [SerializeField] float dashSpeed = 28f, dashTime = 0.18f;
    [Header("Q - 충격파 (조준점)")]
    [SerializeField] float qCooldown = 6f, qRadius = 4.5f, qDamageMul = 3f;
    [Header("E - 생명체 보호막 (전투)")]
    [SerializeField] float eCooldown = 3f, shieldCooldown = 10f, shieldTime = 2.5f;
    [Header("형태 없는 존재 (전투)")]
    [Tooltip("조준점 이동 속도 배율 (stats.moveSpeed 기준)")]
    [SerializeField] float presenceSpeedMul = 2f;
    [SerializeField] Color reticleColor = new Color(0.7f, 0.85f, 1f);

    static readonly Color QColor = new Color(0.4f, 0.8f, 1f);
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    CharacterController cc;
    PlayerStats stats;
    StageDirector stage;
    Transform reticle;
    Vector3 facing = Vector3.forward, dashDir;
    float attackTimer, qReady, eReady, dashLeft, iframe, vy;

    public Vector3 Position => transform.position;
    /// <summary>전투 스테이지에서는 형태 없는 존재 (피격/충돌 없음).</summary>
    public bool Formless { get; private set; }
    /// <summary>현재 상호작용 가능한 가장 가까운 대상 (없으면 null).</summary>
    public Interactable Focus { get; private set; }
    public bool Airborne => transform.position.y > 0.8f;
    public Vector3 Facing => facing;
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
        if (stage != null) BecomeFormless();
    }

    /// <summary>몸을 숨기고 조준점(빛 기둥 + 바닥 원)만 남김.</summary>
    void BecomeFormless()
    {
        Formless = true;
        cc.enabled = false;
        body.gameObject.SetActive(false);
        reticle = new GameObject("Presence").transform;
        reticle.SetParent(transform, false);
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(ColorId, reticleColor);
        MakePart(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(1.4f, 0.01f, 1.4f), mpb);
        MakePart(PrimitiveType.Cylinder, new Vector3(0f, 2.5f, 0f), new Vector3(0.08f, 2.5f, 0.08f), mpb);
        MakePart(PrimitiveType.Sphere, new Vector3(0f, 1.6f, 0f), Vector3.one * 0.45f, mpb);
    }

    void MakePart(PrimitiveType t, Vector3 pos, Vector3 scale, MaterialPropertyBlock mpb)
    {
        var go = GameObject.CreatePrimitive(t);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(reticle, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        var src = body.GetComponentInChildren<Renderer>(true);
        if (src) r.sharedMaterial = src.sharedMaterial;
        r.SetPropertyBlock(mpb);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
        if (move.sqrMagnitude > 0f)
        {
            // 카메라 기준 이동 (시점이 바뀌어도 W = 화면 위쪽)
            if (CameraFollow.I) move = Quaternion.Euler(0f, CameraFollow.I.MoveYaw, 0f) * move;
            move.Normalize(); facing = move;
        }

        if (Formless) UpdatePresence(dt, move, kb);
        else UpdateLobby(dt, move, kb);

        UpdateFocus();
        if (Focus != null && kb != null && kb.fKey.wasPressedThisFrame) Focus.Interact();
    }

    void UpdatePresence(float dt, Vector3 move, Keyboard kb)
    {
        // 지형/적과 충돌 없이 전장 위를 떠다님
        transform.position = Arena.Clamp(transform.position + move * (stats.moveSpeed * presenceSpeedMul * dt));
        AutoAttack(dt);
        if (kb == null) return;
        if (kb.qKey.wasPressedThisFrame && Time.time >= qReady) CastQ();
        if (kb.eKey.wasPressedThisFrame && Time.time >= eReady && Ward.I != null)
        {
            eReady = Time.time + shieldCooldown * stats.cooldownMul;
            Ward.I.Shield(shieldTime);
        }
    }

    void UpdateLobby(float dt, Vector3 move, Keyboard kb)
    {
        Vector3 pos = transform.position;
        bool grounded = (pos.y <= 0.001f || cc.isGrounded) && vy <= 0f;
        if (grounded && stats.canJump && kb != null && kb.spaceKey.wasPressedThisFrame) vy = jumpSpeed;
        else if (grounded) vy = 0f;
        else vy -= gravity * dt;

        Vector3 vel;
        if (dashLeft > 0f) { dashLeft -= dt; vel = dashDir * dashSpeed; }
        else vel = move * stats.moveSpeed;

        Vector3 delta = Arena.Clamp(pos + vel * dt) - pos;
        delta.y = Mathf.Max(-pos.y, vy * dt);
        cc.Move(delta);
        body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(facing), 900f * dt);

        if (kb != null && kb.eKey.wasPressedThisFrame && Time.time >= eReady)
        {
            eReady = Time.time + eCooldown;
            dashDir = facing;
            dashLeft = dashTime;
        }
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
        var target = stage.AttackTarget(transform.position, stats.attackRange);
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

    /// <summary>형태 없는 상태에서는 피해를 받지 않음 (지켜야 할 것은 생명체).</summary>
    public void TakeDamage(float dmg)
    {
        if (Formless || iframe > 0f || GameManager.I.State != GameState.Playing) return;
        stats.hp -= dmg;
        iframe = 0.5f;
        if (stats.hp <= 0f) { stats.hp = 0f; GameManager.I.GameOver(); }
    }
}

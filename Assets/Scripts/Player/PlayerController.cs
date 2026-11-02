using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로비: 몸이 있는 캐릭터 (WASD 이동, 점프, F 상호작용). 대시 없이 이동 속도를 높임.
/// 전투 스테이지: 형태 없는 존재 (동반자와 같은 존재). 전장을 떠다니는 조준점으로만 표시되며 공격받지 않음.
///   WASD 조준점 이동(지형 무시), Q 조준점 충격파, E 생명체 보호막, F 상호작용. 공격은 생명체가 고유 공격으로 함.
///   자율 + 생명체만 조작이면 AI가 대신 움직임 (재화 줍기, 스킬/폭격 자동).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : Singleton<PlayerController>
{
    [SerializeField] Transform body;
    [SerializeField] float interactRange = 2.6f;
    [Header("Jump (로비)")]
    [SerializeField] float jumpSpeed = 10f, gravity = 30f;
    [Header("이동 (로비)")]
    [Tooltip("로비 이동 속도 배율 (stats.moveSpeed 기준, 대시 대신 빠르게)")]
    [SerializeField] float lobbySpeedMul = 1.8f;
    [Header("Q - 충격파 (조준점)")]
    [SerializeField] float qCooldown = 6f, qRadius = 4.5f, qDamageMul = 3f;
    [Header("E - 생명체 보호막 (전투)")]
    [SerializeField] float shieldCooldown = 10f, shieldTime = 2.5f;
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
    Vector3 facing = Vector3.forward;
    float qReady, eReady, iframe, vy;

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

    const int LeashSeg = 64;
    LineRenderer leashRing;
    static readonly Vector3[] leashBuf = new Vector3[LeashSeg + 1];

    /// <summary>생명체 기준 최대 거리로 위치 제한 + 가까워지면 한계선 표시.</summary>
    Vector3 Leash(Vector3 p)
    {
        var pc = GameManager.I.Config.player;
        float max = pc.maxWardDistance;
        var ward = Ward.I;
        if (ward == null || max <= 0f) { ShowLeash(false, Vector3.zero, 0f, 0f, pc); return p; }
        Vector3 c = ward.Position;
        Vector3 d = p - c; d.y = 0f;
        float dist = d.magnitude;
        if (dist > max) { p.x = c.x + d.x / dist * max; p.z = c.z + d.z / dist * max; dist = max; }
        float warn = max * pc.leashWarnRatio;
        ShowLeash(dist > warn, c, max, Mathf.InverseLerp(warn, max, dist), pc);
        return p;
    }

    void ShowLeash(bool on, Vector3 c, float r, float k, PlayerConfig pc)
    {
        if (!on) { if (leashRing) leashRing.enabled = false; return; }
        if (!leashRing)
        {
            var go = new GameObject("LeashRing");
            leashRing = go.AddComponent<LineRenderer>();
            leashRing.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            leashRing.useWorldSpace = true; leashRing.loop = false;
            leashRing.widthMultiplier = 0.1f;
            leashRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            leashRing.receiveShadows = false;
            leashRing.positionCount = LeashSeg + 1;
        }
        leashRing.enabled = true;
        for (int i = 0; i <= LeashSeg; i++)
        {
            float a = i * (Mathf.PI * 2f / LeashSeg);
            leashBuf[i] = new Vector3(c.x + Mathf.Cos(a) * r, 0.08f, c.z + Mathf.Sin(a) * r);
        }
        leashRing.SetPositions(leashBuf);
        Color col = pc.leashColor; col.a *= k; // 한계에 가까울수록 진하게
        leashRing.startColor = leashRing.endColor = col;
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
        // 공격은 생명체가 함 (생명체 고유 공격). 감시자는 이동/스킬/재화 획득
        var ward = Ward.I;
        if (ward != null && ward.WatcherAuto) { AutoPresence(dt, ward); return; }
        if (ward != null && ward.Order == Ward.Command.Auto && ward.Controls == Ward.Control.CreatureOnly) move = Vector3.zero;
        // 지형/적과 충돌 없이 전장 위를 떠다님. 고리 연결 중엔 생명체에서 일정 거리 이상은 못 감 (생명체가 움직이면 같이 끌려감)
        Vector3 p = Arena.Clamp(transform.position + move * (stats.moveSpeed * presenceSpeedMul * dt));
        transform.position = ward == null || ward.Order == Ward.Command.Linked ? Leash(p) : p;
        if (ward == null || ward.Order != Ward.Command.Linked) ShowLeash(false, Vector3.zero, 0f, 0f, null);
        if (kb == null) return;
        if (kb.qKey.wasPressedThisFrame && Time.time >= qReady) CastQ();
        if (kb.eKey.wasPressedThisFrame && Time.time >= eReady && ward != null) CastShield(ward);
    }

    void CastShield(Ward ward)
    {
        eReady = Time.time + shieldCooldown * stats.cooldownMul;
        ward.Shield(shieldTime);
    }

    /// <summary>
    /// 감시자 자동 (자율 + 생명체만 조작): 생명체 근처에서 재화(경험치 보석/자원)를 주우러 다니고,
    /// 충격파/보호막/폭격을 알아서 사용 (폭격 위치도 자동).
    /// </summary>
    void AutoPresence(float dt, Ward ward)
    {
        var W = GameManager.I.Config.ward;
        Vector3 w = ward.Position; w.y = 0f;
        Vector3 goal = NearestPickup(w, W.autoPickupRadius, out bool found);
        if (!found)
        {
            // 줍을 게 없으면 생명체 뒤쪽 가까이에서 대기
            Vector3 back = -ward.Heading; back.y = 0f;
            goal = w + (back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back) * 3f;
        }
        Vector3 p = transform.position;
        Vector3 to = goal - p; to.y = 0f;
        float step = stats.moveSpeed * presenceSpeedMul * dt;
        if (to.sqrMagnitude > step * step) p += to.normalized * step; else { p.x = goal.x; p.z = goal.z; }
        // 생명체에서 너무 멀어지지 않게
        Vector3 off = p - w; off.y = 0f;
        if (off.magnitude > W.autoStayNearWard + W.autoPickupRadius) p = w + off.normalized * (W.autoStayNearWard + W.autoPickupRadius);
        transform.position = Arena.Clamp(p);
        ShowLeash(false, Vector3.zero, 0f, 0f, null);

        // 스킬 자동 사용
        if (Time.time >= qReady && CountEnemies(transform.position, qRadius * stats.areaMul) >= 3) CastQ();
        if (Time.time >= eReady && (stats.wardHp < stats.wardMaxHp * 0.4f || CountEnemies(w, 2.5f) >= 4)) CastShield(ward);
        if (BombardSkill.I) BombardSkill.I.TryAuto(w);
    }

    static Vector3 NearestPickup(Vector3 center, float radius, out bool found)
    {
        float best = radius * radius;
        Vector3 res = center;
        bool any = false;
        foreach (var g in XpGem.Active) Consider(g.transform.position);
        foreach (var r in ResourcePickup.Active) Consider(r.transform.position);
        found = any;
        return res;

        void Consider(Vector3 q)
        {
            Vector3 d = q - center; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < best) { best = sq; res = q; res.y = 0f; any = true; }
        }
    }

    static int CountEnemies(Vector3 c, float r)
    {
        int n = 0;
        float r2 = r * r;
        var list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 d = list[i].transform.position - c; d.y = 0f;
            if (d.sqrMagnitude < r2) n++;
        }
        return n;
    }

    void UpdateLobby(float dt, Vector3 move, Keyboard kb)
    {
        Vector3 pos = transform.position;
        bool grounded = (pos.y <= 0.001f || cc.isGrounded) && vy <= 0f;
        if (grounded && stats.canJump && kb != null && kb.spaceKey.wasPressedThisFrame) vy = jumpSpeed;
        else if (grounded) vy = 0f;
        else vy -= gravity * dt;

        Vector3 vel = move * (stats.moveSpeed * lobbySpeedMul);

        Vector3 delta = Arena.Clamp(pos + vel * dt) - pos;
        delta.y = Mathf.Max(-pos.y, vy * dt);
        cc.Move(delta);
        body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(facing), 900f * dt);
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

    void CastQ()
    {
        qReady = Time.time + qCooldown * stats.cooldownMul;
        float r = qRadius * stats.areaMul;
        Vector3 p = transform.position; p.y = 0f;
        stage.DamageArea(p, r, stats.damage * qDamageMul);
        stage.SpawnFx(p + Vector3.up * 0.05f, new Vector3(0.5f, 0.02f, 0.5f), new Vector3(r * 2f, 0.02f, r * 2f), 0.25f, QColor);
        Vfx.Nova(p, r, QColor);
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

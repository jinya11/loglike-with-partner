using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 지켜야 할 작은 생명체. 스테이지를 자유롭게 돌아다니고(배회), 적이 가까우면 반대쪽으로 도망.
/// 포털이 열리면 포털로 걸어가며, 도착하면 다음 스테이지로. 바닥 구역(눈밭/물/용암/회복)의 영향을 받음.
/// 미약한 저항: 주기적으로 주변 적에게 작은 피해 + 넉백. 체력이 0이 되면 패배.
/// 고유 공격(CreatureDef.attack, 예: 토끼 당근 던지기)으로 사거리 안의 적을 공격.
/// 명령 체계 (WardConfig 키): 고리(플레이어가 근처에 묶임, 기존) / 자율(묶임 해제, 화살표 키로 직접 조작) / 정지 / 따라오기.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Ward : Singleton<Ward>
{
    [SerializeField] Transform body, eye;
    [SerializeField] Renderer[] tintRenderers;
    [Header("표현")]
    [SerializeField] Color normalColor = new Color(0.75f, 1f, 0.7f), hurtColor = new Color(1f, 0.35f, 0.35f), shieldColor = new Color(0.55f, 0.9f, 1f);
    [Tooltip("디버프 색: 공포 / 출혈 / 감속")]
    [SerializeField] Color fearColor = new Color(0.7f, 0.45f, 1f), bleedColor = new Color(0.75f, 0.15f, 0.2f), slowColor = new Color(0.45f, 0.65f, 1f);

    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color PulseColor = new Color(0.6f, 1f, 0.6f), WaterColor = new Color(0.55f, 0.85f, 1f);
    const float HitIFrame = 0.5f, FleeRepick = 0.8f;
    const int FleeDirs = 12;

    // AI 수치는 WardConfig (GameConfig.ward)에서 읽음 → 플레이 중 수정도 바로 반영
    WardConfig W;
    float wanderSpeedMul => W.wanderSpeedMul;
    float wanderRadius => W.wanderRadius;
    float wanderPauseMin => W.wanderPauseMin;
    float wanderPauseMax => W.wanderPauseMax;
    float fleeRadius => W.fleeRadius;
    float portalReach => W.portalReach;
    float fleeDistance => W.fleeDistance;
    float dangerRadius => W.dangerRadius;
    float dangerWeight => W.dangerWeight;
    float replanInterval => W.replanInterval;
    float waypointReach => W.waypointReach;
    float surroundRadius => W.surroundRadius;
    int surroundCount => W.surroundCount;
    int surroundSides => W.surroundSides;
    float surroundDelay => W.surroundDelay;
    float rescueCooldown => W.rescueCooldown;
    float rescueRadius => W.rescueRadius;
    float rescuePush => W.rescuePush;
    float rescueShield => W.rescueShield;

    CharacterController cc;
    PlayerStats stats;
    MaterialPropertyBlock mpb;
    Vector3 wanderTarget, heading = Vector3.forward;
    float pulseTimer, iframe, hurtFlash, shieldLeft, pauseLeft, splashTimer, surroundedFor, rescueReadyAt;
    bool inWater, reachedPortal;
    readonly System.Collections.Generic.List<Vector3> path = new System.Collections.Generic.List<Vector3>(128);
    int pathIndex;
    float replanLeft;
    Vector3 lastGoal, fleeGoal;
    bool hasFleeGoal;
    float fleeRepickLeft;
    // 디버프
    float slowMul = 1f, slowLeft, fearLeft, bleedDps, bleedLeft, debuffFxTimer;
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

    // ---- 디버그 조회용 (WardDebugPanel) ----
    public enum AiMode { Idle, Wander, Pause, Flee, Portal, Manual, Hold, Follow }

    // ---- 명령 체계 ----
    public enum Command { Linked, Auto, Stay, Follow }
    /// <summary>현재 명령. Linked(고리) 외에는 플레이어와 묶이지 않음.</summary>
    public Command Order { get; private set; } = Command.Linked;
    /// <summary>자율 상태에서 화살표 키로 직접 조작 중.</summary>
    public bool Steering { get; private set; }
    public static string CommandName(Command c) => c switch
    {
        Command.Auto => "자율", Command.Stay => "정지", Command.Follow => "따라오기", _ => "고리 연결",
    };
    Command lastFreeOrder = Command.Auto;

    /// <summary>자율 상태에서 플레이어가 조작하는 대상.</summary>
    public enum Control { Both, WatcherOnly, CreatureOnly }
    public Control Controls { get; private set; } = Control.Both;
    /// <summary>감시자(플레이어 조준점)를 AI가 대신 움직임: 재화 줍기, 스킬/폭격 자동.</summary>
    public bool WatcherAuto => Order == Command.Auto && Controls == Control.CreatureOnly;
    public static string ControlName(Control c) => c switch
    {
        Control.WatcherOnly => "감시자만 조작 (생명체 자동)", Control.CreatureOnly => "생명체만 조작 (감시자 자동)", _ => "둘 다 조작 (WASD 감시자 / 화살표 생명체)",
    };

    public void SetOrder(Command c)
    {
        if (c == Order) return;
        Order = c;
        if (c != Command.Linked) lastFreeOrder = c;
        hasFleeGoal = false; replanLeft = 0f;
        if (c == Command.Auto || c == Command.Linked) PickWanderTarget(StageDirector.I);
        Vfx.Pillar(transform.position, c == Command.Linked ? new Color(0.7f, 0.85f, 1f) : new Color(0.6f, 1f, 0.6f));
    }

    /// <summary>고리 연결 ↔ 마지막으로 쓴 명령.</summary>
    public void ToggleLink() => SetOrder(Order == Command.Linked ? lastFreeOrder : Command.Linked);

    void ReadCommands(Keyboard kb)
    {
        if (kb == null) return;
        if (kb[W.linkKey].wasPressedThisFrame) ToggleLink();
        else if (kb[W.autoKey].wasPressedThisFrame) SetOrder(Command.Auto);
        else if (kb[W.stayKey].wasPressedThisFrame) SetOrder(Command.Stay);
        else if (kb[W.followKey].wasPressedThisFrame) SetOrder(Command.Follow);
        else if (kb[W.bothKey].wasPressedThisFrame) SetControl(Control.Both);
        else if (kb[W.watcherOnlyKey].wasPressedThisFrame) SetControl(Control.WatcherOnly);
        else if (kb[W.creatureOnlyKey].wasPressedThisFrame) SetControl(Control.CreatureOnly);
    }

    /// <summary>자율 상태의 조작 대상 지정 (자율 전용이라 다른 명령 중이면 자율로 바꿈). UI 버튼/키에서 호출.</summary>
    public void SetControl(Control c)
    {
        SetOrder(Command.Auto);
        if (Controls == c) return;
        Controls = c;
        Vfx.Pillar(transform.position, new Color(1f, 0.9f, 0.5f));
    }

    /// <summary>자율 상태의 직접 조작 입력 (화면 기준 방향). 화살표, 생명체만 조작할 때는 WASD도.</summary>
    Vector3 ReadSteer(Keyboard kb)
    {
        if (kb == null || Controls == Control.WatcherOnly) return Vector3.zero;
        bool wasd = Controls == Control.CreatureOnly;
        Vector3 m = Vector3.zero;
        if (kb.upArrowKey.isPressed || (wasd && kb.wKey.isPressed)) m.z += 1f;
        if (kb.downArrowKey.isPressed || (wasd && kb.sKey.isPressed)) m.z -= 1f;
        if (kb.rightArrowKey.isPressed || (wasd && kb.dKey.isPressed)) m.x += 1f;
        if (kb.leftArrowKey.isPressed || (wasd && kb.aKey.isPressed)) m.x -= 1f;
        if (m.sqrMagnitude == 0f) return m;
        if (CameraFollow.I) m = Quaternion.Euler(0f, CameraFollow.I.MoveYaw, 0f) * m;
        return m.normalized;
    }
    public AiMode Mode { get; private set; }
    public float FleeRadius => fleeRadius;
    public float DangerRadius => dangerRadius;
    public float SurroundRadius => surroundRadius;
    public int SurroundCountNeed => surroundCount;
    public int SurroundSidesNeed => surroundSides;
    public float SurroundDelay => surroundDelay;
    public float SurroundedFor => surroundedFor;
    /// <summary>마지막 포위 검사 결과: 반경 내 적 수, 막힌 방향 비트(1:+X 2:-X 4:+Z 8:-Z).</summary>
    public int LastSurroundCount { get; private set; }
    public int LastSurroundSides { get; private set; }
    public float RescueCooldownLeft => Mathf.Max(0f, rescueReadyAt - Time.time);
    public float PulseTimer => pulseTimer;
    public bool HasFleeGoal => hasFleeGoal;
    public Vector3 FleeGoal => fleeGoal;
    public Vector3 CurrentGoal => lastGoal;
    public System.Collections.Generic.IReadOnlyList<Vector3> Path => path;
    public int PathIndex => pathIndex;
    public float ReplanLeft => replanLeft;
    public Vector3 KnockVelocity => knockVel;
    public float SlowMul => slowMul;
    public float FearLeft => fearLeft;
    public float SlowLeft => slowLeft;
    public float BleedLeft => bleedLeft;
    public float ShieldLeft => shieldLeft;

    protected override void Awake()
    {
        base.Awake();
        cc = GetComponent<CharacterController>();
        W = GameManager.I.Config.ward;
        mpb = new MaterialPropertyBlock();
    }

    // ---- 생명체 종류 (모델 + 고유 능력) ----
    CreatureDef creature;
    CreatureVisual visual;
    CreatureAbility ability;
    CreatureAttack attack;
    Vector3 lastPos;

    public CreatureDef Creature => creature;
    public CreatureAbility Ability => ability;
    public CreatureAttack Attack => attack;

    void Start()
    {
        stats = GameManager.I.Stats;
        if (stats.wardHp <= 0f) stats.wardHp = stats.wardMaxHp;
        wanderTarget = transform.position;
        lastPos = transform.position;
        ApplyCreature(GameManager.I.Creature);
        SetTint(normalColor);
        if (ability) ability.OnStart();
    }

    protected override void OnDestroy()
    {
        if (ability) ability.OnEnd();
        if (ability) Destroy(ability);
        if (attack) Destroy(attack);
        base.OnDestroy();
    }

    /// <summary>생명체 종류 적용: 기본 도형 대신 리깅된 모델을 쓰고, 능력을 복제해 연결.</summary>
    void ApplyCreature(CreatureDef def)
    {
        creature = def;
        // 공격: 생명체 고유 공격 (없으면 기본 탄환). 런마다 상태를 따로 갖도록 복제
        attack = def != null && def.attack ? Instantiate(def.attack) : ScriptableObject.CreateInstance<BasicShotAttack>();
        attack.Bind(new CreatureContext(this, def));
        if (def == null) return;
        if (def.model)
        {
            // 기본 도형 몸은 숨기고 (시점용 Eye는 유지) 모델로 교체
            foreach (Transform ch in body) if (ch != eye) ch.gameObject.SetActive(false);
            visual = Instantiate(def.model, body);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            tintRenderers = visual.GetComponentsInChildren<Renderer>(true);
            normalColor = def.normalTint;
            if (visual.EyeAnchor) { eye.SetParent(visual.EyeAnchor, false); eye.localPosition = Vector3.zero; eye.localRotation = Quaternion.identity; }
            float h = visual.Height;
            cc.height = Mathf.Max(h * 0.9f, cc.radius * 2f);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        }
        if (def.ability)
        {
            ability = Instantiate(def.ability); // 런마다 상태를 따로 갖도록 복제
            ability.Bind(new CreatureContext(this, def));
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (visual)
        {
            // 실제 이동량으로 걷기 모션 (밀려날 때 포함). 일시정지/게임 종료 시 멈춤
            Vector3 mv = transform.position - lastPos; mv.y = 0f;
            lastPos = transform.position;
            bool live = GameManager.I.State == GameState.Playing && Alive && dt > 0f;
            visual.SetFrozen(!live);
            if (live) visual.SetMoveSpeed(Feared ? 0f : mv.magnitude / dt, dt);
        }
        if (GameManager.I.State != GameState.Playing || !Alive) return; // 죽은 뒤에는 아무것도 하지 않음
        var stage = StageDirector.I;
        Vector3 pos = transform.position;
        if (ability) ability.Tick(dt);
        if (attack) attack.Tick(dt);

        float zoneMul = ApplyZones(dt);

        var kb = GameManager.I.InputBlocked ? null : Keyboard.current;
        ReadCommands(kb);
        Vector3 steer = Order == Command.Auto ? ReadSteer(kb) : Vector3.zero;
        Steering = steer.sqrMagnitude > 0f;

        // 포털 도착
        if (stage.PortalOpen)
        {
            Vector3 tp = stage.PortalPosition - pos; tp.y = 0f;
            if (tp.sqrMagnitude < portalReach * portalReach)
            {
                if (!reachedPortal) { reachedPortal = true; GameManager.I.NextStage(); }
                return;
            }
        }

        // 목표 결정: 직접 조작 > 포털 귀환(정지 명령 제외) > 정지 > 도망 > 따라오기 > 배회
        bool fleeing = false;
        Vector3 goal;
        float speedMul = 1f;
        if (Steering)
        {
            goal = pos + steer * 2f;
            speedMul = W.steerSpeedMul;
            hasFleeGoal = false;
            Mode = AiMode.Manual;
        }
        else if (stage.PortalOpen && Order != Command.Stay)
        {
            goal = stage.PortalPosition;
            Mode = AiMode.Portal;
        }
        else if (Order == Command.Stay)
        {
            goal = pos;
            speedMul = 0f;
            hasFleeGoal = false;
            Mode = AiMode.Hold;
        }
        else if (Threatened(pos))
        {
            fleeing = true;
            // 도망 목표는 한 번 정하면 유지 (매 프레임 바꾸면 경로가 흔들려 제자리에서 떨림)
            if (!hasFleeGoal || Flat(fleeGoal - pos).sqrMagnitude < 2.25f || (fleeRepickLeft -= dt) <= 0f)
            {
                fleeGoal = PickFleeGoal(pos, stage);
                hasFleeGoal = true;
                fleeRepickLeft = FleeRepick;
            }
            goal = fleeGoal;
            Mode = AiMode.Flee;
        }
        else if (Order == Command.Follow && PlayerController.I != null)
        {
            hasFleeGoal = false;
            goal = PlayerController.I.Position; goal.y = 0f;
            Mode = AiMode.Follow;
            if (Flat(goal - pos).sqrMagnitude < W.followStopDistance * W.followStopDistance) { speedMul = 0f; Mode = AiMode.Hold; }
        }
        else
        {
            hasFleeGoal = false;
            goal = wanderTarget;
            speedMul = wanderSpeedMul;
            Mode = AiMode.Wander;
            Vector3 tw = wanderTarget - pos; tw.y = 0f;
            if (tw.sqrMagnitude < 0.5f)
            {
                speedMul = 0f;
                Mode = AiMode.Pause;
                if ((pauseLeft -= dt) <= 0f) PickWanderTarget(stage);
            }
        }

        // 경로 재계산: 적을 피하면서 가장 빠른 길 (주기적으로, 또는 목표가 크게 바뀌면)
        var nav = stage.Nav;
        if (nav != null && !Steering && speedMul > 0f && ((replanLeft -= dt) <= 0f || (goal - lastGoal).sqrMagnitude > 4f))
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
            if (nav != null && !Steering)
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
            // 막혀서 못 움직이면 곧 경로 재계산 (매 프레임 A*는 피함)
            if (!knocked && (transform.position - before).sqrMagnitude < delta.sqrMagnitude * 0.1f)
            {
                replanLeft = Mathf.Min(replanLeft, 0.1f);
                hasFleeGoal = false; // 막힌 방향의 도망 목표는 버림
            }
            if (dir.sqrMagnitude > 0f) body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(dir), 540f * dt);
            if (knocked)
            {
                knockVel = Vector3.MoveTowards(knockVel, Vector3.zero, 25f * dt);
                if (knockVel.sqrMagnitude <= 0.01f) { replanLeft = 0f; hasFleeGoal = false; } // 밀려난 뒤 한 번만 재계산
            }
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
            Vfx.Nova(c, r, PulseColor);
            if (ability) ability.OnPulse(c, r);
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
        LastSurroundCount = count; LastSurroundSides = sides;
        surroundedFor = surrounded ? surroundedFor + dt : 0f;
        if (surroundedFor < surroundDelay || Time.time < rescueReadyAt) return;

        surroundedFor = 0f;
        rescueReadyAt = Time.time + rescueCooldown;
        stage.RadialForce(p, rescueRadius, rescuePush, stats.wardPulseDamage);
        Shield(rescueShield);
        float d2 = rescueRadius * 2f;
        stage.SpawnFx(p + Vector3.up * 0.05f, new Vector3(0.5f, 0.02f, 0.5f), new Vector3(d2, 0.02f, d2), 0.35f, PulseColor);
        Vfx.Rescue(p, rescueRadius, PulseColor);
        if (ability) ability.OnRescue();
        replanLeft = 0f; // 열린 틈으로 바로 길찾기
    }

    void PickWanderTarget(StageDirector stage)
    {
        var p = Arena.RandomPoint(wanderRadius); // 원형/정사각형 경계 모두
        wanderTarget = stage.Nav != null ? stage.Nav.NearestFree(p) : p;
        pauseLeft = Random.Range(wanderPauseMin, wanderPauseMax);
        replanLeft = 0f;
    }

    /// <summary>fleeRadius 안에 적이 있는지.</summary>
    bool Threatened(Vector3 pos)
    {
        var list = Enemy.Active;
        float fr = fleeRadius * fleeRadius;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 d = pos - list[i].transform.position; d.y = 0f;
            if (d.sqrMagnitude < fr) return true;
        }
        return false;
    }

    /// <summary>
    /// 12방향 후보 중 가장 안전한 도망 지점. 후보 주변 적 밀도 + 가는 길을 가로막는 적 + 벽 근접을 비용으로 계산.
    /// 현재 목표는 비용을 깎아 줘서(이력 현상) 비슷한 후보 사이를 오가지 않음.
    /// </summary>
    Vector3 PickFleeGoal(Vector3 pos, StageDirector stage)
    {
        var list = Enemy.Active;
        float edge = stage.ArenaRadius * 0.8f, edgeSq = edge * edge;
        Vector3 best = pos;
        float bestCost = float.MaxValue;
        for (int k = 0; k <= FleeDirs; k++)
        {
            Vector3 c;
            if (k == FleeDirs)
            {
                if (!hasFleeGoal) break;
                c = fleeGoal; // 현재 목표도 후보
            }
            else
            {
                float a = k * (Mathf.PI * 2f / FleeDirs);
                c = Arena.Clamp(pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * fleeDistance);
                if (stage.Nav != null) c = stage.Nav.NearestFree(c);
            }
            Vector3 seg = Flat(c - pos);
            float segLen = seg.magnitude;
            if (segLen < 1f) continue;
            Vector3 segDir = seg / segLen;
            float cost = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 e = list[i].transform.position;
                Vector3 de = Flat(e - c);
                cost += 1f / Mathf.Max(de.sqrMagnitude, 0.25f);
                // 가는 길 위(앞쪽)에 있는 적은 크게 감점
                Vector3 rel = Flat(e - pos);
                float along = Vector3.Dot(rel, segDir);
                if (along > 0f && along < segLen)
                {
                    float side = (rel - segDir * along).sqrMagnitude;
                    if (side < 2.25f) cost += 4f / (side + 0.25f);
                }
            }
            if (Arena.Square) { float m = 12f, ed = Arena.EdgeDistance(c); if (ed < m) cost += (m - ed) * (m - ed) * 0.45f; } // 칸 경계: 가장 가까운 바깥 벽 기준
            else { float cr = c.x * c.x + c.z * c.z; if (cr > edgeSq) cost += (cr - edgeSq) * 0.05f; } // 벽에 몰리지 않도록
            if (k == FleeDirs) cost *= 0.7f;
            if (cost < bestCost) { bestCost = cost; best = c; }
        }
        return best;
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
        Vfx.Shield(transform.position, shieldColor);
        if (ability) ability.OnShield(seconds);
    }

    public void TakeDamage(float dmg)
    {
        if (iframe > 0f || !Alive || GameManager.I.State != GameState.Playing) return;
        iframe = HitIFrame;
        if (Shielded) { Vfx.Hit(transform.position + Vector3.up * 0.5f, shieldColor); return; } // 막힘 표시
        if (ability) { dmg = ability.ModifyDamage(dmg); if (dmg <= 0f) return; }
        stats.wardHp -= dmg;
        hurtFlash = 0.15f;
        Vfx.Hit(transform.position + Vector3.up * 0.5f, hurtColor);
        if (ability) ability.OnDamaged(dmg);
        if (stats.wardHp <= 0f) Die();
    }

    // ---- 디버프 (보호막 중에는 무시) ----
    public void Knockback(Vector3 velocity) { if (!Shielded) knockVel = velocity; }
    public void Slow(float mul, float seconds)
    {
        if (Shielded || !AllowDebuff(EnemyAbility.Slow)) return;
        if (slowLeft <= 0f) Vfx.Debuff(transform.position, EnemyAbility.Slow, 8);
        slowMul = Mathf.Min(slowLeft > 0f ? slowMul : 1f, mul); slowLeft = Mathf.Max(slowLeft, seconds);
    }
    public void Fear(float seconds)
    {
        if (Shielded || !AllowDebuff(EnemyAbility.Fear)) return;
        if (fearLeft <= 0f) Vfx.Debuff(transform.position, EnemyAbility.Fear, 10);
        fearLeft = Mathf.Max(fearLeft, seconds);
    }
    public void Bleed(float dps, float seconds)
    {
        if (Shielded || !AllowDebuff(EnemyAbility.Bleed)) return;
        if (bleedLeft <= 0f) Vfx.Debuff(transform.position, EnemyAbility.Bleed, 8);
        bleedDps = Mathf.Max(bleedLeft > 0f ? bleedDps : 0f, dps); bleedLeft = Mathf.Max(bleedLeft, seconds);
    }

    void TickDebuffs(float dt)
    {
        if (slowLeft > 0f && (slowLeft -= dt) <= 0f) slowMul = 1f;
        if (fearLeft > 0f) fearLeft -= dt;
        if (bleedLeft > 0f) { bleedLeft -= dt; Drain(bleedDps * dt); }
        if ((debuffFxTimer -= dt) > 0f) return; // 지속 중 표시 (0.25초마다 소량)
        debuffFxTimer = 0.25f;
        Vector3 p = transform.position;
        if (slowLeft > 0f) Vfx.Debuff(p, EnemyAbility.Slow, 1);
        if (fearLeft > 0f) Vfx.Debuff(p, EnemyAbility.Fear, 1);
        if (bleedLeft > 0f) Vfx.Debuff(p, EnemyAbility.Bleed, 1);
    }

    void UpdateTint()
    {
        Color c = hurtFlash > 0f ? hurtColor : Shielded ? shieldColor : Feared ? fearColor : Bleeding ? bleedColor : Slowed ? slowColor : normalColor;
        if (visual) c = Color.Lerp(normalColor, c, creature.tintStrength); // 텍스처 모델은 원래 색이 보이도록 살짝만 물들임
        if (c != currentTint) SetTint(c);
    }

    bool AllowDebuff(EnemyAbility kind) => !ability || ability.AllowDebuff(kind);

    void Die()
    {
        if (ability && ability.OnBeforeDeath() && stats.wardHp > 0f) return; // 능력이 사망을 막음 (체력을 되돌려 놔야 함)
        stats.wardHp = 0f;
        Vfx.Death(transform.position, normalColor);
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 새 소녀 깃털 평타 + 성장 노드 효과 (스테이지 단위 싱글톤, StageDirector 오브젝트에 붙음 → 씬과 함께 정리).
/// 깃털 평타는 성장 노드와 상관없이 항상 이 컴포넌트가 쏘고 Tick. 수치는 BirdGrowthConfig.bird.
/// 메인: 날개짓(띄움) / 돌진(피격 시 확률로 피해 무시 + 돌진) / 거침없는 날개(깃털 확정 드롭, 주우면 4방향 깃털).
/// 패시브: 공속 스택(맞으면 1 감소, 캐릭터 옆 표시) / 깃털 드롭(주우면 이속·공속) / 빠를수록 피해 감소. 감시자: 폭격 시 가로 돌풍.
/// </summary>
public class BirdGrowth : Singleton<BirdGrowth>
{
    class Pickup { public Transform t; public Vector3 pos; public float until; }

    GrowthLoadout L;
    BirdGrowthSettings S;
    Pool<FeatherShot> feathers;
    readonly List<Pickup> pickups = new List<Pickup>(8);
    readonly Stack<Pickup> freePickups = new Stack<Pickup>(8);
    readonly Stack<Transform> freePickupObjs = new Stack<Transform>(8);
    readonly HashSet<Enemy> dashHit = new HashSet<Enemy>();
    static readonly Quaternion Lying = Quaternion.Euler(0f, 0f, 90f);

    BirdStackHud stackHud;
    Transform cam;
    int stacks;
    float stackTimer, buffUntil, buffSpeedApplied, dashReadyAt;
    bool dashing;

    /// <summary>공격 간격 배율 (공속 스택 + 깃털 버프). FeatherAttack이 읽음.</summary>
    public float IntervalMul
    {
        get
        {
            float atk = 1f;
            if (L != null && L.momentum) atk += stacks * S.stackAttackSpeed;
            if (Time.time < buffUntil) atk += S.buffAttackSpeed;
            return 1f / atk;
        }
    }

    /// <summary>새 소녀 깃털 평타는 항상 필요 → 성장 노드가 없어도 생성.</summary>
    public static BirdGrowth Ensure(GrowthLoadout l)
    {
        if (!StageDirector.I || l == null || !(l.config is BirdGrowthConfig)) return null;
        var g = I ? I : StageDirector.I.gameObject.AddComponent<BirdGrowth>();
        g.Setup(l);
        return g;
    }

    void Setup(GrowthLoadout l)
    {
        Unhook();
        L = l; S = ((BirdGrowthConfig)l.config).bird;
        if (feathers == null && S.featherPrefab) feathers = new Pool<FeatherShot>(S.featherPrefab, transform, 16);
        if (L.main == GrowthEffect.WingDash) Ward.DodgeIntercept = OnDodge;
        if (L.speedGuard) Ward.DamageScale = Guard;
        if (L.momentum)
        {
            Ward.Hurt += OnHurt;
            if (!stackHud && S.stackHudPrefab) stackHud = Instantiate(S.stackHudPrefab, transform);
            if (stackHud) stackHud.Set(0);
        }
        if (L.birdGust) WatcherSkills.BombardCast += OnBombard;
    }

    void Unhook()
    {
        if (Ward.DodgeIntercept == (System.Func<Enemy, float, bool>)OnDodge) Ward.DodgeIntercept = null;
        if (Ward.DamageScale == (System.Func<float, float>)Guard) Ward.DamageScale = null;
        Ward.Hurt -= OnHurt;
        WatcherSkills.BombardCast -= OnBombard;
    }

    protected override void OnDestroy()
    {
        Unhook();
        RemoveBuffSpeed(); // 런 능력치에 남지 않게
        base.OnDestroy();
    }

    // ================= 깃털 평타 =================

    public void Fire(FeatherShot.Spec spec)
    {
        if (feathers == null) return;
        spec.hitRadius = S.featherHitRadius;
        feathers.Get(spec.from, Quaternion.identity).Init(spec);
    }

    /// <summary>깃털이 적에게 맞음 (피해 전).</summary>
    public void OnFeatherHit(Enemy e, Vector3 at, float dmg, bool primary)
    {
        if (!primary) return; // 4방향 깃털은 피해만
        if (L.main == GrowthEffect.WingLift) { e.Launch(S.liftDuration, S.liftHeight); Vfx.Hit(e.transform.position + Vector3.up * 0.6f, S.windColor); }
        bool drop = L.main == GrowthEffect.ScatterWings || (L.featherDrop && Random.value < S.dropChance);
        if (drop) SpawnPickup(at);
    }

    // ================= 바닥 깃털 =================

    void SpawnPickup(Vector3 at)
    {
        if (pickups.Count >= S.maxPickups || !S.featherPrefab) return;
        var p = freePickups.Count > 0 ? freePickups.Pop() : new Pickup();
        p.t = freePickupObjs.Count > 0 ? freePickupObjs.Pop() : Instantiate(S.featherPrefab.Visual, transform).transform;
        p.pos = Ground.Snap(at); p.until = Time.time + S.pickupLife;
        p.t.gameObject.SetActive(true);
        p.t.SetPositionAndRotation(p.pos + Vector3.up * 0.3f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Lying);
        pickups.Add(p);
    }

    void TickPickups()
    {
        float now = Time.time, rr = S.pickupRadius * S.pickupRadius;
        bool hasWard = Ward.I && Ward.I.Alive, hasPlayer = PlayerController.I;
        Vector3 w = hasWard ? Ward.I.Position : default, pl = hasPlayer ? PlayerController.I.Position : default;
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            var p = pickups[i];
            bool got = (hasWard && Flat(w - p.pos) < rr) || (hasPlayer && Flat(pl - p.pos) < rr); // 새 소녀 또는 감시자가 주움
            if (got) Collect(p.pos);
            if (got || now > p.until) { RemovePickup(i); continue; }
            p.t.position = p.pos + Vector3.up * (0.3f + 0.08f * Mathf.Sin(now * 4f + i));
        }
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude; }

    void RemovePickup(int i)
    {
        var p = pickups[i];
        pickups[i] = pickups[pickups.Count - 1]; pickups.RemoveAt(pickups.Count - 1);
        p.t.gameObject.SetActive(false);
        freePickupObjs.Push(p.t); p.t = null;
        freePickups.Push(p);
    }

    void Collect(Vector3 at)
    {
        Vfx.Pickup(at + Vector3.up * 0.4f, S.windColor);
        if (L.featherDrop) // 이속·공속 버프 (갱신형)
        {
            if (buffSpeedApplied <= 0f) { GameManager.I.Stats.wardSpeed += S.buffMoveSpeed; buffSpeedApplied = S.buffMoveSpeed; }
            buffUntil = Time.time + S.buffDuration;
        }
        if (L.main == GrowthEffect.ScatterWings) // 동서남북 4방향 포물선 깃털
        {
            var st = GameManager.I.Stats;
            var spec = new FeatherShot.Spec
            {
                from = at + Vector3.up * 0.6f, speed = Mathf.Max(6f, st.projectileSpeed * 0.7f), range = S.scatterRange,
                damage = st.damage * S.scatterDamageMul, curve = S.scatterCurve, arc = S.scatterHeight, primary = false, // 지면 기준 수평으로 휘며 퍼짐
            };
            for (int k = 0; k < 4; k++) { spec.dir = Quaternion.Euler(0f, 90f * k, 0f) * Vector3.forward; Fire(spec); }
        }
    }

    void RemoveBuffSpeed()
    {
        if (buffSpeedApplied <= 0f || GameManager.I == null) return;
        GameManager.I.Stats.wardSpeed -= buffSpeedApplied;
        buffSpeedApplied = 0f;
    }

    // ================= 공속 스택 =================

    void TickStacks(float dt)
    {
        if (!L.momentum) return;
        if (stacks < S.maxStacks && (stackTimer += dt) >= S.stackInterval) { stackTimer = 0f; stacks++; }
        if (!stackHud || !Ward.I) return;
        stackHud.Set(stacks);
        if (!cam && Camera.main) cam = Camera.main.transform;
        stackHud.Follow(Ward.I.Position, cam);
    }

    void OnHurt(float dmg) { stacks = Mathf.Max(0, stacks - 1); stackTimer = 0f; } // 전부가 아니라 1만 감소

    // ================= 빠를수록 피해 감소 =================

    float Guard(float dmg)
    {
        var st = GameManager.I.Stats;
        float baseSpeed = Mathf.Max(0.1f, GameManager.I.Config.ward.wardSpeed);
        float move = Mathf.Max(0f, st.wardSpeed / baseSpeed - 1f), atk = Mathf.Max(0f, 1f / IntervalMul - 1f);
        return dmg * (1f - Mathf.Min(S.guardMax, move * S.guardPerMove + atk * S.guardPerAttack));
    }

    // ================= 돌진 =================

    bool OnDodge(Enemy from, float dmg)
    {
        if (dashing || Time.time < dashReadyAt || !Ward.I || Random.value >= S.dashChance) return false;
        StartCoroutine(Dash(from));
        return true; // 이번 피해는 무시
    }

    IEnumerator Dash(Enemy from)
    {
        dashing = true; dashReadyAt = Time.time + S.dashCooldown;
        var ward = Ward.I;
        Vector3 dir = ward.Heading; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f && from) { dir = from.transform.position - ward.Position; dir.y = 0f; }
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        Vector3 start = ward.Position;
        Vfx.Gust(start, dir, S.dashRadius, S.windColor);
        float t = 0f, step = S.dashDistance / Mathf.Max(0.02f, S.dashTime);
        while (t < S.dashTime && ward && ward.Alive)
        {
            float dt = Time.deltaTime;
            t += dt;
            ward.Nudge(dir * (step * dt));
            yield return null;
        }
        dashHit.Clear();
        if (ward)
        {
            Vector3 end = ward.Position;
            Vfx.Gust(end, dir, S.dashRadius, S.windColor);
            float dd = GameManager.I.Stats.damage * S.dashDamageMul, rr = S.dashRadius * S.dashRadius;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--) // 경로(선분) 근처의 적에게 한 번씩
            {
                if (i >= list.Count) continue;
                var e = list[i];
                Vector3 p = e.transform.position, ab = end - start; ab.y = 0f;
                float k = ab.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector3.Dot(p - start, ab) / ab.sqrMagnitude) : 0f;
                Vector3 c = start + ab * k;
                if (Flat(p - c) <= rr && dashHit.Add(e)) e.Hit(dd, c);
            }
        }
        dashing = false;
    }

    // ================= 감시자: 가로 돌풍 =================

    void OnBombard(WatcherSkillType t)
    {
        var ward = Ward.I; var comp = CompanionBase.Current;
        if (!ward || !comp || !PlayerController.I) return;
        float r = comp.BombardRadius * GameManager.I.Stats.areaMul;
        if (Flat(ward.Position - PlayerController.I.Position) > r * r) return; // 폭격 범위 밖
        Vector3 dir = ward.Heading; dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        StartCoroutine(GustRoutine(ward.Position, dir));
    }

    /// <summary>폭격 한 줄기가 새 소녀 앞으로 가로질러 날아감: 앞에서부터 차례로 터지며(피해 없음) 적을 진행 방향으로 날려 보냄.</summary>
    IEnumerator GustRoutine(Vector3 start, Vector3 dir)
    {
        var stage = StageDirector.I;
        SfxManager.Play(SfxManager.S.bombardImpact, start);
        float step = S.gustWidth * 0.8f;
        var wait = new WaitForSeconds(S.gustStepTime);
        for (float d = S.gustWidth * 0.5f; d <= S.gustLength && stage; d += step)
        {
            Vector3 p = Ground.Snap(start + dir * d);
            stage.PushArea(p, S.gustWidth, dir * S.gustPush, 0f); // 피해 없음, 멀리 날려 보냄
            Vfx.Explosion(p, S.gustWidth * 0.7f, S.windColor);
            Vfx.Gust(p, dir, S.gustWidth, S.windColor);
            yield return wait;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        FeatherShot.TickAll(dt);
        TickPickups();
        TickStacks(dt);
        if (buffSpeedApplied > 0f && Time.time >= buffUntil) RemoveBuffSpeed();
    }
}

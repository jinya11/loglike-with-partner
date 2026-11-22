using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 토끼 성장 노드의 전투 효과 (스테이지 단위 싱글톤, StageDirector 오브젝트에 붙음 → 씬과 함께 정리).
/// 메인(관통 당근/소형 당근) 평타, 패시브(팅기는 당근/당근 꼬치/피흡), 감시자(폭격 시 당근 폭격). 수치는 RabbitGrowthConfig.rabbit.
/// </summary>
public class CarrotGrowth : Singleton<CarrotGrowth>
{
    class Skewer { public Enemy enemy; public float until; public GameObject visual; }

    GrowthLoadout L;
    RabbitGrowthSettings S;
    ThrownProjectile thrownPrefab;
    Pool<CarrotShot> shots;
    readonly List<Skewer> skewers = new List<Skewer>(8);
    readonly Stack<Skewer> freeSkewers = new Stack<Skewer>(8);
    readonly Stack<GameObject> freeVisuals = new Stack<GameObject>(8);
    static readonly Quaternion StuckTilt = Quaternion.Euler(55f, 0f, 20f);

    /// <summary>던진 당근 착지 콜백 (캐시: 던질 때마다 할당하지 않음).</summary>
    public System.Action<Vector3, float> ThrownLanded, BouncedLanded;

    public static CarrotGrowth Ensure(GrowthLoadout l, ThrownProjectile thrown)
    {
        if (!StageDirector.I || l == null || !l.Any || !(l.config is RabbitGrowthConfig)) return null;
        var g = I ? I : StageDirector.I.gameObject.AddComponent<CarrotGrowth>();
        g.Setup(l, thrown);
        return g;
    }

    void Setup(GrowthLoadout l, ThrownProjectile thrown)
    {
        if (L != null && L.carrotRain) WatcherSkills.BombardCast -= OnBombard;
        L = l; S = ((RabbitGrowthConfig)l.config).rabbit; thrownPrefab = thrown;
        ThrownLanded ??= (p, d) => OnLand(p, d, true);
        BouncedLanded ??= (p, d) => OnLand(p, d, false);
        if (shots == null && S.shotPrefab) shots = new Pool<CarrotShot>(S.shotPrefab, transform, 12);
        if (L.carrotRain) WatcherSkills.BombardCast += OnBombard;
    }

    protected override void OnDestroy()
    {
        WatcherSkills.BombardCast -= OnBombard;
        StageDirector.EnemyDied -= OnEnemyDied;
        base.OnDestroy();
    }

    void OnEnable() => StageDirector.EnemyDied += OnEnemyDied;
    void OnDisable() => StageDirector.EnemyDied -= OnEnemyDied;

    // ================= 메인 평타 =================

    /// <summary>선택한 메인 노드가 있으면 그 방식으로 발사하고 true (없으면 기존 포물선).</summary>
    public bool FireMain(Vector3 from, Vector3 target, PlayerStats s)
    {
        if (L.main == GrowthEffect.None || shots == null) return false;
        Vector3 d = target - from; d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        d.Normalize();
        var spec = new CarrotShot.Spec
        {
            speed = Mathf.Max(4f, s.projectileSpeed * S.shotSpeedMul), range = s.attackRange + 1.5f,
            hitRadius = S.shotHitRadius, falloff = 1f, nearMul = 1f, farMul = 1f, pierce = 1, canBounce = true,
        };
        int count; float spread;
        if (L.main == GrowthEffect.PierceCarrot)
        {
            spec.pierceMode = true; spec.pierce = Mathf.Max(1, S.pierceCount); spec.falloff = S.pierceFalloff;
            spec.damage = s.damage * S.pierceDamageMul;
            count = s.projectileCount; spread = s.spreadAngle;
        }
        else // SmallCarrots
        {
            spec.smallMode = true; spec.damage = s.damage; spec.nearMul = S.smallNearMul; spec.farMul = S.smallFarMul;
            count = Mathf.Max(1, S.smallCount) + s.projectileCount - 1; spread = S.smallSpread;
        }
        float start = -spread * (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            spec.dir = Quaternion.Euler(0f, start + spread * i, 0f) * d;
            shots.Get(from, Quaternion.identity).Init(spec);
        }
        return true;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt > 0f) CarrotShot.TickAll(dt);
        TickSkewers();
    }

    // ================= 적중 효과 =================

    /// <summary>평타가 적에게 맞음 (피해 전). 꼬친 적이면 폭발, 아니면 꼬치/튕김 확률, 피흡.</summary>
    public void OnHit(Enemy e, Vector3 at, float dmg, bool canBounce, bool pierceMode, bool smallMode)
    {
        if (L.lifesteal) Heal(dmg * S.lifestealFrac);
        int k = SkewerIndex(e);
        if (k >= 0) { Explode(k); return; } // 꼬친 적이 평타를 한 번 더 맞음 → 범위 폭발

        float skewerChance = pierceMode ? S.pierceSkewerChance : L.skewer ? S.skewerChance : 0f;
        if (skewerChance > 0f && Random.value < skewerChance)
        {
            if (pierceMode && L.skewer) Blast(e.transform.position); // 관통 당근 + 당근 꼬치 패시브: 꼬치는 순간 폭발
            else AddSkewer(e);
        }

        float bounceChance = smallMode ? S.smallBounceChance + (L.bounce ? S.smallBounceBonus : 0f) : L.bounce ? S.bounceChance : 0f;
        if (canBounce && bounceChance > 0f && Random.value < bounceChance) Bounce(e, at, dmg * S.bounceDamageMul);
    }

    /// <summary>던진 당근(기본 포물선) 착지: 착지점에 가장 가까운 적을 맞은 적으로 처리.</summary>
    void OnLand(Vector3 p, float dmg, bool canBounce)
    {
        var e = Nearest(p, 1.6f, null);
        if (e) OnHit(e, p, dmg, canBounce, false, false);
    }

    void Bounce(Enemy from, Vector3 at, float dmg)
    {
        var to = Nearest(from.transform.position, S.bounceRadius, from);
        if (!to) return;
        Vector3 a = from.transform.position + Vector3.up * 0.6f, b = to.transform.position;
        if (L.main != GrowthEffect.None && shots != null)
        {
            Vector3 d = b - a; d.y = 0f;
            var spec = new CarrotShot.Spec
            {
                dir = d.normalized, speed = 16f, range = d.magnitude + 1f, hitRadius = S.shotHitRadius,
                damage = dmg, falloff = 1f, nearMul = 1f, farMul = 1f, pierce = 1,
            };
            shots.Get(a, Quaternion.identity).Init(spec);
        }
        else if (thrownPrefab && StageDirector.I)
        {
            b.y = 0f;
            var tp = StageDirector.I.Throw(thrownPrefab, a, b, 0.35f, 1.2f, dmg, 0.9f, S.skewerColor);
            if (tp) tp.Landed = BouncedLanded; // 튕긴 당근은 다시 튕기지 않음
        }
    }

    static Enemy Nearest(Vector3 p, float radius, Enemy exclude)
    {
        Enemy best = null; float bestSq = radius * radius;
        var list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == exclude) continue;
            Vector3 d = e.transform.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    void Heal(float v)
    {
        var st = GameManager.I.Stats;
        if (v > 0f && Ward.I && Ward.I.Alive) st.wardHp = Mathf.Min(st.wardMaxHp, st.wardHp + v);
    }

    // ================= 당근 꼬치 =================

    int SkewerIndex(Enemy e)
    {
        for (int i = 0; i < skewers.Count; i++) if (skewers[i].enemy == e) return i;
        return -1;
    }

    void AddSkewer(Enemy e)
    {
        var sk = freeSkewers.Count > 0 ? freeSkewers.Pop() : new Skewer();
        sk.enemy = e; sk.until = Time.time + S.skewerDuration;
        sk.visual = freeVisuals.Count > 0 ? freeVisuals.Pop() : (S.shotPrefab ? Instantiate(S.shotPrefab.Visual, transform) : null);
        if (sk.visual) { sk.visual.SetActive(true); sk.visual.transform.rotation = StuckTilt; }
        skewers.Add(sk);
    }

    void RemoveSkewer(int i)
    {
        var sk = skewers[i];
        int last = skewers.Count - 1;
        skewers[i] = skewers[last]; skewers.RemoveAt(last);
        if (sk.visual) { sk.visual.SetActive(false); freeVisuals.Push(sk.visual); }
        sk.enemy = null; sk.visual = null;
        freeSkewers.Push(sk);
    }

    void Explode(int i)
    {
        Vector3 p = skewers[i].enemy.transform.position;
        RemoveSkewer(i);
        Blast(p);
    }

    void Blast(Vector3 p)
    {
        var st = GameManager.I.Stats;
        float r = S.skewerRadius * st.areaMul;
        p = Ground.Snap(p);
        Vfx.Explosion(p, r, S.skewerColor);
        SfxManager.Play(SfxManager.Creature?.impact, p);
        StageDirector.I.DamageArea(p, r, st.damage * S.skewerDamageMul);
    }

    void TickSkewers()
    {
        float now = Time.time;
        for (int i = skewers.Count - 1; i >= 0; i--)
        {
            var sk = skewers[i];
            if (!sk.enemy || !sk.enemy.isActiveAndEnabled || now > sk.until) { RemoveSkewer(i); continue; }
            if (sk.visual) sk.visual.transform.position = sk.enemy.transform.position + Vector3.up * 0.9f;
        }
    }

    void OnEnemyDied(Enemy e, bool counted)
    {
        int k = SkewerIndex(e);
        if (k >= 0) RemoveSkewer(k); // 풀에서 재사용되기 전에 꼬치 정리
    }

    // ================= 감시자: 당근 폭격 =================

    void OnBombard(WatcherSkillType t)
    {
        if (isActiveAndEnabled && thrownPrefab) StartCoroutine(CarrotRain());
    }

    IEnumerator CarrotRain()
    {
        var st = GameManager.I.Stats;
        var stage = StageDirector.I;
        float r = S.rainRadius * st.areaMul, dmg = st.damage * S.rainDamageMul, dd = r * 2f;
        var wait = new WaitForSeconds(S.rainInterval);
        for (int i = 0; i < S.rainCount; i++)
        {
            Vector3 p = Ground.Snap(Arena.Clamp(Arena.RandomPoint(S.rainArea)));
            stage.SpawnFx(p + Vector3.up * 0.05f, new Vector3(0.2f, 0.02f, 0.2f), new Vector3(dd, 0.02f, dd), S.rainFallTime, S.rainColor); // 떨어질 자리 표시
            stage.Throw(thrownPrefab, p + Vector3.up * S.rainHeight, p, S.rainFallTime, 0f, dmg, r, S.rainColor);
            yield return wait;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>스테이지(씬) 단위 싱글톤: 풀 관리, 폴링 스폰, 일괄 Tick, 처치 수, 포털.</summary>
public class StageDirector : Singleton<StageDirector>
{
    [SerializeField] StageGenerator generator;

    static readonly Collider[] areaHits = new Collider[256];

    public float ArenaRadius { get; private set; }
    public int EnemyMask { get; private set; }
    public int Kills { get; private set; }
    public bool PortalOpen { get; private set; }
    public int KillTarget { get; private set; }
    public StageTheme Theme { get; private set; }
    public Vector3 PortalPosition => portal ? portal.position : Vector3.zero;
    /// <summary>작은 생명체 길찾기 격자 (벽/장애물 회피).</summary>
    public GridNav Nav { get; private set; }

    GameManager gm;
    GameConfig cfg;
    // 적 종류별 풀 (이번 스테이지에 등장 가능한 종류만)
    readonly System.Collections.Generic.List<EnemyDef> enemyDefs = new System.Collections.Generic.List<EnemyDef>(8);
    readonly System.Collections.Generic.List<Pool<Enemy>> enemyPools = new System.Collections.Generic.List<Pool<Enemy>>(8);
    float enemyWeightTotal;
    Pool<Projectile> projectiles;
    Pool<XpGem> gems;
    Pool<Fx> fx;
    Pool<ResourcePickup> resourcePickups;
    Transform portal;
    float spawnTimer, spawnInterval, enemyHp, enemySpeed, enemyDamage;
    int spawnBatch;
    bool hazards;
    float hazardTimer;
    readonly List<Vector4> pendingHazards = new List<Vector4>(8); // xyz=위치, w=남은 시간

    protected override void Awake()
    {
        base.Awake();
        Enemy.Active.Clear(); Projectile.Active.Clear(); XpGem.Active.Clear(); Fx.Active.Clear();

        gm = GameManager.I;
        cfg = gm.Config;
        EnemyMask = LayerMask.GetMask("Enemy");
        ArenaRadius = generator.Radius;
        Arena.Radius = ArenaRadius;
        generator.Build();
        Nav = new GridNav(ArenaRadius, 1.2f, 0.4f, LayerMask.GetMask("Default"));
        Theme = GetComponent<StageTheme>();
        KillTarget = Mathf.Max(1, Mathf.RoundToInt(gm.KillTarget * (Theme ? Theme.killTargetMul : 1f)));
        int pl = LayerMask.NameToLayer("Player"), en = LayerMask.NameToLayer("Enemy");
        int wd = LayerMask.NameToLayer("Ward");
        if (pl >= 0 && en >= 0) Physics.IgnoreLayerCollision(pl, en); // 플레이어는 형태 없는 존재 (충돌 없음)
        if (wd >= 0)
        {
            if (en >= 0) Physics.IgnoreLayerCollision(wd, en); // 생명체는 적에게 끼이지 않게 (접촉 피해는 거리 판정)
            if (pl >= 0) Physics.IgnoreLayerCollision(wd, pl);
        }
        Enemy.AggroSq = cfg.enemyAggroRadius * cfg.enemyAggroRadius;
        gameObject.AddComponent<BombardSkill>();
        if (cfg.wardPrefab) Instantiate(cfg.wardPrefab, new Vector3(0f, 0f, -1.8f), Quaternion.identity);

        int s = gm.StageIndex;
        spawnInterval = Mathf.Max(cfg.minSpawnInterval, cfg.spawnInterval + cfg.spawnIntervalPerStage * s);
        enemyHp = (cfg.enemyHp + cfg.enemyHpPerStage * s) * (1f + gm.ModValue(ModifierType.EnemyHp));
        enemySpeed = (cfg.enemySpeed + cfg.enemySpeedPerStage * s) * (1f + gm.ModValue(ModifierType.EnemySpeed));
        enemyDamage = cfg.enemyDamage * (1f + gm.ModValue(ModifierType.EnemyDamage));
        spawnBatch = cfg.spawnBatch + (int)gm.ModValue(ModifierType.MoreEnemies);
        hazards = gm.Has(ModifierType.Hazards);
        var comp = gm.Companion;
        if (comp != null && comp.prefab) Instantiate(comp.prefab);

        BuildEnemyPools(s);
        projectiles = new Pool<Projectile>(cfg.projectilePrefab, transform, 64);
        gems = new Pool<XpGem>(cfg.gemPrefab, transform, 128);
        fx = new Pool<Fx>(cfg.fxPrefab, transform, 16);
        if (cfg.resourcePickupPrefab) resourcePickups = new Pool<ResourcePickup>(cfg.resourcePickupPrefab, transform, 4);
        ResourcePickup.Active.Clear();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || gm.State != GameState.Playing || gm.Transitioning) return; // 씬 전환 중인 이전 스테이지는 멈춤

        if (!PortalOpen && (spawnTimer -= dt) <= 0f)
        {
            spawnTimer = spawnInterval;
            for (int i = 0; i < spawnBatch && Enemy.Active.Count < cfg.maxAlive; i++) SpawnEnemy();
        }

        if (hazards) TickHazards(dt);
        Enemy.TickAll(dt);
        Projectile.TickAll(dt);
        XpGem.TickAll(dt);
        Fx.TickAll(dt);
        ResourcePickup.TickAll(dt);

    }

    void SpawnEnemy()
    {
        // 생명체를 중심으로 사방에서 몰려오도록
        Vector3 p = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
        Vector3 pp = PlayerController.I.Position;
        float max = ArenaRadius - 1f;
        for (int a = 0; a < 6; a++)
        {
            Vector2 d = Random.insideUnitCircle.normalized * cfg.spawnDistance;
            var pos = new Vector3(p.x + d.x, 0f, p.z + d.y);
            if (pos.sqrMagnitude > max * max) pos = pos.normalized * max;
            if ((pos - p).sqrMagnitude < 64f || (pos - pp).sqrMagnitude < 64f) continue; // 근처 스폰 방지
            int k = PickEnemyType();
            enemyPools[k].Get(pos, Quaternion.identity).Init(enemyHp, enemySpeed, enemyDamage, enemyDefs[k]);
            return;
        }
    }

    void BuildEnemyPools(int stageIndex)
    {
        enemyWeightTotal = 0f;
        if (cfg.enemyTypes != null)
            foreach (var d in cfg.enemyTypes)
            {
                if (d == null || d.prefab == null || d.weight <= 0f || stageIndex < d.minStage) continue;
                enemyDefs.Add(d);
                enemyWeightTotal += d.weight;
            }
        if (enemyDefs.Count == 0) { enemyDefs.Add(null); enemyWeightTotal = 1f; } // 구버전: 기본 적만
        int prewarm = Mathf.Max(8, cfg.maxAlive / enemyDefs.Count);
        foreach (var d in enemyDefs)
            enemyPools.Add(new Pool<Enemy>(d != null ? d.prefab : cfg.enemyPrefab, transform, prewarm));
    }

    int PickEnemyType()
    {
        if (enemyDefs.Count == 1) return 0;
        float r = Random.value * enemyWeightTotal;
        for (int i = 0; i < enemyDefs.Count - 1; i++) { r -= enemyDefs[i].weight; if (r <= 0f) return i; }
        return enemyDefs.Count - 1;
    }

    public void OnEnemyKilled(Vector3 pos)
    {
        Kills++;
        gems.Get(pos, Quaternion.Euler(45f, 0f, 45f));
        // 이 맵의 자원을 가끔 떨어뜨림
        if (resourcePickups != null && Theme && Theme.resource != ResourceType.None && Random.value < cfg.resourceDropChance)
        {
            var def = cfg.Resource(Theme.resource);
            resourcePickups.Get(pos + Vector3.up * 0.1f, Quaternion.identity).Init(Theme.resource, def != null ? def.color : Color.white);
        }
        if (!PortalOpen && Kills >= KillTarget) OpenPortal();
    }

    void OpenPortal()
    {
        PortalOpen = true;
        portal = Instantiate(cfg.portalPrefab, Vector3.zero, Quaternion.identity, transform).transform;
        // 생명체가 포털에 도착하면 다음 스테이지 (Ward 참고)
    }

    /// <summary>난이도 디버프: 플레이어 주변에 예고 후 폭발하는 위험 장판.</summary>
    void TickHazards(float dt)
    {
        if ((hazardTimer -= dt) <= 0f)
        {
            hazardTimer = cfg.hazardInterval;
            Vector2 o = Random.insideUnitCircle * 5f;
            Vector3 center = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
            var p = Arena.Clamp(center + new Vector3(o.x, 0f, o.y)); p.y = 0f;
            float d = cfg.hazardRadius * 2f;
            SpawnFx(p + Vector3.up * 0.02f, new Vector3(d, 0.02f, d), new Vector3(d, 0.02f, d), cfg.hazardDelay, new Color(0.6f, 0f, 0.4f));
            pendingHazards.Add(new Vector4(p.x, 0f, p.z, cfg.hazardDelay));
        }
        Vector3 pp = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
        for (int i = pendingHazards.Count - 1; i >= 0; i--)
        {
            var h = pendingHazards[i];
            h.w -= dt;
            if (h.w > 0f) { pendingHazards[i] = h; continue; }
            float dx = pp.x - h.x, dz = pp.z - h.z;
            if (dx * dx + dz * dz < cfg.hazardRadius * cfg.hazardRadius)
            {
                if (Ward.I != null) Ward.I.TakeDamage(cfg.hazardDamage);
                else PlayerController.I.TakeDamage(cfg.hazardDamage);
            }
            SpawnFx(new Vector3(h.x, 1.5f, h.z), new Vector3(1f, 1.5f, 1f), new Vector3(cfg.hazardRadius * 2f, 0.1f, cfg.hazardRadius * 2f), 0.2f, new Color(1f, 0.2f, 0.6f));
            pendingHazards.RemoveAt(i);
        }
    }

    public void FireProjectile(Vector3 pos, Vector3 dir, float speed, float dmg)
        => projectiles.Get(pos, Quaternion.LookRotation(dir)).Init(dir, speed, dmg);

    public void SpawnFx(Vector3 pos, Vector3 fromScale, Vector3 toScale, float dur, Color c)
        => fx.Get(pos, Quaternion.identity).Play(fromScale, toScale, dur, c);

    public void SpawnFx(Vector3 pos, Vector3 fromScale, Vector3 toScale, float dur, Color c, Quaternion rot)
        => fx.Get(pos, rot).Play(fromScale, toScale, dur, c);

    /// <summary>범위 내 적을 중심에서 방사형으로 밀거나(speed &gt; 0) 끌어당김(speed &lt; 0).</summary>
    public void RadialForce(Vector3 center, float radius, float speed, float dmg)
    {
        int n = Physics.OverlapSphereNonAlloc(center, radius, areaHits, EnemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            if (!areaHits[i].TryGetComponent(out Enemy e) || !e.isActiveAndEnabled) continue;
            Vector3 d = e.transform.position - center; d.y = 0f;
            if (dmg > 0f) e.Hit(dmg, center);
            if (e.isActiveAndEnabled) e.Push(d.sqrMagnitude > 0.01f ? d.normalized * speed : Vector3.zero);
        }
    }

    /// <summary>범위 내 적에게 피해 후 지정 속도로 밀어냄.</summary>
    public void PushArea(Vector3 center, float radius, Vector3 velocity, float dmg)
    {
        int n = Physics.OverlapSphereNonAlloc(center, radius, areaHits, EnemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            if (!areaHits[i].TryGetComponent(out Enemy e) || !e.isActiveAndEnabled) continue;
            if (dmg > 0f) e.Hit(dmg, center);
            if (e.isActiveAndEnabled) e.Push(velocity);
        }
    }

    public void DamageArea(Vector3 center, float radius, float dmg)
    {
        int n = Physics.OverlapSphereNonAlloc(center, radius, areaHits, EnemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
            if (areaHits[i].TryGetComponent(out Enemy e) && e.isActiveAndEnabled) e.Hit(dmg, center);
    }

    /// <summary>자동 공격 대상: 플레이어 사거리 안에서 생명체에 가장 가까운 적 (디펜스 우선).</summary>
    public Enemy AttackTarget(Vector3 playerPos, float range)
    {
        if (Ward.I == null) return NearestEnemy(playerPos, range);
        Vector3 w = Ward.I.Position;
        Enemy best = null;
        float bestSq = float.MaxValue, rangeSq = range * range;
        var list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 e = list[i].transform.position;
            if ((e - playerPos).sqrMagnitude > rangeSq) continue;
            float sq = (e - w).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = list[i]; }
        }
        return best;
    }

    public Enemy NearestEnemy(Vector3 p, float range)
    {
        Enemy best = null;
        float bestSq = range * range;
        var list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            float sq = (list[i].transform.position - p).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = list[i]; }
        }
        return best;
    }
}

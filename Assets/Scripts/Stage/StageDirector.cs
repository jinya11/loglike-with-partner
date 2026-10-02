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

    GameManager gm;
    GameConfig cfg;
    Pool<Enemy> enemies;
    Pool<Projectile> projectiles;
    Pool<XpGem> gems;
    Pool<Fx> fx;
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
        Theme = GetComponent<StageTheme>();
        KillTarget = Mathf.Max(1, Mathf.RoundToInt(gm.KillTarget * (Theme ? Theme.killTargetMul : 1f)));
        int pl = LayerMask.NameToLayer("Player"), en = LayerMask.NameToLayer("Enemy");
        if (pl >= 0 && en >= 0) Physics.IgnoreLayerCollision(pl, en); // 접촉 피해는 거리 판정, 몸으로 막히지 않게

        int s = gm.StageIndex;
        spawnInterval = Mathf.Max(cfg.minSpawnInterval, cfg.spawnInterval + cfg.spawnIntervalPerStage * s);
        enemyHp = (cfg.enemyHp + cfg.enemyHpPerStage * s) * (1f + gm.ModValue(ModifierType.EnemyHp));
        enemySpeed = (cfg.enemySpeed + cfg.enemySpeedPerStage * s) * (1f + gm.ModValue(ModifierType.EnemySpeed));
        enemyDamage = cfg.enemyDamage * (1f + gm.ModValue(ModifierType.EnemyDamage));
        spawnBatch = cfg.spawnBatch + (int)gm.ModValue(ModifierType.MoreEnemies);
        hazards = gm.Has(ModifierType.Hazards);
        var comp = gm.Companion;
        if (comp != null && comp.prefab) Instantiate(comp.prefab);

        enemies = new Pool<Enemy>(cfg.enemyPrefab, transform, cfg.maxAlive);
        projectiles = new Pool<Projectile>(cfg.projectilePrefab, transform, 64);
        gems = new Pool<XpGem>(cfg.gemPrefab, transform, 128);
        fx = new Pool<Fx>(cfg.fxPrefab, transform, 16);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || gm.State != GameState.Playing) return;

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

    }

    void SpawnEnemy()
    {
        Vector3 p = PlayerController.I.Position;
        float max = ArenaRadius - 1f;
        for (int a = 0; a < 6; a++)
        {
            Vector2 d = Random.insideUnitCircle.normalized * cfg.spawnDistance;
            var pos = new Vector3(p.x + d.x, 0f, p.z + d.y);
            if (pos.sqrMagnitude > max * max) pos = pos.normalized * max;
            if ((pos - p).sqrMagnitude < 64f) continue; // 플레이어 근처 스폰 방지
            enemies.Get(pos, Quaternion.identity).Init(enemyHp, enemySpeed, enemyDamage);
            return;
        }
    }

    public void OnEnemyKilled(Vector3 pos)
    {
        Kills++;
        gems.Get(pos, Quaternion.Euler(45f, 0f, 45f));
        if (!PortalOpen && Kills >= KillTarget) OpenPortal();
    }

    void OpenPortal()
    {
        PortalOpen = true;
        portal = Instantiate(cfg.portalPrefab, Vector3.zero, Quaternion.identity, transform).transform;
        portal.gameObject.AddComponent<StagePortal>(); // F로 이동
    }

    /// <summary>난이도 디버프: 플레이어 주변에 예고 후 폭발하는 위험 장판.</summary>
    void TickHazards(float dt)
    {
        if ((hazardTimer -= dt) <= 0f)
        {
            hazardTimer = cfg.hazardInterval;
            Vector2 o = Random.insideUnitCircle * 5f;
            var p = Arena.Clamp(PlayerController.I.Position + new Vector3(o.x, 0f, o.y)); p.y = 0f;
            float d = cfg.hazardRadius * 2f;
            SpawnFx(p + Vector3.up * 0.02f, new Vector3(d, 0.02f, d), new Vector3(d, 0.02f, d), cfg.hazardDelay, new Color(0.6f, 0f, 0.4f));
            pendingHazards.Add(new Vector4(p.x, 0f, p.z, cfg.hazardDelay));
        }
        Vector3 pp = PlayerController.I.Position;
        for (int i = pendingHazards.Count - 1; i >= 0; i--)
        {
            var h = pendingHazards[i];
            h.w -= dt;
            if (h.w > 0f) { pendingHazards[i] = h; continue; }
            float dx = pp.x - h.x, dz = pp.z - h.z;
            if (dx * dx + dz * dz < cfg.hazardRadius * cfg.hazardRadius && !PlayerController.I.Airborne)
                PlayerController.I.TakeDamage(cfg.hazardDamage);
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

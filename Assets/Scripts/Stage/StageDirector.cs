using System.Collections.Generic;
using UnityEngine;

/// <summary>스테이지(씬) 단위 싱글톤: 풀 관리, 폴링 스폰, 일괄 Tick, 처치 수, 포털.</summary>
public class StageDirector : Singleton<StageDirector>
{
    [SerializeField] StageGenerator generator;

    static readonly Collider[] areaHits = new Collider[256];

    /// <summary>중심에서 경계까지 거리 (원형: 반지름, 지역 스테이지: 보더 반 변. 보더를 넓히면 함께 커짐).</summary>
    public float ArenaRadius => Arena.Extent;
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
    // 던지는 물체 풀 (프리팹별, 생명체 공격이 처음 던질 때 생성)
    readonly Dictionary<ThrownProjectile, Pool<ThrownProjectile>> thrownPools = new Dictionary<ThrownProjectile, Pool<ThrownProjectile>>(4);
    Transform portal;
    float spawnTimer, spawnInterval, enemyHp, enemySpeed, enemyDamage;
    int spawnBatch;
    bool hazards;
    float hazardTimer;
    readonly List<Vector4> pendingHazards = new List<Vector4>(8); // xyz=위치, w=남은 시간

    protected override void Awake()
    {
        base.Awake();
        Enemy.Active.Clear(); Projectile.Active.Clear(); XpGem.Active.Clear(); Fx.Active.Clear(); ThrownProjectile.Active.Clear();

        gm = GameManager.I;
        cfg = gm.Config;
        EnemyMask = LayerMask.GetMask("Enemy");
        var border = generator.Border;
        if (border)
        {
            border.Apply(); // 저장된 열린 칸까지 반영해 Arena 설정
            if (!border.GetComponentInChildren<BorderUnlock>()) { var u = new GameObject("BorderUnlock"); u.transform.SetParent(border.transform, false); u.AddComponent<BorderUnlock>(); }
            border.AreaOpened += OnAreaOpened;
            BorderUnlock.Unlocked += OnUnlockStarted;
            FitRegionToCells();
        }
        else Arena.SetCircle(generator.Radius);
        generator.Build();
        Nav = new GridNav(ArenaRadius, 1.2f, 0.4f, LayerMask.GetMask("Default"), border);
        Theme = GetComponent<StageTheme>();
        UpdateKillTarget(); // 저장된 열린 구역만큼 늘어남
        int pl = LayerMask.NameToLayer("Player"), en = LayerMask.NameToLayer("Enemy");
        int wd = LayerMask.NameToLayer("Ward");
        if (pl >= 0 && en >= 0) Physics.IgnoreLayerCollision(pl, en); // 플레이어는 형태 없는 존재 (충돌 없음)
        if (wd >= 0)
        {
            if (en >= 0) Physics.IgnoreLayerCollision(wd, en); // 생명체는 적에게 끼이지 않게 (접촉 피해는 거리 판정)
            if (pl >= 0) Physics.IgnoreLayerCollision(wd, pl);
        }
        Enemy.AggroSq = cfg.enemy.enemyAggroRadius * cfg.enemy.enemyAggroRadius;
        gameObject.AddComponent<BombardSkill>();
        gameObject.AddComponent<Vfx>();
        new GameObject("CommandHud", typeof(RectTransform)).AddComponent<CommandHud>().transform.SetParent(transform, false);
        if (Hud.I == null && cfg.stage.hudPrefab) Instantiate(cfg.stage.hudPrefab); // 첫 스테이지에서 한 번만 (이후 유지)
        if (cfg.ward.wardPrefab) Instantiate(cfg.ward.wardPrefab, new Vector3(0f, 0f, -1.8f), Quaternion.identity);

        int s = gm.StageIndex;
        spawnInterval = Mathf.Max(cfg.stage.minSpawnInterval, cfg.stage.spawnInterval + cfg.stage.spawnIntervalPerStage * s);
        enemyHp = (cfg.enemy.enemyHp + cfg.enemy.enemyHpPerStage * s) * (1f + gm.ModValue(ModifierType.EnemyHp));
        enemySpeed = (cfg.enemy.enemySpeed + cfg.enemy.enemySpeedPerStage * s) * (1f + gm.ModValue(ModifierType.EnemySpeed));
        enemyDamage = cfg.enemy.enemyDamage * (1f + gm.ModValue(ModifierType.EnemyDamage));
        spawnBatch = cfg.stage.spawnBatch + (int)gm.ModValue(ModifierType.MoreEnemies);
        hazards = gm.Has(ModifierType.Hazards);
        var comp = gm.Companion;
        if (comp != null && comp.prefab) Instantiate(comp.prefab);

        BuildEnemyPools(s);
        projectiles = new Pool<Projectile>(cfg.player.projectilePrefab, transform, 64);
        gems = new Pool<XpGem>(cfg.player.gemPrefab, transform, 128);
        fx = new Pool<Fx>(cfg.stage.fxPrefab, transform, 16);
        if (cfg.meta.resourcePickupPrefab) resourcePickups = new Pool<ResourcePickup>(cfg.meta.resourcePickupPrefab, transform, 4);
        ResourcePickup.Active.Clear();
    }

    protected override void OnDestroy()
    {
        BorderUnlock.Unlocked -= OnUnlockStarted;
        base.OnDestroy();
    }

    /// <summary>보더를 열기 시작할 때: 새 구역 자리의 바깥 풍경(언덕 등)을 미리 치우고, 처치 목표를 늘림.</summary>
    void OnUnlockStarted(Rect r, Vector2Int dir)
    {
        UpdateKillTarget();
        var outside = GameObject.Find("Arena/Outside");
        if (!outside) return;
        r.xMin -= 25f; r.yMin -= 25f; r.xMax += 25f; r.yMax += 25f;
        foreach (Transform t in outside.transform)
            if (r.Contains(new Vector2(t.position.x, t.position.z))) t.gameObject.SetActive(false);
    }

    /// <summary>보더가 열려 새 구역이 다 생겼을 때: 그 구역에 장애물 배치, 바닥/풍경 정리, 길찾기 격자 다시 만들기.</summary>
    void OnAreaOpened(Rect r)
    {
        generator.BuildArea(r, false);
        FitRegionToCells();
        Nav = new GridNav(ArenaRadius, 1.2f, 0.4f, LayerMask.GetMask("Default"), true);
    }

    /// <summary>처치 목표 = 기본 × (1 + 열린 구역 넓이 / 중앙 넓이 × killPerOpenedArea). 보더가 열릴수록 늘어남.</summary>
    void UpdateKillTarget()
    {
        float mul = Theme ? Theme.killTargetMul : 1f;
        var border = generator.Border;
        if (border && border.OpenCount > 0)
        {
            float baseArea = border.HalfSize * border.HalfSize * 4f, extra = 0f;
            for (int i = 1; i < border.Areas.Count; i++) extra += border.Areas[i].width * border.Areas[i].height;
            mul *= 1f + extra / baseArea * cfg.stage.killPerOpenedArea;
        }
        KillTarget = Mathf.Max(1, Mathf.RoundToInt(gm.KillTarget * mul));
    }

    /// <summary>열린 칸에 맞춰 바닥을 넓히고, 칸 안에 들어온 바깥 풍경(언덕 등)은 숨김.</summary>
    void FitRegionToCells()
    {
        var b = Arena.Bounds;
        var floor = GameObject.Find("Arena/Floor");
        if (floor && floor.TryGetComponent(out Renderer fr))
        {
            float need = Mathf.Max(Mathf.Abs(b.xMin), Mathf.Abs(b.xMax), Mathf.Abs(b.yMin), Mathf.Abs(b.yMax)) + 60f;
            float have = Mathf.Max(fr.bounds.extents.x, fr.bounds.extents.z);
            if (have < need) { var s = floor.transform.localScale; float k = need / have; floor.transform.localScale = new Vector3(s.x * k, s.y, s.z * k); }
        }
        var outside = GameObject.Find("Arena/Outside");
        if (outside)
            foreach (Transform t in outside.transform)
                if (t.gameObject.activeSelf && (Arena.Contains(t.position) || Arena.EdgeDistance(t.position) < 25f))
                    t.gameObject.SetActive(false);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || gm.State != GameState.Playing || gm.Transitioning) return; // 씬 전환 중인 이전 스테이지는 멈춤

        if (!PortalOpen && (spawnTimer -= dt) <= 0f)
        {
            spawnTimer = spawnInterval;
            for (int i = 0; i < spawnBatch && Enemy.Active.Count < cfg.stage.maxAlive; i++) SpawnEnemy();
        }

        if (hazards) TickHazards(dt);
        Enemy.TickAll(dt);
        Projectile.TickAll(dt);
        ThrownProjectile.TickAll(dt);
        XpGem.TickAll(dt);
        Fx.TickAll(dt);
        ResourcePickup.TickAll(dt);

    }

    void SpawnEnemy()
    {
        // 생명체를 중심으로 사방에서 몰려오도록
        Vector3 p = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
        Vector3 pp = PlayerController.I.Position;
        // 보더를 한 번이라도 열었으면 활성화된 보더 전체에 넓게 분포 (넓이 비례)
        bool wide = generator.Border && generator.Border.OpenCount > 0;
        for (int a = 0; a < 6; a++)
        {
            Vector2 d = Random.insideUnitCircle.normalized * cfg.stage.spawnDistance;
            var pos = Arena.Clamp(wide ? Arena.RandomPoint(0.95f) : new Vector3(p.x + d.x, 0f, p.z + d.y), 1.5f); // 경계(보더) 밖에는 생성하지 않음
            if ((pos - p).sqrMagnitude < 64f || (pos - pp).sqrMagnitude < 64f) continue; // 근처 스폰 방지
            int k = PickEnemyType();
            enemyPools[k].Get(pos, Quaternion.identity).Init(enemyHp, enemySpeed, enemyDamage, enemyDefs[k]);
            return;
        }
    }

    void BuildEnemyPools(int stageIndex)
    {
        enemyWeightTotal = 0f;
        if (cfg.enemy.enemyTypes != null)
            foreach (var d in cfg.enemy.enemyTypes)
            {
                if (d == null || d.prefab == null || d.weight <= 0f || stageIndex < d.minStage) continue;
                enemyDefs.Add(d);
                enemyWeightTotal += d.weight;
            }
        if (enemyDefs.Count == 0) { enemyDefs.Add(null); enemyWeightTotal = 1f; } // 구버전: 기본 적만
        int prewarm = Mathf.Max(8, cfg.stage.maxAlive / enemyDefs.Count);
        foreach (var d in enemyDefs)
            enemyPools.Add(new Pool<Enemy>(d != null ? d.prefab : cfg.enemy.enemyPrefab, transform, prewarm));
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
        if (resourcePickups != null && Theme && Theme.resource != ResourceType.None && Random.value < cfg.meta.resourceDropChance)
        {
            var def = cfg.meta.Resource(Theme.resource);
            resourcePickups.Get(pos + Vector3.up * 0.1f, Quaternion.identity).Init(Theme.resource, def != null ? def.color : Color.white);
        }
        if (!PortalOpen && Kills >= KillTarget) OpenPortal();
    }

    void OpenPortal()
    {
        PortalOpen = true;
        Vector3 at = PortalSpot();
        portal = Instantiate(cfg.stage.portalPrefab, at, Quaternion.identity, transform).transform;
        Vfx.Pillar(at, new Color(0.6f, 0.4f, 1f));
        // 생명체가 포털에 도착하면 다음 스테이지 (Ward 참고)
    }

    /// <summary>포털 위치: 보더를 한 번이라도 열었으면 활성 보더 안의 임의 지점 (생명체에서 어느 정도 떨어진 곳), 아니면 중앙.</summary>
    Vector3 PortalSpot()
    {
        var border = generator.Border;
        if (!border || border.OpenCount == 0) return Vector3.zero;
        Vector3 w = Ward.I != null ? Ward.I.Position : Vector3.zero;
        Vector3 best = Vector3.zero; float bestSq = -1f;
        for (int i = 0; i < 12; i++)
        {
            var p = Arena.RandomPoint(0.8f);
            if (Nav != null) p = Nav.NearestFree(p);
            p.y = 0f;
            float sq = (p - w).sqrMagnitude;
            if (sq >= 25f * 25f) return p;
            if (sq > bestSq) { bestSq = sq; best = p; }
        }
        return best;
    }

    /// <summary>난이도 디버프: 플레이어 주변에 예고 후 폭발하는 위험 장판.</summary>
    void TickHazards(float dt)
    {
        if ((hazardTimer -= dt) <= 0f)
        {
            hazardTimer = cfg.stage.hazardInterval;
            Vector2 o = Random.insideUnitCircle * 5f;
            Vector3 center = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
            var p = Arena.Clamp(center + new Vector3(o.x, 0f, o.y)); p.y = 0f;
            float d = cfg.stage.hazardRadius * 2f;
            SpawnFx(p + Vector3.up * 0.02f, new Vector3(d, 0.02f, d), new Vector3(d, 0.02f, d), cfg.stage.hazardDelay, new Color(0.6f, 0f, 0.4f));
            pendingHazards.Add(new Vector4(p.x, 0f, p.z, cfg.stage.hazardDelay));
        }
        Vector3 pp = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
        for (int i = pendingHazards.Count - 1; i >= 0; i--)
        {
            var h = pendingHazards[i];
            h.w -= dt;
            if (h.w > 0f) { pendingHazards[i] = h; continue; }
            float dx = pp.x - h.x, dz = pp.z - h.z;
            if (dx * dx + dz * dz < cfg.stage.hazardRadius * cfg.stage.hazardRadius)
            {
                if (Ward.I != null) Ward.I.TakeDamage(cfg.stage.hazardDamage);
                else PlayerController.I.TakeDamage(cfg.stage.hazardDamage);
            }
            SpawnFx(new Vector3(h.x, 1.5f, h.z), new Vector3(1f, 1.5f, 1f), new Vector3(cfg.stage.hazardRadius * 2f, 0.1f, cfg.stage.hazardRadius * 2f), 0.2f, new Color(1f, 0.2f, 0.6f));
            Vfx.Explosion(new Vector3(h.x, 0f, h.z), cfg.stage.hazardRadius, new Color(1f, 0.2f, 0.6f));
            pendingHazards.RemoveAt(i);
        }
    }

    /// <summary>포물선으로 던지기 (생명체 공격). prefab이 없으면 기본 탄환으로 대신 쏨.</summary>
    public void Throw(ThrownProjectile prefab, Vector3 from, Vector3 to, float time, float arc, float dmg, float radius, Color c)
    {
        if (!prefab) { Vector3 d = to - from; d.y = 0f; FireProjectile(from, d.normalized, d.magnitude / Mathf.Max(0.05f, time), dmg); return; }
        if (!thrownPools.TryGetValue(prefab, out var pool)) thrownPools[prefab] = pool = new Pool<ThrownProjectile>(prefab, transform, 16);
        pool.Get(from, Quaternion.identity).Init(from, to, time, arc, dmg, radius, c);
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

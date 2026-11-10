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
        Enemy.Routed = false; Enemy.SpeedMul = 1f;
        Ground.Clear();
        Traits.Clear(); if (!gm.InBonus) Traits.AddRange(gm.ActiveTraits); // 누적 스테이지 증강 (보너스 스테이지는 없음)
        if (!cfg.stage.IsExpandable(gameObject.scene.name) && !gm.InBonus) SetupTerrain(); // 심연 외: 씬의 Terrain이 지면
        gm.XpMul = 1f;
        var border = generator.Border;
        // 보더를 넓혀가는 구조는 심연(StageConfig.expandableBorderScenes)에서만. 나머지는 중앙 칸 하나로 고정
        Expandable = border && cfg.stage.IsExpandable(gameObject.scene.name);
        if (border)
        {
            if (Expandable)
            {
                border.Apply(); // 저장된 열린 칸까지 반영해 Arena 설정
                if (!border.GetComponentInChildren<BorderUnlock>()) { var u = new GameObject("BorderUnlock"); u.transform.SetParent(border.transform, false); u.AddComponent<BorderUnlock>(); }
                border.AreaOpened += OnAreaOpened;
                BorderUnlock.Unlocked += OnUnlockStarted;
            }
            else border.LockToBase();
            FitRegionToCells();
        }
        else Arena.SetCircle(generator.Radius);
        generator.Build();
        if (!Expandable && !gm.InBonus) BuildLevelDesign();
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
        maxAlive = cfg.stage.maxAlive;
        resourceDropMul = 1f;
        ApplyTraits();
        UpdateKillTarget(); // 증강(속전속결) 반영
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
        if (I == this) { Ground.Clear(); Enemy.Routed = false; Enemy.SpeedMul = 1f; if (GameManager.I) GameManager.I.XpMul = 1f; }
        base.OnDestroy();
    }

    // ---- 심연 외 스테이지의 레벨 디자인 (Unity Terrain 지형 + 누적 스테이지 증강) ----
    /// <summary>보더를 넓혀가는 스테이지인지 (심연). 이 경우 포털/다음 스테이지 없음.</summary>
    public bool Expandable { get; private set; }
    /// <summary>이번 스테이지에 적용 중인 스테이지 증강 (런 동안 고른 것 전부, 누적).</summary>
    public readonly List<StageTraitDef> Traits = new List<StageTraitDef>(16);
    /// <summary>제한 시간 증강: 남은 시간 (없으면 음수).</summary>
    public float TimeLeft => Is(StageTraitType.TimeLimit) ? timeLeft : -1f;
    public bool Enraged { get; private set; }
    int maxAlive;
    float flowTimer;
    float resourceDropMul = 1f, eliteChance, eliteHpMul = 1f, timeLeft, enemyScale = 1f, killTargetMul = 1f;
    float meteorTimer, supplyTimer, waveTimer, treasureTimer;
    // 예고 후 터지는 유성: xyz = 위치, w = 남은 시간
    readonly List<Vector4> pendingMeteors = new List<Vector4>(8);

    StageTraitDef Get(StageTraitType t) { for (int i = 0; i < Traits.Count; i++) if (Traits[i].type == t) return Traits[i]; return null; }
    bool Is(StageTraitType t) => Get(t) != null;

    /// <summary>
    /// 지형: 씬의 Unity Terrain을 지면으로 등록 (심연 외). 원본 TerrainData(손으로 고친 지형)는 건드리지 않음 —
    /// 고지대 증강/런타임 랜덤 생성이 필요할 때만 복제본을 만들어 바꿈. 지형 충돌은 끔 (높이는 Ground가 계산).
    /// </summary>
    void SetupTerrain()
    {
        var t = FindAnyObjectByType<Terrain>();
        if (!t || !t.terrainData) return;
        var g = cfg.stage.ground;
        var high = Get(StageTraitType.Highlands);
        if (g.randomizeAtRuntime || high != null)
        {
            var td = Instantiate(t.terrainData);
            if (g.randomizeAtRuntime) TerrainHills.Generate(td, t.transform.position, g.hills, Random.Range(1, int.MaxValue));
            if (high != null) { var sz = td.size; sz.y *= Mathf.Max(0.1f, high.value); td.size = sz; }
            t.terrainData = td;
            if (t.TryGetComponent(out TerrainCollider tcd)) tcd.terrainData = td;
        }
        if (t.TryGetComponent(out TerrainCollider tc)) tc.enabled = false;
        Ground.SetTerrain(t, g.maxWalkSlope);
        if (g.randomizeAtRuntime || high != null) Ground.MarkRuntimeCopy();
    }

    /// <summary>증강 장판(독/회복/진흙), 배치물을 지면 높이로. 길찾기 격자를 만들기 전에 호출 (장판 비용 반영).</summary>
    void BuildLevelDesign()
    {
        StageTraitDef d;
        if ((d = Get(StageTraitType.Toxic)) != null) SpawnPools(Mathf.RoundToInt(d.value), ZoneType.Damage, d.value2, new Color(0.45f, 0.75f, 0.15f), "Trait_PoisonPool");
        if ((d = Get(StageTraitType.Sanctuary)) != null) SpawnPools(Mathf.RoundToInt(d.value), ZoneType.Heal, d.value2, new Color(0.4f, 1f, 0.7f), "Trait_HealSpring");
        if ((d = Get(StageTraitType.Mire)) != null) SpawnPools(Mathf.RoundToInt(d.value), ZoneType.Slow, d.value2, new Color(0.45f, 0.32f, 0.2f), "Trait_Mire");
        if (!Ground.HasTerrain) return;
        LiftToGround(generator.transform);
        LiftToGround(transform);
    }

    /// <summary>자식 배치물을 지형 높이로 올림 (장애물/장판이 언덕에 묻히지 않게).</summary>
    static void LiftToGround(Transform root)
    {
        foreach (Transform t in root)
        {
            var p = t.position;
            p.y += Ground.HeightAt(p.x, p.z);
            t.position = p;
        }
    }

    /// <summary>증강 장판(독 웅덩이/회복의 샘/진흙탕): 평평한 원형 StageZone. 지형 고저차가 있는 곳(경사지)에는 놓지 않음.</summary>
    void SpawnPools(int count, ZoneType type, float value, Color c, string name)
    {
        var fxr = cfg.stage.fxPrefab ? cfg.stage.fxPrefab.GetComponent<Renderer>() : null;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(Shader.PropertyToID("_BaseColor"), new Color(c.r, c.g, c.b, 0.55f));
        var g = cfg.stage.ground;
        for (int i = 0, tries = 0; i < count && tries < count * 40; tries++)
        {
            var p = Arena.RandomPoint(0.85f);
            if (p.magnitude < 12f) continue; // 시작 지점은 비움
            float d = Random.Range(5f, 8f);
            if (!Ground.IsFlat(p, d * 0.5f + 0.5f, g.zoneFlatTolerance, g.zoneMaxSlope)) continue; // 평지에만
            bool near = false;
            foreach (var z in StageZone.All) if ((z.transform.position - p).sqrMagnitude < 64f) { near = true; break; }
            if (near) continue;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.position = p + Vector3.up * 0.03f;
            go.transform.localScale = new Vector3(d, 0.01f, d);
            var r = go.GetComponent<Renderer>();
            if (fxr) r.sharedMaterial = fxr.sharedMaterial;
            r.SetPropertyBlock(mpb);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var zone = go.AddComponent<StageZone>();
            zone.type = type; zone.value = value; zone.box = false;
            i++;
        }
    }

    /// <summary>누적 스테이지 증강 수치 적용 (스폰/능력치/보상 배율). 같은 종류 효과는 곱해서 쌓임.</summary>
    void ApplyTraits()
    {
        Enemy.RegenFrac = 0f;
        foreach (var t in Traits)
            switch (t.type)
            {
                case StageTraitType.Horde:
                    spawnBatch = Mathf.CeilToInt(spawnBatch * (1f + t.value));
                    maxAlive = Mathf.RoundToInt(maxAlive * (1f + t.value));
                    gm.XpMul *= 1f + t.value2;
                    break;
                case StageTraitType.Elite: eliteChance = t.value; eliteHpMul = t.value2; break;
                case StageTraitType.TimeLimit: timeLeft = t.value; gm.XpMul *= 1f + t.value2; break;
                case StageTraitType.MeteorShower: meteorTimer = t.value; break;
                case StageTraitType.SupplyDrop: supplyTimer = t.value; break;
                case StageTraitType.Waves: waveTimer = t.value; break;
                case StageTraitType.TreasureCarrier: treasureTimer = t.value * 0.5f; break;
                case StageTraitType.Riches: resourceDropMul *= t.value; break;
                case StageTraitType.Fog:
                    RenderSettings.fog = true;
                    RenderSettings.fogStartDistance *= t.value; RenderSettings.fogEndDistance *= t.value;
                    gm.XpMul *= 1f + t.value2;
                    break;
                case StageTraitType.Blitz: killTargetMul *= t.value; enemyHp *= 1f + t.value2; break;
                case StageTraitType.Swift: enemySpeed *= 1f + t.value; gm.XpMul *= 1f + t.value2; break;
                case StageTraitType.IronSkin: enemyHp *= 1f + t.value; gm.XpMul *= 1f + t.value2; break;
                case StageTraitType.Giant: enemyHp *= t.value; enemySpeed *= 0.85f; enemyScale *= 1.35f; gm.XpMul *= 1f + t.value2; break;
                case StageTraitType.Sharp: enemyDamage *= 1f + t.value; gm.XpMul *= 1f + t.value2; break;
                case StageTraitType.Regen: Enemy.RegenFrac += t.value; gm.XpMul *= 1f + t.value2; break;
                case StageTraitType.Toxic: hazards = true; break;
                // Split/Volatile: 적이 죽을 때 (OnDeathMutations). Highlands: SetupTerrain. Sanctuary/Mire: BuildLevelDesign
            }
    }

    void TickTimeLimit(float dt)
    {
        var t = Get(StageTraitType.TimeLimit);
        if (t == null || Enraged || PortalOpen) return;
        if ((timeLeft -= dt) > 0f) return;
        timeLeft = 0f;
        Enraged = true; // 시간 초과: 적 광폭화, 경험치 보너스 끝
        Enemy.SpeedMul = cfg.stage.enrageSpeedMul;
        spawnBatch = Mathf.CeilToInt(spawnBatch * cfg.stage.enrageSpawnMul);
        gm.XpMul /= 1f + t.value2;
        if (PlayerController.I) Vfx.Pillar(PlayerController.I.Position, new Color(1f, 0.2f, 0.2f));
    }

    static readonly Color MeteorWarn = new Color(1f, 0.35f, 0.1f), MeteorBlast = new Color(1f, 0.7f, 0.3f);

    /// <summary>주기형 사건 증강: 유성우 / 보급 상자 / 습격 파도 / 보물 운반자.</summary>
    void TickEvents(float dt)
    {
        Vector3 w = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
        StageTraitDef t;
        var meteor = Get(StageTraitType.MeteorShower);
        if (meteor != null && !PortalOpen && (meteorTimer -= dt) <= 0f)
        {
            meteorTimer = meteor.value;
            for (int i = 0; i < 3; i++)
            {
                Vector2 o = Random.insideUnitCircle * 14f;
                var p = Ground.Snap(Arena.Clamp(w + new Vector3(o.x, 0f, o.y), 2f));
                const float r = 3f, delay = 1.4f;
                SpawnFx(p + Vector3.up * 0.05f, new Vector3(r * 2f, 0.02f, r * 2f), new Vector3(r * 0.4f, 0.02f, r * 0.4f), delay, MeteorWarn);
                pendingMeteors.Add(new Vector4(p.x, p.y, p.z, delay));
            }
        }
        for (int i = pendingMeteors.Count - 1; i >= 0; i--)
        {
            var m = pendingMeteors[i];
            m.w -= dt;
            if (m.w > 0f) { pendingMeteors[i] = m; continue; }
            pendingMeteors.RemoveAt(i);
            var p = new Vector3(m.x, m.y, m.z);
            float dmg = meteor != null ? meteor.value2 : 40f;
            DamageArea(p, 3f, dmg);
            if (Ward.I != null) { Vector3 d = Ward.I.Position - p; d.y = 0f; if (d.sqrMagnitude < 9f) Ward.I.TakeDamage(dmg); }
            SpawnFx(p + Vector3.up * 6f, new Vector3(1.2f, 6f, 1.2f), new Vector3(0.2f, 6f, 0.2f), 0.25f, MeteorBlast);
            Vfx.Explosion(p, 3f, MeteorBlast);
        }
        if (PortalOpen) return;
        if ((t = Get(StageTraitType.SupplyDrop)) != null && (supplyTimer -= dt) <= 0f)
        {
            supplyTimer = t.value;
            Vector2 o = Random.insideUnitCircle.normalized * Random.Range(6f, 12f);
            var p = Arena.Clamp(w + new Vector3(o.x, 0f, o.y), 3f);
            if (Nav != null) p = Nav.NearestFree(p);
            p = Ground.Snap(p);
            Vfx.Pillar(p, new Color(0.5f, 0.9f, 1f));
            int n = Mathf.RoundToInt(t.value2);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                gems.Get(Ground.Snap(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.2f), Quaternion.Euler(45f, 0f, 45f));
            }
        }
        if ((t = Get(StageTraitType.Waves)) != null && (waveTimer -= dt) <= 0f)
        {
            waveTimer = t.value;
            Vfx.Pillar(w, new Color(1f, 0.3f, 0.4f));
            int n = Mathf.RoundToInt(t.value2);
            for (int i = 0; i < n && Enemy.Active.Count < maxAlive + n; i++) SpawnEnemy();
        }
        if ((t = Get(StageTraitType.TreasureCarrier)) != null && (treasureTimer -= dt) <= 0f)
        {
            treasureTimer = t.value;
            var e = SpawnEnemy();
            if (e) { e.MakeTreasure(3f, 30f); Vfx.Pillar(e.transform.position, new Color(1f, 0.85f, 0.2f)); }
        }
    }

    /// <summary>적이 죽을 때 변이 효과: 자폭 / 분열.</summary>
    void OnDeathMutations(Enemy dead, Vector3 pos)
    {
        StageTraitDef t;
        if ((t = Get(StageTraitType.Volatile)) != null)
        {
            Vfx.Explosion(pos, t.value, new Color(1f, 0.4f, 0.2f));
            if (Ward.I != null) { Vector3 d = Ward.I.Position - pos; d.y = 0f; if (d.sqrMagnitude < t.value * t.value) Ward.I.TakeDamage(t.value2); }
        }
        if ((t = Get(StageTraitType.Split)) != null && !dead.Splitling && Random.value < t.value && Enemy.Active.Count < maxAlive)
            for (int i = 0; i < 2; i++)
            {
                Vector2 o = Random.insideUnitCircle * 0.8f;
                var p = Ground.Snap(pos + new Vector3(o.x, 0f, o.y));
                int k = PickEnemyType();
                var e = enemyPools[k].Get(p, Quaternion.identity);
                e.Init(enemyHp * t.value2, enemySpeed * 1.1f, enemyDamage * 0.5f, enemyDefs[k]);
                e.MakeSplitling(enemyScale * 0.6f);
            }
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
        KillTarget = Mathf.Max(1, Mathf.RoundToInt(gm.KillTarget * mul * killTargetMul));
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
            for (int i = 0; i < spawnBatch && Enemy.Active.Count < maxAlive; i++) SpawnEnemy();
        }

        TickTimeLimit(dt);
        TickEvents(dt);
        // 지형: 적이 가파른 비탈을 돌아오도록 생명체까지의 흐름장을 주기적으로 갱신
        if (Ground.HasTerrain && Nav != null && (flowTimer -= dt) <= 0f)
        {
            flowTimer = 0.35f;
            Nav.BuildFlow(Ward.I != null ? Ward.I.Position : PlayerController.I.Position);
        }
        if (hazards) TickHazards(dt);
        Enemy.TickAll(dt);
        Projectile.TickAll(dt);
        ThrownProjectile.TickAll(dt);
        XpGem.TickAll(dt);
        Fx.TickAll(dt);
        ResourcePickup.TickAll(dt);

    }

    Enemy SpawnEnemy()
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
            if (Ground.HasTerrain && Nav != null && !Nav.IsFree(pos)) pos = Nav.NearestFree(pos); // 너무 가파른 칸에는 스폰하지 않음
            pos.y = Ground.HeightAt(pos);
            var e = enemyPools[k].Get(pos, Quaternion.identity);
            e.Init(enemyHp, enemySpeed, enemyDamage, enemyDefs[k]);
            if (enemyScale != 1f) e.SetScale(enemyScale); // 거대화 변이
            if (eliteChance > 0f && Random.value < eliteChance) e.MakeElite(eliteHpMul, cfg.stage.eliteDamageMul, cfg.stage.eliteScale * enemyScale);
            return e;
        }
        return null;
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

    public void OnEnemyKilled(Enemy dead)
    {
        Vector3 pos = dead.transform.position;
        bool elite = dead.Elite, treasure = dead.Treasure;
        Kills++;
        gems.Get(pos, Quaternion.Euler(45f, 0f, 45f));
        int extra = elite ? cfg.stage.eliteGems - 1 : 0;
        var tc = Get(StageTraitType.TreasureCarrier);
        if (treasure && tc != null) extra += Mathf.RoundToInt(tc.value2) - 1;
        for (int i = 0; i < extra; i++)
        {
            Vector2 o = Random.insideUnitCircle * 1.5f;
            gems.Get(Ground.Snap(pos + new Vector3(o.x, 0f, o.y)), Quaternion.Euler(45f, 0f, 45f));
        }
        // 이 맵의 자원을 가끔 떨어뜨림 (보물 운반자는 반드시)
        if (resourcePickups != null && Theme && Theme.resource != ResourceType.None && (treasure || Random.value < cfg.meta.resourceDropChance * resourceDropMul * (elite ? 3f : 1f)))
        {
            var def = cfg.meta.Resource(Theme.resource);
            resourcePickups.Get(pos + Vector3.up * 0.1f, Quaternion.identity).Init(Theme.resource, def != null ? def.color : Color.white);
        }
        OnDeathMutations(dead, pos);
        if (!PortalOpen && !Expandable && Kills >= KillTarget) OpenPortal(); // 심연은 포털 없음
    }

    void OpenPortal()
    {
        PortalOpen = true;
        Vector3 at = PortalSpot();
        portal = Instantiate(cfg.stage.portalPrefab, at, Quaternion.identity, transform).transform;
        Vfx.Pillar(at, new Color(0.6f, 0.4f, 1f));
        // 남은 적은 플레이어에게서 도망 (Enemy.Routed, 공격하지 않음. 처치는 가능). 생명체가 포털에 도착하면 다음 스테이지 (Ward 참고)
        Enemy.Routed = true;
        Enemy.RoutSpeedMul = cfg.stage.routSpeedMul;
        foreach (var e in Enemy.Active) e.OnRouted();
    }

    /// <summary>
    /// 포털 위치: 경계 안 임의의 이동 가능한 지면 (길찾기 격자에서 막히지 않은 칸, 바닥 피해 구역 제외).
    /// 생명체/플레이어에서 portalMinDistance 이상 떨어진 곳을 우선 (못 찾으면 가장 먼 후보).
    /// </summary>
    Vector3 PortalSpot()
    {
        Vector3 w = Ward.I != null ? Ward.I.Position : Vector3.zero;
        Vector3 pp = PlayerController.I ? PlayerController.I.Position : w;
        float min = Mathf.Min(cfg.stage.portalMinDistance, ArenaRadius * 0.9f);
        Vector3 best = Vector3.zero; float bestSq = -1f;
        for (int i = 0; i < 40; i++)
        {
            var p = Arena.RandomPoint(0.8f);
            if (Nav != null) p = Nav.NearestFree(p);
            if (!Arena.Contains(p, 3f) || InDamageZone(p)) continue;
            p = Ground.Snap(p);
            float sq = Mathf.Min(Flat(p - w).sqrMagnitude, Flat(p - pp).sqrMagnitude);
            if (sq >= min * min) return p;
            if (sq > bestSq) { bestSq = sq; best = p; }
        }
        return bestSq >= 0f ? best : Ground.Snap(Vector3.zero);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static bool InDamageZone(Vector3 p)
    {
        foreach (var z in StageZone.All) if (z.type == ZoneType.Damage && z.Contains(p)) return true;
        return false;
    }

    /// <summary>난이도 디버프: 플레이어 주변에 예고 후 폭발하는 위험 장판.</summary>
    void TickHazards(float dt)
    {
        if ((hazardTimer -= dt) <= 0f)
        {
            hazardTimer = cfg.stage.hazardInterval;
            Vector2 o = Random.insideUnitCircle * 5f;
            Vector3 center = Ward.I != null ? Ward.I.Position : PlayerController.I.Position;
            var p = Ground.Snap(Arena.Clamp(center + new Vector3(o.x, 0f, o.y)));
            float d = cfg.stage.hazardRadius * 2f;
            SpawnFx(p + Vector3.up * 0.02f, new Vector3(d, 0.02f, d), new Vector3(d, 0.02f, d), cfg.stage.hazardDelay, new Color(0.6f, 0f, 0.4f));
            pendingHazards.Add(new Vector4(p.x, p.y, p.z, cfg.stage.hazardDelay));
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
            SpawnFx(new Vector3(h.x, h.y + 1.5f, h.z), new Vector3(1f, 1.5f, 1f), new Vector3(cfg.stage.hazardRadius * 2f, 0.1f, cfg.stage.hazardRadius * 2f), 0.2f, new Color(1f, 0.2f, 0.6f));
            Vfx.Explosion(new Vector3(h.x, h.y, h.z), cfg.stage.hazardRadius, new Color(1f, 0.2f, 0.6f));
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
        center.y = Ground.HeightAt(center) + 0.4f; // 언덕 위에서도 지면 기준으로 판정
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
        center.y = Ground.HeightAt(center) + 0.4f; // 언덕 위에서도 지면 기준으로 판정
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
        center.y = Ground.HeightAt(center) + 0.4f; // 언덕 위에서도 지면 기준으로 판정
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
            Vector3 d = list[i].transform.position - p; d.y = 0f; // 고저차가 있어도 수평 거리로
            float sq = d.sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = list[i]; }
        }
        return best;
    }
}

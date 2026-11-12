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
        Enemy.Routed = false; Enemy.SpeedMul = 1f; Enemy.Frozen = false;
        Ground.Clear();
        Traits.Clear(); if (!gm.InBonus) Traits.AddRange(gm.ActiveTraits); // 누적 스테이지 증강 (보너스 스테이지는 없음)
        // 파생 스테이지 (지금 씬이 그 파생형의 지역일 때만)
        // 파생 스테이지 전용 씬: 이 씬을 에디터에서 바로 Play해도 그 파생형으로 시작 (QA)
        var vs = FindAnyObjectByType<VariantStage>();
        if (vs && (gm.ActiveVariant == null || gm.ActiveVariant.scene != gameObject.scene.name) && cfg.variant != null)
            foreach (var v in cfg.variant.stageVariants) if (v != null && v.id == vs.variantId) { gm.ForceVariant(v); break; }
        Variant = !gm.InBonus && gm.ActiveVariant != null && gm.ActiveVariant.scene == gameObject.scene.name ? gm.ActiveVariant : null;
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
        if (!NoAutoProps) generator.Build(); // 파생 스테이지는 지역 기본 배치물(나무/바위/장판) 없음 (StageVariantDef.autoProps)
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
        enemyHp = (cfg.enemy.enemyHp + cfg.enemy.enemyHpPerStage * s) * cfg.enemy.HpStageMul(s) * (1f + gm.ModValue(ModifierType.EnemyHp)); // 스테이지가 높을수록 체력 증가
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
        AttachVariant();
    }

    protected override void OnDestroy()
    {
        BorderUnlock.Unlocked -= OnUnlockStarted;
        if (I == this) { Ground.Clear(); Enemy.Routed = false; Enemy.SpeedMul = 1f; Enemy.Frozen = false; if (GameManager.I) GameManager.I.XpMul = 1f; }
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
        var outside = GameObject.Find("Arena/Outside");
        if (NoAutoProps)
        {
            // 파생 스테이지: 자동 생성 모델 없음 — 증강 장판(독 웅덩이/회복의 샘/진흙탕)과 보더 밖 풍경도 끔 (증강 수치 효과는 그대로)
            if (outside) outside.SetActive(false);
        }
        else
        {
            if ((d = Get(StageTraitType.Toxic)) != null) SpawnPools(Mathf.RoundToInt(d.value), ZoneType.Damage, d.value2, new Color(0.45f, 0.75f, 0.15f), "Trait_PoisonPool");
            if ((d = Get(StageTraitType.Sanctuary)) != null) SpawnPools(Mathf.RoundToInt(d.value), ZoneType.Heal, d.value2, new Color(0.4f, 1f, 0.7f), "Trait_HealSpring");
            if ((d = Get(StageTraitType.Mire)) != null) SpawnPools(Mathf.RoundToInt(d.value), ZoneType.Slow, d.value2, new Color(0.45f, 0.32f, 0.2f), "Trait_Mire");
        }
        if (!Ground.HasTerrain) return;
        SettleProps(transform); // 생성기(StageGenerator)도 이 오브젝트에 있음 → 한 번만
        if (outside && outside.activeSelf) SettleProps(outside.transform, false); // 보더 밖 풍경도 지형 위에 (치우지는 않음)
    }

    /// <summary>
    /// 배치물(장애물/소품/장판)을 땅에 맞춤 — 각 오브젝트가 자기 발밑 지면을 감지:
    /// 렌더러 발자국의 네 귀퉁이 + 변 중점 + 중심 지면 높이를 재서 가장 낮은 곳에 앉힘 (어느 귀퉁이도 뜨지 않음).
    /// 발자국 아래 높이 차가 propMaxUnevenness를 넘으면 경사에 크게 파묻히므로 치움. 바닥 장판은 평지가 아니면 치움.
    /// (생성기와 StageDirector가 같은 오브젝트에 있으므로 한 번만 호출)
    /// </summary>
    void SettleProps(Transform root, bool removeUneven = true)
    {
        var g = cfg.stage.ground;
        foreach (Transform t in root)
        {
            if (!t.gameObject.activeSelf) continue;
            var zone = t.GetComponentInChildren<StageZone>();
            if (zone)
            {
                var s = zone.transform.lossyScale;
                float r = Mathf.Max(s.x, s.z) * 0.5f * (zone.box ? 1.42f : 1f); // 상자는 대각선까지
                if (r <= 20f && !Ground.IsFlat(t.position, r + 0.3f, g.zoneFlatTolerance * 1.25f, g.zoneMaxSlope + 2f)) { t.gameObject.SetActive(false); continue; }
                var zp = t.position; zp.y += Ground.HeightAt(zp.x, zp.z); t.position = zp;
                continue;
            }
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            // 발자국 (조금 안쪽): 3x3 표본
            float hx = b.extents.x * 0.85f, hz = b.extents.z * 0.85f;
            float min = float.MaxValue, max = float.MinValue;
            for (int iz = -1; iz <= 1; iz++)
                for (int ix = -1; ix <= 1; ix++)
                {
                    float h = Ground.HeightAt(b.center.x + ix * hx, b.center.z + iz * hz);
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            if (removeUneven && max - min > g.propMaxUnevenness) { t.gameObject.SetActive(false); continue; } // 경사가 심하면 지형에 파묻히므로 배치 안 함
            // 평지(높이 0) 기준으로 만든 프리셋 → 발자국 최저 지면만큼 올림 (원래 의도한 묻힘 정도는 유지)
            var p = t.position; p.y += min; t.position = p;
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

    // ---- 파생 스테이지 ----
    /// <summary>이번 스테이지가 파생형이면 그 정의 (기본형이면 null).</summary>
    public StageVariantDef Variant { get; private set; }
    /// <summary>적이 길찾기 흐름장을 따라 움직이는지: 지형이 있거나, 파생 규칙이 바닥 일부를 막았을 때 (칼데라 용암).</summary>
    public bool UseFlow => Nav != null && (Ground.HasTerrain || Nav.HasDynamicBlocks);
    /// <summary>파생 규칙이 포털 자리를 거를 때 (칼데라: 사라지지 않는 플랫폼 위만). 후보가 하나도 없으면 PortalFallback.</summary>
    public System.Func<Vector3, bool> PortalFilter;
    public System.Func<Vector3> PortalFallback;
    /// <summary>파생 규칙의 추가 클리어 조건 (깨어난 묘지: 무덤 N개 파괴). 처치 목표와 함께 둘 다 채워야 포털이 열림. 바뀌면 규칙이 CheckClear 호출.</summary>
    public System.Func<bool> ExtraClearCondition;
    /// <summary>HUD 정보 줄에 덧붙는 추가 목표 수치 (예: "무덤 2/5"). 비우면 없음.</summary>
    public System.Func<string> ExtraObjective;
    /// <summary>현재 스테이지 적 기본 체력 (무덤 체력 등 기준).</summary>
    public float EnemyHp => enemyHp;

    /// <summary>처치 목표 배율 (파생 규칙용, 곱해 누적).</summary>
    public void MulKillTarget(float m) { killTargetMul *= m; UpdateKillTarget(); }

    /// <summary>클리어 조건(처치 목표 + 추가 조건)을 채웠으면 포털을 엶. 심연은 포털 없음.</summary>
    public void CheckClear()
    {
        if (PortalOpen || Expandable || Kills < KillTarget) return;
        if (ExtraClearCondition != null && !ExtraClearCondition()) return;
        OpenPortal();
    }

    /// <summary>파생 스테이지에서 자동 생성 모델(배치 소품, 보더 밖 풍경, 증강 장판)을 끄는지 (StageVariantDef.autoProps가 꺼져 있으면).</summary>
    bool NoAutoProps => Variant != null && !Variant.autoProps;
    /// <summary>HUD에 표시할 스테이지 이름 (파생형이면 파생 이름).</summary>
    public string DisplayName => Variant != null ? Variant.name : Theme ? Theme.displayName : "";
    readonly List<float> enemyWeights = new List<float>(8);
    Transform variantPortal;
    VariantPortal variantPortalComp;
    /// <summary>파생 포털이 열려 있는지 (조건 증강 보유 시 일반 포털과 함께).</summary>
    public bool VariantPortalOpen => variantPortal != null;
    public Vector3 VariantPortalPosition => variantPortal ? variantPortal.position : Vector3.zero;
    public StageVariantDef OfferedVariant => variantPortalComp ? variantPortalComp.Def : null;

    /// <summary>
    /// 생명체가 걸어갈 포털: 감시자가 더 가까이 있는 포털 (플레이어가 감시자를 움직여 고름). F로 바로 들어갈 수도 있음.
    /// </summary>
    public Vector3 WardPortalGoal
    {
        get
        {
            if (variantPortal && PlayerController.I)
            {
                // 감시자(조준점)가 더 가까이 있는 포털로 (감시자를 원하는 포털 쪽으로 움직이면 생명체가 따라감)
                Vector3 w = PlayerController.I.Position;
                Vector3 dv = w - variantPortal.position, dn = w - PortalPosition; dv.y = 0f; dn.y = 0f;
                if (dv.sqrMagnitude < dn.sqrMagnitude) return variantPortal.position;
            }
            return PortalPosition;
        }
    }

    /// <summary>파생형 고유 규칙 붙이기 (Awake 끝, 길찾기 격자가 만들어진 뒤).</summary>
    T SceneRule<T>() where T : MonoBehaviour
    {
        var c = FindAnyObjectByType<T>();
        if (c) return c;
        Debug.LogWarning($"[StageDirector] 파생 전용 씬에 {typeof(T).Name}이(가) 없습니다 — 메뉴 Roguelike/Build Variant Scenes로 씬을 만드세요");
        return gameObject.AddComponent<T>();
    }

    void AttachVariant()
    {
        if (Variant == null) return;
        switch (Variant.kind)
        {
            // 규칙 컴포넌트는 전용 씬에 배치돼 있음 (없으면 경고 후 기본 생성)
            case StageVariantKind.ForestFirefly: SceneRule<ForestFireflyVariant>().Init(this, cfg.variant.firefly); break;
            case StageVariantKind.ForestFarmland: SceneRule<ForestFarmlandVariant>().Init(this, cfg.variant.farmland); break;
            case StageVariantKind.DesertSandstorm: SceneRule<DesertSandstormVariant>().Init(this, cfg.variant.sandstorm); break;
            case StageVariantKind.SnowFrozenLake: SceneRule<FrozenLakeVariant>().Init(this, cfg.variant.frozenLake); break;
            case StageVariantKind.VolcanoCaldera: SceneRule<CalderaVariant>().Init(this, cfg.variant.caldera); break;
            case StageVariantKind.SwampWisp: SceneRule<SwampWispVariant>().Init(this, cfg.variant.wisp); break;
            case StageVariantKind.GraveyardAwakened: SceneRule<AwakenedGraveyardVariant>().Init(this, cfg.variant.awakened); break;
            case StageVariantKind.GraveyardMoonlit: SceneRule<MoonlitGraveyardVariant>().Init(this, cfg.variant.moonlit); break;
        }
    }

    /// <summary>조건 증강을 보유했으면 일반 포털과 떨어진 곳에 파생 포털을 하나 더 엶.</summary>
    void OpenVariantPortal()
    {
        var v = gm.AvailableVariant(gameObject.scene.name);
        if (v == null) return;
        Vector3 at = Vector3.zero; float bestSq = -1f;
        for (int i = 0; i < 30; i++)
        {
            Vector3 p = PortalSpot();
            float sq = Flat(p - PortalPosition).sqrMagnitude;
            if (sq >= cfg.variant.portalSpacing * cfg.variant.portalSpacing) { at = p; bestSq = sq; break; }
            if (sq > bestSq) { bestSq = sq; at = p; }
        }
        var go = Instantiate(cfg.stage.portalPrefab, at, Quaternion.identity, transform);
        go.name = "VariantPortal_" + v.id;
        foreach (var sp in go.GetComponentsInChildren<StagePortal>()) DestroyImmediate(sp); // 일반 포털 동작 제거
        variantPortalComp = go.AddComponent<VariantPortal>();
        variantPortalComp.Init(v);
        variantPortal = go.transform;
        if (HudExtras.I) HudExtras.I.Notice($"<color=#{ColorUtility.ToHtmlStringRGB(v.color)}>{v.name}</color>");
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

        if (!PortalOpen && !Enemy.Frozen && (spawnTimer -= dt) <= 0f) // 적 정지(모래폭풍) 중엔 새로 나오지 않음
        {
            spawnTimer = spawnInterval;
            for (int i = 0; i < spawnBatch && Enemy.Active.Count < maxAlive; i++) SpawnEnemy();
        }

        TickRoutDespawn(dt);
        TickTimeLimit(dt);
        TickEvents(dt);
        // 지형: 적이 가파른 비탈을 돌아오도록 생명체까지의 흐름장을 주기적으로 갱신
        if (UseFlow && (flowTimer -= dt) <= 0f)
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
            if (UseFlow && !Nav.IsFree(pos)) pos = Nav.NearestFree(pos); // 너무 가파른 칸에는 스폰하지 않음
            return SpawnEnemyAt(pos, k);
        }
        return null;
    }

    /// <summary>정한 위치에 적 하나 (종류 k, -1이면 가중치 무작위). 파생 규칙(무덤에서 일어나는 적 등)도 사용.</summary>
    public Enemy SpawnEnemyAt(Vector3 pos, int k = -1)
    {
        if (k < 0) k = PickEnemyType();
        pos.y = Ground.HeightAt(pos);
        var e = enemyPools[k].Get(pos, Quaternion.identity);
        e.Init(enemyHp, enemySpeed, enemyDamage, enemyDefs[k]);
        if (enemyScale != 1f) e.SetScale(enemyScale); // 거대화 변이
        if (eliteChance > 0f && Random.value < eliteChance) e.MakeElite(eliteHpMul, cfg.stage.eliteDamageMul, cfg.stage.eliteScale * enemyScale);
        if (Routed()) e.OnRouted();
        return e;
    }

    static bool Routed() => Enemy.Routed;

    void BuildEnemyPools(int stageIndex)
    {
        enemyWeightTotal = 0f;
        if (cfg.enemy.enemyTypes != null)
            foreach (var d in cfg.enemy.enemyTypes)
            {
                if (d == null || d.prefab == null || d.weight <= 0f || stageIndex < d.minStage) continue;
                enemyDefs.Add(d);
                float w = d.weight * (Variant != null && Variant.enemyBias != EnemyAbility.None && d.ability == Variant.enemyBias ? Variant.enemyBiasMul : 1f); // 파생형 적 구성
                enemyWeights.Add(w);
                enemyWeightTotal += w;
            }
        if (enemyDefs.Count == 0) { enemyDefs.Add(null); enemyWeights.Add(1f); enemyWeightTotal = 1f; } // 구버전: 기본 적만
        int prewarm = Mathf.Max(8, cfg.stage.maxAlive / enemyDefs.Count);
        foreach (var d in enemyDefs)
            enemyPools.Add(new Pool<Enemy>(d != null ? d.prefab : cfg.enemy.enemyPrefab, transform, prewarm));
    }

    int PickEnemyType()
    {
        if (enemyDefs.Count == 1) return 0;
        float r = Random.value * enemyWeightTotal;
        for (int i = 0; i < enemyDefs.Count - 1; i++) { r -= enemyWeights[i]; if (r <= 0f) return i; }
        return enemyDefs.Count - 1;
    }

    /// <summary>보상 떨어뜨리기 (파생 스테이지의 유물 등): 경험치 보석 gemCount개 + (resource면) 이 지역 자원 하나.</summary>
    public void DropReward(Vector3 pos, int gemCount, bool resource)
    {
        for (int i = 0; i < gemCount; i++)
        {
            Vector2 o = Random.insideUnitCircle * 1.5f;
            gems.Get(Ground.Snap(pos + new Vector3(o.x, 0f, o.y)), Quaternion.Euler(45f, 0f, 45f));
        }
        if (resource && resourcePickups != null && Theme && Theme.resource != ResourceType.None)
        {
            var def = cfg.meta.Resource(Theme.resource);
            resourcePickups.Get(Ground.Snap(pos) + Vector3.up * 0.1f, Quaternion.identity).Init(Theme.resource, def != null ? def.color : Color.white);
        }
    }

    /// <summary>적 사망 처리. environmental = 구멍/용암 등 환경사: 처치 수에는 안 침 (파생 설정 environmentKillsCount로 예외). 보상(보석/자원/변이)은 같음.</summary>
    public void OnEnemyKilled(Enemy dead, bool environmental = false)
    {
        Vector3 pos = dead.transform.position;
        bool elite = dead.Elite, treasure = dead.Treasure;
        if (!environmental || (Variant != null && Variant.environmentKillsCount)) Kills++;
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
        CheckClear();
    }

    float routDespawnLeft;
    readonly List<Enemy> routCleanup = new List<Enemy>(64);
    /// <summary>포털이 열리고 routDespawnDelay초 뒤 도망치던 적을 모두 지움 (처치 수·보상 없음).</summary>
    void TickRoutDespawn(float dt)
    {
        if (!PortalOpen || routDespawnLeft <= 0f || (routDespawnLeft -= dt) > 0f) return;
        routCleanup.Clear(); routCleanup.AddRange(Enemy.Active);
        foreach (var e in routCleanup) { Vfx.Pickup(e.transform.position + Vector3.up * 0.3f, new Color(0.85f, 0.85f, 0.9f), 4); e.Remove(); }
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
        routDespawnLeft = cfg.stage.routDespawnDelay;
        foreach (var e in Enemy.Active) e.OnRouted();
        OpenVariantPortal(); // 조건 증강 보유 시 파생 포털도
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
            if (!Arena.Contains(p, 3f) || InDamageZone(p) || !Ground.IsFlat(p, 1.6f, 0.35f, 18f) || (PortalFilter != null && !PortalFilter(p))) continue; // 포털 발밑이 평평한 곳 (뜨거나 묻히지 않게)
            p = Ground.Snap(p);
            float sq = Mathf.Min(Flat(p - w).sqrMagnitude, Flat(p - pp).sqrMagnitude);
            if (sq >= min * min) return p;
            if (sq > bestSq) { bestSq = sq; best = p; }
        }
        return bestSq >= 0f ? best : PortalFallback != null ? PortalFallback() : Ground.Snap(Vector3.zero);
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
        HitTargets(center, radius, dmg);
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
        HitTargets(center, radius, dmg);
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
        HitTargets(center, radius, dmg);
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

    /// <summary>범위 안의 부술 수 있는 오브젝트(StageTarget)에 피해.</summary>
    void HitTargets(Vector3 center, float radius, float dmg)
    {
        if (dmg <= 0f || StageTarget.All.Count == 0) return;
        for (int i = StageTarget.All.Count - 1; i >= 0; i--)
        {
            var t = StageTarget.All[i];
            if (!t.Alive) continue;
            Vector3 d = t.transform.position - center; d.y = 0f;
            float rr = radius + t.Radius; // 오브젝트 크기만큼 (가장자리에 떨어져도 맞음)
            if (d.sqrMagnitude <= rr * rr) t.Hit(dmg, center);
        }
    }

    /// <summary>생명체 자동 공격 대상: 사거리 안에서 가장 가까운 적 또는 부술 수 있는 오브젝트(무덤 등). 조준 위치를 돌려줌.</summary>
    public bool NearestTarget(Vector3 p, float range, out Vector3 aim)
    {
        var e = NearestEnemy(p, range);
        aim = default;
        // 사거리 안 가장 가까운 부술 수 있는 오브젝트 (무덤 등)
        StageTarget near = null; float tSq = range * range;
        foreach (var t in StageTarget.All)
        {
            if (!t.Alive) continue;
            Vector3 d = t.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < tSq) { tSq = d.sqrMagnitude; near = t; }
        }
        if (near != null)
        {
            // 적보다 가깝거나, 적이 더 가까워도 TargetShare 확률로 오브젝트를 노림 (적에 둘러싸인 무덤도 부서지게)
            float eSq = float.MaxValue;
            if (e != null) { Vector3 d = e.transform.position - p; d.y = 0f; eSq = d.sqrMagnitude; }
            if (tSq <= eSq || Random.value < TargetShare) { aim = near.AimPoint; return true; }
        }
        if (e == null) return false;
        aim = e.transform.position;
        return true;
    }

    /// <summary>사거리 안에 부술 수 있는 오브젝트(무덤 등)가 있을 때, 적이 더 가까워도 그쪽을 노리는 확률 (파생 규칙이 정함, 기본 0).</summary>
    [System.NonSerialized] public float TargetShare;

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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Playing, LevelUp, GameOver, Victory }

/// <summary>런 전체 상태(스탯, 스테이지 진행, 레벨업)를 씬 간에 유지.</summary>
public class GameManager : Singleton<GameManager>
{
    protected override bool Persistent => true;

    public GameConfig Config { get; private set; }
    public PlayerStats Stats { get; private set; }
    public int StageIndex { get; private set; }
    public GameState State { get; private set; }
    public bool ChoiceIsSpecial { get; private set; }
    public UpgradeCategory ChoiceCategory { get; private set; }
    public readonly List<UpgradeDef> Choices = new List<UpgradeDef>(3);
    public event System.Action ChoicesChanged;

    public int CompanionIndex { get; set; }
    public int ModifierMask { get; private set; } // 활성 난이도 석판 비트마스크
    public CompanionDef Companion => Config.companion.companions != null && Config.companion.companions.Length > 0 ? Config.companion.companions[CompanionIndex] : null;
    /// <summary>이번 런의 작은 생명체 종류 (선택 UI 추가 전까지는 WardConfig.defaultCreature).</summary>
    public int CreatureIndex { get; set; } = -1;
    /// <summary>실제로 쓰는 생명체 번호 (고르지 않았으면 WardConfig.defaultCreature).</summary>
    public int CreatureIndexEffective => CreatureIndex >= 0 ? CreatureIndex : Config.ward.defaultCreature;
    public CreatureDef Creature
    {
        get
        {
            var list = Config.ward.creatures;
            if (list == null || list.Length == 0) return null;
            int i = CreatureIndex >= 0 ? CreatureIndex : Config.ward.defaultCreature;
            return list[Mathf.Clamp(i, 0, list.Length - 1)];
        }
    }

    int pendingLevelUps;
    readonly List<UpgradeDef> buffer = new List<UpgradeDef>(16);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (I == null) new GameObject("GameManager").AddComponent<GameManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        if (I != this) return;
        Config = Resources.Load<GameConfig>("GameConfig");
        SceneManager.sceneLoaded += OnSceneLoaded;
        RotateRegions();
        ResetRun();
    }

    // ---- 지역 지도 (3x3) ----
    /// <summary>지도 칸 번호(1~9) → 지역. [0]은 사용 안 함. 1번은 고정(심연), 2~9번은 로테이션.</summary>
    public readonly RegionDef[] Regions = new RegionDef[10];
    /// <summary>이번 런에서 고른 지도 칸 번호 (0이면 고르지 않음 → 기존 랜덤 스테이지).</summary>
    public int RegionSlot { get; private set; }
    public RegionDef CurrentRegion => RegionSlot > 0 ? Regions[RegionSlot] : null;
    /// <summary>로테이션이 바뀔 때마다 증가 (UI 갱신용).</summary>
    public int RegionVersion { get; private set; }

    /// <summary>2~9번 칸에 지역 후보를 섞어서 배치. 로비로 돌아올 때마다 호출.</summary>
    public void RotateRegions()
    {
        var st = Config.stage;
        Regions[1] = st.centerRegion != null && !string.IsNullOrEmpty(st.centerRegion.scene) ? st.centerRegion : null;
        var pool = new List<RegionDef>();
        if (st.regionPool != null)
            foreach (var r in st.regionPool) if (r != null && !string.IsNullOrEmpty(r.scene)) pool.Add(r);
        // 셔플 후 앞에서부터 (후보가 8개보다 적으면 반복)
        for (int i = pool.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        for (int n = 2; n <= 9; n++) Regions[n] = pool.Count > 0 ? pool[(n - 2) % pool.Count] : null;
        RegionVersion++;
    }

    /// <summary>지도 칸을 골라 런 시작. 런 동안 모든 일반 스테이지가 그 지역에서 진행.</summary>
    public void StartRun(int slot)
    {
        ResetRun();
        RegionSlot = slot >= 1 && slot <= 9 && Regions[slot] != null ? slot : 0;
        LoadStage();
    }

    protected override void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) { Time.timeScale = 1f; transitioning = false; }

    /// <summary>씬 전환 중 (LoadScene 호출 ~ 로드 완료). 이전 씬의 남은 프레임이 패배 처리를 다시 일으키지 않도록.</summary>
    bool transitioning;
    public bool Transitioning => transitioning;

    public bool IsModifierOn(int i) => (ModifierMask & (1 << i)) != 0;
    public void ToggleModifier(int i) => ModifierMask ^= 1 << i;

    public bool Has(ModifierType t)
    {
        for (int i = 0; i < Config.stage.modifiers.Length; i++)
            if (IsModifierOn(i) && Config.stage.modifiers[i].type == t) return true;
        return false;
    }

    /// <summary>같은 타입 활성 디버프 값 합.</summary>
    public float ModValue(ModifierType t)
    {
        float v = 0f;
        for (int i = 0; i < Config.stage.modifiers.Length; i++)
            if (IsModifierOn(i) && Config.stage.modifiers[i].type == t) v += Config.stage.modifiers[i].value;
        return v;
    }

    public void StartRun() => StartRun(0);

    public void GoLobby() { ResetRun(); RegionSlot = 0; RotateRegions(); Load(Config.stage.lobbyScene); }

    public void ResetRun()
    {
        Stats = Config.CreateStats();
        ApplyBaseUpgrades();
        StageIndex = 0;
        InBonus = false;
        pendingLevelUps = 0;
        State = GameState.Playing;
        Kind = ChoiceKind.Upgrade;
        RewardOrder.Clear(); RewardStacks.Clear();
        ActiveTraits.Clear(); TraitPoolExhausted = false;
        SkillCost = Config.skills ? Config.skills.startCost : 0f; // 감시자 스킬 코스트: 첫 스테이지 입장 시 (씬이 바뀌어도 그대로)
        UsedVariants.Clear(); ActiveVariant = null; pendingVariant = null;
        XpMul = 1f; xpCarry = 0f;
    }

    /// <summary>거점 성장 레벨만큼 영구 강화 적용.</summary>
    void ApplyBaseUpgrades()
    {
        if (Config.meta.baseUpgrades == null) return;
        foreach (var b in Config.meta.baseUpgrades)
        {
            int lv = SaveData.Level(b.id);
            if (lv > 0) Stats.Apply(new UpgradeDef { stat = b.stat, value = b.valuePerLevel * lv });
        }
        Stats.hp = Stats.maxHp;
        Stats.wardHp = Stats.wardMaxHp;
    }

    public int XpToNext => Mathf.RoundToInt(Config.player.xpBase * Mathf.Pow(Config.player.xpGrowth, Stats.level - 1));
    public int KillTarget => Config.stage.baseKillTarget + StageIndex * Config.stage.killTargetPerStage;
    public bool InBonus { get; private set; }
    /// <summary>감시자 스킬 코스트 (런 동안 유지, 씬이 바뀌어도 그대로). 적 처치로 충전.</summary>
    public float SkillCost { get; set; }
    /// <summary>UI가 열려 있을 때 플레이어 입력 차단.</summary>
    public bool InputLocked
    {
        get => inputLocked;
        set { inputLocked = value; lockChangedFrame = Time.frameCount; }
    }
    /// <summary>잠금 중이거나 잠금 상태가 이번 프레임에 바뀐 경우 (같은 F 입력이 열기/닫기 둘 다 처리되는 것 방지).</summary>
    public bool InputBlocked => inputLocked || lockChangedFrame == Time.frameCount;
    bool inputLocked;
    int lockChangedFrame = -1;
    string lastScene;

    /// <summary>이번 스테이지 경험치 배율 (스테이지 특징이 설정, StageDirector가 스테이지마다 초기화).</summary>
    public float XpMul { get; set; } = 1f;
    float xpCarry;

    public void AddXp(int v)
    {
        if (transitioning) return;
        float add = v * XpMul + xpCarry;
        int whole = Mathf.FloorToInt(add);
        xpCarry = add - whole;
        Stats.xp += whole;
        int need;
        while (Stats.xp >= (need = XpToNext))
        {
            Stats.xp -= need;
            Stats.level++;
            pendingLevelUps++;
        }
        if (pendingLevelUps > 0 && State == GameState.Playing)
        {
            if (PlayerController.I) Vfx.Pillar(PlayerController.I.Position, new Color(1f, 0.85f, 0.3f));
            if (Config.player.levelUpChoices) OpenLevelUp();
            else GrantAutoRewards();
        }
    }

    // ---- 레벨업 자동 보상 (증강 선택 임시 비활성: PlayerConfig.levelUpChoices) ----
    /// <summary>이번 런에서 얻은 자동 보상과 횟수 (획득 순서대로).</summary>
    public readonly List<LevelRewardDef> RewardOrder = new List<LevelRewardDef>(16);
    public readonly Dictionary<LevelRewardDef, int> RewardStacks = new Dictionary<LevelRewardDef, int>(16);
    /// <summary>자동 보상 획득 (보상, 획득한 레벨). HUD 하단 알림/아이콘 목록이 구독.</summary>
    public event System.Action<LevelRewardDef, int> RewardGained;
    readonly List<LevelRewardDef> rewardBuf = new List<LevelRewardDef>(16);

    void GrantAutoRewards()
    {
        var list = Config.player.autoRewards;
        while (pendingLevelUps > 0)
        {
            int lv = Stats.level - pendingLevelUps + 1;
            pendingLevelUps--;
            if (list == null || list.Length == 0) continue;
            rewardBuf.Clear();
            float total = 0f;
            foreach (var r in list)
            {
                if (r == null || r.weight <= 0f) continue;
                if (r.maxStacks > 0 && RewardStacks.TryGetValue(r, out int n) && n >= r.maxStacks) continue;
                if (r.stat == StatType.WardHeal && Has(ModifierType.NoHealing)) continue;
                rewardBuf.Add(r); total += r.weight;
            }
            if (rewardBuf.Count == 0) continue;
            float pick = Random.value * total;
            var got = rewardBuf[rewardBuf.Count - 1];
            foreach (var r in rewardBuf) { pick -= r.weight; if (pick <= 0f) { got = r; break; } }
            Stats.Apply(new UpgradeDef { name = got.name, stat = got.stat, value = got.value });
            if (RewardStacks.TryGetValue(got, out int c)) RewardStacks[got] = c + 1;
            else { RewardStacks[got] = 1; RewardOrder.Add(got); }
            RewardGained?.Invoke(got, lv);
        }
    }

    // ---- 다음 스테이지 특징 (카드 선택) ----
    public enum ChoiceKind { Upgrade, StageTrait }
    /// <summary>선택 화면(State = LevelUp)이 무엇을 고르는지: 레벨업 증강 / 다음 스테이지 특징.</summary>
    public ChoiceKind Kind { get; private set; }
    public readonly List<StageTraitDef> TraitChoices = new List<StageTraitDef>(3);
    /// <summary>이번 런에서 고른 스테이지 증강 (누적형: 고른 순서대로, 이후 모든 일반 스테이지에 적용. 다시 고를 수 없음).</summary>
    public readonly List<StageTraitDef> ActiveTraits = new List<StageTraitDef>(16);
    /// <summary>후보로 나올 수 있는 남은 증강이 없음 (풀 소진).</summary>
    public bool TraitPoolExhausted { get; private set; }

    /// <summary>
    /// 포털 진입: 다음 일반 스테이지가 있으면 먼저 특징 카드를 고르게 하고(선택 후 이동), 아니면 바로 이동.
    /// </summary>
    public void RequestNextStage(StageVariantDef variant = null)
    {
        if (State != GameState.Playing || transitioning) return;
        pendingVariant = variant; // 파생 포털로 들어가면 다음 스테이지 = 그 파생형
        var st = Config.stage;
        if (!st.stageTraitsEnabled || st.stageTraits == null || st.stageTraits.Length == 0 || StageIndex + 1 >= st.stageCount)
        {
            NextStage();
            return;
        }
        var pool = new List<StageTraitDef>(st.stageTraits.Length);
        foreach (var t in st.stageTraits) if (t != null && t.weight > 0f && !ActiveTraits.Contains(t)) pool.Add(t); // 이미 고른 증강은 제외
        TraitPoolExhausted = pool.Count == 0;
        TraitChoices.Clear();
        for (int i = 0; i < st.traitChoiceCount && pool.Count > 0; i++)
        {
            float total = 0f;
            foreach (var t in pool) total += t.weight;
            float pick = Random.value * total;
            int r = 0;
            for (; r < pool.Count - 1; r++) { pick -= pool[r].weight; if (pick <= 0f) break; }
            TraitChoices.Add(pool[r]);
            pool.RemoveAt(r);
        }
        if (TraitChoices.Count == 0) { NextStage(); return; } // 풀 소진: 고를 증강이 없으면 바로 이동
        Kind = ChoiceKind.StageTrait;
        State = GameState.LevelUp;
        Time.timeScale = 0f;
        ChoicesChanged?.Invoke();
    }

    void OpenLevelUp()
    {
        Kind = ChoiceKind.Upgrade;
        int lv = Stats.level - pendingLevelUps + 1;
        // 1번째 탄환 → 2번째 생명체 생존력 → 3번째 동반자 (순환)
        ChoiceCategory = (lv % 3) == 1 ? UpgradeCategory.Projectile : (lv % 3) == 2 ? UpgradeCategory.Ward : UpgradeCategory.Companion;
        ChoiceIsSpecial = ChoiceCategory == UpgradeCategory.Companion;
        buffer.Clear();
        foreach (var u in Config.player.upgrades)
            if (u.category == ChoiceCategory && u.weight > 0f
                && !((u.stat == StatType.Heal || u.stat == StatType.WardHeal) && Has(ModifierType.NoHealing))
                && !(u.stat == StatType.Jump && Stats.canJump)
                && !IsBodyOnly(u.stat)) buffer.Add(u);
        Choices.Clear();
        for (int i = 0; i < 3 && buffer.Count > 0; i++)
        {
            // 가중치 랜덤 (중복 없이)
            float total = 0f;
            for (int k = 0; k < buffer.Count; k++) total += buffer[k].weight;
            float pick = Random.value * total;
            int r = 0;
            for (; r < buffer.Count - 1; r++) { pick -= buffer[r].weight; if (pick <= 0f) break; }
            Choices.Add(buffer[r]);
            buffer[r] = buffer[buffer.Count - 1];
            buffer.RemoveAt(buffer.Count - 1);
        }
        State = GameState.LevelUp;
        Time.timeScale = 0f;
        ChoicesChanged?.Invoke();
    }

    /// <summary>전투에서 플레이어는 형태 없는 존재 → 몸 관련 강화(체력/회복/점프)는 제외.</summary>
    static bool IsBodyOnly(StatType s) => s == StatType.MaxHp || s == StatType.Heal || s == StatType.Jump;

    public void Choose(int i)
    {
        if (State != GameState.LevelUp) return;
        if (Kind == ChoiceKind.StageTrait)
        {
            if (i >= TraitChoices.Count) return;
            ActiveTraits.Add(TraitChoices[i]); // 누적: 다음 일반 스테이지부터 계속 적용
            Kind = ChoiceKind.Upgrade;
            State = GameState.Playing;
            Time.timeScale = 1f;
            NextStage();
            return;
        }
        if (i >= Choices.Count) return;
        Stats.Apply(Choices[i]);
        pendingLevelUps--;
        if (pendingLevelUps > 0) { OpenLevelUp(); return; }
        State = GameState.Playing;
        Time.timeScale = 1f;
    }

    /// <summary>true면 생명체를 잃어서 패배.</summary>
    public bool WardLost { get; private set; }

    public void GameOver(bool wardLost = false)
    {
        if (State == GameState.GameOver || transitioning) return;
        WardLost = wardLost;
        State = GameState.GameOver;
        Time.timeScale = 0f;
    }

    // ---- 파생 스테이지 ----
    /// <summary>현재 스테이지가 파생형이면 그 정의 (기본형이면 null).</summary>
    public StageVariantDef ActiveVariant { get; private set; }
    /// <summary>이번 런에 이미 들어간 파생형 (같은 파생형은 한 번만).</summary>
    public readonly List<StageVariantDef> UsedVariants = new List<StageVariantDef>(8);
    StageVariantDef pendingVariant;

    /// <summary>이 지역(씬)에서 지금 열 수 있는 파생형: 조건 증강 보유 + 아직 안 들어감 + 지금이 기본형일 때.</summary>
    /// <summary>QA: 파생 전용 씬을 에디터에서 바로 Play했을 때 그 파생형으로 시작.</summary>
    public void ForceVariant(StageVariantDef v) { ActiveVariant = v; InBonus = false; }

    bool HasTrait(StageTraitType type)
    {
        foreach (var t in ActiveTraits) if (t.type == type) return true;
        return false;
    }

    bool AllTraits(StageTraitType[] types)
    {
        if (types == null) return true;
        foreach (var t in types) if (!HasTrait(t)) return false;
        return true;
    }

    public StageVariantDef AvailableVariant(string scene)
    {
        var list = Config.variant.stageVariants;
        if (list == null || ActiveVariant != null || InBonus) return null;
        foreach (var v in list)
        {
            if (v == null || v.regionScene != scene || UsedVariants.Contains(v)) continue;
            if (HasTrait(v.requiredTrait) && AllTraits(v.alsoRequired)) return v; // 조건 증강을 모두 보유
        }
        return null;
    }

    public void NextStage()
    {
        if (pendingVariant != null)
        {
            // 파생 스테이지: 보너스 없이 바로. 스테이지 번호는 현재에서 variantStageDecrease만큼 감소 (최소 1번 = 인덱스 0)
            var v = pendingVariant; pendingVariant = null;
            UsedVariants.Add(v);
            InBonus = false;
            StageIndex = Mathf.Max(0, StageIndex - Config.variant.stageDecrease);
            if (StageIndex >= Config.stage.stageCount) { State = GameState.Victory; Time.timeScale = 0f; return; }
            ActiveVariant = v;
            Load(v.scene);
            return;
        }
        ActiveVariant = null;
        if (!InBonus && Config.stage.bonusScenes.Length > 0 && StageIndex + 1 < Config.stage.stageCount && Random.value < Config.stage.bonusChance)
        {
            InBonus = true;
            Load(Config.stage.bonusScenes[Random.Range(0, Config.stage.bonusScenes.Length)]);
            return;
        }
        InBonus = false;
        StageIndex++;
        if (StageIndex >= Config.stage.stageCount) { State = GameState.Victory; Time.timeScale = 0f; return; }
        LoadStage();
    }

    void LoadStage()
    {
        var region = CurrentRegion;
        if (region != null) { Load(region.scene); return; }
        var s = Config.stage.stageScenes;
        string next = s[Random.Range(0, s.Length)];
        if (s.Length > 1 && next == lastScene) next = s[(System.Array.IndexOf(s, next) + 1) % s.Length];
        Load(next);
    }

    void Load(string scene)
    {
        lastScene = scene;
        transitioning = true;
        InputLocked = false;
        SceneManager.LoadScene(scene);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        switch (State)
        {
            case GameState.LevelUp:
                if (kb.digit1Key.wasPressedThisFrame) Choose(0);
                else if (kb.digit2Key.wasPressedThisFrame) Choose(1);
                else if (kb.digit3Key.wasPressedThisFrame) Choose(2);
                break;
            case GameState.GameOver:
            case GameState.Victory:
                if (kb.rKey.wasPressedThisFrame) GoLobby();
                break;
        }
    }
}

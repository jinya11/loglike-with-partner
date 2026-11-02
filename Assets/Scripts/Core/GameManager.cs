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

    public void AddXp(int v)
    {
        if (transitioning) return;
        Stats.xp += v;
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
            OpenLevelUp();
        }
    }

    void OpenLevelUp()
    {
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
        if (State != GameState.LevelUp || i >= Choices.Count) return;
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

    public void NextStage()
    {
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

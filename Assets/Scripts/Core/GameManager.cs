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
    public readonly List<UpgradeDef> Choices = new List<UpgradeDef>(3);
    public event System.Action ChoicesChanged;

    public int WeaponIndex { get; set; }
    public int CompanionIndex { get; set; }
    public int ModifierMask { get; private set; } // 활성 난이도 석판 비트마스크
    public WeaponDef Weapon => Config.weapons != null && Config.weapons.Length > 0 ? Config.weapons[WeaponIndex] : null;
    public CompanionDef Companion => Config.companions != null && Config.companions.Length > 0 ? Config.companions[CompanionIndex] : null;

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
        ResetRun();
    }

    protected override void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => Time.timeScale = 1f;

    public bool IsModifierOn(int i) => (ModifierMask & (1 << i)) != 0;
    public void ToggleModifier(int i) => ModifierMask ^= 1 << i;

    public bool Has(ModifierType t)
    {
        for (int i = 0; i < Config.modifiers.Length; i++)
            if (IsModifierOn(i) && Config.modifiers[i].type == t) return true;
        return false;
    }

    /// <summary>같은 타입 활성 디버프 값 합.</summary>
    public float ModValue(ModifierType t)
    {
        float v = 0f;
        for (int i = 0; i < Config.modifiers.Length; i++)
            if (IsModifierOn(i) && Config.modifiers[i].type == t) v += Config.modifiers[i].value;
        return v;
    }

    public void StartRun() { ResetRun(); LoadStage(); }

    public void GoLobby() { ResetRun(); Load(Config.lobbyScene); }

    public void ResetRun()
    {
        Stats = Config.baseStats.Clone();
        var w = Weapon;
        if (w != null)
        {
            Stats.damage *= w.damageMul;
            Stats.attackInterval *= w.intervalMul;
            Stats.projectileSpeed *= w.speedMul;
            Stats.attackRange *= w.rangeMul;
            Stats.spreadAngle = w.spreadAngle;
            Stats.projectileCount += w.bonusProjectiles;
        }
        StageIndex = 0;
        InBonus = false;
        pendingLevelUps = 0;
        State = GameState.Playing;
    }

    public int XpToNext => Mathf.RoundToInt(Config.xpBase * Mathf.Pow(Config.xpGrowth, Stats.level - 1));
    public int KillTarget => Config.baseKillTarget + StageIndex * Config.killTargetPerStage;
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
        Stats.xp += v;
        int need;
        while (Stats.xp >= (need = XpToNext))
        {
            Stats.xp -= need;
            Stats.level++;
            pendingLevelUps++;
        }
        if (pendingLevelUps > 0 && State == GameState.Playing) OpenLevelUp();
    }

    void OpenLevelUp()
    {
        int lv = Stats.level - pendingLevelUps + 1;
        ChoiceIsSpecial = lv % Config.specialEvery == 0;
        buffer.Clear();
        foreach (var u in Config.upgrades)
            if (u.special == ChoiceIsSpecial && u.weight > 0f
                && !(u.stat == StatType.Heal && Has(ModifierType.NoHealing))
                && !(u.stat == StatType.Jump && Stats.canJump)) buffer.Add(u);
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

    public void Choose(int i)
    {
        if (State != GameState.LevelUp || i >= Choices.Count) return;
        Stats.Apply(Choices[i]);
        pendingLevelUps--;
        if (pendingLevelUps > 0) { OpenLevelUp(); return; }
        State = GameState.Playing;
        Time.timeScale = 1f;
    }

    public void GameOver() { State = GameState.GameOver; Time.timeScale = 0f; }

    public void NextStage()
    {
        if (!InBonus && Config.bonusScenes.Length > 0 && StageIndex + 1 < Config.stageCount && Random.value < Config.bonusChance)
        {
            InBonus = true;
            Load(Config.bonusScenes[Random.Range(0, Config.bonusScenes.Length)]);
            return;
        }
        InBonus = false;
        StageIndex++;
        if (StageIndex >= Config.stageCount) { State = GameState.Victory; Time.timeScale = 0f; return; }
        LoadStage();
    }

    void LoadStage()
    {
        var s = Config.stageScenes;
        string next = s[Random.Range(0, s.Length)];
        if (s.Length > 1 && next == lastScene) next = s[(System.Array.IndexOf(s, next) + 1) % s.Length];
        Load(next);
    }

    void Load(string scene)
    {
        lastScene = scene;
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

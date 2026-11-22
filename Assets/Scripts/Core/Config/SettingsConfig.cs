using UnityEngine;

public enum SplitMode { Half, Pip }
public enum PipTarget { Creature, Watcher }

/// <summary>설정 창 기본값 + 프리팹. Resources/Config/SettingsConfig.asset (사용자 값은 PlayerPrefs, GameSettings).</summary>
[CreateAssetMenu(menuName = "Roguelike/Config/Settings")]
public class SettingsConfig : ScriptableObject
{
    static SettingsConfig cached;
    public static SettingsConfig I => cached ? cached : (cached = Resources.Load<SettingsConfig>("Config/SettingsConfig") ?? CreateInstance<SettingsConfig>());

    [Header("기본값")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float ambientVolume = 1f;
    public bool splitEnabled = true;
    public SplitMode splitMode = SplitMode.Half;
    public PipTarget pipTarget = PipTarget.Creature;

    [Header("UI")]
    [Tooltip("설정 창 프리팹 (HUD_Settings)")] public SettingsUI settingsPrefab;
    public string returnToBaseLabel = "거점으로 돌아가기";
    public string returnToTitleLabel = "타이틀로";
}

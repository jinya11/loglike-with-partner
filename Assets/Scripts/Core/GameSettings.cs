using System;
using UnityEngine;

/// <summary>
/// 사용자 설정 (음량, 화면 분할). PlayerPrefs에 저장 — 새 게임(save.json 삭제)과 무관.
/// 정적 싱글톤: 첫 접근 때 한 번 읽고, 바뀌면 Changed (카메라/효과음이 즉시 반영).
/// 마스터 음량은 AudioListener.volume (타이틀 BGM 포함 전체).
/// </summary>
public static class GameSettings
{
    const string KMaster = "set_master", KSfx = "set_sfx", KAmb = "set_ambient", KSplit = "set_split", KMode = "set_splitMode", KPip = "set_pipTarget";

    static bool loaded;
    static float master, sfx, ambient;
    static bool splitEnabled;
    static SplitMode splitMode;
    static PipTarget pipTarget;

    /// <summary>값이 바뀔 때마다.</summary>
    public static event Action Changed;
    /// <summary>바뀔 때마다 증가 (매 프레임 비교용, 할당 없음).</summary>
    public static int Version { get; private set; }

    public static float Master { get { Load(); return master; } set { Load(); master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KMaster, master); Apply(); } }
    public static float Sfx { get { Load(); return sfx; } set { Load(); sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KSfx, sfx); Apply(); } }
    public static float Ambient { get { Load(); return ambient; } set { Load(); ambient = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KAmb, ambient); Apply(); } }
    public static bool SplitEnabled { get { Load(); return splitEnabled; } set { Load(); splitEnabled = value; PlayerPrefs.SetInt(KSplit, value ? 1 : 0); Apply(); } }
    public static SplitMode Mode { get { Load(); return splitMode; } set { Load(); splitMode = value; PlayerPrefs.SetInt(KMode, (int)value); Apply(); } }
    public static PipTarget Pip { get { Load(); return pipTarget; } set { Load(); pipTarget = value; PlayerPrefs.SetInt(KPip, (int)value); Apply(); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() { loaded = false; Load(); }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        var d = SettingsConfig.I;
        master = PlayerPrefs.GetFloat(KMaster, d.masterVolume);
        sfx = PlayerPrefs.GetFloat(KSfx, d.sfxVolume);
        ambient = PlayerPrefs.GetFloat(KAmb, d.ambientVolume);
        splitEnabled = PlayerPrefs.GetInt(KSplit, d.splitEnabled ? 1 : 0) != 0;
        splitMode = (SplitMode)PlayerPrefs.GetInt(KMode, (int)d.splitMode);
        pipTarget = (PipTarget)PlayerPrefs.GetInt(KPip, (int)d.pipTarget);
        AudioListener.volume = master;
    }

    static void Apply()
    {
        AudioListener.volume = master;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>설정 창을 닫을 때 디스크에 기록.</summary>
    public static void Flush() => PlayerPrefs.Save();
}

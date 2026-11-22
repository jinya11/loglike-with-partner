using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 효과음 관리자 (영구 싱글톤). 첫 씬 로드 전에 스스로 생성되고 씬이 바뀌어도 유지됨.
/// AudioSource는 효과음용 1개(PlayOneShot으로 겹쳐 재생) + 배경 루프용 1개(바람, 음량을 따로 조절해야 해서 분리).
/// 사용: SfxManager.Play(SfxManager.S.enemyHit, pos). 설정은 Resources/Config/SfxConfig.
/// </summary>
public class SfxManager : Singleton<SfxManager>
{
    protected override bool Persistent => true;

    static SfxConfig cfg;
    /// <summary>효과음 설정 (없으면 빈 설정 → 무음).</summary>
    public static SfxConfig S => cfg ? cfg : cfg = Resources.Load<SfxConfig>("Config/SfxConfig") ?? ScriptableObject.CreateInstance<SfxConfig>();

    AudioSource oneShot, loop;
    float loopBase; // 루프 기본 음량 (환경음 설정 곱하기 전)
    string creatureName; CreatureSfx creatureSfx;
    PointerEventData pointer; EventSystem pointerEs; readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (I) return;
        new GameObject("SfxManager").AddComponent<SfxManager>();
    }

    protected override void Awake()
    {
        base.Awake();
        if (I != this) return;
        oneShot = gameObject.AddComponent<AudioSource>(); oneShot.playOnAwake = false;
        loop = gameObject.AddComponent<AudioSource>(); loop.playOnAwake = false; loop.loop = true;
        GameSettings.Changed += () => { if (loop) loop.volume = loopBase * GameSettings.Ambient; }; // 설정 창 환경음 즉시 반영
        RestoreWind();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => RestoreWind(); // 모래폭풍 중 씬이 바뀌어도 바람으로 복귀
    }

    /// <summary>지금 생명체의 효과음 (WardConfig 이름으로 찾음, 없으면 null).</summary>
    public static CreatureSfx Creature
    {
        get
        {
            if (!I || Ward.I == null || Ward.I.Creature == null) return null;
            string n = Ward.I.Creature.name;
            if (n != I.creatureName) { I.creatureName = n; I.creatureSfx = S.Creature(n); }
            return I.creatureSfx;
        }
    }

    /// <summary>2D 효과음 (UI, 레벨업 등).</summary>
    public static void Play(Sfx s, float volume = 1f)
    {
        if (!I || s == null || !s.clip) return;
        float now = Time.unscaledTime;
        if (now - s.last < s.cooldown) return;
        s.last = now;
        I.oneShot.PlayOneShot(s.clip, s.volume * volume * S.masterVolume * GameSettings.Sfx);
    }

    /// <summary>월드 위치 효과음: 카메라에서 멀면 작아짐.</summary>
    public static void Play(Sfx s, Vector3 pos, float volume = 1f)
    {
        if (s == null || !s.clip) return;
        var cam = Camera.main;
        if (cam)
        {
            Vector3 d = pos - cam.transform.position; d.y = 0f;
            float far = d.magnitude - S.hearRange;
            if (far > 0f) { volume *= Mathf.Clamp01(1f - far / S.hearRange); if (volume <= 0.02f) return; }
        }
        Play(s, volume);
    }

    /// <summary>배경 루프 교체 (한 번에 하나).</summary>
    public static void StartLoop(Sfx s)
    {
        if (!I) return;
        var l = I.loop;
        if (s == null || !s.clip || s.volume <= 0f) { l.Stop(); return; }
        I.loopBase = s.volume * S.masterVolume;
        l.volume = I.loopBase * GameSettings.Ambient; // 환경음 (설정 창)
        if (l.clip == s.clip && l.isPlaying) return;
        l.clip = s.clip; l.Play(); l.time = Random.Range(0f, s.clip.length * 0.8f);
    }

    public static void RestoreWind() => StartLoop(S.wind);

    // 모든 UI 버튼 클릭 소리: 클릭한 프레임에만 UI 레이캐스트 (버튼마다 연결할 필요 없음, 런타임 생성 버튼 포함)
    void Update()
    {
        var m = Mouse.current;
        if (m == null || !m.leftButton.wasPressedThisFrame) return;
        var es = EventSystem.current;
        if (!es) return;
        if (pointer == null || pointerEs != es) { pointer = new PointerEventData(es); pointerEs = es; }
        pointer.position = m.position.ReadValue();
        uiHits.Clear();
        es.RaycastAll(pointer, uiHits);
        foreach (var h in uiHits)
        {
            var sel = h.gameObject.GetComponentInParent<Selectable>();
            if (sel && sel.IsInteractable()) { Play(S.uiClick); return; }
        }
    }
}

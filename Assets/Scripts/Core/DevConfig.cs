using UnityEngine;

/// <summary>
/// 개발자 전용 설정 (Resources/DevConfig). 게임 밸런스용 GameConfig와 분리.
/// devMode가 꺼져 있으면 디버그 기능은 키로도 켤 수 없음.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Dev Config")]
public class DevConfig : ScriptableObject
{
    [Tooltip("개발자 모드. 꺼져 있으면 모든 디버그 기능 비활성")]
    public bool devMode = true;
    [Tooltip("빌드에서도 개발자 모드 허용 (끄면 에디터/개발 빌드에서만)")]
    public bool allowInReleaseBuild = false;

    [Header("감시자 잠금")]
    [Tooltip("모든 감시자를 잠금 해제된 것으로 취급 (저장 데이터는 건드리지 않음)")]
    public bool unlockAllCompanions = false;
    [Tooltip("unlockAllCompanions를 켜고 끄는 키")] public UnityEngine.InputSystem.Key unlockAllKey = UnityEngine.InputSystem.Key.F2;

    /// <summary>개발자 모드에서 모든 감시자 잠금 해제 중인지.</summary>
    public static bool UnlockAll => Enabled && I.unlockAllCompanions;

    [Header("생명체 감지 디버그")]
    [Tooltip("시작 시 바로 표시")] public bool wardDebugOnStart = false;
    [Tooltip("켜고 끄는 키 (Input System Key 이름)")] public UnityEngine.InputSystem.Key wardDebugKey = UnityEngine.InputSystem.Key.F1;
    [Tooltip("월드에 감지 범위/경로/도망 목표 선 표시")] public bool drawWorldGizmos = true;
    [Tooltip("상태창 갱신 주기(초). 0이면 매 프레임")] public float refreshInterval = 0.1f;
    [Tooltip("상태창 글자 크기")] public int fontSize = 11;
    [Tooltip("상태창 처음 위치 (화면 왼쪽 위 기준 픽셀). 마우스로 끌어서 옮길 수 있음")] public Vector2 panelPosition = new Vector2(12f, 140f);
    public Color fleeRadiusColor = new Color(1f, 0.85f, 0.2f), dangerRadiusColor = new Color(1f, 0.3f, 0.3f);
    public Color surroundRadiusColor = new Color(0.7f, 0.45f, 1f), pathColor = new Color(0.3f, 1f, 0.5f);

    static DevConfig cached;
    static bool loaded;

    /// <summary>Resources에서 1회 로드 (없으면 null → 개발자 기능 없음).</summary>
    public static DevConfig I
    {
        get
        {
            if (!loaded) { cached = Resources.Load<DevConfig>("DevConfig"); loaded = true; }
            return cached;
        }
    }

    /// <summary>현재 개발자 모드가 실제로 유효한지.</summary>
    public static bool Enabled
    {
        get
        {
            var c = I;
            if (c == null || !c.devMode) return false;
            return c.allowInReleaseBuild || Application.isEditor || Debug.isDebugBuild;
        }
    }
}

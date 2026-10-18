using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 추적 카메라 + 시점 전환 (전투 스테이지에서 V 키로 전환).
/// 사이드뷰(스테이지 시점, 조준점 추적) / 탑뷰(동반자 시점). 로비에서는 사이드뷰 고정.
/// 감시자 폭격 조준 중에는 맵 전체 탑뷰(Overview).
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public enum Mode { Side, Top }

    public static CameraFollow I { get; private set; }

    [Header("사이드뷰 (스테이지 시점)")]
    [SerializeField] Vector3 offset = new Vector3(0f, 11f, -17f);
    [SerializeField] float sideFov = 60f;
    [Header("탑뷰 (동반자 시점: 북쪽 하늘에서 내려다봄)")]
    [SerializeField] float topDistance = 30f, topPitch = 72f, topFov = 50f;
    [Header("전체 맵 탑뷰 (감시자 폭격 조준)")]
    [SerializeField] float overviewDistance = 95f, overviewPitch = 82f;
    [Header("공통")]
    [SerializeField] float smooth = 8f;
    [SerializeField] Key switchKey = Key.V;
    [Tooltip("HUD 표시용 시점 이름 (Mode 순서)")]
    [SerializeField] string[] modeNames = { "사이드뷰 (스테이지)", "탑뷰 (동반자)" };

    const int ModeCount = 2;
    Camera cam;
    Mode mode;
    bool overview;

    public Camera Cam => cam;
    public Mode Current => mode;
    public string CurrentName => modeNames != null && (int)mode < modeNames.Length ? modeNames[(int)mode] : mode.ToString();
    /// <summary>이동 입력 기준 방향 (카메라 수평 회전).</summary>
    public float MoveYaw => transform.eulerAngles.y;

    /// <summary>감시자 폭격 조준 중: 맵 전체를 내려다보는 탑뷰로 강제.</summary>
    public void SetOverview(bool on)
    {
        overview = on;
        ApplyFov();
    }

    void Awake()
    {
        I = this;
        cam = GetComponent<Camera>();
    }

    void OnDestroy() { if (I == this) I = null; }

    void Start()
    {
        Compute(out var pos, out var rot);
        transform.SetPositionAndRotation(pos, rot);
        ApplyFov();
    }

    void LateUpdate()
    {
        if (PlayerController.I == null) return;
        var kb = Keyboard.current;
        var gm = GameManager.I;
        bool canSwitch = !overview && Ward.I != null && gm != null && gm.State == GameState.Playing && !gm.InputBlocked;
        if (canSwitch && kb != null && kb[switchKey].wasPressedThisFrame)
        {
            mode = (Mode)(((int)mode + 1) % ModeCount);
            ApplyFov();
        }
        if (Ward.I == null && mode != Mode.Side) { mode = Mode.Side; ApplyFov(); }

        Compute(out var p, out var r);
        float k = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, p, k), Quaternion.Slerp(transform.rotation, r, k));
    }

    void Compute(out Vector3 pos, out Quaternion rot)
    {
        Vector3 player = PlayerController.I ? PlayerController.I.Position : Vector3.zero;
        var ward = Ward.I;
        if (overview)
        {
            // 감시자(북쪽)의 시선으로 맵 전체
            rot = Quaternion.Euler(overviewPitch, 180f, 0f);
            pos = -(rot * Vector3.forward) * overviewDistance;
            return;
        }
        if (mode == Mode.Top)
        {
            // 동반자(북쪽)가 내려다보는 방향: 남쪽을 향해 내려다봄. 조준점(플레이어)과 생명체 사이를 중심으로
            Vector3 focus = ward ? (player + ward.Position) * 0.5f : player;
            rot = Quaternion.Euler(topPitch, 180f, 0f);
            pos = focus - rot * Vector3.forward * topDistance;
            return;
        }
        rot = Quaternion.LookRotation(-offset);
        pos = player + offset;
    }

    void ApplyFov()
    {
        if (cam == null) return;
        cam.fieldOfView = overview || mode == Mode.Top ? topFov : sideFov;
        cam.nearClipPlane = 0.3f;
    }
}

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
        if (on == overview) return;
        overview = on;
        // 지역 스테이지의 안개가 멀리서 내려다보는 탑뷰를 가리지 않게 잠시 끔
        if (on) { fogWasOn = RenderSettings.fog; RenderSettings.fog = false; }
        else RenderSettings.fog = fogWasOn;
        ApplyFov();
    }

    bool fogWasOn;

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

        UpdateSplit();
        Compute(out var p, out var r);
        float k = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, p, k), Quaternion.Slerp(transform.rotation, r, k));
        if (split)
        {
            ComputeAt(Ward.I.Position, out var wp, out var wr);
            wardCam.transform.SetPositionAndRotation(Vector3.Lerp(wardCam.transform.position, wp, k), Quaternion.Slerp(wardCam.transform.rotation, wr, k));
        }
    }

    // ---- 듀얼 카메라: 고리를 풀고 생명체와 멀어지면 화면을 둘로 나눔 (왼쪽 감시자 / 오른쪽 생명체) ----
    Camera wardCam;
    bool split;
    /// <summary>화면이 둘로 나뉘어 있는지 (왼쪽: 감시자, 오른쪽: 생명체).</summary>
    public bool Split => split;

    void UpdateSplit()
    {
        var ward = Ward.I;
        var gm = GameManager.I;
        bool want = false;
        if (!overview && ward != null && gm != null && gm.State == GameState.Playing && ward.Order != Ward.Command.Linked && PlayerController.I.Formless)
        {
            Vector3 d = ward.Position - PlayerController.I.Position; d.y = 0f;
            float dist = gm.Config.ward.splitDistance;
            want = d.magnitude > (split ? dist * 0.8f : dist); // 경계에서 깜빡이지 않게
        }
        if (want == split) return;
        split = want;
        cam.rect = split ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
        if (split)
        {
            if (!wardCam)
            {
                wardCam = new GameObject("WardCamera").AddComponent<Camera>();
                wardCam.CopyFrom(cam);
                wardCam.depth = cam.depth + 1;
                var src = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (src) wardCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = src.renderPostProcessing;
            }
            wardCam.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            wardCam.fieldOfView = cam.fieldOfView;
            ComputeAt(ward.Position, out var wp, out var wr);
            wardCam.transform.SetPositionAndRotation(wp, wr);
        }
        if (wardCam) wardCam.gameObject.SetActive(split);
    }

    /// <summary>나뉜 화면에서 대상 하나를 현재 시점(사이드/탑)으로 비추는 위치.</summary>
    void ComputeAt(Vector3 target, out Vector3 pos, out Quaternion rot)
    {
        if (mode == Mode.Top)
        {
            rot = Quaternion.Euler(topPitch, 180f, 0f);
            pos = target - rot * Vector3.forward * topDistance;
            return;
        }
        rot = Quaternion.LookRotation(-offset);
        pos = target + offset;
    }

    void Compute(out Vector3 pos, out Quaternion rot)
    {
        Vector3 player = PlayerController.I ? PlayerController.I.Position : Vector3.zero;
        var ward = Ward.I;
        if (overview)
        {
            // 감시자(북쪽)의 시선으로 맵 전체
            rot = Quaternion.Euler(overviewPitch, 180f, 0f);
            pos = Arena.Center - (rot * Vector3.forward) * Mathf.Max(overviewDistance, Arena.Extent * 2.1f); // 넓은 지역 스테이지(열린 칸 포함)도 전체가 보이게
            return;
        }
        if (mode == Mode.Top)
        {
            // 동반자(북쪽)가 내려다보는 방향: 남쪽을 향해 내려다봄. 조준점(플레이어)과 생명체 사이를 중심으로 (화면이 나뉘면 플레이어만)
            Vector3 focus = ward && !split ? (player + ward.Position) * 0.5f : player;
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
        if (wardCam) wardCam.fieldOfView = cam.fieldOfView;
    }
}

using UnityEngine;

/// <summary>
/// 작은 생명체 모델(리깅된 프리팹) 루트에 붙는 표현 담당. 이동 속도 → 걷기/대기 블렌드 + 걸음 빠르기.
/// CreatureRigBuilder가 프리팹을 만들 때 자동으로 설정.
/// </summary>
public class CreatureVisual : MonoBehaviour
{
    public const string SpeedParam = "Speed";
    static readonly int SpeedId = Animator.StringToHash(SpeedParam);

    [SerializeField] Animator animator;
    [Tooltip("1인칭 시점 카메라 위치")] [SerializeField] Transform eyeAnchor;
    [Tooltip("모델 키 (월드 단위)")] [SerializeField] float height = 0.9f;
    [Tooltip("이 이동 속도에서 걷기 모션 속도 1")] [SerializeField] float walkRefSpeed = 1.6f;
    [Tooltip("걷기 모션 최대 배속")] [SerializeField] float maxAnimSpeed = 2.5f;

    float shownSpeed;

    public Transform EyeAnchor => eyeAnchor;
    /// <summary>걷기 모션이 있는지 (리깅 전 모델은 false).</summary>
    public bool Animated => animator;
    public float Height => height;
    public Renderer[] Renderers { get; private set; }

    public void Setup(Animator anim, Transform eye, float h, float refSpeed)
    {
        animator = anim; eyeAnchor = eye; height = h; walkRefSpeed = refSpeed;
    }

    void Awake() { Renderers = GetComponentsInChildren<Renderer>(true); }

    /// <summary>현재 수평 이동 속도(월드 단위/초)를 매 프레임 전달.</summary>
    public void SetMoveSpeed(float speed, float dt)
    {
        if (!animator) return;
        shownSpeed = Mathf.MoveTowards(shownSpeed, speed, 12f * dt); // 급정지/급출발 완화
        float k = shownSpeed / walkRefSpeed;
        animator.SetFloat(SpeedId, Mathf.Clamp01(k * 1.5f));
        animator.speed = k > 1f ? Mathf.Min(k, maxAnimSpeed) : 1f;
    }

    /// <summary>게임 일시정지/공포 등으로 멈출 때.</summary>
    // Animator를 끄면 기본 자세(T자)로 돌아가므로 끄지 않고 속도만 0으로
    public void SetFrozen(bool frozen) { if (animator && frozen) animator.speed = 0f; }
}

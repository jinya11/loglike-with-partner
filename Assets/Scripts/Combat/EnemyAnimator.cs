using UnityEngine;

/// <summary>
/// 적 모델(Animator) 표현 담당. 움직임은 전부 Animator 컨트롤러의 클립이 하고, 여기서는 파라미터만 넘김.
/// Speed(0=대기, 1=걷기 블렌드) / Charge(돌진 중) / Ability(능력 사용 트리거).
/// EnemyRigBuilder가 적 모델 프리팹을 만들 때 설정.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAnimator : MonoBehaviour
{
    public const string SpeedParam = "Speed", ChargeParam = "Charge", AbilityParam = "Ability";
    static readonly int SpeedId = Animator.StringToHash(SpeedParam), ChargeId = Animator.StringToHash(ChargeParam), AbilityId = Animator.StringToHash(AbilityParam);

    [SerializeField] Animator animator;
    [Tooltip("이 이동 속도에서 걷기 모션 속도 1")] [SerializeField] float walkRefSpeed = 2.6f;
    [Tooltip("걷기 모션 최대 배속")] [SerializeField] float maxAnimSpeed = 2f;
    [SerializeField] bool hasCharge, hasAbility;

    float shownSpeed;
    bool charging;

    public void Setup(Animator anim, float refSpeed, bool charge, bool ability)
    {
        animator = anim; walkRefSpeed = refSpeed; hasCharge = charge; hasAbility = ability;
    }

    /// <summary>풀에서 다시 꺼낼 때.</summary>
    public void ResetState()
    {
        shownSpeed = 0f; charging = false;
        animator.speed = 1f;
        animator.SetFloat(SpeedId, 0f);
        if (hasCharge) animator.SetBool(ChargeId, false);
        if (hasAbility) animator.ResetTrigger(AbilityId);
    }

    /// <summary>현재 수평 이동 속도(월드 단위/초). 매 틱.</summary>
    public void SetMove(float speed, float dt)
    {
        shownSpeed = Mathf.MoveTowards(shownSpeed, speed, 12f * dt);
        float k = shownSpeed / walkRefSpeed;
        animator.SetFloat(SpeedId, Mathf.Clamp01(k * 1.5f));
        animator.speed = charging ? 1f : (k > 1f ? Mathf.Min(k, maxAnimSpeed) : 1f);
    }

    public void SetCharging(bool on)
    {
        if (!hasCharge || on == charging) return;
        charging = on;
        animator.SetBool(ChargeId, on);
    }

    public void PlayAbility() { if (hasAbility) animator.SetTrigger(AbilityId); }
}

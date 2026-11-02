using UnityEngine;

/// <summary>
/// 던지기 공격 (작은 토끼: 당근). 적이 있는 곳으로 포물선을 그리며 날아가 떨어진 자리에 작은 범위 피해.
/// 날아가는 도중 적에게 닿아도 그 자리에서 터짐.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Creature Attack/Throw")]
public class ThrowAttack : CreatureAttack
{
    [Tooltip("던지는 물체 (ThrownProjectile 프리팹)")] public ThrownProjectile projectile;
    [Tooltip("포물선 최고 높이")] public float arcHeight = 2.2f;
    [Tooltip("탄속(PlayerStats.projectileSpeed) 대비 비행 속도 배율")] public float speedMul = 0.55f;
    [Tooltip("착지 피해 반경 (범위 강화 배율 적용)")] public float splashRadius = 1.1f;
    [Tooltip("피해 배율")] public float damageMul = 1.2f;
    [Tooltip("손 높이")] public float handHeight = 0.7f;
    public Color splashColor = new Color(1f, 0.55f, 0.15f);

    protected override void Fire(Enemy target, PlayerStats s)
    {
        Vector3 from = Ctx.Position + Vector3.up * handHeight;
        Vector3 to = target.transform.position; to.y = 0f;
        Vector3 d = to - Ctx.Position; d.y = 0f;
        float dist = Mathf.Max(1f, d.magnitude);
        Vector3 dir = d / dist;
        float time = dist / Mathf.Max(1f, s.projectileSpeed * speedMul);
        for (int i = 0; i < s.projectileCount; i++)
        {
            Vector3 p = Ctx.Position + SpreadDir(dir, s, i) * dist; p.y = 0f;
            Ctx.Stage.Throw(projectile, from, Arena.Clamp(p), time, arcHeight, s.damage * damageMul, splashRadius * s.areaMul, splashColor);
        }
    }
}

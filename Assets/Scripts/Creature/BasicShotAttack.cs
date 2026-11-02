using UnityEngine;

/// <summary>기본 공격: 가장 가까운 적에게 곧게 날아가는 탄환 (공격이 정해지지 않은 생명체).</summary>
[CreateAssetMenu(menuName = "Roguelike/Creature Attack/Basic Shot")]
public class BasicShotAttack : CreatureAttack
{
    [Tooltip("발사 높이")] public float muzzleHeight = 0.6f;

    protected override void Fire(Enemy target, PlayerStats s)
    {
        Vector3 origin = Ctx.Position + Vector3.up * muzzleHeight;
        Vector3 dir = target.transform.position - Ctx.Position; dir.y = 0f; dir.Normalize();
        for (int i = 0; i < s.projectileCount; i++)
            Ctx.Stage.FireProjectile(origin, SpreadDir(dir, s, i), s.projectileSpeed, s.damage);
    }
}

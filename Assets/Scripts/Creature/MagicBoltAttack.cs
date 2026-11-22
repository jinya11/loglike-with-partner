using UnityEngine;

/// <summary>
/// 마법 탄 공격 (마법사). 기본 탄환과 같지만 피해가 약하고(damageMul), 성장 노드 효과(연쇄/연소/빙결/저주)가 명중 시 덧붙음.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Creature Attack/Magic Bolt")]
public class MagicBoltAttack : CreatureAttack
{
    [Tooltip("발사 높이")] public float muzzleHeight = 0.7f;
    [Tooltip("피해 배율 (마법사는 메인 노드 효과가 강한 대신 기본 평타가 약함)")] public float damageMul = 0.7f;

    WizardGrowth growth;

    protected override void OnBound() => growth = WizardGrowth.Ensure(GrowthProgress.I.Loadout(Ctx.Def?.growth));

    protected override void Fire(Vector3 target, PlayerStats s)
    {
        Vector3 origin = Ctx.Position + Vector3.up * muzzleHeight;
        Vector3 dir = target - Ctx.Position; dir.y = 0f; dir.Normalize();
        for (int i = 0; i < s.projectileCount; i++)
        {
            var p = Ctx.Stage.FireProjectile(origin, SpreadDir(dir, s, i), s.projectileSpeed, s.damage * damageMul);
            if (growth) p.HitEnemy = growth.BoltHit;
        }
    }
}

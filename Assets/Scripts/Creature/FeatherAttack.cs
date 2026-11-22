using UnityEngine;

/// <summary>
/// 깃털 던지기 (새 소녀). 적 방향으로 옆으로 살짝 휘는 포물선을 그리며 날아가는 깃털. 첫 적에 맞으면 사라짐.
/// 성장 노드 효과(날개짓/깃털 드롭/공속 스택 등)는 BirdGrowth.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/Creature Attack/Feather")]
public class FeatherAttack : CreatureAttack
{
    [Tooltip("손 높이")] public float handHeight = 0.8f;
    [Tooltip("탄속(PlayerStats.projectileSpeed) 대비 비행 속도 배율")] public float speedMul = 0.8f;
    [Tooltip("피해 배율")] public float damageMul = 1f;
    [Tooltip("옆으로 휘는 정도 (발사마다 좌우 번갈아). 클수록 많이 휨 — 가운데 깃털은 대상을 따라가서 끝에서 정확히 닿음")] public float curve = 0.6f;

    BirdGrowth growth;
    int flip = 1;

    protected override void OnBound() => growth = BirdGrowth.Ensure(GrowthProgress.I.Loadout(Ctx.Def?.growth));
    protected override float IntervalMul => growth ? growth.IntervalMul : 1f;

    protected override void Fire(Vector3 target, PlayerStats s)
    {
        if (!growth) return;
        Vector3 d = target - Ctx.Position; d.y = 0f;
        float dist = Mathf.Max(2f, d.magnitude);
        Vector3 dir = d / Mathf.Max(0.001f, d.magnitude);
        flip = -flip;
        var spec = new FeatherShot.Spec
        {
            from = Ctx.Position + Vector3.up * handHeight, speed = Mathf.Max(4f, s.projectileSpeed * speedMul),
            range = Mathf.Max(s.attackRange, dist + 2f), damage = s.damage * damageMul, curve = curve * flip, primary = true,
        };
        var aimed = NearestEnemy(target, 1.5f); // 겨눈 적 (유도 대상)
        for (int i = 0; i < s.projectileCount; i++)
        {
            spec.dir = SpreadDir(dir, s, i);
            spec.target = i == (s.projectileCount - 1) / 2 ? aimed : null; // 가운데 깃털만 유도, 나머지는 퍼짐
            growth.Fire(spec);
        }
    }

    static Enemy NearestEnemy(Vector3 p, float radius)
    {
        Enemy best = null; float bestSq = radius * radius;
        var list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)
        {
            Vector3 v = list[i].transform.position - p; v.y = 0f;
            float sq = v.sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = list[i]; }
        }
        return best;
    }
}

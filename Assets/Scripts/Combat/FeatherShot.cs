using UnityEngine;

/// <summary>
/// 깃털 투사체 (새 소녀 평타 / 거침없는 날개의 4방향 깃털). 방향으로 날아가며 옆(curve) 또는 위(arc)로 포물선을 그림.
/// 첫 적에 맞으면 BirdGrowth.OnFeatherHit → 피해 후 사라짐. BirdGrowth가 풀에서 꺼내 쓰고 Tick.
/// </summary>
public class FeatherShot : Ticked<FeatherShot>
{
    static readonly Collider[] hits = new Collider[8];

    [Tooltip("모델 (앞쪽 = +Z)")] [SerializeField] Transform model;

    public struct Spec
    {
        public Vector3 from, dir;
        public float speed, range, damage, hitRadius;
        [Tooltip("옆으로 휘는 정도 (부호 = 방향)")] public float curve;
        [Tooltip("위로 뜨는 포물선 높이")] public float arc;
        public bool primary; // 평타 (날개짓/깃털 드롭 대상). 4방향 깃털은 false
        [Tooltip("유도 대상 (있으면 그 적의 현재 위치로 날아감, 없으면 dir 방향 직진)")] public Enemy target;
    }

    /// <summary>모델 부분 (바닥 깃털 표시용으로 복제).</summary>
    public GameObject Visual => model ? model.gameObject : gameObject;

    Spec s;
    float traveled;
    Vector3 side, last, aim;

    public void Init(in Spec spec)
    {
        s = spec; traveled = 0f;
        side = Vector3.Cross(Vector3.up, s.dir);
        last = s.from;
        aim = s.from + s.dir * s.range;
        transform.SetPositionAndRotation(s.from, Quaternion.LookRotation(s.dir));
    }

    public override void Tick(float dt)
    {
        traveled += s.speed * dt;
        // 유도: 대상이 살아 있으면 현재 위치를 끝점으로 (휘는 곡선은 끝에서 0이 되어 정확히 대상에 닿음)
        if (s.target && s.target.isActiveAndEnabled) { aim = s.target.transform.position; aim.y = s.from.y; }
        else s.target = null;
        Vector3 to = aim - s.from; to.y = 0f;
        float total = Mathf.Max(0.5f, to.magnitude);
        float k = Mathf.Clamp01(traveled / total), bend = 4f * k * (1f - k);
        Vector3 p = s.from + to * k + side * (s.curve * bend) + Vector3.up * (s.arc * bend);
        if (k >= 1f && traveled < s.range) p += (to / total) * (traveled - total); // 놓치면 같은 방향으로 사거리까지 직진
        bool done = traveled >= Mathf.Max(s.range, total);
        if (Ground.HasTerrain) p.y += Ground.HeightAt(p) - Ground.HeightAt(s.from);
        Vector3 v = p - last;
        if (v.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(v);
        transform.position = last = p;

        var stage = StageDirector.I;
        int n = Physics.OverlapSphereNonAlloc(p, s.hitRadius, hits, stage.EnemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].TryGetComponent(out Enemy e))
            {
                if (!e.isActiveAndEnabled) continue;
                var g = BirdGrowth.I;
                if (g) g.OnFeatherHit(e, p, s.damage, s.primary);
                if (e.isActiveAndEnabled) e.Hit(s.damage, p - s.dir);
                SfxManager.Play(SfxManager.Creature?.impact, p);
                Despawn();
                return;
            }
            if (hits[i].TryGetComponent(out StageTarget st) && st.Alive) { st.Hit(s.damage, p - s.dir); Despawn(); return; }
        }
        if (done) Despawn();
    }
}

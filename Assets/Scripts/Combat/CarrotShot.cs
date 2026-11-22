using UnityEngine;

/// <summary>
/// 직선으로 날아가는 당근 (토끼 성장: 관통 당근 / 소형 당근 / 튕긴 당근). CarrotGrowth가 풀에서 꺼내 쓰고 Tick.
/// 맞을 때마다 CarrotGrowth.OnHit (꼬치/튕김/피흡) → 피해. 관통 수가 남으면 피해 × falloff로 계속 날아감.
/// </summary>
public class CarrotShot : Ticked<CarrotShot>
{
    static readonly Collider[] hits = new Collider[8];

    [Tooltip("회전하는 모델 (앞쪽 = +Z)")] [SerializeField] Transform spinner;
    [Tooltip("초당 회전 (도, 진행 방향 축)")] [SerializeField] float spinSpeed = 540f;

    /// <summary>발사 정보 (풀 재사용 시 매번 덮어씀).</summary>
    public struct Spec
    {
        public Vector3 dir;
        public float speed, damage, range, hitRadius, falloff, nearMul, farMul;
        public int pierce;
        public bool canBounce, pierceMode, smallMode;
    }

    /// <summary>모델 부분 (꼬친 당근 표시용으로 복제).</summary>
    public GameObject Visual => spinner ? spinner.gameObject : gameObject;

    Spec s;
    float traveled, lift;
    int hitCount;
    readonly Enemy[] hitEnemies = new Enemy[8];

    public void Init(in Spec spec)
    {
        s = spec;
        traveled = 0f; hitCount = 0;
        lift = transform.position.y - Ground.HeightAt(transform.position);
        transform.rotation = Quaternion.LookRotation(s.dir);
        if (!spinner) spinner = transform;
    }

    bool AlreadyHit(Enemy e)
    {
        for (int i = 0; i < hitCount; i++) if (hitEnemies[i] == e) return true;
        return false;
    }

    public override void Tick(float dt)
    {
        float step = s.speed * dt;
        Vector3 pos = transform.position + s.dir * step;
        traveled += step;
        if (Ground.HasTerrain) pos.y = Ground.HeightAt(pos) + lift;
        transform.position = pos;
        if (spinner != transform) spinner.Rotate(0f, 0f, spinSpeed * dt, Space.Self);

        var stage = StageDirector.I;
        int n = Physics.OverlapSphereNonAlloc(pos, s.hitRadius, hits, stage.EnemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].TryGetComponent(out Enemy e))
            {
                if (!e.isActiveAndEnabled || AlreadyHit(e)) continue;
                float d = s.damage * Mathf.Lerp(s.nearMul, s.farMul, traveled / Mathf.Max(0.1f, s.range));
                if (hitCount < hitEnemies.Length) hitEnemies[hitCount++] = e;
                var g = CarrotGrowth.I;
                if (g) g.OnHit(e, pos, d, s.canBounce, s.pierceMode, s.smallMode);
                if (e.isActiveAndEnabled) e.Hit(d, pos - s.dir);
                SfxManager.Play(SfxManager.Creature?.impact, pos);
                if (--s.pierce <= 0) { Despawn(); return; }
                s.damage *= s.falloff;
            }
            else if (hits[i].TryGetComponent(out StageTarget st) && st.Alive) // 부술 수 있는 오브젝트 (무덤 등)
            {
                st.Hit(s.damage, pos - s.dir);
                Despawn();
                return;
            }
        }
        if (traveled >= s.range) Despawn();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        System.Array.Clear(hitEnemies, 0, hitEnemies.Length); // 풀에 있는 동안 적 참조를 잡고 있지 않게
    }
}

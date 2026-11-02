using UnityEngine;

/// <summary>포물선으로 던져지는 물체 (당근 등). 착지하거나 적에게 닿으면 작은 범위 피해. StageDirector.Throw로 생성.</summary>
public class ThrownProjectile : Ticked<ThrownProjectile>
{
    static readonly Collider[] hits = new Collider[4];

    [Tooltip("회전하는 모델 (비우면 자신)")] [SerializeField] Transform spinner;
    [Tooltip("초당 회전 (도)")] [SerializeField] float spinSpeed = 720f;
    [Tooltip("날아가는 도중 적과 닿는 반경")] [SerializeField] float hitRadius = 0.5f;

    Vector3 from, to;
    float duration, t, arc, damage, radius;
    Color color;
    Vector3 spinAxis;

    public void Init(Vector3 from, Vector3 to, float duration, float arc, float damage, float radius, Color color)
    {
        this.from = from; this.to = to; this.duration = Mathf.Max(0.05f, duration);
        this.arc = arc; this.damage = damage; this.radius = radius; this.color = color;
        t = 0f;
        Vector3 d = to - from; d.y = 0f;
        spinAxis = d.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, d.normalized) : Vector3.right; // 앞으로 구르듯이
        if (!spinner) spinner = transform;
        transform.position = from;
    }

    public override void Tick(float dt)
    {
        t += dt / duration;
        float k = Mathf.Min(t, 1f);
        Vector3 p = Vector3.Lerp(from, to, k);
        p.y = Mathf.Lerp(from.y, to.y, k) + arc * 4f * k * (1f - k);
        transform.position = p;
        spinner.Rotate(spinAxis, spinSpeed * dt, Space.World);

        var stage = StageDirector.I;
        if (k >= 1f || (k > 0.15f && Physics.OverlapSphereNonAlloc(p, hitRadius, hits, stage.EnemyMask, QueryTriggerInteraction.Collide) > 0))
            Land(stage, p);
    }

    void Land(StageDirector stage, Vector3 p)
    {
        p.y = 0f;
        stage.DamageArea(p, radius, damage);
        float d = radius * 2f;
        stage.SpawnFx(p + Vector3.up * 0.04f, new Vector3(0.3f, 0.02f, 0.3f), new Vector3(d, 0.02f, d), 0.25f, color);
        Despawn();
    }
}

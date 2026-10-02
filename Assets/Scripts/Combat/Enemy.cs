using UnityEngine;

/// <summary>Rigidbody 속도로 이동 → 장애물/벽/다른 적과 충돌. 플레이어와는 레이어로 충돌 제외(접촉 피해는 거리 판정).</summary>
[RequireComponent(typeof(Rigidbody))]
public class Enemy : Ticked<Enemy>
{
    const float ContactSq = 1f;
    [SerializeField] Transform visual;
    Rigidbody rb;
    float hp, speed, damage;
    Vector3 knock;

    void Awake() { rb = GetComponent<Rigidbody>(); }

    public void Init(float hp, float speed, float damage)
    {
        this.hp = hp; this.speed = speed * Random.Range(0.85f, 1.15f); this.damage = damage;
        knock = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
    }

    public override void Tick(float dt)
    {
        var player = PlayerController.I;
        Vector3 to = player.Position - rb.position;
        to.y = 0f;
        float sq = to.sqrMagnitude;
        if (sq < ContactSq && !player.Airborne) player.TakeDamage(damage);
        Vector3 v = knock;
        if (sq > 0.0001f)
        {
            Vector3 dir = to / Mathf.Sqrt(sq);
            v += dir * speed;
            visual.rotation = Quaternion.LookRotation(dir);
        }
        rb.linearVelocity = v;
        knock = Vector3.MoveTowards(knock, Vector3.zero, 30f * dt);
    }

    /// <summary>외부 힘으로 밀기 (동반자 돌풍 등). 감쇠되며 사라짐.</summary>
    public void Push(Vector3 velocity) => knock = velocity;

    public void Hit(float dmg, Vector3 from)
    {
        hp -= dmg;
        Vector3 k = rb.position - from; k.y = 0f;
        knock = k.normalized * 8f;
        if (hp <= 0f)
        {
            StageDirector.I.OnEnemyKilled(rb.position);
            Despawn();
        }
    }
}

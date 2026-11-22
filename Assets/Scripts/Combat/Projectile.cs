using UnityEngine;

public class Projectile : Ticked<Projectile>
{
    static readonly Collider[] hits = new Collider[8];
    Vector3 velocity;
    float damage, life, lift;

    /// <summary>적에게 맞음 (피해 전: 적, 위치, 피해). 마법사 성장 효과용. Init마다 비워짐.</summary>
    public System.Action<Enemy, Vector3, float> HitEnemy;

    public void Init(Vector3 dir, float speed, float damage)
    {
        velocity = dir * speed; this.damage = damage; life = 1.2f; HitEnemy = null;
        lift = transform.position.y - Ground.HeightAt(transform.position); // 지면에서의 높이 유지 (언덕을 따라 날아감)
    }

    public override void Tick(float dt)
    {
        Vector3 pos = transform.position + velocity * dt;
        if (Ground.HasTerrain) pos.y = Ground.HeightAt(pos) + lift;
        transform.position = pos;
        int n = Physics.OverlapSphereNonAlloc(pos, 0.45f, hits, StageDirector.I.EnemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            if (hits[i].TryGetComponent(out Enemy e) && e.isActiveAndEnabled)
            {
                HitEnemy?.Invoke(e, pos, damage);
                if (e.isActiveAndEnabled) e.Hit(damage, pos - velocity);
                Despawn();
                return;
            }
            if (hits[i].TryGetComponent(out StageTarget st) && st.Alive) // 부술 수 있는 오브젝트 (무덤 등)
            {
                st.Hit(damage, pos - velocity);
                Despawn();
                return;
            }
        }
        if ((life -= dt) <= 0f) Despawn();
    }
}

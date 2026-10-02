using UnityEngine;

public class XpGem : Ticked<XpGem>
{
    public int value = 1;
    bool magnet;

    protected override void OnEnable() { base.OnEnable(); magnet = false; }

    public override void Tick(float dt)
    {
        Vector3 pos = transform.position;
        Vector3 to = PlayerController.I.Position - pos;
        to.y = 0f;
        float sq = to.sqrMagnitude;
        float r = GameManager.I.Stats.pickupRadius;
        if (!magnet && sq < r * r) magnet = true;
        if (!magnet) return;
        if (sq < 0.36f)
        {
            GameManager.I.AddXp(value);
            Despawn();
            return;
        }
        transform.position = pos + to * (16f * dt / Mathf.Sqrt(sq));
    }
}

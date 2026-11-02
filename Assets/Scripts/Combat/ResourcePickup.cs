using UnityEngine;

/// <summary>적이 떨어뜨린 맵 자원. 조준점(형태 없는 플레이어)의 획득 범위에 들어오면 끌려와서 획득.</summary>
public class ResourcePickup : Ticked<ResourcePickup>
{
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock mpb;
    [SerializeField] Renderer[] tint;
    ResourceType type;
    bool magnet;
    float life, sparkle;
    Color color;

    public void Init(ResourceType t, Color color)
    {
        type = t; magnet = false; life = 40f; this.color = color;
        Vfx.Pillar(transform.position, color); // 드롭 알림
        mpb ??= new MaterialPropertyBlock();
        mpb.SetColor(ColorId, color);
        foreach (var r in tint) r.SetPropertyBlock(mpb);
    }

    public override void Tick(float dt)
    {
        transform.Rotate(0f, 120f * dt, 0f, Space.World);
        if ((life -= dt) <= 0f) { Despawn(); return; }
        Vector3 pos = transform.position;
        if ((sparkle -= dt) <= 0f) { sparkle = 0.4f; Vfx.Pickup(pos, color, 1); }
        Vector3 to = PlayerController.I.Position - pos; to.y = 0f;
        float sq = to.sqrMagnitude, r = GameManager.I.Stats.pickupRadius + 1f;
        if (!magnet && sq < r * r) magnet = true;
        if (!magnet) return;
        if (sq < 0.36f)
        {
            SaveData.Add(type, 1);
            Vfx.Pickup(pos, color, 12);
            var def = GameManager.I.Config.meta.Resource(type);
            if (Hud.I) Hud.I.ShowResource(def != null ? def.name : type.ToString());
            Despawn();
            return;
        }
        transform.position = pos + to * (14f * dt / Mathf.Sqrt(sq));
    }
}

using UnityEngine;

/// <summary>기본 도형을 크기 보간하는 단순 이펙트 (텔레그래프/광역/낙뢰).</summary>
public class Fx : Ticked<Fx>
{
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock mpb;
    Renderer rend;
    Vector3 from, to;
    float t, duration;

    void Awake() { rend = GetComponent<Renderer>(); }

    public void Play(Vector3 fromScale, Vector3 toScale, float duration, Color color)
    {
        from = fromScale; to = toScale; this.duration = duration; t = 0f;
        transform.localScale = fromScale;
        mpb ??= new MaterialPropertyBlock();
        mpb.SetColor(ColorId, color);
        rend.SetPropertyBlock(mpb);
    }

    public override void Tick(float dt)
    {
        t += dt;
        transform.localScale = Vector3.LerpUnclamped(from, to, t / duration);
        if (t >= duration) Despawn();
    }
}

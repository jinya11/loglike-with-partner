using UnityEngine;

/// <summary>기본 도형을 크기 보간하는 단순 이펙트 (텔레그래프/광역/낙뢰). 끝 40% 구간에서 투명해짐.</summary>
public class Fx : Ticked<Fx>
{
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock mpb;
    Renderer rend;
    Vector3 from, to;
    Color color;
    float t, duration;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    public void Play(Vector3 fromScale, Vector3 toScale, float duration, Color color)
    {
        from = fromScale; to = toScale; this.duration = duration; t = 0f;
        transform.localScale = fromScale;
        this.color = color;
        SetColor(color);
    }

    void SetColor(Color c)
    {
        mpb ??= new MaterialPropertyBlock();
        mpb.SetColor(ColorId, c);
        rend.SetPropertyBlock(mpb);
    }

    public override void Tick(float dt)
    {
        t += dt;
        float k = t / duration;
        transform.localScale = Vector3.LerpUnclamped(from, to, k);
        if (k > 0.6f) { Color c = color; c.a *= (1f - k) * 2.5f; SetColor(c); }
        if (t >= duration) Despawn();
    }
}

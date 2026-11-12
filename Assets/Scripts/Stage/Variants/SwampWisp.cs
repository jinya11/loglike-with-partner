using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도깨비불 늪의 도깨비불 하나 (씬 배치, 프리팹 SwampWisp). 놓인 자리(home) 둘레를 천천히 떠다님.
/// 닿으면 순간이동시키는 규칙은 SwampWispVariant. 씬에서 옮기거나 복제하면 그 자리를 중심으로 떠다님.
/// </summary>
public class SwampWisp : MonoBehaviour
{
    public static readonly List<SwampWisp> All = new List<SwampWisp>();

    [Tooltip("불빛 몸체 (꺼질 때 작아짐)")] public Transform glow;
    [Tooltip("주변을 비추는 빛")] public Light lightSource;
    [Tooltip("이 도깨비불만의 떠다니는 반경(m). 0이면 VariantConfig 값")] public float wanderRadius;

    [System.NonSerialized] public Vector3 home;
    [System.NonSerialized] public float cooldown;
    float a, b, pa, pb, baseLight;
    Vector3 glowScale = Vector3.one;

    /// <summary>켜져 있어서 닿을 수 있는지.</summary>
    public bool Active => cooldown <= 0f;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Begin(float seed)
    {
        home = transform.position;
        var r = new System.Random(Mathf.RoundToInt(seed * 1000f) + 17);
        a = 0.55f + (float)r.NextDouble() * 0.5f; b = 0.4f + (float)r.NextDouble() * 0.5f;
        pa = (float)r.NextDouble() * 6.28f; pb = (float)r.NextDouble() * 6.28f;
        if (glow) glowScale = glow.localScale;
        baseLight = lightSource ? lightSource.intensity : 0f;
    }

    /// <summary>떠다니기 (t = 경과 시간, speed = m/초 정도의 속도, radius = 반경).</summary>
    public void Drift(float t, float speed, float radius, float hover, float dt)
    {
        float r = wanderRadius > 0f ? wanderRadius : radius;
        float w = speed / Mathf.Max(0.5f, r); // 각속도
        Vector3 p = home + new Vector3(Mathf.Sin(t * w * a + pa), 0f, Mathf.Sin(t * w * b + pb)) * r;
        p.y = Ground.HeightAt(p) + hover + Mathf.Sin(t * 2.1f + pa) * 0.18f;
        transform.position = p;
        if (cooldown > 0f) cooldown -= dt;
        // 꺼져 있는 동안 작고 어둡게, 다시 켜질 때 부풀어 오름
        float k = cooldown > 0f ? 0.15f : 1f;
        if (glow) glow.localScale = Vector3.Lerp(glow.localScale, glowScale * k * (1f + Mathf.Sin(t * 5f + pb) * 0.08f), 1f - Mathf.Exp(-8f * dt));
        if (lightSource) lightSource.intensity = Mathf.Lerp(lightSource.intensity, baseLight * k, 1f - Mathf.Exp(-6f * dt));
    }
}

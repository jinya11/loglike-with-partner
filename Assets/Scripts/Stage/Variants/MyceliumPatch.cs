using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발광 균사 동굴의 균사 덩어리 하나 (씬 배치, 프리팹 MyceliumPatch): 땅에 뻗은 빛나는 실 가닥 + 실제 빛(Point Light) + 떠오르는 포자 파티클.
/// 생명체가 밟으면(규칙: MyceliumCaveVariant) 빛이 확 번지며(빛 범위가 넓어짐 + 바깥으로 퍼지는 포자 파티클 고리) 한동안 어두워졌다가 다시 차오름.
/// </summary>
public class MyceliumPatch : MonoBehaviour
{
    public static readonly List<MyceliumPatch> All = new List<MyceliumPatch>();

    [Tooltip("빛나는 가닥들")] public Renderer[] strands;
    [Tooltip("빛날 때 / 어두울 때 재질")] public Material glowMaterial, dimMaterial;
    [Tooltip("균사가 내는 빛")] public Light glowLight;
    [Tooltip("평소 떠오르는 포자")] public ParticleSystem spores;
    [Tooltip("밟았을 때 바깥으로 퍼지는 포자 고리")] public ParticleSystem burst;
    [Tooltip("이 덩어리만의 반경(m). 0이면 VariantConfig 값")] public float radius;

    [System.NonSerialized] public float cooldown;
    bool lit = true;
    float idleI, idleR, burstI, spreadR, spreadT, burstAge = -1f, phase;

    public bool Ready => cooldown <= 0f;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Setup(float idleIntensity, float idleRange, float burstIntensity)
    {
        idleI = idleIntensity; idleR = idleRange; burstI = burstIntensity;
        phase = Random.value * 6.28f;
        if (glowLight) { glowLight.intensity = idleI; glowLight.range = idleR; }
    }

    /// <summary>밟힘: 빛이 spreadRadius까지 spreadTime 동안 번짐.</summary>
    public void Trigger(float spreadRadius, float spreadTime)
    {
        spreadR = spreadRadius; spreadT = Mathf.Max(0.05f, spreadTime); burstAge = 0f;
        SetLit(false);
        if (burst)
        {
            var main = burst.main;
            main.startSpeed = spreadRadius / spreadT;     // 빛이 번지는 속도로 바깥으로
            main.startLifetime = spreadT;
            burst.Play(true);
        }
        if (spores) spores.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Update()
    {
        if (!glowLight) return;
        float dt = Time.deltaTime;
        if (burstAge >= 0f)
        {
            // 번지는 빛: 범위가 바깥으로 커지며 밝게 → 서서히 사그라듦
            burstAge += dt;
            float k = Mathf.Clamp01(burstAge / spreadT);
            glowLight.range = Mathf.Lerp(idleR, spreadR * 1.15f, Mathf.Sqrt(k));
            glowLight.intensity = burstI * (1f - k * k);
            if (burstAge >= spreadT) { burstAge = -1f; glowLight.intensity = 0.15f; glowLight.range = idleR; }
            return;
        }
        float target = cooldown > 0f ? 0.15f : idleI * (0.85f + 0.15f * Mathf.Sin(Time.time * 1.7f + phase)); // 평소엔 숨 쉬듯 은은하게
        glowLight.intensity = Mathf.MoveTowards(glowLight.intensity, target, dt * 2f);
    }

    public void SetLit(bool on)
    {
        if (lit == on) return;
        lit = on;
        if (on && spores) spores.Play(true);
        var m = on ? glowMaterial : dimMaterial;
        if (!m || strands == null) return;
        foreach (var r in strands) if (r) r.sharedMaterial = m;
    }
}

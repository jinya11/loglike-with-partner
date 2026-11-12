using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발광 균사 동굴의 균사 덩어리 하나 (씬 배치, 프리팹 MyceliumPatch): 땅에 뻗은 빛나는 실 가닥들.
/// 생명체가 밟으면 빛이 번져 나가고(규칙: MyceliumCaveVariant), 한동안 어두워졌다가 다시 차오름.
/// </summary>
public class MyceliumPatch : MonoBehaviour
{
    public static readonly List<MyceliumPatch> All = new List<MyceliumPatch>();

    [Tooltip("빛나는 가닥들")] public Renderer[] strands;
    [Tooltip("빛날 때 / 어두울 때 재질")] public Material glowMaterial, dimMaterial;
    [Tooltip("이 덩어리만의 반경(m). 0이면 VariantConfig 값")] public float radius;

    [System.NonSerialized] public float cooldown;
    bool lit = true;

    public bool Ready => cooldown <= 0f;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void SetLit(bool on)
    {
        if (lit == on || strands == null) return;
        lit = on;
        var m = on ? glowMaterial : dimMaterial;
        if (!m) return;
        foreach (var r in strands) if (r) r.sharedMaterial = m;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>감시자 스킬이 잠깐 만들어 쓰는 간단한 소품 (디코이 장치, 표시 고리, 가시). 무광 파스텔 클레이(URP Lit, metallic 0 / smoothness 0.12) + 발광 표시는 Unlit.</summary>
public static class SkillProps
{
    static readonly Dictionary<Color, Material> clay = new Dictionary<Color, Material>(), glow = new Dictionary<Color, Material>();

    static Material Clay(Color c)
    {
        if (clay.TryGetValue(c, out var m) && m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.12f);
        return clay[c] = m;
    }

    static Material Glow(Color c)
    {
        if (glow.TryGetValue(c, out var m) && m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c);
        return glow[c] = m;
    }

    static Transform Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(t);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        if (rot.HasValue) go.transform.localRotation = rot.Value;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    /// <summary>디코이 장치: 기둥 + 빛나는 머리 + 유도 범위 고리.</summary>
    public static GameObject Decoy(Vector3 p, float radius)
    {
        var root = new GameObject("Decoy").transform;
        root.position = p;
        Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.6f, 0f), new Vector3(0.35f, 0.6f, 0.35f), Clay(new Color(0.7f, 0.62f, 0.55f)));
        Part(PrimitiveType.Sphere, root, new Vector3(0f, 1.4f, 0f), Vector3.one * 0.6f, Glow(new Color(1f, 0.85f, 0.45f))).name = "Head";
        Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.04f, 0f), new Vector3(radius * 2f, 0.01f, radius * 2f), Glow(new Color(1f, 0.85f, 0.45f, 1f) * 0.35f)).name = "Range";
        return root.gameObject;
    }

    /// <summary>디코이 머리 깜빡임 (작동 전엔 작게).</summary>
    public static void PulseDecoy(GameObject go, bool armed)
    {
        var head = go.transform.Find("Head");
        if (head) head.localScale = Vector3.one * (armed ? 0.6f + Mathf.Sin(Time.time * 8f) * 0.08f : 0.35f);
    }

    /// <summary>바닥 범위 고리 (폭격 예고).</summary>
    public static GameObject Ring(Vector3 p, float radius, Color c)
    {
        var root = new GameObject("Ring").transform;
        root.position = p;
        Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.05f, 0f), new Vector3(radius * 2f, 0.01f, radius * 2f), Glow(c * 0.6f));
        return root.gameObject;
    }

    /// <summary>가시 덤불: 바닥에서 솟은 뾰족한 가시 여러 개.</summary>
    public static GameObject Spikes(Vector3 p, float radius)
    {
        var root = new GameObject("Spikes").transform;
        root.position = p;
        var m = Clay(new Color(0.5f, 0.72f, 0.5f));
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f, d = i == 0 ? 0f : radius * 0.55f;
            var pos = new Vector3(Mathf.Cos(a) * d, 0.35f, Mathf.Sin(a) * d);
            Part(PrimitiveType.Cube, root, pos, new Vector3(0.18f, 0.7f, 0.18f), m, Quaternion.Euler(Random.Range(-15f, 15f), i * 40f, Random.Range(-15f, 15f) + 45f * 0f));
        }
        Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.02f, 0f), new Vector3(radius * 2f, 0.01f, radius * 2f), Clay(new Color(0.42f, 0.55f, 0.4f)));
        return root.gameObject;
    }
}

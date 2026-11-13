using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발광 균사 동굴의 회수 물품 "포자 결정" (씬 배치, 프리팹 SporeCrystal). 생명체가 가까이 가면 회수됨 (규칙: MyceliumCaveVariant).
/// 천천히 돌고 떠오르며 빛남 — 어두운 동굴에서 멀리서도 보이게.
/// </summary>
public class SporeCrystal : MonoBehaviour
{
    public static readonly List<SporeCrystal> All = new List<SporeCrystal>();

    [Tooltip("돌고 떠오르는 몸체")] public Transform body;

    [System.NonSerialized] public bool collected;
    Vector3 bodyBase;
    float phase;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    void Awake() { if (body) bodyBase = body.localPosition; phase = Random.value * 6.28f; }

    void Update()
    {
        if (!body || collected) return;
        body.localPosition = bodyBase + Vector3.up * (Mathf.Sin(Time.time * 2f + phase) * 0.15f);
        body.Rotate(0f, 50f * Time.deltaTime, 0f, Space.World);
    }

    public void Collect()
    {
        collected = true;
        gameObject.SetActive(false);
    }
}

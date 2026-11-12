using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 깨어난 묘지의 무덤 하나 (씬 배치, 프리팹 GraveMound). 부술 수 있음(StageTarget). 부서지기 전까지 적이 계속 일어남.
/// 맞을수록 비석이 기울고, 부서지면 비석이 쓰러진 잔해로 바뀜. 규칙(적이 일어나는 간격, 체력)은 AwakenedGraveyardVariant.
/// </summary>
[RequireComponent(typeof(StageTarget))]
public class GraveMound : MonoBehaviour
{
    public static readonly List<GraveMound> All = new List<GraveMound>();

    [Tooltip("비석 (맞을수록 기울어짐)")] public Transform stone;
    [Tooltip("멀쩡할 때 보이는 것 (비석+흙무덤)")] public GameObject intact;
    [Tooltip("부서진 뒤 보이는 잔해")] public GameObject rubble;

    public StageTarget Target { get; private set; }
    public bool Broken => Target && !Target.Alive;
    [System.NonSerialized] public float riseTimer;
    Quaternion stoneBase;
    float tilt;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake()
    {
        Target = GetComponent<StageTarget>();
        if (stone) stoneBase = stone.localRotation;
        if (rubble) rubble.SetActive(false);
        Target.Damaged += (_, frac) => tilt = (1f - frac) * 22f; // 맞을수록 기울어짐
    }

    void Update()
    {
        if (!stone || Broken) return;
        stone.localRotation = Quaternion.Slerp(stone.localRotation, stoneBase * Quaternion.Euler(tilt, 0f, tilt * 0.4f), 1f - Mathf.Exp(-10f * Time.deltaTime));
    }

    /// <summary>부서진 모습으로.</summary>
    public void ShowBroken()
    {
        if (intact) intact.SetActive(false);
        if (rubble) rubble.SetActive(true);
    }
}

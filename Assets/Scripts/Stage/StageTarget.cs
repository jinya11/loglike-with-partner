using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적이 아닌데 공격으로 부술 수 있는 스테이지 오브젝트 (깨어난 묘지의 무덤 등).
/// 생명체 자동 공격의 대상이 되고(가장 가까운 적과 함께 후보), 탄환/범위 피해(DamageArea 등)를 받음. 처치 수에는 안 침.
/// 충돌체는 Enemy 레이어 트리거 (탄환/범위 판정에 걸리게). 체력은 규칙 컴포넌트가 Init으로 정함.
/// </summary>
public class StageTarget : MonoBehaviour
{
    public static readonly List<StageTarget> All = new List<StageTarget>();

    [Tooltip("조준 높이 (탄환이 맞는 위치)")] public float aimHeight = 0.6f;
    [Tooltip("맞을 때 튀는 색")] public Color hitColor = new Color(0.75f, 0.72f, 0.68f);

    public float Hp { get; private set; }
    public float MaxHp { get; private set; }
    public bool Alive => Hp > 0f;
    public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
    /// <summary>부서졌을 때 (규칙 컴포넌트가 구독).</summary>
    public event System.Action<StageTarget> Broken;
    /// <summary>맞았을 때 (남은 체력 비율 0~1).</summary>
    public event System.Action<StageTarget, float> Damaged;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Init(float hp) { MaxHp = Hp = Mathf.Max(1f, hp); }

    /// <summary>수평 크기(반경). 범위 피해 판정에 더함 (가장자리에 떨어진 범위 공격도 맞게).</summary>
    public float Radius
    {
        get
        {
            if (radius < 0f) { radius = 0f; foreach (var c in GetComponentsInChildren<Collider>()) radius = Mathf.Max(radius, c.bounds.extents.x, c.bounds.extents.z); }
            return radius;
        }
    }
    float radius = -1f;

    public void Hit(float dmg, Vector3 from)
    {
        if (!Alive || dmg <= 0f) return;
        Hp -= dmg;
        Vfx.Hit(AimPoint, hitColor);
        Damaged?.Invoke(this, Mathf.Clamp01(Hp / MaxHp));
        if (Hp <= 0f) { Hp = 0f; Broken?.Invoke(this); }
    }
}

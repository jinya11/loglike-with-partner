using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 밧줄(덩굴 사다리) 타기 지점 — 독립 기능 (씬 배치, 프리팹 ClimbSpot). 다른 스테이지에서 재사용하려고 남겨 둔 것.
/// 아래쪽 표시(이 위치) + 덩굴 사다리 + 위쪽 표시(upperMark). 감시자가 가까이 있으면 상호작용 키(F)가 뜨고,
/// 누르면 Used 이벤트가 발생 — 실제로 무엇을 할지(생명체를 다른 높이로 옮기기 등)는 그 스테이지의 규칙 컴포넌트가 구독해서 정함.
/// </summary>
public class ClimbSpot : Interactable
{
    public static readonly List<ClimbSpot> Spots = new List<ClimbSpot>();
    /// <summary>상호작용(F)으로 사용됨. 규칙 컴포넌트가 구독.</summary>
    public static event System.Action<ClimbSpot> Used;

    [Tooltip("위쪽 표시 (사다리 끝 고리)")] public Transform upperMark;
    [Tooltip("덩굴 사다리 (아래에서 위쪽 끝까지 늘어남)")] public Transform vine;
    [Tooltip("생명체가 이 거리 안에 있어야 사용할 수 있음(m)")] public float useRange = 3f;

    protected override void OnEnable() { base.OnEnable(); Spots.Add(this); }
    protected override void OnDisable() { base.OnDisable(); Spots.Remove(this); }

    public override string Prompt => Used != null ? "F" : "";
    public override void Interact() => Used?.Invoke(this);

    /// <summary>수평 위치 (y = 0).</summary>
    public Vector3 Point => new Vector3(transform.position.x, 0f, transform.position.z);

    /// <summary>생명체가 사용할 수 있는 거리 안인지.</summary>
    public bool InRange(Vector3 p) { Vector3 d = Point - p; d.y = 0f; return d.sqrMagnitude <= useRange * useRange; }

    /// <summary>위쪽 표시와 덩굴을 topY 높이에 맞춤.</summary>
    public void FitHeight(float topY)
    {
        float g = transform.position.y, h = Mathf.Max(0.5f, topY - g);
        if (upperMark) upperMark.position = new Vector3(transform.position.x, topY + 0.05f, transform.position.z);
        if (vine) { vine.localScale = new Vector3(vine.localScale.x, h * 0.5f, vine.localScale.z); var v = vine.localPosition; v.y = h * 0.5f; vine.localPosition = v; }
    }
}

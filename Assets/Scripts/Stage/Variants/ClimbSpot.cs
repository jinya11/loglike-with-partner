using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 거대 버섯 숲의 오르내리는 곳 (씬 배치, 프리팹 ClimbSpot — 버섯 줄기 옆 덩굴 사다리).
/// 아래층은 이 위치의 지면, 위층은 같은 자리의 갓 위. 상호작용 키(F, 감시자가 가까이 있을 때)로 생명체가 층을 옮김.
/// 적도 다른 층의 생명체를 쫓을 때 가장 가까운 이곳으로 와서 오르내림. 규칙은 GiantMushroomVariant.
/// </summary>
public class ClimbSpot : Interactable
{
    public static readonly List<ClimbSpot> Spots = new List<ClimbSpot>();

    [Tooltip("위층 쪽 표시 (갓 위 고리, 높이를 맞춤)")] public Transform upperMark;
    [Tooltip("덩굴 사다리 (땅에서 갓까지 늘어남)")] public Transform vine;

    /// <summary>위층 표시와 덩굴을 갓 높이에 맞춤.</summary>
    public void FitHeight(float capY)
    {
        float g = transform.position.y, h = Mathf.Max(0.5f, capY - g);
        if (upperMark) upperMark.position = new Vector3(transform.position.x, capY + 0.05f, transform.position.z);
        if (vine) { vine.localScale = new Vector3(vine.localScale.x, h * 0.5f, vine.localScale.z); var v = vine.localPosition; v.y = h * 0.5f; vine.localPosition = v; }
    }

    [System.NonSerialized] public GiantMushroomVariant rule;

    protected override void OnEnable() { base.OnEnable(); Spots.Add(this); }
    protected override void OnDisable() { base.OnDisable(); Spots.Remove(this); }

    public override string Prompt => "F";
    public override void Interact() { if (rule) rule.TryClimb(this); }

    public Vector3 Point => new Vector3(transform.position.x, 0f, transform.position.z);
}

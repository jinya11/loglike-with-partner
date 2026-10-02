using System.Collections.Generic;
using UnityEngine;

/// <summary>F 키 상호작용 대상 공용 베이스. 플레이어가 가장 가까운 대상을 찾아 작동 (로비/전투 공통).</summary>
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>(16);

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    public abstract string Prompt { get; }
    public abstract void Interact();
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상류의 댐의 수문 장치 하나 (씬 배치, 프리팹 SluiceGate). 적이 붙어서 부수려 하고, 다 차오르면 감시자가 상호작용(F)으로 열 수 있음.
/// 열면 물살이 아래쪽으로 쏟아져 적을 쓸어 감 (규칙: RiverDamVariant).
/// </summary>
public class SluiceGate : Interactable
{
    public static readonly List<SluiceGate> Gates = new List<SluiceGate>();

    [Tooltip("수문 문짝 (열릴 때 올라감)")] public Transform door;
    [Tooltip("차오름 표시 (바닥 고리, 차오를수록 커짐)")] public Transform chargeMark;
    [Tooltip("차오름 표시 렌더러 (다 차오르면 밝은 색)")] public Renderer chargeRenderer;
    [Tooltip("체력 막대 (가로 크기)")] public Transform hpBar;
    [Tooltip("부서졌을 때 보이는 것")] public GameObject brokenLook;
    [Tooltip("멀쩡할 때 보이는 것")] public GameObject intactLook;
    [Tooltip("다 차오름 / 차오르는 중 재질")] public Material readyMaterial, chargingMaterial;

    [System.NonSerialized] public RiverDamVariant rule;
    [System.NonSerialized] public float hp, maxHp, charge, openLeft;
    Vector3 doorBase, markBase, barBase;
    bool init;

    public bool Broken => init && hp <= 0f;
    public bool Ready => !Broken && charge >= 1f && openLeft <= 0f;

    protected override void OnEnable() { base.OnEnable(); Gates.Add(this); }
    protected override void OnDisable() { base.OnDisable(); Gates.Remove(this); }

    public override string Prompt => Ready ? "F" : "";
    public override void Interact() { if (rule && Ready) rule.Open(this); }

    public void Init(float hpMax)
    {
        maxHp = hp = hpMax; charge = 0f; openLeft = 0f; init = true;
        if (door) doorBase = door.localPosition;
        if (chargeMark) markBase = chargeMark.localScale;
        if (hpBar) barBase = hpBar.localScale;
        if (brokenLook) brokenLook.SetActive(false);
        Refresh();
    }

    /// <summary>문짝 높이/차오름 고리/체력 막대 갱신.</summary>
    public void Refresh()
    {
        if (door) door.localPosition = doorBase + Vector3.up * (openLeft > 0f ? 2.6f : 0f);
        if (chargeMark)
        {
            float k = Broken ? 0f : Mathf.Clamp01(charge);
            chargeMark.localScale = new Vector3(markBase.x * (0.25f + 0.75f * k), markBase.y, markBase.z * (0.25f + 0.75f * k));
            if (chargeRenderer) { var m = Ready ? readyMaterial : chargingMaterial; if (m && chargeRenderer.sharedMaterial != m) chargeRenderer.sharedMaterial = m; }
        }
        if (hpBar) hpBar.localScale = new Vector3(barBase.x * Mathf.Clamp01(maxHp > 0f ? hp / maxHp : 0f), barBase.y, barBase.z);
    }

    public void ShowBroken()
    {
        if (intactLook) intactLook.SetActive(false);
        if (brokenLook) brokenLook.SetActive(true);
        if (hpBar) hpBar.gameObject.SetActive(false);
        Refresh();
    }
}

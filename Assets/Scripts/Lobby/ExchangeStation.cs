using UnityEngine;

/// <summary>로비 교환소 (Meshy 과일 노점 모델). F로 교환 창(HUD_Exchange)을 엶: 거점 재화 → 성장의 씨앗.</summary>
public class ExchangeStation : Interactable
{
    [SerializeField] string prompt = "[F] 교환소";

    public override string Prompt => prompt;
    public override void Interact() => ExchangeUI.Open();
}

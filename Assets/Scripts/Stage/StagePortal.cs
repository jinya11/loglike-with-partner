/// <summary>처치 목표 달성 후 열리는 전투 스테이지 포털. F로 다음 스테이지 이동.</summary>
public class StagePortal : Interactable
{
    public override string Prompt => "[F] 다음 스테이지로 이동";
    public override void Interact() => GameManager.I.NextStage();
}

using UnityEngine;

/// <summary>
/// 파생 스테이지 전용 씬 표시. 이 씬을 에디터에서 직접 열고 Play해도 바로 그 파생형으로 시작 (QA용: 로비/포털을 거치지 않음).
/// StageDirector가 Awake에서 읽음. variantId = VariantConfig.stageVariants의 id.
/// </summary>
public class VariantStage : MonoBehaviour
{
    [Tooltip("VariantConfig.stageVariants의 id (예: forest_firefly)")] public string variantId;
}


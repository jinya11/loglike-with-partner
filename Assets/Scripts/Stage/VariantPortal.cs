using TMPro;
using UnityEngine;

/// <summary>
/// 파생 포털: 조건 증강을 보유한 상태로 포털이 열리면 일반 포털 옆에 하나 더 열림. 색과 이름으로 일반 포털과 구분.
/// 감시자가 다가가 F → 그 파생 스테이지로 (증강 카드 선택 후). 감시자가 가까이 있으면 생명체도 이쪽으로 걸어감 (Ward 참고).
/// </summary>
public class VariantPortal : Interactable
{
    public StageVariantDef Def { get; private set; }
    static readonly int ColorId = Shader.PropertyToID("_BaseColor"), EmissionId = Shader.PropertyToID("_EmissionColor");

    public override string Prompt => Def != null ? $"[F] 파생 스테이지로: {Def.name}" : "";
    public override void Interact() { if (Def != null) GameManager.I.RequestNextStage(Def); }

    public void Init(StageVariantDef def)
    {
        Def = def;
        // 색 구분
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(ColorId, def.color);
        mpb.SetColor(EmissionId, def.color * 2f);
        foreach (var r in GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mpb);
        // 이름 표시 (월드 공간, 카메라를 향함)
        var label = new GameObject("VariantLabel");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(0f, 4.2f, 0f);
        var t = label.AddComponent<TextMeshPro>();
        var hudFont = Hud.I ? Hud.I.GetComponentInChildren<TMP_Text>(true) : null;
        if (hudFont) t.font = hudFont.font;
        t.text = $"<size=70%>파생</size>\n{def.name}";
        t.fontSize = 6f; t.alignment = TextAlignmentOptions.Center; t.color = def.color;
        t.outlineWidth = 0.2f; t.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        t.rectTransform.sizeDelta = new Vector2(12f, 4f);
        label.AddComponent<Billboard>();
        Vfx.Pillar(transform.position, def.color);
    }
}

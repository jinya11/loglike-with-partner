using UnityEngine;

/// <summary>
/// 감시자 스킬 장착대 (보너스 스테이지 = 설탕 집에만). 인게임에서는 스킬을 바꿀 수 없고 여기서만 바꿈 (로비와 같은 장착 창).
/// 감시자가 가까이 가서 상호작용(F). StageDirector가 보너스 스테이지에서 생명체 시작 지점 옆에 만듦.
/// </summary>
public class SkillStation : Interactable
{
    public override string Prompt => "F";
    public override void Interact() => SkillSelectUI.Toggle();

    /// <summary>받침 + 빛나는 구슬 (무광 파스텔).</summary>
    public static SkillStation Spawn(Vector3 p)
    {
        var root = new GameObject("SkillStation");
        root.transform.position = Ground.Snap(p);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var stone = new Material(lit); stone.SetColor("_BaseColor", new Color(0.8f, 0.72f, 0.85f)); stone.SetFloat("_Metallic", 0f); stone.SetFloat("_Smoothness", 0.12f);
        var orb = new Material(Shader.Find("Universal Render Pipeline/Unlit")); orb.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f));
        Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.45f, 0.9f), stone);
        Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.3f, 0f), Vector3.one * 0.55f, orb);
        return root.AddComponent<SkillStation>();
    }

    static void Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }
}
